using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using VoiSlate.Controls;
using VoiSlate.Models;
using VoiSlate.ViewModels;

namespace VoiSlate.Views;

/// <summary>
/// 记录页视图（复刻 voislate-html 之 page-record，布局规范 docs/ui-layout-spec.md §3）。
/// Loaded/Unloaded → RecordViewModel.Activate()/Deactivate()（契约 B5）。
/// 新增交互（HTML app.js 映射）：NEXT 折叠、补录 pill、600ms 长按撤回、joystick 拖拽阈值、
/// 触控/锁定、monitor-card 长按改镜（占位）、预览文本联动。
/// </summary>
public partial class RecordView : UserControl
{
    private const double LongPressMilliseconds = 600;

    private bool _dialsBuilt;
    private DispatcherTimer? _longPressTimer;
    private bool _isLocked;
    private bool _joyDragging;

    // ---- joystick 拖拽状态 ----
    private const double JoyMinL = 5;
    private const double JoyMaxL = 55;
    private const double JoyBase = 32;
    private double _joyStartX;
    private double _joyPos = JoyBase;

    private RecordViewModel? Vm => DataContext as RecordViewModel;

    public RecordView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        QuickViewButton.Click += OnQuickViewClick;

        NextHead.Click += OnNextHeadClick;
        LockTouch.Click += (_, _) => SetLocked(false);
        LockLocked.Click += (_, _) => SetLocked(true);

        // 撤回：长按 600ms 触发
        BtnDec.PointerPressed += OnDecPressed;
        BtnDec.PointerReleased += OnDecReleased;
        BtnDec.PointerCaptureLost += OnDecReleased;
        BtnDec.LostFocus += (_, _) => StopLongPress();

        // joystick
        JoyKnob.PointerPressed += OnJoyPressed;
        Joy.PointerMoved += OnJoyMoved;
        Joy.PointerReleased += OnJoyReleased;
        Joy.PointerCaptureLost += OnJoyCaptureLost;

