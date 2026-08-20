// ToastHost — 全局底部 Toast 宿主（契约 §5 ToastHost 行 + §3 IToastService）。
//
// 依赖属性：
//   Message string?  OneWay  非空时显示底部 toast（2.6s 后由 ToastService 清空）
//
// 装配（由 C 接线，见报告）：
//   * MainWindow 根 Grid 最上层放 ToastHost；
//   * DI 注册 ToastService（本实现类）为 IToastService 单例；
//   * 窗口构建后把 ToastService.Host 指向该 ToastHost（自动绑定 Message）。
//
// 本控件只负责"显示"，消息生命周期（2.6s 自动消失）由 ToastService 承载。

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace VoiSlate.Controls;

/// <summary>全局底部 Toast 宿主。</summary>
public class ToastHost : TemplatedControl
{
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<ToastHost, string?>(
            nameof(Message), defaultValue: null, defaultBindingMode: Avalonia.Data.BindingMode.OneWay);

    private Border? _toast;
    private TextBlock? _toastText;

    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _toast = e.NameScope.Find<Border>("PART_Toast");
        _toastText = e.NameScope.Find<TextBlock>("PART_ToastText");
        UpdateToast();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MessageProperty)
        {
            UpdateToast();
        }
    }

    private void UpdateToast()
    {
        var hasMessage = !string.IsNullOrEmpty(Message);
        if (_toast is null)
        {
            return;
        }

        if (_toastText is not null)
        {
            _toastText.Text = Message ?? string.Empty;
        }

        _toast.IsVisible = hasMessage;
        _toast.Opacity = hasMessage ? 1 : 0;
    }
}