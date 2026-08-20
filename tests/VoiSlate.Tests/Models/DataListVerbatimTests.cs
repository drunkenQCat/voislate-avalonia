using VoiSlate.Models;
using Xunit;

namespace VoiSlate.Tests.Models;

/// <summary>
/// DataList/SceneSchedule 集合语义测试（A 拥模型 fixture；契约 §2：增/插/改/索引赋值均按 name 查重，
/// 先查重再赋值；SceneSchedule 索引器复用基类校验）。
/// </summary>
public class DataListVerbatimTests
{
    [Fact]
    public void Null_Constructor_Yields_Empty_List()
    {
        // 原版 DataList(null) → _data 保持空（构造函数守卫）。
        var list = new DataList<ScheduleItem>(null);
        Assert.Empty(list.Items);
        Assert.Equal(0, list.Count);
        Assert.Equal(0, list.Length);
    }

    [Fact]
    public void Constructor_Detects_Duplicates_Among_Items()
    {
        var shots = new List<ScheduleItem>
        {
            new("1", "A", new Note()),
            new("1", "A", new Note()),
        };
        Assert.Throws<DuplicateItemException>(() => new DataList<ScheduleItem>(shots));
    }

    [Fact]
    public void Add_Appends_And_Insert_Places_At_Index()
    {
        var list = new DataList<ScheduleItem>();
        var a = new ScheduleItem("1", "A", new Note());
        var b = new ScheduleItem("2", "B", new Note());
        var c = new ScheduleItem("3", "C", new Note());

        list.Add(a);
        list.Add(c);
        list.Insert(1, b);

        Assert.Equal(["1A", "2B", "3C"], list.Items.Select(x => x.Name).ToList());
    }

    [Fact]
    public void Indexer_Set_Validates_Duplicates_Then_Assigns()
    {
        var list = new DataList<ScheduleItem>
        {
            new("1", "A", new Note()),
            new("2", "B", new Note()),
        };

        // 与既有项重名 → 抛（契约修复：set 也走校验，不改原值）。
        Assert.Throws<DuplicateItemException>(() => list[0] = new ScheduleItem("2", "B", new Note()));
        Assert.Equal("1A", list[0].Name); // 原值未被破坏

        // 合法赋值生效。
        var replacement = new ScheduleItem("9", "Z", new Note());
        list[0] = replacement;
        Assert.Same(replacement, list[0]);
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void Update_Replaces_At_OldIndex_And_Validates()
    {
        var list = new DataList<ScheduleItem>
        {
            new("1", "A", new Note()),
            new("2", "B", new Note()),
        };

        // oldIndex 自身不参与查重（原地替换），但不得与其余项重名。
        list.Update(0, new ScheduleItem("1", "A", new Note()));
        Assert.Equal("1A", list[0].Name);
        Assert.Throws<DuplicateItemException>(() => list.Update(0, new ScheduleItem("2", "B", new Note())));
    }

    [Fact]
    public void Remove_RemoveAt_Behave_Like_Original()
    {
        var a = new ScheduleItem("1", "A", new Note());
        var b = new ScheduleItem("2", "B", new Note());
        var c = new ScheduleItem("3", "C", new Note());
        var list = new DataList<ScheduleItem> { a, b, c };

        Assert.True(list.Remove(b)); // 引用删除（原版 _data.remove）
        Assert.False(list.Remove(b));
        Assert.Equal(2, list.Count);

        var removed = list.RemoveAt(0);
        Assert.Same(a, removed);
        Assert.Equal(["3C"], list.Items.Select(x => x.Name).ToList());
    }

    [Fact]
    public void SceneSchedule_Indexer_Get_And_Set_Flow_Through_Base_Validation()
    {
        var a = new ScheduleItem("1", "A", new Note());
        var b = new ScheduleItem("2", "B", new Note());
        var info = new ScheduleItem("S", "1", new Note());
        var scene = new SceneSchedule([a, b], info);

        // [] 读取走基类索引器。
        Assert.Same(a, scene[0]);
        Assert.Same(b, scene[1]);

        // [] 赋值：重名抛、合法生效（契约修复：set 也走校验）。
        Assert.Throws<DuplicateItemException>(() => scene[0] = new ScheduleItem("2", "B", new Note()));
        var c = new ScheduleItem("3", "C", new Note());
        scene[0] = c;
        Assert.Same(c, scene[0]);
        Assert.Equal(2, scene.Count); // 索引赋值是替换，不改变长度

        // Info 独立于镜头列表。
        Assert.Same(info, scene.Info);
        Assert.Equal("S1", scene.Info.Name);
    }

    [Fact]
    public void DuplicateItemException_Carries_Original_Message()
    {
        var ex = Assert.Throws<DuplicateItemException>(() =>
        {
            var list = new DataList<ScheduleItem> { new("1", "A", new Note()) };
            list.Add(new ScheduleItem("1", "A", new Note()));
        });
        // 原版 throw DuplicateItemException('Duplicate items in the list')；
        // Message 即原文字符串（C# 基类 ToString 会加类型前缀，展示建议用 Message）。
        Assert.Equal("Duplicate items in the list", ex.Message);
    }
}