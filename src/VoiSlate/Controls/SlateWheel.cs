// SlateWheel — 场记三列滚轮控件（契约 §5 SlateWheel 行）。
//
// 依赖属性（签名与契约逐字一致）：
//   Items           IReadOnlyList<string>?  OneWay   默认 null
//   SelectedIndex   int                     TwoWay   默认 0
//   ItemHeight      double                  OneWay   默认 48
//   IsLoop          bool                    OneWay   默认 false
// 事件：SelectedItemChanged(string)
// 方法：ScrollTo(int index, bool animate = true) / ScrollNext(bool isLinked) / ScrollPrev(bool isLinked)
//
// 行为协议落实：
//   * 手势拖拽连续更新 SelectedIndex（TwoWay 回源），松手吸附最近项；
//   * 鼠标滚轮滚动（逐项，200ms ease-in 动画）；fling 惯性为可选能力（本期跳过，见报告）；
//   * 程序化滚动（ScrollTo / 外部 SelectedIndex 写入）与手势共用同一 _position 状态通路；
//   * 滚轮动画 200ms ease-in（契约 §5；与 Flutter 原版 scrollSelectedToIndex 的
//     200ms + Curves.easeIn 一致——任务书所述"300ms ease-out"与原版/契约不符，以契约为准，
//     歧义已写入报告）；
//   * 边界/循环语义全部经 WheelSelectionLogic（可单测）。

using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace VoiSlate.Controls;

