using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VoiSlate.ViewModels;

namespace VoiSlate.Views;

/// <summary>设置页视图（全屏 push 页：复刻 voislate-html 之 screen-settings；返回经主 VM GoBack；
/// 清空今日场记经 §8 样式 confirm 蒙层确认后执行 ClearTodayCommand）。</summary>
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    /// <summary>返回头整区命中即返回（按钮在部分 X 环境下命中不可靠，区域转发更稳）。</summary>
    private void OnHeaderPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        GoBack();
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => GoBack();

    private void GoBack()
    {
        App.Main?.GoBackCommand.Execute(null);
    }

    // ================================================================ confirm overlay（clearToday）

    private void OnClearTodayClick(object? sender, RoutedEventArgs e)
    {
        // CONFIRM_MAP['clearToday']：清除场记 / 是否确认要清除场记？ / 确认
        ConfirmTitle.Text = "清除场记";
        ConfirmBody.Text = "是否确认要清除场记？";
        ConfirmOverlay.IsVisible = true;
    }

    private void OnConfirmOk(object? sender, RoutedEventArgs e)
    {
        ConfirmOverlay.IsVisible = false;
        if (DataContext is SettingsViewModel vm)
        {
            vm.ClearTodayCommand.Execute(null);
        }
    }

    private void OnConfirmCancel(object? sender, RoutedEventArgs e) => ConfirmOverlay.IsVisible = false;

    private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ReferenceEquals(e.Source, ConfirmOverlay))
        {
            ConfirmOverlay.IsVisible = false; // 点击蒙层空白处关闭（e.target===e.currentTarget）
        }
    }
}