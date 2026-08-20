using VoiSlate.Models;
using VoiSlate.Services;
using VoiSlate.Tests.TestDoubles;
using VoiSlate.ViewModels;
using Xunit;

namespace VoiSlate.Tests.VM;

/// <summary>
/// RecordViewModel：13 键恢复/文件号显示/记条时序/撤回/音量键/命令（B5；真实 TakeFlowService + 既有 Fake）。
/// 注意：测试命名空间用 VoiSlate.Tests.VM（不得用 Tests.ViewModels——会遮蔽 VoiSlate.ViewModels 使既有 SmokeTests 无法解析）。
/// </summary>
public class RecordViewModelTests
{
    private sealed record Fixture(
        RecordViewModel Vm,
        RecordingSessionViewModel Session,
        FakeSessionSettingsStore Settings,
        FakeLogRepository Logs,
        FakePickerHistoryStore History,
        TakeFlowService Flow,
        TestHardwareKeyService Keys);

    private static async Task<Fixture> NewAsync(Action<FakeSessionSettingsStore>? seed = null)
    {
        var settings = new FakeSessionSettingsStore();
        seed?.Invoke(settings);
        var time = new FakeTimeProvider();
        var session = new RecordingSessionViewModel(settings, time);
        await session.Initialization;

        var logs = new FakeLogRepository();
        var history = new FakePickerHistoryStore();
        var fileNum = new FileNumberingService(time);
        var flow = new TakeFlowService(
            logs, history, session, fileNum, settings, time, new NoopHapticsService(), new NoopToastService())
        {
            SceneLabelProvider = () => "1A",
            ShotLabelProvider = () => "1A",
            CurrentObjectsProvider = () => ["缪尔赛斯", "塞雷娅"],
        };
        await flow.InitializeAsync(CancellationToken.None);

        var keys = new TestHardwareKeyService();
        var vm = new RecordViewModel(settings, flow, new MockAsrService(), keys, session, time, logs);
        return new Fixture(vm, session, settings, logs, history, flow, keys);
    }

    private static async Task<Fixture> ActiveAsync(Action<FakeSessionSettingsStore>? seed = null)
    {
        var fx = await NewAsync(seed);
        fx.Vm.Activate();
        await fx.Vm.HydrationTask;
        return fx;
    }

    [Fact]
    public async Task Activate_Hydrates_Thirteen_Keys_And_File_Display()
    {
        var fx = await ActiveAsync(s =>
        {
            s.Data[SessionKeys.IsLinked] = false;
            s.Data[SessionKeys.Date] = "260820";
            s.Data[SessionKeys.RecordCount] = 7;
            s.Data[SessionKeys.Desc] = "desc";
            s.Data[SessionKeys.Note] = "note";
            s.Data[SessionKeys.SceneIndex] = 0;
            s.Data[SessionKeys.ShotIndex] = 0;
            s.Data[SessionKeys.TakeIndex] = 0;
            s.Data[SessionKeys.RecordLinker] = "-X";
            s.Data[SessionKeys.PrefixType] = "custom";
            s.Data[SessionKeys.CustomPrefix] = "MY";
        });

        var vm = fx.Vm;
        Assert.False(vm.IsLinked);
        Assert.Equal("desc", vm.DescText);
        Assert.Equal("note", vm.ShotNoteText);
        Assert.Equal(7, vm.FileNumber);
        Assert.Equal("MY-X007", vm.CurrentFileNumber);
        Assert.Equal("MY", vm.PrefixText);
        Assert.Equal("-X", vm.LinkerText);
        Assert.Equal("MY-X006\n 录音标注...", vm.PreviewHint);
        Assert.Equal(7, fx.Session.RecordCount);
        Assert.Equal(200, vm.TakeCol.Items.Count);
    }

    [Fact]
    public async Task AdvanceTake_Writes_Log_Clears_Desc_And_Sets_Hint()
    {
        // 已恢复文件号（当日 2）→ 首按即写日志；对齐原版“记条后清空 desc”（首按也清空）
        var fx = await ActiveAsync(s =>
        {
            s.Data[SessionKeys.Date] = "260820";
            s.Data[SessionKeys.RecordCount] = 2;
        });
        var vm = fx.Vm;
        vm.DescText = "hello";

        await vm.AdvanceTakeAsync(TakeType.Normal); // 首按即写日志（number=2）

        var item = Assert.Single(fx.Logs.Items).Item;
        Assert.Equal("hello", item.TkNote);
        Assert.Equal("", vm.DescText);
        Assert.Equal("260820-T002\n 录音标注...", vm.PreviewHint); // 记完 T001（prevFileName）
        Assert.Equal(TkStatus.NotChecked, fx.Session.OkTk);
        Assert.Equal(3, fx.Session.RecordCount);
        Assert.Equal(3, vm.FileNumber);
        Assert.Equal("", await fx.Settings.GetStringAsync(SessionKeys.Desc, "x"));
    }

    [Fact]
    public async Task AdvanceTake_Fake_And_End_Set_Dedicated_Hints()
    {
        var fx = await ActiveAsync();
        var vm = fx.Vm;

        await vm.AdvanceTakeAsync(TakeType.Fake);
        Assert.Equal(RecordViewModel.HintFake, vm.PreviewHint);

        await vm.AdvanceTakeAsync(TakeType.End);
        Assert.Equal(RecordViewModel.HintEnd, vm.PreviewHint);
    }