        MonitorCard.PointerPressed += OnMonitorPressed;
        MonitorCard.PointerReleased += OnMonitorReleased;
        MonitorCard.PointerCaptureLost += OnMonitorReleased;
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
        vm.SceneCol.PropertyChanged += OnColumnChanged;
        vm.ShotCol.PropertyChanged += OnColumnChanged;
        vm.TakeCol.PropertyChanged += OnColumnChanged;
        vm.PropertyChanged += OnVmPropertyChanged;
        RefreshDynamicTexts();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        StopLongPress();
        if (Vm is not { } vm) return;
        vm.SceneCol.PropertyChanged -= OnColumnChanged;
        vm.ShotCol.PropertyChanged -= OnColumnChanged;
        vm.TakeCol.PropertyChanged -= OnColumnChanged;
        vm.PropertyChanged -= OnVmPropertyChanged;
        vm.Deactivate();
    }

    // ================================================================ 动态文本联动

    private void OnColumnChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(SlateColumnViewModel.SelectedIndex)
                or nameof(SlateColumnViewModel.SelectedItem)))
        {
            return;
        }

        RefreshDynamicTexts();
        if (sender is SlateColumnViewModel && ReferenceEquals(sender, Vm?.SceneCol) ||
            ReferenceEquals(sender, Vm?.ShotCol))
        {
            ShowShotHint(true);
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RecordViewModel.IsLinked))
        {
            UpdateLinkPillVisual();
        }
        else if (e.PropertyName is nameof(RecordViewModel.FileNumber)
                 or nameof(RecordViewModel.PrefixText)
                 or nameof(RecordViewModel.LinkerText))
        {
            RefreshDynamicTexts();
        }
    }

    private void RefreshDynamicTexts()
    {
        if (Vm is not { } vm) return;
        var scn = vm.SceneCol.SelectedItem ?? "1A";
        var sht = vm.ShotCol.SelectedItem ?? "1A";
        var tk = vm.TakeCol.SelectedItem ?? "1";

        PreviewScn.Text = scn;
        PreviewSht.Text = sht;
        PreviewTk.Text = tk.PadLeft(3, '0');
        NextText.Text = $"{scn}场{sht}镜{tk}次";
        ShtTitle.Text = $"S{scn} Sh{sht} Tk";
        TkBadgeText.Text = tk;
        RecTitle.Text = $"正在录制:{vm.LinkerText.TrimStart('-')}{vm.NumberText}";
    }

    // ================================================================ NEXT 折叠

    private void OnNextHeadClick(object? sender, RoutedEventArgs e)
    {
        var open = !NextBody.IsVisible;
        NextBody.IsVisible = open;
        PreviewWheels.IsVisible = !open;
        NextText.IsVisible = open;
        Chev.RenderTransform = new RotateTransform(open ? 180 : 0);
    }

    private void UpdateLinkPillVisual()
    {
        if (Vm is not { } vm) return;
        var linked = vm.IsLinked;
        LinkPill.Background = new SolidColorBrush(Color.Parse(linked ? "#D9FFFFFF" : "#FF9E9E9E"));
        LinkPillIcon.Text = linked ? "🔗" : "🚫";
        (LinkPill.Foreground) = new SolidColorBrush(Color.Parse(linked ? "#37474F" : "White"));
        NextLabel.Text = linked ? "NEXT" : "补录";
        NextLabel.FontSize = linked ? 15 : 12;
        MonitorCard.Background = new SolidColorBrush(Color.Parse(linked ? "White" : "#E0E0E0"));
    }

    // ================================================================ 600ms 长按撤回

    private void OnDecPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Pointer.Capture(BtnDec);
        StopLongPress();
        _longPressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(LongPressMilliseconds) };
        _longPressTimer.Tick += (_, _) =>
        {
            StopLongPress();
            if (Vm is { } vm)
            {
                vm.RewindTakeCommand.Execute(null);
                ShowShotHint(false);
            }
        };
        _longPressTimer.Start();
    }

    private void OnDecReleased(object? sender, EventArgs e) => StopLongPress();

    private void StopLongPress()
    {
        if (_longPressTimer is null) return;
        _longPressTimer.Stop();
        _longPressTimer = null;
    }

    // ================================================================ joystick（RecorderJoystick）

    private void OnJoyPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Vm is { } vm) vm.StartAsr(); // 原版按下开始录音（Mock ASR 语义）
        _joyStartX = e.GetPosition(Joy).X;
        _joyPos = JoyBase;
        _joyDragging = true;
        e.Pointer.Capture(Joy);
    }

    private void OnJoyMoved(object? sender, PointerEventArgs e)
    {
        if (!_joyDragging) return;
        var x = e.GetPosition(Joy).X;
        _joyPos = Math.Clamp(JoyBase + (x - _joyStartX), JoyMinL, JoyMaxL);
        JoyKnob.Margin = new Thickness(_joyPos, 5, 0, 0);
    }

    private void OnJoyReleased(object? sender, PointerReleasedEventArgs e) => EndJoystick();

    private void OnJoyCaptureLost(object? sender, PointerCaptureLostEventArgs e) => EndJoystick();

    private void EndJoystick()
    {
        if (!_joyDragging) return;
        _joyDragging = false;
        if (Vm is not { } vm) return;
        vm.StopAsr();
        if (_joyPos > JoyMaxL - 12.5)
        {
            NoteInput.Text = "录音内容已转为镜头标注（语音识别演示）";
        }
        else if (_joyPos < JoyMinL + 12.5)
        {
            DescInput.Text = "录音内容已转为录音描述（语音识别演示）";
        }

        DispatcherTimer.RunOnce(
            () => JoyKnob.Margin = new Thickness(JoyBase, 5, 0, 0),
            TimeSpan.FromMilliseconds(200));
        _joyPos = JoyBase;
    }

    // ================================================================ 长按 monitor-card → 修改当前镜

    private DispatcherTimer? _monitorTimer;

    private void OnMonitorPressed(object? sender, PointerPressedEventArgs e)
    {
        _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(LongPressMilliseconds) };
        _monitorTimer.Tick += async (_, _) =>
        {
            _monitorTimer = null;
            await HandleMonitorLongPressAsync();
        };
        _monitorTimer.Start();
    }

    private void OnMonitorReleased(object? sender, EventArgs e)
    {
        if (_monitorTimer is null) return;
        _monitorTimer.Stop();
        _monitorTimer = null;
    }

    private async Task HandleMonitorLongPressAsync()
    {
        if (Vm is not { } vm) return;
        var owner = this.VisualRoot as Window;
        var (ok, text) = await SmallEditDialog.ShowAsync(
            owner, "修改当前镜", $"{vm.SceneCol.SelectedItem} 场 {vm.ShotCol.SelectedItem} 镜（计划服务接线后持久化）",
            string.Empty);
        _ = ok; _ = text;
    }

    // ================================================================ 触控 / 锁定

    private void SetLocked(bool locked)
    {
        _isLocked = locked;
        LockTouch.Background = new SolidColorBrush(locked ? Colors.Transparent : Colors.White);
        LockLocked.Background = new SolidColorBrush(locked ? Colors.White : Colors.Transparent);
        LockTouch.Opacity = locked ? 0.6 : 1;
        LockLocked.Opacity = locked ? 1 : 0.6;

        var enabled = !locked;
        BtnAdd.IsEnabled = enabled;
        BtnFake.IsEnabled = enabled;
        BtnDec.IsEnabled = enabled;
        DescInput.IsEnabled = enabled;
        NoteInput.IsEnabled = enabled;
        TkDial.IsEnabled = enabled;
        ShtDial.IsEnabled = enabled;
        Joy.IsEnabled = enabled;
        MonitorCard.IsEnabled = enabled;
    }

    private void ShowShotHint(bool show)
    {
        ShotHint.IsVisible = show;
    }

    // ================================================================ DialFAB ×2（原有）

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

    private async void OnFcCardPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        => await HandleEditAsync(
            sender is Control { Tag: string tag }
                ? tag switch
                {
                    "Number" => EditRequestedSection.Number,
                    "Linker" => EditRequestedSection.Linker,
                    _ => EditRequestedSection.Prefix,
                }
                : EditRequestedSection.Prefix);

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