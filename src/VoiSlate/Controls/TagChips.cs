// TagChips — 标签 chip 流式排列（契约 §5 TagChips 行；对齐原版 tag_chips.dart）。
//
// 依赖属性：
//   Tags  IReadOnlyList<string>?  OneWay
// 事件：AddRequested / EditRequested(string) / DeleteRequested(string)
//
// 行为协议落实：
//   * 流式排列（WrapPanel）；每 chip：点击 → EditRequested(tag)，删除按钮 → DeleteRequested(tag)；
//   * 末尾"+"chip → AddRequested；
//   * Add/Edit 共用编辑对话框（TagEditingMessage 语义）由 C 实现（NoteEditor 归属 C）。

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace VoiSlate.Controls;

/// <summary>标签 chips 流式展示与交互。</summary>
public class TagChips : TemplatedControl
{
    public static readonly StyledProperty<IReadOnlyList<string>?> TagsProperty =
        AvaloniaProperty.Register<TagChips, IReadOnlyList<string>?>(
            nameof(Tags), defaultValue: null, defaultBindingMode: Avalonia.Data.BindingMode.OneWay);

    private WrapPanel? _itemsPanel;

    public IReadOnlyList<string>? Tags
    {
        get => GetValue(TagsProperty);
        set => SetValue(TagsProperty, value);
    }

    /// <summary>添加请求（点"+"chip；契约 §5）。</summary>
    public event Action? AddRequested;

    /// <summary>编辑请求（点 chip 主体；契约 §5）。</summary>
    public event Action<string>? EditRequested;

    /// <summary>删除请求（点 chip 的 ×；契约 §5）。</summary>
    public event Action<string>? DeleteRequested;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _itemsPanel = e.NameScope.Find<WrapPanel>("PART_Items");
        RebuildChips();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TagsProperty)
        {
            RebuildChips();
        }
    }

    private void RebuildChips()
    {
        if (_itemsPanel is null)
        {
            return;
        }

        _itemsPanel.Children.Clear();

        foreach (var tag in Tags ?? Array.Empty<string>())
        {
            _itemsPanel.Children.Add(BuildChip(tag));
        }

        _itemsPanel.Children.Add(BuildAddChip());
    }

    private Border BuildChip(string tag)
    {
        var delete = new TextBlock
        {
            Text = "×",
            FontSize = 14,
            Foreground = Brushes.White,
            Margin = new Thickness(6, 0, 0, 0),
        };
        var captured = tag;
        delete.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(delete).Properties.IsLeftButtonPressed)
            {
                DeleteRequested?.Invoke(captured);
                e.Handled = true;
            }
        };

        var chip = new Border
        {
            Background = TryBrush(VoiSlatePalette.PrimaryKey, Color.Parse("#0067A0")),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 4),
            Margin = new Thickness(2),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new TextBlock { Text = tag, FontSize = 13, Foreground = Brushes.White },
                    delete,
                },
            },
        };
        chip.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(chip).Properties.IsLeftButtonPressed)
            {
                EditRequested?.Invoke(captured);
                e.Handled = true;
            }
        };
        return chip;
    }

    private Border BuildAddChip()
    {
        var chip = new Border
        {
            Background = TryBrush(VoiSlatePalette.DialNotCheckedKey, Color.Parse("#F2F5DE")),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 4),
            Margin = new Thickness(2),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new TextBlock { Text = "＋", FontSize = 16, Foreground = Brushes.Black },
        };
        chip.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(chip).Properties.IsLeftButtonPressed)
            {
                AddRequested?.Invoke();
                e.Handled = true;
            }
        };
        return chip;
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
}