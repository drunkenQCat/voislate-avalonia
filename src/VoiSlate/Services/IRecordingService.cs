namespace VoiSlate.Services;

#pragma warning disable CS0067 // Mock 事件暂不触发只在特定实现（MockRecordingService 会触发，占位说明）

/// <summary>
/// 录音服务（契约 §3 IRecordingService；原版 record 能力被 Mock 替代，接口先行）。
/// </summary>
public interface IRecordingService
{
    Task<bool> RequestPermissionAsync();
    Task StartAsync();
    Task StopAsync();
    bool IsRunning { get; }

    /// <summary>模拟电平（0..1），Mock 以 500ms 周期触发。</summary>
    event Action<double>? LevelChanged;
}

/// <summary>
/// Mock 录音：不采集真实音频；Start 后以 500ms（可注入）周期模拟电平曲线（确定性递增循环，便于单测）。
/// </summary>
public sealed class MockRecordingService : IRecordingService, IDisposable
{
    private readonly TimeSpan _tickInterval;
    private readonly object _gate = new();
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;
    private int _tick;

    public MockRecordingService(TimeSpan? tickInterval = null)
    {
        _tickInterval = tickInterval ?? TimeSpan.FromMilliseconds(500);
    }

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _cts != null;
            }
        }
    }

    public event Action<double>? LevelChanged;

    public Task<bool> RequestPermissionAsync() => Task.FromResult(true);

    public Task StartAsync()
    {
        lock (_gate)
        {
            if (_cts != null)
            {
                return Task.CompletedTask; // 已在运行：幂等
            }

            _cts = new CancellationTokenSource();
            _timer = new PeriodicTimer(_tickInterval);
            _ = Task.Run(() => RunAsync(_timer, _cts.Token));
        }

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        StopTimer();
        return Task.CompletedTask;
    }

    private CancellationTokenSource? StopTimer()
    {
        lock (_gate)
        {
            var cts = _cts;
            cts?.Cancel();
            _timer?.Dispose();
            cts?.Dispose();
            _cts = null;
            _timer = null;
            return cts;
        }
    }

    private async Task RunAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            // 确定性电平曲线：0.2, 0.5, 0.8, 0.4 → 循环。
            double[] curve = [0.2, 0.5, 0.8, 0.4];
            while (await timer.WaitForNextTickAsync(ct))
            {
                var value = curve[_tick % curve.Length];
                _tick++;
                LevelChanged?.Invoke(value);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停止
        }
    }

    public void Dispose() => StopTimer();
}