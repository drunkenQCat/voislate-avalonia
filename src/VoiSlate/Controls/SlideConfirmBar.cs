// SlideConfirmBar — 水平滑动确认条（契约 §5 SlideConfirmBar 行）。
//
// 依赖属性：
//   State          SlideConfirmState  OneWay   Idle/Pressed/Armed（按契约）
//   IsRecording    bool               OneWay
//   RecordDuration string             OneWay
//   Transcription  string             OneWay
//   TextLeft       string             TwoWay   ↔ DescText
//   TextRight      string             TwoWay   ↔ ShotNoteText
// 事件：SlideRight / SlideLeft
//
// 行为协议落实：
//   * 文本实时 TwoWay（键入即回源，二分绑定自然成立）；
//   * 水平拖动：右滑越过 slideLength 阈值触发 SlideRight，左滑越过 0 触发 SlideLeft；
//   * 触发时对同属性做幂等补提交（SlideConfirmLogic.TryCommit*：同一文本只提交一次，
//     不覆盖未保存输入）；
//   * 松手 200ms 回弹居中；
//   * 背景按位置红→绿插值（原版 backgroundColor red.shade200 → green.shade200）。

using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace VoiSlate.Controls;

/// <summary>水平滑动确认条。</summary>
public class SlideConfirmBar : TemplatedControl
{
    public static readonly StyledProperty<SlideConfirmState> StateProperty =
        AvaloniaProperty.Register<SlideConfirmBar, SlideConfirmState>(
            nameof(State), defaultValue: SlideConfirmState.Idle, defaultBindingMode: Avalonia.Data.BindingMode.OneWay);

    public static readonly StyledProperty<bool> IsRecordingProperty =
        AvaloniaProperty.Register<SlideConfirmBar, bool>(
            nameof(IsRecording), defaultValue: false, defaultBindingMode: Avalonia.Data.BindingMode.OneWay);

    public static readonly StyledProperty<string?> RecordDurationProperty =
        AvaloniaProperty.Register<SlideConfirmBar, string?>(
            nameof(RecordDuration), defaultValue: null, defaultBindingMode: Avalonia.Data.BindingMode.OneWay);

    public static readonly StyledProperty<string?> TranscriptionProperty =
        AvaloniaProperty.Register<SlideConfirmBar, string?>(
            nameof(Transcription), defaultValue: null, defaultBindingMode: Avalonia.Data.BindingMode.OneWay);

    public static readonly StyledProperty<string?> TextLeftProperty =
        AvaloniaProperty.Register<SlideConfirmBar, string?>(
            nameof(TextLeft), defaultValue: string.Empty, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string?> TextRightProperty =
        AvaloniaProperty.Register<SlideConfirmBar, string?>(
            nameof(TextRight), defaultValue: string.Empty, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>松手回弹动画时长（契约 §5：200ms）。</summary>
    public const double BounceBackMilliseconds = 200;

    /// <summary>滑块直径（对齐原版 height - 10 语义的近似）。</summary>
    public const double BallSize = 44;

    private static readonly IEasing BounceEasing = new QuadraticEaseOut();

    private readonly SlideConfirmLogic _logic = new();

    private Control? _background;
    private Control? _ball;
    private Control? _arrows;
    private Control? _recordingPanel;

    private bool _isDragging;
    private double _position;             // 滑块左缘像素位置 [0, slideLength]
    private double _dragStartPointerX;
    private double _dragStartPosition;
    private DispatcherTimer? _bounceTimer;
    private double _bounceFrom;
    private double _bounceTo;
    private long _bounceStartTimestamp;

    /// <summary>左右判定阈值滑程（像素）：slideLength = Width - BallSize（原版 width - height）。</summary>
    public double SlideLengthPixels
    {
        get
        {
            var width = Math.Max(Bounds.Width, 0);
            return _logic.SlideLength(width, BallSize);
        }
    }

    public SlideConfirmState State
    {
        get => GetValue(StateProperty);
        private set => SetValue(StateProperty, value);
    }

    public bool IsRecording
    {
        get => GetValue(IsRecordingProperty);
        set => SetValue(IsRecordingProperty, value);
    }

    public string? RecordDuration
    {
        get => GetValue(RecordDurationProperty);
        set => SetValue(RecordDurationProperty, value);
    }

    public string? Transcription
    {
        get => GetValue(TranscriptionProperty);
        set => SetValue(TranscriptionProperty, value);
    }

    public string? TextLeft
    {
        get => GetValue(TextLeftProperty);
        set => SetValue(TextLeftProperty, value);
    }

    public string? TextRight
    {
        get => GetValue(TextRightProperty);
        set => SetValue(TextRightProperty, value);
    }

    /// <summary>右滑到达终点触发（契约 §5）。</summary>
    public event Action? SlideRight;

    /// <summary>左滑到达起点触发（契约 §5）。</summary>
    public event Action? SlideLeft;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _background = e.NameScope.Find<Control>("PART_Background");
        _ball = e.NameScope.Find<Control>("PART_Ball");
        _arrows = e.NameScope.Find<Control>("PART_Arrows");
        _recordingPanel = e.NameScope.Find<Control>("PART_Recording");

        // 初始居中（原版 initValue = slideLength / 2）。
        _position = _logic.InitialPosition(Math.Max(Bounds.Width, 0), BallSize);
        UpdateRecordingPanelVisibility();
        UpdateVisuals();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsRecordingProperty)
        {
            UpdateRecordingPanelVisibility();
        }
        else if (change.Property == TextLeftProperty || change.Property == TextRightProperty)
        {
            // 实时 TwoWay：无需额外处理（绑定已回源）；此处仅保持内部一致性。
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (!_isDragging)
        {
            _position = _logic.InitialPosition(e.NewSize.Width, BallSize);
        }

        UpdateVisuals();
    }

    // ---------------------------------------------------------------- 输入

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        StopBounce();
        _isDragging = true;
        _dragStartPointerX = point.Position.X;
        _dragStartPosition = _position;
        State = SlideConfirmState.Pressed;
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
        var deltaX = point.Position.X - _dragStartPointerX;
        _position = _logic.ClampPosition(_dragStartPosition + deltaX, Bounds.Width, BallSize);
        State = _logic.IsPastRightThreshold(_position, Bounds.Width, BallSize) ||
                _logic.IsPastLeftThreshold(_position)
            ? SlideConfirmState.Armed
            : SlideConfirmState.Pressed;
        UpdateVisuals();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        TriggerIfArmed();
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        TriggerIfArmed();
    }

