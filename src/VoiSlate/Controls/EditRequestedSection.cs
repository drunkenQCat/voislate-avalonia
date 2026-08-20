// EditRequestedSection — FileCounter 的编辑请求区段（契约 §5 FileCounter 行，D 产出）。
//
// 依契约 §4 B6："D 控件的 DialOption / EditRequestedSection 等 View 层类型不得泄漏进 VM"。

namespace VoiSlate.Controls;

/// <summary>文件号三卡片编辑区段（契约 §5：Prefix=三模式 Toggle / Linker=文本 / Number=整型）。</summary>
public enum EditRequestedSection
{
    /// <summary>前缀卡片：Date / Sound Devices / Custom 三模式 Toggle + custom 文本。</summary>
    Prefix,

    /// <summary>分隔符卡片：Linker 文本编辑。</summary>
    Linker,

    /// <summary>编号卡片：整数编辑（不输入 0，显示 D3 补零）。</summary>
    Number,
}