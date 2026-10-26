using Avalonia.Controls;
using Avalonia.Headless;
using VoiSlate.Desktop;
using VoiSlate.Models;
using VoiSlate.Services;
using Xunit;

namespace VoiSlate.Tests.Desktop;

/// <summary>
/// P7 遗留缺口收口（docs/p7-vm-coverage.md 阶段 1+2）：旧会话迁移、记条备注不串味、
/// 计划删除守卫、撤回栈不再被计划操作清空、按日导出、音量键通道。
/// </summary>
public sealed partial class DesktopWorkspaceTests
{
    private sealed class FakeHardwareKeys : IHardwareKeyService
    {
        public event Action<HardwareKey>? KeyPressed;
        public void Raise(HardwareKey key) => KeyPressed?.Invoke(key);
    }

    [Fact]
    public async Task Legacy_project_naming_linker_and_draft_are_adopted_on_first_launch()
    {
        using var f = new Fixture();
        var settings = new LiteDbSessionSettingsStore(f.Db);
        await settings.SetAsync(DesktopWorkspaceService.LegacyProjectKey, "老工程 · 万星园");
        await settings.SetAsync(SessionKeys.PrefixType, "sound devices");
        await settings.SetAsync(SessionKeys.RecordLinker, "_");
        await settings.SetAsync(SessionKeys.IsLinked, false);
        await settings.SetAsync(SessionKeys.Desc, "旧录音标注");
        await settings.SetAsync(SessionKeys.Note, "旧镜头标注");
        await settings.SetAsync(SessionKeys.OkTk, 1);
        await settings.SetAsync(SessionKeys.OkSht, 2);

        await f.Start();
        var s = f.Workspace;
        Assert.Equal("老工程 · 万星园", s.State.Project);
        Assert.Equal("sound devices", s.State.Naming.Mode);
        Assert.Equal("_", s.State.Naming.Linker);
        Assert.False(s.State.Linked);
        Assert.Equal("旧录音标注", s.Draft.RecordingNote);
        Assert.Equal("旧镜头标注", s.Draft.ShotNote);
        Assert.Equal(TkStatus.Ok, s.Draft.Sound);
        Assert.Equal(ShtStatus.Nice, s.Draft.Picture);

        // 第二次启动走存档分支，不会把迁移结果覆盖回默认值。
        var again = f.NewWorkspace();
        await again.InitializeAsync();
        Assert.Equal("老工程 · 万星园", again.State.Project);
        Assert.Equal("旧录音标注", again.Draft.RecordingNote);
    }

    [Fact]
    public async Task Invalid_legacy_naming_falls_back_instead_of_blocking_startup()
    {
        using var f = new Fixture();
        var settings = new LiteDbSessionSettingsStore(f.Db);
        await settings.SetAsync(SessionKeys.PrefixType, "custom");
        await settings.SetAsync(SessionKeys.CustomPrefix, "");
        await f.Start();
        Assert.Equal("default", f.Workspace.State.Naming.Mode);
    }

    [Fact]
    public async Task Notes_go_to_the_saved_take_and_the_next_draft_starts_empty()
    {
        using var f = await Create();
        var s = f.Workspace;
        await s.SaveDraftAsync("录音一", "镜头一");
        await s.SaveTakeAsync();
        var first = Assert.Single(s.Logs).Item;
        Assert.Equal("录音一", first.TkNote);
        Assert.Equal("镜头一", first.ShtNote);
        Assert.Empty(s.Draft.RecordingNote);
        Assert.Empty(s.Draft.ShotNote);

        await s.SaveDraftAsync("录音二", "镜头二");
        await s.SaveTakeAsync(end: true);
        await s.NextShotAsync();
        Assert.Empty(s.Draft.RecordingNote);
        Assert.Empty(s.Draft.ShotNote);
    }

    [Fact]
    public async Task Used_file_numbers_list_existing_rows_for_the_same_naming()
    {
        using var f = await Create();
        var s = f.Workspace;
        await s.SaveTakeAsync();
        await s.SaveTakeAsync();
        Assert.Equal(new[] { 1, 2 }, s.UsedFileNumbers(s.FilePrefix, s.State.Naming.Linker).ToArray());
        Assert.Empty(s.UsedFileNumbers("999999", "-X"));
    }

