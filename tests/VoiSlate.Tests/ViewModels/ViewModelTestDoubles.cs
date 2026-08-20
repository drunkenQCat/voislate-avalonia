using VoiSlate.Models;
using VoiSlate.Services;

namespace VoiSlate.Tests.VM;

/// <summary>
/// B 新增 VM 测试专用桩（B 自产接口：IHardwareKeyService/IExportService/IScheduleStore/ICsvScheduleParser。
/// 归 E 演进；P0.5 TestDoubles.cs 既有 Fake 不动——本文件独立，不属 E 资产）。
/// </summary>
public sealed class TestHardwareKeyService : IHardwareKeyService
{
    public void Raise(HardwareKey key) => KeyPressed?.Invoke(key);

    public event Action<HardwareKey>? KeyPressed;
}

/// <summary>内存计划仓（记录 SaveAll 快照 + 可配 Load 结果）。</summary>
public sealed class StubScheduleStore : IScheduleStore
{
    public List<SceneSchedule> Saved { get; } = [];

    public IReadOnlyList<SceneSchedule>? LoadResult { get; set; }

    public Task<IReadOnlyList<SceneSchedule>> LoadAllAsync()
        => Task.FromResult<IReadOnlyList<SceneSchedule>>(LoadResult ?? []);

    public Task SaveAllAsync(IReadOnlyList<SceneSchedule> schedules)
    {
        Saved.Clear();
        Saved.AddRange(schedules);
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        Saved.Clear();
        return Task.CompletedTask;
    }
}

/// <summary>CSV 解析桩（返回可配结果）。</summary>
public sealed class StubCsvScheduleParser : ICsvScheduleParser
{
    public IReadOnlyList<SceneSchedule>? Result { get; set; }

    public bool Called { get; private set; }

    public Task<IReadOnlyList<SceneSchedule>> ParseAsync(Stream stream, CancellationToken ct)
    {
        Called = true;
        return Task.FromResult(Result ?? []);
    }
}

/// <summary>导出间谍（捕获 Serialize 输入与落盘参数）。</summary>
public sealed class SpyExportService : IExportService
{
    public IReadOnlyList<SlateLogItem>? LastLogs { get; private set; }

    public string? LastDir { get; private set; }

    public string? LastName { get; private set; }

    public string? LastContent { get; private set; }

    public string SerializeLogs(IEnumerable<SlateLogItem> logs)
    {
        LastLogs = logs.ToList();
        return "[]";
    }

    public Task SaveToFileAsync(string dir, string name, string content)
    {
        LastDir = dir;
        LastName = name;
        LastContent = content;
        return Task.CompletedTask;
    }
}

/// <summary>计划构造助手：场(itemKey, fix, name, note) + 镜列表。</summary>
public static class ScheduleFactory
{
    public static ScheduleItem Scn(string key = "1", string fix = "A", string? note = null)
        => new(key, fix, new Note([], "日戏", note ?? string.Empty));

    public static ScheduleItem Sht(string key = "1", string fix = "", string? note = null)
        => new(key, fix, new Note(["Boom"], "近景", note ?? string.Empty));
}

/// <summary>只读计划簿桩（P0.5 未产 Fake；仅测试用，E 演进真实实现）。</summary>
public sealed class StubScheduleBook : IScheduleBook
{
    public SceneSchedule Scene { get; set; } =
        new([ScheduleFactory.Sht("1", "", "")], ScheduleFactory.Scn("1", "A", ""));

    public int SceneCount => 1;

    public SceneSchedule GetScene(int index) => Scene;

    public string SceneLabel(int index) => Scene.Info.Name;

    public string ShotLabel(int sceneIndex, int shotIndex)
        => Scene.Count > 0 ? Scene.Items[Math.Min(shotIndex, Scene.Count - 1)].Name : string.Empty;

    public IReadOnlyList<string> ObjectsOf(int sceneIndex, int shotIndex)
        => Scene.Count > 0 && Scene.Items[Math.Min(shotIndex, Scene.Count - 1)].Note.Objects.Count > 0
            ? Scene.Items[Math.Min(shotIndex, Scene.Count - 1)].Note.Objects
            : [];

    public IReadOnlyList<string> AllSceneNames() => [Scene.Info.Name];
}