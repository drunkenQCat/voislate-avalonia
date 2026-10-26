using System.Text.Json;
using VoiSlate.Desktop;
using VoiSlate.Models;
using VoiSlate.Services;
using Xunit;

namespace VoiSlate.Tests.Desktop;

public sealed partial class DesktopWorkspaceTests
{
    [Fact]
    public async Task Full_backup_restores_plan_drafts_names_tracks_and_end_markers_and_merge_is_idempotent()
    {
        using var f = await Create(); var s = f.Workspace;
        await s.SetPreferencesAsync("备份工程", true, true); await s.SetNamingAsync(new() { Mode = "custom", CustomPrefix = "REC" });
        await s.SetTracksAsync("Boom，演员甲"); await s.SaveTakeAsync(true);
        var exported = s.ExportJson(); var preview = DesktopWorkspaceService.PrepareImport(exported, s.Today);
        Assert.Equal(2, preview.Backup.Version); Assert.Equal(1, preview.TakeCount);
        await s.ImportAsync(preview, false); Assert.Single(s.Logs);
        await s.ClearPlanAsync(); await s.ClearLogsAsync(true);
        await s.ImportAsync(preview, true);
        Assert.Equal("备份工程", s.State.Project); Assert.True(s.State.PocketLock); Assert.True(s.Draft.Ended); Assert.Equal("REC-T002", s.CurrentFile);
        Assert.Equal(new[] { "Boom", "演员甲" }, Assert.Single(s.Logs).Item.Tracks); Assert.Equal(2, s.Scenes.Count);
        var restarted = f.NewWorkspace(); await restarted.InitializeAsync(); Assert.True(restarted.Draft.Ended); Assert.Equal(ShotProgress.Done, restarted.Progress("1A", "1A"));
    }
    [Fact]
    public async Task Flutter_array_accepts_string_enums_and_legacy_tracks_and_explicit_undated_archive_date()
    {
        using var f = await Create(); var s = f.Workspace;
        const string legacy = """
        [{"scn":"1A","sht":"1A","tk":999,"filenamePrefix":"custom","filenameLinker":"-T","filenameNum":8,"tkNote":"Fake Take","shtNote":"正文<Boom/>","scnNote":"场","okTk":"bad","okSht":"notChecked"}]
        """;
        var preview = DesktopWorkspaceService.PrepareImport(legacy, "261023"); Assert.True(preview.Legacy); Assert.Equal(1, preview.UndatedCount);
        await s.ImportAsync(preview, false); var row = Assert.Single(s.Logs); Assert.Equal("261023", row.Date); Assert.True(row.Item.IsFakeTake); Assert.Equal("正文", row.Item.ShtNote); Assert.Equal(new[] { "Boom" }, row.Item.Tracks);
        using var output = JsonDocument.Parse(s.ExportFlutterJson()); Assert.Equal("bad", output.RootElement[0].GetProperty("okTk").GetString()); Assert.Contains("<Boom/>", output.RootElement[0].GetProperty("shtNote").GetString());
        var roundtrip = DesktopWorkspaceService.PrepareImport(s.ExportFlutterJson(), "261023");
        Assert.Equal("正文", roundtrip.Backup.Days[0].Takes[0].ShtNote);
        Assert.Equal(new[] { "Boom" }, roundtrip.Backup.Days[0].Takes[0].Tracks);
        var dated = legacy.Replace("custom", "26Y10M22"); Assert.Equal("261022", DesktopWorkspaceService.PrepareImport(dated, "261023").Backup.Days[0].Date);
    }
    [Fact]
    public async Task Invalid_import_and_conflicting_merge_do_not_change_database_or_workspace()
    {
        using var f = await Create(); var s = f.Workspace; await s.SaveTakeAsync(); var original = s.ExportJson();
        Assert.Throws<InvalidDataException>(() => DesktopWorkspaceService.PrepareImport("{}", s.Today));
        var preview = DesktopWorkspaceService.PrepareImport(original, s.Today); preview.Backup.Days[0].Takes[0].FilenameNum = 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => s.ImportAsync(preview, true)); Assert.Equal(original, s.ExportJson());
        preview = DesktopWorkspaceService.PrepareImport(original, s.Today); preview.Backup.Days[0].Takes[0].TkNote = "冲突修改";
        preview.Backup.Plan.Add(new() { Name = "99A", Shots = [new() { Name = "1" }] });
        await Assert.ThrowsAsync<InvalidDataException>(() => s.ImportAsync(preview, false));
        Assert.Equal(original, s.ExportJson()); var restarted = f.NewWorkspace(); await restarted.InitializeAsync(); Assert.Equal(2, restarted.Scenes.Count); Assert.Empty(Assert.Single(restarted.Logs).Item.TkNote);
    }
    private sealed class FailingPlan(IScheduleStore inner) : IScheduleStore
    {
        public Task<IReadOnlyList<SceneSchedule>> LoadAllAsync() => inner.LoadAllAsync();
        public Task ClearAsync() => inner.ClearAsync();
        public async Task SaveAllAsync(IReadOnlyList<SceneSchedule> scenes) { await inner.SaveAllAsync(scenes); throw new IOException("simulated write failure"); }
    }
    [Fact]
    public async Task Restore_rolls_back_prior_deletions_when_a_store_write_fails()
    {
        using var f = await Create(); await f.Workspace.SaveTakeAsync(); var original = f.Workspace.ExportJson();
        var broken = new DesktopWorkspaceService(new FailingPlan(new LiteDbScheduleStore(f.Db)), f.Logs, new LiteDbSessionSettingsStore(f.Db), f.Clock, new LiteDbWorkspaceTransaction(f.Db));
        await broken.InitializeAsync(); var preview = DesktopWorkspaceService.PrepareImport(original, broken.Today);
        await Assert.ThrowsAsync<IOException>(() => broken.ImportAsync(preview, true));
        var restarted = f.NewWorkspace(); await restarted.InitializeAsync(); Assert.Equal(original, restarted.ExportJson());
    }
    [Fact]
    public async Task Desktop_v1_import_resumes_take_number_and_preserves_end_status()
    {
        using var f = await Create(); var s = f.Workspace;
        await s.SetTakeAsync(8); await s.SaveTakeAsync(true);
        var preview = DesktopWorkspaceService.PrepareImport(s.ExportJson(), s.Today);
        preview.Backup.Version = 1; preview.Backup.Session = null;
        foreach (var scene in preview.Backup.Plan) { scene.StableId = ""; foreach (var shot in scene.Shots) shot.StableId = ""; }
        foreach (var item in preview.Backup.Days.SelectMany(d => d.Takes)) { item.ShotId = null; item.SceneId = null; }
        await s.ImportAsync(preview, true); Assert.True(s.Draft.Ended);
        await s.SelectShotAsync("1A", "1A"); Assert.Equal(9, s.Draft.Take);
    }
    [Fact]
    public async Task Backup_files_are_complete_unique_and_leave_no_temporary_files()
    {
        using var f = await Create(); var s = f.Workspace; await s.SaveTakeAsync();
        var directory = Path.Combine(Path.GetTempPath(), "VoiSlate-backup-test-" + Guid.NewGuid().ToString("N"));
        await s.SetBackupPreferencesAsync(true, directory);
        var one = await s.WriteBackupAsync("auto"); var two = await s.WriteBackupAsync("exit");
        Assert.NotEqual(one, two); Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        Assert.Equal(1, DesktopWorkspaceService.PrepareImport(await File.ReadAllTextAsync(one), s.Today).TakeCount);
        File.Delete(one); File.Delete(two); Directory.Delete(directory);
    }
}
