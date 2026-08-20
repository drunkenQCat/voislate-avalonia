// SlideConfirmState — SlideConfirmBar 的 State 依赖属性取值（契约 §5 SlideConfirmBar 行）。

namespace VoiSlate.Controls;

/// <summary>滑动确认条状态（契约 §5：Idle/Pressed/Armed）。</summary>
public enum SlideConfirmState
{
    /// <summary>未按压。</summary>
    Idle,

    /// <summary>拖动中（未越过触发阈值）。</summary>
    Pressed,

    /// <summary>已越过触发阈值（右端/左端），松手即触发确认。</summary>
    Armed,
}