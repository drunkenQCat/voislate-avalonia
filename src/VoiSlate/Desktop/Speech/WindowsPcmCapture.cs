using System.Runtime.CompilerServices;
using System.Threading.Channels;
using NAudio.Wave;

namespace VoiSlate.Desktop.Speech;

public interface IPcmCapture : IDisposable
{
    void Start();
    Task StopAsync();
    IAsyncEnumerable<byte[]> ReadAsync(CancellationToken ct);
}
/// <summary>Windows microphone, 16 kHz / 16 bit / mono signed little-endian PCM.</summary>
public sealed class WindowsPcmCapture(int deviceNumber) : IPcmCapture
{
    private readonly Channel<byte[]> _audio = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(100) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private WaveInEvent? _wave;
    private bool _started, _stopRequested;
    public static IReadOnlyList<string> Devices()
    {
        if (!OperatingSystem.IsWindows()) return [];
        return Enumerable.Range(0, WaveInEvent.DeviceCount).Select(i => WaveInEvent.GetCapabilities(i).ProductName).ToList();
    }
    public void Start()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("麦克风采集需要 Windows");
        if (_started) throw new InvalidOperationException("录音已经开始");
        if (deviceNumber >= WaveInEvent.DeviceCount) throw new InvalidOperationException("找不到所选麦克风，请在设置中重新选择");
        _wave = new WaveInEvent { DeviceNumber = deviceNumber, WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 100, NumberOfBuffers = 3 };
        _wave.DataAvailable += (_, e) =>
        {
            if (!_audio.Writer.TryWrite(e.Buffer.AsSpan(0, e.BytesRecorded).ToArray()))
            {
                _audio.Writer.TryComplete(new IOException("网络发送过慢，录音缓冲已满，请重试"));
                _wave?.StopRecording();
            }
        };
        _wave.RecordingStopped += (_, e) => { _audio.Writer.TryComplete(e.Exception); _stopped.TrySetResult(); };
        try { _wave.StartRecording(); _started = true; }
        catch (Exception e) { _audio.Writer.TryComplete(e); _stopped.TrySetResult(); _wave.Dispose(); _wave = null; throw new InvalidOperationException("麦克风启动失败，请检查设备和 Windows 麦克风访问设置", e); }
    }
    public async Task StopAsync()
    {
        if (!_started) { _audio.Writer.TryComplete(); return; }
        if (!_stopRequested) { _stopRequested = true; _wave?.StopRecording(); }
        await _stopped.Task.WaitAsync(TimeSpan.FromSeconds(3));
    }
    public async IAsyncEnumerable<byte[]> ReadAsync([EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var block in _audio.Reader.ReadAllAsync(ct)) yield return block;
    }
    public void Dispose()
    {
        if (_wave is not null) { try { _wave.StopRecording(); } finally { _wave.Dispose(); _wave = null; } }
        _audio.Writer.TryComplete(); _stopped.TrySetResult();
    }
}