/// <summary>场记滚轮（自绘控件）。</summary>
public class SlateWheel : Control
{
    public static readonly StyledProperty<IReadOnlyList<string>?> ItemsProperty =
        AvaloniaProperty.Register<SlateWheel, IReadOnlyList<string>?>(
            nameof(Items), defaultValue: null, defaultBindingMode: Avalonia.Data.BindingMode.OneWay);

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<SlateWheel, int>(
            nameof(SelectedIndex), defaultValue: 0, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> ItemHeightProperty =
        AvaloniaProperty.Register<SlateWheel, double>(
            nameof(ItemHeight), defaultValue: 48.0, defaultBindingMode: Avalonia.Data.BindingMode.OneWay);

    public static readonly StyledProperty<bool> IsLoopProperty =
        AvaloniaProperty.Register<SlateWheel, bool>(
            nameof(IsLoop), defaultValue: false, defaultBindingMode: Avalonia.Data.BindingMode.OneWay);

    /// <summary>滚动动画时长（契约 §5：200ms ease-in；对齐原版 scrollSelectedToIndex）。</summary>
    public const double ScrollAnimationMilliseconds = 200;

    private const double BandAlpha = 0x50; // 原版 selectionOverlay：withAlpha(80)

    private static readonly IEasing ScrollEasing = new CubicEaseIn();

    private readonly Dictionary<string, FormattedText> _regularLayouts = new();
    private readonly Dictionary<string, FormattedText> _selectedLayouts = new();

    private double _position;            // 浮点选中位置（条目单位；循环模式可越界）
    private bool _isDragging;
    private double _dragStartPosition;   // 拖动起始 _position
    private double _dragStartPointerY;   // 拖动起始指针 Y
    private DispatcherTimer? _animationTimer;
    private double _animFrom;
    private double _animTo;
    private long _animStartTimestamp;
    private bool _suppressExternalSync;
    private string? _lastSelectedItem;

    static SlateWheel()
    {
        AffectsRender<SlateWheel>(SelectedIndexProperty, ItemHeightProperty, IsLoopProperty, ItemsProperty);
    }

    public SlateWheel()
    {
        ClipToBounds = true;
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        // 卸载时停止动画与拖动（Control.OnDetachedFromVisualTreeCore 为密封，改挂 Unloaded）。
        StopAnimation();
        _isDragging = false;
    }

    public IReadOnlyList<string>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public double ItemHeight
    {
        get => GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    public bool IsLoop
    {
        get => GetValue(IsLoopProperty);
        set => SetValue(IsLoopProperty, value);
    }

    /// <summary>选中项变化事件（契约 §5；Items 变化导致选中文本变化时同样触发）。</summary>
    public event Action<string>? SelectedItemChanged;

    /// <summary>当前选中条目文本（Items 为空时为 null）。</summary>
    public string? SelectedItem
    {
        get
        {
            var items = EnsureItems();
            return items.Count > 0 ? items[SelectedIndex] : null;
        }
    }

    private IReadOnlyList<string> EnsureItems() => Items ?? Array.Empty<string>();

    // ---------------------------------------------------------------- 契约方法

    /// <summary>程序化滚动到指定索引；animate=true 时控件负责 200ms ease-in 动画。</summary>
    public void ScrollTo(int index, bool animate = true)
    {
        var items = EnsureItems();
        if (items.Count == 0)
        {
            return;
        }

        var target = IsLoop
            ? ((index % items.Count) + items.Count) % items.Count
            : Math.Clamp(index, 0, items.Count - 1);

        SetSelectedIndexCore(target, animate);
    }

    /// <summary>下一条（边界/循环语义经 WheelSelectionLogic；isLinked 时联动推进）。</summary>
    public void ScrollNext(bool isLinked)
    {
        var items = EnsureItems();
        var target = WheelSelectionLogic.Next(SelectedIndex, items.Count, isLinked, IsLoop);
        SetSelectedIndexCore(target, animate: true);
    }

    /// <summary>上一条（边界/循环语义经 WheelSelectionLogic）。</summary>
    public void ScrollPrev(bool isLinked)
    {
        var items = EnsureItems();
        var target = WheelSelectionLogic.Prev(SelectedIndex, items.Count, isLinked, IsLoop);
        SetSelectedIndexCore(target, animate: true);
    }

    // ---------------------------------------------------------------- 内部状态通路

    /// <summary>唯一状态入口：程序化滚动与手势共用（契约："程序化滚动与手势共用同一状态通路"）。</summary>
    private void SetSelectedIndexCore(int index, bool animate)
    {
        var items = EnsureItems();
        if (items.Count == 0)
        {
            _suppressExternalSync = true;
            SelectedIndex = 0;
            _suppressExternalSync = false;
            _position = 0;
            InvalidateVisual();
            return;
        }

        index = IsLoop
            ? ((index % items.Count) + items.Count) % items.Count
            : Math.Clamp(index, 0, items.Count - 1);

        _suppressExternalSync = true;
        SelectedIndex = index;
        _suppressExternalSync = false;

        var target = (double)index;
        if (IsLoop)
        {
            // 循环模式下动画走最短路径（对 _position 取最近等价副本）。
            var count = items.Count;
            while (target - _position > count / 2.0)
            {
                target -= count;
            }

            while (_position - target > count / 2.0)
            {
                target += count;
            }
        }

        if (animate && CanAnimate)
        {
            AnimateTo(target);
        }
        else
        {
            _position = target;
            InvalidateVisual();
        }
    }

    private bool CanAnimate => IsLoaded && Application.Current is not null;

    private void AnimateTo(double target)
    {
        StopAnimation();
        _animFrom = _position;
        _animTo = target;
        _animStartTimestamp = Stopwatch.GetTimestamp();

        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animationTimer.Tick += (_, _) =>
        {
            var elapsedMs = (Stopwatch.GetTimestamp() - _animStartTimestamp) * 1000.0 / Stopwatch.Frequency;
            var t = Math.Clamp(elapsedMs / ScrollAnimationMilliseconds, 0, 1);
            _position = _animFrom + (_animTo - _animFrom) * ScrollEasing.Ease(t);
            UpdateContinuousIndex();
            InvalidateVisual();

            if (t >= 1)
            {
                _position = _animTo;
                StopAnimation();
                FinalizeIndex();
                InvalidateVisual();
            }
        };
        _animationTimer.Start();
    }

    private void StopAnimation()
    {
        _animationTimer?.Stop();
        _animationTimer = null;
    }

    /// <summary>动画/拖动结束时把 SelectedIndex 精确收敛到位。</summary>
    private void FinalizeIndex()
    {
        var items = EnsureItems();
        if (items.Count == 0)
        {
            return;
        }

        _suppressExternalSync = true;
        SelectedIndex = WheelSelectionLogic.DisplayIndex(_position, items.Count, IsLoop);
        _suppressExternalSync = false;
    }

    /// <summary>拖动/动画中连续更新 SelectedIndex（手势 TwoWay 回源 + 程序化共用）。</summary>
    private void UpdateContinuousIndex()
    {
        var items = EnsureItems();
        if (items.Count == 0)
        {
            return;
        }

        var display = WheelSelectionLogic.DisplayIndex(_position, items.Count, IsLoop);
        if (display != SelectedIndex)
        {
            _suppressExternalSync = true;
            SelectedIndex = display;
            _suppressExternalSync = false;
        }
    }

    // ---------------------------------------------------------------- 属性联动

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsProperty)
        {
            OnItemsChanged();
        }
        else if (change.Property == SelectedIndexProperty)
        {
            if (!_suppressExternalSync)
            {
                // 外部（VM/绑定）写入：跳转到该索引（与手势共用状态通路）；越界收敛回 DP。
                var items = EnsureItems();
                var index = items.Count == 0 ? 0 : Math.Clamp((int)(change.NewValue ?? 0), 0, items.Count - 1);
                if (index != SelectedIndex)
                {
                    _suppressExternalSync = true;
                    SelectedIndex = index;
                    _suppressExternalSync = false;
                }

                _position = index;
                InvalidateVisual();
            }

            RaiseSelectionChangedIfNeeded();
        }
    }

