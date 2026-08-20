using VoiSlate.Infrastructure;
using VoiSlate.Models;
using VoiSlate.Services;
using Xunit;

namespace VoiSlate.Tests;

/// <summary>LiteDB 持久化层测试（:memory:；枚举数值语义 + 插入序 + 日期分箱）。</summary>
public class LiteDbRepositoryTests
{
    private static LiteDbStore NewStore() => new(":memory:");

    private static SlateLogItem NewItem(int tk, int num, TkStatus okTk = TkStatus.Ok, ShtStatus okSht = ShtStatus.Ok) =>
        new()
        {
            Scn = "1",
            Sht = "A",
            Tk = tk,
            FilenamePrefix = "260820",
            FilenameLinker = "-T",
            FilenameNum = num,
            TkNote = $"Tk {tk}",
            ShtNote = "note",
            ScnNote = "scene",
            OkTk = okTk,
            OkSht = okSht,
        };

    [Fact]
    public async Task Logs_RoundTrip_By_Date_Preserving_Insertion_Order_And_Enums()
    {
        using var store = NewStore();
        var repo = new LiteDbLogRepository(store);

        await repo.AddAsync("260820", "k1", NewItem(1, 1, TkStatus.NotChecked, ShtStatus.Nice));
        await repo.AddAsync("260820", "k2", NewItem(2, 2, TkStatus.Bad, ShtStatus.NotChecked));
        await repo.AddAsync("260819", "k3", NewItem(9, 1));

        var dates = await repo.GetDatesAsync();
        Assert.Equal(["260819", "260820"], dates);

        var today = await repo.GetByDateAsync("260820");
        Assert.Equal(2, today.Count);
        Assert.Equal("260820-T001", today[0].FileName);          // 插入序保持
        Assert.Equal("Tk 1", today[0].TkNote);
        Assert.Equal(TkStatus.NotChecked, today[0].OkTk);
        Assert.Equal(ShtStatus.Nice, today[0].OkSht);
        Assert.Equal(TkStatus.Bad, today[1].OkTk);
        Assert.Equal(2, today[1].Tk);
    }

    [Fact]
    public async Task Logs_Replace_RemoveAt_RemoveLast_RemoveByKey()
    {
        using var store = NewStore();
        var repo = new LiteDbLogRepository(store);

        await repo.AddAsync("260820", "k1", NewItem(1, 1));
        await repo.AddAsync("260820", "k2", NewItem(2, 2));
        await repo.AddAsync("260820", "k3", NewItem(3, 3));

        var edited = NewItem(11, 1);
        edited.TkNote = "edited";
        await repo.ReplaceAtAsync("260820", 0, edited);
        var afterEdit = await repo.GetByDateAsync("260820");
        Assert.Equal("edited", afterEdit[0].TkNote);
        Assert.Equal(11, afterEdit[0].Tk);

        var removed = await repo.RemoveAtAsync("260820", 1);
        Assert.Equal(2, removed.Tk);
        Assert.Equal(2, (await repo.GetByDateAsync("260820")).Count);

        var last = await repo.RemoveLastAsync("260820");
        Assert.Equal(3, last.Tk);
        Assert.Single(await repo.GetByDateAsync("260820"));

        await repo.RemoveByKeyAsync("260820", "k1");
        Assert.Empty(await repo.GetByDateAsync("260820"));
    }

    [Fact]
    public async Task PickerHistory_RoundTrip()
    {
        using var store = NewStore();
        var history = new LiteDbPickerHistoryStore(store);

        Assert.Empty(await history.GetLastAsync());
        await history.AddAsync(["1A", "1A", "1", "缪尔赛斯"]);
        await history.AddAsync(["1A", "1A", "2", "塞雷娅"]);

        Assert.Equal(2, await history.CountAsync());
        Assert.Equal(["1A", "1A", "2", "塞雷娅"], await history.GetLastAsync());

        await history.RemoveLastAsync();
        Assert.Equal(["1A", "1A", "1", "缪尔赛斯"], await history.GetLastAsync());

        await history.ClearAsync();
        Assert.Empty(await history.GetLastAsync());
    }

    [Fact]
    public async Task Settings_RoundTrip_Typed()
    {
        using var store = NewStore();
        var settings = new LiteDbSessionSettingsStore(store);

        Assert.Equal(0, await settings.GetIntAsync("absent", 0));
        Assert.Equal("d", await settings.GetStringAsync("absent", "d"));
        Assert.True(await settings.GetBoolAsync("absent", true));

        await settings.SetAsync("count", 42);
        await settings.SetAsync("linker", "-T");
        await settings.SetAsync("linked", false);

        Assert.Equal(42, await settings.GetIntAsync("count", 0));
        Assert.Equal("-T", await settings.GetStringAsync("linker", ""));
        Assert.False(await settings.GetBoolAsync("linked", true));
    }

    [Fact]
    public async Task Seed_Inserts_Two_Scenes_Once()
    {
        using var store = NewStore();
        var seed = new SeedService(store);
        await seed.EnsureSeededAsync(CancellationToken.None);
        await seed.EnsureSeededAsync(CancellationToken.None); // 幂等

        var book = new LiteDbScheduleBook(store);
        Assert.Equal(2, book.SceneCount);
        Assert.Equal("1A", book.SceneLabel(0));
        Assert.Equal("2A", book.SceneLabel(1));
        Assert.Equal(3, book.GetScene(0).Count);
        Assert.Equal("1A", book.ShotLabel(0, 0));
        Assert.Equal(["缪尔赛斯", "塞雷娅"], book.ObjectsOf(0, 0));
        Assert.Equal("万星园", book.GetScene(0).Info.Note.Type);
        Assert.Equal("三人会面，缪尔赛斯提出了她的计划，塞雷娅和克里斯滕都表示了支持。",
            book.GetScene(0).Info.Note.Append);
    }

    [Fact]
    public void MockAsr_Is_Available_And_Toggles()
    {
        var asr = new MockAsrService();
        Assert.True(asr.IsAvailable);
        Assert.False(asr.IsListening);
        asr.Start();
        Assert.True(asr.IsListening);
        asr.Stop();
        Assert.False(asr.IsListening);
    }
}