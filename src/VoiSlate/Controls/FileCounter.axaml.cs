using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace VoiSlate.Controls;

/// <summary>契约 §5：文件号编辑分区（D 产出；视图按分区打开对应编辑对话框）。</summary>
public enum EditRequestedSection
{
    Prefix,
    Linker,
    Number,
}

/// <summary>
/// Agent C 占位 FileCounter（契约 v0.5 §5 签名一致）。
/// 显示 prefix + linker + number(D3)；三个编辑按钮触发 EditRequested 事件，
/// 由视图层开对话框并经 ITakeFlowService.SetFileNumberAsync/SetLinkerAsync/SetPrefixAsync 写回（BLOCKER-1/C-2）。
/// D 合入后删除本文件（连同 FileCounter.axaml）。
/// </summary>
public partial class FileCounter : UserControl
{
    private int _numberValue = 1;

    public FileCounter()
    {
        InitializeComponent();
        UpdateDisplay();
    }

    public static readonly StyledProperty<string> PrefixProperty =
        AvaloniaProperty.Register<FileCounter, string>(nameof(Prefix), defaultValue: string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>契约 §5：文件名前缀文本（TwoWay）。</summary>
    public string Prefix
    {
        get => GetValue(PrefixProperty);
        set => SetValue(PrefixProperty, value);
    }

    public static readonly StyledProperty<string> LinkerProperty =
        AvaloniaProperty.Register<FileCounter, string>(nameof(Linker), defaultValue: "-T", defaultBindingMode: BindingMode.TwoWay);

    /// <summary>契约 §5：链接符文本（TwoWay；默认 -T）。</summary>
    public string Linker
    {
        get => GetValue(LinkerProperty);
        set => SetValue(LinkerProperty, value);
    }

    public static readonly StyledProperty<string> NumberTextProperty =
        AvaloniaProperty.Register<FileCounter, string>(nameof(NumberText), defaultValue: "001", defaultBindingMode: BindingMode.TwoWay);

    /// <summary>契约 §5：编号文本（TwoWay，D3 补零）。</summary>
    public string NumberText
    {
        get => GetValue(NumberTextProperty);
        set => SetValue(NumberTextProperty, value);
    }

    public static readonly DirectProperty<FileCounter, int> NumberValueProperty =
        AvaloniaProperty.RegisterDirect<FileCounter, int>(nameof(NumberValue), o => o.NumberValue, (o, v) => o.NumberValue = v);

    /// <summary>契约 §5：编号数值（只读；由 NumberText 解析得出）。</summary>
    public int NumberValue
    {
        get => _numberValue;
        private set => _numberValue = value;
    }

    /// <summary>契约 §5：编辑请求（Prefix/Linker/Number）。</summary>
    public event Action<EditRequestedSection>? EditRequested;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PrefixProperty || change.Property == LinkerProperty || change.Property == NumberTextProperty)
        {
            var parsed = int.TryParse(NumberText, out var n) ? n : 1;
            if (parsed != _numberValue)
            {
                SetValue(NumberValueProperty, parsed);
            }

            UpdateDisplay();
        }
    }

    private void UpdateDisplay()
    {
        if (PrefixText == null || LinkerText == null || NumberTextBlock == null || NumberValueText == null) return;
        PrefixText.Text = Prefix;
        LinkerText.Text = Linker;
        NumberTextBlock.Text = NumberText;
        NumberValueText.Text = $"编号数值：{NumberValue}（D3 补零：{NumberValue:D3}；编辑走 ITakeFlowService，C-2 唯一写入口）";
    }

    private void OnEditPrefixClick(object? sender, RoutedEventArgs e) => EditRequested?.Invoke(EditRequestedSection.Prefix);

    private void OnEditLinkerClick(object? sender, RoutedEventArgs e) => EditRequested?.Invoke(EditRequestedSection.Linker);

    private void OnEditNumberClick(object? sender, RoutedEventArgs e) => EditRequested?.Invoke(EditRequestedSection.Number);
}