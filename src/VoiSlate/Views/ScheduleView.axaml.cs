using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using VoiSlate.Models;
using VoiSlate.ViewModels;

namespace VoiSlate.Views;

/// <summary>
/// 计划页视图（复刻 voislate-html 之 renderSchedulePage（docs/ui-layout-spec.md §4）：
/// 左右两栏场景/镜头列表 + 选中高亮 + scn-info 紫条 + add-scn-tile + 右下双 FAB。
/// 双击场行/镜行/紫条 → sheet 编辑（SmallEditDialog 简化版，数据真实落库 ApplySceneEdit/ApplyShotEdit）；
/// 导入 CSV 走系统文件选择器（真实解析）。
/// </summary>
public partial class ScheduleView : UserControl
{
    private ScheduleViewModel? Vm => DataContext as ScheduleViewModel;

    public ScheduleView()
    {
        InitializeComponent();
        // DataContextChanged 在 DataTemplate 绑定后触发（AttachedToVisualTree 时 DataContext 可能尚未绑定）
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ScheduleViewModel vm && !IsLoadedOnce)
            {
                IsLoadedOnce = true;
                _ = vm.LoadAsync();
            }
        };
    }

    private bool IsLoadedOnce;

    // ================================================================ 导入 CSV（.low FAB）

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

    // ================================================================ 双击/单击 → sheet 编辑

    private async void OnSceneDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: SceneSchedule scene })
        {
            return;
        }

        var idx = Vm is { } vm ? vm.Scenes.IndexOf(scene) : -1;
        if (idx < 0)
        {
            return;
        }

        await EditSceneAsync(idx);
    }

    private async void OnSceneInfoTapped(object? sender, TappedEventArgs e)
    {
        if (Vm is { } vm && vm.SelectedSceneIndex >= 0 && vm.SelectedSceneIndex < vm.Scenes.Count)
        {
            await EditSceneAsync(vm.SelectedSceneIndex);
        }
    }

    private async void OnShotDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: ScheduleItem shot } || Vm is not { } vm)
        {
            return;
        }

        var scene = vm.SelectedScene;
        var shotIdx = -1;
        if (scene is not null)
        {
            for (var i = 0; i < scene.Items.Count; i++)
            {
                if (ReferenceEquals(scene.Items[i], shot))
                {
                    shotIdx = i;
                    break;
                }
            }
        }

        if (scene is null || shotIdx < 0)
        {
            return;
        }

        await EditShotAsync(vm.SelectedSceneIndex, shotIdx, shot);
    }

    /// <summary>编辑场次（sheet scene 简化：名称 key/fix + 场地 type 两步）。</summary>
    private async Task EditSceneAsync(int sceneIndex)
    {
        if (Vm is not { } vm || this.VisualRoot is not Window owner)
        {
            return;
        }

        var scene = vm.Scenes[sceneIndex];
        var (okName, name) = await SmallEditDialog.ShowAsync(
            owner, "编辑场次", "场名（key 1-200；fix 空或 A-Z，如 1A）", scene.Info.Name);
        if (!okName)
        {
            return;
        }

        var (okType, type) = await SmallEditDialog.ShowAsync(
            owner, "场地类型", "场地（info.note.type），如 万星园 / 洛肯实验室", scene.Info.Note.Type);
        if (!okType)
        {
            return;
        }

        var key = name.Length > 0 ? name[..1] : "1";
        var fix = name.Length > 1 ? name[1..] : string.Empty;
        var updated = new ScheduleItem(key, fix, new Note([.. scene.Info.Note.Objects], type, scene.Info.Note.Append));
        vm.ApplySceneEdit(sceneIndex, updated);
    }

    /// <summary>编辑镜头（sheet shot 简化：名称 key/fix + 类型 type 两步；append 保留）。</summary>
    private async Task EditShotAsync(int sceneIndex, int shotIndex, ScheduleItem shot)
    {
        if (Vm is not { } vm || this.VisualRoot is not Window owner)
        {
            return;
        }

        var (okName, name) = await SmallEditDialog.ShowAsync(
            owner, "编辑镜头", "镜名（key + fix，如 1A / 2B / 3C）", shot.Name);
        if (!okName)
        {
            return;
        }

        var (okType, type) = await SmallEditDialog.ShowAsync(
            owner, "镜头类型", "景别（特写/近景/中景/全景/远景）", shot.Note.Type);
        if (!okType)
        {
            return;
        }

        var key = name.Length > 0 ? name[..1] : "1";
        var fix = name.Length > 1 ? name[1..] : string.Empty;
        var updated = new ScheduleItem(key, fix, new Note([.. shot.Note.Objects], type, shot.Note.Append));
        vm.ApplyShotEdit(sceneIndex, shotIndex, updated);
    }
}