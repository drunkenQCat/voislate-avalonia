using CommunityToolkit.Mvvm.ComponentModel;

namespace VoiSlate.ViewModels;

/// <summary>
/// Agent C stub：滚轮列 VM（契约 §4 —— Agent B 产出，本文件为编译用占位，合并后删除）。
/// 每列一个实例（场/镜/次）；SelectedIndex 为 TwoWay 绑定通路（程序化滚动与手势共用，
/// 契约 §5 SlateWheel 说明）。不含滚动实现（边界/循环语义 stub，正式语义归 B）。
/// </summary>
public partial class SlateColumnViewModel : ObservableObject
{
    [ObservableProperty]
    private IReadOnlyList<string> _items = [];

    [ObservableProperty]
    private int _selectedIndex;

    /// <summary>契约 §4：当前选中项文本（计算）。</summary>
    public string? SelectedItem =>
        SelectedIndex >= 0 && SelectedIndex < Items.Count ? Items[SelectedIndex] : null;

    partial void OnSelectedIndexChanged(int value) => OnPropertyChanged(nameof(SelectedItem));

    /// <summary>契约 §4：滚动到指定索引（animate 由 D 控件实现决定；stub 忽略动画）。</summary>
    public void ScrollTo(int index, bool animate = true)
    {
        if (index < 0 || index >= Items.Count) return;
        SelectedIndex = index;
    }

    /// <summary>契约 §4：下一项（isLinked=false 不滚动——补录模式由 FileCounter 接管）。</summary>
    public void ScrollNext(bool isLinked)
    {
        if (!isLinked || Items.Count == 0) return;
        SelectedIndex = Math.Min(SelectedIndex + 1, Items.Count - 1);
    }

    /// <summary>契约 §4：上一项（isLinked=false 不滚动）。</summary>
    public void ScrollPrev(bool isLinked)
    {
        if (!isLinked || Items.Count == 0) return;
        SelectedIndex = Math.Max(SelectedIndex - 1, 0);
    }
}