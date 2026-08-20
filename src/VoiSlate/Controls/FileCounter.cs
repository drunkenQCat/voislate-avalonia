// FileCounter — 文件号三卡片计数器（契约 §5 FileCounter 行；对齐原版 file_counter.dart）。
//
// 依赖属性：
//   Prefix      string?  OneWay   前缀（recorderType 决定 Date/SoundDevices/Custom 文本）
//   Linker      string?  OneWay   分隔符（原版 intervalSymbol，默认 "-T"）
//   NumberText  string   TwoWay   编号文本（VM/RecordCount 双向同步；显示 D3 补零）
//   NumberValue int      只读     由 NumberText 解析（下限 1，"不输入 0"）
// 事件：EditRequested(EditRequestedSection)
//
// 行为协议落实：
//   * 三张卡片（前缀/分隔符/编号）长按/点击 → EditRequested(Prefix|Linker|Number)，
//     编辑对话框由 C 按契约 §5 说明实现（Prefix=三模式 Toggle + custom 文本；
//     Linker=文本；Number=整型，编号输入校验用 FileNumberFormat.TryParseNumber）；
//   * 编号显示 D3 补零（FileNumberFormat.Pad3）；
//   * 前缀标签：纯数字 → Date，否则 Custom（原版 regex ^[0-9]+$）。

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace VoiSlate.Controls;

/// <summary>文件号三卡片计数器。</summary>
public class FileCounter : TemplatedControl
{
    public static readonly StyledProperty<string?> PrefixProperty =
        AvaloniaProperty.Register<FileCounter, string?>(
            nameof(Prefix), defaultValue: null, defaultBindingMode: Avalonia.Data.BindingMode.OneWay);

    public static readonly StyledProperty<string?> LinkerProperty =
        AvaloniaProperty.Register<FileCounter, string?>(
            nameof(Linker), defaultValue: "-T", defaultBindingMode: Avalonia.Data.BindingMode.OneWay);

    public static readonly StyledProperty<string?> NumberTextProperty =
        AvaloniaProperty.Register<FileCounter, string?>(
            nameof(NumberText), defaultValue: "1", defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    // 契约：NumberValue(int 只读)。
    public static readonly StyledProperty<int> NumberValueProperty =
        AvaloniaProperty.Register<FileCounter, int>(
            nameof(NumberValue), defaultValue: 1, defaultBindingMode: Avalonia.Data.BindingMode.OneWay);

    private Border? _prefixCard;
    private Border? _linkerCard;
    private Border? _numberCard;
    private TextBlock? _prefixTag;
    private TextBlock? _prefixText;
    private TextBlock? _linkerText;
    private TextBlock? _numberText;

    static FileCounter()
    {
        AffectsRender<FileCounter>(PrefixProperty, LinkerProperty, NumberTextProperty);
    }

    public string? Prefix
    {
        get => GetValue(PrefixProperty);
        set => SetValue(PrefixProperty, value);
    }

    public string? Linker
    {
        get => GetValue(LinkerProperty);
        set => SetValue(LinkerProperty, value);
    }

    public string? NumberText
    {
        get => GetValue(NumberTextProperty);
        set => SetValue(NumberTextProperty, value);
    }

    /// <summary>编号数值（只读；由 NumberText 解析，下限 1）。</summary>
    public int NumberValue
    {
        get => GetValue(NumberValueProperty);
        private set => SetValue(NumberValueProperty, value);
    }

    /// <summary>编辑请求（契约 §5；C 据此弹出对应编辑对话框）。</summary>
    public event Action<EditRequestedSection>? EditRequested;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _prefixCard = e.NameScope.Find<Border>("PART_PrefixCard");
        _linkerCard = e.NameScope.Find<Border>("PART_LinkerCard");
        _numberCard = e.NameScope.Find<Border>("PART_NumberCard");
        _prefixTag = e.NameScope.Find<TextBlock>("PART_PrefixTag");
        _prefixText = e.NameScope.Find<TextBlock>("PART_PrefixText");
        _linkerText = e.NameScope.Find<TextBlock>("PART_LinkerText");
        _numberText = e.NameScope.Find<TextBlock>("PART_NumberText");

        HookCardClick(_prefixCard, EditRequestedSection.Prefix);
        HookCardClick(_linkerCard, EditRequestedSection.Linker);
        HookCardClick(_numberCard, EditRequestedSection.Number);

        UpdateDisplays();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PrefixProperty || change.Property == LinkerProperty ||
            change.Property == NumberTextProperty)
        {
            UpdateDisplays();
        }
    }

    private void HookCardClick(Border? card, EditRequestedSection section)
    {
        if (card is null)
        {
            return;
        }

        card.Cursor = new Cursor(StandardCursorType.Hand);
        card.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(card).Properties.IsLeftButtonPressed)
            {
                EditRequested?.Invoke(section);
                e.Handled = true;
            }
        };
    }

    private void UpdateDisplays()
    {
        // NumberValue 与模板无关，始终由 NumberText 解析（契约：只读、下限 1）。
        var parsed = FileNumberFormat.TryParseNumber(NumberText, out var number) ? number : 1;
        NumberValue = parsed;

        if (_numberText is not null)
        {
            _numberText.Text = FileNumberFormat.Pad3(parsed);
        }

        if (_prefixText is not null)
        {
            _prefixText.Text = Prefix ?? string.Empty;
        }

        if (_prefixTag is not null)
        {
            _prefixTag.Text = FileNumberFormat.IsDatePrefix(Prefix) ? "Date" : "Custom";
        }

        if (_linkerText is not null)
        {
            _linkerText.Text = Linker ?? string.Empty;
        }
    }
}