using VoiSlate.Models;

namespace VoiSlate.Services;

/// <summary>
/// 场次计划持久化（契约 v0.5 §3 ScheduleStore；LiteDB "scenes" 集合；ScheduleViewModel 唯一写入口）。
/// P0.5 未产 → B 补桩，演进权归 E。
/// </summary>
public interface IScheduleStore
{
    Task<IReadOnlyList<SceneSchedule>> LoadAllAsync();

    Task SaveAllAsync(IReadOnlyList<SceneSchedule> schedules);

    Task ClearAsync();
}

/// <summary>B 补桩（演进权归 E）：不落库、Load 返回空。</summary>
public sealed class NoopScheduleStore : IScheduleStore
{
    public Task<IReadOnlyList<SceneSchedule>> LoadAllAsync()
        => Task.FromResult<IReadOnlyList<SceneSchedule>>([]);

    public Task SaveAllAsync(IReadOnlyList<SceneSchedule> schedules) => Task.CompletedTask;

    public Task ClearAsync() => Task.CompletedTask;
}