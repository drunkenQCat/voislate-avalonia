// ToastService — IToastService 的 Avalonia 实现（契约 §3；ToastHost 承载显示）。
//
// 生命周期：Show(message) → CurrentMessage 置值（ToastHost 底部弹出）→ 2.6s 后自动清空。
// 与 ToastHost 的接线由 C 在窗口装配时完成：toastService.Host = host（自动建立绑定，
// 无需 XAML 手写 Binding）。

using Avalonia;
using Avalonia.Data;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using VoiSlate.Services;

namespace VoiSlate.Controls;

/// <summary>基于 ToastHost 的 IToastService 实现（全局单例，C 接线）。</summary>
public sealed partial class ToastService : ObservableObject, IToastService
{
    private const double AutoDismissMilliseconds = 2600;

    private ToastHost? _host;
    private DispatcherTimer? _dismissTimer;

    public ToastService()
    {
    }

    public ToastService(ToastHost? host)
    {
        Host = host;
    }

    /// <summary>承载显示的 ToastHost；设置时自动把 CurrentMessage 绑定到宿主 Message。</summary>
    public ToastHost? Host
    {
        get => _host;
        set
        {
            if (ReferenceEquals(_host, value))
            {
                return;
            }

            _host = value;
            if (_host is not null)
            {
                _host.Bind(ToastHost.MessageProperty, new Binding(nameof(CurrentMessage)) { Source = this });
            }
        }
    }

    /// <summary>当前 toast 文本（空 → ToastHost 隐藏）。</summary>
    [ObservableProperty]
    private string? _currentMessage;

    /// <inheritdoc />
    public void Show(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        // Show 可能在非 UI 线程被调用（如后台服务）；统一调度到 UI 线程。
        if (Dispatcher.UIThread.CheckAccess())
        {
            ShowCore(message);
        }
        else
        {
            Dispatcher.UIThread.Post(() => ShowCore(message));
        }
    }

    private void ShowCore(string message)
    {
        CurrentMessage = message;

        _dismissTimer?.Stop();
        _dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(AutoDismissMilliseconds) };
        _dismissTimer.Tick += (_, _) =>
        {
            _dismissTimer?.Stop();
            CurrentMessage = null;
        };
        _dismissTimer.Start();
    }
}