using LiteDB;
using VoiSlate.Models;

namespace VoiSlate.Services;

/// <summary>
/// LiteDB "scene_schedules" 集合的文档形态（由 <see cref="SeedService"/> 播种、
/// <see cref="LiteDbScheduleStore"/> 与 <c>LiteDbWorkspaceTransaction</c> 侧读写同一集合）。
///
/// <para>历史：本类型原先住在 <c>IScheduleBook.cs</c> 里，随 2026-10-26 删除 legacy 只读计划簿时独立出来。</para>
/// </summary>
public sealed class SceneScheduleDoc
{
    [BsonId]
    public string Key { get; set; } = string.Empty;
    public List<ScheduleItem> Items { get; set; } = [];
    public ScheduleItem Info { get; set; } = new();
}