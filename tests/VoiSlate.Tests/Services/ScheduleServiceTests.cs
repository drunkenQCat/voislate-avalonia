using VoiSlate.Models;
using VoiSlate.Services;
using VoiSlate.Tests.TestDoubles;
using Xunit;

namespace VoiSlate.Tests.Services;

/// <summary>计划簿用户数据服务（IScheduleService：增删改查/移动/CSV 全量替换/清空/undo + 至少 1 场 1 镜不变量）。</summary>
public class ScheduleServiceTests
{
    private readonly FakeScheduleStore _store = new();

    private static ScheduleItem Info(string key, string fix, string type = "万星园") =>
        new(key, fix, new Note(["麦克风"], type, "场景备注"));

    private static ScheduleItem Shot(string key, string fix, string append = "镜头备注") =>
        new(key, fix, new Note(["对象"], "近景", append));

    private static SceneSchedule Scene(string name, params ScheduleItem[] shots) =>
        new(shots.ToList(), Info(name[..1], name[1..]));

    private ScheduleService NewService() => new(_store);

    [Fact]
    public async Task LoadAll_Returns_Clones_And_Exposes_Read_Helpers()
    {
        await _store.SaveAllAsync([Scene("1A", Shot("1", "A")), Scene("2B", Shot("1", "A"))]);
        var svc = NewService();

        var loaded = await svc.LoadAllAsync(CancellationToken.None);

        Assert.Equal(2, loaded.Count);
        Assert.Equal(2, svc.SceneCount);
        Assert.Equal("1A", svc.SceneLabel(0));
        Assert.Equal("2B", svc.SceneLabel(1));
        Assert.Equal("1A", svc.ShotLabel(0, 0));
        Assert.Equal(["对象"], svc.ObjectsOf(0, 0));

        // 克隆：改返回值不影响服务内部
        loaded[0].Info.Key = "9";
        Assert.Equal("1A", svc.SceneLabel(0));
    }

    [Fact]
    public async Task AddScene_Persists_And_Rejects_Duplicate_Or_Empty_Scene()
    {
        var svc = NewService();
        await svc.AddSceneAsync(Scene("1A", Shot("1", "A")), CancellationToken.None);

        await Assert.ThrowsAsync<DuplicateItemException>(
            () => svc.AddSceneAsync(Scene("1A", Shot("1", "A")), CancellationToken.None));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.AddSceneAsync(Scene("2B"), CancellationToken.None)); // 空场（无镜头）

