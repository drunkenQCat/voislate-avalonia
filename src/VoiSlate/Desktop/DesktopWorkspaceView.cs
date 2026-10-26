using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using VoiSlate.Models;
using VoiSlate.Services;

namespace VoiSlate.Desktop;

/// <summary>Native Windows workspace. Rendering is a projection of the service, never the data store.</summary>
public sealed partial class DesktopWorkspaceView : UserControl
{
    private readonly DesktopWorkspaceService _service;
    private Grid _root = null!, _shell = null!;
    private readonly Grid _overlayLayer = new();
    private Border _rail = null!, _toast = null!;
    private ContentControl _host = null!;
    private StackPanel _planOverview = null!, _planFilters = null!, _sceneNav = null!, _planBody = null!;
    private StackPanel _logSummary = null!, _dates = null!, _logFilters = null!, _timeline = null!;
    private ScrollViewer? _planScroll, _logScroll;
    private TextBox? _recNote, _shotNote;
    private Button? _exportButton;
    private string _page = "record", _planScene = "", _planQuery = "", _logQuery = "";
    private ShotProgress? _planFilter;
    private LogFilter _logFilter;
    private string? _logDate, _scopeScene, _scopeShot, _inspectShot;
    private bool _busy, _compact;
    private DispatcherTimer? _toastTimer;
    private readonly DispatcherTimer _hold = new() { Interval = TimeSpan.FromMilliseconds(650) };
    private bool _holding, _held;
    private Action? _finishVoice;
    private ScrollViewer? _recordScroll;
    // 窄屏整页翻页（用户不要底部多选 Tab）：三页竖向排列，下滑去上一页、上滑去下一页。
    private bool _pagerAttached, _pagerTracking;
    private double _pagerStartY, _pagerShift;
    private const double PagerCommit = 70;
    private readonly bool _voiceCaptureAvailable;

    public DesktopWorkspaceView(DesktopWorkspaceService service, Speech.BailianSettingsStore speechSettings,
        Speech.BailianRealtimeClient? speechClient = null, Func<int, Speech.IPcmCapture>? captureFactory = null)
    {
        _service = service; _speechSettings = speechSettings; _speechClient = speechClient ?? new();
        _captureFactory = captureFactory ?? (device => new Speech.WindowsPcmCapture(device)); _logDate = service.Today;
        // 采集器实现是 Windows 专用的；Android 还没有麦克风接线，界面据此降级（不给出一个按了只会报错的摇杆）。
        _voiceCaptureAvailable = captureFactory is not null || OperatingSystem.IsWindows();
        Classes.Add("workspace");
        Background = Brush("#F2F5F3");
        _service.Changed += OnChanged;
        DetachedFromVisualTree += (_, _) => { _service.Changed -= OnChanged; _toastTimer?.Stop(); _hold.Stop(); _finishVoice?.Invoke(); HardwareKeys.Pressed -= OnHardwareKey; };
        // 音量键：Android 由 MainActivity 注入实现（原版就是这样记条/撤回，单手戴手套也能用）；桌面为 null，走键盘快捷键。
        HardwareKeys.Pressed += OnHardwareKey;
        AttachPager();
        _hold.Tick += (_, _) => { _hold.Stop(); if (_holding) { _held = true; SaveTake(); } };
        SizeChanged += (_, _) => { var compact = Bounds.Width < 820; if (compact != _compact) { _compact = compact; Render(); } };
        AddHandler(InputElement.KeyDownEvent, (_, e) => HandleShortcut(e), Avalonia.Interactivity.RoutingStrategies.Tunnel);
        InitializeBackups();
        Render();
    }
    /// <summary>数据在后台装载完成后让视图重画（Android 启动路径用；桌面是同步初始化，不需要）。</summary>
    internal void Refresh() => Render();

