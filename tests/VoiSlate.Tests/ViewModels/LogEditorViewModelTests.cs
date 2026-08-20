using VoiSlate.Models;
using VoiSlate.Services;
using VoiSlate.Tests.TestDoubles;
using VoiSlate.ViewModels;
using Xunit;

namespace VoiSlate.Tests.VM;

/// <summary>
/// LogEditorViewModel：编辑副本/可用文件号/保存删除唯一入口/轨道标签（对齐原版 log_editor.dart）。
/// </summary>
public class LogEditorViewModelTests
{
    private static async Task<(TakeFlowService Flow, FakeLogRepository Logs)> NewFlowAsync()
    {
        var time = new FakeTimeProvider();
        var settings = new FakeSessionSettingsStore();
        var session = new RecordingSessionViewModel(settings, time);
        await session.Initialization;
        var logs = new FakeLogRepository();
        var flow = new TakeFlowService(logs, new FakePickerHistoryStore(), session,
            new FileNumberingService(time), settings, time, new NoopHapticsService(), new NoopToastService())
        {
            SceneLabelProvider = () => "1A",
            ShotLabelProvider = () => "1A",
            CurrentObjectsProvider = () => [],
        };
        await flow.InitializeAsync(CancellationToken.None);
        return (flow, logs);
    }

    private static SlateLogItem LogItem() => new()
    {
        Scn = "1A",
        Sht = "1A",
        Tk = 3,
        FilenamePrefix = "260820",
        FilenameLinker = "-T",
        FilenameNum = 7,
        TkNote = "描述",
        ShtNote = "标注<缪尔赛斯/><塞雷娅/>",
        ScnNote = "本场信息",
        OkTk = TkStatus.Ok,
        OkSht = ShtStatus.Nice,
    };

    [Fact]
    public async Task Ctor_Snapshots_Item_And_Splits_Mic_Protocol()
    {
        var (flow, _) = await NewFlowAsync();
        var vm = new LogEditorViewModel(flow, LogItem(), 0, [7]);

        Assert.Equal("1A", vm.Scn);
        Assert.Equal("1A", vm.Sht);
        Assert.Equal(3, vm.TkNumber);
        Assert.Equal(7, vm.FilenameNum);
        Assert.Equal("描述", vm.TkNote);
        Assert.Equal("标注", vm.ShtNote);
        Assert.Equal(["缪尔赛斯", "塞雷娅"], vm.TrackTags);
        Assert.Equal(TkStatus.Ok, vm.OkTk);
        Assert.Equal(ShtStatus.Nice, vm.OkSht);
    }

    [Fact]
    public async Task AvailableFileNumbers_Excludes_Used_And_Keeps_Original_First()
    {
        var (flow, _) = await NewFlowAsync();
        var vm = new LogEditorViewModel(flow, LogItem(), 0, [7, 100, 500]);

        Assert.Equal(7, vm.AvailableFileNumbers[0]); // 当前号置顶
        Assert.DoesNotContain(100, vm.AvailableFileNumbers);
        Assert.DoesNotContain(500, vm.AvailableFileNumbers);
        Assert.Contains(1, vm.AvailableFileNumbers);
        Assert.Equal(498, vm.AvailableFileNumbers.Count); // 500-3 已用 + 当前号置顶
    }

    [Fact]
    public async Task Save_Goes_Through_Service_And_Updates_Repo()
    {
        var (flow, logs) = await NewFlowAsync();
        await flow.AddItemAsync(TakeType.Normal, CancellationToken.None); // 无日志 push
        await flow.AddItemAsync(TakeType.Normal, CancellationToken.None); // log1

        var vm = new LogEditorViewModel(flow, logs.Items[0].Item, 0, [1]);
        vm.TkNumber = 5;
        vm.FilenameNum = 9;
        vm.TkNote = "改";
        vm.AddTag("对象X");
        vm.OkTk = TkStatus.Bad;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(vm.Saved);
        var saved = logs.Items[0].Item;
        Assert.Equal(5, saved.Tk);
        Assert.Equal(9, saved.FilenameNum);
        Assert.Equal("改", saved.TkNote);
        Assert.Equal("<对象X/>", saved.ShtNote); // 空正文 + 轨道
        Assert.Equal(TkStatus.Bad, saved.OkTk);
    }

    [Fact]
    public async Task Delete_Goes_Through_Service_And_Removes_Repo_Item()
    {
        var (flow, logs) = await NewFlowAsync();
        await flow.AddItemAsync(TakeType.Normal, CancellationToken.None);
        await flow.AddItemAsync(TakeType.Normal, CancellationToken.None);

        var vm = new LogEditorViewModel(flow, logs.Items[0].Item, 0, [1]);
        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.True(vm.Deleted);
        Assert.Empty(logs.Items);
    }

    [Fact]
    public async Task Tag_Mutations_Update_Collection()
    {
        var (flow, _) = await NewFlowAsync();
        var vm = new LogEditorViewModel(flow, LogItem(), 0, [7]);

        vm.AddTag("X");
        vm.AddTag("X"); // 去重
        Assert.Equal(["缪尔赛斯", "塞雷娅", "X"], vm.TrackTags);

        vm.RenameTag(0, "Y");
        Assert.Equal("Y", vm.TrackTags[0]);

        vm.RemoveTag(1);
        Assert.Equal(["Y", "X"], vm.TrackTags);
    }
}