using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace VoiSlate.Controls;

/// <summary>契约 §5：滑动条状态（Idle 未按下 / Pressed 拖动中 / Armed 已越过阈值）。</summary>
public enum SlideConfirmBarState
{
    Idle,
    Pressed,
    Armed,
}

/// <summary>
/// Agent C 占位 SlideConfirmBar（契约 v0.5 §5 签名一致）。
/// 文本为实时 TwoWay（键入即回源）；滑动判定/回弹/背景红绿插值由 Agent D 的正式实现提供，
/// 占位以两个按钮模拟滑动触发。
/// D 合入后删除本文件（连同 SlideConfirmBar.axaml）。
/// </summary>
public partial class SlideConfirmBar : UserControl
{
    public SlideConfirmBar()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<SlideConfirmBarState> StateProperty =
        AvaloniaProperty.Register<SlideConfirmBar, SlideConfirmBarState>(nameof(State));

    /// <summary>契约 §5：Idle/Pressed/Armed（OneWay）。</summary>
    public SlideConfirmBarState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public static readonly StyledProperty<bool> IsRecordingProperty =
        AvaloniaProperty.Register<SlideConfirmBar, bool>(nameof(IsRecording));

    /// <summary>契约 §5：是否正在录音（OneWay）。</summary>
    public bool IsRecording
    {
        get => GetValue(IsRecordingProperty);
        set => SetValue(IsRecordingProperty, value);
    }

    public static readonly StyledProperty<string> RecordDurationProperty =
        AvaloniaProperty.Register<SlideConfirmBar, string>(nameof(RecordDuration), defaultValue: "00:00");

    /// <summary>契约 §5：录音时长文本（OneWay）。</summary>
    public string RecordDuration
    {
        get => GetValue(RecordDurationProperty);
        set => SetValue(RecordDurationProperty, value);
    }

    public static readonly StyledProperty<string> TranscriptionProperty =
        AvaloniaProperty.Register<SlideConfirmBar, string>(nameof(Transcription), defaultValue: string.Empty);

    /// <summary>契约 §5：ASR 转写/状态文本（OneWay）。</summary>
    public string Transcription
    {
        get => GetValue(TranscriptionProperty);
        set => SetValue(TranscriptionProperty, value);
    }

    public static readonly StyledProperty<string> TextLeftProperty =
        AvaloniaProperty.Register<SlideConfirmBar, string>(nameof(TextLeft), defaultValue: string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>契约 §5：录音标注文本（TwoWay ↔ DescText）。</summary>
    public string TextLeft
    {
        get => GetValue(TextLeftProperty);
        set => SetValue(TextLeftProperty, value);
    }

    public static readonly StyledProperty<string> TextRightProperty =
        AvaloniaProperty.Register<SlideConfirmBar, string>(nameof(TextRight), defaultValue: string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>契约 §5：镜头标注文本（TwoWay ↔ ShotNoteText）。</summary>
    public string TextRight
    {
        get => GetValue(TextRightProperty);
        set => SetValue(TextRightProperty, value);
    }

    /// <summary>契约 §5：右滑过阈值 → 写镜头标注（对同属性幂等补提交）。</summary>
    public event Action? SlideRight;

    /// <summary>契约 §5：左滑过 0 → 写录音标注（对同属性幂等补提交）。</summary>
    public event Action? SlideLeft;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsRecordingProperty || change.Property == RecordDurationProperty)
        {
            UpdateRecIndicator();
        }
        else if (change.Property == TranscriptionProperty)
        {
            if (TranscriptionText != null)
            {
                TranscriptionText.Text = Transcription;
            }
        }
    }

    private void UpdateRecIndicator()
    {
        if (RecIndicator == null) return;
        RecIndicator.IsVisible = IsRecording;
        RecIndicator.Text = IsRecording ? $"● REC {RecordDuration}" : $"○ {RecordDuration}";
    }

    private void OnSlideLeftClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // 占位：以按钮模拟左滑（正式实现为拖动判定，超过 0 阈值触发）。
        State = SlideConfirmBarState.Armed;
        SlideLeft?.Invoke();
        State = SlideConfirmBarState.Idle;
    }

    private void OnSlideRightClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // 占位：以按钮模拟右滑（正式实现为拖动判定，超过 slideLength 阈值触发）。
        State = SlideConfirmBarState.Armed;
        SlideRight?.Invoke();
        State = SlideConfirmBarState.Idle;
    }
}