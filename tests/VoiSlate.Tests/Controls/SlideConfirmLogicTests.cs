using VoiSlate.Controls;
using Xunit;

namespace VoiSlate.Tests.Controls;

/// <summary>契约 §5 SlideConfirmBar 拖动/幂等提交判定（SlideConfirmLogic）锁定测试。</summary>
public class SlideConfirmLogicTests
{
    private const double Width = 300;
    private const double BallSize = 44;

    [Fact]
    public void SlideLength_IsWidthMinusBall()
    {
        var logic = new SlideConfirmLogic();
        Assert.Equal(256, logic.SlideLength(Width, BallSize));
    }

    [Fact]
    public void SlideLength_NeverNegative()
    {
        var logic = new SlideConfirmLogic();
        Assert.Equal(0, logic.SlideLength(10, 44));
    }

    [Fact]
    public void InitialPosition_IsCentered()
    {
        var logic = new SlideConfirmLogic();
        Assert.Equal(128, logic.InitialPosition(Width, BallSize));
    }

    [Theory]
    [InlineData(-50, 0)]
    [InlineData(0, 0)]
    [InlineData(100, 100)]
    [InlineData(256, 256)]
    [InlineData(999, 256)]
    public void ClampPosition_StaysInRange(double position, double expected)
    {
        var logic = new SlideConfirmLogic();
        Assert.Equal(expected, logic.ClampPosition(position, Width, BallSize));
    }

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(128, 0.5)]
    [InlineData(256, 1.0)]
    public void Progress_IsZeroToOne(double position, double expected)
    {
        var logic = new SlideConfirmLogic();
        Assert.Equal(expected, logic.Progress(position, Width, BallSize), 2);
    }

    [Fact]
    public void Thresholds_DetectedOnlyAtEdges()
    {
        var logic = new SlideConfirmLogic();
        Assert.False(logic.IsPastRightThreshold(255, Width, BallSize));
        Assert.True(logic.IsPastRightThreshold(256, Width, BallSize));
        Assert.False(logic.IsPastLeftThreshold(1));
        Assert.True(logic.IsPastLeftThreshold(0));
        Assert.True(logic.IsPastLeftThreshold(-1));
    }

    [Fact]
    public void ReleaseTarget_IsCenter()
    {
        var logic = new SlideConfirmLogic();
        Assert.Equal(128, logic.ReleaseTarget(Width, BallSize));
    }

    // ---- 幂等补提交（契约：滑动触发时对同属性做幂等补提交，不覆盖未保存输入） ----

    [Fact]
    public void TryCommit_OnlyCommitsOnTextChange()
    {
        var logic = new SlideConfirmLogic();

        Assert.True(logic.TryCommitRight("shot note"));   // 首次提交
        Assert.False(logic.TryCommitRight("shot note"));  // 同一文本重复滑动 → 幂等，不再提交
        Assert.True(logic.TryCommitRight("shot note v2")); // 文本变化 → 提交

        Assert.True(logic.TryCommitLeft("desc note"));
        Assert.False(logic.TryCommitLeft("desc note"));
        Assert.False(logic.TryCommitLeft("desc note"));   // 重复触发不覆盖未保存输入
    }

    [Fact]
    public void TryCommit_NormalizesNullToEmpty()
    {
        var logic = new SlideConfirmLogic();
        Assert.True(logic.TryCommitRight(null!));
        Assert.False(logic.TryCommitRight(null!));
        Assert.False(logic.TryCommitRight(string.Empty));
    }

    [Fact]
    public void Reset_ClearsBothSides()
    {
        var logic = new SlideConfirmLogic();
        logic.TryCommitRight("a");
        logic.TryCommitLeft("b");
        logic.Reset();
        Assert.True(logic.TryCommitRight("a")); // 重置后再提交视为新提交
        Assert.True(logic.TryCommitLeft("b"));
    }

    [Fact]
    public void LeftAndRightCommitAreIndependent()
    {
        var logic = new SlideConfirmLogic();
        Assert.True(logic.TryCommitRight("same"));
        Assert.True(logic.TryCommitLeft("same")); // 两侧互不影响
        Assert.False(logic.TryCommitRight("same"));
        Assert.False(logic.TryCommitLeft("same"));
    }
}