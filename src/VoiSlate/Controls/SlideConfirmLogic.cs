// SlideConfirmLogic — SlideConfirmBar 的水平拖动判定纯逻辑（可单测，无 Avalonia 依赖）。
//
// 对齐契约 §5 SlideConfirmBar 行为协议与 Flutter 原版 recorder_joystick.dart：
//   * slideLength = width - ballSize（原版 slideLength = width - height）；
//   * 位置初始居中（原版 initValue = slideLength / 2）；
//   * 右滑越过 slideLength 阈值触发 SlideRight，左滑越过 0 触发 SlideLeft；松手回弹居中；
//   * "滑动触发时对同属性做幂等补提交（不覆盖未保存输入）"：
//     幂等记账以文本值变化为准 — 同一文本重复滑动只产生一次"新提交"，不覆盖未保存输入。

namespace VoiSlate.Controls;

/// <summary>
/// SlideConfirmBar 的拖动与幂等提交判定；纯逻辑便于契约级单测。
/// </summary>
public sealed class SlideConfirmLogic
{
    /// <summary>拖动条长度（像素）：宽度扣掉滑块直径。</summary>
    public double SlideLength(double width, double ballSize)
    {
        return Math.Max(0, width - ballSize);
    }

    /// <summary>初始（居中）位置。</summary>
    public double InitialPosition(double width, double ballSize) => SlideLength(width, ballSize) / 2;

    /// <summary>把任意位置夹到 [0, slideLength]。</summary>
    public double ClampPosition(double position, double width, double ballSize)
    {
        var length = SlideLength(width, ballSize);
        return Math.Clamp(position, 0, length);
    }

    /// <summary>当前相对进度 0..1（越靠右越接近 1），供背景红→绿插值。</summary>
    public double Progress(double position, double width, double ballSize)
    {
        var length = SlideLength(width, ballSize);
        if (length <= 0)
        {
            return 0;
        }

        return Math.Clamp(ClampPosition(position, width, ballSize) / length, 0, 1);
    }

    /// <summary>是否已越过右阈值（触发 SlideRight 的临界判定）。</summary>
    public bool IsPastRightThreshold(double position, double width, double ballSize)
        => position >= SlideLength(width, ballSize);

    /// <summary>是否已越过左阈值（触发 SlideLeft 的临界判定）。</summary>
    public bool IsPastLeftThreshold(double position)
        => position <= 0;

    /// <summary>松手回弹目标：居中位置（原版 200ms 回弹语义）。</summary>
    public double ReleaseTarget(double width, double ballSize) => InitialPosition(width, ballSize);

    private string? _lastCommittedLeft;
    private string? _lastCommittedRight;

    /// <summary>上一次各侧"已确认提交"的文本（幂等记账）。</summary>
    public string? LastCommittedLeft => _lastCommittedLeft;

    /// <summary>上一次各侧"已确认提交"的文本（幂等记账）。</summary>
    public string? LastCommittedRight => _lastCommittedRight;

    /// <summary>
    /// 幂等补提交：仅在文本相较上次提交有变化时返回 true 并记账；
    /// 重复滑动同一文本返回 false（不覆盖未保存输入、不重复提交）。
    /// </summary>
    public bool TryCommitLeft(string? currentText) => TryCommit(ref _lastCommittedLeft, currentText);

    /// <summary>
    /// 幂等补提交：仅在文本相较上次提交有变化时返回 true 并记账；
    /// 重复滑动同一文本返回 false（不覆盖未保存输入、不重复提交）。
    /// </summary>
    public bool TryCommitRight(string? currentText) => TryCommit(ref _lastCommittedRight, currentText);

    private static bool TryCommit(ref string? last, string? current)
    {
        current ??= string.Empty;
        if (string.Equals(last, current, StringComparison.Ordinal))
        {
            return false;
        }

        last = current;
        return true;
    }

    /// <summary>重置两侧幂等记账（如页面重新进入补录/结构化复位时）。</summary>
    public void Reset() => (_lastCommittedLeft, _lastCommittedRight) = (null, null);
}