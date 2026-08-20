using VoiSlate.Models;

namespace VoiSlate.Services;

/// <summary>撤回结果（B11：恢复的备注文本；VM 据此回填 TextBox）。</summary>
public sealed record RewindResult(string RestoredDesc, string RestoredShotNote, bool WasOkMarkerOnly);

/// <summary>
/// 记录流服务（唯一写入口，契约 v0.5 B1-B5/B7/B11 在此实现）。
/// - B1 记条：以 picker_history 尾为"上一拍"（空栈视为 ['0','0','0']，B2）；首按 normal 时开拍。
/// - B3 联动：shot 变更或 end 时自动优良（pending tk=Ok / sht=Nice）。
/// - B4 假条/wild：fake→tk=999+'Fake Take'；wild→tk=0+'wild track ' 前缀；未联动时除 end 外自动转 wild。
/// - B7 文件号：唯一实例（C-2），NumberChanged 转发为 FileNumberChanged；编辑经 SetFileNumberAsync 等（BLOCKER-1）。
/// - B11 撤回：OK 尾只弹哨兵并恢复备注；否则递减文件号+弹历史尾+删末条+恢复备注。
/// </summary>
public interface ITakeFlowService
{
    Task AddItemAsync(TakeType type, CancellationToken ct,
        string? tkNoteOverride = null, string? shtNoteOverride = null);

    Task<RewindResult> RewindAsync(CancellationToken ct);

    Task SaveEditAsync(SlateLogItem item, int index, CancellationToken ct);
    Task DeleteItemAsync(int index, CancellationToken ct);

    Task SetFileNumberAsync(int value, CancellationToken ct);
    Task SetLinkerAsync(string linker, CancellationToken ct);
    Task SetPrefixAsync(PrefixType mode, string? customPrefix, CancellationToken ct);

    /// <summary>初始化：从设置恢复文件号/链接符/前缀/待定评价（对齐原版 SlateStatusNotifier 各字段读取）。</summary>
    Task InitializeAsync(CancellationToken ct);

    event Action? LogsChanged;
    event Action<int>? FileNumberChanged;
    event Action? HistoryChanged;
}