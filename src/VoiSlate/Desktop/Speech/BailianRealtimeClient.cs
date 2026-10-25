using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace VoiSlate.Desktop.Speech;

public interface IBailianTransport : IAsyncDisposable
{
    Task ConnectAsync(BailianOptions options, CancellationToken ct);
    Task SendTextAsync(string text, CancellationToken ct);
    Task SendAudioAsync(byte[] audio, CancellationToken ct);
    IAsyncEnumerable<string> ReceiveAsync(CancellationToken ct);
}
public sealed class BailianWebSocketTransport : IBailianTransport
{
    private readonly ClientWebSocket _socket = new();
    public async Task ConnectAsync(BailianOptions options, CancellationToken ct)
    {
        options.Validate();
        _socket.Options.SetRequestHeader("Authorization", "Bearer " + options.Token);
        _socket.Options.SetRequestHeader("User-Agent", "VoiSlate-Windows/1.0");
        if (options.WorkspaceId.Length > 0) _socket.Options.SetRequestHeader("X-DashScope-WorkSpace", options.WorkspaceId);
        _socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        try { await _socket.ConnectAsync(options.Endpoint, ct); }
        catch (WebSocketException ex) { throw new InvalidOperationException("百炼连接失败，请检查网络、北京地域 Token 和业务空间配置", ex); }
    }
    public Task SendTextAsync(string text, CancellationToken ct) => _socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, ct);
    public Task SendAudioAsync(byte[] audio, CancellationToken ct) => _socket.SendAsync(audio, WebSocketMessageType.Binary, true, ct);
    public async IAsyncEnumerable<string> ReceiveAsync([EnumeratorCancellation] CancellationToken ct)
    {
        var buffer = new byte[8192];
        while (_socket.State == WebSocketState.Open)
        {
            using var message = new MemoryStream(); WebSocketReceiveResult result;
            do
            {
                result = await _socket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close) throw new IOException("百炼提前关闭了连接，请重试");
                if (result.MessageType != WebSocketMessageType.Text) throw new InvalidDataException("百炼返回了不支持的消息类型");
                await message.WriteAsync(buffer.AsMemory(0, result.Count), ct);
                if (message.Length > 2 * 1024 * 1024) throw new InvalidDataException("百炼返回的消息过大");
            } while (!result.EndOfMessage);
            yield return Encoding.UTF8.GetString(message.ToArray());
        }
    }
    public ValueTask DisposeAsync() { _socket.Abort(); _socket.Dispose(); return ValueTask.CompletedTask; }
}
/// <summary>Tracks sentences by begin_time, replacing partials and duplicate finals instead of appending them.</summary>
public sealed class BailianTranscript
{
    private readonly SortedDictionary<long, (string Text, bool Final)> _sentences = [];
    public string Text => string.Concat(_sentences.Values.Select(s => s.Text));
    public string FinalText => string.Concat(_sentences.Values.Where(s => s.Final).Select(s => s.Text));
    public void Accept(JsonElement sentence)
    {
        if (sentence.TryGetProperty("heartbeat", out var heartbeat) && heartbeat.ValueKind == JsonValueKind.True) return;
        if (!sentence.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String) return;
        var start = sentence.TryGetProperty("begin_time", out var time) && time.TryGetInt64(out var value) ? value : 0;
        var final = sentence.TryGetProperty("sentence_end", out var ending) && ending.ValueKind == JsonValueKind.True;
        if (_sentences.TryGetValue(start, out var previous) && previous.Final && !final) return;
        _sentences[start] = (text.GetString() ?? "", final);
    }
}
public sealed class BailianRealtimeClient(Func<IBailianTransport>? transportFactory = null)
{
    private readonly Func<IBailianTransport> _factory = transportFactory ?? (() => new BailianWebSocketTransport());
    public async Task CheckConnectionAsync(BailianOptions options, CancellationToken ct = default)
    {
        options.Validate(); using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await using var transport = _factory(); await transport.ConnectAsync(options, timeout.Token);
    }
    public async Task<string> RecognizeAsync(BailianOptions options, IAsyncEnumerable<byte[]> pcm,
        Action<string>? partial = null, CancellationToken ct = default)
    {
        options.Validate(); using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct); lifetime.CancelAfter(TimeSpan.FromMinutes(2));
        var token = lifetime.Token;
        await using var transport = _factory();
        try
        {
            using (var connect = CancellationTokenSource.CreateLinkedTokenSource(token))
            { connect.CancelAfter(TimeSpan.FromSeconds(10)); await transport.ConnectAsync(options, connect.Token); }
            var taskId = Guid.NewGuid().ToString("N");
            await transport.SendTextAsync(JsonSerializer.Serialize(new { header = new { action = "run-task", task_id = taskId, streaming = "duplex" }, payload = new { task_group = "audio", task = "asr", function = "recognition", model = BailianOptions.Model, parameters = new { format = "pcm", sample_rate = 16000, punctuation_prediction_enabled = true }, input = new { } } }), token);
            var transcript = new BailianTranscript();
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task ReceiveResults()
            {
                await foreach (var message in transport.ReceiveAsync(token))
                {
                    using var doc = JsonDocument.Parse(message); var root = doc.RootElement; var header = root.GetProperty("header");
                    if (header.TryGetProperty("task_id", out var id) && id.GetString() != taskId) continue;
                    ThrowTaskError(header);
                    switch (header.GetProperty("event").GetString())
                    {
                        case "task-started": started.TrySetResult(); break;
                        case "result-generated":
                            if (root.GetProperty("payload").TryGetProperty("output", out var output) && output.TryGetProperty("sentence", out var sentence)) { transcript.Accept(sentence); partial?.Invoke(transcript.Text); }
                            break;
                        case "task-finished": return;
                    }
                }
                throw new IOException("识别连接在任务完成前结束");
            }
            bool finishRequested = false;
            async Task SendAudio()
            {
                await started.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
                await foreach (var chunk in pcm.WithCancellation(token))
                {
                    if (chunk.Length % 2 != 0) throw new InvalidDataException("PCM 音频必须是 16 位采样");
                    await transport.SendAudioAsync(chunk, token);
                }
                finishRequested = true;
                await transport.SendTextAsync(JsonSerializer.Serialize(new { header = new { action = "finish-task", task_id = taskId, streaming = "duplex" }, payload = new { input = new { } } }), token);
            }
            var receive = ReceiveResults(); var send = SendAudio();
            try
            {
                var first = await Task.WhenAny(receive, send);
                if (first == receive && !send.IsCompleted)
                {
                    await receive;
                    if (!finishRequested) throw new IOException("百炼在音频发送完毕前结束了任务");
                }
                await send;
                await receive.WaitAsync(TimeSpan.FromSeconds(15), token);
                return transcript.FinalText.Trim();
            }
            finally
            {
                lifetime.Cancel();
                try { await Task.WhenAll(receive, send); } catch { /* Observe both tasks before disposing their transport. */ }
            }
        }
        catch (TimeoutException) { throw new TimeoutException("识别服务响应超时，已停止本次任务，请重试"); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new TimeoutException("识别超时，已停止本次任务，请重试"); }
        finally { lifetime.Cancel(); }
    }
    private static void ThrowTaskError(JsonElement header)
    {
        if (header.GetProperty("event").GetString() != "task-failed") return;
        // Report the provider's error code, never its raw body which could echo credentials.
        var code = header.TryGetProperty("error_code", out var value) ? value.GetString() : "UNKNOWN";
        throw new InvalidOperationException($"百炼识别失败（{code}），请检查 Token、账户额度和模型权限");
    }
}
public sealed class BailianMicrophoneSession : IAsyncDisposable
{
    private readonly IPcmCapture _capture;
    private readonly CancellationTokenSource _cancel = new();
    public Task<string> Completion { get; }
    public BailianMicrophoneSession(BailianOptions options, BailianRealtimeClient client, IPcmCapture capture, Action<string> partial)
    {
        options.Validate(); _capture = capture;
        try { _capture.Start(); Completion = client.RecognizeAsync(options, _capture.ReadAsync(_cancel.Token), partial, _cancel.Token); }
        catch { _capture.Dispose(); _cancel.Dispose(); throw; }
    }
    public Task StopAudioAsync() => _capture.StopAsync();
    private int _cancelled, _disposed;
    public void Cancel() { if (Interlocked.Exchange(ref _cancelled, 1) == 0) { _cancel.Cancel(); _capture.Dispose(); } }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        Cancel(); try { await Completion; } catch { } finally { _cancel.Dispose(); }
    }
}
