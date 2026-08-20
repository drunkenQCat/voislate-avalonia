using Avalonia.Controls;
using VoiSlate.Models;
using VoiSlate.Services;
using VoiSlate.ViewModels;

namespace VoiSlate.Views;

/// <summary>
/// Agent C：场记编辑对话框（契约 §6 LogEditorWindow；数据经 LogEditorViewModel —— B 的 stub）。
/// DataContext 注入 LogEditorViewModel；保存/删除经 ITakeFlowService。
/// </summary>
public partial class LogEditorWindow : Window
{
    private LogEditorWindow()
    {
        InitializeComponent();
    }

    public static Task ShowDialogAsync(Window owner, SlateLogItem item, int index, ITakeFlowService flow)
    {
        var vm = new LogEditorViewModel(item, index, flow);
        var window = new LogEditorWindow
        {
            DataContext = vm,
        };
        vm.CloseRequested += () => window.Close();
        return window.ShowDialog(owner);
    }
}