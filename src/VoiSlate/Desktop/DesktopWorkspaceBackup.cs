using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using VoiSlate.Models;

namespace VoiSlate.Desktop;

public sealed partial class DesktopWorkspaceService
{
    private static readonly JsonSerializerOptions ImportJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private WorkspaceBackup Snapshot() => new()
    {
        Project = State.Project, ExportedAt = new DateTimeOffset(clock.Now), Session = Clone(State),
        Plan = Scenes.Select(s => new BackupPlanItem
        {
            StableId = s.Info.StableId, Name = s.Info.Name, Type = s.Info.Note.Type, Append = s.Info.Note.Append, Objects = s.Info.Note.Objects.ToList(),
            Shots = s.Select(sh => new BackupPlanItem { StableId = sh.StableId, Name = sh.Name, Type = sh.Note.Type, Append = sh.Note.Append, Objects = sh.Note.Objects.ToList() }).ToList()
        }).ToList(),
        Days = Logs.GroupBy(l => l.Date).Select(g => new WorkspaceBackupDay { Date = g.Key, Takes = g.Select(l => Clone(l.Item)).ToList() }).ToList()
    };
    public string ExportJson() => JsonSerializer.Serialize(Snapshot(), Json);
    public string ExportFlutterJson() => JsonSerializer.Serialize(FlutterRows(Logs), ImportJson);
    /// <summary>只导出某一天（场记时间轴切到单日时用；原版按日导出，现役此前永远全量）。</summary>
    public string ExportDayJson(string date) => JsonSerializer.Serialize(FlutterRows(Logs.Where(row => row.Date == date)), ImportJson);
    public int DayCount(string date) => Logs.Count(row => row.Date == date);
    private static IEnumerable<object> FlutterRows(IEnumerable<WorkspaceLog> rows) => rows.Select(row => (object)new
    {
        row.Item.Scn, row.Item.Sht, Tk = row.Item.IsRetake ? 0 : row.Item.Tk,
        row.Item.FilenamePrefix, row.Item.FilenameLinker, row.Item.FilenameNum,
        row.Item.TkNote,
        ShtNote = row.Item.ShtNote + string.Concat((row.Item.Tracks ?? []).Select(t => "<" + t + "/>")),
        row.Item.ScnNote, row.Item.OkTk, row.Item.OkSht
    });
    private static bool ValidDate(string? date) => date is not null && Regex.IsMatch(date, @"^\d{6}$") && DateTime.TryParseExact(date, "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    private static string? InferDate(SlateLogItem item)
    {
        if (item.RecordedAt is { } stamp) return stamp.ToLocalTime().ToString("yyMMdd");
        if (ValidDate(item.FilenamePrefix)) return item.FilenamePrefix;
        var sd = Regex.Match(item.FilenamePrefix ?? "", @"^(\d{2})Y(\d{2})M(\d{2})$");
        var date = sd.Success ? sd.Groups[1].Value + sd.Groups[2].Value + sd.Groups[3].Value : null;
        return ValidDate(date) ? date : null;
    }
    public static WorkspaceImport PrepareImport(string json, string fallbackDate)
    {
        if (!ValidDate(fallbackDate)) throw new InvalidDataException("无日期记录的归档日期需为六位年月日，例如 261024");
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
        bool legacy = doc.RootElement.ValueKind == JsonValueKind.Array; int undated = 0; WorkspaceBackup backup;
        if (legacy)
        {
            var items = JsonSerializer.Deserialize<List<SlateLogItem>>(json, ImportJson) ?? throw new InvalidDataException("场记文件为空");
            backup = new();
            foreach (var item in items)
            {
                if (item is null) throw new InvalidDataException("场记文件包含空条目");
                var date = InferDate(item); if (date is null) { date = fallbackDate; undated++; }
                var day = backup.Days.FirstOrDefault(d => d.Date == date);
                if (day is null) { day = new() { Date = date }; backup.Days.Add(day); }
                backup.Days.Single(d => d.Date == date).Takes.Add(item);
            }
        }
        else
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("version", out var version) || !version.TryGetInt32(out var v) || v is not (1 or 2) || !doc.RootElement.TryGetProperty("days", out _) || !doc.RootElement.TryGetProperty("plan", out _))
                throw new InvalidDataException("不是支持的 VoiSlate 备份（支持桌面 v1/v2 或 Flutter 场记数组）");
            backup = JsonSerializer.Deserialize<WorkspaceBackup>(json, ImportJson) ?? throw new InvalidDataException("备份为空");
        }
        ValidateBackup(backup);
        return new(backup, legacy, undated);
    }
    private static ScheduleItem PlanItem(BackupPlanItem value)
    {
        var match = Regex.Match(value.Name ?? "", @"^(\d{1,4})([A-Z]?)$");
        if (!match.Success) throw new InvalidDataException("备份计划的场镜编号无效");
        if (value.Objects is null || value.Objects.Any(x => x is null) || value.Type is null || value.Append is null) throw new InvalidDataException("备份计划的内容无效");
        return new(match.Groups[1].Value, match.Groups[2].Value, new(value.Objects.ToList(), value.Type, value.Append)) { StableId = string.IsNullOrWhiteSpace(value.StableId) ? Guid.NewGuid().ToString("N") : value.StableId };
    }
    private static void ValidateBackup(WorkspaceBackup backup)
    {
        if (backup.Plan is null || backup.Days is null || backup.Project is null) throw new InvalidDataException("备份缺少必要数据");
        var ids = new HashSet<string>(); var scenes = new HashSet<string>();
        foreach (var scene in backup.Plan)
        {
            if (scene is null || scene.Shots is null || scene.Shots.Any(s => s is null)) throw new InvalidDataException("备份计划包含空条目");
            var info = PlanItem(scene); scene.StableId = info.StableId;
            if (!scenes.Add(scene.Name) || !ids.Add(info.StableId)) throw new InvalidDataException("备份包含重复的场次或标识");
            var shots = new HashSet<string>();
            foreach (var shot in scene.Shots)
            {
                var item = PlanItem(shot); shot.StableId = item.StableId;
                if (!shots.Add(shot.Name) || !ids.Add(item.StableId)) throw new InvalidDataException("备份包含重复的镜头或标识");
            }
        }
        var keys = new HashSet<string>();
        foreach (var day in backup.Days)
        {
            if (day is null || !ValidDate(day.Date) || day.Takes is null) throw new InvalidDataException("备份归档日期或条目无效");
            foreach (var item in day.Takes)
            {
                if (item is null || string.IsNullOrWhiteSpace(item.Scn) || string.IsNullOrWhiteSpace(item.Sht) || string.IsNullOrWhiteSpace(item.FilenamePrefix) || item.FilenameLinker is null || item.FilenameNum < 1 || item.FilenameNum == int.MaxValue || item.Tk < 0 || item.Tk > 9999 || !Enum.IsDefined(item.OkSht) || !Enum.IsDefined(item.OkTk)) throw new InvalidDataException("备份包含无效场记、编号或评价");
                if (!keys.Add(day.Date + "/" + item.FileName)) throw new InvalidDataException("备份同一天中存在重复文件名：" + item.FileName);
                item.TkNote ??= ""; item.ShtNote ??= ""; item.ScnNote ??= "";
                if (item.Tracks?.Any(t => t is null) == true) throw new InvalidDataException("备份轨道列表无效");
                if (item.RecordedAt is null)
                {
                    item.IsFakeTake |= item.Tk == 999;
                    if (item.Tracks is null)
                    {
                        item.Tracks = Regex.Matches(item.ShtNote, @"<([^<>/]+)/>").Select(m => m.Groups[1].Value).Distinct().ToList();
                        item.ShtNote = Regex.Replace(item.ShtNote, @"<([^<>/]+)/>", "");
                    }
                }
            }
        }
        if (backup.Session is { } session)
        {
            if (session.Naming is null || session.FileCounters is null || session.Drafts is null || session.Project is null || session.FileNumber < 1 || session.FileNumber == int.MaxValue || session.FileCounters.Values.Any(n => n < 1 || n == int.MaxValue)) throw new InvalidDataException("备份会话数据无效");
            session.Naming.Validate();
            foreach (var draft in session.Drafts.Values)
                if (draft is null || draft.Take < 1 || draft.Take > 9999 || draft.RetakeIndex < 0 || draft.RetakeIndex > 9998 || draft.RecordingNote is null || draft.ShotNote is null || !Enum.IsDefined(draft.Sound) || !Enum.IsDefined(draft.Picture)) throw new InvalidDataException("备份草稿无效");
        }
    }
    public Task ImportAsync(WorkspaceImport import, bool replace) => Change(async () =>
    {
        // Revalidate a clone, so callers cannot bypass validation by mutating the preview object.
        var backup = Clone(import.Backup); ValidateBackup(backup);
        var incoming = backup.Plan.Select(s => new SceneSchedule(s.Shots.Select(PlanItem).ToList(), PlanItem(s))).ToList();
        var map = new Dictionary<string, string>();
        if (replace)
        {
            foreach (var date in await logs.GetDatesAsync()) await logs.ClearAsync(date);
            Scenes.Clear(); Scenes.AddRange(incoming); await schedules.SaveAllAsync(Scenes);
            var local = State;
            State = backup.Session ?? new() { Project = backup.Project };
            State.BackupDirectory = local.BackupDirectory; State.AutoBackup = local.AutoBackup;
            foreach (var pair in local.FileCounters) State.FileCounters[pair.Key] = Math.Max(State.FileCounters.GetValueOrDefault(pair.Key, 1), pair.Value);
        }
        else
        {
            foreach (var scene in incoming)
            {
                var existing = Scenes.FirstOrDefault(s => s.Info.StableId == scene.Info.StableId) ?? Scenes.FirstOrDefault(s => s.Info.Name == scene.Info.Name);
                if (existing is null) { Scenes.Add(scene); continue; }
                map[scene.Info.StableId] = existing.Info.StableId;
                foreach (var shot in scene)
                {
                    var old = existing.Items.FirstOrDefault(s => s.StableId == shot.StableId) ?? existing.Items.FirstOrDefault(s => s.Name == shot.Name);
                    if (old is null) existing.Add(shot); else map[shot.StableId] = old.StableId;
                }
            }
            await schedules.SaveAllAsync(Scenes);
        }
        var current = replace ? new List<WorkspaceLog>() : Logs.ToList();
        foreach (var day in backup.Days)
            foreach (var item in day.Takes)
            {
                if (item.SceneId is not null && map.TryGetValue(item.SceneId, out var sceneId)) item.SceneId = sceneId;
                if (item.ShotId is not null && map.TryGetValue(item.ShotId, out var shotId)) item.ShotId = shotId;
                if (item.ShotId is null)
                {
                    var found = FindPlan(new(day.Date, item));
                    if (found is { } plan) { item.SceneId = plan.Scene.Info.StableId; item.ShotId = plan.Shot.StableId; }
                }
                var duplicate = current.FirstOrDefault(l => l.Date == day.Date && l.Item.FileName == item.FileName);
                if (duplicate is not null)
                {
                    var old = Clone(duplicate.Item); old.SceneId = item.SceneId; old.ShotId = item.ShotId;
                    if (JsonSerializer.Serialize(old, Json) != JsonSerializer.Serialize(item, Json)) throw new InvalidDataException("同名场记内容冲突：" + item.FileName + "。可选择恢复备份以整体替换。");
                    continue;
                }
                await logs.AddAsync(day.Date, item.FileName, item); current.Add(new(day.Date, item));
            }
        await ReloadLogs(); _undo.Clear(); _deletedPlan = null;
        if (Scene is null || Shot is null)
        {
            State.Scene = Scenes.FirstOrDefault(s => s.Count > 0)?.Info.Name ?? "";
            State.Shot = Scene?.Items.FirstOrDefault()?.Name ?? "";
        }
        foreach (var scene in Scenes)
            foreach (var shot in scene)
            {
                var rows = Logs.Where(l => MatchesShot(l, scene.Info.Name, shot.Name)).ToList();
                if (rows.Count == 0) continue;
                var nextTake = rows.Where(l => !l.Item.IsFakeTake && !l.Item.IsRetake && l.Item.Tk > 0).Select(l => l.Item.Tk).DefaultIfEmpty(0).Max() + 1;
                if (!State.Drafts.TryGetValue(shot.StableId, out var draft))
                    State.Drafts[shot.StableId] = new() { Take = Math.Min(9999, nextTake), Ended = rows[^1].Item.IsShotEnd };
                else if (!draft.Ended) draft.Take = Math.Max(draft.Take, Math.Min(9999, nextTake));
            }
        State.Date = Today; State.FileNumber = Math.Max(State.FileCounters.GetValueOrDefault(CounterKey, 1), NextAvailableNumber());
    });
    public string BackupDirectory => string.IsNullOrWhiteSpace(State.BackupDirectory)
        ? Path.Combine(Environment.GetEnvironmentVariable("VOISLATE_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VoiSlate Logs"), "backups")
        : State.BackupDirectory;
    public Task SetBackupPreferencesAsync(bool automatic, string directory) => Change(() =>
    {
        if (directory.Length > 0 && !Path.IsPathFullyQualified(directory)) throw new InvalidOperationException("备份目录需要绝对路径");
        State.AutoBackup = automatic; State.BackupDirectory = directory; return Task.CompletedTask;
    });
    public async Task<string> WriteBackupAsync(string reason = "manual")
    {
        string content, directory;
        await _gate.WaitAsync();
        try { content = ExportJson(); directory = BackupDirectory; } finally { _gate.Release(); }
        Directory.CreateDirectory(directory);
        var name = "VoiSlate-" + clock.Now.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + (reason == "before-import" ? "before-import" : reason == "auto" ? "auto" : reason == "exit" ? "exit" : "manual") + "-" + Guid.NewGuid().ToString("N")[..8] + ".json";
        var path = Path.Combine(directory, name); var temporary = path + ".tmp";
        try { await File.WriteAllTextAsync(temporary, content); File.Move(temporary, path); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return path;
    }
}
