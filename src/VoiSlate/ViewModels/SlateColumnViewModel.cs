using CommunityToolkit.Mvvm.ComponentModel;

namespace VoiSlate.ViewModels;

/// <summary>
/// 单列滚轮状态（契约 §4 SlateColumnViewModel；对齐原版 SlatePickerState）。每列一个实例。
/// 不含滚动实现（SlateWheel 控件负责动画/手势，契约 §5）；SelectedIndex TwoWay 由控件驱动。
/// </summary>
public partial class SlateColumnViewModel : ObservableObject
{
    private IReadOnlyList<string> _items = [];

    /// <summary>
    /// 列数据（场景名 / 镜名 / 1..200 次标签）。设置时查重（原版 numList 语义：重复抛异常）并夹取 SelectedIndex。
    /// </summary>
    public IReadOnlyList<string> Items
    {
        get => _items;
        set
        {
            var list = value ?? [];
            var set = new HashSet<string>(list, StringComparer.Ordinal);
            if (set.Count != list.Count)
            {
                throw new InvalidOperationException("numList has duplicate elements");
            }

            _items = list;
            if (SelectedIndex >= list.Count)
            {
                SelectedIndex = list.Count == 0 ? 0 : list.Count - 1;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedItem));
        }
    }

    public void SetItems(IReadOnlyList<string> items) => Items = items;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedItem))]
    private int _selectedIndex;

    /// <summary>计算选中项（原版 selected；越界返回空串）。</summary>
    public string SelectedItem
        => _items.Count == 0 || SelectedIndex < 0 || SelectedIndex >= _items.Count
            ? string.Empty
            : _items[SelectedIndex];

    /// <summary>程序化滚动到指定索引（animate 语义由 SlateWheel 承担；越界夹取，对齐原版 init 的越界归 0/夹取）。</summary>
    public void ScrollTo(int index, bool animate = true)
    {
        if (_items.Count == 0)
        {
            return;
        }

        SelectedIndex = Math.Clamp(index, 0, _items.Count - 1);
    }

    /// <summary>下一项：末尾不循环；未联动不变（对齐原版 scrollToNext）。</summary>
    public void ScrollNext(bool isLinked)
    {
        if (_items.Count == 0)
        {
            return;
        }

        if (SelectedIndex == _items.Count - 1)
        {
            return;
        }

        if (isLinked)
        {
            SelectedIndex++;
        }
    }

    /// <summary>上一项：首项不循环；索引递减与联动无关（对齐原版 scrollToPrev 的既成行为——递减发生在联动判断之前）。</summary>
    public void ScrollPrev(bool isLinked)
    {
        if (_items.Count == 0)
        {
            return;
        }

        if (SelectedIndex == 0)
        {
            return;
        }

        SelectedIndex--;
    }
}