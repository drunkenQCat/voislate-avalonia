using VoiSlate.Models;

namespace VoiSlate.Desktop;

public sealed class WorkspaceDraft
{
    public int Take { get; set; } = 1;
    public int RetakeIndex { get; set; }
    public bool Ended { get; set; }
    public string RecordingNote { get; set; } = "";
    public string ShotNote { get; set; } = "";
    public List<string>? Tracks { get; set; }
    public TkStatus Sound { get; set; }
    public ShtStatus Picture { get; set; }
}

public sealed class WorkspaceSession
{
    public int Version { get; set; } = 1;
    public string Date { get; set; } = "";
    public string Scene { get; set; } = "";
    public string Shot { get; set; } = "";
    public int FileNumber { get; set; } = 1;
    public bool Linked { get; set; } = true;
    public bool PocketLock { get; set; }
    public bool LeftHanded { get; set; }
    public string Project { get; set; } = "NewProject";
    public WorkspaceNaming Naming { get; set; } = new();
    public Dictionary<string, int> FileCounters { get; set; } = [];
    public bool AutoBackup { get; set; } = true;
    public string BackupDirectory { get; set; } = "";
    public Dictionary<string, WorkspaceDraft> Drafts { get; set; } = [];
}

public sealed record WorkspaceLog(string Date, SlateLogItem Item)
{
    public string TakeLabel => Item.IsRetake ? $"补录 {Item.RetakeLabel ?? "A"}" : Item.IsFakeTake || (Item.Tk == 999 && Item.RecordedAt is null) ? "假条" : Item.Tk == 0 ? "补录 W" : $"TAKE {Item.Tk}";
    public string PictureLabel => Item.OkSht switch { ShtStatus.Ok => "画面过", ShtStatus.Nice => "画面保", _ => "画面未评" };
    public string SoundLabel => Item.OkTk switch { TkStatus.Ok => "声音可", TkStatus.Bad => "声音弃", _ => "声音未评" };
    public string TimeLabel => Item.RecordedAt?.ToLocalTime().ToString("HH:mm") ?? "时间未记录";
}

public sealed record WorkspaceGroup(string Date, string Scene, string Shot, IReadOnlyList<WorkspaceLog> Rows);
public enum ShotProgress { Waiting, Shooting, Done }
public enum LogFilter { All, Usable, Unreviewed, Muted, Retake }

public sealed class WorkspaceNaming
{
    public string Mode { get; set; } = "default";
    public string CustomPrefix { get; set; } = "custom";
    public string Linker { get; set; } = "-T";
    public string Prefix(string date)
    {
        if (Mode == "custom") return CustomPrefix;
        return Mode == "sound devices" && date.Length == 6 ? $"{date[..2]}Y{date[2..4]}M{date[4..]}" : date;
    }
    public void Validate()
    {
        if (CustomPrefix is null || Linker is null) throw new InvalidOperationException("文件命名配置不完整");
        if (Mode is not ("default" or "sound devices" or "custom")) throw new InvalidOperationException("请选择有效的文件命名模式");
        if (Mode == "custom" && (string.IsNullOrWhiteSpace(CustomPrefix) || CustomPrefix.Length > 100 || CustomPrefix.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)) throw new InvalidOperationException("文件名前缀不能为空，且不能包含文件名非法字符");
        if (Linker.Length > 20 || Linker.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0) throw new InvalidOperationException("分隔符过长或包含文件名非法字符");
    }
}

public sealed class WorkspaceUndo
{
    public WorkspaceSession State { get; set; } = new();
    public int LogId { get; set; }
    public string Date { get; set; } = "";
}

public sealed class BackupPlanItem
{
    public string StableId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Append { get; set; } = "";
    public List<string> Objects { get; set; } = [];
    public List<BackupPlanItem> Shots { get; set; } = [];
}
public sealed class WorkspaceBackup
{
    public int Version { get; set; } = 2;
    public string Project { get; set; } = "";
    public DateTimeOffset ExportedAt { get; set; }
    public List<BackupPlanItem> Plan { get; set; } = [];
    public List<WorkspaceBackupDay> Days { get; set; } = [];
    public WorkspaceSession? Session { get; set; }
}
public sealed class WorkspaceBackupDay
{
    public string Date { get; set; } = "";
    public List<SlateLogItem> Takes { get; set; } = [];
}
public sealed record WorkspaceImport(WorkspaceBackup Backup, bool Legacy, int UndatedCount)
{
    public int TakeCount => Backup.Days.Sum(d => d.Takes.Count);
}