    [Fact]
    public async Task Day_export_contains_only_that_day()
    {
        using var f = new Fixture();
        await f.Logs.AddAsync("261023", "261023-T007", new SlateLogItem { Scn = "9", Sht = "9A", Tk = 1, FilenamePrefix = "261023", FilenameLinker = "-T", FilenameNum = 7, TkNote = "昨天的条", ShtNote = "" });
        await f.Start();
        var s = f.Workspace;
        await s.SaveTakeAsync();

        Assert.Equal(1, s.DayCount("261023"));
        Assert.Equal(1, s.DayCount(s.Today));
        var day = s.ExportDayJson("261023");
        using var doc = System.Text.Json.JsonDocument.Parse(day);
        Assert.Equal(1, doc.RootElement.GetArrayLength());
        Assert.Equal("昨天的条", doc.RootElement[0].GetProperty("tkNote").GetString());
        using var all = System.Text.Json.JsonDocument.Parse(s.ExportFlutterJson());
        Assert.Equal(2, all.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Plan_deletion_keeps_at_least_one_scene_and_one_shot()
    {
        using var f = await Create();
        var s = f.Workspace;
        await s.DeletePlanItemAsync("2A", null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.DeletePlanItemAsync("1A", null));

        var scene = Assert.Single(s.Scenes);
        while (scene.Count > 1) await s.DeletePlanItemAsync(scene.Info.Name, scene[scene.Count - 1].Name);
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.DeletePlanItemAsync(scene.Info.Name, scene[0].Name));
        Assert.Single(s.Scenes);
        Assert.Single(s.Scenes[0]);
    }

    [Fact]
    public async Task Deleting_a_plan_item_keeps_the_take_undo_history()
    {
        using var f = await Create();
        var s = f.Workspace;
        await s.SaveTakeAsync();
        await s.SaveTakeAsync();
        Assert.True(s.CanUndo);
        await s.DeletePlanItemAsync("2A", null);
        Assert.True(s.CanUndo);
        await s.UndoAsync();
        Assert.Single(s.Logs);
    }

    [Fact]
    public async Task Undo_after_the_current_shot_is_deleted_lands_on_a_live_shot()
    {
        using var f = await Create();
        var s = f.Workspace;
        await s.SelectShotAsync("1A", "2B");
        await s.SaveTakeAsync();
        await s.DeletePlanItemAsync("1A", "2B");
        await s.UndoAsync();
        Assert.NotNull(s.Scene);
        Assert.NotNull(s.Shot);
        await s.SaveTakeAsync();
        Assert.Single(s.Logs);
    }

    [Fact]
    public async Task Next_plan_key_continues_the_existing_numbering()
    {
        using var f = await Create();
        var s = f.Workspace;
        Assert.Equal("3", s.NextPlanKey(null, true));
        Assert.Equal("4A", s.NextPlanKey("1A", false));
        await s.SavePlanItemAsync("1A", null, "4", "天台", "内容", "", isScene: false);
        Assert.Equal("5A", s.NextPlanKey("1A", false));
    }

    [Fact]
    public Task Record_page_notes_stay_with_their_own_take() => RunUi(async () =>
    {
        using var f = await Create();
        var keys = new FakeHardwareKeys();
        HardwareKeys.Current = keys;
        var window = await WindowFor(f);
        try
        {
            var box = Control<TextBox>(window, "recording-note");
            box.Text = "第一条备注";
            await Until(() => f.Workspace.Draft.RecordingNote == "第一条备注");
            keys.Raise(HardwareKey.VolumeUp);
            await Until(() => f.Workspace.Logs.Count == 1);

            // 记条后输入框必须回到空（R1：上一条备注不能被写进下一条）
            Assert.Equal(string.Empty, Control<TextBox>(window, "recording-note").Text);
            Assert.Empty(f.Workspace.Draft.RecordingNote);

            Control<TextBox>(window, "recording-note").Text = "第二条备注";
            await Until(() => f.Workspace.Draft.RecordingNote == "第二条备注");
            keys.Raise(HardwareKey.VolumeUp);
            await Until(() => f.Workspace.Logs.Count == 2);

            Assert.Equal("第一条备注", f.Workspace.Logs[0].Item.TkNote);
            Assert.Equal("第二条备注", f.Workspace.Logs[1].Item.TkNote);
        }
        finally { HardwareKeys.Current = null; window.Close(); }
    });

    [Fact]
    public Task Volume_up_saves_a_take_and_volume_down_undoes_it() => RunUi(async () =>
    {
        using var f = await Create();
        var keys = new FakeHardwareKeys();
        HardwareKeys.Current = keys;
        try
        {
            var window = await WindowFor(f);
            try
            {
                keys.Raise(HardwareKey.VolumeUp);
                await Until(() => f.Workspace.Logs.Count == 1);
                keys.Raise(HardwareKey.VolumeDown);
                await Until(() => f.Workspace.Logs.Count == 0);
            }
            finally { window.Close(); }
        }
        finally { HardwareKeys.Current = null; }
    });
}