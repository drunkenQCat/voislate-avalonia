using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using VoiSlate.Desktop.Speech;
using VoiSlate.Infrastructure;
using VoiSlate.Services;
using Xunit;

namespace VoiSlate.Tests.Desktop;

public sealed class BailianSpeechTests
{
    private sealed class Transport : IBailianTransport
    {
        private readonly Channel<string> _events = Channel.CreateUnbounded<string>();
        public readonly List<string> Commands = [];
        public readonly List<byte[]> Audio = [];
        public readonly TaskCompletionSource Running = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Disposed;
        public bool AutoStart = true;
        public bool Fail;
        public bool HoldFinishSend;
        public readonly TaskCompletionSource FinishReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource FinishSendCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Id = "";
        public Task ConnectAsync(BailianOptions options, CancellationToken ct) { options.Validate(); return Task.CompletedTask; }
        public Task SendTextAsync(string text, CancellationToken ct)
        {
            using var doc = JsonDocument.Parse(text); var header = doc.RootElement.GetProperty("header");
            var action = header.GetProperty("action").GetString()!; Commands.Add(action);
            if (action == "run-task")
            {
                Id = header.GetProperty("task_id").GetString()!;
                Assert.Equal("paraformer-realtime-v2", doc.RootElement.GetProperty("payload").GetProperty("model").GetString());
                Assert.Equal(16000, doc.RootElement.GetProperty("payload").GetProperty("parameters").GetProperty("sample_rate").GetInt32());
                if (Fail) Event("task-failed", error: "InvalidApiKey");
                else if (AutoStart) Event("task-started");
                Running.TrySetResult();
            }
            else if (action == "finish-task")
            {
                Assert.Equal(Id, header.GetProperty("task_id").GetString());
                Sentence(0, "最终第一句。", true); Sentence(0, "最终第一句。", true);
                Sentence(0, "过期中间结果", false); Sentence(200, "第二句。", true);
                Sentence(300, "心跳不应显示", false, true); Event("task-finished");
                FinishReceived.TrySetResult();
                if (HoldFinishSend) return FinishSendCompleted.Task.WaitAsync(ct);
            }
            return Task.CompletedTask;
        }
        public Task SendAudioAsync(byte[] audio, CancellationToken ct)
        {
            Audio.Add(audio); Sentence(0, "中间", false); Sentence(0, "中间修改", false); return Task.CompletedTask;
        }
        public void Event(string type, string? error = null) => _events.Writer.TryWrite(JsonSerializer.Serialize(new { header = new { task_id = Id, @event = type, error_code = error, error_message = "never-display-secret" }, payload = new { } }));
        private void Sentence(int begin, string text, bool final, bool heartbeat = false) => _events.Writer.TryWrite(JsonSerializer.Serialize(new { header = new { task_id = Id, @event = "result-generated" }, payload = new { output = new { sentence = new { begin_time = begin, text, sentence_end = final, heartbeat } } } }));
        public async IAsyncEnumerable<string> ReceiveAsync([EnumeratorCancellation] CancellationToken ct) { await foreach (var e in _events.Reader.ReadAllAsync(ct)) yield return e; }
        public ValueTask DisposeAsync() { Disposed = true; _events.Writer.TryComplete(); return ValueTask.CompletedTask; }
    }
    private static async IAsyncEnumerable<byte[]> Audio([EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield(); ct.ThrowIfCancellationRequested(); yield return new byte[] { 0, 1, 2, 3 };
    }
    [Fact]
    public async Task Protocol_waits_for_started_drains_audio_and_replaces_partial_sentences()
    {
        var wire = new Transport { AutoStart = false }; var previews = new List<string>();
        var recognition = new BailianRealtimeClient(() => wire).RecognizeAsync(new("test-key"), Audio(), previews.Add);
        await wire.Running.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Empty(wire.Audio); Assert.False(recognition.IsCompleted);
        wire.Event("task-started");
        Assert.Equal("最终第一句。第二句。", await recognition.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(new[] { "run-task", "finish-task" }, wire.Commands);
        Assert.Single(wire.Audio); Assert.Contains("中间修改", previews); Assert.DoesNotContain(previews, s => s.Contains("心跳")); Assert.True(wire.Disposed);
    }
    [Fact]
    public async Task Final_event_before_finish_send_returns_is_still_a_successful_recognition()
    {
        var wire = new Transport { HoldFinishSend = true };
        var recognition = new BailianRealtimeClient(() => wire).RecognizeAsync(new("test-key"), Audio());
        await wire.FinishReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));
        // Let the receive loop consume task-finished while the send completion is deliberately held.
        await Task.Delay(100);
        Assert.False(recognition.IsCompleted);
        wire.FinishSendCompleted.TrySetResult();
        Assert.Equal("最终第一句。第二句。", await recognition.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.True(wire.Disposed);
    }
    [Fact]
    public async Task Provider_failure_cancels_audio_wait_and_never_displays_raw_response()
    {
        var wire = new Transport { Fail = true };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new BailianRealtimeClient(() => wire).RecognizeAsync(new("test-key"), Audio()).WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Contains("InvalidApiKey", error.Message); Assert.DoesNotContain("never-display-secret", error.Message);
        Assert.Empty(wire.Audio); Assert.True(wire.Disposed);
    }
    [Fact]
    public async Task Cancellation_while_waiting_for_started_disposes_transport()
    {
        var wire = new Transport { AutoStart = false }; using var cancel = new CancellationTokenSource();
        var recognition = new BailianRealtimeClient(() => wire).RecognizeAsync(new("test-key"), Audio(), ct: cancel.Token);
        await wire.Running.Task; cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => recognition.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.True(wire.Disposed); Assert.Empty(wire.Audio);
    }
    [Fact]
    public async Task Startup_timeout_cancels_receiver_and_disposes_transport()
    {
        var wire = new Transport { AutoStart = false };
        var error = await Assert.ThrowsAsync<TimeoutException>(() => new BailianRealtimeClient(() => wire).RecognizeAsync(new("test-key"), Audio()).WaitAsync(TimeSpan.FromSeconds(15)));
        Assert.Contains("超时", error.Message); Assert.Empty(wire.Audio); Assert.True(wire.Disposed);
    }
    [Fact]
    public async Task Pcm_source_failure_is_observed_and_cancels_receiver()
    {
        var wire = new Transport();
        async IAsyncEnumerable<byte[]> Broken() { await Task.Yield(); yield return new byte[] { 1 }; }
        await Assert.ThrowsAsync<InvalidDataException>(() => new BailianRealtimeClient(() => wire).RecognizeAsync(new("test-key"), Broken()).WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.True(wire.Disposed); Assert.DoesNotContain("finish-task", wire.Commands);
    }
    [WindowsOnlyFact]
    public async Task Windows_token_is_encrypted_and_can_be_replaced_even_if_old_ciphertext_is_corrupt()
    {
        using var db = new LiteDbStore(":memory:"); var store = new LiteDbSessionSettingsStore(db);
        var speech = new BailianSettingsStore(store, new WindowsTokenProtector());
        await speech.SaveAsync("Bearer sk-test-secret", "", 1);
        var cipher = await store.GetStringAsync(BailianSettingsStore.TokenKey);
        Assert.DoesNotContain("sk-test-secret", cipher!);
        Assert.Equal(new BailianOptions("sk-test-secret", "", 1), await speech.LoadAsync());
        await speech.SaveAsync("", "workspace123", 0); Assert.Equal("sk-test-secret", (await speech.LoadAsync()).Token);
        await store.SetAsync(BailianSettingsStore.TokenKey, "corrupt");
        await speech.SaveAsync("sk-replacement", "", 0); Assert.Equal("sk-replacement", (await speech.LoadAsync()).Token);
        await speech.ClearTokenAsync(); Assert.Empty((await speech.LoadAsync()).Token);
    }
    [Fact]
    public void Token_protectors_report_whether_they_really_encrypt()
    {
        Assert.False(new PlainTokenProtector().IsEncrypted);
        if (OperatingSystem.IsWindows()) Assert.True(new WindowsTokenProtector().IsEncrypted);
    }
    private sealed class Capture : IPcmCapture
    {
        private readonly Channel<byte[]> _audio = Channel.CreateUnbounded<byte[]>();
        public bool Started, Disposed;
        public void Start() { Started = true; _audio.Writer.TryWrite(new byte[] { 0, 1 }); }
        public Task StopAsync() { _audio.Writer.TryWrite(new byte[] { 2, 3 }); _audio.Writer.TryComplete(); return Task.CompletedTask; }
        public IAsyncEnumerable<byte[]> ReadAsync(CancellationToken ct) => _audio.Reader.ReadAllAsync(ct);
        public void Dispose() { Disposed = true; _audio.Writer.TryComplete(); }
    }
    [Fact]
    public async Task Microphone_stop_preserves_tail_audio_and_dispose_is_idempotent()
    {
        var wire = new Transport(); var capture = new Capture();
        var session = new BailianMicrophoneSession(new("test-key"), new(() => wire), capture, _ => { });
        Assert.True(capture.Started); await session.StopAudioAsync();
        Assert.Equal("最终第一句。第二句。", await session.Completion.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(2, wire.Audio.Count); await session.DisposeAsync(); session.Cancel(); await session.DisposeAsync(); Assert.True(capture.Disposed);
    }
    [Theory]
    [InlineData("", "")]
    [InlineData("a b", "")]
    [InlineData("key", "evil.example/route")]
    public void Invalid_configuration_is_rejected_before_network(string token, string workspace) => Assert.Throws<InvalidOperationException>(() => new BailianOptions(token, workspace).Validate());
}
