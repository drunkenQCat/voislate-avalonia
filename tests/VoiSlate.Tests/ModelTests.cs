using System.Text.Json;
using VoiSlate.Models;
using VoiSlate.Services;
using VoiSlate.Tests.TestDoubles;
using Xunit;

namespace VoiSlate.Tests;

public class SlateLogItemTests
{
    [Fact]
    public void FileName_Computes_With_Padded_Number()
    {
        var item = NewItem(filenameNum: 7);
        Assert.Equal("230522-T007", item.FileName);
    }

    [Fact]
    public void Json_RoundTrip_Uses_CamelCase_And_Short_Enum_Names()
    {
        var item = NewItem();
        item.OkTk = TkStatus.Bad;
        item.OkSht = ShtStatus.Nice;

        var json = JsonSerializer.Serialize(item, VoiSlateJson.Options);
        Assert.DoesNotContain("Id", json);
        Assert.DoesNotContain("FileName", json);
        Assert.Contains("\"okTk\":\"bad\"", json);   // A#16：camelCase 短名而非 "Bad"
        Assert.Contains("\"okSht\":\"nice\"", json);
        Assert.Contains("\"filenamePrefix\":\"230522\"", json);

        var back = JsonSerializer.Deserialize<SlateLogItem>(json, VoiSlateJson.Options);
        Assert.NotNull(back);
        Assert.Equal(TkStatus.Bad, back!.OkTk);
        Assert.Equal(ShtStatus.Nice, back.OkSht);
        Assert.Equal(3, back.Tk);
    }

    [Fact]
    public void Enum_Numeric_Values_Match_Original()
    {
        Assert.Equal(0, (int)TkStatus.NotChecked);
        Assert.Equal(1, (int)TkStatus.Ok);
        Assert.Equal(2, (int)TkStatus.Bad);
        Assert.Equal(0, (int)ShtStatus.NotChecked);
        Assert.Equal(1, (int)ShtStatus.Ok);
        Assert.Equal(2, (int)ShtStatus.Nice);
    }

    private static SlateLogItem NewItem(int filenameNum = 3) => new()
    {
        Scn = "1",
        Sht = "A",
        Tk = 3,
        FilenamePrefix = "230522",
        FilenameLinker = "-T",
        FilenameNum = filenameNum,
        TkNote = "S1 ShA Tk3",
        ShtNote = "note",
        ScnNote = "scene note",
        OkTk = TkStatus.Ok,
        OkSht = ShtStatus.Ok,
    };
}

public class SlateScheduleTests
{
    [Fact]
    public void Duplicate_Name_Throws_On_Add()
    {
        var list = new DataList<ScheduleItem>
        {
            new("1", "A", new Note()),
        };
        Assert.Throws<DuplicateItemException>(() => list.Add(new ScheduleItem("1", "A", new Note())));
        Assert.Throws<DuplicateItemException>(() => list.Insert(0, new ScheduleItem("1", "A", new Note())));
    }

    [Fact]
    public void Duplicate_Name_Throws_On_Update_And_Indexer_Set()
    {
        var list = new DataList<ScheduleItem>
        {
            new("1", "A", new Note()),
            new("2", "B", new Note()),
        };
        Assert.Throws<DuplicateItemException>(() => list.Update(0, new ScheduleItem("2", "B", new Note())));
        Assert.Throws<DuplicateItemException>(() => list[0] = new ScheduleItem("2", "B", new Note()));
    }

    [Fact]
    public void Duplicate_Among_Constructor_Items_Throws()
    {
        var shots = new List<ScheduleItem>
        {
            new("1", "A", new Note()),
            new("1", "A", new Note()),
        };
        Assert.Throws<DuplicateItemException>(() => new SceneSchedule(shots, new ScheduleItem("S", "1", new Note())));
    }

    [Fact]
    public void Name_Is_Key_Plus_Fix_And_Recomputes()
    {
        var item = new ScheduleItem("1", "A", new Note());
        Assert.Equal("1A", item.Name);
        item.Key = "9";
        item.Fix = "Z";
        Assert.Equal("9Z", item.Name);
    }
}

public class FileNumberingServiceTests
{
    [Fact]
    public void Starts_At_One_And_Increments()
    {
        var svc = new FileNumberingService(new FakeTimeProvider());
        Assert.Equal(1, svc.Number);
        svc.Increment();
        Assert.Equal(2, svc.Number);
        svc.Increment();
        Assert.Equal(3, svc.Number);
    }

    [Fact]
    public void Decrement_Floor_At_One_No_Event()
    {
        var svc = new FileNumberingService(new FakeTimeProvider());
        var events = 0;
        svc.NumberChanged += _ => events++;
        Assert.Equal(1, svc.Decrement());
        Assert.Equal(1, svc.Number);
        Assert.Equal(0, events);
    }

    [Fact]
    public void PrevFileName_Empty_At_One_Then_Correct()
    {
        var svc = new FileNumberingService(new FakeTimeProvider());
        Assert.Equal(string.Empty, svc.PrevFileName());
        svc.Increment();
        Assert.Equal("260820-T001", svc.PrevFileName());
        Assert.Equal(1, svc.PrevFileNum());
    }

    [Fact]
    public void Prefix_Modes_Compute_Like_Original()
    {
        var time = new FakeTimeProvider(new DateTime(2026, 8, 20, 12, 0, 0));
        var svc = new FileNumberingService(time);
        Assert.Equal("260820", svc.Prefix);                       // default → yyMMdd
        svc.PrefixMode = PrefixType.SoundDevices;
        Assert.Equal("26Y08M20", svc.Prefix);                     // sound devices
        svc.PrefixMode = PrefixType.Custom;
        svc.CustomPrefix = "custom";
        Assert.Equal("custom", svc.Prefix);                       // custom
    }

    [Fact]
    public void FullName_Pads_Number_To_Three()
    {
        var svc = new FileNumberingService(new FakeTimeProvider());
        svc.Increment(); // number 2
        Assert.Equal("260820-T002", svc.FullName());
        svc.SetValue(123);
        Assert.Equal("260820-T123", svc.FullName());
    }
}