    /// <summary>松手：越过阈值则触发确认（含幂等补提交），随后 200ms 回弹居中。</summary>
    private void TriggerIfArmed()
    {
        var wasDragging = _isDragging;
        _isDragging = false;

        if (wasDragging)
        {
            if (_logic.IsPastRightThreshold(_position, Bounds.Width, BallSize))
            {
                _logic.TryCommitRight(TextRight);
                SlideRight?.Invoke();
            }
            else if (_logic.IsPastLeftThreshold(_position))
            {
                _logic.TryCommitLeft(TextLeft);
                SlideLeft?.Invoke();
            }
        }

        BounceBack();
    }

    private void BounceBack()
    {
        State = SlideConfirmState.Idle;
        var target = _logic.ReleaseTarget(Bounds.Width, BallSize);
        if (Math.Abs(_position - target) < 0.5)
        {
            _position = target;
            UpdateVisuals();
            return;
        }

        if (!IsLoaded || Application.Current is null)
        {
            _position = target;
            UpdateVisuals();
            return;
        }

        StopBounce();
        _bounceFrom = _position;
        _bounceTo = target;
        _bounceStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();

        _bounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _bounceTimer.Tick += (_, _) =>
        {
            var elapsedMs =
                (System.Diagnostics.Stopwatch.GetTimestamp() - _bounceStartTimestamp) * 1000.0 /
                System.Diagnostics.Stopwatch.Frequency;
            var t = Math.Clamp(elapsedMs / BounceBackMilliseconds, 0, 1);
            _position = _bounceFrom + (_bounceTo - _bounceFrom) * BounceEasing.Ease(t);
            UpdateVisuals();

            if (t >= 1)
            {
                _position = _bounceTo;
                StopBounce();
                UpdateVisuals();
            }
        };
        _bounceTimer.Start();
    }

    private void StopBounce()
    {
        _bounceTimer?.Stop();
        _bounceTimer = null;
    }

    // ---------------------------------------------------------------- 视觉

    private void UpdateVisuals()
    {
        if (_ball is null)
        {
            return;
        }

        _ball.Margin = new Thickness(_position, 0, 0, 0);
        if (_background is Border backgroundBorder)
        {
            var progress = _logic.Progress(_position, Bounds.Width, BallSize);
            backgroundBorder.Background = new SolidColorBrush(ColorLerp(StartRed, EndGreen, progress));
        }
    }

    private void UpdateRecordingPanelVisibility()
    {
        if (_arrows is null || _recordingPanel is null)
        {
            return;
        }

        _arrows.IsVisible = !IsRecording;
        _recordingPanel.IsVisible = IsRecording;
    }

    private static readonly Color StartRed = Color.Parse("#EF9A9A");   // 原版 Colors.red.shade200
    private static readonly Color EndGreen = Color.Parse("#A5D6A7");   // 原版 Colors.green.shade200

    private static Color ColorLerp(Color from, Color to, double t)
    {
        static byte L(byte a, byte b, double k) => (byte)Math.Round(a + (b - a) * k);

        return Color.FromArgb(
            L(from.A, to.A, t),
            L(from.R, to.R, t),
            L(from.G, to.G, t),
            L(from.B, to.B, t));
    }
}