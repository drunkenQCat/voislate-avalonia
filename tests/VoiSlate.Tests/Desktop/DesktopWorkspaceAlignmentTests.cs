using System.Text.Json;
using VoiSlate.Desktop;
using VoiSlate.Desktop.Speech;
using VoiSlate.Models;
using VoiSlate.Services;
using Xunit;

namespace VoiSlate.Tests.Desktop;

public sealed partial class DesktopWorkspaceTests
{
    [Fact]
    public async Task Stable_identity_keeps_history_and_draft_after_rename_and_distinguishes_reused_names()
    {
        using var f = await Create(); var s = f.Workspace;
        await s.SaveTakeAsync(); var id = s.Logs[0].Item.ShotId;
        await s.SaveDraftAsync("留在此镜", "");
        await s.SavePlanItemAsync("1A", "1A", "12A", "新场地", "概要", "", true);
        await s.SavePlanItemAsync("12A", "1A", "8B", "近景", "改号", "", false);
        Assert.Equal("留在此镜", s.Draft.RecordingNote); Assert.Equal(id, s.Shot!.StableId);
        Assert.Single(s.QueryLogs(scene: "12A", shot: "8B")); Assert.Equal("8B", s.FindPlan(s.Logs[0])!.Value.Shot.Name);
        await s.DeletePlanItemAsync("12A", "8B");
        await s.SavePlanItemAsync("12A", null, "8B", "近景", "新镜头", "", false);
        Assert.Empty(s.QueryLogs(scene: "12A", shot: "8B"));
        await s.SelectShotAsync("12A", "8B"); Assert.Empty(s.Draft.RecordingNote);
        var reloaded = f.NewWorkspace(); await reloaded.InitializeAsync(); Assert.Empty(reloaded.QueryLogs(scene: "12A", shot: "8B"));
    }
    [Fact]
    public async Task Naming_counters_are_independent_persistent_and_custom_names_do_not_restart_at_midnight()
    {
        using var f = await Create(); var s = f.Workspace;
        await s.SaveTakeAsync();
        await s.SetNamingAsync(new() { Mode = "sound devices" }); Assert.Equal("26Y10M24-T001", s.CurrentFile); await s.SaveTakeAsync();
        await s.SetNamingAsync(new() { Mode = "custom", CustomPrefix = "REC", Linker = "_" }); await s.SaveTakeAsync();
        await s.ClearLogsAsync(true);
        var restarted = f.NewWorkspace(); await restarted.InitializeAsync(); Assert.Equal("REC_002", restarted.CurrentFile);
        f.Clock.Now = f.Clock.Now.AddDays(1); await restarted.SaveTakeAsync(); Assert.Equal("REC_002", Assert.Single(restarted.Logs).Item.FileName);
        await restarted.SetNamingAsync(new()); Assert.Equal("261025-T001", restarted.CurrentFile);
        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.SetNamingAsync(new() { Mode = "custom", CustomPrefix = "bad/path" }));
    }
    [Fact]
    public async Task Equivalent_file_stems_share_high_water_mark_even_after_logs_are_cleared()
    {
        using var f = await Create(); var s = f.Workspace;
        await s.SetNamingAsync(new() { Mode = "custom", CustomPrefix = "REC", Linker = "-T" }); await s.SaveTakeAsync();
        await s.ClearLogsAsync(true);
        await s.SetNamingAsync(new() { Mode = "custom", CustomPrefix = "REC-", Linker = "T" });
        Assert.Equal("REC-T002", s.CurrentFile); await s.SaveTakeAsync();
        await s.SetNamingAsync(new() { Mode = "custom", CustomPrefix = "REC", Linker = "-T" });
        await s.EditLogDetailsAsync(s.Logs[0], 1, 50, "", "", "", "", ShtStatus.Ok, TkStatus.Ok);
        Assert.Equal("REC-T051", s.CurrentFile);
    }
    [Fact]
    public async Task Several_undo_steps_survive_restart_and_restore_notes_ratings_and_numbers()
    {
        using var f = await Create(); var s = f.Workspace;
        for (int n = 1; n <= 3; n++) { await s.SaveDraftAsync("第" + n, "镜" + n); await s.SetSoundAsync(TkStatus.Ok); await s.SaveTakeAsync(); }
        var restarted = f.NewWorkspace(); await restarted.InitializeAsync();
        await restarted.SetPreferencesAsync("后来更改", true, true);
        for (int n = 3; n >= 1; n--)
        {
            Assert.True(restarted.CanUndo); await restarted.UndoAsync(); Assert.Equal(n - 1, restarted.Logs.Count);
            Assert.Equal(n, restarted.State.FileNumber); Assert.Equal("第" + n, restarted.Draft.RecordingNote); Assert.Equal(TkStatus.Ok, restarted.Draft.Sound);
        }
        Assert.False(restarted.CanUndo); Assert.Equal("后来更改", restarted.State.Project);
        await restarted.SaveTakeAsync(); Assert.Equal("261024-T001", Assert.Single(restarted.Logs).Item.FileName);
    }
    [Fact]
    public async Task Plan_insertion_movement_delete_and_restore_preserve_order_and_history()
    {
        using var f = await Create(); var s = f.Workspace; await s.SaveTakeAsync();
        await s.SavePlanItemAsync("1A", null, "19A", "特写", "插入镜", "", false, 0);
        Assert.Equal("19A", s.Scenes[0][0].Name);
        await s.MovePlanItemAsync("1A", "19A", 1); Assert.Equal("19A", s.Scenes[0][1].Name);
        await s.DeletePlanItemAsync("1A", "1A"); Assert.Single(s.Logs); Assert.True(s.CanRestorePlan);
        await s.RestorePlanItemAsync(); Assert.Equal("1A", s.Scenes[0][0].Name); Assert.False(s.CanRestorePlan);
        await s.MovePlanItemAsync("1A", null, 1);
        var restarted = f.NewWorkspace(); await restarted.InitializeAsync(); Assert.Equal("2A", restarted.Scenes[0].Info.Name); Assert.Equal("19A", restarted.Scenes[1][1].Name);
    }
    [Fact]
    public async Task Fake_take_has_bad_sound_and_advances_file_without_skipping_real_take()
    {
        using var f = await Create(); var s = f.Workspace; await s.SaveTakeAsync(fake: true);
        var row = Assert.Single(s.Logs); Assert.True(row.Item.IsFakeTake); Assert.Equal(999, row.Item.Tk); Assert.Equal("假条", row.TakeLabel);
        Assert.Equal(TkStatus.Bad, row.Item.OkTk); Assert.Equal("Fake Take", row.Item.TkNote); Assert.Equal(1, s.Draft.Take); Assert.Equal(2, s.State.FileNumber);
        await s.SaveTakeAsync(); Assert.Equal(1, s.Logs[1].Item.Tk);
    }
    [Fact]
    public async Task Track_snapshots_and_full_history_edits_persist_and_reject_duplicate_files()
    {
        using var f = await Create(); var s = f.Workspace;
        await s.SetTracksAsync("Boom，演员甲"); await s.SaveTakeAsync(); await s.SetTracksAsync("演员乙"); await s.SaveTakeAsync();
        var first = s.Logs[0]; Assert.Equal(new[] { "Boom", "演员甲" }, first.Item.Tracks);
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.EditLogDetailsAsync(first, 7, 2, "场", "X", "", "", ShtStatus.Ok, TkStatus.Ok));
        await s.EditLogDetailsAsync(first, 7, 50, "新场信息", "Boom，X", "复核", "标注", ShtStatus.Nice, TkStatus.Bad);
        var restarted = f.NewWorkspace(); await restarted.InitializeAsync(); var item = restarted.Logs[0].Item;
        Assert.Equal(50, item.FilenameNum); Assert.Equal(7, item.Tk); Assert.Equal("新场信息", item.ScnNote); Assert.Equal(new[] { "Boom", "X" }, item.Tracks); Assert.Equal(51, restarted.State.FileNumber);
    }
    [Fact]
    public async Task Legacy_track_tags_are_migrated_and_token_is_excluded_from_workspace_export()
    {
        using var f = await Create();
        await f.Logs.AddAsync("261023", "261023-T001", new() { Scn = "1A", Sht = "1A", Tk = 1, FilenamePrefix = "261023", FilenameNum = 1, ShtNote = "正文<Boom/><演员甲/>" });
        var speech = new BailianSettingsStore(new LiteDbSessionSettingsStore(f.Db), new WindowsTokenProtector()); await speech.SaveAsync("sk-private-secret", "", 0);
        var s = f.NewWorkspace(); await s.InitializeAsync();
        var item = Assert.Single(s.Logs).Item; Assert.Equal("正文", item.ShtNote); Assert.Equal(new[] { "Boom", "演员甲" }, item.Tracks);
        Assert.NotNull(item.ShotId); Assert.DoesNotContain("sk-private-secret", s.ExportJson()); Assert.DoesNotContain("bailian", s.ExportJson(), StringComparison.OrdinalIgnoreCase);
    }
}
