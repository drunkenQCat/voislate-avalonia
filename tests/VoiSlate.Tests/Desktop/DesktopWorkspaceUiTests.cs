using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VoiSlate.Desktop;
using VoiSlate.Desktop.Speech;
using VoiSlate.Services;
using Xunit;

namespace VoiSlate.Tests.Desktop;

public sealed partial class DesktopWorkspaceTests
{
    private static T Control<T>(Visual root, string automationId) where T : Avalonia.Controls.Control { Dispatcher.UIThread.RunJobs(); return root.GetVisualDescendants().OfType<T>().Single(c => AutomationProperties.GetAutomationId(c) == automationId); }
    private static Button Button(Visual root, string label) => root.GetVisualDescendants().OfType<Button>().Single(b => (b.Content as string)?.Replace("  ", " ") == label);
    private static void Invoke(Button button) { button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent)); Dispatcher.UIThread.RunJobs(); }
    private static void Key(Window window, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
    { window.KeyPress(key, modifiers, physical, null); window.KeyRelease(key, modifiers, physical, null); }
    private static async Task RunUi(Func<Task> work)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(WorkspaceTestAppBuilder));
        // Select the async generic overload; Dispatch(Func<Task>) would return a nested task.
        await session.Dispatch<bool>(async () => { await work(); return true; }, CancellationToken.None);
    }
    private static async Task Until(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(3);
        while (!condition()) { if (DateTime.UtcNow > until) throw new TimeoutException("UI did not reach expected state"); await Task.Delay(10); }
        Dispatcher.UIThread.RunJobs();
    }
    private static void CaptureFrame(Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("VOISLATE_RENDER_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory); using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame); frame.Save(Path.Combine(directory, name + ".png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }
    private sealed class UiCapture : IPcmCapture
    {
        private readonly Channel<byte[]> _audio = Channel.CreateUnbounded<byte[]>();
        public bool Disposed, Stopped, Started;
        public void Start() { Started = true; _audio.Writer.TryWrite(new byte[] { 0, 1 }); }
        public Task StopAsync() { Stopped = true; _audio.Writer.TryComplete(); return Task.CompletedTask; }
        public IAsyncEnumerable<byte[]> ReadAsync(CancellationToken ct) => _audio.Reader.ReadAllAsync(ct);
        public void Dispose() { Disposed = true; _audio.Writer.TryComplete(); }
    }
    private sealed class UiTransport : IBailianTransport
    {
        private readonly Channel<string> _events = Channel.CreateUnbounded<string>();
        public bool HoldFinal, Finished, Disposed;
        private string _id = "";
        public Task ConnectAsync(BailianOptions options, CancellationToken ct) => Task.CompletedTask;
        public Task SendTextAsync(string text, CancellationToken ct)
        {
            using var doc = JsonDocument.Parse(text); var h = doc.RootElement.GetProperty("header"); _id = h.GetProperty("task_id").GetString()!;
            if (h.GetProperty("action").GetString() == "run-task") Event("task-started");
            else { Finished = true; if (!HoldFinal) Complete(); }
            return Task.CompletedTask;
        }
        public Task SendAudioAsync(byte[] audio, CancellationToken ct) { Sentence("中间预览", false); return Task.CompletedTask; }
        private void Event(string name) => _events.Writer.TryWrite(JsonSerializer.Serialize(new { header = new { task_id = _id, @event = name }, payload = new { } }));
        private void Sentence(string text, bool final) => _events.Writer.TryWrite(JsonSerializer.Serialize(new { header = new { task_id = _id, @event = "result-generated" }, payload = new { output = new { sentence = new { begin_time = 0, text, sentence_end = final } } } }));
        public void Complete() { Sentence("最终转写。", true); Event("task-finished"); }
        public async IAsyncEnumerable<string> ReceiveAsync([EnumeratorCancellation] CancellationToken ct) { await foreach (var value in _events.Reader.ReadAllAsync(ct)) yield return value; }
        public ValueTask DisposeAsync() { Disposed = true; _events.Writer.TryComplete(); return ValueTask.CompletedTask; }
    }
    private static BailianSettingsStore SpeechSettings(Fixture f) => new(new LiteDbSessionSettingsStore(f.Db), new WindowsTokenProtector());
    private static async Task<DesktopWorkspaceWindow> WindowFor(Fixture f, UiTransport? transport = null, UiCapture? capture = null)
    {
        await f.Workspace.SetBackupPreferencesAsync(false, "");
        var window = new DesktopWorkspaceWindow(f.Workspace, SpeechSettings(f), transport is null ? null : new(() => transport), capture is null ? null : _ => capture);
        window.Show(); Dispatcher.UIThread.RunJobs(); return window;
    }
    [WindowsOnlyFact]
    public Task Ui_token_can_be_saved_masked_and_reopened_without_exposing_it_in_backup() => RunUi(async () =>
    {
        using var f = await Create(); var window = await WindowFor(f);
        try
        {
            Invoke(Button(window, "⚙ 工程与偏好")); var dialog = Assert.Single(window.OwnedWindows);
            var token = dialog.GetVisualDescendants().OfType<TextBox>().Single(c => AutomationProperties.GetName(c) == "百炼 Token");
            Assert.NotEqual('\0', token.PasswordChar); token.Focus(); dialog.KeyTextInput("sk-ui-test-only");
            Invoke(Button(dialog, "保存语音设置")); Assert.Equal("sk-ui-test-only", (await SpeechSettings(f).LoadAsync()).Token);
            Assert.Empty(token.Text ?? ""); Assert.DoesNotContain("sk-ui-test-only", f.Workspace.ExportJson());
            CaptureFrame(dialog, "settings-bailian"); dialog.Close();
            Invoke(Button(window, "⚙ 工程与偏好")); dialog = Assert.Single(window.OwnedWindows);
            Assert.Contains(dialog.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Token 已加密保存") == true);
            dialog.Close();
        }
        finally { window.Close(); }
    });
    [WindowsOnlyFact]
    public Task Ui_voice_previews_then_appends_final_once_to_selected_annotation() => RunUi(async () =>
    {
        using var f = await Create(); await SpeechSettings(f).SaveAsync("sk-ui-test-only", "", 0);
        await f.Workspace.SaveDraftAsync("已有录音", "已有镜头"); var transport = new UiTransport(); var capture = new UiCapture();
        var window = await WindowFor(f, transport, capture);
        try
        {
            Control<Button>(window, "voice-joystick").Focus(); Key(window, Avalonia.Input.Key.Left, PhysicalKey.ArrowLeft);
            await Until(() => window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "中间预览"));
            Assert.Equal("已有录音", Control<TextBox>(window, "recording-note").Text);
            Assert.True(Control<Button>(window, "voice-joystick").IsFocused);
            Key(window, Avalonia.Input.Key.Enter, PhysicalKey.Enter);
            Assert.True(capture.Stopped, string.Join(" | ", window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text)));
            await Until(() => Control<TextBox>(window, "recording-note").Text == "已有录音；最终转写。");
            await Until(() => Control<TextBlock>(window, "voice-preview").Text == "已追加到录音标注");
            Assert.Equal("已有镜头", Control<TextBox>(window, "shot-note").Text); Assert.Equal("已有录音；最终转写。", f.Workspace.Draft.RecordingNote);
            Key(window, Avalonia.Input.Key.Enter, PhysicalKey.Enter); Assert.Equal("已有录音；最终转写。", f.Workspace.Draft.RecordingNote); Assert.True(capture.Disposed);
            CaptureFrame(window, "record-bailian");
        }
        finally { window.Close(); }
    });
    [WindowsOnlyFact]
    public Task Ui_cancel_while_final_is_pending_cannot_write_late_results_into_another_shot() => RunUi(async () =>
    {
        using var f = await Create(); await SpeechSettings(f).SaveAsync("sk-ui-test-only", "", 0);
        var transport = new UiTransport { HoldFinal = true }; var capture = new UiCapture(); var window = await WindowFor(f, transport, capture);
        try
        {
            Control<Button>(window, "voice-joystick").Focus(); Key(window, Avalonia.Input.Key.Right, PhysicalKey.ArrowRight);
            await Until(() => window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "中间预览"));
            Key(window, Avalonia.Input.Key.Enter, PhysicalKey.Enter); await Until(() => transport.Finished);
            Key(window, Avalonia.Input.Key.Escape, PhysicalKey.Escape); await f.Workspace.SelectShotAsync("1A", "2B");
            transport.Complete(); await Until(() => transport.Disposed);
            Assert.Empty(f.Workspace.Draft.RecordingNote); Assert.Empty(f.Workspace.Draft.ShotNote); Assert.True(capture.Disposed);
        }
        finally { window.Close(); }
    });
    [WindowsOnlyTheory]
    [InlineData(-45, "rec")]
    [InlineData(45, "shot")]
    [InlineData(0, "cancel")]
    public Task Ui_pointer_drag_commits_to_selected_side_and_center_release_cancels(int delta, string side) => RunUi(async () =>
    {
        using var f = await Create(); await SpeechSettings(f).SaveAsync("sk-ui-test-only", "", 0);
        await f.Workspace.SaveDraftAsync("原录音", "原镜头");
        var transport = new UiTransport(); var capture = new UiCapture(); var window = await WindowFor(f, transport, capture);
        try
        {
            var knob = Control<Button>(window, "voice-joystick");
            var origin = knob.TranslatePoint(new Point(knob.Bounds.Width / 2, knob.Bounds.Height / 2), window)!.Value;
            window.MouseDown(origin, MouseButton.Left);
            await Until(() => window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "中间预览"));
            var end = origin + new Vector(delta, 0); window.MouseMove(end, RawInputModifiers.LeftMouseButton); window.MouseUp(end, MouseButton.Left);
            await Until(() => capture.Disposed);
            Assert.Equal(side == "rec" ? "原录音；最终转写。" : "原录音", f.Workspace.Draft.RecordingNote);
            Assert.Equal(side == "shot" ? "原镜头；最终转写。" : "原镜头", f.Workspace.Draft.ShotNote);
        }
        finally { window.Close(); }
    });
    [Fact]
    public Task Ui_ctrl_enter_from_annotation_saves_and_respects_pocket_lock() => RunUi(async () =>
    {
        using var f = await Create(); var window = await WindowFor(f);
        try
        {
            Control<TextBox>(window, "recording-note").Focus(); window.KeyTextInput("键盘记录");
            Key(window, Avalonia.Input.Key.Enter, PhysicalKey.Enter, RawInputModifiers.Control);
            await Until(() => f.Workspace.Logs.Count == 1); Assert.Equal("键盘记录", f.Workspace.Logs[0].Item.TkNote);
            await f.Workspace.SetPreferencesAsync("测试", true, false); Control<TextBox>(window, "recording-note").Focus();
            Key(window, Avalonia.Input.Key.Enter, PhysicalKey.Enter, RawInputModifiers.Control);
            Assert.Single(f.Workspace.Logs);
        }
        finally { window.Close(); }
    });
    [Fact]
    public Task Ui_pocket_lock_long_press_saves_exactly_once() => RunUi(async () =>
    {
        using var f = await Create(); await f.Workspace.SetPreferencesAsync("测试", true, false); var window = await WindowFor(f);
        try
        {
            var add = Control<Button>(window, "add-take"); var point = add.TranslatePoint(new Point(add.Bounds.Width / 2, add.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left); await Task.Delay(750); window.MouseUp(point, MouseButton.Left);
            Assert.Single(f.Workspace.Logs); Assert.Equal(2, f.Workspace.State.FileNumber);
        }
        finally { window.Close(); }
    });
    [Fact]
    public Task Ui_pocket_lock_rejects_short_press_and_accepts_keyboard_hold_once() => RunUi(async () =>
    {
        using var f = await Create(); await f.Workspace.SetPreferencesAsync("测试", true, false); var window = await WindowFor(f);
        try
        {
            var add = Control<Button>(window, "add-take"); var point = add.TranslatePoint(new Point(add.Bounds.Width / 2, add.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
            window.MouseDown(point, MouseButton.Right); await Task.Delay(700); window.MouseUp(point, MouseButton.Right);
            Assert.Empty(f.Workspace.Logs);
            add.Focus(); window.KeyPress(Avalonia.Input.Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            await Task.Delay(750); window.KeyRelease(Avalonia.Input.Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            Assert.Single(f.Workspace.Logs);
        }
        finally { window.Close(); }
    });
    [Fact]
    public Task Ui_missing_token_shows_settings_hint_without_starting_microphone_or_changing_notes() => RunUi(async () =>
    {
        using var f = await Create(); await f.Workspace.SaveDraftAsync("保留录音", "保留镜头");
        var capture = new UiCapture(); var window = await WindowFor(f, capture: capture);
        try
        {
            var knob = Control<Button>(window, "voice-joystick"); var point = knob.TranslatePoint(new Point(knob.Bounds.Width / 2, knob.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
            Assert.False(capture.Started); Assert.Contains("填写百炼 Token", Control<TextBlock>(window, "voice-preview").Text);
            Assert.Equal("保留录音", f.Workspace.Draft.RecordingNote); Assert.Equal("保留镜头", f.Workspace.Draft.ShotNote);
        }
        finally { window.Close(); }
    });
    [Fact]
    public Task Ui_window_close_finishes_exit_backup_before_closing() => RunUi(async () =>
    {
        using var f = await Create(); await f.Workspace.SaveTakeAsync(); var window = await WindowFor(f);
        var folder = Path.Combine(Path.GetTempPath(), "VoiSlate-ui-exit-" + Guid.NewGuid().ToString("N"));
        try
        {
            await f.Workspace.SetBackupPreferencesAsync(true, folder); window.Close();
            await Until(() => !window.IsVisible);
            var file = Assert.Single(Directory.GetFiles(folder, "*-exit-*.json"));
            Assert.Equal(1, DesktopWorkspaceService.PrepareImport(await File.ReadAllTextAsync(file), f.Workspace.Today).TakeCount);
        }
        finally
        {
            if (window.IsVisible) { await f.Workspace.SetBackupPreferencesAsync(false, folder); window.Close(); }
            if (Directory.Exists(folder)) { foreach (var file in Directory.GetFiles(folder)) File.Delete(file); Directory.Delete(folder); }
        }
    });
    [Fact]
    public Task Ui_plan_editor_reorders_and_opens_insertion_form() => RunUi(async () =>
    {
        using var f = await Create(); var window = await WindowFor(f);
        try
        {
            Invoke(Control<Button>(window, "nav-plan")); var card = Control<Border>(window, "shot-1A-1A");
            Invoke(Button(card, "•••")); var dialog = Assert.Single(window.OwnedWindows); CaptureFrame(dialog, "plan-edit-actions");
            Invoke(Button(dialog, "下移")); Assert.Equal("1A", f.Workspace.Scenes[0][1].Name);
            card = Control<Border>(window, "shot-1A-1A"); Invoke(Button(card, "•••")); dialog = Assert.Single(window.OwnedWindows);
            Invoke(Button(dialog, "前面插入")); dialog = Assert.Single(window.OwnedWindows);
            var number = dialog.GetVisualDescendants().OfType<TextBox>().Single(t => t.PlaceholderText == "例如 12A");
            number.Focus(); dialog.KeyTextInput("99A"); Invoke(Button(dialog, "保存"));
            Assert.Equal("99A", f.Workspace.Scenes[0][1].Name); Assert.Equal("1A", f.Workspace.Scenes[0][2].Name);
        }
        finally { window.Close(); }
    });
    [Fact]
    public Task Ui_wide_compact_plan_and_timeline_render_with_usable_controls() => RunUi(async () =>
    {
        using var f = await Create(); var window = await WindowFor(f);
        try
        {
            await f.Workspace.SetTracksAsync("Boom，演员甲"); await f.Workspace.SaveDraftAsync("声音干净", "视线接上"); await f.Workspace.SaveTakeAsync();
            CaptureFrame(window, "record-wide"); window.Width = 500; Dispatcher.UIThread.RunJobs();
            CaptureFrame(window, "record-compact"); Assert.True(Control<Button>(window, "add-take").Bounds.Width > 0);
            window.Width = 1120; Dispatcher.UIThread.RunJobs(); Invoke(Control<Button>(window, "nav-plan")); CaptureFrame(window, "plan-alignment");
            Invoke(Control<Button>(window, "nav-log")); CaptureFrame(window, "timeline-alignment");
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Boom") == true);
        }
        finally { window.Close(); }
    });
}
