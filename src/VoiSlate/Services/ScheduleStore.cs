using LiteDB;
using VoiSlate.Infrastructure;
using VoiSlate.Models;

namespace VoiSlate.Services;

/// <summary>
/// 计划存储（契约 §3 ScheduleStore；LiteDB "scene_schedules" 集合，与 SeedService/LiteDbScheduleBook 同集合兼容）。
/// - SaveAllAsync 全量重写（对齐原版计划页 CSV 导入全量重写 / 数据整体保存）。
/// - 顺序保持：内部以 Seq 编号记录插入序（原版 Hive List 语义）；LiteDbScheduleBook 仍按 Key 字符串排序
///   （既有实现缺陷：如 "10A" 会排在 "2A" 前——见报告缺口，建议下次演进让 ScheduleBook 按 Seq 排）。
/// - 与 SeedService（SceneScheduleDoc：Key/Items/Info）集合兼容：本实现写入的文档带额外 Seq 字段，
///   LiteDB BsonMapper 读取时忽略未知字段；反向读取亦然（Seq 缺省 0，回退按 Key 排序）。
/// </summary>
public interface ScheduleStore
{
    Task<IReadOnlyList<SceneSchedule>> LoadAllAsync();
    Task SaveAllAsync(IReadOnlyList<SceneSchedule> scenes);
    Task ClearAsync();
}

public sealed class LiteDbScheduleStore(LiteDbStore store) : ScheduleStore
{
    private sealed class ScheduleDoc
    {
        [BsonId]
        public string Key { get; set; } = string.Empty;

        /// <summary>插入序号（保存序）；读侧排序依据。</summary>
        public int Seq { get; set; }

        public List<ScheduleItem> Items { get; set; } = [];
        public ScheduleItem Info { get; set; } = new();
    }

    private ILiteCollection<ScheduleDoc> Collection => store.Database.GetCollection<ScheduleDoc>("scene_schedules");

    public Task<IReadOnlyList<SceneSchedule>> LoadAllAsync()
    {
        var scenes = Collection.FindAll()
            .OrderBy(x => x.Seq)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .Select(d => new SceneSchedule(d.Items, d.Info))
            .ToList();
        return Task.FromResult<IReadOnlyList<SceneSchedule>>(scenes);
    }

    public Task SaveAllAsync(IReadOnlyList<SceneSchedule> scenes)
    {
        Collection.DeleteAll();
        for (var i = 0; i < scenes.Count; i++)
        {
            var scene = scenes[i];
            var key = string.IsNullOrEmpty(scene.Info.Name) ? $"scene-{i}" : scene.Info.Name;
            Collection.Insert(new ScheduleDoc
            {
                Key = key,
                Seq = i,
                Items = scene.Items.ToList(),
                Info = scene.Info,
            });
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        Collection.DeleteAll();
        return Task.CompletedTask;
    }
}