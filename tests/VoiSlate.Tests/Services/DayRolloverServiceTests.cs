using VoiSlate.Services;
using VoiSlate.Tests.TestDoubles;
using Xunit;

namespace VoiSlate.Tests.Services;

/// <summary>
/// 跨天补偿（契约 §3 IDayRolloverService；对齐 main.dart:46-53 日期登记/recordCount 归 1/picker_history 清空 + B9）。
/// </summary>
public class DayRolloverServiceTests
{
    private readonly FakeSessionSettingsStore _settings = new();
    private readonly FakePickerHistoryStore _history = new();
    private readonly FakeLogRepository _logs = new();
    private readonly FakeTimeProvider _time = new();

    private DayRolloverService NewService() => new(_history, _settings, _time);

    private static readonly DateTime Day1 = new(2026, 8, 20, 12, 0, 0); // 260820
    private static readonly DateTime Day2 = new(2026, 8, 21, 12, 0, 0); // 260821

    [Fact]
    public void First_Launch_Is_New_Day_And_OnStartup_Registers_Today()
    {
        var svc = NewService();

        Assert.True(svc.IsNewDay()); // 无持久化日期 → 视为新日志日

        svc.OnStartup();

        Assert.Equal("260820", _settings.Data[SessionKeys.Date]);
        Assert.Equal(1, _settings.Data[SessionKeys.RecordCount]);
        Assert.False(svc.IsNewDay());
    }

    [Fact]
    public async Task Same_Day_Startup_Does_Not_Reset_Nor_Clear_History()
    {
        _settings.Data[SessionKeys.Date] = "260820";
        _settings.Data[SessionKeys.RecordCount] = 7;
        await _history.AddAsync(["1A", "1A", "3", "对象"]);

        var svc = NewService();

        Assert.False(svc.IsNewDay());
        svc.OnStartup();

        Assert.Equal(7, (int)_settings.Data[SessionKeys.RecordCount]!); // 同日不重置
        Assert.Equal(1, await _history.CountAsync());                    // 同日不清空
    }

    [Fact]
    public async Task New_Day_Startup_Rolls_Over_RecordCount_History_And_Date()
    {
        _settings.Data[SessionKeys.Date] = "260819";
        _settings.Data[SessionKeys.RecordCount] = 12;
        await _history.AddAsync(["1A", "1A", "5"]);

        var svc = NewService();

        Assert.True(svc.IsNewDay());
        svc.OnStartup();

        Assert.Equal(1, (int)_settings.Data[SessionKeys.RecordCount]!);  // B9：recordCount 归 1
        Assert.Equal(0, await _history.CountAsync());                    // B9：picker_history 清空
        Assert.Equal("260820", _settings.Data[SessionKeys.Date]);        // 日期登记
    }

    [Fact]
    public async Task Periodic_Check_Detects_Mid_Session_Day_Change()
    {
        _settings.Data[SessionKeys.Date] = "260820";
        _settings.Data[SessionKeys.RecordCount] = 3;
        await _history.AddAsync(["1A", "1A", "2"]);

        var time = new MutableFakeTimeProvider { Now = Day1 };
        using var svc = new DayRolloverService(_history, _settings, time);
        var rolledOver = 0;
        svc.DayChanged += () => rolledOver++;

        svc.StartPeriodicCheck(TimeSpan.FromMilliseconds(20));

        // 同日 tick：不触发
        await Task.Delay(150);
        Assert.Equal(0, rolledOver);
        Assert.Equal(3, (int)_settings.Data[SessionKeys.RecordCount]!);

        // 跨天 tick：B9 补偿触发一次
        time.Now = Day2;
        await WaitUntil(() => rolledOver >= 1, TimeSpan.FromSeconds(3));

        Assert.Equal(1, rolledOver);
        Assert.Equal(1, (int)_settings.Data[SessionKeys.RecordCount]!);
        Assert.Equal(0, await _history.CountAsync());
        Assert.Equal("260821", _settings.Data[SessionKeys.Date]);
    }

    [Fact]
    public void StartPeriodicCheck_Is_Idempotent_And_Dispose_Stops()
    {
        using var svc = NewService();
        svc.StartPeriodicCheck(TimeSpan.FromMilliseconds(10));
        svc.StartPeriodicCheck(TimeSpan.FromMilliseconds(10)); // 二次启动忽略

        svc.Dispose();
        // 不抛异常即通过；Dispose 幂等
        svc.Dispose();
    }

    private static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.True(condition(), "超时未满足条件");
    }
}