    private void OnItemsChanged()
    {
        _regularLayouts.Clear();
        _selectedLayouts.Clear();
        StopAnimation();
        _isDragging = false;

        var items = EnsureItems();
        if (items.Count == 0)
        {
            _position = 0;
            _suppressExternalSync = true;
            SelectedIndex = 0;
            _suppressExternalSync = false;
        }
        else
        {
            var index = Math.Clamp(SelectedIndex, 0, items.Count - 1);
            _position = index;
            _suppressExternalSync = true;
            SelectedIndex = index;
            _suppressExternalSync = false;
        }

        RaiseSelectionChangedIfNeeded();
        InvalidateVisual();
    }

    private void RaiseSelectionChangedIfNeeded()
    {
        var items = EnsureItems();
        var selected = items.Count > 0 ? items[SelectedIndex] : null;
        if (string.Equals(_lastSelectedItem, selected, StringComparison.Ordinal))
        {
            return;
        }

        _lastSelectedItem = selected;
        if (selected is not null)
        {
            SelectedItemChanged?.Invoke(selected);
        }
    }

    // ---------------------------------------------------------------- 输入

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (EnsureItems().Count == 0)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        StopAnimation();
        _isDragging = true;
        _dragStartPosition = _position;
        _dragStartPointerY = point.Position.Y;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isDragging)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        var itemHeight = Math.Max(1.0, ItemHeight);
        // 手指上移 → 后一条；下移 → 前一条（对齐 CupertinoPicker 方向）。
        var raw = _dragStartPosition + (_dragStartPointerY - point.Position.Y) / itemHeight;
        _position = IsLoop ? raw : WheelSelectionLogic.ClampPosition(raw, EnsureItems().Count, IsLoop);
        UpdateContinuousIndex();
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndDrag();
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndDrag();
    }

    private void EndDrag()
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        var items = EnsureItems();
        if (items.Count == 0)
        {
            return;
        }

        // 松手吸附最近项（循环模式取最近等价路径，避免反向长动画）。
        var snap = WheelSelectionLogic.SnapToNearest(_position, items.Count, IsLoop);
        SetSelectedIndexCore(snap, animate: true);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var items = EnsureItems();
        if (items.Count <= 1)
        {
            return;
        }

        // 滚轮上滚（Delta.Y > 0）→ 前一条；下滚 → 后一条。
        if (e.Delta.Y > 0)
        {
            var target = WheelSelectionLogic.Prev(SelectedIndex, items.Count, isLinked: true, IsLoop);
            SetSelectedIndexCore(target, animate: true);
        }
        else if (e.Delta.Y < 0)
        {
            var target = WheelSelectionLogic.Next(SelectedIndex, items.Count, isLinked: true, IsLoop);
            SetSelectedIndexCore(target, animate: true);
        }

        e.Handled = true;
    }

    // ---------------------------------------------------------------- 渲染

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var items = EnsureItems();
        var itemHeight = Math.Max(1.0, ItemHeight);
        var bounds = Bounds;

        // 背景（VoiSlate.WheelItemBackground；未挂主题时回退原版 0xFFD1C4E9）。
        var background = TryBrush(VoiSlatePalette.WheelItemBackgroundKey, Color.Parse("#D1C4E9"));
        context.FillRectangle(background, new Rect(bounds.Size), (float)GetCornerRadius(bounds.Height));

        if (items.Count == 0)
        {
            return;
        }

        var centerY = bounds.Height / 2;

        // 中心选中带（原版 selectionOverlay：itemBackgroundColor.withAlpha(80)）。
        var bandColor = Color.FromArgb((byte)BandAlpha, 0xD1, 0xC4, 0xE9);
        context.FillRectangle(new SolidColorBrush(bandColor),
            new Rect(0, centerY - itemHeight / 2, bounds.Width, itemHeight));

        var fontSize = Math.Clamp(itemHeight * 0.5, 12, 32);
        var regularTypeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Normal);
        var selectedTypeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);

        // 可见范围（行）；循环模式渲染最近等价副本，保证拖过边界视觉连续。
        var halfVisible = (int)Math.Ceiling(bounds.Height / itemHeight / 2) + 1;
        for (var i = 0; i < items.Count; i++)
        {
            var offset = i - _position; // 相对中心的整项偏移
            if (IsLoop)
            {
                var count = items.Count;
                var a = Math.Abs(offset);
                var b = Math.Abs(offset - count);
                var c = Math.Abs(offset + count);
                offset = a <= b && a <= c ? offset : (b <= c ? offset - count : offset + count);
            }

            var y = centerY - itemHeight / 2 + offset * itemHeight;
            if (y + itemHeight < 0 || y > bounds.Height)
            {
                continue;
            }

            var isSelected = Math.Abs(offset) < 0.5;
            var layout = GetLayout(items[i], isSelected, fontSize, regularTypeface, selectedTypeface);
            var x = (bounds.Width - layout.Width) / 2;

            if (isSelected)
            {
                context.DrawText(layout, new Point(x, y));
            }
            else
            {
                // 非选中项淡出（模拟透视；同一文本布局，仅叠加透明度）。
                var distance = Math.Abs(offset);
                using (context.PushOpacity(Math.Clamp(1 - distance * 0.30, 0.25, 1)))
                {
                    context.DrawText(layout, new Point(x, y));
                }
            }
        }
    }

    private FormattedText GetLayout(string text, bool selected, double fontSize,
        Typeface regular, Typeface selectedFace)
    {
        var cache = selected ? _selectedLayouts : _regularLayouts;
        if (cache.TryGetValue(text, out var existing))
        {
            return existing;
        }

        var foreground = selected
            ? TryBrush(VoiSlatePalette.WheelTextSelectedKey, Color.Parse("#212121"))
            : TryBrush(VoiSlatePalette.WheelTextKey, Color.Parse("#5A5A5A"));
#pragma warning disable CS0618 // FormattedText/DrawText 为遗留 API（Avalonia 12 保留），控件内部文本绘制使用
        var layout = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            selected ? selectedFace : regular,
            fontSize,
            foreground);
#pragma warning restore CS0618
        cache[text] = layout;
        return layout;
    }

    private static IBrush TryBrush(string key, Color fallback)
    {
        if (Application.Current is not null && Application.Current.TryFindResource(key, out var value) &&
            value is IBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallback);
    }

    private static double GetCornerRadius(double height) => Math.Clamp(height * 0.12, 4, 12);
}