    /// <summary>键盘快捷键（Ctrl+1/2/3、Ctrl+Shift+Z、Ctrl+Enter、Esc）。薄 Window 壳会在窗口层转发同一入口。</summary>
    internal void HandleShortcut(KeyEventArgs e)
    {
        if (e.Handled) return;
        if (e.KeyModifiers == KeyModifiers.Control && e.Key is Key.D1 or Key.D2 or Key.D3)
        { Navigate(e.Key == Key.D1 ? "record" : e.Key == Key.D2 ? "plan" : "log"); e.Handled = true; }
        else if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.Z) { AskUndo(); e.Handled = true; }
        else if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.Enter)
        {
            if (_service.State.PocketLock) Toast("口袋锁已开启，请长按记条按钮"); else SaveTake();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape) { Navigate("record"); e.Handled = true; }
    }

    /// <summary>音量上 = 记一条（含滚动下一条），音量下 = 撤回（原版映射；仅记录页响应）。</summary>
    private void OnHardwareKey(HardwareKey key)
    {
        if (_busy) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (_busy || _page != "record") return;
            if (key == HardwareKey.VolumeUp)
            {
                if (_service.State.PocketLock) Toast("口袋锁已开启，请长按记条按钮");
                else SaveTake(success: "已记一条 · 音量上键");
            }
            else AskUndo();
        });
    }

    private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));
    private static TextBlock T(string value, double size = 13, string color = "#536C60", bool bold = false) => new()
    {
        Text = value, FontSize = size, Foreground = Brush(color), FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal,
        TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center,
    };
    private static StackPanel Stack(double gap = 10, params Control[] children)
    {
        var panel = new StackPanel { Spacing = gap }; foreach (var c in children) panel.Children.Add(c); return panel;
    }
    private static StackPanel Row(double gap = 8, params Control[] children)
    {
        var panel = Stack(gap, children); panel.Orientation = Orientation.Horizontal; return panel;
    }
    private static Border Box(Control child, string color = "#FFFFFF", double padding = 16, string? border = null, double radius = 12) => new()
    {
        Child = child, Background = Brush(color), Padding = new Thickness(padding), CornerRadius = new CornerRadius(radius),
        BorderBrush = border is null ? null : Brush(border), BorderThickness = new Thickness(border is null ? 0 : 1),
    };
    private Button B(string text, Action action, string style = "", string? id = null)
    {
        var b = new Button { Content = text, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center };
        if (style.Length > 0) b.Classes.Add(style);
        AutomationProperties.SetName(b, text); if (id is not null) AutomationProperties.SetAutomationId(b, id);
        b.Click += (_, _) => action(); return b;
    }
    private void OnChanged() { if (Dispatcher.UIThread.CheckAccess()) Render(); else Dispatcher.UIThread.Post(Render); }
    private async void Run(Func<Task> work, string? success = null)
    {
        if (_busy) return; _finishVoice?.Invoke(); _busy = true;
        try { await work(); if (success is not null) Toast(success); }
        catch (Exception ex) { Toast(ex.Message, true); }
        finally { _busy = false; }
    }
    /// <summary>记一条拍板：成功后给触觉反馈（对齐原版 record_page.dart 的 addItem：正常 128/150、假条 240/900）。</summary>
    private void SaveTake(bool end = false, bool fake = false, string? success = null) =>
        Run(async () => { await _service.SaveTakeAsync(end, fake); Haptics.TakeSaved(fake); }, success);

    /// <summary>撤回一条：成功后给「长震动」（原版 drawBackItem 的 240/900）。</summary>
    private void UndoTake() =>
        Run(async () => { await _service.UndoAsync(); Haptics.Undone(); },
            "上一条编号、评价与标注已恢复，可修改后重新保存");

    private void Toast(string message, bool error = false)
    {
        _toastTimer?.Stop(); _toast.Child = T(message, 13, "#FFFFFF"); _toast.Background = Brush(error ? "#9A4848" : "#2C4249");
        _toast.IsVisible = true;
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _toastTimer.Tick += (_, _) => { _toast.IsVisible = false; _toastTimer.Stop(); }; _toastTimer.Start();
    }
    private void Navigate(string page)
    {
        _finishVoice?.Invoke(); _page = page;
        if (page == "plan" && _planScene.Length == 0) _planScene = _service.State.Scene;
        Render();
    }
    private int DoneCount => _service.Scenes.Sum(s => s.Count(sh => _service.Progress(s.Info.Name, sh.Name) == ShotProgress.Done));
    private int ShotCount => _service.Scenes.Sum(s => s.Count);
    private void Render()
    {
        _finishVoice?.Invoke(); _hold.Stop(); _holding = false;
        _root = new Grid(); _shell = new Grid { ColumnDefinitions = new ColumnDefinitions(_compact ? "0,*" : "184,*") };
        _rail = Box(BuildRail(), "#173F49", 18, radius: 0); _rail.IsVisible = !_compact;
        _host = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        Grid.SetColumn(_host, 1); _shell.Children.Add(_rail); _shell.Children.Add(_host);
        // 窄屏没有底部 Tab（用户明确不接受多选 tab）：三页改成一条竖向翻页——下滑回拍摄计划、
        // 上滑去场记时间轴，见 AttachPager/BuildPagerDots。设置入口是记录页页头那颗 ⚙。
        var frame = new Grid();
        frame.Children.Add(_shell);
        if (_compact) frame.Children.Add(BuildPagerDots());
        _root.Children.Add(frame);
        _toast = Box(T(""), "#2C4249", 14); _toast.MaxWidth = 560; _toast.IsVisible = false; _toast.Margin = new Thickness(20, 0, 20, 80);
        _toast.HorizontalAlignment = HorizontalAlignment.Center; _toast.VerticalAlignment = VerticalAlignment.Bottom; _root.Children.Add(_toast);
        if (_overlayLayer.Parent is Panel oldParent) oldParent.Children.Remove(_overlayLayer); // 换父级前先摘，否则 "already has a visual parent"
        _root.Children.Add(_overlayLayer); // 弹层宿主：跨重渲染存活（对象本身是字段）
        Content = _root;
        _recordScroll = _planScroll = _logScroll = null; // 换页后旧滚动器已经分离，别留着判边界
        _host.Content = _page switch { "plan" => BuildPlan(), "log" => BuildTimeline(), _ => BuildRecord() };
    }
    /// <summary>页面留白：窄屏减半，否则 390 宽的手机上左右 48px 会被吃掉 1/5。</summary>
    private Thickness PagePad(double horizontal, double vertical) =>
        new(_compact ? Math.Min(horizontal, 14) : horizontal, _compact ? Math.Min(vertical, 12) : vertical);

    /// <summary>三页竖向顺序：拍摄计划在上、记录在中、场记时间轴在下（下滑=上一页，上滑=下一页）。</summary>
    private static readonly string[] PageOrder = { "plan", "record", "log" };

    /// <summary>给测试与诊断看当前页（"plan"/"record"/"log"）。</summary>
    internal string CurrentPage => _page;

    private int PageIndex
    {
        get { var i = Array.IndexOf(PageOrder, _page); return i < 0 ? 1 : i; }
    }

    private ScrollViewer? ActiveScroll => _page == "plan" ? _planScroll : _page == "log" ? _logScroll : _recordScroll;

    /// <summary>内层还能滚就先滚内容，滚到顶/底再滑才翻页（iOS 分页手感）。</summary>
    private bool AtScrollEdge(double dy)
    {
        var scroll = ActiveScroll;
        if (scroll is null) return true; // 内容不足一屏：本来就在两端
        return dy > 0
            ? scroll.Offset.Y <= 0.5
            : scroll.Offset.Y >= Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height) - 0.5;
    }

    /// <summary>相对当前页上下滑一格的目标页；没有上一页/下一页时返回 null（交给回弹）。</summary>
    internal string? PageForSwipe(double dy)
    {
        if (dy > PagerCommit) return PageIndex > 0 ? PageOrder[PageIndex - 1] : null;
        if (dy < -PagerCommit) return PageIndex < PageOrder.Length - 1 ? PageOrder[PageIndex + 1] : null;
        return null;
    }

    /// <summary>翻页手势挂在视图自身（Tunnel + handledEventsToo）：页内 ScrollViewer 和摇杆都会先吃掉指针。</summary>
    private void AttachPager()
    {
        if (_pagerAttached) return; _pagerAttached = true;
        AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (!_compact || _overlayLayer.Children.Count > 0) return;   // 桌面有左栏；弹窗开着时不翻页
            if (InsideJoy(e.GetPosition(this))) return;                    // 摇杆自己拖
            _pagerStartY = e.GetPosition(this).Y; _pagerShift = 0; _pagerTracking = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.PointerMovedEvent, (_, e) =>
        {
            if (!_pagerTracking) return;
            var dy = e.GetPosition(this).Y - _pagerStartY;
            if (Math.Abs(dy) < 8) return;
            if (!AtScrollEdge(dy)) { if (Math.Abs(_pagerShift) > 0.5) { var back = _pagerShift; _pagerShift = 0; AnimateHost(back); } _pagerTracking = false; return; }
            _pagerShift = dy * 0.5; // 跟手打折，拖到一半就有「有种弹力」的观感
            if (_host is not null) _host.RenderTransform = new TranslateTransform(0, _pagerShift);
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.PointerReleasedEvent, (_, _) =>
        {
            if (!_pagerTracking) return;
            _pagerTracking = false;
            var shift = _pagerShift; _pagerShift = 0;
            if (Math.Abs(shift) < 0.5) return;
            var target = PageForSwipe(shift * 2); // 跟手是 0.5 倍，还原成手指行程再判阈值
            if (target is null) { AnimateHost(shift); return; }
            Navigate(target);
            AnimateHost(-shift); // 新页从滑走的方向接上来，衔接不跳
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private bool InsideJoy(Point point)
    {
        if (_joyArea is not { IsEffectivelyVisible: true } joy) return false;
        if (joy.TranslatePoint(new Point(0, 0), this) is not { } origin) return false;
        return point.X >= origin.X - 8 && point.X <= origin.X + joy.Bounds.Width + 8
            && point.Y >= origin.Y - 8 && point.Y <= origin.Y + joy.Bounds.Height + 8;
    }

    /// <summary>整页纵向滑入/回弹：7 帧三次缓出。不用 Transitions 是为了拖动期间零延迟。</summary>
    private void AnimateHost(double fromY)
    {
        if (_host is null) return;
        if (Math.Abs(fromY) < 1) { _host.RenderTransform = null; return; }
        var step = 0;
        var ticker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        ticker.Tick += (_, _) =>
        {
            step++;
            var eased = 1 - Math.Pow(1 - Math.Min(1, step / 7d), 3);
            _host.RenderTransform = new TranslateTransform(0, fromY * (1 - eased));
            if (step < 7) return;
            ticker.Stop(); _host.RenderTransform = null;
        };
        ticker.Start();
    }

    /// <summary>右侧三颗小圆点：只说清「上下还有页」，不吃点击（翻页靠滑）。</summary>
    private Control BuildPagerDots()
    {
        var dots = new StackPanel
        {
            Spacing = 7, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0), IsHitTestVisible = false
        };
        AutomationProperties.SetAutomationId(dots, "pager-dots");
        for (var i = 0; i < PageOrder.Length; i++)
        {
            var on = i == PageIndex;
            dots.Children.Add(new Border
            {
                Width = on ? 6 : 4, Height = on ? 6 : 4, CornerRadius = new CornerRadius(3),
                Background = Brush(on ? "#31574C" : "#B9C7C2"), HorizontalAlignment = HorizontalAlignment.Center
            });
        }
        return dots;
    }

    /// <summary>UserControl 上没有 StorageProvider；桌面拿 Window 的，无窗口宿主则明确报错（Android 无文件选择器）。</summary>
    private IStorageProvider StorageProvider =>
        TopLevel.GetTopLevel(this)?.StorageProvider ?? throw new InvalidOperationException("当前平台不支持文件选择");

    private Control BuildRail()
    {
        var panel = Stack(8, T("VoiSlate", 24, "#F4F8F5", true), T("现场场记工作台", 11, "#9BB6B6"));
        panel.Children.Add(new Border { Height = 30 });
        foreach (var entry in new[] { ("record", "◉  当前记录"), ("plan", "▤  拍摄计划"), ("log", "◷  场记时间轴") })
        {
            var key = entry.Item1; var b = B(entry.Item2, () => Navigate(key), _page == key ? "selected" : "quiet", "nav-" + key);
            b.HorizontalAlignment = HorizontalAlignment.Stretch; b.HorizontalContentAlignment = HorizontalAlignment.Left;
            b.Foreground = Brush("#DFEAE5"); b.Classes.Add("rail"); panel.Children.Add(b);
        }
        panel.Children.Add(new Border { Height = 20 }); panel.Children.Add(T(_service.State.Project, 13, "#C2D5CD", true));
        panel.Children.Add(T($"{DoneCount} / {ShotCount} 镜完成", 11, "#9CB9AC"));
        panel.Children.Add(T("计划、场记与草稿\n自动保存到本机", 11, "#92B0A3"));
        var preferences = B("⚙  工程与偏好", ShowSettings, "quiet", "settings"); preferences.Foreground = Brush("#DFEAE5"); preferences.Classes.Add("rail"); panel.Children.Add(preferences);
        return panel;
    }
    private Control PageHeader(string eyebrow, string title, params Control[] actions)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(Stack(4, T(eyebrow, 10, "#84988A"), T(title, 25, "#28483B", true)));
        var tools = Row(6, actions); tools.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(tools, 1); grid.Children.Add(tools);
        return Box(grid, "#FCFDFB", 20, radius: 0);
    }
    private Control Footer(string cross, string page)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
        grid.Children.Add(B(cross, () => Navigate(page), "quiet"));
        var back = B($"●  {_service.State.Scene} 场 / {_service.State.Shot} 镜     返回当前拍摄 →", () => Navigate("record"), "", "resume");
        back.HorizontalAlignment = HorizontalAlignment.Stretch; Grid.SetColumn(back, 1); grid.Children.Add(back);
        return Box(grid, "#FCFDFB", 12, radius: 0);
    }
    private Control BuildRecord()
    {
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };
        grid.Children.Add(PageHeader("VOISLATE / ON SET", "当前记录", B("⚙", ShowSettings, "quiet", "open-settings")));
        var project = B($"▧  {_service.State.Project}   ·   完成 {DoneCount} / {ShotCount} 镜   ↗", () => Navigate("plan"), "quiet");
        project.HorizontalAlignment = HorizontalAlignment.Stretch; Grid.SetRow(project, 1); grid.Children.Add(project);
        var d = _service.Draft; var panel = Stack(14); panel.MaxWidth = 470; panel.Margin = PagePad(20, 20);
        panel.Children.Add(B("⌃  拍摄计划", () => Navigate("plan"), "quiet", "open-plan"));
        var card = Stack(16);
        var title = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        title.Children.Add(T(d.Ended ? "本镜最后一条 · 已保存" : "当前待记条", 11, "#7F8069"));
        var more = B("•••", ShowMore, "quiet", "take-more"); Grid.SetColumn(more, 1); title.Children.Add(more); card.Children.Add(title);
        var slate = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 10 };
        var values = new[] { (_service.State.Scene, "场"), (_service.State.Shot, "镜"), (d.Ended ? "OK" : _service.State.Linked ? d.Take.ToString() : DesktopWorkspaceService.RetakeLetter(d.RetakeIndex), _service.State.Linked ? "次" : "补录") };
        for (int i = 0; i < 3; i++)
        {
            var index = i; var item = values[i];
            var value = T(item.Item1.Length == 0 ? "—" : item.Item1, item.Item1.Length > 3 ? 29 : 38, "#203A40", true); value.HorizontalAlignment = HorizontalAlignment.Center;
            var unit = T(item.Item2, 12, "#89958B"); unit.HorizontalAlignment = HorizontalAlignment.Center;
            var button = B("", () => { if (index < 2) Navigate("plan"); else ShowRenum(); }, "quiet");
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch; button.VerticalContentAlignment = VerticalAlignment.Stretch;
            button.Content = Box(Stack(4, value, unit), "#FFFFFF", 12, "#D8D5C0", 10); button.Padding = new Thickness(0); button.HorizontalAlignment = HorizontalAlignment.Stretch;
            AutomationProperties.SetName(button, $"{item.Item2} {item.Item1}"); Grid.SetColumn(button, i); slate.Children.Add(button);
        }
        card.Children.Add(slate);
        var file = d.Ended ? _service.FilePrefix + _service.State.Naming.Linker + (_service.State.FileNumber - 1).ToString("D3") : _service.CurrentFile;
        var fileLine = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        fileLine.Children.Add(T("◉  " + file, 19, "#28434C", true));
        var next = T("NEXT  " + (d.Ended ? _service.CurrentFile : _service.NextFile), 10, "#8D9B9C"); Grid.SetColumn(next, 1); fileLine.Children.Add(next);
        card.Children.Add(Box(fileLine, "#FFFFFF", 12));
        card.Children.Add(T($"{_service.Scene?.Info.Note.Type ?? "还没有拍摄计划"} · {_service.Shot?.Note.Type} · {_service.Shot?.Note.Append}", 12, "#768371"));
        var verdicts = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 10 };
        var picture = new ComboBox { ItemsSource = new[] { "画面 · 未评", "画面过 ✓", "画面保" }, SelectedIndex = (int)d.Picture, HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = !d.Ended };
        AutomationProperties.SetName(picture,"画面评价"); picture.SelectionChanged += (_, _) => { if (picture.SelectedIndex >= 0) Run(() => _service.SetPictureAsync((ShtStatus)picture.SelectedIndex)); };
        var sound = new ComboBox { ItemsSource = new[] { "声音 · 未评", "声音可 ✓", "声音弃" }, SelectedIndex = (int)d.Sound, HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = !d.Ended };
        AutomationProperties.SetName(sound,"声音评价"); sound.SelectionChanged += (_, _) => { if (sound.SelectedIndex >= 0) Run(() => _service.SetSoundAsync((TkStatus)sound.SelectedIndex)); };
        verdicts.Children.Add(picture); Grid.SetColumn(sound, 1); verdicts.Children.Add(sound); card.Children.Add(verdicts);
        if (d.Ended) card.Children.Add(B("✓ 本镜已保存并结束    下一镜 →", () => Run(async () => { try { await _service.NextShotAsync(); } catch (InvalidOperationException) { Navigate("plan"); Toast("已到计划末尾，可添加或选择镜头"); } }), "primary", "next-shot"));
        panel.Children.Add(Box(card, "#FFFDF5", 18, "#E4E3D5", 18));
        _recNote = new TextBox { Text = d.RecordingNote, PlaceholderText = "◉  录音标注…", IsReadOnly = d.Ended };
        _shotNote = new TextBox { Text = d.ShotNote, PlaceholderText = "▧  镜头标注…", IsReadOnly = d.Ended };
        AutomationProperties.SetAutomationId(_recNote, "recording-note"); AutomationProperties.SetName(_recNote,"录音标注");
        AutomationProperties.SetAutomationId(_shotNote, "shot-note"); AutomationProperties.SetName(_shotNote,"镜头标注");
        _recNote.TextChanged += NotesChanged; _shotNote.TextChanged += NotesChanged;
        panel.Children.Add(_recNote);
        panel.Children.Add(T("按住语音摇杆 · 左滑录音标注 / 右滑镜头标注 · 回中取消", 10, "#8A9691"));
        var undo = B("↶\n撤回", AskUndo, "quiet", "undo-take"); undo.IsEnabled = _service.CanUndo;
        var add = B(_service.State.PocketLock ? "＋\n长按记条" : "＋\n下一条", () =>
        {
            if (_service.State.PocketLock) { if (!_held) Toast("口袋锁已开，请按住 650 毫秒"); }
            else SaveTake(success: "当前条已保存，可撤回修改");
        }, "purple", "add-take");
        add.Width = 92; add.Height = 82; add.CornerRadius = new CornerRadius(40); add.IsEnabled = _service.Shot is not null && !d.Ended;
        Point holdFrom = default;
        add.AddHandler(InputElement.PointerPressedEvent, (_, e) => { if (e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(add).Properties.IsLeftButtonPressed) return; _held = false; holdFrom = e.GetPosition(add); if (_service.State.PocketLock) { _holding = true; _hold.Start(); } }, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        // 触屏：长按期间手指滑出 10px 视为滚动而非长按（手机上按住再拖是滚页面的常见手势）
        add.PointerMoved += (_, e) => { var d = e.GetPosition(add) - holdFrom; if (_holding && (Math.Abs(d.X) > 10 || Math.Abs(d.Y) > 10)) { _holding = false; _hold.Stop(); } };
        add.PointerReleased += (_, _) => { _holding = false; _hold.Stop(); }; add.PointerCaptureLost += (_, _) => { _holding = false; _hold.Stop(); };
        add.PointerExited += (_, _) => { _holding = false; _hold.Stop(); };
        add.AddHandler(InputElement.KeyDownEvent, (_, e) => { if (_service.State.PocketLock && e.KeyModifiers == KeyModifiers.None && e.Key is Key.Space or Key.Enter) { e.Handled = true; if (!_holding) { _held = false; _holding = true; _hold.Start(); } } }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        add.AddHandler(InputElement.KeyUpEvent, (_, e) => { if (_service.State.PocketLock && e.Key is Key.Space or Key.Enter) { e.Handled = true; _holding = false; _hold.Stop(); } }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        var hotzone = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12, Margin = new Thickness(0, 3) };
        hotzone.Children.Add(undo); var voice = BuildVoice(); Grid.SetColumn(voice, 1); hotzone.Children.Add(voice); Grid.SetColumn(add, 2); hotzone.Children.Add(add);
        if (_service.State.LeftHanded) { Grid.SetColumn(add, 0); Grid.SetColumn(undo, 2); }
        panel.Children.Add(hotzone); panel.Children.Add(_shotNote);
        panel.Children.Add(B("⌄  场记时间轴", () => { _scopeScene = _scopeShot = null; Navigate("log"); }, "quiet", "open-timeline"));
        if (!_service.State.Linked) panel.Children.Add(T("补录模式 · 次号冻结，文件号继续前进", 11, "#9875A3"));
        var scroll = new ScrollViewer { Content = panel, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        _recordScroll = scroll;
        Grid.SetRow(scroll, 2); grid.Children.Add(scroll);
        var caption = T("本机自动保存  ·  百炼实时语音标注", 10, "#96A397"); caption.Margin = new Thickness(20, 10); caption.HorizontalAlignment = HorizontalAlignment.Center;
        Grid.SetRow(caption, 3); grid.Children.Add(caption); return grid;
    }
    private async void NotesChanged(object? sender, TextChangedEventArgs e)
    {
        try { await _service.SaveDraftAsync(_recNote?.Text ?? "", _shotNote?.Text ?? ""); }
        catch (Exception ex) { Toast("草稿保存失败：" + ex.Message, true); }
    }
    private Control BuildPlan()
    {
        var page = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*,Auto"), Background = Brush("#F3F6F3") };
        page.Children.Add(PageHeader("01 / SHOT PLAN", "拍摄计划", B("导入 CSV", ImportPlan, "quiet", "import-plan"), B("返回拍摄 ×", () => Navigate("record"), "quiet", "nav-back")));
        _planOverview = Stack(8); _planOverview.Margin = new Thickness(24, 15, 24, 0); Grid.SetRow(_planOverview, 1); page.Children.Add(_planOverview);
        var search = new TextBox { Text = _planQuery, PlaceholderText = "搜索场地、场镜、人物或内容…", Margin = new Thickness(24, 12, 24, 0) };
        AutomationProperties.SetName(search,"搜索拍摄计划"); search.TextChanged += (_, _) => { _planQuery = search.Text ?? ""; RenderPlanContents(); };
        Grid.SetRow(search, 2); page.Children.Add(search);
        var controls = Stack(8); controls.Margin = new Thickness(24, 8, 24, 0);
        _planFilters = Row(5); controls.Children.Add(new ScrollViewer { Content = _planFilters, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var nav = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        _sceneNav = Row(8); nav.Children.Add(new ScrollViewer { Content = _sceneNav, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var add = B("＋ 场次", () => ShowPlanEditor(null, true), "quiet"); Grid.SetColumn(add, 1); nav.Children.Add(add); controls.Children.Add(nav);
        Grid.SetRow(controls, 3); page.Children.Add(controls);
        _planBody = Stack(14); _planBody.MaxWidth = 830; _planBody.Margin = PagePad(24, 20);
        _planScroll = new ScrollViewer { Content = _planBody, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        Grid.SetRow(_planScroll, 4); page.Children.Add(_planScroll);
        var footer = Footer("◷ 场记时间轴", "log"); Grid.SetRow(footer, 5); page.Children.Add(footer);
        RenderPlanContents(); return page;
    }
    private bool Matches(SceneSchedule scene, ScheduleItem shot)
    {
        var haystack = string.Join(' ', scene.Info.Name, scene.Info.Note.Type, scene.Info.Note.Append, string.Join(' ', scene.Info.Note.Objects), shot.Name, shot.Note.Type, shot.Note.Append, string.Join(' ', shot.Note.Objects));
        return haystack.Contains(_planQuery, StringComparison.OrdinalIgnoreCase) && (_planFilter is null || _service.Progress(scene.Info.Name, shot.Name) == _planFilter);
    }
    private static string ProgressLabel(ShotProgress progress) => progress switch { ShotProgress.Done => "已完成", ShotProgress.Shooting => "拍摄中", _ => "待拍" };
    private void RenderPlanContents()
    {
        _planOverview.Children.Clear(); _planFilters.Children.Clear(); _sceneNav.Children.Clear(); _planBody.Children.Clear();
        var progress = T($"{DoneCount} / {ShotCount} 镜完成     ·     {_service.Scenes.Count} 场", 21, "#385D48", true);
        _planOverview.Children.Add(progress);
        _planOverview.Children.Add(new ProgressBar { Minimum = 0, Maximum = Math.Max(1, ShotCount), Value = DoneCount, Height = 4, Foreground = Brush("#5B8066"), Background = Brush("#DEE8DD") });
        foreach (var f in new ShotProgress?[] { null, ShotProgress.Waiting, ShotProgress.Shooting, ShotProgress.Done })
        {
            var value = f; var count = _service.Scenes.Sum(s => s.Count(sh => value is null || _service.Progress(s.Info.Name, sh.Name) == value));
            _planFilters.Children.Add(B($"{(f is null ? "全部" : ProgressLabel(f.Value))}  {count}", () => { _planFilter = value; RenderPlanContents(); }, f == _planFilter ? "selected" : "quiet"));
        }
        var visible = _service.Scenes.Where(s => s.Any(sh => Matches(s, sh))).ToList();
        if (!visible.Any(s => s.Info.Name == _planScene)) _planScene = visible.FirstOrDefault()?.Info.Name ?? "";
        foreach (var scene in visible)
        {
            var sceneName = scene.Info.Name; var done = scene.Count(sh => _service.Progress(sceneName, sh.Name) == ShotProgress.Done);
            _sceneNav.Children.Add(B($"{sceneName} 场  ·  {done}/{scene.Count}", () => { _planScene = sceneName; _inspectShot = null; RenderPlanContents(); if (_planScroll is not null) _planScroll.Offset = default; }, _planScene == sceneName ? "selected" : ""));
        }
        if (_service.CanRestorePlan) _planBody.Children.Add(B("↶ 恢复最近删除的计划", () => Run(() => _service.RestorePlanItemAsync()), "quiet"));
        var selected = visible.FirstOrDefault(s => s.Info.Name == _planScene);
        if (selected is null)
        {
            _planBody.Children.Add(Empty(_service.Scenes.Count == 0 ? "暂无计划，从第一场开始" : "没有符合条件的镜头", "添加场次，或调整关键词与状态筛选。", B(_service.Scenes.Count == 0 ? "＋ 添加第一场" : "重置筛选", () =>
            {
                if (_service.Scenes.Count == 0) ShowPlanEditor(null, true); else { _planQuery = ""; _planFilter = null; Render(); }
            }, "primary"))); return;
        }
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        heading.Children.Add(Stack(4, T("SCENE " + selected.Info.Name, 11, "#7B8F79"), T(selected.Info.Note.Type.Length > 0 ? selected.Info.Note.Type : "未设置场地", 24, "#35543C", true)));
        var editScene = B("✎ 编辑场次", () => ShowPlanEditor(selected.Info, true), "quiet"); Grid.SetColumn(editScene,1); heading.Children.Add(editScene); _planBody.Children.Add(heading);
        var details = new Expander
        {
            Header = T(selected.Info.Note.Append.Length > 0 ? selected.Info.Note.Append : "展开场次概要与人物", 13, "#70856B"),
            Content = Stack(8, T(selected.Info.Note.Append, 13), T(string.Join("  /  ", selected.Info.Note.Objects), 12, "#82947C")),
            HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0),
        }; _planBody.Children.Add(details);
        _planBody.Children.Add(T($"镜头清单    {selected.Count(sh => Matches(selected, sh))} / {selected.Count} 镜", 12, "#7B8B73", true));
        foreach (var sh in selected.Where(sh => Matches(selected, sh)))
        {
            var shot = sh; var sceneName = selected.Info.Name; var status = _service.Progress(sceneName, shot.Name);
            var current = sceneName == _service.State.Scene && shot.Name == _service.State.Shot && !_service.Draft.Ended;
            var count = _service.Logs.Count(l => _service.MatchesShot(l, sceneName, shot.Name));
            var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 12 };
            top.Children.Add(T(shot.Name + " 镜", 25, "#34523F", true)); var type = T(shot.Note.Type, 12, "#85917D"); Grid.SetColumn(type,1); top.Children.Add(type);
            var badge = Box(T(current ? "● 当前拍摄" : ProgressLabel(status), 11, "#4B765C"), current ? "#E3EEE4" : "#F0F3EA", 7, radius:4); Grid.SetColumn(badge,2); top.Children.Add(badge);
            var edit = B("•••", () => ShowPlanEditor(shot, false), "quiet"); Grid.SetColumn(edit,3); top.Children.Add(edit);
            var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10 };
            var history = B(count == 0 ? "尚无场记" : $"已记 {count} 条 · 查看场记 ↗", () =>
            {
                _scopeScene = sceneName; _scopeShot = shot.Name; _logDate = null; _logFilter = LogFilter.All; _logQuery = ""; Navigate("log");
            }, "quiet"); history.IsEnabled = count > 0; history.HorizontalAlignment = HorizontalAlignment.Left; actions.Children.Add(history);
            var start = B(current ? "继续拍摄 →" : status == ShotProgress.Done ? "再拍一条 →" : "选择开拍 →", () =>
            {
                Run(async () => { await _service.SelectShotAsync(sceneName, shot.Name); _page = "record"; Render(); });
            }, current ? "primary" : ""); Grid.SetColumn(start,1); actions.Children.Add(start);
            var card = Box(Stack(10, top, T(shot.Note.Append.Length > 0 ? shot.Note.Append : "暂无镜头内容", 15, "#45583E"), T(string.Join(" / ", shot.Note.Objects), 11, "#84947B"), actions), "#FFFFFF", 16,
                _inspectShot == shot.Name ? "#BF9D54" : current ? "#74957C" : "#E0E7DA");
            AutomationProperties.SetAutomationId(card, $"shot-{sceneName}-{shot.Name}"); _planBody.Children.Add(card);
            if (_inspectShot == shot.Name) Dispatcher.UIThread.Post(() => { if (_page == "plan") card.BringIntoView(); }, DispatcherPriority.Loaded);
        }
        _planBody.Children.Add(B("＋ 添加镜头", () => ShowPlanEditor(null, false), "quiet"));
    }
    private Control BuildTimeline()
    {
        var page = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,*,Auto"), Background = Brush("#F4F5F8") };
        page.Children.Add(PageHeader("02 / TAKE TIMELINE", "场记时间轴", _exportButton = B(_logDate is null ? "导出 JSON" : "导出当日", ExportTimeline, "quiet", "export"), B("返回拍摄 ×", () => Navigate("record"), "quiet", "nav-back")));
        _logSummary = Stack(8); _logSummary.Margin = new Thickness(24, 15, 24, 0); Grid.SetRow(_logSummary,1); page.Children.Add(_logSummary);
        _dates = Row(7); var dateScroll = new ScrollViewer { Content = _dates, Margin = new Thickness(24, 12, 24, 0), HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(dateScroll,2); page.Children.Add(dateScroll);
        var search = new TextBox { Text = _logQuery, PlaceholderText = "搜索文件号、场镜、场地或标注…", Margin = new Thickness(24, 12, 24, 0) };
        AutomationProperties.SetName(search,"搜索场记"); search.TextChanged += (_, _) => { _logQuery = search.Text ?? ""; RenderTimelineContents(); if (_logScroll is not null) _logScroll.Offset = default; };
        Grid.SetRow(search,3); page.Children.Add(search);
        _logFilters = Row(5); var filterScroll = new ScrollViewer { Content = _logFilters, Margin = new Thickness(24, 8, 24, 4), HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(filterScroll,4); page.Children.Add(filterScroll);
        _timeline = Stack(16); _timeline.MaxWidth = 830; _timeline.Margin = new Thickness(_compact ? 14 : 24, _compact ? 12 : 14, _compact ? 14 : 24, _compact ? 16 : 24);
        _logScroll = new ScrollViewer { Content = _timeline, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        Grid.SetRow(_logScroll,5); page.Children.Add(_logScroll);
        var footer = Footer("▤ 拍摄计划", "plan"); Grid.SetRow(footer,6); page.Children.Add(footer);
        RenderTimelineContents(); return page;
    }
    private void RenderTimelineContents()
    {
        if (_exportButton is not null)
        {
            // 只重绘内容时表头不重建，导出按钮的标签必须跟着 _logDate 一起更新，否则「导出当日/导出 JSON」会骗人
            var label = _logDate is null ? "导出 JSON" : "导出当日";
            _exportButton.Content = label;
            Avalonia.Automation.AutomationProperties.SetName(_exportButton, label);
        }
        _logSummary.Children.Clear(); _dates.Children.Clear(); _logFilters.Children.Clear(); _timeline.Children.Clear();
        var all = _service.Logs.Where(l => (_logDate is null || l.Date == _logDate) && (_scopeScene is null || _service.MatchesShot(l, _scopeScene, _scopeShot ?? ""))).ToList();
        var stats = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*") };
        var metrics = new[] { (all.Count.ToString(), "已存场记"), (all.Select(l => l.Item.Scn + "/" + l.Item.Sht).Distinct().Count().ToString(), "记录镜头"), (all.Count(l => l.Item.OkSht != ShtStatus.NotChecked && l.Item.OkTk == TkStatus.Ok).ToString(), "画声可用") };
        for (int i = 0; i < metrics.Length; i++) { var item = metrics[i]; var cell = Stack(3, T(item.Item1, 27, "#4A5574", true), T(item.Item2, 11, "#8B94A8")); Grid.SetColumn(cell,i); stats.Children.Add(cell); } _logSummary.Children.Add(stats);
        _dates.Children.Add(B("全部日期", () => { _logDate = null; RenderTimelineContents(); }, _logDate is null ? "selected" : ""));
        foreach (var day in _service.Logs.Select(l => l.Date).Append(_service.Today).Distinct().OrderDescending(StringComparer.Ordinal))
        {
            var date = day; var count = _service.Logs.Count(l => l.Date == date && (_scopeScene is null || _service.MatchesShot(l, _scopeScene, _scopeShot ?? "")));
            _dates.Children.Add(B($"{(date == _service.Today ? "今天" : FormatDate(date))} {count}", () => { _logDate = date; RenderTimelineContents(); if (_logScroll is not null) _logScroll.Offset = default; }, _logDate == date ? "selected" : ""));
        }
        foreach (var filter in Enum.GetValues<LogFilter>())
        {
            var f = filter; var label = f switch { LogFilter.Usable => "画声可用", LogFilter.Unreviewed => "待评价", LogFilter.Muted => "声音弃", LogFilter.Retake => "补录", _ => "全部" };
            var count = _service.QueryLogs(_logDate, _logQuery, f, _scopeScene, _scopeShot).Sum(g => g.Rows.Count);
            _logFilters.Children.Add(B($"{label} {count}", () => { _logFilter = f; RenderTimelineContents(); }, f == _logFilter ? "selected" : "quiet"));
        }
        if (_scopeScene is not null) _timeline.Children.Add(B($"仅看 {_scopeScene} 场 / {_scopeShot} 镜     查看全部镜头 ×", () => { _scopeScene = _scopeShot = null; RenderTimelineContents(); }, ""));
        var groups = _service.QueryLogs(_logDate, _logQuery, _logFilter, _scopeScene, _scopeShot);
        var info = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        info.Children.Add(T($"{groups.Sum(g => g.Rows.Count)} 条 · 按拍摄顺序 · 点条目编辑", 11, "#939AAF"));
        var latest = B("定位最新 ↓", () => { if (_logScroll is not null) _logScroll.ScrollToEnd(); }, "quiet"); Grid.SetColumn(latest,1); info.Children.Add(latest); _timeline.Children.Add(info);
        string? previousDate = null;
        foreach (var group in groups)
        {
            if (group.Date != previousDate) { _timeline.Children.Add(T(FormatDate(group.Date) + (group.Date == _service.Today ? "  今天" : ""), 12, "#8391AC", true)); previousDate = group.Date; }
            var source = group.Rows[0].Item;
            var plan = _service.FindPlan(group.Rows[0]);
            var plannedScene = plan?.Scene; var plannedShot = plan?.Shot;
            var groupHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            groupHeader.Children.Add(Stack(4, T($"◉  {group.Scene} 场  /  {group.Shot} 镜", 17, "#546685", true), T($"{source.Location ?? plannedScene?.Info.Note.Type ?? "场地未记录"} · {source.ShotType ?? plannedShot?.Note.Type}", 11, "#929BAF")));
            var inspect = B("查看计划 ↗", () => { _planScene = plannedScene?.Info.Name ?? group.Scene; _inspectShot = plannedShot?.Name ?? group.Shot; _planQuery = ""; _planFilter = null; Navigate("plan"); }, "quiet"); inspect.IsEnabled = plannedShot is not null; Grid.SetColumn(inspect,1); groupHeader.Children.Add(inspect); _timeline.Children.Add(groupHeader);
            foreach (var entry in group.Rows)
            {
                var row = entry; var item = entry.Item;
                var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                top.Children.Add(T(row.TakeLabel, 11, item.IsRetake ? "#9C76A9" : "#8391AC", true)); var time = T(row.TimeLabel + "  ✎", 11, "#9DA5B7"); Grid.SetColumn(time,1); top.Children.Add(time);
                var labels = Row(7, Box(T(row.PictureLabel, 11, "#5F7493"), item.OkSht == ShtStatus.NotChecked ? "#EDF0F4" : "#E6EDF7", 6, radius:4), Box(T(row.SoundLabel, 11, item.OkTk == TkStatus.Bad ? "#AE5961" : "#648373"), item.OkTk == TkStatus.Bad ? "#FBEDED" : item.OkTk == TkStatus.Ok ? "#E5F1E7" : "#EDF0F4", 6, radius:4));
                var content = Stack(9, top, T(item.FileName, 20, "#465777", true), labels);
                if (item.Tracks?.Count > 0) content.Children.Add(T("轨道   " + string.Join(" / ", item.Tracks), 11, "#83919C"));
                if (item.TkNote.Length > 0) content.Children.Add(T("录音   " + item.TkNote, 13, "#7D899E"));
                if (item.ShtNote.Length > 0) content.Children.Add(T("镜头   " + item.ShtNote, 13, "#7D899E"));
                if (item.TkNote.Length == 0 && item.ShtNote.Length == 0) content.Children.Add(T("无标注", 11, "#A5ADBD"));
                var card = B("", () => ShowLogEditor(row), "quiet"); card.Padding = new Thickness(0); card.HorizontalAlignment = HorizontalAlignment.Stretch; card.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                card.Content = Box(content, item.IsRetake ? "#FDFAFF" : "#FFFFFF", 16, item.IsRetake ? "#E4D9EB" : "#E1E6EF");
                AutomationProperties.SetName(card, "编辑 " + item.FileName + " " + row.TakeLabel); card.Margin = new Thickness(16,0,0,0); _timeline.Children.Add(card);
                if (item.IsShotEnd) { var end = T("✓  本镜结束 · 最后一条已保存", 12, "#7B9682"); end.Margin = new Thickness(16,0); _timeline.Children.Add(end); }
            }
        }
        if (groups.Count == 0) _timeline.Children.Add(Empty(all.Count == 0 ? "这个范围还没有场记" : "没有匹配的场记", "保存当前条后会按日期和镜头显示在这里。", B("清除筛选 / 查看全部日期", () => { _logDate = null; _logQuery = ""; _logFilter = LogFilter.All; _scopeScene = _scopeShot = null; Render(); }, "")));
        if ((_logDate is null || _logDate == _service.Today) && _scopeScene is null && _logQuery.Length == 0 && _logFilter == LogFilter.All && _service.Shot is not null)
            _timeline.Children.Add(Box(Stack(4, T($"● 当前拍摄 · {_service.State.Scene} 场 / {_service.State.Shot} 镜", 12, "#708C70"), T(_service.Draft.Ended ? "本镜已结束，返回选择下一镜" : _service.CurrentFile + " · 待保存，不计入场记", 11, "#95A48E")), "#EEF4EC", 16, "#C6D5BF"));
    }
    private static string FormatDate(string date) => DateTime.TryParseExact(date, "yyMMdd", null, System.Globalization.DateTimeStyles.None, out var parsed) ? parsed.ToString("yyyy-MM-dd") : date;
    private Control Empty(string title, string text, Control action) => Box(Stack(14,T(title,19,"#6A8068",true),T(text,13,"#95A18E"),action),"#F9FBF7",24);
    /// <summary>
    /// 平台无关的弹窗工厂：TopLevel 是 Window（桌面）时仍弹独立窗口，保持既有观感与 OwnedWindows 语义；
    /// TopLevel 只是 Windowless（Android）时降级为组件内 overlay，因此同一套界面两处都跑得起来。
    /// </summary>
    private WorkspaceDialog Dialog(string title, Control body, double width = 500) =>
        TopLevel.GetTopLevel(this) is Window owner
            ? WorkspaceDialog.ForWindow(DialogWindow(title, body, width), owner)
            : WorkspaceDialog.ForOverlay(_overlayLayer, title, body, width);

    private Window DialogWindow(string title, Control body, double width)
    {
        var window = new Window
        {
            Title = title, Width = width, MinWidth = 380, MaxHeight = 740, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = true, Background = Brush("#F6F8F4"),
            RequestedThemeVariant = ThemeVariant.Light,
        };
        window.Classes.Add("workspace");
        window.Content = new ScrollViewer { Content = Stack(16, T(title,23,"#35523D",true), body), Margin = new Thickness(24) };
        return window;
    }
    private async Task<bool> Confirm(string title, string message)
    {
        WorkspaceDialog? dialog = null;
        var buttons = Row(10, B("取消", () => dialog!.Close(false), "quiet"), B("确认", () => dialog!.Close(true), "primary"));
        dialog = Dialog(title, Stack(18, T(message,14), buttons));
        return await dialog!.ShowAsync<bool>();
    }
    private async void AskUndo()
    {
        if (!_service.CanUndo) return;
        var d = _service.Draft;
        if (!d.Ended && (d.RecordingNote.Length > 0 || d.ShotNote.Length > 0 || d.Picture != ShtStatus.NotChecked || d.Sound != TkStatus.NotChecked))
            if (!await Confirm("撤回上一条", "当前草稿将替换为上一条的编号、评价和标注，是否继续？")) return;
        UndoTake();
    }
    private async void ShowMore()
    {
        _finishVoice?.Invoke(); WorkspaceDialog? dialog = null;
        var body = Stack(10);
        var end = B("✓ 保存并结束本镜", () => { dialog!.Close(); SaveTake(end: true, success: "最后一条已保存，本镜结束"); }, "primary"); end.IsEnabled = !_service.Draft.Ended && _service.Shot is not null;
        body.Children.Add(end); body.Children.Add(T("未评项设为画面保 / 声音可，保留已有评价。", 11, "#8A9988"));
        body.Children.Add(B(_service.State.Linked ? "进入补录模式" : "退出补录模式", () => { dialog!.Close(); Run(() => _service.ToggleRetakeAsync()); }));
        body.Children.Add(T("补录用 A、B、C 标识；次号冻结，文件号继续前进。", 11, "#8A9988"));
        body.Children.Add(B("手动改号 · 场 / 镜 / 次", () => { dialog!.Close(); ShowRenum(); }));
        body.Children.Add(B("记录假条 F", () => { dialog!.Close(); SaveTake(fake: true, success: "假条已保存，文件号前进，正常次号保留"); }));
        body.Children.Add(B("人物 / 录音轨道", () => { dialog!.Close(); ShowTracks(); }));
        body.Children.Add(B("修改当前镜头信息", () => { dialog!.Close(); _planScene = _service.State.Scene; ShowPlanEditor(_service.Shot, false); }));
        body.Children.Add(B("指定文件序号（补录模式）", () => { dialog!.Close(); ShowFileNumber(); }));
        body.Children.Add(B("关闭", () => dialog!.Close(), "quiet"));
        dialog = Dialog("当前条操作", body, 440); await dialog!.ShowAsync();
    }
    private async void ShowFileNumber()
    {
        if (_service.State.Linked) { Toast("请先进入补录模式，再手动指定文件号"); return; }
        WorkspaceDialog? dialog = null;
        var number = new NumericUpDown { Minimum = _service.State.FileNumber, Maximum = int.MaxValue - 1, Value = _service.State.FileNumber, FormatString = "0" };
        dialog = Dialog("指定文件号", Stack(12, T("编号只向前调整，避免重用已有或已删除文件的编号。",12), number,
            B("保存", () => { var value = (int)(number.Value ?? _service.State.FileNumber); dialog!.Close(); Run(() => _service.SetNumberAsync(value)); }, "primary")));
        await dialog!.ShowAsync();
    }
    private async void ShowRenum()
    {
        if (_service.Scenes.Count == 0) { Navigate("plan"); return; }
        WorkspaceDialog? dialog = null;
        var scenes = new ComboBox { ItemsSource = _service.Scenes.Select(s => s.Info.Name).ToList(), SelectedItem = _service.State.Scene, HorizontalAlignment = HorizontalAlignment.Stretch };
        var shots = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        void ReloadShots()
        {
            var scene = _service.Scenes.FirstOrDefault(s => s.Info.Name == scenes.SelectedItem as string);
            shots.ItemsSource = scene?.Items.Select(sh => sh.Name).ToList(); shots.SelectedIndex = 0;
        }
        ReloadShots(); shots.SelectedItem = _service.State.Shot; scenes.SelectionChanged += (_, _) => ReloadShots();
        var retake = !_service.State.Linked;
        var take = new NumericUpDown { Minimum = 1, Maximum = 9998, Value = retake ? _service.Draft.RetakeIndex + 1 : _service.Draft.Take, FormatString = "0" };
        var actions = Row(10, B("取消", () => dialog!.Close(), "quiet"), B("就位", () =>
        {
            var scene = scenes.SelectedItem as string; var shot = shots.SelectedItem as string; if (scene is null || shot is null) return;
            var tk = (int)(take.Value ?? 1); dialog!.Close();
            Run(async () => { await _service.SelectShotAsync(scene,shot);
                if (retake) { if (_service.State.Linked) await _service.ToggleRetakeAsync(); await _service.SetRetakeIndexAsync(tk - 1); }
                else await _service.SetTakeAsync(tk);
                _page = "record"; Render(); });
        }, "primary"));
        dialog = Dialog("手动改号", Stack(10, T("场次"), scenes, T("镜头"), shots, T(retake ? "补录序号（1=A、2=B；仅改号）" : "次号（仅改号，不撤回场记）"), take, actions));
        await dialog!.ShowAsync();
    }
    private async void ShowPlanEditor(ScheduleItem? item, bool isScene, int? insertIndex = null)
    {
        WorkspaceDialog? dialog = null;
        var sceneName = _planScene.Length > 0 ? _planScene : _service.State.Scene;
        var number = new TextBox { Text = item?.Name ?? (insertIndex is null ? _service.NextPlanKey(sceneName, isScene) : ""), PlaceholderText = isScene ? "例如 12" : "例如 12A" };
        var type = new TextBox { Text = item?.Note.Type ?? "", PlaceholderText = isScene ? "场地名称" : "近景 / 特写 / 中景…" };
        var content = new TextBox { Text = item?.Note.Append ?? "", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 85, PlaceholderText = "概要与拍摄内容" };
        var objects = new TextBox { Text = string.Join("，",item?.Note.Objects ?? []), PlaceholderText = "人物或轨道，用逗号分隔" };
        var error = T("",12,"#AD4C4C");
        var save = B("保存", async () =>
        {
            try
            {
                await _service.SavePlanItemAsync(sceneName, item?.Name, number.Text ?? "", type.Text ?? "", content.Text ?? "", objects.Text ?? "", isScene, insertIndex);
                _planScene = isScene ? number.Text!.Trim().ToUpperInvariant() : sceneName; _planQuery = ""; _planFilter = null;
                dialog!.Close(); Render(); Toast("计划已保存");
            }
            catch (Exception ex) { error.Text = ex.Message; }
        }, "primary");
        var body = Stack(10,T(isScene ? "场号" : "镜号"),number,T(isScene ? "场地" : "景别"),type,T("概要 / 内容"),content,T("人物 / 录音轨道"),objects,error,Row(10,B("取消",()=>dialog!.Close(),"quiet"),save));
        if (item is not null)
        {
            string? shotName = isScene ? null : item.Name;
            var targetScene = _service.Scenes.FirstOrDefault(s => s.Info.Name == sceneName);
            var index = isScene ? _service.Scenes.FindIndex(s => s.Info.Name == item.Name) : targetScene?.Items.ToList().FindIndex(s => s.Name == item.Name) ?? 0;
            var ownerScene = isScene ? item.Name : sceneName;
            body.Children.Add(Row(6, B("上移", () => { dialog!.Close(); Run(() => _service.MovePlanItemAsync(ownerScene, shotName, -1)); }),
                B("下移", () => { dialog!.Close(); Run(() => _service.MovePlanItemAsync(ownerScene, shotName, 1)); }),
                B("前面插入", () => { dialog!.Close(); ShowPlanEditor(null, isScene, index); }),
                B("后面插入", () => { dialog!.Close(); ShowPlanEditor(null, isScene, index + 1); })));
            body.Children.Add(B("删除" + (isScene ? "此场次" : "此镜头"), async () =>
            {
                if (!await Confirm("删除计划条目", "删除此计划条目？已保存场记会保留，可在计划页恢复最近一次删除。")) return;
                dialog!.Close(); Run(() => _service.DeletePlanItemAsync(ownerScene, shotName), "计划条目已删除，可在计划页恢复");
            }, "danger"));
        }
        dialog = Dialog((item is null ? "添加" : "编辑") + (isScene ? "场次" : "镜头"), body);
        await dialog!.ShowAsync();
    }
    private async void ShowLogEditor(WorkspaceLog row)
    {
        WorkspaceDialog? dialog = null;
        var rec = new TextBox { Text = row.Item.TkNote, AcceptsReturn = true, MinHeight = 85, TextWrapping = TextWrapping.Wrap };
        var shot = new TextBox { Text = row.Item.ShtNote, AcceptsReturn = true, MinHeight = 85, TextWrapping = TextWrapping.Wrap };
        var picture = new ComboBox { ItemsSource = new[] { "画面未评", "画面过", "画面保" }, SelectedIndex = (int)row.Item.OkSht, HorizontalAlignment = HorizontalAlignment.Stretch };
        var sound = new ComboBox { ItemsSource = new[] { "声音未评", "声音可", "声音弃" }, SelectedIndex = (int)row.Item.OkTk, HorizontalAlignment = HorizontalAlignment.Stretch };
        var take = new NumericUpDown { Minimum = 0, Maximum = 9999, Value = row.Item.Tk, FormatString = "0" };
        var fileNumber = new NumericUpDown { Minimum = 1, Maximum = int.MaxValue - 1, Value = row.Item.FilenameNum, FormatString = "0" };
        var sceneNote = new TextBox { Text = row.Item.ScnNote, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        var tracks = new TextBox { Text = string.Join("，", row.Item.Tracks ?? []), PlaceholderText = "人物 / 轨道，用逗号分隔" };
        // 原版把「未占用的文件序号」做成候选列表，避免手输撞号；这里给出实际占用清单，保存时仍会拒绝重复。
        var used = _service.UsedFileNumbers(row.Item.FilenamePrefix, row.Item.FilenameLinker);
        var usedHint = T(used.Count == 0 ? "这套命名还没有已占用的文件序号" : "已占用：" + string.Join(", ", used), 11, "#8A9691");
        var error = T("",12,"#AD4C4C");
        var save = B("保存", async () =>
        {
            try { await _service.EditLogDetailsAsync(row,(int)(take.Value ?? 1),(int)(fileNumber.Value ?? 1),sceneNote.Text??"",tracks.Text??"",rec.Text??"",shot.Text??"",(ShtStatus)picture.SelectedIndex,(TkStatus)sound.SelectedIndex); dialog!.Close(); Toast("场记已更新"); }
            catch (Exception ex) { error.Text = ex.Message; }
        }, "primary");
        var delete = B("删除此条", async () =>
        {
            if (!await Confirm("删除场记", $"删除 {row.Item.FileName}？文件号保持不变。")) return;
            try { await _service.DeleteLogAsync(row); dialog!.Close(); Toast("场记已删除，文件号保留"); }
            catch (Exception ex) { error.Text = ex.Message; }
        }, "danger");
        dialog = Dialog("编辑场记",Stack(10,T($"{FormatDate(row.Date)} · {row.Item.Scn} 场 / {row.Item.Sht} 镜 · {row.TakeLabel}",12),T(row.Item.FileName,20,"#4E607E",true),T("文件序号"),fileNumber,usedHint,T("次号（假条 999 / 补录 W 为 0）"),take,picture,sound,T("本场信息"),sceneNote,T("人物 / 录音轨道"),tracks,T("录音标注"),rec,T("镜头标注"),shot,error,Row(8,delete,B("取消",()=>dialog!.Close(),"quiet"),save)));
        await dialog!.ShowAsync();
    }
    private async void ShowSettings()
    {
        _finishVoice?.Invoke();
        WorkspaceDialog? dialog = null;
        var project = new TextBox { Text = _service.State.Project };
        var pocket = new CheckBox { Content = "口袋锁 · 下一条需长按 650 毫秒", IsChecked = _service.State.PocketLock };
        var left = new CheckBox { Content = "左手操作 · 调换记条与撤回位置", IsChecked = _service.State.LeftHanded };
        var error = T("",12,"#AD4C4C");
        var body = Stack(12,T("工程名称"),project,BuildSpeechSettings(),T("偏好",16,"#45654D",true),pocket,left,BuildNamingSettings(), T("数据",16,"#45654D",true),
            B("导出全部计划与场记（JSON）",Export), BuildBackupSettings(),T("危险区",14,"#AE6662",true));
        body.Children.Add(B("清空今日场记",async()=>{if(await Confirm("清空今日场记","清空今天的全部记录？此操作不可撤销。文件号继续保留。")) {dialog!.Close();Run(()=>_service.ClearLogsAsync(false),"今日场记已清空");}},"danger"));
        body.Children.Add(B("清空所有场记",async()=>{if(await Confirm("清空所有场记","清空全部拍摄日记录？此操作不可撤销。计划和文件号保留。")) {dialog!.Close();Run(()=>_service.ClearLogsAsync(true),"全部场记已清空");}},"danger"));
        body.Children.Add(B("清空拍摄计划",async()=>{if(await Confirm("清空计划","清空所有场次与镜头？已保存场记保留，此操作不可撤销。")) {dialog!.Close();Run(()=>_service.ClearPlanAsync(),"拍摄计划已清空");}},"danger"));
        body.Children.Add(error);
        body.Children.Add(Row(10,B("取消",()=>dialog!.Close(),"quiet"),B("保存偏好",async()=>
        {
            try {await _service.SetPreferencesAsync(project.Text??"",pocket.IsChecked==true,left.IsChecked==true);dialog!.Close();Toast("工程与偏好已保存");}
            catch(Exception ex){error.Text=ex.Message;}
        },"primary")));
        dialog=Dialog("工程与偏好",body,520);await dialog!.ShowAsync();
        Render(); // 弹窗期间指针被抢走，底部 Tab / 左栏按钮会卡在 :pressed 状态；重渲染收尾
    }
    private async void ImportPlan()
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "导入拍摄计划", AllowMultiple = false, FileTypeFilter = [new FilePickerFileType("CSV 计划") { Patterns = ["*.csv"] }] });
            if (files.Count == 0) return;
            await using var stream = await files[0].OpenReadAsync(); using var reader = new StreamReader(stream);
            var imported = DesktopWorkspaceService.ParsePlan(await reader.ReadToEndAsync());
            if (!await Confirm("合并拍摄计划", $"解析到 {imported.Count} 场、{imported.Sum(s=>s.Count)} 镜。同编号更新内容，其余计划保留。是否合并？")) return;
            Run(async()=>{await _service.MergePlanAsync(imported);_planScene=imported[0].Info.Name;_planQuery="";_planFilter=null;Render();},"计划已导入");
        }
        catch(Exception ex){Toast("导入失败："+ex.Message,true);}
    }
    private async void Export()
    {
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "导出计划与全部场记", SuggestedFileName = "VoiSlate-" + _service.Today + ".json", DefaultExtension = "json", FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }] });
            if(file is null)return;
            await using var stream=await file.OpenWriteAsync(); stream.SetLength(0);
            await using var writer=new StreamWriter(stream);await writer.WriteAsync(_service.ExportJson());
            Toast("计划与场记已导出");
        }
        catch(Exception ex){Toast("导出失败："+ex.Message,true);}
    }
    /// <summary>场记时间轴的导出：切到单日时只导出当天（原版按日导出），"全部日期"仍导出完整备份。</summary>
    private async void ExportTimeline()
    {
        if (_logDate is null) { Export(); return; }
        try
        {
            var date = _logDate;
            var count = _service.DayCount(date);
            if (count == 0) { Toast($"{FormatDate(date)} 还没有场记"); return; }
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "导出当日场记", SuggestedFileName = "VoiSlate-" + date + ".json", DefaultExtension = "json",
                FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }],
            });
            if (file is null) return;
            await using var stream = await file.OpenWriteAsync(); stream.SetLength(0);
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(_service.ExportDayJson(date));
            Toast($"{FormatDate(date)} 的 {count} 条场记已导出");
        }
        catch (Exception ex) { Toast("导出失败：" + ex.Message, true); }
    }
}
