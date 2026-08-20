// DialOption — DialFAB 的展示数据项（契约 §5 DialFAB 行，D 产出，仅显示数据）。
//
// 依契约 §4 B6："D 控件的 DialOption/EditRequestedSection 等 View 层类型不得泄漏进 VM"，
// 且 "DialOption.EnumValue(object) → TkStatus/ShtStatus 的转换由 C 在 RecordView
// code-behind 映射表完成"。故 EnumValue 定义为 object，由 C 填入模型枚举。

namespace VoiSlate.Controls;

/// <summary>
/// 评价拨盘（DialFAB）的单个选项；仅承载显示数据。
/// </summary>
/// <param name="Label">选项文字（如 "声音可" / "画面过"）。</param>
/// <param name="Icon">图标（任意可渲染对象：Geometry / string 字形 / IImage 等）。</param>
/// <param name="EnumValue">绑定值：由 C 映射为 TkStatus / ShtStatus 等模型枚举（object）。</param>
public sealed record DialOption(string Label, object? Icon, object? EnumValue);