        Assert.Equal(1, svc.SceneCount);
        Assert.Single(_store.Scenes);
    }

    [Fact]
    public async Task AddShot_Rejects_Duplicate_Name_Within_Scene()
    {
        var svc = NewService();
        await svc.AddSceneAsync(Scene("1A", Shot("1", "A")), CancellationToken.None);

        await Assert.ThrowsAsync<DuplicateItemException>(
            () => svc.AddShotAsync(0, new ScheduleItem("1", "A", new Note([], "近景", "x")), CancellationToken.None));

        await svc.AddShotAsync(0, Shot("2", "B"), CancellationToken.None);
        Assert.Equal(2, svc.GetScene(0).Count);
    }

    [Fact]
    public async Task Edit_Scene_Info_And_Shot()
    {
        var svc = NewService();
        await svc.AddSceneAsync(Scene("1A", Shot("1", "A")), CancellationToken.None);

        await svc.EditSceneInfoAsync(0, Info("1", "A", "外景"), CancellationToken.None);
        await svc.EditShotAsync(0, 0, new ScheduleItem("1", "A", new Note(["新对象"], "特写", "新备注")), CancellationToken.None);

        Assert.Equal("外景", svc.GetScene(0).Info.Note.Type);
        Assert.Equal("特写", svc.GetScene(0)[0].Note.Type);
        Assert.Equal(["新对象"], svc.ObjectsOf(0, 0));

        // 改镜名为已存在 → 重复
        await svc.AddShotAsync(0, Shot("2", "B"), CancellationToken.None);
        await Assert.ThrowsAsync<DuplicateItemException>(
            () => svc.EditShotAsync(0, 0, Shot("2", "B"), CancellationToken.None));
    }

    [Fact]
    public async Task Delete_Guards_At_Least_One_Scene_And_One_Shot()
    {
        var svc = NewService();
        await svc.AddSceneAsync(Scene("1A", Shot("1", "A")), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.DeleteSceneAsync(0, CancellationToken.None)); // 最后一场不可删
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.DeleteShotAsync(0, 0, CancellationToken.None)); // 最后一镜不可删

        await svc.AddSceneAsync(Scene("2B", Shot("1", "A"), Shot("2", "B")), CancellationToken.None);

        await svc.DeleteSceneAsync(0, CancellationToken.None);
        Assert.Equal(1, svc.SceneCount);
        Assert.Equal("2B", svc.SceneLabel(0));

        await svc.DeleteShotAsync(0, 0, CancellationToken.None);
        Assert.Equal(1, svc.GetScene(0).Count);
        Assert.Equal("2B", svc.GetScene(0)[0].Name);
    }

    [Fact]
    public async Task Move_Scene_And_Shot_Reorder_And_Persist()
    {
        var svc = NewService();
        await svc.AddSceneAsync(Scene("1A", Shot("1", "A")), CancellationToken.None);
        await svc.AddSceneAsync(Scene("2B", Shot("1", "A"), Shot("2", "B")), CancellationToken.None);
        await svc.AddSceneAsync(Scene("3C", Shot("1", "A")), CancellationToken.None);

        await svc.MoveSceneAsync(0, 2, CancellationToken.None);
        var names = (await svc.LoadAllAsync(CancellationToken.None)).Select(s => s.Info.Name).ToArray();
        Assert.Equal(["2B", "3C", "1A"], names);

        await svc.MoveShotAsync(0, 0, 1, CancellationToken.None);
        Assert.Equal(["2B", "1A"], svc.GetScene(0).Items.Select(i => i.Name).ToArray());
        // 越界 toIndex 收敛到边界（from==to → no-op，状态不变）
        await svc.MoveShotAsync(0, 1, 99, CancellationToken.None);
        Assert.Equal(["2B", "1A"], svc.GetScene(0).Items.Select(i => i.Name).ToArray());
    }

    [Fact]
    public async Task ReplaceAll_Implements_Csv_Full_Rewrite_With_Guards()
    {
        var svc = NewService();
        await svc.AddSceneAsync(Scene("1A", Shot("1", "A")), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ReplaceAllAsync([], CancellationToken.None));               // 空计划
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ReplaceAllAsync([Scene("9Z")], CancellationToken.None));    // 含空场
        await Assert.ThrowsAsync<DuplicateItemException>(
            () => svc.ReplaceAllAsync([Scene("1A", Shot("1", "A")), Scene("1A", Shot("1", "A"))], CancellationToken.None));

        var csvResult = new List<SceneSchedule>
        {
            Scene("5E", Shot("1", "A"), Shot("2", "B")),
            Scene("6F", Shot("1", "A")),
        };
        await svc.ReplaceAllAsync(csvResult, CancellationToken.None);

        Assert.Equal(2, svc.SceneCount);
        Assert.Equal("5E", svc.SceneLabel(0));
    }

    [Fact]
    public async Task Clear_Empties_And_Undo_Restores_Previous_State()
    {
        var svc = NewService();
        await svc.AddSceneAsync(Scene("1A", Shot("1", "A")), CancellationToken.None);
        await svc.AddSceneAsync(Scene("2B", Shot("1", "A")), CancellationToken.None);

        await svc.ClearAsync(CancellationToken.None);
        Assert.Equal(0, svc.SceneCount);

        var undone = await svc.UndoAsync(CancellationToken.None);
        Assert.True(undone);
        Assert.Equal(2, svc.SceneCount);
        Assert.Equal(2, _store.Scenes.Count); // 已落库

        // 依次回滚到初始（空）→ 无可撤销项
        Assert.True(await svc.UndoAsync(CancellationToken.None)); // → [1A]
        Assert.True(await svc.UndoAsync(CancellationToken.None)); // → []
        Assert.False(await svc.UndoAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Undo_After_Delete_Scene_Restores_It()
    {
        var svc = NewService();
        await svc.AddSceneAsync(Scene("1A", Shot("1", "A")), CancellationToken.None);
        await svc.AddSceneAsync(Scene("2B", Shot("1", "A")), CancellationToken.None);

        await svc.DeleteSceneAsync(1, CancellationToken.None);
        Assert.Equal(1, svc.SceneCount);

        Assert.True(await svc.UndoAsync(CancellationToken.None));
        Assert.Equal(2, svc.SceneCount);
        Assert.Equal("2B", svc.SceneLabel(1));
    }

    [Fact]
    public async Task ScenesChanged_Fires_On_Each_Mutation_Not_On_Reads()
    {
        var svc = NewService();
        var changes = 0;
        svc.ScenesChanged += () => changes++;

        await svc.AddSceneAsync(Scene("1A", Shot("1", "A")), CancellationToken.None);
        _ = svc.SceneCount;
        await svc.AddShotAsync(0, Shot("2", "B"), CancellationToken.None);
        await svc.DeleteShotAsync(0, 1, CancellationToken.None);

        Assert.Equal(3, changes);
    }
}