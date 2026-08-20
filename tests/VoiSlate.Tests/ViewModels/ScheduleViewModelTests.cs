using VoiSlate.Models;
using VoiSlate.Services;
using VoiSlate.Tests.TestDoubles;
using VoiSlate.ViewModels;
using Xunit;

namespace VoiSlate.Tests.VM;

/// <summary>
/// ScheduleViewModel：增删改/导入/移动 + undo + 选择联动（对齐原版 scene_schedule_page）。持久化经 IScheduleStore。
/// </summary>
public class ScheduleViewModelTests
{
    private sealed record Harness(
        ScheduleViewModel Vm,
        StubScheduleStore Store,
        StubCsvScheduleParser Parser,
        RecordingSessionViewModel Session);

    private static async Task<Harness> NewAsync(Action<StubScheduleStore>? seedStore = null)
    {
        var store = new StubScheduleStore();
        seedStore?.Invoke(store);
        var settings = new FakeSessionSettingsStore();
        var time = new FakeTimeProvider();
        var session = new RecordingSessionViewModel(settings, time);
        await session.Initialization;

        var parser = new StubCsvScheduleParser();
        var vm = new ScheduleViewModel(store, parser, session);
        await vm.LoadAsync();
        return new Harness(vm, store, parser, session);
    }

    private static SceneSchedule Scene1() => new(
        [ScheduleFactory.Sht("1", ""), ScheduleFactory.Sht("2", "")],
        ScheduleFactory.Scn("1", "A", "开场"));

    private static SceneSchedule Scene2() => new(
        [ScheduleFactory.Sht("1", ""), ScheduleFactory.Sht("2", ""), ScheduleFactory.Sht("3", "")],
        ScheduleFactory.Scn("2", "A", "过场"));

    [Fact]
    public async Task Load_Populates_Scenes_And_Syncs_Selection()
    {
        var h = await NewAsync(s => s.LoadResult = [Scene1(), Scene2()]);

        Assert.Equal(2, h.Vm.Scenes.Count);
        Assert.Equal(0, h.Vm.SelectedSceneIndex);

        h.Session.SelectScene(1);
        await h.Vm.LoadAsync();
        Assert.Equal(1, h.Vm.SelectedSceneIndex);
    }

    [Fact]
    public async Task ImportCsv_Replaces_Scenes_And_Persists()
    {
        var h = await NewAsync(s => s.LoadResult = [Scene1()]);
        h.Parser.Result = [Scene2()];

        await h.Vm.ImportCsvAsync(new MemoryStream("csv"u8.ToArray()));

        Assert.True(h.Parser.Called);
        Assert.Single(h.Vm.Scenes);
        Assert.Equal("2A", h.Vm.Scenes[0].Info.Name);
        Assert.Contains(h.Store.Saved, x => x.Info.Name == "2A");
    }

    [Fact]
    public async Task AddScene_Clones_Current_Scene_With_Next_Key_And_Placeholder_Shot()
    {
        var h = await NewAsync(s => s.LoadResult = [Scene1(), Scene2()]);
        h.Session.SelectScene(1); // 当前选中第 2 场 → 克隆其 note

        var added = h.Vm.AddScene();

        Assert.Equal(3, h.Vm.Scenes.Count);
        Assert.Equal("3", added.Info.Key);
        Assert.Equal("", added.Info.Fix);
        Assert.Equal("过场", added.Info.Note.Append); // 克隆当前场 note
        Assert.Single(added.Items);
        Assert.Equal("1", added.Items[0].Key);
        Assert.Equal("近景", added.Items[0].Note.Type);
        Assert.Contains(h.Store.Saved, x => x.Info.Key == "3"); // 持久化快照含新场
    }

    [Fact]
    public async Task FinalizeNewScene_Rebuilds_Default_Shot_From_Objects()
    {
        var h = await NewAsync(s => s.LoadResult = [Scene1()]);
        var added = h.Vm.AddScene();

        var edited = new ScheduleItem("3", "", new Note(["缪尔赛斯", "塞雷娅"], "日戏", "新场"));
        h.Vm.ApplySceneEdit(h.Vm.Scenes.Count - 1, edited); // 编辑器保存回写
        h.Vm.FinalizeNewScene(h.Vm.Scenes.Count - 1);

        var scene = h.Vm.Scenes[h.Vm.Scenes.Count - 1];
        Assert.Equal("1", scene.Items[0].Key);
        Assert.Equal("从缪尔赛斯，塞雷娅的【正面】拍【近景】", scene.Items[0].Note.Append);
    }

    [Fact]
    public async Task AddShot_Clones_Last_Shot_With_Next_Key()
    {
        var h = await NewAsync(s => s.LoadResult = [Scene1()]);

        var ok = h.Vm.AddShot();

        Assert.True(ok);
        var scene = h.Vm.Scenes[0];
        Assert.Equal(3, scene.Count);
        Assert.Equal("3", scene.Items[^1].Key);
        Assert.Equal("", scene.Items[^1].Fix);
        Assert.Equal(h.Vm.Scenes[0].Items[^2].Note.Append, scene.Items[^1].Note.Append);
    }

    private static SceneSchedule Scene3() => new(
        [ScheduleFactory.Sht("1", ""), ScheduleFactory.Sht("2", "")],
        ScheduleFactory.Scn("3", "A", "高潮"));

