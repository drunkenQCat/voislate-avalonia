using VoiSlate.Models;

namespace VoiSlate.Services;

/// <summary>
/// 计划簿用户数据服务（任务指定 IScheduleService；封装 ScheduleBook 的增删改查 + 持久化）。
/// - 唯一写入口：场景/镜的增删改/移动/全量替换（CSV 导入）/清空/undo 全部经本服务，每次变更立即落库。
/// - 不变量（原版计划页"至少 1 场 1 镜"）：DeleteSceneAsync 保留至少 1 场；DeleteShotAsync 保留至少 1 镜；
///   AddScene/ReplaceAll 拒绝空场/空计划（InvalidOperationException）。
/// - 重名：场名重名抛 DuplicateItemException（场景 BsonId=Info.Name，DataList 语义扩展）；镜重名由 DataList 抛。
/// - 简化 undo：每次变更前快照工作副本（上限 20 步）；UndoAsync 回滚上一步并落库（原版无 undo，迁移新增，契约 §2.7）。
/// - 读接口（SceneLabel/ShotLabel/ObjectsOf/GetScene/SceneCount）读服务内工作副本——页面激活时调 LoadAllAsync
///   刷新（R7）；C 接线 TakeFlowService 的标签/对象 Provider 时可改用本服务（取代缓存常驻的 IScheduleBook）。
/// </summary>
public interface IScheduleService
{
    /// <summary>读刷新：从存储重载工作副本并清空 undo 栈；返回快照（克隆，调用方改动不影响服务内部）。</summary>
    Task<IReadOnlyList<SceneSchedule>> LoadAllAsync(CancellationToken ct);

    // ---- 读（工作副本）----
    int SceneCount { get; }
    SceneSchedule GetScene(int index);
    string SceneLabel(int index);
    string ShotLabel(int sceneIndex, int shotIndex);
    IReadOnlyList<string> ObjectsOf(int sceneIndex, int shotIndex);

    // ---- 写（每次变更立即持久化）----
    Task AddSceneAsync(SceneSchedule scene, CancellationToken ct);
    Task AddShotAsync(int sceneIndex, ScheduleItem shot, CancellationToken ct);
    Task EditSceneInfoAsync(int sceneIndex, ScheduleItem info, CancellationToken ct);
    Task EditShotAsync(int sceneIndex, int shotIndex, ScheduleItem item, CancellationToken ct);
    Task DeleteSceneAsync(int sceneIndex, CancellationToken ct);
    Task DeleteShotAsync(int sceneIndex, int shotIndex, CancellationToken ct);
    Task MoveSceneAsync(int fromIndex, int toIndex, CancellationToken ct);
    Task MoveShotAsync(int sceneIndex, int fromIndex, int toIndex, CancellationToken ct);

    /// <summary>全量替换（CSV 导入语义）；空计划或含空场抛 InvalidOperationException。</summary>
    Task ReplaceAllAsync(IReadOnlyList<SceneSchedule> scenes, CancellationToken ct);

    /// <summary>清空拍摄计划（设置页语义；原版随后清 settings/history 并退出，属 B/C 接线，见报告）。</summary>
    Task ClearAsync(CancellationToken ct);

    /// <summary>撤销上一步（有可撤销项返回 true）。</summary>
    Task<bool> UndoAsync(CancellationToken ct);

    /// <summary>任何写操作成功后触发（R7：页面激活刷新 / ScheduleViewModel 联动）。</summary>
    event Action? ScenesChanged;
}

public sealed class ScheduleService : IScheduleService
{
    private const int MaxUndoSteps = 20;

    private readonly ScheduleStore _store;
    private List<SceneSchedule> _scenes = [];
    private readonly Stack<List<SceneSchedule>> _undo = new();
    private bool _loaded;

    public ScheduleService(ScheduleStore store)
    {
        _store = store;
    }

    public event Action? ScenesChanged;

    public int SceneCount
    {
        get
        {
            EnsureLoaded();
            return _scenes.Count;
        }
    }

