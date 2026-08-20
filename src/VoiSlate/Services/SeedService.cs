using LiteDB;
using VoiSlate.Data;
using VoiSlate.Models;

using VoiSlate.Infrastructure;
namespace VoiSlate.Services;

/// <summary>
/// 播种（契约 ADR-008 启动序第 2 步；空库播种两份生产场表）。
/// </summary>
public interface ISeedService
{
    Task EnsureSeededAsync(CancellationToken ct);
}

public sealed class SeedService(LiteDbStore store) : ISeedService
{
    public Task EnsureSeededAsync(CancellationToken ct)
    {
        var collection = store.Database.GetCollection<SceneScheduleDoc>("scene_schedules");
        if (collection.Count() > 0)
        {
            return Task.CompletedTask;
        }

        var s1 = SeedData.SceneSchedule1A();
        var s2 = SeedData.SceneSchedule2A();
        collection.Insert(new SceneScheduleDoc { Key = "1A", Items = s1.Items.ToList(), Info = s1.Info });
        collection.Insert(new SceneScheduleDoc { Key = "2A", Items = s2.Items.ToList(), Info = s2.Info });
        return Task.CompletedTask;
    }
}