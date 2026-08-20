// VoiSlatePalette — 主题资源键常量（与 Themes/VoiSlatePalette.axaml 一一对应）。
//
// 契约 §6 主题资源键（必须逐字一致）：
//   VoiSlate.Bg / VoiSlate.Primary(bahamaBlue #0067A0) / VoiSlate.OkGreen /
//   VoiSlate.BadRed / VoiSlate.NiceGold / VoiSlate.TextHint
// 其余 VoiSlate.* 键为控件默认外观的补充键（取色自 Flutter 原版源码，见 axaml 注释）。

namespace VoiSlate.Controls;

/// <summary>VoiSlatePalette.axaml 资源键常量（供控件代码与测试引用）。</summary>
public static class VoiSlatePalette
{
    // ---- 契约 §6 必选键 ----
    public const string BgKey = "VoiSlate.Bg";
    public const string PrimaryKey = "VoiSlate.Primary";
    public const string OkGreenKey = "VoiSlate.OkGreen";
    public const string BadRedKey = "VoiSlate.BadRed";
    public const string NiceGoldKey = "VoiSlate.NiceGold";
    public const string TextHintKey = "VoiSlate.TextHint";

    // ---- 控件补充键（对齐原版取色） ----
    public const string AccentColorKey = "VoiSlate.AccentColor";          // = Primary bahamaBlue
    public const string NeutralColorKey = "VoiSlate.NeutralColor";        // 中性灰（未知状态/提示）
    public const string RecordPageBackgroundKey = "VoiSlate.RecordPageBackground"; // 记录页底色
    public const string CardBackgroundKey = "VoiSlate.CardBackground";    // 原版 0xFFF2F5DE
    public const string WheelItemBackgroundKey = "VoiSlate.WheelItemBackground";   // 原版 0xFFD1C4E9
    public const string WheelTextKey = "VoiSlate.WheelText";              // 滚轮文本
    public const string WheelTextSelectedKey = "VoiSlate.WheelTextSelected";       // 选中文本 0xFF212121
    public const string TextStrongKey = "VoiSlate.TextStrong";            // 强文本 0xFF212121
    public const string SlatePurpleKey = "VoiSlate.SlatePurple";          // 原版 0xFF63326E（记条按钮）
    public const string DialNotCheckedKey = "VoiSlate.DialNotChecked";    // 评价未选 0xFFF2F5DE

    /// <summary>契约 §6 必选键清单（供资源键测试逐字核对）。</summary>
    public static readonly IReadOnlyList<string> ContractRequiredKeys = new[]
    {
        BgKey,
        PrimaryKey,
        OkGreenKey,
        BadRedKey,
        NiceGoldKey,
        TextHintKey,
    };
}