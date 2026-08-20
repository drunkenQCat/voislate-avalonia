using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiSlate.Models;
using VoiSlate.Services;

namespace VoiSlate.ViewModels;

/// <summary>
/// 计划页 VM（契约 §4 ScheduleViewModel）。对齐原版 scene_schedule_page：
/// ImportCsv / AddScene / AddShot / ApplySceneEdit / ApplyShotEdit / DeleteScene / DeleteShot /
/// MoveScene / MoveShot（删后索引随动、至少 1 场 1 镜、undo 上一步）+ 选择联动 RecordingSessionViewModel。
/// IScheduleStore / ICsvScheduleParser 为 B 补桩（P0.5 未产；演进权 E——E 交付前计划页显示空、不落库）。
/// </summary>
public partial class ScheduleViewModel : ObservableObject
{
    private readonly IScheduleStore _store;
    private readonly ICsvScheduleParser _parser;
    private readonly RecordingSessionViewModel _session;

    private bool _syncingSelection;
    private DeletedScene? _undoScene;
    private DeletedShot? _undoShot;

    public ScheduleViewModel(IScheduleStore store, ICsvScheduleParser parser, RecordingSessionViewModel session)
    {
        _store = store;
        _parser = parser;
        _session = session;
        _session.PropertyChanged += OnSessionPropertyChanged;
    }

    public ObservableCollection<SceneSchedule> Scenes { get; } = [];

    [ObservableProperty]
    private int _selectedSceneIndex;

    [ObservableProperty]
    private int _selectedShotIndex;

    public bool CanUndo => _undoScene != null || _undoShot != null;

    public bool HasScenes => Scenes.Count > 0;

    // ---- 选择联动（本 VM ↔ RecordingSessionViewModel 双向）----

    partial void OnSelectedSceneIndexChanged(int value)
    {
        if (_syncingSelection)
        {
            return;
        }

        // 原版 leftList.onTap：选场 → 镜/次归 0
        _session.SelectScene(value);
        _session.SelectShot(0);
        _session.SelectTake(0);
    }

    partial void OnSelectedShotIndexChanged(int value)
    {
        if (_syncingSelection)
        {
            return;
        }

        // 原版 rightList.onTap：选镜 → 次归 0
        _session.SelectShot(value);
        _session.SelectTake(0);
    }

