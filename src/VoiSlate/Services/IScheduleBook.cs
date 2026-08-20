using LiteDB;
using VoiSlate.Models;

using VoiSlate.Infrastructure;
namespace VoiSlate.Services;

/// <summary>
/// 场次计划簿（P0.5 占位，演进权归 E 的 ScheduleService）。
/// 读取 LiteDB "scene_schedules"（SeedService 播种），提供场/镜标签与 objects 供 TakeFlowService 组合。
/// </summary>
public interface IScheduleBook
{
    int SceneCount { get; }
    SceneSchedule GetScene(int index);
    string SceneLabel(int index);
    string ShotLabel(int sceneIndex, int shotIndex);
    IReadOnlyList<string> ObjectsOf(int sceneIndex, int shotIndex);
    IReadOnlyList<string> AllSceneNames();
}

public sealed class SceneScheduleDoc
{
    [BsonId]
    public string Key { get; set; } = string.Empty;
    public List<ScheduleItem> Items { get; set; } = [];
    public ScheduleItem Info { get; set; } = new();
}

public sealed class LiteDbScheduleBook(LiteDbStore store) : IScheduleBook
{
    private ILiteCollection<SceneScheduleDoc> Collection => store.Database.GetCollection<SceneScheduleDoc>("scene_schedules");

    private readonly List<SceneScheduleDoc> _cache = [];

    private void EnsureLoaded()
    {
        if (_cache.Count == 0)
        {
            _cache.AddRange(Collection.FindAll().OrderBy(x => x.Key));
        }
    }

    public int SceneCount
    {
        get
        {
            EnsureLoaded();
            return _cache.Count;
        }
    }

    public SceneSchedule GetScene(int index)
    {
        EnsureLoaded();
        if (index < 0 || index >= _cache.Count)
        {
            index = 0;
        }

        var doc = _cache[index];
        return new SceneSchedule(doc.Items, doc.Info);
    }

    public string SceneLabel(int index) => GetScene(index).Info.Name;

    public string ShotLabel(int sceneIndex, int shotIndex)
    {
        var scene = GetScene(sceneIndex);
        if (shotIndex < 0 || shotIndex >= scene.Count)
        {
            shotIndex = 0;
        }

        return scene[shotIndex].Name;
    }

    public IReadOnlyList<string> ObjectsOf(int sceneIndex, int shotIndex)
    {
        var scene = GetScene(sceneIndex);
        if (shotIndex < 0 || shotIndex >= scene.Count)
        {
            return [];
        }

        return scene[shotIndex].Note.Objects;
    }

    public IReadOnlyList<string> AllSceneNames()
    {
        EnsureLoaded();
        return _cache.Select(x => x.Info.Name).ToList();
    }
}