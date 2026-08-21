using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using VoiSlate.Models;
using VoiSlate.ViewModels;

namespace VoiSlate.Views;

/// <summary>
/// 场记页视图（复刻 voislate-html 之 page-log：日期 Tab + 两级 ExpansionTile + 行色；
/// docs/ui-layout-spec.md §5）。tab 生成/prev-next 走 code-behind（Dates 真实来自 ILogRepository）；
/// 双击 log-item → LogEditorWindow（保留既有编辑/删除能力）。
/// </summary>
public partial class SlateLogView : UserControl
{
    private SlateLogViewModel? Vm => DataContext as SlateLogViewModel;

    public SlateLogView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (Vm is { } vm)
        {
            vm.EditRequested += OnVmEditRequested;
            vm.PropertyChanged += OnVmPropertyChanged;
            RebuildTabs();
        }
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;
        vm.EditRequested -= OnVmEditRequested;
        vm.PropertyChanged -= OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SlateLogViewModel.SelectedDate)
            or nameof(SlateLogViewModel.Dates))
        {
            RebuildTabs();
        }
    }

    // ================================================================ 日期 Tab（.log-tabs / .log-tab）

    private void RebuildTabs()
    {
        if (Vm is not { } vm)
        {
            return;
        }

        TabHost.Children.Clear();
        foreach (var date in vm.Dates)
        {
            var tab = new Button
            {
                Height = 52,
                Padding = new Thickness(0, 0, 14, 0),
                Background = Brushes.Transparent,
                FontSize = 13,
                Tag = date,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            };
            tab.BorderThickness = new Thickness(0, 0, 0, 3);
            var active = string.Equals(date, vm.SelectedDate, StringComparison.Ordinal);
            tab.Foreground = new SolidColorBrush(Color.Parse(active ? "#2196F3" : "#222222"));
            tab.BorderBrush = new SolidColorBrush(Color.Parse(active ? "#2196F3" : "Transparent"));
            tab.FontWeight = active ? FontWeight.Medium : FontWeight.Normal;
            tab.Content = new TextBlock
            {
                Text = date,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            };
            tab.Click += (_, _) =>
            {
                if (tab.Tag is string d)
                {
                    vm.SelectedDate = d;
                }
            };
            TabHost.Children.Add(tab);
        }
    }

    private void OnPrevClick(object? sender, RoutedEventArgs e) => ShiftDate(-1);

    private void OnNextClick(object? sender, RoutedEventArgs e) => ShiftDate(+1);

    private void ShiftDate(int delta)
    {
        if (Vm is not { } vm)
        {
            return;
        }

        var idx = vm.Dates.IndexOf(vm.SelectedDate);
        var target = idx + delta;
        if (idx >= 0 && target >= 0 && target < vm.Dates.Count)
        {
            vm.SelectedDate = vm.Dates[target];
        }
    }

    // ================================================================ 两级分组展开/收起

    private void OnSceneHeadClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: LogSceneGroup group })
        {
            group.IsOpen = !group.IsOpen;
        }
    }

    private void OnShotHeadClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: LogShotGroup group })
        {
            group.IsOpen = !group.IsOpen;
        }
    }

    // ================================================================ 双击条目 → LogEditor

    private void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border { Tag: SlateLogItem item })
        {
            return;
        }

        Vm?.RequestEdit(item);
    }

    private async void OnVmEditRequested(LogEditorViewModel editor)
    {
        if (this.VisualRoot is not Window owner)
        {
            return;
        }

        await LogEditorWindow.ShowDialogAsync(owner, editor);
    }
}