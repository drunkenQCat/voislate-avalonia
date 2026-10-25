using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using VoiSlate.Models;
using VoiSlate.Services;

namespace VoiSlate.Desktop;

/// <summary>Windows workspace write boundary. Uses the existing stores, with current-take semantics.</summary>
public sealed partial class DesktopWorkspaceService(IScheduleStore schedules, ILogRepository logs,
    ISessionSettingsStore settings, ITimeProvider clock, IWorkspaceTransaction? transaction = null)
{
    public const string SessionKey = "desktop.workspace.v1";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private readonly List<WorkspaceUndo> _undo = [];
    private const string UndoKey = "desktop.workspace.undo.v1";
    public WorkspaceSession State { get; private set; } = new();
    public List<SceneSchedule> Scenes { get; } = [];
    public List<WorkspaceLog> Logs { get; } = [];
    public event Action? Changed;
    public string Today => VoiSlateDates.TodayKey(clock.Now);
    public SceneSchedule? Scene => Scenes.FirstOrDefault(s => s.Info.Name == State.Scene);
    public ScheduleItem? Shot => Scene?.Items.FirstOrDefault(s => s.Name == State.Shot);
    private string DraftKey => Shot?.StableId ?? State.Scene + "/" + State.Shot;
    public WorkspaceDraft Draft
    {
        get
        {
            if (!State.Drafts.TryGetValue(DraftKey, out var draft)) State.Drafts[DraftKey] = draft = new();
            return draft;
        }
    }
    public bool CanUndo => _undo.Count > 0;
    public string CurrentFile => FilePrefix + State.Naming.Linker + State.FileNumber.ToString("D3");
    public string NextFile => FilePrefix + State.Naming.Linker + ((long)State.FileNumber + 1).ToString("D3");
    public static string RetakeLetter(int index) => index < 26 ? ((char)('A' + index)).ToString() : RetakeLetter(index / 26 - 1) + (char)('A' + index % 26);
    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Json), Json)!;

    public async Task InitializeAsync()
    {
        Scenes.AddRange(await schedules.LoadAllAsync());
        var saved = await settings.GetStringAsync(SessionKey);
        if (!string.IsNullOrEmpty(saved)) State = JsonSerializer.Deserialize<WorkspaceSession>(saved, Json) ?? throw new InvalidDataException("无法读取桌面会话存档");
        else if (await settings.GetStringAsync(SessionKeys.Date, "") == Today)
        {
            State.Date = Today;
            State.FileNumber = Math.Max(1, await settings.GetIntAsync(SessionKeys.RecordCount, 1));
        }
        await ReloadLogs();
        await MigratePlanAndLogs();
        var undo = await settings.GetStringAsync(UndoKey);
        if (!string.IsNullOrEmpty(undo)) _undo.AddRange(JsonSerializer.Deserialize<List<WorkspaceUndo>>(undo, Json) ?? []);
        await EnsureDate();
        if (Scene is null || Shot is null)
        {
            State.Scene = Scenes.FirstOrDefault(s => s.Count > 0)?.Info.Name ?? "";
            State.Shot = Scene?.Items.FirstOrDefault()?.Name ?? "";
        }
        State.FileNumber = Math.Max(State.FileNumber, NextAvailableNumber());
        await Persist();
    }
    private int NextAvailableNumber()
    {
        var max = Logs.Where(l => l.Item.FilenamePrefix + l.Item.FilenameLinker == FilePrefix + State.Naming.Linker).Select(l => l.Item.FilenameNum).DefaultIfEmpty(0).Max();
        return Math.Max(max == int.MaxValue ? max : max + 1, State.FileCounters.Where(p => p.Key.Replace("\u001f", "") == FilePrefix + State.Naming.Linker).Select(p => p.Value).DefaultIfEmpty(1).Max());
    }
    private async Task EnsureDate()
    {
        if (State.Date == Today) return;
        _undo.Clear();
        State.Date = Today;
        State.FileNumber = NextAvailableNumber();
        await Persist();
    }
    private async Task ReloadLogs()
    {
        Logs.Clear();
        foreach (var date in (await logs.GetDatesAsync()).Order(StringComparer.Ordinal))
            Logs.AddRange((await logs.GetByDateAsync(date)).Select(item => new WorkspaceLog(date, item)));
    }
    private async Task Persist()
    {
        State.FileCounters[State.Naming.Prefix(State.Date) + "\u001f" + State.Naming.Linker] = State.FileNumber;
        await settings.SetAsync(SessionKey, JsonSerializer.Serialize(State, Json));
        await settings.SetAsync(UndoKey, JsonSerializer.Serialize(_undo, Json));
    }
    private async Task Change(Func<Task> action)
    {
        await _gate.WaitAsync();
        var previous = Clone(State); var undo = Clone(_undo);
        var plan = Scenes.Select(s => new SceneSchedule(s.Items.Select(Clone).ToList(), Clone(s.Info))).ToList();
        var deleted = _deletedPlan; bool begun = false;
        try
        {
            transaction?.Begin(); begun = transaction is not null;
            await EnsureDate(); await action(); await Persist(); await ReloadLogs();
            transaction?.Commit(); begun = false;
        }
        catch
        {
            if (begun) transaction!.Rollback();
            State = previous; Scenes.Clear(); Scenes.AddRange(plan); _undo.Clear(); _undo.AddRange(undo); _deletedPlan = deleted;
            await ReloadLogs(); throw;
        }
        finally { _gate.Release(); }
        Changed?.Invoke();
    }
    public Task SaveDraftAsync(string recording, string shot) => ChangeNotes(recording, shot);
    private async Task ChangeNotes(string recording, string shot)
    {
        await _gate.WaitAsync();
        try { Draft.RecordingNote = recording; Draft.ShotNote = shot; await Persist(); }
        finally { _gate.Release(); }
    }
    public Task SetPictureAsync(ShtStatus value) => Change(() => { if (!Draft.Ended) Draft.Picture = value; return Task.CompletedTask; });
    public Task SetSoundAsync(TkStatus value) => Change(() => { if (!Draft.Ended) Draft.Sound = value; return Task.CompletedTask; });
    public Task SetPreferencesAsync(string project, bool pocket, bool left) => Change(() =>
    {
        State.Project = project.Trim().Length == 0 ? "未命名工程" : project.Trim(); State.PocketLock = pocket; State.LeftHanded = left;
        return Task.CompletedTask;
    });
    public Task ToggleRetakeAsync() => Change(() =>
    {
        if (Draft.Ended) throw new InvalidOperationException("请先选择下一镜或再拍一条");
        State.Linked = !State.Linked; _undo.Clear(); return Task.CompletedTask;
    });
    public Task SetNumberAsync(int value) => Change(() =>
    {
        if (State.Linked) throw new InvalidOperationException("请先进入补录模式再手动修改文件号");
        if (value < State.FileNumber) throw new InvalidOperationException("文件号不能回退到已使用或已删除的编号");
        if (value < 1 || value == int.MaxValue) throw new InvalidOperationException("文件号超出范围");
        State.FileNumber = value; _undo.Clear(); return Task.CompletedTask;
    });
    public Task SetTakeAsync(int value) => Change(() =>
    {
        if (value < 1 || value > 9999 || Draft.Ended) throw new InvalidOperationException("当前次号不可修改或超出范围");
        Draft.Take = value; _undo.Clear(); return Task.CompletedTask;
    });
    public Task SetRetakeIndexAsync(int index) => Change(() =>
    {
        if (State.Linked || Draft.Ended || index < 0 || index > 9998) throw new InvalidOperationException("请在补录模式下指定有效补录序号");
        Draft.RetakeIndex = index; _undo.Clear(); return Task.CompletedTask;
    });
    public Task SaveTakeAsync(bool end = false, bool fake = false) => Change(async () =>
    {
        if (Shot is null || Scene is null) throw new InvalidOperationException("请先添加并选择拍摄计划");
        if (Draft.Ended) throw new InvalidOperationException("本镜已结束，请前往下一镜或选择再拍一条");
        if (State.FileNumber == int.MaxValue || Draft.Take >= 9999) throw new InvalidOperationException("编号已到上限，请检查编号设置");
        if (Logs.Any(l => l.Item.FileName == CurrentFile)) throw new InvalidOperationException("文件号已存在，请指定未使用的编号");
        var before = Clone(State);
        var d = Draft;
        var item = new SlateLogItem
        {
            Scn = State.Scene, Sht = State.Shot, Tk = fake ? 999 : d.Take,
            SceneId = Scene.Info.StableId, ShotId = Shot.StableId, Tracks = (d.Tracks ?? Shot.Note.Objects).ToList(), IsFakeTake = fake,
            FilenamePrefix = FilePrefix, FilenameLinker = State.Naming.Linker, FilenameNum = State.FileNumber,
            TkNote = fake && d.RecordingNote.Length == 0 ? "Fake Take" : d.RecordingNote, ShtNote = d.ShotNote, ScnNote = Scene.Info.Note.Append,
            OkSht = fake ? ShtStatus.NotChecked : end && d.Picture == ShtStatus.NotChecked ? ShtStatus.Nice : d.Picture,
            OkTk = fake ? TkStatus.Bad : end && d.Sound == TkStatus.NotChecked ? TkStatus.Ok : d.Sound,
            IsRetake = !fake && !State.Linked, RetakeLabel = fake || State.Linked ? null : RetakeLetter(d.RetakeIndex),
            IsShotEnd = end, RecordedAt = new DateTimeOffset(clock.Now), Location = Scene.Info.Note.Type, ShotType = Shot.Note.Type,
        };
        await logs.AddAsync(Today, item.FileName, item);
        _undo.Add(new() { State = before, LogId = item.Id, Date = Today });
        if (_undo.Count > 100) _undo.RemoveAt(0);
        State.FileNumber++;
        if (end)
        {
            d.Ended = true; d.Picture = item.OkSht; d.Sound = item.OkTk;
        }
        else State.Drafts[DraftKey] = new WorkspaceDraft { Take = d.Take + (!fake && State.Linked ? 1 : 0), RetakeIndex = d.RetakeIndex + (!fake && !State.Linked ? 1 : 0), Tracks = d.Tracks?.ToList() };
    });
    public Task UndoAsync() => Change(async () =>
    {
        var snapshot = _undo.LastOrDefault() ?? throw new InvalidOperationException("没有可以撤回的场记");
        var day = await logs.GetByDateAsync(snapshot.Date);
        var index = day.ToList().FindIndex(l => l.Id == snapshot.LogId);
        if (index < 0 || index != day.Count - 1) throw new InvalidOperationException("历史记录已变化，无法撤回");
        await logs.RemoveAtAsync(snapshot.Date, index);
        var preferences = (State.Project, State.PocketLock, State.LeftHanded, State.AutoBackup, State.BackupDirectory);
        State = Clone(snapshot.State);
        (State.Project, State.PocketLock, State.LeftHanded, State.AutoBackup, State.BackupDirectory) = preferences;
        _undo.RemoveAt(_undo.Count - 1);
    });
    public Task SelectShotAsync(string scene, string shot) => Change(() =>
    {
        var target = Scenes.FirstOrDefault(s => s.Info.Name == scene)?.Items.FirstOrDefault(s => s.Name == shot);
        if (target is null) throw new InvalidOperationException("镜头不在当前计划中");
        if (State.Scene == scene && State.Shot == shot && !Draft.Ended) return Task.CompletedTask;
        State.Scene = scene; State.Shot = shot; State.Linked = true; _undo.Clear();
        var next = Logs.Where(l => MatchesShot(l, scene, shot) && !l.Item.IsFakeTake && !l.Item.IsRetake && l.Item.Tk > 0 && (l.Item.Tk != 999 || l.Item.RecordedAt is not null)).Select(l => l.Item.Tk).DefaultIfEmpty(0).Max() + 1;
        if (!State.Drafts.TryGetValue(DraftKey, out var d) || d.Ended) State.Drafts[DraftKey] = new() { Take = next };
        return Task.CompletedTask;
    });
    public async Task NextShotAsync()
    {
        var flat = Scenes.SelectMany(s => s.Items.Select(sh => (Scene: s.Info.Name, Shot: sh.Name))).ToList();
        var index = flat.FindIndex(s => s.Scene == State.Scene && s.Shot == State.Shot);
        if (index < 0 || index + 1 >= flat.Count) throw new InvalidOperationException("已到计划末尾，可在计划中添加或选择镜头");
        await SelectShotAsync(flat[index + 1].Scene, flat[index + 1].Shot);
    }
    public ShotProgress Progress(string scene, string shot)
    {
        if (State.Scene == scene && State.Shot == shot && !Draft.Ended) return ShotProgress.Shooting;
        if (State.Drafts.TryGetValue(Scenes.FirstOrDefault(s => s.Info.Name == scene)?.Items.FirstOrDefault(s => s.Name == shot)?.StableId ?? scene + "/" + shot, out var draft) && !draft.Ended) return ShotProgress.Shooting;
        var rows = Logs.Where(l => MatchesShot(l, scene, shot)).ToList();
        return rows.LastOrDefault()?.Item.IsShotEnd == true ? ShotProgress.Done : rows.Count > 0 ? ShotProgress.Shooting : ShotProgress.Waiting;
    }
    public IReadOnlyList<WorkspaceGroup> QueryLogs(string? date = null, string query = "", LogFilter filter = LogFilter.All, string? scene = null, string? shot = null)
    {
        var groups = new List<WorkspaceGroup>();
        var current = new List<WorkspaceLog>();
        string? key = null;
        foreach (var row in Logs)
        {
            var h = row.Item;
            var next = row.Date + "/" + h.Scn + "/" + h.Sht;
            if (next != key) { current = []; key = next; }
            if ((date is not null && row.Date != date) || (scene is not null && !MatchesShot(row, scene, shot ?? ""))) continue;
            if (!string.Join(' ', h.FileName, h.Scn, h.Sht, h.TkNote, h.ShtNote, h.Location, h.ScnNote, string.Join(" ", h.Tracks ?? [])).Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
            var pass = filter switch
            {
                LogFilter.Usable => h.OkSht != ShtStatus.NotChecked && h.OkTk == TkStatus.Ok,
                LogFilter.Unreviewed => h.OkSht == ShtStatus.NotChecked || h.OkTk == TkStatus.NotChecked,
                LogFilter.Muted => h.OkTk == TkStatus.Bad,
                LogFilter.Retake => h.IsRetake || h.Tk == 0,
                _ => true,
            };
            if (!pass) continue;
            if (current.Count == 0) groups.Add(new(row.Date, h.Scn, h.Sht, current));
            current.Add(row);
        }
        return groups;
    }
    private async Task<int> FindLogIndex(WorkspaceLog row)
    {
        var day = await logs.GetByDateAsync(row.Date);
        var index = day.ToList().FindIndex(h => h.Id == row.Item.Id);
        if (index < 0) throw new InvalidOperationException("这条场记已被删除");
        return index;
    }
    public Task EditLogAsync(WorkspaceLog row, string recording, string shot, ShtStatus picture, TkStatus sound) => Change(async () =>
    {
        var index = await FindLogIndex(row); var item = Clone(row.Item);
        item.TkNote = recording; item.ShtNote = shot; item.OkSht = picture; item.OkTk = sound;
        await logs.ReplaceAtAsync(row.Date, index, item); _undo.Clear();
    });
    public Task DeleteLogAsync(WorkspaceLog row) => Change(async () => { await logs.RemoveAtAsync(row.Date, await FindLogIndex(row)); _undo.Clear(); });
    public Task ClearLogsAsync(bool all) => Change(async () =>
    {
        foreach (var day in all ? await logs.GetDatesAsync() : new[] { Today }) await logs.ClearAsync(day);
        _undo.Clear();
    });
    public Task ClearPlanAsync() => Change(async () =>
    {
        await schedules.ClearAsync(); Scenes.Clear(); _deletedPlan = null; State.Scene = State.Shot = ""; State.Drafts.Clear(); _undo.Clear();
    });
    public Task SavePlanItemAsync(string? sceneName, string? oldName, string name, string type, string content, string objects, bool isScene, int? insertIndex = null) => Change(async () =>
    {
        var match = Regex.Match(name.Trim().ToUpperInvariant(), @"^(\d{1,4})([A-Z]?)$");
        if (!match.Success) throw new InvalidOperationException("编号需要 1–4 位数字加可选字母，例如 12A");
        var replacement = new ScheduleItem(match.Groups[1].Value, match.Groups[2].Value, new Note(objects.Split([',', '，', '/', '、'], StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).ToList(), type.Trim(), content.Trim()));
        replacement.StableId = Guid.NewGuid().ToString("N");
        var copy = Scenes.Select(s => new SceneSchedule(s.Items.ToList(), s.Info)).ToList();
        if (isScene)
        {
            if (copy.Any(s => s.Info.Name == replacement.Name && s.Info.Name != oldName)) throw new InvalidOperationException("场号已存在");
            var existing = copy.FirstOrDefault(s => s.Info.Name == oldName);
            if (existing is null) copy.Insert(Math.Clamp(insertIndex ?? copy.Count, 0, copy.Count), new SceneSchedule([new ScheduleItem("1", "A", new Note([], "近景", "")) { StableId = Guid.NewGuid().ToString("N") }], replacement));
            else { replacement.StableId = existing.Info.StableId; existing.Info = replacement; }
        }
        else
        {
            var target = copy.FirstOrDefault(s => s.Info.Name == sceneName) ?? throw new InvalidOperationException("请先选择场次");
            var index = target.Items.ToList().FindIndex(sh => sh.Name == oldName);
            if (index < 0) target.Insert(Math.Clamp(insertIndex ?? target.Count, 0, target.Count), replacement); else { replacement.StableId = target[index].StableId; target[index] = replacement; }
        }
        await schedules.SaveAllAsync(copy); Scenes.Clear(); Scenes.AddRange(copy);
        if (isScene && State.Scene == oldName)
        {
            foreach (var entry in State.Drafts.Where(x => x.Key.StartsWith(oldName + "/", StringComparison.Ordinal)).ToList())
            { State.Drafts[replacement.Name + entry.Key[oldName!.Length..]] = entry.Value; State.Drafts.Remove(entry.Key); }
            State.Scene = replacement.Name;
        }
        else if (!isScene && State.Scene == sceneName && State.Shot == oldName)
        { State.Shot = replacement.Name; }
        if (Scene is null) { State.Scene = copy[0].Info.Name; State.Shot = copy[0][0].Name; }
        _undo.Clear();
    });
    public Task MergePlanAsync(IReadOnlyList<SceneSchedule> imported) => Change(async () =>
    {
        var copy = Scenes.Select(s => new SceneSchedule(s.Items.ToList(), s.Info)).ToList();
        foreach (var source in imported)
        {
            var target = copy.FirstOrDefault(s => s.Info.Name == source.Info.Name);
            if (target is null) { source.Info.StableId = Guid.NewGuid().ToString("N"); foreach (var sh in source) sh.StableId = Guid.NewGuid().ToString("N"); copy.Add(source); continue; }
            foreach (var sh in source)
            {
                var index = target.Items.ToList().FindIndex(s => s.Name == sh.Name);
                if (index >= 0) { sh.StableId = target[index].StableId; target[index] = sh; } else { sh.StableId = Guid.NewGuid().ToString("N"); target.Add(sh); }
            }
            if (source.Info.Note.Type.Length > 0 || source.Info.Note.Append.Length > 0) { source.Info.StableId = target.Info.StableId; target.Info = source.Info; }
        }
        await schedules.SaveAllAsync(copy); Scenes.Clear(); Scenes.AddRange(copy);
        if (Scene is null && copy.Count > 0) { State.Scene = copy[0].Info.Name; State.Shot = copy[0][0].Name; }
    });
    public static IReadOnlyList<SceneSchedule> ParsePlan(string text)
    {
        using var reader = new StringReader(text.TrimStart('\uFEFF'));
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false, TrimOptions = TrimOptions.Trim });
        var result = new List<SceneSchedule>(); SceneSchedule? current = null; int line = 0;
        while (csv.Read())
        {
            line++; var row = csv.Parser.Record!;
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            if (line == 1 && Regex.IsMatch(row[0], "场|scene", RegexOptions.IgnoreCase) && !Regex.IsMatch(row[0], @"\d")) continue;
            if (row.Length != 4 && row.Length < 7) throw new InvalidDataException($"第 {line} 行需要四列简表或七列工程格式");
            var engineering = row.Length >= 7;
            if (row[0].Length > 0)
            {
                var match = Regex.Match(row[0], @"^(\d{1,4}[a-zA-Z]?)(.*)$");
                if (!match.Success) throw new InvalidDataException($"第 {line} 行场号无效");
                var name = match.Groups[1].Value.ToUpperInvariant();
                current = result.FirstOrDefault(s => s.Info.Name == name);
                if (current is null)
                {
                    var num = Regex.Match(name, @"^(\d+)([A-Z]?)$");
                    current = new([], new ScheduleItem(num.Groups[1].Value, num.Groups[2].Value, new Note(["Boom"], engineering ? match.Groups[2].Value.Trim() : "", engineering ? string.Join('，', new[] { row[1], row[3] }.Where(x => x.Length > 0)) : "")));
                    result.Add(current);
                }
            }
            if (current is null) throw new InvalidDataException($"第 {line} 行缺少场号");
            var shot = engineering ? row[2] : row[1]; if (shot.Length == 0) continue;
            var sh = Regex.Match(shot.ToUpperInvariant(), @"^(\d{1,4})([A-Z]?)$");
            if (!sh.Success) throw new InvalidDataException($"第 {line} 行镜号无效");
            var item = new ScheduleItem(sh.Groups[1].Value, sh.Groups[2].Value, new Note(["Boom"], engineering ? row[4] : row[2], engineering ? string.Join('；', new[] { row[5], row[6] }.Where(x => x.Length > 0)) : row[3]));
            var index = current.Items.ToList().FindIndex(s => s.Name == item.Name);
            if (index < 0) current.Add(item); else current[index] = item;
        }
        if (result.Count == 0 || result.Any(s => s.Count == 0)) throw new InvalidDataException("每个场次至少需要一条有效镜头");
        return result;
    }
}
