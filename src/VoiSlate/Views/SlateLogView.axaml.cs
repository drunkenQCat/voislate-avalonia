using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VoiSlate.Models;
using VoiSlate.ViewModels;

namespace VoiSlate.Views;

/// <summary>
/// Agent C：场记页视图（日期切换 + 当日卡片；编辑经 LogEditorWindow——LogEditorViewModel stub，
/// ITakeFlowService.SaveEditAsync 唯一写入口；删除经 VM DeleteCommand）。
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
        }
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (Vm is { } vm)
        {
            vm.EditRequested -= OnVmEditRequested;
        }
    }

    private void OnEditClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;
        if (sender is not Button { Tag: SlateLogItem item }) return;
        vm.RequestEdit(item); // 日期守卫 + 索引解析 + EditRequested 事件（stub：跨日限制提示）
    }

    private async void OnVmEditRequested(SlateLogItem item, int index)
    {
        if (Vm is not { } vm) return;
        var owner = this.VisualRoot as Window;
        if (owner == null) return;
        await LogEditorWindow.ShowDialogAsync(owner, item, index, vm.TakeFlowService);
    }
}