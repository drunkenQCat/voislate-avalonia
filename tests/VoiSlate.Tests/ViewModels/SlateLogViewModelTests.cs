using VoiSlate.Models;
using VoiSlate.Services;
using VoiSlate.Tests.TestDoubles;
using VoiSlate.ViewModels;
using Xunit;

namespace VoiSlate.Tests.VM;

/// <summary>
/// SlateLogViewModel：Today/Dates/TodayLogs 只读订阅 LogsChanged（契约 §4；写入口唯一 ITakeFlowService）。
/// </summary>
public class SlateLogViewModelTests
{
    private static async Task<(SlateLogViewModel Vm, FakeLogRepository Logs, TakeFlowService Flow, FakeSessionSettingsStore Settings, FileNumberingService FileNum)>
        NewFlowAsync()
    {
        var time = new FakeTimeProvider();
        var settings = new FakeSessionSettingsStore();
        var session = new RecordingSessionViewModel(settings, time);
        await session.Initialization;
        var logs = new FakeLogRepository();
        var fileNum = new FileNumberingService(time);
        var flow = new TakeFlowService(logs, new FakePickerHistoryStore(), session, fileNum, settings, time,
            new NoopHapticsService(), new NoopToastService())
        {
            SceneLabelProvider = () => "1A",
            ShotLabelProvider = () => "1A",
            CurrentObjectsProvider = () => [],
        };
        await flow.InitializeAsync(CancellationToken.None);

        var vm = new SlateLogViewModel(flow, logs, time);
        return (vm, logs, flow, settings, fileNum);
    }

    private static SlateLogItem Log(string scn, string sht) => new()
    {
        Scn = scn,
        Sht = sht,
        Tk = 1,
        FilenamePrefix = "260820",
        FilenameLinker = "-T",
        FilenameNum = 1,
        TkNote = "n",
    };

    [Fact]
    public async Task Load_Exposes_Today_Dates_And_TodayLogs()
    {
        var (vm, logs, _, _, _) = await NewFlowAsync();
        await logs.AddAsync("260820", "260820-T001", Log("1A", "1A"));
        await logs.AddAsync("260819", "260819-T001", Log("2A", "1A"));

        await vm.LoadAsync();

        Assert.Equal("260820", vm.Today);
        Assert.Equal(["260819", "260820"], vm.Dates);
        Assert.Single(vm.TodayLogs);
        Assert.Equal("1A", vm.TodayLogs[0].Scn);
    }

    [Fact]
    public async Task LogsChanged_Refreshes_TodayLogs_Automatically()
    {
        var (vm, logs, flow, _, _) = await NewFlowAsync();
        await vm.LoadAsync();

        await flow.AddItemAsync(TakeType.Normal, CancellationToken.None); // 无日志
        await flow.AddItemAsync(TakeType.Normal, CancellationToken.None); // log1

        Assert.Single(vm.TodayLogs);
        Assert.Equal("1A", vm.TodayLogs[0].Scn);
        Assert.Equal("260820-T001", vm.TodayLogs[0].FileName);

        await flow.RewindAsync(CancellationToken.None);
        Assert.Empty(vm.TodayLogs);
    }

    [Fact]
    public async Task Dispose_Unsubscribes_LogsChanged()
    {
        var (vm, _, flow, _, _) = await NewFlowAsync();
        await vm.LoadAsync();

        vm.Dispose();
        await flow.AddItemAsync(TakeType.Normal, CancellationToken.None);
        await flow.AddItemAsync(TakeType.Normal, CancellationToken.None);

        Assert.Empty(vm.TodayLogs); // 已退订 → 不刷新
    }
}