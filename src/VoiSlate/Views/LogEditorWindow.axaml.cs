using Avalonia.Controls;
using VoiSlate.ViewModels;

namespace VoiSlate.Views;

/// <summary>
/// Agent C：场记编辑对话框（契约 §6 LogEditorWindow；数据经 LogEditorViewModel —— B 正式实现）。
/// 保存/删除经 ITakeFlowService（唯一写入口 B1）；Saved/Deleted 置位即关闭。
/// </summary>
public partial class LogEditorWindow : Window
{
    public LogEditorWindow()
    {
        InitializeComponent();
    }

    public static Task ShowDialogAsync(Window owner, LogEditorViewModel vm)
    {
        var window = new LogEditorWindow
        {
            DataContext = vm,
        };
        System.ComponentModel.PropertyChangedEventHandler closer = null!;
        closer = (_, e) =>
        {
            if (e.PropertyName is nameof(LogEditorViewModel.Saved) or nameof(LogEditorViewModel.Deleted))
            {
                vm.PropertyChanged -= closer;
                window.Close();
            }
        };
        vm.PropertyChanged += closer;
        return window.ShowDialog(owner);
    }
}