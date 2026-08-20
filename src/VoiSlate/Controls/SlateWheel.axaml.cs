using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace VoiSlate.Controls;

/// <summary>
/// Agent C 占位 SlateWheel（契约 v0.5 §5 签名一致）。
/// 依赖属性/事件/方法与契约一致；滚动动画、手势吸附由 Agent D 的正式实现提供。
/// D 合入后删除本文件（连同 SlateWheel.axaml）。
/// </summary>
public partial class SlateWheel : UserControl
{
    private bool _syncing;

    public SlateWheel()
    {
        InitializeComponent();
        PART_List.SelectionChanged += OnListSelectionChanged;
        PART_List.ContainerPrepared += OnContainerPrepared;
    }

    public static readonly StyledProperty<IReadOnlyList<string>?> ItemsProperty =
        AvaloniaProperty.Register<SlateWheel, IReadOnlyList<string>?>(nameof(Items));

    /// <summary>契约 §5：选项列表（OneWay；通常绑定 SlateColumnViewModel.Items）。</summary>
    public IReadOnlyList<string>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<SlateWheel, int>(nameof(SelectedIndex), defaultValue: 0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>契约 §5：当前选中索引（TwoWay；程序化滚动与手势共用同一状态通路）。</summary>
    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public static readonly StyledProperty<double> ItemHeightProperty =
        AvaloniaProperty.Register<SlateWheel, double>(nameof(ItemHeight), defaultValue: 48);

    /// <summary>契约 §5：行高（默认 48）。</summary>
    public double ItemHeight
    {
        get => GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    public static readonly StyledProperty<bool> IsLoopProperty =
        AvaloniaProperty.Register<SlateWheel, bool>(nameof(IsLoop));

    /// <summary>契约 §5：是否循环（默认 false）。</summary>
    public bool IsLoop
    {
        get => GetValue(IsLoopProperty);
        set => SetValue(IsLoopProperty, value);
    }

    /// <summary>契约 §5：选中项变化（string 为选中项文本；空串表示无选中）。</summary>
    public event Action<string>? SelectedItemChanged;

    /// <summary>契约 §5：程序化滚动（200ms ease-in 动画由 D 的正式实现负责；占位直接跳转）。</summary>
    public void ScrollTo(int index, bool animate = true) => SelectedIndex = index;

    /// <summary>契约 §5：下一项（isLinked=false 时不滚动——补录模式由 FileCounter 接管）。</summary>
    public void ScrollNext(bool isLinked)
    {
        if (!isLinked || Items is not { Count: > 0 } list) return;
        SelectedIndex = IsLoop
            ? (SelectedIndex + 1) % list.Count
            : Math.Min(SelectedIndex + 1, list.Count - 1);
    }

    /// <summary>契约 §5：上一项（isLinked=false 时不滚动）。</summary>
    public void ScrollPrev(bool isLinked)
    {
        if (!isLinked || Items is not { Count: > 0 } list) return;
        SelectedIndex = IsLoop
            ? (SelectedIndex - 1 + list.Count) % list.Count
            : Math.Max(SelectedIndex - 1, 0);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsProperty)
        {
            if (PART_List != null)
            {
                PART_List.ItemsSource = Items;
            }
        }
        else if (change.Property == SelectedIndexProperty && !_syncing)
        {
            SyncListIndex();
        }
        else if (change.Property == ItemHeightProperty)
        {
            ReapplyItemHeight();
        }
    }

    private void SyncListIndex()
    {
        if (PART_List == null) return;
        _syncing = true;
        PART_List.SelectedIndex = SelectedIndex;
        _syncing = false;
    }

    private void OnListSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || PART_List == null) return;
        _syncing = true;
        SelectedIndex = PART_List.SelectedIndex;
        _syncing = false;

        var text = Items is { Count: > 0 } list && SelectedIndex >= 0 && SelectedIndex < list.Count
            ? list[SelectedIndex]
            : string.Empty;
        SelectedItemChanged?.Invoke(text);
    }

    private void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is ListBoxItem item)
        {
            item.Height = ItemHeight;
        }
    }

    private void ReapplyItemHeight()
    {
        if (PART_List == null || Items == null) return;
        for (var i = 0; i < Items.Count; i++)
        {
            if (PART_List.ContainerFromIndex(i) is ListBoxItem item)
            {
                item.Height = ItemHeight;
            }
        }
    }
}