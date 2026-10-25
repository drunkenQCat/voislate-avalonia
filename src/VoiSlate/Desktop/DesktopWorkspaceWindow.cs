using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;

namespace VoiSlate.Desktop;

/// <summary>
/// 桌面薄窗口壳。窗口属性与键盘转发留在这里，界面本体全在平台无关的 <see cref="DesktopWorkspaceView"/>：
/// 桌面仍是 1120×860 的独立窗口，Android 则直接把这个控件挂到 Activity 上（Android 没有可 new 的 Window）。
/// </summary>
public sealed class DesktopWorkspaceWindow : Window
{
    private readonly DesktopWorkspaceView _view;

    public DesktopWorkspaceWindow(DesktopWorkspaceService service, Speech.BailianSettingsStore speechSettings,
        Speech.BailianRealtimeClient? speechClient = null, Func<int, Speech.IPcmCapture>? captureFactory = null)
    {
        Title = "VoiSlate · 现场场记工作台"; Width = 1120; Height = 860; MinWidth = 360; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        // 窄屏自测用：VOISLATE_WINDOW_WIDTH=390 可以直接把工作台按手机宽度打开（不必上真机）。
        if (double.TryParse(Environment.GetEnvironmentVariable("VOISLATE_WINDOW_WIDTH"), out var debugWidth) && debugWidth >= 320) Width = debugWidth;
        if (double.TryParse(Environment.GetEnvironmentVariable("VOISLATE_WINDOW_HEIGHT"), out var debugHeight) && debugHeight >= 480) Height = debugHeight;
        RequestedThemeVariant = ThemeVariant.Light; Classes.Add("workspace");
        Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F2F5F3"));
        _view = new DesktopWorkspaceView(service, speechSettings, speechClient, captureFactory);
        Content = _view;
        AddHandler(InputElement.KeyDownEvent, (_, e) => _view.HandleShortcut(e), RoutingStrategies.Tunnel, handledEventsToo: true);
        // 退出前备份：原来挂在窗口自身的 Closing 上，现在逻辑在控件里，窗口只负责「取消一次关闭再关」。
        Closing += async (_, e) =>
        {
            if (_view.ExitReady) return;
            e.Cancel = true;
            try { if (await _view.PrepareExitAsync()) Close(); }
            catch { /* 退出备份异常已在 PrepareExitAsync 内提示，这里兜底避免 async void 崩溃 */ }
        };
    }

    /// <summary>供测试与 Android 宿主访问界面本体。</summary>
    internal DesktopWorkspaceView View => _view;
}
