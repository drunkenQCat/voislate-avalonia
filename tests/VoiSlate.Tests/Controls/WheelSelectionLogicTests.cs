using VoiSlate.Controls;
using Xunit;

namespace VoiSlate.Tests.Controls;

/// <summary>契约 §5 SlateWheel 滚动语义（WheelSelectionLogic）锁定测试。</summary>
public class WheelSelectionLogicTests
{
    // ---- Next ----

    [Fact]
    public void Next_Linked_Advances()
    {
        Assert.Equal(1, WheelSelectionLogic.Next(0, 5, isLinked: true, isLoop: false));
    }

    [Fact]
    public void Next_NotLinked_Stays()
    {
        // 原版 scrollToNext：isLink=false 时不前移（补录模式 Take 与文件号解绑）。
        Assert.Equal(2, WheelSelectionLogic.Next(2, 5, isLinked: false, isLoop: false));
        Assert.Equal(0, WheelSelectionLogic.Next(0, 5, isLinked: false, isLoop: false));
    }

    [Fact]
    public void Next_AtLast_Stops_WhenNotLoop()
    {
        // 原版 scrollToNext：selected == numList.last 时直接 return。
        Assert.Equal(4, WheelSelectionLogic.Next(4, 5, isLinked: true, isLoop: false));
    }

    [Fact]
    public void Next_Loop_WrapsAtLast()
    {
        Assert.Equal(0, WheelSelectionLogic.Next(4, 5, isLinked: true, isLoop: true));
        Assert.Equal(3, WheelSelectionLogic.Next(2, 5, isLinked: true, isLoop: true));
    }

    [Fact]
    public void Next_Empty_ReturnsZero()
    {
        Assert.Equal(0, WheelSelectionLogic.Next(0, 0, isLinked: true, isLoop: false));
        Assert.Equal(0, WheelSelectionLogic.Next(3, -1, isLinked: true, isLoop: true));
    }

    // ---- Prev ----

    [Fact]
    public void Prev_GoesBackward()
    {
        // 原版 scrollToPrev：索引回退不依赖 isLink（仅动画依赖）。
        Assert.Equal(1, WheelSelectionLogic.Prev(2, 5, isLinked: true, isLoop: false));
        Assert.Equal(1, WheelSelectionLogic.Prev(2, 5, isLinked: false, isLoop: false));
    }

    [Fact]
    public void Prev_AtFirst_Stops_WhenNotLoop()
    {
        Assert.Equal(0, WheelSelectionLogic.Prev(0, 5, isLinked: true, isLoop: false));
    }

    [Fact]
    public void Prev_Loop_WrapsAtFirst()
    {
        Assert.Equal(4, WheelSelectionLogic.Prev(0, 5, isLinked: true, isLoop: true));
    }

    // ---- ClampIndex / ClampPosition ----

    [Fact]
    public void ClampIndex_ConvergesOutOfRange()
    {
        Assert.Equal(0, WheelSelectionLogic.ClampIndex(-3, 5));
        Assert.Equal(4, WheelSelectionLogic.ClampIndex(99, 5));
        Assert.Equal(2, WheelSelectionLogic.ClampIndex(2, 5));
        Assert.Equal(0, WheelSelectionLogic.ClampIndex(2, 0));
    }

    [Fact]
    public void ClampPosition_WrapsInLoop()
    {
        Assert.Equal(1.0, WheelSelectionLogic.ClampPosition(6.0, 5, isLoop: true));
        Assert.Equal(4.5, WheelSelectionLogic.ClampPosition(-0.5, 5, isLoop: true));
        Assert.Equal(2.0, WheelSelectionLogic.ClampPosition(2.0, 5, isLoop: true));
    }

    [Fact]
    public void ClampPosition_ConvergesWhenNotLoop()
    {
        Assert.Equal(4.0, WheelSelectionLogic.ClampPosition(9.0, 5, isLoop: false));
        Assert.Equal(0.0, WheelSelectionLogic.ClampPosition(-1.0, 5, isLoop: false));
    }

    // ---- Snap / DisplayIndex ----

    [Theory]
    [InlineData(0.2, 0)]
    [InlineData(0.6, 1)]
    [InlineData(3.4, 3)]
    [InlineData(4.9, 5)]
    public void SnapToNearest_RoundsToItem(double position, int expected)
    {
        Assert.Equal(expected, WheelSelectionLogic.SnapToNearest(position, 6, isLoop: false));
    }

    [Fact]
    public void SnapToNearest_Loop_WrapsBeyondEdges()
    {
        Assert.Equal(4, WheelSelectionLogic.SnapToNearest(-0.4, 5, isLoop: true));
        Assert.Equal(0, WheelSelectionLogic.SnapToNearest(5.1, 5, isLoop: true));
        Assert.Equal(1, WheelSelectionLogic.SnapToNearest(6.1, 5, isLoop: true));
    }

    [Fact]
    public void DisplayIndex_Loop_IsContinuousAcrossBoundary()
    {
        // 拖过边界时索引从末项平滑过渡到首项（视觉连续）。
        Assert.Equal(4, WheelSelectionLogic.DisplayIndex(4.4, 5, isLoop: true));
        Assert.Equal(0, WheelSelectionLogic.DisplayIndex(4.6, 5, isLoop: true));
        Assert.Equal(4, WheelSelectionLogic.DisplayIndex(-0.4, 5, isLoop: true));
    }
}