using VoiSlate.Models;
using VoiSlate.Services;
using VoiSlate.Tests.TestDoubles;
using VoiSlate.ViewModels;
using Xunit;

namespace VoiSlate.Tests.VM;

/// <summary>
/// SettingsViewModel：ProjectName 持久化 / TodayCount / ClearToday（唯一写入口循环）/ ExportAll（IExportService）。
/// </summary>
public class SettingsViewModelTests
{
    private sealed record Harness(
        SettingsViewModel Vm,
        FakeSessionSettingsStore Settings,
        FakeLogRepository Logs,
        TakeFlowService Flow,
        SpyExportService Exporter);

    private static async Task<Harness> NewAsync(Action<FakeSessionSettingsStore>? seedSettings = null,
        Action<FakeLogRepository>? seedLogs = null)
    {
        var time = new FakeTimeProvider();
        var settings = new FakeSessionSettingsStore();
        seedSettings?.Invoke(settings);
        var session = new RecordingSessionViewModel(settings, time);
        await session.Initialization;
        var logs = new FakeLogRepository();
        seedLogs?.Invoke(logs);
        var flow = new TakeFlowService(logs, new FakePickerHistoryStore(), session,
            new FileNumberingService(time), settings, time, new NoopHapticsService(), new NoopToastService())
        {
            SceneLabelProvider = () => "1A",
            ShotLabelProvider = () => "1A",
            CurrentObjectsProvider = () => [],
        };
        await flow.InitializeAsync(CancellationToken.None);

        var exporter = new SpyExportService();
        var vm = new SettingsViewModel(settings, logs, flow, exporter, time, session);
        await vm.Initialization;
        return new Harness(vm, settings, logs, flow, exporter);
    }

    private static SlateLogItem Log(string scn) => new()
    {
        Scn = scn,
        Sht = "1A",
        Tk = 1,
        FilenamePrefix = "260820",
        FilenameLinker = "-T",
        FilenameNum = 1,
        TkNote = "n",
    };

    [Fact]
    public async Task ProjectName_Loads_And_Persists_On_Change()
    {
        var h = await NewAsync(s => s.Data[SettingsViewModel.ProjectKey] = "我的项目");

        Assert.Equal("我的项目", h.Vm.ProjectName);

        h.Vm.ProjectName = "新项目";
        Assert.Equal("新项目", await h.Settings.GetStringAsync(SettingsViewModel.ProjectKey, ""));
    }

    [Fact]
    public async Task Load_Refreshes_TodayCount()
    {
        var h = await NewAsync(seedLogs: logs =>
        {
            logs.AddAsync("260820", "x1", Log("1A")).GetAwaiter().GetResult();
            logs.AddAsync("260820", "x2", Log("2A")).GetAwaiter().GetResult();
            logs.AddAsync("260819", "x3", Log("3A")).GetAwaiter().GetResult(); // 昨日不计
        });

        Assert.Equal(2, h.Vm.TodayCount);
    }

    [Fact]
    public async Task ClearToday_Removes_Todays_Logs_Through_Service_Only()
    {
        var h = await NewAsync(seedLogs: logs =>
        {
            logs.AddAsync("260820", "x1", Log("1A")).GetAwaiter().GetResult();
            logs.AddAsync("260820", "x2", Log("2A")).GetAwaiter().GetResult();
            logs.AddAsync("260819", "x3", Log("3A")).GetAwaiter().GetResult();
        });
        h.Settings.Data[SessionKeys.RecordCount] = 5; // 文件号不受清空影响（原版 clear 语义）

        await h.Vm.ClearTodayCommand.ExecuteAsync(null);

        Assert.DoesNotContain(h.Logs.Items, x => x.Date == "260820");
        Assert.Single(h.Logs.Items); // 昨日仍在
        Assert.Equal(0, h.Vm.TodayCount);
        Assert.Equal(5, await h.Settings.GetIntAsync(SessionKeys.RecordCount, 0)); // 文件号不受清空影响
    }

    [Fact]
    public async Task ExportAll_Collects_All_Dates_And_Delegates_To_IExportService()
    {
        var h = await NewAsync(seedLogs: logs =>
        {
            logs.AddAsync("260819", "x1", Log("3A")).GetAwaiter().GetResult();
            logs.AddAsync("260820", "x2", Log("1A")).GetAwaiter().GetResult();
            logs.AddAsync("260820", "x3", Log("2A")).GetAwaiter().GetResult();
        });

        await h.Vm.ExportAllCommand.ExecuteAsync(null);

        Assert.Equal(3, h.Exporter.LastLogs!.Count);
        Assert.Equal("all.json", h.Exporter.LastName);
        Assert.Equal("[]", h.Exporter.LastContent);
    }
}