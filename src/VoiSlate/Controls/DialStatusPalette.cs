// DialStatusPalette — DialFAB 选中状态回显色映射（契约 §5 DialFAB 行）。
//
// "选中后背景色/图标随状态回显（NotChecked=浅色/Ok=绿/Bad=红/Nice=金黄）"。
// 颜色取值与 Themes/VoiSlatePalette.axaml 中的资源键保持一致（代码侧作为
// 未挂主题时的回退值），便于纯逻辑单测。

using Avalonia.Media;
using VoiSlate.Models;

namespace VoiSlate.Controls;

/// <summary>评价状态 → 回显色映射（纯静态，可单测）。</summary>
public static class DialStatusPalette
{
    // 与 VoiSlatePalette.axaml 同名资源键（代码回退值；主题合并后以资源为准）。
    public const string NotCheckedResourceKey = "VoiSlate.DialNotChecked";
    public const string OkResourceKey = "VoiSlate.OkGreen";
    public const string BadResourceKey = "VoiSlate.BadRed";
    public const string NiceResourceKey = "VoiSlate.NiceGold";
    public const string NeutralResourceKey = "VoiSlate.NeutralColor";

    /// <summary>NotChecked / 未选中（浅色）。原版 0xFFF2F5DE。</summary>
    public static readonly Color NotChecked = Color.Parse("#F2F5DE");

    /// <summary>Ok（绿）。Material green 500。</summary>
    public static readonly Color Ok = Color.Parse("#4CAF50");

    /// <summary>Bad（红）。Material red 500。</summary>
    public static readonly Color Bad = Color.Parse("#F44336");

    /// <summary>Nice（金黄）。Material amber 500。</summary>
    public static readonly Color Nice = Color.Parse("#FFC107");

    /// <summary>未知状态（中性灰）。Material grey 500。</summary>
    public static readonly Color Neutral = Color.Parse("#9E9E9E");

    /// <summary>枚举值 → 回显色（null / 未知 → 中性浅色，对齐 "NotChecked=浅色" 语义）。</summary>
    public static Color StatusColor(object? enumValue) => enumValue switch
    {
        null => NotChecked,
        TkStatus.NotChecked => NotChecked,
        TkStatus.Ok => Ok,
        TkStatus.Bad => Bad,
        TkStatus _ => Neutral,
        ShtStatus.NotChecked => NotChecked,
        ShtStatus.Ok => Ok,
        ShtStatus.Nice => Nice,
        ShtStatus _ => Neutral,
        _ => Neutral,
    };

    /// <summary>枚举值 → 主题资源键（供控件在主题中取用；回退 Value 常量）。</summary>
    public static string StatusResourceKey(object? enumValue) => enumValue switch
    {
        TkStatus.Bad => BadResourceKey,
        ShtStatus.Nice => NiceResourceKey,
        ShtStatus.Ok => OkResourceKey,
        TkStatus.Ok => OkResourceKey,
        TkStatus.NotChecked or ShtStatus.NotChecked or null => NotCheckedResourceKey,
        _ => NeutralResourceKey,
    };
}