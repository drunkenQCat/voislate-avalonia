using VoiSlate.Infrastructure;
using VoiSlate.Models;
using VoiSlate.Services;
using Xunit;

namespace VoiSlate.Tests.Services;

/// <summary>计划存储（契约 §3 ScheduleStore；插入序保持 + 与 SeedService/LiteDbScheduleBook 集合兼容）。</summary>
public class ScheduleStoreTests
{
    private const string CollectionName = "scene_schedules";

    private static LiteDbStore NewStore() => new(":memory:");

    private static ScheduleItem Info(string key, string fix) =>
        new(key, fix, new Note(["麦克风"], "万星园", "场景备注"));

    private static ScheduleItem Shot(string key, string fix) =>
        new(key, fix, new Note(["对象"], "近景", "镜头备注"));

    private static SceneSchedule Scene(string name, params ScheduleItem[] shots) =>
        new(shots.ToList(), Info(name[..1], name[1..]));

    [Fact]
    public async Task SaveAll_LoadAll_Preserves_Insertion_Order()
    {
        using var store = NewStore();
        var repo = new LiteDbScheduleStore(store);

        var scenes = new List<SceneSchedule>
        {
            Scene("1A", Shot("1", "A"), Shot("2", "B")),
            Scene("2B", Shot("1", "A")),
            Scene("10C", Shot("1", "A")), // 数字序 10 应保持在 2 之后（Seq，而非 Key 字符串序）
        };
        await repo.SaveAllAsync(scenes);

        var loaded = await repo.LoadAllAsync();
        Assert.Equal(["1A", "2B", "10C"], loaded.Select(s => s.Info.Name).ToArray());
        Assert.Equal(2, loaded[0].Count);
        Assert.Equal("镜头备注", loaded[0][0].Note.Append);
    }

    [Fact]
    public async Task Clear_Empties_The_Collection()
    {
        using var store = NewStore();
        var repo = new LiteDbScheduleStore(store);
        await repo.SaveAllAsync([Scene("1A", Shot("1", "A"))]);

        await repo.ClearAsync();

        Assert.Empty(await repo.LoadAllAsync());
    }

    [Fact]
    public async Task Reads_SeedService_Seeded_Scenes()
    {
        using var store = NewStore();
        await new SeedService(store).EnsureSeededAsync(CancellationToken.None);

        var loaded = await new LiteDbScheduleStore(store).LoadAllAsync();

        Assert.Equal(2, loaded.Count);
        Assert.Equal("1A", loaded[0].Info.Name); // 种子无 Seq → 回退按 Key 排序
        Assert.Equal("2A", loaded[1].Info.Name);
        Assert.Equal("万星园", loaded[0].Info.Note.Type);
    }

    [Fact]
    public async Task Writes_Are_Readable_By_LiteDbScheduleBook()
    {
        using var store = NewStore();
        await new LiteDbScheduleStore(store).SaveAllAsync([Scene("7A", Shot("1", "A"))]);

        // 既有只读 ScheduleBook 读同一集合（跨类读写兼容）
        var book = new LiteDbScheduleBook(store);
        Assert.Equal(1, book.SceneCount);
        Assert.Equal("7A", book.SceneLabel(0));
    }
}