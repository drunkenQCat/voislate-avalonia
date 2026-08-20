using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using VoiSlate.Models;
using VoiSlate.ViewModels;

namespace VoiSlate.Views;

/// <summary>
/// Agent C：计划页视图（数据全经 ScheduleViewModel / IScheduleBook；导入走文件选择器，编辑经 SmallEditDialog）。
/// </summary>
public partial class ScheduleView : UserControl
{
    private ScheduleViewModel? Vm => DataContext as ScheduleViewModel;

    public ScheduleView()
    {
        InitializeComponent();
    }

    private async void OnImportCsvClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm || this.VisualRoot is not Window owner)
        {
            return;
        }

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择拍摄计划 CSV",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("CSV 文件") { Patterns = ["*.csv"] }],
        });
        if (files.Count == 0)
        {
            return;
        }

        await using var stream = await files[0].OpenReadAsync();
        await vm.ImportCsvAsync(stream);
    }

    private async void OnEditClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm || this.VisualRoot is not Window owner)
        {
            return;
        }

        if (vm.SelectedScene is not { } scene || vm.SelectedShotIndex < 0 || vm.SelectedShotIndex >= scene.Count)
        {
            return;
        }

        var old = scene.Items[vm.SelectedShotIndex];
        var (ok, text) = await SmallEditDialog.ShowAsync(owner, "编辑镜头备注",
            $"镜头 {old.Name} 的备注（append）", old.Note.Append);
        if (!ok)
        {
            return;
        }

        var updated = new ScheduleItem(old.Key, old.Fix,
            new Note([.. old.Note.Objects], old.Note.Type, text));
        vm.ApplyShotEdit(vm.SelectedSceneIndex, vm.SelectedShotIndex, updated);
    }
}
