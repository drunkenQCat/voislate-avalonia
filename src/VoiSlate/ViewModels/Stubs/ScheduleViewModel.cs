using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiSlate.Models;
using VoiSlate.Services;

namespace VoiSlate.ViewModels;

/// <summary>
/// Agent C stub：计划页 VM（契约 §4 ScheduleViewModel —— Agent B 产出，本文件为编译用占位，合并后删除）。
/// 契约命令：ImportCsv/AddScene/AddShot/EditItem/DeleteItem/MoveItem。
/// 数据源 IScheduleBook（P0.5 只读 Book；写入演进归 E 的 ScheduleStore）。
/// stub 的增删为内存操作 + Toast 提示；undo/索引随动/至少 1 场 1 镜由 B 正式实现。
/// </summary>
public partial class ScheduleViewModel : ObservableObject
{
    private readonly IScheduleBook _book;
    private readonly IToastService _toast;

    public ObservableCollection<SceneSchedule> Scenes { get; } = [];

    [ObservableProperty]
    private int _selectedSceneIndex;

    [ObservableProperty]
    private int _selectedShotIndex;

    /// <summary>当前场（计算）。</summary>
    public SceneSchedule? SelectedScene =>
        SelectedSceneIndex >= 0 && SelectedSceneIndex < Scenes.Count ? Scenes[SelectedSceneIndex] : null;

    /// <summary>当前场镜列表（计算）。</summary>
    public IReadOnlyList<ScheduleItem> SelectedSceneShots => SelectedScene?.Items ?? [];

    public RelayCommand ImportCsvCommand { get; }

    public RelayCommand AddSceneCommand { get; }

    public RelayCommand AddShotCommand { get; }

    public RelayCommand<string> EditItemCommand { get; }

    public RelayCommand<string> DeleteItemCommand { get; }

    public RelayCommand<string> MoveItemCommand { get; }

    public ScheduleViewModel(IScheduleBook book, IToastService toast)
    {
        _book = book;
        _toast = toast;

        ImportCsvCommand = new RelayCommand(() => _toast.Show("导入 CSV（占位：待 CsvScheduleParser + ScheduleStore，契约 §3）"));
        AddSceneCommand = new RelayCommand(AddScene);
        AddShotCommand = new RelayCommand(AddShot);
        EditItemCommand = new RelayCommand<string>(key => _toast.Show($"编辑（占位：NoteEditorWindow 由 C 后续交付）—— {key}"));
        DeleteItemCommand = new RelayCommand<string>(DeleteItem);
        MoveItemCommand = new RelayCommand<string>(key => _toast.Show("拖拽排序（占位：ReorderableListView 语义由 B/D 提供）"));

        Reload();
    }

    partial void OnSelectedSceneIndexChanged(int value)
    {
        OnPropertyChanged(nameof(SelectedScene));
        OnPropertyChanged(nameof(SelectedSceneShots));
        SelectedShotIndex = 0;
    }

    private void Reload()
    {
        Scenes.Clear();
        for (var i = 0; i < _book.SceneCount; i++)
        {
            Scenes.Add(_book.GetScene(i));
        }

        if (Scenes.Count == 0)
        {
            Scenes.Add(new SceneSchedule(
                [new ScheduleItem("1", string.Empty, new Note())],
                new ScheduleItem("1", string.Empty, new Note { Type = "默认" })));
        }

        SelectedSceneIndex = 0;
        OnPropertyChanged(nameof(SelectedSceneShots));
    }

    private void AddScene()
    {
        var index = Scenes.Count + 1;
        Scenes.Add(new SceneSchedule(
            [new ScheduleItem("1", string.Empty, new Note { Type = "近景" })],
            new ScheduleItem(index.ToString(), string.Empty, new Note { Type = "近景" })));
        SelectedSceneIndex = Scenes.Count - 1;
        _toast.Show($"已添加第 {index} 场（stub 内存态；持久化待 E 的 ScheduleStore）");
    }

    private void AddShot()
    {
        if (SelectedScene == null) return;
        var index = SelectedScene.Count + 1;
        SelectedScene.Add(new ScheduleItem(index.ToString(), string.Empty, new Note { Type = "近景" }));
        SelectedShotIndex = SelectedScene.Count - 1;
        OnPropertyChanged(nameof(SelectedSceneShots));
        _toast.Show($"已添加第 {SelectedScene.Info.Name} 场第 {index} 镜（stub 内存态）");
    }

    private void DeleteItem(string? target)
    {
        if (target == "scene")
        {
            if (Scenes.Count <= 1)
            {
                _toast.Show("至少保留一个场");
                return;
            }

            Scenes.RemoveAt(SelectedSceneIndex);
            SelectedSceneIndex = Math.Clamp(SelectedSceneIndex, 0, Scenes.Count - 1);
            OnPropertyChanged(nameof(SelectedSceneShots));
            _toast.Show("场已删除（stub 内存态；undo 由 B 正式实现）");
            return;
        }

        if (target == "shot")
        {
            if (SelectedScene == null || SelectedScene.Count <= 1)
            {
                _toast.Show("至少保留一个镜");
                return;
            }

            SelectedScene.RemoveAt(SelectedShotIndex);
            SelectedShotIndex = Math.Clamp(SelectedShotIndex, 0, SelectedScene.Count - 1);
            OnPropertyChanged(nameof(SelectedSceneShots));
            _toast.Show("镜已删除（stub 内存态；undo 由 B 正式实现）");
        }
    }
}