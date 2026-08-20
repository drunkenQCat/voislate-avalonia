using VoiSlate.Services;
using Xunit;

namespace VoiSlate.Tests.Services;

/// <summary>Mock 录音（契约 §3 IRecordingService；500ms 周期电平，可注入间隔，确定性曲线）。</summary>
public class MockRecordingServiceTests
{
    [Fact]
    public async Task Permission_Is_Granted_And_Start_Stop_Toggle_Running()
    {
        using var svc = new MockRecordingService();

        Assert.True(await svc.RequestPermissionAsync());
        Assert.False(svc.IsRunning);

        await svc.StartAsync();
        Assert.True(svc.IsRunning);
        await svc.StartAsync(); // 幂等

        await svc.StopAsync();
        Assert.False(svc.IsRunning);
    }

    [Fact]
    public async Task LevelChanged_Emits_Deterministic_Curve_On_Ticks()
    {
        using var svc = new MockRecordingService(TimeSpan.FromMilliseconds(5));
        var levels = new List<double>();
        svc.LevelChanged += l => levels.Add(l);

        await svc.StartAsync();

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (levels.Count < 6 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(15);
        }

        await svc.StopAsync();

        // 曲线 0.2, 0.5, 0.8, 0.4 循环（确定性：前 6 个采样 = 两轮）
        Assert.True(levels.Count >= 6, $"电平事件不足，收到 {levels.Count}");
        Assert.Equal([0.2, 0.5, 0.8, 0.4, 0.2, 0.5], levels.Take(6).ToArray());
    }

    [Fact]
    public async Task Levels_Stop_Firing_After_Stop()
    {
        using var svc = new MockRecordingService(TimeSpan.FromMilliseconds(5));
        var count = 0;
        svc.LevelChanged += _ => Interlocked.Increment(ref count);

        await svc.StartAsync();
        await Task.Delay(80);
        await svc.StopAsync();
        var afterStop = count;

        await Task.Delay(60);
        Assert.Equal(afterStop, count); // 停止后不再触发
    }

    [Fact]
    public async Task Dispose_Stops_The_Timer()
    {
        var svc = new MockRecordingService(TimeSpan.FromMilliseconds(5));
        await svc.StartAsync();
        Assert.True(svc.IsRunning);

        svc.Dispose();
        Assert.False(svc.IsRunning);
        svc.Dispose(); // 幂等
    }
}