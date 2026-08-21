using Avalonia.Controls;
using Avalonia.Interactivity;
using VoiSlate.ViewModels;

namespace VoiSlate.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>底部导航区命中（按 x 分段：计划/记录/场记）。
    /// 按钮在部分 X 环境下命中不可靠（动态背景/边缘），改由 nav 区 PointerPressed 直接驱动，
    /// 视觉高亮仍由 CurrentPageKey + PageKeyVis pill 驱动。</summary>
    private void OnNavAreaPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || sender is not Avalonia.Visual area)
        {
            return;
        }

        var x = e.GetPosition(area).X;
        var thirds = area.Bounds.Width / 3;
        var key = x < thirds ? "Schedule" : x < thirds * 2 ? "Record" : "SlateLog";
        vm.NavigateCommand.Execute(key);
    }

    /// <summary>导航入口（AppBar 齿轮 / 底部 3 Tab）。Command 绑定之外加 Click 兜底，
    /// 生产量产的 X11 点击经 CommandParameter 直达导航（幂等双通道）。</summary>
    private void OnNavButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: string key } && DataContext is MainViewModel vm && vm.NavigateCommand.CanExecute(key))
        {
            vm.NavigateCommand.Execute(key);
        }
    }
}