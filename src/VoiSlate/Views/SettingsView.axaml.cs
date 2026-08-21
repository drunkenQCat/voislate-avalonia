using Avalonia.Controls;
using Avalonia.Interactivity;
using VoiSlate.ViewModels;

namespace VoiSlate.Views;

/// <summary>设置页视图（全屏 push 页：复刻 voislate-html 之 screen-settings；返回经主 VM GoBack）。</summary>
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        if ((this.VisualRoot as Window)?.DataContext is MainViewModel main)
        {
            main.GoBackCommand.Execute(null);
        }
    }
}