    private void OnSessionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RecordingSessionViewModel.SelectedSceneIndex):
                SyncSelection(scene: _session.SelectedSceneIndex);
                break;
            case nameof(RecordingSessionViewModel.SelectedShotIndex):
                SyncSelection(shot: _session.SelectedShotIndex);
                break;
        }
    }

    private void SyncSelection(int? scene = null, int? shot = null)
    {
        _syncingSelection = true;
        try
        {
            // 有意直写 backing field：绕开生成 setter 的会话回写（本处即为会话联动回填）
#pragma warning disable MVVMTK0034
            if (scene is not null && _selectedSceneIndex != scene)
            {
                _selectedSceneIndex = scene.Value;
            }

            if (shot is not null && _selectedShotIndex != shot)
            {
                _selectedShotIndex = shot.Value;
            }
#pragma warning restore MVVMTK0034
            if (scene is not null)
            {
                OnPropertyChanged(nameof(SelectedSceneIndex));
            }

            if (shot is not null)
            {
                OnPropertyChanged(nameof(SelectedShotIndex));
            }
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    // ---- 加载 / 导入 ----

    /// <summary>加载计划（C 于计划页激活时调用——R7 计划数据陈旧刷新）。</summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        var loaded = await _store.LoadAllAsync();
        Scenes.Clear();
        foreach (var scene in loaded)
        {
            Scenes.Add(scene);
        }

        // 以会话当前选择为准（联动），夹取到有效范围
        var sceneIndex = Scenes.Count == 0
            ? 0
            : Math.Clamp(_session.SelectedSceneIndex, 0, Scenes.Count - 1);
        SyncSelection(scene: sceneIndex, shot: 0);
        OnPropertyChanged(nameof(HasScenes));
        ct.ThrowIfCancellationRequested();
    }

    /// <summary>CSV 导入（全量替换，对齐原版导入路径）。C 文件选择后调用。 </summary>
    public async Task ImportCsvAsync(Stream stream, CancellationToken ct = default)
    {
        var parsed = await _parser.ParseAsync(stream, ct);
        Scenes.Clear();
        foreach (var scene in parsed)
        {
            Scenes.Add(scene);
        }

        SyncSelection(scene: Scenes.Count == 0 ? 0 : Math.Clamp(_session.SelectedSceneIndex, 0, Scenes.Count - 1), shot: 0);
        OnPropertyChanged(nameof(HasScenes));
        await PersistAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task ImportCsv(Stream? stream) => stream is null ? Task.CompletedTask : ImportCsvAsync(stream);

    // ---- 增：场次+ / 镜头+（对齐 addNewSceneAtLast / addNewShotAtLast）----

    /// <summary>场次+：克隆当前场信息（key=max+1、fix=''），占位镜 ('1','',近景)。返回新场（C 定位编辑器用）。</summary>
    public SceneSchedule AddScene()
    {
        var current = Scenes.Count > 0 ? Scenes[Math.Min(SelectedSceneIndex, Scenes.Count - 1)] : null;
        var key = NextKey(Scenes.Select(s => s.Info.Key));
        var newInfo = new ScheduleItem(key, string.Empty, CloneNote(current?.Info.Note ?? new Note()));
        var newShot = new ScheduleItem("1", string.Empty, new Note([.. newInfo.Note.Objects], "近景", string.Empty));
        var scene = new SceneSchedule([newShot], newInfo);
        Scenes.Add(scene);
        OnPropertyChanged(nameof(HasScenes));
        PersistAsync();
        return scene;
    }

    /// <summary>新增场三步曲第 3 步（原版“场次+”流程）：用编辑后 objects 重建占位镜（'从{a}的【正面】拍【近景】'）。</summary>
    public void FinalizeNewScene(int sceneIndex)
    {
        if (sceneIndex < 0 || sceneIndex >= Scenes.Count)
        {
            return;
        }

        var info = Scenes[sceneIndex].Info;
        var objects = info.Note.Objects;
        var appendText = $"从{string.Join("，", objects)}的【正面】拍【近景】";
        var newShot = new ScheduleItem("1", string.Empty, new Note([.. objects], "近景", appendText));
        Scenes[sceneIndex] = new SceneSchedule([newShot], info);
        PersistAsync();
    }

    /// <summary>镜头+：克隆当前场末镜（key=max+1、fix=''）。返回 false=无有效场/镜。 </summary>
    public bool AddShot()
    {
        if (!ValidSelection())
        {
            return false;
        }

        var scene = Scenes[SelectedSceneIndex];
        if (scene.Count == 0)
        {
            return false;
        }

        var last = scene.Items[^1];
        var newShot = new ScheduleItem(NextKey(scene.Items.Select(x => x.Key)), string.Empty, CloneNote(last.Note));
        scene.Add(newShot); // DataList 查重：重名抛 DuplicateItemException（key+fix 唯一时不会触发）
        PersistAsync();
        return true;
    }

    // ---- 改：编辑（C 的 NoteEditor 对话框收集后回写；重名返回 false）----

    public bool ApplySceneEdit(int sceneIndex, ScheduleItem newInfo)
    {
        if (sceneIndex < 0 || sceneIndex >= Scenes.Count)
        {
            return false;
        }

        try
        {
            var scene = Scenes[sceneIndex];
            Scenes[sceneIndex] = new SceneSchedule([.. scene.Items], newInfo);
        }
        catch (DuplicateItemException)
        {
            return false; // C 提示“本镜号已存在”
        }

        PersistAsync();
        return true;
    }

    public bool ApplyShotEdit(int sceneIndex, int shotIndex, ScheduleItem newItem)
    {
        if (sceneIndex < 0 || sceneIndex >= Scenes.Count)
        {
            return false;
        }

        var scene = Scenes[sceneIndex];
        if (shotIndex < 0 || shotIndex >= scene.Count)
        {
            return false;
        }

        try
        {
            scene.Update(shotIndex, newItem);
        }
        catch (DuplicateItemException)
        {
            return false;
        }

        PersistAsync();
        return true;
    }

    // ---- 删（删后索引随动 + 至少 1 场 1 镜 + undo 上一步）----

    /// <summary>删场。返回 false 且 message 非空 = 至少保留一个场守卫。</summary>
    public bool DeleteScene(int sceneIndex, out string? message)
    {
        if (Scenes.Count <= 1)
        {
            message = "至少保留一个场";
            return false;
        }

        if (sceneIndex < 0 || sceneIndex >= Scenes.Count)
        {
            message = null;
            return false;
        }

        var removed = Scenes[sceneIndex];
        var selected = _session.SelectedSceneIndex;
        if (selected == sceneIndex && selected == Scenes.Count - 1)
        {
            // 删除的是“选中的最后一个场”→ 上移一位并镜/次归 0（原版 removeItem 分支）
            _session.SelectScene(sceneIndex - 1);
            _session.SelectShot(0);
            _session.SelectTake(0);
        }
        else if (selected > sceneIndex)
        {
            _session.SelectScene(selected - 1);
        }

        Scenes.RemoveAt(sceneIndex);
        _undoScene = new DeletedScene(sceneIndex, removed);
        _undoShot = null;
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(HasScenes));
        message = $"第 {removed.Info.Name} 场已删除";
        PersistAsync();
        return true;
    }

    /// <summary>删镜。返回 false 且 message 非空 = 至少保留一个镜守卫。</summary>
    public bool DeleteShot(int shotIndex, out string? message)
    {
        if (!ValidSelection())
        {
            message = null;
            return false;
        }

        var scene = Scenes[SelectedSceneIndex];
        if (scene.Count <= 1)
        {
            message = "至少保留一个镜";
            return false;
        }

        if (shotIndex < 0 || shotIndex >= scene.Count)
        {
            message = null;
            return false;
        }

        var removed = scene.RemoveAt(shotIndex);
        var selected = _session.SelectedShotIndex;
        if (selected == shotIndex && selected == scene.Count)
        {
            // 删除的是“选中的最后一个镜”→ 上移一位并次归 0（原版分支；删后 scene.Count == 末索引）
            _session.SelectShot(shotIndex - 1);
            _session.SelectTake(0);
        }
        else if (selected > shotIndex)
        {
            _session.SelectShot(selected - 1);
        }

        _undoScene = null;
        _undoShot = new DeletedShot(SelectedSceneIndex, shotIndex, removed);
        OnPropertyChanged(nameof(CanUndo));
        message = $"第 {removed.Name} 镜已删除";
        PersistAsync();
        return true;
    }

    /// <summary>撤销上一步删除（SnackBar “恢复”）。</summary>
    [RelayCommand]
    private void Undo()
    {
        if (_undoScene is { } sceneUndo)
        {
            Scenes.Insert(Math.Min(sceneUndo.Index, Scenes.Count), sceneUndo.Scene);
            _undoScene = null;
        }
        else if (_undoShot is { } shotUndo)
        {
            var scene = Scenes[shotUndo.SceneIndex];
            scene.Insert(Math.Min(shotUndo.Index, scene.Count), shotUndo.Item);
            _undoShot = null;
        }

        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(HasScenes));
        PersistAsync();
    }

    // ---- 移动（Reorderable 语义：newIndex > oldIndex 时先减 1，选中随拖拽项）----

    public void MoveScene(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= Scenes.Count)
        {
            return;
        }

        if (newIndex > oldIndex)
        {
            newIndex -= 1;
        }

        if (newIndex < 0 || newIndex >= Scenes.Count)
        {
            return;
        }

        var item = Scenes[oldIndex];
        Scenes.RemoveAt(oldIndex);
        Scenes.Insert(newIndex, item);
        _session.SelectScene(newIndex);
        _session.SelectShot(0);
        _session.SelectTake(0);
        PersistAsync();
    }

    public void MoveShot(int oldIndex, int newIndex)
    {
        if (!ValidSelection())
        {
            return;
        }

        var scene = Scenes[SelectedSceneIndex];
        if (oldIndex < 0 || oldIndex >= scene.Count)
        {
            return;
        }

        if (newIndex > oldIndex)
        {
            newIndex -= 1;
        }

        if (newIndex < 0 || newIndex >= scene.Count)
        {
            return;
        }

        var item = scene.RemoveAt(oldIndex);
        scene.Insert(newIndex, item);
        _session.SelectShot(newIndex);
        _session.SelectTake(0);
        PersistAsync();
    }

    // ---- 内部 ----

    public Task PersistAsync() => _store.SaveAllAsync(Scenes.ToList());

    private bool ValidSelection()
        => Scenes.Count > 0 && SelectedSceneIndex >= 0 && SelectedSceneIndex < Scenes.Count;

    private static string NextKey(IEnumerable<string> keys)
    {
        var max = keys.Select(k => int.TryParse(k, out var v) ? v : 0).DefaultIfEmpty(0).Max();
        return (max + 1).ToString();
    }

    private static Note CloneNote(Note src) => new([.. src.Objects], src.Type, src.Append);

    private sealed record DeletedScene(int Index, SceneSchedule Scene);

    private sealed record DeletedShot(int SceneIndex, int Index, ScheduleItem Item);
}