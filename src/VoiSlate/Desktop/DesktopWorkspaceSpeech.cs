using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using VoiSlate.Desktop.Speech;

namespace VoiSlate.Desktop;

public sealed partial class DesktopWorkspaceWindow
{
    private readonly BailianSettingsStore _speechSettings;
    private readonly BailianRealtimeClient _speechClient;
    private readonly Func<int, IPcmCapture> _captureFactory;
    private Control BuildVoice()
    {
        var preview = T("按住话筒，左滑录音标注 / 右滑镜头标注，松手提交", 11, "#83919C");
        AutomationProperties.SetAutomationId(preview, "voice-preview");
        var joy = Box(new Grid(), "#EAEFF0", 8, "#DFE6E7", 40); joy.Height = 76;
        var track = (Grid)joy.Child!;
        var hints = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        hints.Children.Add(T("≪ 录音", 10, "#8C9AA3")); var right = T("镜头 ≫", 10, "#8C9AA3");
        right.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumn(right, 1); hints.Children.Add(right); track.Children.Add(hints);
        var knob = B("开始语音标注", () => { }, "purple", "voice-joystick");
        Control Mic() => new PathIcon { Data = Geometry.Parse("M12,14C13.66,14 15,12.66 15,11V5C15,3.34 13.66,2 12,2C10.34,2 9,3.34 9,5V11C9,12.66 10.34,14 12,14 M18,11C18,14.53 15.06,17.44 11.53,17.5C8.08,17.56 5,14.93 5,11H3.13C3.13,14.66 6.31,17.66 10,17.9V21H14V17.9C17.69,17.66 20.87,14.66 20.87,11H18"), Width = 25, Height = 25, Foreground = Brushes.White };
        knob.Content = Mic(); knob.Width = 58; knob.Height = 58; knob.CornerRadius = new CornerRadius(29);
        knob.HorizontalAlignment = HorizontalAlignment.Center; knob.Padding = new Thickness(0); knob.Background = Brush("#C6A4D4"); track.Children.Add(knob);
        BailianMicrophoneSession? session = null;
        bool active = false, processing = false; int generation = 0; double start = 0; string? target = null;
        string beforeRec = "", beforeShot = "", scene = "", shot = "";
        var limit = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        void Reset() { active = processing = false; limit.Stop(); knob.RenderTransform = null; knob.Content = Mic(); }
        void Cancel()
        {
            if (!active && !processing && session is null) return;
            generation++; Reset(); var old = session; session = null;
            if (old is not null) { old.Cancel(); _ = old.DisposeAsync(); }
            preview.Text = "已取消；按住话筒重新标注";
        }
        _finishVoice = Cancel;
        async Task Begin()
        {
            if (active || processing || _busy || _service.Draft.Ended || _service.Shot is null) return;
            active = true; var current = ++generation;
            beforeRec = _recNote?.Text ?? ""; beforeShot = _shotNote?.Text ?? "";
            scene = _service.State.Scene; shot = _service.State.Shot;
            preview.Text = "正在连接百炼…"; knob.Content = "■";
            try
            {
                var options = await _speechSettings.LoadAsync(); options.Validate();
                if (current != generation) return;
                session = new BailianMicrophoneSession(options, _speechClient, _captureFactory(options.DeviceNumber), text =>
                    Dispatcher.UIThread.Post(() => { if (current == generation && (active || processing)) preview.Text = text.Length == 0 ? "正在聆听…" : text; }));
                preview.Text = "正在聆听，左滑录音 / 右滑镜头；回中松手取消"; limit.Start();
                var observed = session;
                _ = ObserveFailure(observed, current);
            }
            catch (Exception ex) { if (current == generation) { Cancel(); preview.Text = ex.Message; Toast(ex.Message, true); } }
        }
        async Task ObserveFailure(BailianMicrophoneSession observed, int current)
        {
            try { await observed.Completion; }
            catch (Exception ex) { if (current == generation && active) { Cancel(); preview.Text = ex.Message; Toast(ex.Message, true); } }
        }
        async Task Finish()
        {
            if (!active) return;
            if (target is null || session is null) { Cancel(); return; }
            active = false; processing = true; limit.Stop(); knob.RenderTransform = null; knob.Content = "…";
            var current = generation; var completing = session; var side = target;
            preview.Text = "正在整理识别结果…";
            try
            {
                await completing.StopAudioAsync();
                var text = await completing.Completion;
                if (current != generation) return;
                var box = side == "rec" ? _recNote : _shotNote; var previous = side == "rec" ? beforeRec : beforeShot;
                if (_service.State.Scene != scene || _service.State.Shot != shot || box is null || (box.Text ?? "") != previous)
                    throw new InvalidOperationException("标注内容或镜头已变化，本次转写未写入，请重新标注");
                if (text.Length == 0) { preview.Text = "未识别到语音，请重试"; return; }
                box.Text = previous + (previous.Length > 0 ? "；" : "") + text;
                preview.Text = "已追加到" + (side == "rec" ? "录音标注" : "镜头标注"); Toast(preview.Text);
            }
            catch (Exception ex) { if (current == generation) { preview.Text = ex.Message; Toast(ex.Message, true); } }
            finally { await completing.DisposeAsync(); if (current == generation) { session = null; Reset(); } }
        }
        limit.Tick += async (_, _) => await Finish();
        knob.AddHandler(InputElement.PointerPressedEvent, async (_, e) =>
        {
            if (e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(knob).Properties.IsLeftButtonPressed) return;
            e.Handled = true;
            if (active || processing || _service.Draft.Ended) return;
            knob.Focus(); target = null; start = e.GetPosition(joy).X; e.Pointer.Capture(knob); await Begin();
            if (!active) e.Pointer.Capture(null);
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        knob.AddHandler(InputElement.PointerMovedEvent, (_, e) =>
        {
            if (!active) return; e.Handled = true; var delta = Math.Clamp(e.GetPosition(joy).X - start, -55, 55);
            knob.RenderTransform = new TranslateTransform(delta, 0); target = delta < -24 ? "rec" : delta > 24 ? "shot" : null;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        knob.AddHandler(InputElement.PointerReleasedEvent, async (_, e) => { if (e.Pointer.Type == PointerType.Mouse && e.InitialPressMouseButton != MouseButton.Left) return; e.Handled = true; var task = Finish(); e.Pointer.Capture(null); await task; }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        knob.PointerCaptureLost += (_, _) => { if (active) Cancel(); };
        knob.AddHandler(InputElement.KeyDownEvent, async (_, e) =>
        {
            if (e.KeyModifiers != KeyModifiers.None) return;
            if (e.Key is Key.Left or Key.Right)
            {
                e.Handled = true;
                if (!active && !processing) await Begin();
                target = e.Key == Key.Left ? "rec" : "shot";
                if (active) preview.Text = "正在录制" + (target == "rec" ? "录音标注" : "镜头标注") + "；Enter 提交，Esc 取消";
            }
            else if (e.Key is Key.Enter or Key.Space) { e.Handled = true; if (active) await Finish(); }
            else if (e.Key == Key.Escape && (active || processing)) { e.Handled = true; Cancel(); }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        ToolTip.SetTip(knob, "按住并左右滑动选择标注，松手提交；键盘左右键开始，Enter 提交，Esc 取消");
        return Stack(6, joy, preview);
    }
    private Control BuildSpeechSettings()
    {
        var token = new TextBox { PasswordChar = '●', PlaceholderText = "粘贴北京地域百炼 API Key；留空保留原 Token" };
        AutomationProperties.SetName(token, "百炼 Token");
        var workspace = new TextBox { PlaceholderText = "可选：业务空间 ID" };
        AutomationProperties.SetName(workspace, "百炼业务空间 ID");
        var microphones = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        var status = T("", 12, "#83919C");
        BailianOptions? loaded = null;
        try
        {
            var devices = WindowsPcmCapture.Devices(); microphones.ItemsSource = devices;
            loaded = _speechSettings.LoadAsync().GetAwaiter().GetResult();
            workspace.Text = loaded.WorkspaceId;
            microphones.SelectedIndex = loaded.DeviceNumber < devices.Count ? loaded.DeviceNumber : devices.Count > 0 ? 0 : -1;
            status.Text = (loaded.Token.Length > 0 ? "Token 已加密保存；输入新值可替换。" : "尚未配置 Token。") + (devices.Count == 0 ? "未发现麦克风。" : "");
        }
        catch (Exception ex) { status.Text = ex.Message; }
        var save = B("保存语音设置", async () =>
        {
            try
            {
                await _speechSettings.SaveAsync(token.Text, workspace.Text ?? "", Math.Max(0, microphones.SelectedIndex));
                token.Text = ""; loaded = await _speechSettings.LoadAsync();
                status.Text = loaded.Token.Length > 0 ? "Token 已加密保存，语音设置立即生效。" : "语音设置已保存；请填写 Token 后开始识别。";
            }
            catch (Exception ex) { status.Text = ex.Message; }
        });
        var test = B("测试连接", async () =>
        {
            status.Text = "正在测试连接…";
            try
            {
                var key = string.IsNullOrWhiteSpace(token.Text) ? (await _speechSettings.LoadAsync()).Token : token.Text.Trim();
                if (key.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) key = key[7..].Trim();
                await _speechClient.CheckConnectionAsync(new(key, workspace.Text?.Trim() ?? "", Math.Max(0, microphones.SelectedIndex)));
                status.Text = "连接成功；实际识别还需要模型权限、额度和可用麦克风。";
            }
            catch (Exception ex) { status.Text = ex.Message; }
        });
        var clear = B("移除 Token", async () =>
        {
            try { await _speechSettings.ClearTokenAsync(); token.Text = ""; status.Text = "Token 已移除。"; }
            catch (Exception ex) { status.Text = ex.Message; }
        }, "quiet");
        return Stack(10, T("百炼语音识别", 16, "#45654D", true),
            T("北京地域 · 实时识别；按住话筒时上传音频。Token 仅供本机当前 Windows 用户使用，不包含在 JSON 导出中。", 11),
            T("Token（API Key）"), token, T("业务空间（可选）"), workspace, T("麦克风"), microphones, status, Row(6, save, test, clear));
    }
}