    public SceneSchedule GetScene(int index)
    {
        EnsureLoaded();
        return _scenes[Math.Clamp(index, 0, Math.Max(0, _scenes.Count - 1))];
    }

    public string SceneLabel(int index)
    {
        EnsureLoaded();
        if (_scenes.Count == 0)
        {
            return string.Empty;
        }

        return _scenes[Math.Clamp(index, 0, _scenes.Count - 1)].Info.Name;
    }

    public string ShotLabel(int sceneIndex, int shotIndex)
    {
        EnsureLoaded();
        var scene = GetScene(sceneIndex);
        if (scene.Count == 0)
        {
            return string.Empty;
        }

        return scene[Math.Clamp(shotIndex, 0, scene.Count - 1)].Name;
    }

    public IReadOnlyList<string> ObjectsOf(int sceneIndex, int shotIndex)
    {
        EnsureLoaded();
        if (_scenes.Count == 0)
        {
            return [];
        }

        var scene = _scenes[Math.Clamp(sceneIndex, 0, _scenes.Count - 1)];
        if (scene.Count == 0)
        {
            return [];
        }

        return scene[Math.Clamp(shotIndex, 0, scene.Count - 1)].Note.Objects;
    }

    public async Task<IReadOnlyList<SceneSchedule>> LoadAllAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var loaded = await _store.LoadAllAsync();
        _scenes = loaded.Select(CloneScene).ToList();
        _undo.Clear();
        _loaded = true;
        return Snapshot();
    }

    public async Task AddSceneAsync(SceneSchedule scene, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        EnsureLoaded();
        if (scene.Count == 0)
        {
            throw new InvalidOperationException("新场至少要有 1 个镜头。");
        }

        var name = scene.Info.Name;
        if (_scenes.Any(s => s.Info.Name == name) || string.IsNullOrEmpty(name))
        {
            throw new DuplicateItemException("Duplicate items in the list");
        }

        await MutateAsync(ct, work =>
        {
            work.Add(CloneScene(scene));
        });
    }

    public async Task AddShotAsync(int sceneIndex, ScheduleItem shot, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        EnsureLoaded();
        await MutateAsync(ct, work =>
        {
            work[IndexOrThrow(work, sceneIndex)].Add(CloneItem(shot)); // DataList.Add 查重，重名抛 DuplicateItemException
        });
    }

    public async Task EditSceneInfoAsync(int sceneIndex, ScheduleItem info, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        EnsureLoaded();
        await MutateAsync(ct, work =>
        {
            var scene = work[IndexOrThrow(work, sceneIndex)];
            if (scene.Info.Name != info.Name &&
                work.Any(s => s.Info.Name == info.Name))
            {
                throw new DuplicateItemException("Duplicate items in the list");
            }

            scene.Info = CloneItem(info);
        });
    }

    public async Task EditShotAsync(int sceneIndex, int shotIndex, ScheduleItem item, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        EnsureLoaded();
        await MutateAsync(ct, work =>
        {
            var scene = work[IndexOrThrow(work, sceneIndex)];
            scene[IndexOrThrow(scene, shotIndex)] = CloneItem(item); // 索引器赋值走查重（C-13）
        });
    }

    public async Task DeleteSceneAsync(int sceneIndex, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        EnsureLoaded();
        if (_scenes.Count <= 1)
        {
            throw new InvalidOperationException("至少保留 1 场。");
        }

        await MutateAsync(ct, work =>
        {
            work.RemoveAt(IndexOrThrow(work, sceneIndex));
        });
    }

    public async Task DeleteShotAsync(int sceneIndex, int shotIndex, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        EnsureLoaded();
        await MutateAsync(ct, work =>
        {
            var scene = work[IndexOrThrow(work, sceneIndex)];
            if (scene.Count <= 1)
            {
                throw new InvalidOperationException("至少保留 1 镜。");
            }

            scene.RemoveAt(IndexOrThrow(scene, shotIndex));
        });
    }

    public async Task MoveSceneAsync(int fromIndex, int toIndex, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        EnsureLoaded();
        await MutateAsync(ct, work =>
        {
            fromIndex = IndexOrThrow(work, fromIndex);
            toIndex = Math.Clamp(toIndex, 0, work.Count - 1);
            if (fromIndex == toIndex)
            {
                return;
            }

            var item = work[fromIndex];
            work.RemoveAt(fromIndex);
            work.Insert(toIndex, item);
        });
    }

    public async Task MoveShotAsync(int sceneIndex, int fromIndex, int toIndex, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        EnsureLoaded();
        await MutateAsync(ct, work =>
        {
            var scene = work[IndexOrThrow(work, sceneIndex)];
            fromIndex = IndexOrThrow(scene, fromIndex);
            toIndex = Math.Clamp(toIndex, 0, scene.Count - 1);
            if (fromIndex == toIndex)
            {
                return;
            }

            var item = scene[fromIndex];
            scene.RemoveAt(fromIndex);
            scene.Insert(toIndex, item);
        });
    }

    public async Task ReplaceAllAsync(IReadOnlyList<SceneSchedule> scenes, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        EnsureLoaded();
        var list = scenes.ToList();
        if (list.Count == 0 || list.Any(s => s.Count == 0))
        {
            throw new InvalidOperationException("计划至少保留 1 场且每场至少 1 镜。");
        }

        // 场名查重（BsonId 唯一；与 AddSceneAsync 一致的口径）
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in list)
        {
            if (string.IsNullOrEmpty(s.Info.Name) || !names.Add(s.Info.Name))
            {
                throw new DuplicateItemException("Duplicate items in the list");
            }
        }

        await MutateAsync(ct, work =>
        {
            work.Clear();
            work.AddRange(list.Select(CloneScene));
        });
    }

    public async Task ClearAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        EnsureLoaded();
        await MutateAsync(ct, work => work.Clear());
    }

    public async Task<bool> UndoAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        EnsureLoaded();
        if (_undo.Count == 0)
        {
            return false;
        }

        var snapshot = _undo.Pop();
        _scenes = snapshot;
        await _store.SaveAllAsync(snapshot);
        ScenesChanged?.Invoke();
        return true;
    }

    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _scenes = _store.LoadAllAsync().GetAwaiter().GetResult().Select(CloneScene).ToList();
        _loaded = true;
    }

    private async Task MutateAsync(CancellationToken ct, Action<List<SceneSchedule>> mutate)
    {
        PushUndoSnapshot();
        mutate(_scenes);
        await _store.SaveAllAsync(_scenes);
        ScenesChanged?.Invoke();
        ct.ThrowIfCancellationRequested();
    }

    private void PushUndoSnapshot()
    {
        _undo.Push(_scenes.Select(CloneScene).ToList());
        while (_undo.Count > MaxUndoSteps)
        {
            _undo.PopLast();
        }
    }

    private IReadOnlyList<SceneSchedule> Snapshot() => _scenes.Select(CloneScene).ToList();

    private static int IndexOrThrow(IReadOnlyCollection<SceneSchedule> list, int index)
    {
        if (index < 0 || index >= list.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "场景索引越界。");
        }

        return index;
    }

    private static int IndexOrThrow(DataList<ScheduleItem> list, int index)
    {
        if (index < 0 || index >= list.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "镜头索引越界。");
        }

        return index;
    }

    internal static ScheduleItem CloneItem(ScheduleItem item) =>
        new(item.Key, item.Fix, new Note(new List<string>(item.Note.Objects), item.Note.Type, item.Note.Append));

    internal static SceneSchedule CloneScene(SceneSchedule scene) =>
        new(scene.Items.Select(CloneItem).ToList(), CloneItem(scene.Info));
}

/// <summary>Stack 扩展：弹出栈底（丢最旧快照）。</summary>
internal static class StackExtensions
{
    public static T PopLast<T>(this Stack<T> stack)
    {
        var bottom = stack.Last();
        // 重建栈去掉栈底（步骤少，拷贝代价可忽略）。
        var rest = stack.Reverse().Skip(1).Reverse().ToList();
        stack.Clear();
        foreach (var item in rest)
        {
            stack.Push(item);
        }

        return bottom;
    }
}