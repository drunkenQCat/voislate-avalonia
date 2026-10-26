using System.Text.RegularExpressions;
using VoiSlate.Models;

namespace VoiSlate.Desktop;

public sealed partial class DesktopWorkspaceService
{
    public string FilePrefix => State.Naming.Prefix(Today);
    private string CounterKey => FilePrefix + "\u001f" + State.Naming.Linker;
    public Task SetNamingAsync(WorkspaceNaming naming) => Change(() =>
    {
        naming.Validate(); State.FileCounters[CounterKey] = State.FileNumber;
        State.Naming = Clone(naming); State.FileNumber = NextAvailableNumber(); _undo.Clear(); return Task.CompletedTask;
    });
    public Task SetTracksAsync(string tracks) => Change(() =>
    {
        Draft.Tracks = tracks.Split([',', '，', '/', '、', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList(); return Task.CompletedTask;
    });
    public bool MatchesShot(WorkspaceLog row, string scene, string shot)
    {
        var planned = Scenes.FirstOrDefault(s => s.Info.Name == scene)?.Items.FirstOrDefault(s => s.Name == shot);
        return row.Item.ShotId is not null ? planned?.StableId == row.Item.ShotId : row.Item.Scn == scene && row.Item.Sht == shot;
    }
    public (SceneSchedule Scene, ScheduleItem Shot)? FindPlan(WorkspaceLog row)
    {
        foreach (var scene in Scenes)
            foreach (var shot in scene)
                if (MatchesShot(row, scene.Info.Name, shot.Name)) return (scene, shot);
        return null;
    }
    private async Task MigratePlanAndLogs()
    {
        bool changed = false;
        foreach (var scene in Scenes)
            foreach (var item in scene.Items.Prepend(scene.Info))
                if (item.StableId.Length == 0) { item.StableId = Guid.NewGuid().ToString("N"); changed = true; }
        if (changed) await schedules.SaveAllAsync(Scenes);
        foreach (var scene in Scenes)
            foreach (var shot in scene)
                if (State.Drafts.Remove(scene.Info.Name + "/" + shot.Name, out var draft) && !State.Drafts.ContainsKey(shot.StableId)) State.Drafts[shot.StableId] = draft;
        foreach (var row in Logs)
        {
            var item = row.Item; bool edited = false;
            if (item.ShotId is null)
            {
                var found = FindPlan(row);
                if (found is { } plan) { item.SceneId = plan.Scene.Info.StableId; item.ShotId = plan.Shot.StableId; edited = true; }
            }
            if (item.RecordedAt is null && item.Tracks is null)
            {
                var matches = Regex.Matches(item.ShtNote, @"<([^<>/]+)/>");
                item.Tracks = matches.Select(m => m.Groups[1].Value).Distinct().ToList();
                item.ShtNote = Regex.Replace(item.ShtNote, @"<([^<>/]+)/>", ""); edited = true;
            }
            if (edited) await logs.ReplaceAtAsync(row.Date, await FindLogIndex(row), item);
        }
    }
    public Task EditLogDetailsAsync(WorkspaceLog row, int take, int file, string sceneNote, string tracks, string recording, string shot, ShtStatus picture, TkStatus sound) => Change(async () =>
    {
        if (take < 0 || take > 9999 || file < 1 || file == int.MaxValue) throw new InvalidOperationException("次号或文件序号超出范围");
        var item = Clone(row.Item); item.Tk = take; item.FilenameNum = file; item.ScnNote = sceneNote;
        item.Tracks = tracks.Split([',', '，', '/', '、'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
        item.TkNote = recording; item.ShtNote = shot; item.OkSht = picture; item.OkTk = sound;
        if (Logs.Any(l => !(l.Date == row.Date && l.Item.Id == row.Item.Id) && l.Item.FileName == item.FileName)) throw new InvalidOperationException("文件名已被其他场记使用");
        await logs.ReplaceAtAsync(row.Date, await FindLogIndex(row), item);
        var key = item.FilenamePrefix + "\u001f" + item.FilenameLinker;
        State.FileCounters[key] = Math.Max(State.FileCounters.GetValueOrDefault(key, 1), file + 1);
        if (item.FilenamePrefix + item.FilenameLinker == FilePrefix + State.Naming.Linker) State.FileNumber = Math.Max(State.FileNumber, file + 1);
        _undo.Clear();
    });
    private (SceneSchedule Scene, ScheduleItem? Shot, int Index)? _deletedPlan;
    public bool CanRestorePlan => _deletedPlan is not null;
    public Task DeletePlanItemAsync(string sceneName, string? shotName) => Change(async () =>
    {
        var scene = Scenes.FirstOrDefault(s => s.Info.Name == sceneName) ?? throw new InvalidOperationException("场次已不存在");
        if (shotName is null)
        {
            // 原版守卫：计划不能删空，否则当场无法记条（SaveTakeAsync 会抛「请先添加并选择拍摄计划」）。
            if (Scenes.Count <= 1) throw new InvalidOperationException("拍摄计划至少保留一个场次，请先添加新场次");
            var index = Scenes.IndexOf(scene); _deletedPlan = (scene, null, index); Scenes.RemoveAt(index);
        }
        else
        {
            var index = scene.Items.ToList().FindIndex(s => s.Name == shotName);
            if (index < 0) throw new InvalidOperationException("镜头已不存在");
            if (scene.Count <= 1) throw new InvalidOperationException("每个场次至少保留一个镜头，请先添加新镜头");
            _deletedPlan = (scene, scene[index], index); scene.RemoveAt(index);
        }
        if (Scene is null || Shot is null)
        {
            State.Scene = Scenes.FirstOrDefault(s => s.Count > 0)?.Info.Name ?? "";
            State.Shot = Scene?.Items.FirstOrDefault()?.Name ?? "";
        }
        // 不在这里 _undo.Clear()：删计划与场记撤回栈无关，原版抹掉 100 步纯属副作用。
        await schedules.SaveAllAsync(Scenes);
    });
    public Task RestorePlanItemAsync() => Change(async () =>
    {
        var removed = _deletedPlan ?? throw new InvalidOperationException("没有可恢复的计划");
        if (removed.Shot is null)
        {
            if (Scenes.Any(s => s.Info.Name == removed.Scene.Info.Name)) throw new InvalidOperationException("同编号场次已存在，请先修改编号");
            Scenes.Insert(Math.Min(removed.Index, Scenes.Count), removed.Scene);
        }
        else
        {
            var target = Scenes.FirstOrDefault(s => s.Info.StableId == removed.Scene.Info.StableId) ?? throw new InvalidOperationException("所属场次已删除");
            target.Insert(Math.Min(removed.Index, target.Count), removed.Shot);
        }
        await schedules.SaveAllAsync(Scenes); _deletedPlan = null;
        if (Shot is null) { State.Scene = removed.Scene.Info.Name; State.Shot = removed.Shot?.Name ?? removed.Scene.Items.FirstOrDefault()?.Name ?? ""; }
    });
    public Task MovePlanItemAsync(string sceneName, string? shotName, int direction) => Change(async () =>
    {
        if (direction is not (-1 or 1)) throw new InvalidOperationException("移动方向无效");
        var scene = Scenes.FirstOrDefault(s => s.Info.Name == sceneName) ?? throw new InvalidOperationException("场次不存在");
        if (shotName is null)
        {
            var from = Scenes.IndexOf(scene); var to = from + direction;
            if (to < 0 || to >= Scenes.Count) return;
            Scenes.RemoveAt(from); Scenes.Insert(to, scene);
        }
        else
        {
            var from = scene.Items.ToList().FindIndex(s => s.Name == shotName); var to = from + direction;
            if (from < 0 || to < 0 || to >= scene.Count) return;
            var item = scene[from]; scene.RemoveAt(from); scene.Insert(to, item);
        }
        await schedules.SaveAllAsync(Scenes);
    });
}
