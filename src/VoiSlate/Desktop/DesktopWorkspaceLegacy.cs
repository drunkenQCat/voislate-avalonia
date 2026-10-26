using VoiSlate.Models;
using VoiSlate.Services;

namespace VoiSlate.Desktop;

/// <summary>
/// 旧版会话（Hive box 'scn_sht_tk' 13 键 + 'settings' 里的 project 键）一次性迁入工作台。
/// P7 的 ViewModel 是这些键唯一的写入方；页面删除后若不再读，老用户的工程名、文件名前缀、
/// 联动状态与未保存标注会静默丢失 —— 因此首次启动（尚无 desktop.workspace.v1 存档）时兜底接管。
/// </summary>
public sealed partial class DesktopWorkspaceService
{
    /// <summary>旧版工程名键（原版 Hive box 'settings' key 'project'）。</summary>
    public const string LegacyProjectKey = "project";

    private WorkspaceDraft? _legacyDraft;

    private async Task AdoptLegacySessionAsync()
    {
        var project = await settings.GetStringAsync(LegacyProjectKey);
        if (!string.IsNullOrWhiteSpace(project) && project.Trim() != "NewProject") State.Project = project.Trim();

        State.Linked = await settings.GetBoolAsync(SessionKeys.IsLinked, State.Linked);

        var naming = new WorkspaceNaming
        {
            Mode = await settings.GetStringAsync(SessionKeys.PrefixType, State.Naming.Mode),
            CustomPrefix = await settings.GetStringAsync(SessionKeys.CustomPrefix, State.Naming.CustomPrefix),
            Linker = await settings.GetStringAsync(SessionKeys.RecordLinker, State.Naming.Linker),
        };
        try
        {
            naming.Validate();
            if (naming.Mode != State.Naming.Mode || naming.CustomPrefix != State.Naming.CustomPrefix || naming.Linker != State.Naming.Linker)
            {
                State.Naming = naming;
                State.FileNumber = Math.Max(State.FileNumber, NextAvailableNumber());
            }
        }
        catch (InvalidOperationException)
        {
            // 旧值非法（自定义前缀为空、分隔符超长等）时保留默认命名，不让存档卡住启动。
        }

        var sound = await settings.GetIntAsync(SessionKeys.OkTk, 0);
        var picture = await settings.GetIntAsync(SessionKeys.OkSht, 0);
        _legacyDraft = new WorkspaceDraft
        {
            RecordingNote = await settings.GetStringAsync(SessionKeys.Desc, ""),
            ShotNote = await settings.GetStringAsync(SessionKeys.Note, ""),
            Sound = sound is >= 0 and <= 2 ? (TkStatus)sound : TkStatus.NotChecked,
            Picture = picture is >= 0 and <= 2 ? (ShtStatus)picture : ShtStatus.NotChecked,
        };
    }

    /// <summary>把旧会话草稿挂到当前镜头；当前镜头已有新草稿时不覆盖。</summary>
    private void AttachLegacyDraft()
    {
        if (_legacyDraft is null) return;
        if (State.Drafts.TryGetValue(DraftKey, out var existing) && (existing.RecordingNote.Length > 0 || existing.ShotNote.Length > 0))
        {
            _legacyDraft = null;
            return;
        }
        State.Drafts[DraftKey] = _legacyDraft;
        _legacyDraft = null;
    }

    /// <summary>下一个可用的计划编号：场次为纯数字，镜头沿用「数字+字母」惯例（如 3A）。</summary>
    public string NextPlanKey(string? sceneName, bool isScene)
    {
        var names = isScene ? Scenes.Select(s => s.Info.Name) : Scenes.FirstOrDefault(s => s.Info.Name == sceneName)?.Items.Select(s => s.Name) ?? [];
        var max = names.Select(n => System.Text.RegularExpressions.Regex.Match(n, @"^(\d{1,4})")).Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(0).Max();
        var next = Math.Clamp(max + 1, 1, 9999).ToString();
        return isScene ? next : next + "A";
    }

    /// <summary>某套命名下已被占用的文件序号（供场记编辑提示，避免撞号）。</summary>
    public IReadOnlyList<int> UsedFileNumbers(string prefix, string linker) => Logs
        .Where(l => l.Item.FilenamePrefix + l.Item.FilenameLinker == prefix + linker)
        .Select(l => l.Item.FilenameNum).Distinct().Order().ToList();
}