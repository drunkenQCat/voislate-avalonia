using VoiSlate.Models;
using VoiSlate.Services;
using VoiSlate.Tests.TestDoubles;
using VoiSlate.ViewModels;
using Xunit;

namespace VoiSlate.Tests.VM;

/// <summary>
/// SlateLogPageViewModel：日期 Tab/分组树/高亮/编辑入口/导出（契约 §4；编辑唯一入口 ITakeFlowService）。
/// </summary>
public class SlateLogPageViewModelTests
{
    private sealed record Harness(
        SlateLogPageViewModel Vm,
        FakeLogRepository Logs,
        SpyExportService Exporter,
        RecordingSessionViewModel Session,
        TakeFlowService Flow);

    private static async Task<Harness> NewAsync(Action<FakeLogRepository>? seed = null)
    {
        var time = new FakeTimeProvider();
        var settings = new FakeSessionSettingsStore();
        var session = new RecordingSessionViewModel(settings, time);
        await session.Initialization;
        var logs = new FakeLogRepository();
        seed?.Invoke(logs);
        var flow = new TakeFlowService(logs, new FakePickerHistoryStore(), session,
            new FileNumberingService(time), settings, time, new NoopHapticsService(), new NoopToastService())
        {
            SceneLabelProvider = () => "1A",
            ShotLabelProvider = () => "1A",
            CurrentObjectsProvider = () => [],
        };
        await flow.InitializeAsync(CancellationToken.None);

        var exporter = new SpyExportService();
        var vm = new SlateLogPageViewModel(logs, exporter, flow, session, new StubScheduleBook(), time);
        await vm.LoadAsync();
        return new Harness(vm, logs, exporter, session, flow);
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
    public async Task Load_Selects_Latest_Date_And_Builds_Groups()
    {
        var h = await NewAsync(logs =>
        {
            logs.AddAsync("260819", "x1", Log("2A", "1A")).GetAwaiter().GetResult();
            logs.AddAsync("260820", "x2", Log("1A", "1A")).GetAwaiter().GetResult();
            logs.AddAsync("260820", "x3", Log("1A", "2A")).GetAwaiter().GetResult();
        });

        Assert.Equal(["260819", "260820"], h.Vm.Dates);
        Assert.Equal("260820", h.Vm.SelectedDate); // 新日期在前，默认选中
        Assert.Single(h.Vm.Groups);
        Assert.Equal("1A", h.Vm.Groups[0].Scn);
        Assert.Equal(2, h.Vm.Groups[0].Shots.Count);
    }

    [Fact]
    public async Task SelectDate_Reloads_Groups()
    {
        var h = await NewAsync(logs =>
        {
            logs.AddAsync("260819", "x1", Log("2A", "1A")).GetAwaiter().GetResult();
            logs.AddAsync("260820", "x2", Log("1A", "1A")).GetAwaiter().GetResult();
        });

        h.Vm.SelectedDate = "260819";

        Assert.Single(h.Vm.Groups);
        Assert.Equal("2A", h.Vm.Groups[0].Scn);
    }

    [Fact]
    public async Task LogsChanged_Refreshes_When_Selected_Date_Is_Today()
    {
        var h = await NewAsync();

        await h.Flow.AddItemAsync(TakeType.Normal, CancellationToken.None);
        await h.Flow.AddItemAsync(TakeType.Normal, CancellationToken.None);

        Assert.Single(h.Vm.Groups);
    }

    [Fact]
    public async Task ExportJson_Delegates_To_IExportService()
    {
        var h = await NewAsync(logs =>
            logs.AddAsync("260820", "x1", Log("1A", "1A")).GetAwaiter().GetResult());

        await h.Vm.ExportJsonCommand.ExecuteAsync(null);

        Assert.Single(h.Exporter.LastLogs!);
        Assert.Equal("260820.json", h.Exporter.LastName);
        Assert.Equal("[]", h.Exporter.LastContent);
    }

    [Fact]
    public async Task CreateLogEditor_Works_Only_For_Today()
    {
        var h = await NewAsync(logs =>
        {
            logs.AddAsync("260819", "x1", Log("2A", "1A")).GetAwaiter().GetResult();
            logs.AddAsync("260820", "x2", Log("1A", "1A")).GetAwaiter().GetResult();
        });

        h.Vm.SelectedDate = "260819";
        Assert.False(h.Vm.CanEditSelectedDate);
        Assert.Null(h.Vm.CreateLogEditor(h.Vm.Groups[0].Shots[0].Items[0]));

        h.Vm.SelectedDate = "260820";
        Assert.True(h.Vm.CanEditSelectedDate);
        var editor = h.Vm.CreateLogEditor(h.Vm.Groups[0].Shots[0].Items[0]);
        Assert.NotNull(editor);
        Assert.Equal("1A", editor!.Scn);
    }

    [Fact]
    public async Task Highlight_Follows_Session_Selection()
    {
        var h = await NewAsync();
        Assert.Equal("1A", h.Vm.CurrentScene);

        h.Session.SelectScene(1);
        h.Session.SelectShot(1);

        Assert.Equal("1A", h.Vm.CurrentScene); // Stub 场标签固定 "1A"；仅验证联动接线
        Assert.Equal("1", h.Vm.CurrentShot);   // Stub 单镜名 = key "1" + fix ""

    }
}