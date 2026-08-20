// DialFAB — 评价弹出拨盘（契约 §5 DialFAB 行；对齐原版 speed_dial 的 take_ok_dial /
// shot_ok_dial 语义）。
//
// 依赖属性：
//   Options         IReadOnlyList<DialOption>?  OneWay
//   SelectedOption  DialOption?                 TwoWay
// 事件：SelectionChanged(DialOption) / Opened / Closed
//
// 行为协议落实：
//   * 点击主按钮展开选项（原版 SpeedDial 向上展开）；点击外部关闭（Popup IsLightDismissEnabled）；
//   * 点击选项 → SelectedOption（TwoWay 回源）+ SelectionChanged；
//   * 选中后主按钮背景色/图标随状态回显：NotChecked=浅色/Ok=绿/Bad=红/Nice=金黄
//     （DialStatusPalette；EnumValue → TkStatus/ShtStatus 的转换由 C 完成，见契约 §4 B6）；
//   * 实例化：C 用 Options 装配"声音可/声音弃"（TkStatus）与"画面保/画面过"（ShtStatus）两组。

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace VoiSlate.Controls;

/// <summary>评价状态拨盘（可复用；记录页两个实例：声音组 / 画面组）。</summary>
public class DialFAB : TemplatedControl
{
    public static readonly StyledProperty<IReadOnlyList<DialOption>?> OptionsProperty =
        AvaloniaProperty.Register<DialFAB, IReadOnlyList<DialOption>?>(
            nameof(Options), defaultValue: null, defaultBindingMode: Avalonia.Data.BindingMode.OneWay);

    public static readonly StyledProperty<DialOption?> SelectedOptionProperty =
        AvaloniaProperty.Register<DialFAB, DialOption?>(
            nameof(SelectedOption), defaultValue: null, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private Border? _mainButton;
    private Popup? _popup;
    private StackPanel? _optionsPanel;
    private ContentControl? _mainIcon;
    private IReadOnlyList<DialOption> _builtOptions = Array.Empty<DialOption>();

    static DialFAB()
    {
        AffectsRender<DialFAB>(SelectedOptionProperty);
    }

    public IReadOnlyList<DialOption>? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    public DialOption? SelectedOption
    {
        get => GetValue(SelectedOptionProperty);
        set => SetValue(SelectedOptionProperty, value);
    }

    /// <summary>选项被选中（契约 §5）。</summary>
    public event Action<DialOption>? SelectionChanged;

    /// <summary>展开（契约 §5）。</summary>
    public event Action? Opened;

    /// <summary>关闭（契约 §5；含点击外部关闭）。</summary>
    public event Action? Closed;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _mainButton = e.NameScope.Find<Border>("PART_MainButton");
        _mainIcon = e.NameScope.Find<ContentControl>("PART_MainIcon");
        _optionsPanel = e.NameScope.Find<StackPanel>("PART_Options");
        _popup = e.NameScope.Find<Popup>("PART_Popup");

        if (_popup is not null)
        {
            _popup.PlacementTarget = this;
            _popup.Placement = PlacementMode.Top;
            _popup.IsLightDismissEnabled = true;
            _popup.Opened += (_, _) => Opened?.Invoke();
            _popup.Closed += (_, _) => Closed?.Invoke();
        }

        if (_mainButton is not null)
        {
            _mainButton.PointerPressed += OnMainButtonPressed;
        }

        RebuildOptions();
        UpdateMainButtonVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == OptionsProperty)
        {
            RebuildOptions();
        }
        else if (change.Property == SelectedOptionProperty)
        {
            UpdateMainButtonVisual();
        }
    }

    // ---------------------------------------------------------------- 展开/收起

    public void Open()
    {
        if (_popup is not null)
        {
            _popup.IsOpen = true;
        }
    }

    public void Close()
    {
        if (_popup is not null)
        {
            _popup.IsOpen = false;
        }
    }

    private void Toggle()
    {
        if (_popup is { IsOpen: true })
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    private void OnMainButtonPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            Toggle();
            e.Handled = true;
        }
    }

    // ---------------------------------------------------------------- 选项装配

    private void RebuildOptions()
    {
        _builtOptions = Options ?? Array.Empty<DialOption>();
        if (_optionsPanel is null)
        {
            return;
        }

        _optionsPanel.Children.Clear();

        foreach (var option in _builtOptions)
        {
            var row = new Border
            {
                Background = statusBrush(option),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 8),
                Margin = new Thickness(4, 2),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        TextOrIcon(option.Icon, light: true),
                        new TextBlock { Text = option.Label, Foreground = Brushes.White, FontSize = 14 },
                    },
                },
            };
            var captured = option;
            row.PointerPressed += (_, args) =>
            {
                if (args.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    Select(captured);
                    args.Handled = true;
                }
            };
            _optionsPanel.Children.Add(row);
        }
    }

    private void Select(DialOption option)
    {
        SelectedOption = option; // TwoWay 回源
        SelectionChanged?.Invoke(option);
        Close();
    }

    // ---------------------------------------------------------------- 主按钮回显

    private void UpdateMainButtonVisual()
    {
        if (_mainButton is null)
        {
            return;
        }

        var selected = SelectedOption;
        _mainButton.Background = statusBrush(selected);
        if (_mainIcon is not null)
        {
            _mainIcon.Content = TextOrIcon(selected?.Icon ?? DefaultIconGlyph, light: false);
        }
    }

    private const string DefaultIconGlyph = "●"; // 未选中占位（NotChecked 浅色背景 + 圆圈）

    private static IBrush statusBrush(DialOption? option) => statusBrush(option?.EnumValue);

    private static IBrush statusBrush(object? enumValue)
    {
        // 主题资源优先，否则回退常量（与 VoiSlatePalette.axaml 同值）。
        var key = DialStatusPalette.StatusResourceKey(enumValue);
        var fallback = DialStatusPalette.StatusColor(enumValue);
        if (Application.Current is not null && Application.Current.TryFindResource(key, out var value) &&
            value is IBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallback);
    }

    /// <summary>把 DialOption.Icon（Geometry / string / IImage 等）渲染为可显示控件。</summary>
    private static Control TextOrIcon(object? icon, bool light)
    {
        var foreground = light ? Brushes.White : Brushes.Black;
        switch (icon)
        {
            case Geometry geometry:
                return new GeometryIcon { Data = geometry, Fill = foreground };
            case string glyph when !string.IsNullOrEmpty(glyph):
                return new TextBlock
                {
                    Text = glyph,
                    FontSize = 18,
                    Foreground = foreground,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                };
            case IImage image:
                return new Image { Source = image };
            default:
                return new TextBlock
                {
                    Text = icon?.ToString() ?? string.Empty,
                    FontSize = 18,
                    Foreground = foreground,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                };
        }
    }

    /// <summary>Geometry 图标渲染（避免依赖具体 Path 控件类型）。</summary>
    private sealed class GeometryIcon : Control
    {
        public Geometry? Data { get; set; }

        public IBrush? Fill { get; set; }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            if (Data is null)
            {
                return;
            }

            context.DrawGeometry(Fill ?? Brushes.Black, null, Data);
        }
    }
}