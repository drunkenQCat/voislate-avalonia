using System.ComponentModel;
using VoiSlate.ViewModels;
using Xunit;

namespace VoiSlate.Tests.VM;

/// <summary>
/// SlateColumnViewModel：列数据/查重/SelectedIndex 通知/边界与循环语义（对齐原版 slate_picker_notifier.dart）。
/// </summary>
public class SlateColumnViewModelTests
{
    private static SlateColumnViewModel Col(params string[] items) => new() { Items = items };

    [Fact]
    public void SelectedItem_Tracks_Index()
    {
        var col = Col("1", "2", "3");
        Assert.Equal("1", col.SelectedItem);

        col.SelectedIndex = 2;
        Assert.Equal("3", col.SelectedItem);
    }

    [Fact]
    public void SetItems_With_Duplicates_Throws()
    {
        var col = new SlateColumnViewModel();
        Assert.Throws<InvalidOperationException>(() => col.Items = ["a", "a"]);
    }

    [Fact]
    public void SetItems_Shrinking_Clamps_SelectedIndex()
    {
        var col = Col("1", "2", "3");
        col.SelectedIndex = 2;
        col.SetItems(["1", "2"]);
        Assert.Equal(1, col.SelectedIndex);
        Assert.Equal("2", col.SelectedItem);
    }

    [Fact]
    public void PropertyChanged_Raised_For_SelectedIndex_And_SelectedItem()
    {
        var col = Col("1", "2");
        var list = new List<string?>();
        col.PropertyChanged += (_, e) => list.Add(e.PropertyName);

        col.SelectedIndex = 1;

        Assert.Contains(nameof(SlateColumnViewModel.SelectedIndex), list);
        Assert.Contains(nameof(SlateColumnViewModel.SelectedItem), list);
    }

    [Fact]
    public void ScrollTo_Clamps_Out_Of_Range()
    {
        var col = Col("1", "2", "3");
        col.ScrollTo(99);
        Assert.Equal(2, col.SelectedIndex);

        col.ScrollTo(-5);
        Assert.Equal(0, col.SelectedIndex);

        col.ScrollTo(1, animate: false);
        Assert.Equal(1, col.SelectedIndex);
    }

    [Fact]
    public void ScrollNext_Stops_At_Last_And_Respects_Link()
    {
        var col = Col("1", "2", "3");
        col.SelectedIndex = 2;
        col.ScrollNext(isLinked: true);
        Assert.Equal(2, col.SelectedIndex); // 末尾不循环

        col.SelectedIndex = 1;
        col.ScrollNext(isLinked: false);
        Assert.Equal(1, col.SelectedIndex); // 未联动不变

        col.ScrollNext(isLinked: true);
        Assert.Equal(2, col.SelectedIndex);
    }

    [Fact]
    public void ScrollPrev_Stops_At_Zero_And_Decs_Even_When_Unlinked()
    {
        var col = Col("1", "2", "3");
        col.SelectedIndex = 0;
        col.ScrollPrev(isLinked: true);
        Assert.Equal(0, col.SelectedIndex); // 首项不循环

        col.SelectedIndex = 2;
        col.ScrollPrev(isLinked: false);
        Assert.Equal(1, col.SelectedIndex); // 原版 scrollToPrev 的既成行为：未联动也递减
    }

    [Fact]
    public void Empty_Column_Is_NoOp()
    {
        var col = new SlateColumnViewModel();
        col.ScrollTo(3);
        col.ScrollNext(true);
        col.ScrollPrev(true);
        Assert.Equal(0, col.SelectedIndex);
        Assert.Equal("", col.SelectedItem);
    }
}