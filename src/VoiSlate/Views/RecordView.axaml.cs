using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VoiSlate.Controls;
using VoiSlate.Models;
using VoiSlate.ViewModels;

namespace VoiSlate.Views;

/// <summary>
/// Agent C：记录页视图（契约 §6/N3）。
/// Loaded/Unloaded → RecordViewModel.Activate()/Deactivate()（契约 B5：防音量键泄漏/漏订，
/// 文件号同步经此钩子 C-2）。
/// DialFAB EnumValue → Model 枚举转换（契约 B-6 由 C 在视图完成）；文件号编辑经
/// ITakeFlowService 唯一写入口（BLOCKER-1）。
/// </summary>
public partial class RecordView : UserControl
{
    private bool _dialsBuilt;

    private RecordViewModel? Vm => DataContext as RecordViewModel;

    public RecordView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        QuickViewButton.Click += OnQuickViewClick;
        FileCounterCtl.EditRequested += OnFileCounterEditRequested;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;
        if (!_dialsBuilt)
        {
            BuildDials(vm);
            _dialsBuilt = true;
        }

        vm.Activate();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;
        vm.Deactivate();
    }

    /// <summary>契约 §5：DialFAB ×2（实例 1 声音可/弃 → TkStatus；实例 2 画面保/过 → ShtStatus）。</summary>
    private void BuildDials(RecordViewModel vm)
    {
        TkDial.Options =
        [
            new DialOption("声音可", "✓", TkStatus.Ok),
            new DialOption("声音弃", "✗", TkStatus.Bad),
        ];
        TkDial.SelectionChanged += option =>
        {
            if (option.EnumValue is TkStatus status)
            {
                vm.SetOkTake(status);
            }
        };

        ShtDial.Options =
        [
            new DialOption("画面保", "✓", ShtStatus.Ok),
            new DialOption("画面过", "★", ShtStatus.Nice),
        ];
        ShtDial.SelectionChanged += option =>
        {
            if (option.EnumValue is ShtStatus status)
            {
                vm.SetOkShot(status);
            }
        };
    }

    private async void OnFileCounterEditRequested(EditRequestedSection section) => await HandleEditAsync(section);

    private async Task HandleEditAsync(EditRequestedSection section)
    {
        if (Vm is not { } vm) return;
        var owner = this.VisualRoot as Window;

        switch (section)
        {
            case EditRequestedSection.Number:
            {
                var (ok, text) = await SmallEditDialog.ShowAsync(
                    owner, "编辑文件号", "文件号（下限 1；D3 补零显示）", vm.NumberText);
                if (ok && int.TryParse(text, out var number) && number >= 1)
                {
                    await vm.EditFileNumberAsync(number);
                }

                break;
            }
            case EditRequestedSection.Linker:
            {
                var (ok, text) = await SmallEditDialog.ShowAsync(
                    owner, "编辑链接符", "链接符（默认 -T，B6）", vm.LinkerText);
                if (ok)
                {
                    await vm.EditLinkerAsync(text);
                }

                break;
            }
            case EditRequestedSection.Prefix:
            {
                var (ok, modeText) = await SmallEditDialog.ShowAsync(
                    owner,
                    "编辑前缀模式",
                    "文件名前缀三模式（B6：默认日期 yymmdd / 声音设备 yyYmMd / 自定义）",
                    string.Empty,
                    ["默认（日期 yymmdd）", "声音设备（yyYmMd）", "自定义"]);
                if (!ok)
                {
                    return;
                }

                var mode = modeText switch
                {
                    "声音设备（yyYmMd）" => PrefixType.SoundDevices,
                    "自定义" => PrefixType.Custom,
                    _ => PrefixType.Default,
                };

                string? custom = null;
                if (mode == PrefixType.Custom)
                {
                    var (ok2, customText) = await SmallEditDialog.ShowAsync(
                        owner, "自定义前缀", "前缀文本（custom）", vm.PrefixText);
                    if (!ok2)
                    {
                        return;
                    }

                    custom = customText;
                }

                await vm.EditPrefixAsync(mode, custom);
                break;
            }
        }
    }

    private async void OnQuickViewClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;
        await vm.RefreshQuickNotesAsync();
        var owner = this.VisualRoot as Window;
        await QuickViewLogWindow.ShowAsync(owner, vm.QuickNotes.ToList());
    }
}