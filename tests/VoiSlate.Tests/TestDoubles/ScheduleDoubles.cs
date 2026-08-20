using VoiSlate.Models;

namespace VoiSlate.Tests.TestDoubles;

/// <summary>可变时间源（E 新增：DayRollover 周期跨天测试需要时间可推进；既有 FakeTimeProvider 时间固定）。</summary>
public sealed class MutableFakeTimeProvider : VoiSlate.Services.ITimeProvider
{
    public DateTime Now { get; set; } = new(2026, 8, 20, 12, 0, 0);
}

/// <summary>内存计划存储（E 新增 Fake，供 ScheduleService 单测；语义对齐 ScheduleStore：全量重写 + 顺序保存）。</summary>
public sealed class FakeScheduleStore : VoiSlate.Services.ScheduleStore
{
    private List<SceneSchedule> _scenes = [];

    public IReadOnlyList<SceneSchedule> Scenes => _scenes;

    public Task<IReadOnlyList<SceneSchedule>> LoadAllAsync() =>
        Task.FromResult<IReadOnlyList<SceneSchedule>>(_scenes.Select(CloneScene).ToList());

    public Task SaveAllAsync(IReadOnlyList<SceneSchedule> scenes)
    {
        _scenes = scenes.Select(CloneScene).ToList();
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        _scenes = [];
        return Task.CompletedTask;
    }

    internal static ScheduleItem CloneItem(ScheduleItem item) =>
        new(item.Key, item.Fix, new Note(new List<string>(item.Note.Objects), item.Note.Type, item.Note.Append));

    internal static SceneSchedule CloneScene(SceneSchedule scene) =>
        new(scene.Items.Select(CloneItem).ToList(), CloneItem(scene.Info));
}