    [Fact]
    public async Task RewindTake_Restores_Desc_And_ShotNote()
    {
        var fx = await ActiveAsync();
        var vm = fx.Vm;

        vm.DescText = "d1";
        vm.ShotNoteText = "s1";
        await vm.AdvanceTakeAsync(TakeType.Normal); // 无日志
        vm.DescText = "d2";
        await vm.AdvanceTakeAsync(TakeType.Normal); // log1 TkNote=d2 ShtNote=s1<缪尔赛斯/><塞雷娅/>

        await vm.RewindTakeAsync();

        Assert.Equal("d2", vm.DescText);
        Assert.Equal("s1", vm.ShotNoteText);
        Assert.Equal(2, vm.FileNumber);
    }

    [Fact]
    public async Task Volume_Up_Advances_Take_And_Scrolls_Next()
    {
        var fx = await ActiveAsync();

        fx.Keys.Raise(HardwareKey.VolumeUp);

        Assert.Single(fx.History.Entries);
        Assert.Equal("1", fx.History.Entries[0][2]); // normal 关键字 = TakeIndex+1 = 1
        Assert.Equal(1, fx.Vm.TakeCol.SelectedIndex); // ScrollNext
        Assert.Equal(1, fx.Session.SelectedTakeIndex);
        Assert.Equal(2, fx.Vm.FileNumber);
        Assert.Equal("", fx.Vm.DescText);
    }

    [Fact]
    public async Task Volume_Down_Rewinds_And_Does_Not_Scroll()
    {
        var fx = await ActiveAsync();
        var vm = fx.Vm;

        await vm.AdvanceTakeAsync(TakeType.Normal);
        vm.DescText = "d1";
        await vm.AdvanceTakeAsync(TakeType.Normal); // log1
        var tkIndexBefore = vm.TakeCol.SelectedIndex;

        fx.Keys.Raise(HardwareKey.VolumeDown);

        Assert.Equal("d1", vm.DescText); // RestoreNotes
        Assert.Equal(tkIndexBefore, vm.TakeCol.SelectedIndex); // 契约映射：VolumeDown 只 RewindTake
        Assert.Equal(2, vm.FileNumber);
    }

    [Fact]
    public async Task ToggleLink_Flips_And_Persists()
    {
        var fx = await ActiveAsync();
        var vm = fx.Vm;
        Assert.True(vm.IsLinked);

        vm.ToggleLinkCommand.Execute(null);

        Assert.False(vm.IsLinked);
        Assert.False(await fx.Settings.GetBoolAsync(SessionKeys.IsLinked, true));
    }

    [Fact]
    public async Task FileNumber_Edits_Go_Through_Service_And_Update_Display()
    {
        var fx = await ActiveAsync();
        var vm = fx.Vm;

        await vm.EditFileNumberAsync(50);
        Assert.Equal(50, vm.FileNumber);
        Assert.Equal("260820-T050", vm.CurrentFileNumber);
        Assert.Equal(50, await fx.Settings.GetIntAsync(SessionKeys.RecordCount, 0));

        await vm.EditLinkerAsync("-Y");
        Assert.Equal("-Y", vm.LinkerText);
        Assert.Equal("260820-Y050", vm.CurrentFileNumber);

        await vm.EditPrefixAsync(PrefixType.Custom, "PRE");
        Assert.Equal("PRE", vm.PrefixText);
        Assert.Equal("PRE-Y050", vm.CurrentFileNumber);
        Assert.Equal("PRE", await fx.Settings.GetStringAsync(SessionKeys.CustomPrefix, ""));
    }

    [Fact]
    public async Task EditFileNumber_Clamps_To_One()
    {
        var fx = await ActiveAsync();
        await fx.Vm.EditFileNumberAsync(0);
        Assert.Equal(1, fx.Vm.FileNumber);
    }

    [Fact]
    public async Task Column_Change_Syncs_Session_And_Scene_Change_Resets_Shot_Take()
    {
        var fx = await ActiveAsync();
        var vm = fx.Vm;

        vm.TakeCol.ScrollNext(isLinked: true);
        Assert.Equal(1, fx.Session.SelectedTakeIndex);

        vm.SceneCol.SetItems(["A", "B", "C"]);
        vm.ShotCol.SetItems(["1", "2"]);
        vm.SceneCol.SelectedIndex = 2;
        Assert.Equal(2, fx.Session.SelectedSceneIndex);
        Assert.Equal(0, fx.Session.SelectedShotIndex);
        Assert.Equal(0, fx.Session.SelectedTakeIndex);

        vm.ShotCol.SelectedIndex = 1;
        Assert.Equal(1, fx.Session.SelectedShotIndex);
        Assert.Equal(0, fx.Session.SelectedTakeIndex);
    }

    [Fact]
    public async Task Session_Change_Syncs_Columns_Back()
    {
        var fx = await ActiveAsync();
        var vm = fx.Vm;
        fx.Session.SelectScene(1);
        Assert.Equal(1, vm.SceneCol.SelectedIndex);

        fx.Session.SelectTake(5);
        Assert.Equal(5, vm.TakeCol.SelectedIndex);
    }

    [Fact]
    public async Task Deactivate_Unsubscribes_Hardware_Keys_And_File_Number()
    {
        var fx = await ActiveAsync();
        fx.Vm.Deactivate();

        fx.Keys.Raise(HardwareKey.VolumeUp);
        Assert.Empty(fx.History.Entries); // 已取消订阅 → 不记条
    }

    [Fact]
    public async Task Activate_Is_Idempotent()
    {
        var fx = await NewAsync();
        fx.Vm.Activate();
        fx.Vm.Activate();
        await fx.Vm.HydrationTask;
        Assert.True(fx.Vm.IsLinked); // 无异常即可
    }
}