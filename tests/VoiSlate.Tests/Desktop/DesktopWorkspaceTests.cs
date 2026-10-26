using System.Text.Json;
using VoiSlate.Data;
using VoiSlate.Desktop;
using VoiSlate.Infrastructure;
using VoiSlate.Models;
using VoiSlate.Services;
using Xunit;

namespace VoiSlate.Tests.Desktop;

public sealed partial class DesktopWorkspaceTests
{
    private sealed class Clock : ITimeProvider { public DateTime Now { get; set; } = new(2026, 10, 24, 10, 30, 0); }
    private sealed class Fixture : IDisposable
    {
        public LiteDbStore Db { get; } = new(":memory:");
        public Clock Clock { get; } = new();
        public DesktopWorkspaceService Workspace { get; private set; } = null!;
        public LiteDbLogRepository Logs => new(Db);
        public DesktopWorkspaceService NewWorkspace() => new(new LiteDbScheduleStore(Db),Logs,new LiteDbSessionSettingsStore(Db),Clock,new LiteDbWorkspaceTransaction(Db));
        public async Task Start()
        {
            await new LiteDbScheduleStore(Db).SaveAllAsync([SeedData.SceneSchedule1A(), SeedData.SceneSchedule2A()]);
            Workspace=NewWorkspace(); await Workspace.InitializeAsync();
        }
        public void Dispose()=>Db.Dispose();
    }
    private static async Task<Fixture> Create() { var f=new Fixture();await f.Start();return f; }
    [Fact]
    public async Task Undo_keeps_later_preferences_and_manual_retake_number_is_independent_of_take()
    {
        using var f = await Create(); var s = f.Workspace;
        await s.SaveTakeAsync(); await s.SetPreferencesAsync("新工程名", true, true); await s.UndoAsync();
        Assert.Equal("新工程名", s.State.Project); Assert.True(s.State.PocketLock); Assert.True(s.State.LeftHanded);
        await s.ToggleRetakeAsync(); await s.SetRetakeIndexAsync(26); await s.SaveTakeAsync();
        Assert.Equal("AA", Assert.Single(s.Logs).Item.RetakeLabel); Assert.Equal(1, s.Draft.Take);
    }
    [Fact]
    public async Task First_desktop_launch_preserves_legacy_file_high_water_mark()
    {
        using var f = new Fixture();
        var settings = new LiteDbSessionSettingsStore(f.Db);
        await settings.SetAsync(SessionKeys.Date, "261024"); await settings.SetAsync(SessionKeys.RecordCount, 42);
        await f.Start(); Assert.Equal(42, f.Workspace.State.FileNumber);
    }
    [Fact]
    public async Task Empty_notes_remain_non_null_when_read_from_the_database()
    {
        using var f = await Create(); var s = f.Workspace;
        await s.SaveTakeAsync();
        var item = Assert.Single(s.Logs).Item;
        Assert.Equal(string.Empty, item.TkNote); Assert.Equal(string.Empty, item.ShtNote);
        var reloaded = f.NewWorkspace(); await reloaded.InitializeAsync();
        Assert.Equal(string.Empty, Assert.Single(reloaded.Logs).Item.ShtNote);
    }
    [Fact]
    public async Task First_click_saves_current_take_and_metadata_in_real_repository()
    {
        using var f=await Create();var s=f.Workspace;
        await s.SaveDraftAsync("声音干净 <原文>","视线接上");await s.SetPictureAsync(ShtStatus.Ok);await s.SetSoundAsync(TkStatus.Ok);
        await s.SaveTakeAsync();
        var row=Assert.Single(await f.Logs.GetByDateAsync("261024"));
        Assert.Equal("261024-T001",row.FileName);Assert.Equal(1,row.Tk);Assert.Equal("声音干净 <原文>",row.TkNote);
        Assert.Equal("视线接上",row.ShtNote);Assert.Equal("万星园",row.Location);Assert.Equal("近景",row.ShotType);
        Assert.Equal(new DateTimeOffset(f.Clock.Now),row.RecordedAt);Assert.Equal(2,s.Draft.Take);Assert.Equal(2,s.State.FileNumber);
        Assert.Empty(s.Draft.RecordingNote);Assert.Empty(s.Draft.ShotNote);
    }
    [Fact]
    public async Task Undo_restores_numbers_both_notes_ratings_and_retake_mode()
    {
        using var f=await Create();var s=f.Workspace;
        await s.ToggleRetakeAsync();await s.SaveDraftAsync("补录环境","空镜");await s.SetSoundAsync(TkStatus.Bad);await s.SaveTakeAsync();
        Assert.Equal("B",DesktopWorkspaceService.RetakeLetter(s.Draft.RetakeIndex));
        await s.UndoAsync();Assert.Empty(s.Logs);Assert.Equal(1,s.State.FileNumber);Assert.Equal(1,s.Draft.Take);
        Assert.False(s.State.Linked);Assert.Equal(0,s.Draft.RetakeIndex);Assert.Equal(TkStatus.Bad,s.Draft.Sound);
        Assert.Equal("补录环境",s.Draft.RecordingNote);Assert.Equal("空镜",s.Draft.ShotNote);
    }
    [Fact]
    public async Task End_saves_last_take_once_preserves_existing_rating_and_can_reopen()
    {
        using var f=await Create();var s=f.Workspace;
        await s.SetSoundAsync(TkStatus.Bad);await s.SaveDraftAsync("风声","保画面");await s.SaveTakeAsync(true);
        var item=Assert.Single(s.Logs).Item;Assert.True(item.IsShotEnd);Assert.Equal(ShtStatus.Nice,item.OkSht);Assert.Equal(TkStatus.Bad,item.OkTk);
        Assert.Equal(ShotProgress.Done,s.Progress("1A","1A"));await Assert.ThrowsAsync<InvalidOperationException>(()=>s.SaveTakeAsync(true));
        await s.SelectShotAsync("1A","1A");Assert.False(s.Draft.Ended);Assert.Equal(2,s.Draft.Take);await s.SaveTakeAsync();Assert.Equal(2,s.Logs.Count);
    }
    [Fact]
    public async Task Browse_query_and_switch_preserve_per_shot_drafts_and_resume_take_number()
    {
        using var f=await Create();var s=f.Workspace;
        await s.SaveDraftAsync("原镜录音","原镜标注");await s.SetPictureAsync(ShtStatus.Nice);
        s.QueryLogs(null,"不存在",LogFilter.Muted);Assert.Equal("1A",s.State.Shot);
        await s.SelectShotAsync("1A","2B");await s.SaveDraftAsync("第二镜","另一草稿");await s.SelectShotAsync("1A","1A");
        Assert.Equal("原镜录音",s.Draft.RecordingNote);Assert.Equal("原镜标注",s.Draft.ShotNote);Assert.Equal(ShtStatus.Nice,s.Draft.Picture);
        var restarted=f.NewWorkspace();await restarted.InitializeAsync();Assert.Equal("原镜录音",restarted.Draft.RecordingNote);
        await restarted.SelectShotAsync("1A","2B");Assert.Equal("另一草稿",restarted.Draft.ShotNote);
    }
    [Fact]
    public async Task Deletion_and_clear_never_reuse_file_numbers_even_after_restart()
    {
        using var f=await Create();var s=f.Workspace;
        await s.SaveTakeAsync();await s.SaveTakeAsync();await s.DeleteLogAsync(s.Logs[0]);Assert.Equal(3,s.State.FileNumber);
        await s.ClearLogsAsync(true);var restarted=f.NewWorkspace();await restarted.InitializeAsync();Assert.Equal(3,restarted.State.FileNumber);
        await restarted.SaveTakeAsync();Assert.Equal("261024-T003",Assert.Single(restarted.Logs).Item.FileName);
        await restarted.ToggleRetakeAsync();await Assert.ThrowsAsync<InvalidOperationException>(()=>restarted.SetNumberAsync(2));
    }
    [Fact]
    public async Task Cross_day_scoped_edit_uses_original_row_identity_and_preserves_others()
    {
        using var f=await Create();var s=f.Workspace;
        await s.SaveDraftAsync("昨天","old");await s.SaveTakeAsync();var yesterday=s.Logs[0];
        f.Clock.Now=f.Clock.Now.AddDays(1);await s.SaveDraftAsync("今天","new");await s.SaveTakeAsync();
        Assert.Equal("261025-T001",s.Logs[^1].Item.FileName);
        await s.EditLogAsync(yesterday,"历史复核","修正",ShtStatus.Ok,TkStatus.Bad);
        Assert.Equal("历史复核",(await f.Logs.GetByDateAsync("261024"))[0].TkNote);
        Assert.Equal("今天",(await f.Logs.GetByDateAsync("261025"))[0].TkNote);
        Assert.Equal(2,s.State.FileNumber);
    }
    [Fact]
    public async Task Grouping_preserves_A_B_A_chronology_and_supports_filters()
    {
        using var f=await Create();var s=f.Workspace;
        await s.SaveTakeAsync();await s.SelectShotAsync("1A","2B");await s.SaveTakeAsync();await s.SelectShotAsync("1A","1A");await s.ToggleRetakeAsync();await s.SaveTakeAsync();
        var groups=s.QueryLogs();Assert.Equal(new[]{"1A","2B","1A"},groups.Select(g=>g.Shot));
        var retake=Assert.Single(s.QueryLogs(filter:LogFilter.Retake));Assert.Equal("补录 A",Assert.Single(retake.Rows).TakeLabel);
        Assert.Single(s.QueryLogs(query:"T002"));Assert.Empty(s.QueryLogs(query:"不匹配"));
    }
    [Fact]
    public async Task Csv_parses_both_formats_quoted_fields_and_merges_without_duplicate_shots()
    {
        using var f=await Create();var s=f.Workspace;
        var simple=DesktopWorkspaceService.ParsePlan("\uFEFF场号,镜号,类型,内容\n12A,1,近景,\"含逗号,和换行\n文本\"\n12A,2,特写,对白");
        Assert.Equal(2,simple[0].Count);Assert.Contains('\n',simple[0][0].Note.Append);
        await s.MergePlanAsync(simple);await s.MergePlanAsync(simple);Assert.Equal(2,s.Scenes.Single(x=>x.Info.Name=="12A").Count);
        var engineering=DesktopWorkspaceService.ParsePlan("场次,概要,镜号,备注,景别,内容,附注\n15A实验室,会面,1,日,近景,走入,脚步\n,,2,,特写,对白,");
        Assert.Equal("实验室",engineering[0].Info.Note.Type);Assert.Equal(2,engineering[0].Count);
        Assert.Throws<InvalidDataException>(()=>DesktopWorkspaceService.ParsePlan("1A,1,近景"));
        Assert.Equal(3,s.Scenes.Count);
    }
    [Fact]
    public async Task Empty_plan_survives_reload_and_can_be_rebuilt_without_losing_logs()
    {
        using var f=await Create();var s=f.Workspace;
        await s.SaveTakeAsync();await s.ClearPlanAsync();var reloaded=f.NewWorkspace();await reloaded.InitializeAsync();
        Assert.Empty(reloaded.Scenes);Assert.Single(reloaded.Logs);await Assert.ThrowsAsync<InvalidOperationException>(()=>reloaded.SaveTakeAsync());
        await reloaded.SavePlanItemAsync(null,null,"12A","场地","概要","Boom，演员",true);
        Assert.Equal("12A",reloaded.State.Scene);Assert.Equal("1A",reloaded.State.Shot);await reloaded.SaveTakeAsync();Assert.Equal(2,reloaded.Logs.Count);
    }
    [Fact]
    public async Task Preferences_export_and_metadata_persist_across_reinitialization()
    {
        using var f=await Create();var s=f.Workspace;
        await s.SetPreferencesAsync("测试工程",true,true);await s.ToggleRetakeAsync();await s.SaveTakeAsync(true);
        var reloaded=f.NewWorkspace();await reloaded.InitializeAsync();Assert.Equal("测试工程",reloaded.State.Project);Assert.True(reloaded.State.PocketLock);Assert.True(reloaded.State.LeftHanded);
        var item=Assert.Single(reloaded.Logs).Item;Assert.True(item.IsRetake);Assert.True(item.IsShotEnd);Assert.Equal("A",item.RetakeLabel);Assert.Equal("万星园",item.Location);
        using var export=JsonDocument.Parse(reloaded.ExportJson());Assert.Equal("测试工程",export.RootElement.GetProperty("project").GetString());
        Assert.Equal(2,export.RootElement.GetProperty("plan").GetArrayLength());Assert.Equal("261024",export.RootElement.GetProperty("days")[0].GetProperty("date").GetString());
    }
}