    [Fact]
    public async Task DeleteScene_Guards_Last_Scene_And_Adjusts_Session_Index()
    {
        var h = await NewAsync(s => s.LoadResult = [Scene1(), Scene2(), Scene3()]);
        h.Session.SelectScene(2);
        h.Session.SelectShot(1);

        Assert.True(h.Vm.DeleteScene(0, out _)); // 非末位删除：selected 2 > 0 → 减 1
        Assert.Equal(1, h.Session.SelectedSceneIndex);
        Assert.Equal(2, h.Vm.Scenes.Count);

        Assert.True(h.Vm.DeleteScene(1, out _)); // 删除“选中的最后一个场”→ 上移一位并镜/次归 0
        Assert.Equal(0, h.Session.SelectedSceneIndex);
        Assert.Equal(0, h.Session.SelectedShotIndex);
        Assert.Equal(0, h.Session.SelectedTakeIndex);
        Assert.Single(h.Vm.Scenes);

        Assert.False(h.Vm.DeleteScene(0, out var msg)); // 至少保留一个场
        Assert.Equal("至少保留一个场", msg);
    }

    [Fact]
    public async Task DeleteHook_Undo_Flag_And_Undo_Restores()
    {
        var h = await NewAsync(s => s.LoadResult = [Scene1(), Scene2()]);

        h.Vm.DeleteScene(1, out _);
        Assert.True(h.Vm.CanUndo);

        h.Vm.UndoCommand.Execute(null);

        Assert.Equal(2, h.Vm.Scenes.Count);
        Assert.Equal("2A", h.Vm.Scenes[1].Info.Name);
        Assert.False(h.Vm.CanUndo);
    }

    [Fact]
    public async Task DeleteShot_Guards_Last_Shot_And_Undo_Restores()
    {
        var h = await NewAsync(s => s.LoadResult = [Scene1()]); // 2 镜

        Assert.True(h.Vm.DeleteShot(1, out _)); // 删第 2 镜 → 余 1 镜
        Assert.Single(h.Vm.Scenes[0].Items);

        Assert.False(h.Vm.DeleteShot(0, out var guardMsg)); // 最后一镜守卫
        Assert.Equal("至少保留一个镜", guardMsg);

        h.Vm.AddShot(); // 恢复 2 镜
        Assert.True(h.Vm.DeleteShot(1, out _));
        h.Vm.UndoCommand.Execute(null);
        Assert.Equal(2, h.Vm.Scenes[0].Count);
    }

    [Fact]
    public async Task MoveScene_Adjusts_Session_To_Dragged_Index()
    {
        var h = await NewAsync(s => s.LoadResult = [Scene1(), Scene2()]);
        h.Session.SelectScene(0);

        h.Vm.MoveScene(0, 2); // Reorderable 语义：newIndex>oldIndex → 减 1 → 实际插到 1

        Assert.Equal(2, h.Vm.Scenes.Count);
        Assert.Equal("2A", h.Vm.Scenes[0].Info.Name);
        Assert.Equal(1, h.Session.SelectedSceneIndex);
        Assert.Equal(0, h.Session.SelectedShotIndex);
    }

    [Fact]
    public async Task MoveShot_Adjusts_Session_Shot()
    {
        var h = await NewAsync(s => s.LoadResult = [Scene1()]);
        h.Vm.AddShot(); // 3 镜
        h.Session.SelectShot(0);

        h.Vm.MoveShot(0, 2);

        Assert.Equal(3, h.Vm.Scenes[0].Count);
        Assert.Equal(1, h.Session.SelectedShotIndex);
        Assert.Equal(0, h.Session.SelectedTakeIndex);
    }

    [Fact]
    public async Task Selection_Change_Links_Session_And_Back()
    {
        var h = await NewAsync(s => s.LoadResult = [Scene1(), Scene2()]);

        h.Vm.SelectedSceneIndex = 1; // 用户选场 → 会话联动
        Assert.Equal(1, h.Session.SelectedSceneIndex);
        Assert.Equal(0, h.Session.SelectedShotIndex);
        Assert.Equal(0, h.Session.SelectedTakeIndex);

        h.Session.SelectShot(2); // 会话变化（记录页）→ 本 VM 跟随
        Assert.Equal(2, h.Vm.SelectedShotIndex);
    }

    [Fact]
    public async Task Scene_Edit_Applies_And_Shot_Edit_Rejects_Duplicate_Name()
    {
        var h = await NewAsync(s => s.LoadResult = [Scene1()]);

        // 原版 saveChanges：场级编辑不做重名检测（查重仅 DataList 镜级与新增场景路径）
        var renamed = new ScheduleItem("9", "B", new Note([], "日戏", "改"));
        Assert.True(h.Vm.ApplySceneEdit(0, renamed));
        Assert.Equal("9B", h.Vm.Scenes[0].Info.Name);

        // 镜级编辑走 DataList.update 查重：重名拒绝
        var dupShot = new ScheduleItem("2", "", new Note(["Boom"], "近景", "")); // 与既有第 2 镜同名
        Assert.False(h.Vm.ApplyShotEdit(0, 0, dupShot));
        Assert.True(h.Vm.ApplyShotEdit(0, 0, new ScheduleItem("9", "", new Note(["Boom"], "近景", ""))));
        Assert.Equal("9", h.Vm.Scenes[0].Items[0].Name);
    }
}