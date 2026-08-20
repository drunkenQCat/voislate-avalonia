using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using VoiSlate.Models;

namespace VoiSlate.Controls;

/// <summary>
/// 契约 §5：拨盘选项（D 产出的显示数据；EnumValue 为 object，携带 Model 枚举
/// TkStatus/ShtStatus——由 C 在视图 code-behind 映射表完成转换，B-6）。
/// </summary>
public sealed class DialOption
{
    public string Label { get; }
    public string Icon { get; }
    public object? EnumValue { get; }

    public DialOption(string label, string icon, object? enumValue = null)
    {
        Label = label;
        Icon = icon;
        EnumValue = enumValue;
    }
}

/// <summary>
/// Agent C 占位 DialFAB（契约 v0.5 §5 签名一致）。
/// 背景色/图标按 EnumValue 回显（NotChecked 浅色 / Ok 绿 / Bad 红 / Nice 金黄）；
/// 展开点外部关闭由 Flyout 宿主承担。
/// D 合入后删除本文件（连同 DialFAB.axaml）。
/// </summary>
public partial class DialFAB : UserControl
{
    private Flyout? _flyout;

    public DialFAB()
    {
        InitializeComponent();
        MainButton.Click += OnMainClick;
    }

    public static readonly DirectProperty<DialFAB, IReadOnlyList<DialOption>?> OptionsProperty =
        AvaloniaProperty.RegisterDirect<DialFAB, IReadOnlyList<DialOption>?>(
            nameof(Options), o => o.Options, (o, v) => o.Options = v);

    private IReadOnlyList<DialOption>? _options;

    /// <summary>契约 §5：拨盘选项（示例 1：声音可/弃；示例 2：画面保/过）。</summary>
    public IReadOnlyList<DialOption>? Options
    {
        get => _options;
        set
        {
            _options = value;
            RebuildFlyout();
        }
    }

    public static readonly DirectProperty<DialFAB, DialOption?> SelectedOptionProperty =
        AvaloniaProperty.RegisterDirect<DialFAB, DialOption?>(
            nameof(SelectedOption), o => o.SelectedOption, (o, v) => o.SelectedOption = v);

    private DialOption? _selectedOption;

    /// <summary>契约 §5：当前选中项（TwoWay）。</summary>
    public DialOption? SelectedOption
    {
        get => _selectedOption;
        set
        {
            SetAndRaise(SelectedOptionProperty, ref _selectedOption, value);
            RefreshMainButton();
        }
    }

    /// <summary>契约 §5：选项变化（DialOption.EnumValue → Model 枚举的转换由 C 在视图完成）。</summary>
    public event Action<DialOption>? SelectionChanged;

    /// <summary>契约 §5：展开。</summary>
    public event Action? Opened;

    /// <summary>契约 §5：收起。</summary>
    public event Action? Closed;

    private void OnMainClick(object? sender, RoutedEventArgs e)
    {
        if (_flyout == null) RebuildFlyout();
        if (_flyout == null) return;
        if (_flyout.IsOpen)
        {
            _flyout.Hide();
        }
        else
        {
            _flyout.ShowAt(MainButton);
            Opened?.Invoke();
        }
    }

    private void RebuildFlyout()
    {
        if (Options == null) return;

        var panel = new StackPanel { Spacing = 4, MinWidth = 140 };
        foreach (var option in Options)
        {
            var optionButton = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = option.Icon, FontSize = 16, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center },
                        new TextBlock { Text = option.Label, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center },
                    },
                },
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                Background = BrushFor(option.EnumValue),
            };
            optionButton.Click += (_, _) =>
            {
                SelectedOption = option;
                SelectionChanged?.Invoke(option);
                _flyout?.Hide();
            };
            panel.Children.Add(optionButton);
        }

        _flyout = new Flyout
        {
            Content = new Border { Child = panel, Padding = new Thickness(8), MinWidth = 160 },
            Placement = PlacementMode.Bottom,
            ShowMode = FlyoutShowMode.Standard,
        };
        _flyout.Closed += (_, _) => Closed?.Invoke();
    }

    private void RefreshMainButton()
    {
        if (MainButton == null || IconText == null || LabelText == null) return;
        var first = Options is { Count: > 0 } opts ? opts[0] : null;
        IconText.Text = SelectedOption?.Icon ?? first?.Icon ?? "✓";
        LabelText.Text = SelectedOption?.Label ?? first?.Label ?? string.Empty;
        var value = SelectedOption?.EnumValue ?? first?.EnumValue;
        MainButton.Background = BrushFor(value);
    }

    /// <summary>状态 → 背景色（NotChecked 浅色 / Ok 绿 / Bad 红 / Nice 金黄；占位映射）。</summary>
    private static IBrush BrushFor(object? value) => value switch
    {
        TkStatus.Bad => Brush("#C62828"),
        ShtStatus.Nice => Brush("#B8860B"),
        TkStatus.Ok or ShtStatus.Ok => Brush("#2E7D32"),
        _ => Brush("#E8F0FA"),
    };

    private static SolidColorBrush Brush(string hex) => new(Color.Parse(hex));
}