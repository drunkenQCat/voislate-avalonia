using System.Threading.Channels;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media;
using VoiSlate.Desktop.Speech;

namespace VoiSlate.Android;

/// <summary>
/// Android 侧麦克风采集：<see cref="IPcmCapture"/> 的手机实现。
/// 与小节其余部分保持一致：16 kHz / 16 bit / 单声道 / 小端 PCM——百炼实时识别直接吃这个格式，
/// 不要在这里做重采样或压缩，否则要同步改 BailianRealtimeClient 的上行参数。
/// </summary>
internal sealed class AndroidPcmCapture : IPcmCapture
{
    private const int SampleRate = 16000;
    private const int BytesPerBlock = 3200; // 100 ms

    private readonly Channel<byte[]> _audio = Channel.CreateBounded<byte[]>(
        new BoundedChannelOptions(100) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private AudioRecord? _record;
    private Thread? _pump;
    private bool _started, _stopRequested, _released;
    private readonly object _gate = new();

    public AndroidPcmCapture(int deviceNumber) => DeviceNumber = deviceNumber;

    public int DeviceNumber { get; }

    public void Start()
    {
        if (_started) throw new InvalidOperationException("录音已经开始");
        if (global::Android.App.Application.Context?.CheckSelfPermission(global::Android.Manifest.Permission.RecordAudio) != Permission.Granted)
            throw new InvalidOperationException("没有麦克风权限：请在系统设置里允许 VoiSlate 录音");

        var min = AudioRecord.GetMinBufferSize(SampleRate, ChannelIn.Mono, global::Android.Media.Encoding.Pcm16bit);
        // GetMinBufferSize 在某些机型上返回极小值（甚至负值），取两者的较大者并留 2 倍余量防欠载。
        var buffer = Math.Max(min > 0 ? min : 0, BytesPerBlock) * 2;
#pragma warning disable CA1416, CA1422 // 构造式 API 在新系统上标记过时，但仍是兼容 minSdk 23 的唯一写法
        _record = new AudioRecord(AudioSource.Mic, SampleRate, ChannelIn.Mono, global::Android.Media.Encoding.Pcm16bit, buffer);
#pragma warning restore CA1416, CA1422
        if (_record.State != State.Initialized)
        {
            _record.Release(); _record = null;
            throw new InvalidOperationException("麦克风初始化失败，可能被其它应用占用");
        }
        _record.StartRecording();
        if (_record.RecordingState != RecordState.Recording)
        {
            _record.Release(); _record = null;
            throw new InvalidOperationException("麦克风启动失败，请检查录音权限");
        }
        _started = true;
        _pump = new Thread(Pump) { IsBackground = true, Name = "voislate-pcm" };
        _pump.Start();
    }

    private void Pump()
    {
        var buffer = new byte[BytesPerBlock];
        // 本地抓一份引用：Release 之后 _record 会被置空，循环里再读字段会 NRE，也不该让主线程
        // 在采集线程还卡在 Read() 里的时候就把原生对象释放掉（那是 SIGSEGV，进程直接没）。
        var record = _record;
        try
        {
            while (!_stopRequested)
            {
                var read = record?.Read(buffer, 0, buffer.Length) ?? 0;
                if (read < 0) { _audio.Writer.TryComplete(new IOException("麦克风读取失败，请重新标注")); return; }
                // 真机实测：AudioRecord 在停止瞬间会返回 0（不是负数），原来的 `<= 0 → continue`
                // 会让采集线程空转不退，StopAsync 等 3 秒后抛英文 TimeoutException 给用户看。
                if (read == 0) { Thread.Sleep(5); continue; }
                if (!_audio.Writer.TryWrite(buffer.AsSpan(0, read).ToArray()))
                {
                    _audio.Writer.TryComplete(new IOException("网络发送过慢，录音缓冲已满，请重试"));
                    break;
                }
            }
        }
        catch (Exception ex) { _audio.Writer.TryComplete(ex); }
        finally { _audio.Writer.TryComplete(); _stopped.TrySetResult(); }
    }

    public async Task StopAsync()
    {
        if (!_started) { _audio.Writer.TryComplete(); return; }
        if (!_stopRequested)
        {
            _stopRequested = true;
            try { _record?.Stop(); } catch { /* 已停止或未初始化 */ }
        }
        try { await _stopped.Task.WaitAsync(TimeSpan.FromSeconds(3)); }
        catch (TimeoutException)
        {
            // 采集线程万一卡在 Read 上：只把流关掉让识别链路结束（用户看到的仍是「录音停止超时」中文提示），
            // 但**不要**在这里 Release —— 采集线程还停在原生 Read() 里，同时 Release 会直接 SIGSEGV。
            // 原生对象交给 Dispose（它会先等线程退出再释放）。
            VoiSlate.Services.Diagnostics.Log("[PCM] stop timeout, leaving release to Dispose");
            _audio.Writer.TryComplete();
        }
    }

    public async IAsyncEnumerable<byte[]> ReadAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var block in _audio.Reader.ReadAllAsync(ct)) yield return block;
    }

    /// <summary>
    /// 收尾必须「先停、再等线程退出、最后释放原生对象」。
    /// 真机实测：原实现在采集线程仍阻塞于 AudioRecord.Read() 时就 Release()，进程无 Java 异常直接消失
    /// （logcat 只有 `Death received`）。释放失败也好过崩进程，所以宁可泄漏也不抢着 Release。
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_released) return;
            _released = true;
        }
        _stopRequested = true;
        try { _record?.Stop(); } catch { /* 已停止或未初始化 */ }
        _audio.Writer.TryComplete();
        var drained = false;
        try { drained = _stopped.Task.Wait(TimeSpan.FromSeconds(2)); } catch { /* 已取消 */ }
        if (!drained) VoiSlate.Services.Diagnostics.Log("[PCM] dispose: pump still running, skip native release");
        var record = _record;
        _record = null;
        if (!drained) return; // 线程还在 Read() 里，宁可泄漏 AudioRecord 也不 Release
        try { record?.Release(); } catch { /* 已释放 */ }
    }
}

/// <summary>把 Android 采集器注册进核心工程的 <see cref="PcmCapture"/> 注册表。</summary>
internal static class AndroidMic
{
    /// <summary>当前前台 Activity（权限申请要用它；由 MainActivity.OnCreate 填入）。</summary>
    public static Activity? CurrentActivity { get; set; }

    public static void Register()
    {
        PcmCapture.Factory = _ => new AndroidPcmCapture(0);
        PcmCapture.DeviceList = ListDevices;
        PcmCapture.PermissionRequest = RequestPermissionAsync;
    }

    private static IReadOnlyList<string> ListDevices()
    {
        var names = new List<string>();
        try
        {
            if (global::Android.App.Application.Context?.GetSystemService(Context.AudioService) is AudioManager audio)
            {
                foreach (var device in audio.GetDevices(GetDevicesTargets.Inputs) ?? [])
                    names.Add(device?.ProductName?.ToString() is { Length: > 0 } name ? name : "内置麦克风");
            }
        }
        catch (Exception) { /* 枚举失败就退回默认项 */ }
        if (names.Count == 0) names.Add("系统默认麦克风");
        return names;
    }

    private static Task<bool> RequestPermissionAsync()
    {
        var activity = CurrentActivity;
        if (activity is null) return Task.FromResult(false);
        if (activity.CheckSelfPermission(global::Android.Manifest.Permission.RecordAudio) == Permission.Granted)
            return Task.FromResult(true);
        return MainActivity.RequestRecordAudioAsync();
    }
}