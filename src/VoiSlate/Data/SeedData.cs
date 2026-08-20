using VoiSlate.Models;

namespace VoiSlate.Data;

/// <summary>
/// 生产种子数据（对齐原版 data/dummy_data.dart 两份场表 + 示例场记）。
/// P0.5 产出，所有权随 SeedService 移交 E（契约 C-3）。
/// </summary>
public static class SeedData
{
    public static SceneSchedule SceneSchedule1A()
    {
        var note = new Note(["缪尔赛斯", "塞雷娅", "克里斯滕"], "万星园",
            "三人会面，缪尔赛斯提出了她的计划，塞雷娅和克里斯滕都表示了支持。");
        var info = new ScheduleItem("1", "A", note);
        var shots = new List<ScheduleItem>
        {
            new("1", "A", new Note(["缪尔赛斯", "塞雷娅"], "近景", "小插曲")),
            new("2", "B", new Note(["克里斯滕", "塞雷娅"], "特写", "两人对峙")),
            new("3", "C", new Note(["缪尔赛斯", "塞雷娅"], "中景", "缪尔赛斯向塞雷娅介绍生态园")),
        };
        return new SceneSchedule(shots, info);
    }

    public static SceneSchedule SceneSchedule2A()
    {
        var info = new ScheduleItem("2", "A", new Note(["Dr", "凯尔希", "迷迭香"], "洛肯实验室", "三人准备准备会面洛肯"));
        var shots = new List<ScheduleItem>
        {
            new("1", "A", new Note(["缪尔赛斯", "塞雷娅"], "近景", "小插曲")),
            new("2", "B", new Note(["克里斯滕", "塞雷娅"], "特写", "两人对峙")),
            new("3", "C", new Note(["缪尔赛斯", "塞雷娅"], "中景", "缪尔赛斯向塞雷娅介绍生态园")),
        };
        return new SceneSchedule(shots, info);
    }
}