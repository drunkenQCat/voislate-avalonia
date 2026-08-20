// WheelSelectionLogic — SlateWheel 滚动语义的纯逻辑层（可单测，无 Avalonia 依赖）。
//
// 语义来源（契约 §4/§5 + Flutter 原版 slate_picker_notifier.dart 的
// SlatePickerState.scrollToNext / scrollToPrev / scrollSelectedToIndex）：
//   * scrollToNext(isLink)：仅在 isLinked==true 时前进一格；联动关闭时不动；
//     到达末项时停止（不循环）。原版判定"selected == numList.last"停在最后一项。
//   * scrollToPrev(isLink)：未到首项则回退一格（原版索引无论 isLink 都回退，
//     仅动画受 isLink 控制 —— 本实现忠实保留该索引语义）；到首项停止。
//   * IsLoop 为契约新增能力（对齐 CupertinoPicker looping）：循环模式在边界回绕。
//
// 说明：原版 scrollToPrev 中"回退后 <0 回绕到末项"为死代码（前面已拦 _selectedIndex==0），
// 但契约 IsLoop=true 时按"0 再回退 → 末项"处理，即循环回绕由 IsLoop 显式开启。

namespace VoiSlate.Controls;

/// <summary>
/// SlateWheel 的滚动/边界/循环语义；纯静态，便于契约级单测锁定行为。
/// </summary>
public static class WheelSelectionLogic
{
    /// <summary>
    /// 计算"下一条"目标索引。
    /// </summary>
    /// <param name="current">当前索引（[0, count) 内）。</param>
    /// <param name="count">条目数；0 或负数视为空列表。</param>
    /// <param name="isLinked">联动标志（对齐原版 scrollToNext 的 isLink）。</param>
    /// <param name="isLoop">循环模式：边界回绕。</param>
    /// <returns>新索引；空列表返回 0。</returns>
    public static int Next(int current, int count, bool isLinked, bool isLoop)
    {
        if (count <= 0)
        {
            return 0;
        }

        // 原版语义：联动关闭时滚轮不前移（补录模式下 Take 号与文件号解绑）。
        if (!isLinked)
        {
            return ClampIndex(current, count);
        }

        if (isLoop)
        {
            return (ClampIndex(current, count) + 1) % count;
        }

        return Math.Min(ClampIndex(current, count) + 1, count - 1);
    }

    /// <summary>
    /// 计算"上一条"目标索引。
    /// </summary>
    /// <remarks>
    /// 原版 scrollToPrev：索引回退不依赖 isLink（仅动画依赖），首项时停止；
    /// IsLoop=true 时首项继续回退则回绕到末项。
    /// </remarks>
    public static int Prev(int current, int count, bool isLinked, bool isLoop)
    {
        if (count <= 0)
        {
            return 0;
        }

        var clamped = ClampIndex(current, count);
        if (isLoop)
        {
            return (clamped - 1 + count) % count;
        }

        return Math.Max(clamped - 1, 0);
    }

    /// <summary>
    /// 把任意整数索引收敛到 [0, count)（空列表返回 0）。用于 Items 变化后的越界收敛。
    /// </summary>
    public static int ClampIndex(int index, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        return Math.Clamp(index, 0, count - 1);
    }

    /// <summary>
    /// 把循环模式下可能越界的浮点位置收敛到 [0, count)（空列表返回 0）。
    /// 非循环模式同样收敛到 [0, count - 1]。
    /// </summary>
    public static double ClampPosition(double position, int count, bool isLoop)
    {
        if (count <= 0)
        {
            return 0;
        }

        if (isLoop)
        {
            var wrapped = position % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }

        return Math.Clamp(position, 0, count - 1);
    }

    /// <summary>
    /// 手势拖动中的连续吸附：对连续浮点位置取最近整项（拖动中 SelectedIndex 连续更新用）。
    /// 循环模式下先收敛再四舍五入。
    /// </summary>
    public static int SnapToNearest(double position, int count, bool isLoop)
    {
        if (count <= 0)
        {
            return 0;
        }

        if (isLoop)
        {
            var wrapped = position % count;
            if (wrapped < 0)
            {
                wrapped += count;
            }

            return (int)Math.Round(wrapped) % count;
        }

        return (int)Math.Round(Math.Clamp(position, 0, count - 1));
    }

    /// <summary>
    /// 计算连续浮点位置（非循环视图坐标，可能越界/循环越界）对应的"显示索引"：
    /// 非循环：直接收敛；循环：取最近等价副本（保证拖过边界时视觉连续）。
    /// </summary>
    public static int DisplayIndex(double position, int count, bool isLoop)
    {
        if (count <= 0)
        {
            return 0;
        }

        if (!isLoop)
        {
            return (int)Math.Round(Math.Clamp(position, 0, count - 1));
        }

        // 循环：位置可能等于 0*count 之外；取模后以 0.5 为界四舍五入，
        // 保证索引从 count-1 平滑过渡到 0。
        var wrapped = position % count;
        if (wrapped < 0)
        {
            wrapped += count;
        }

        return (int)Math.Round(wrapped) % count;
    }
}