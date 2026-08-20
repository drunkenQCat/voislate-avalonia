using System.Text.Json;
using VoiSlate.Models;
using Xunit;

namespace VoiSlate.Tests.Models;

/// <summary>
/// SlateLogItem 哨兵值 JSON 语义测试（A 拥模型 fixture；契约 ADR-005/B4：
/// fake 条 tk=999+okTk=bad+tkNote='Fake Take'、wild 条 tk=0+'wild track ' 前缀；
/// 导出格式无 Id/FileName、camelCase 属性 + 短名枚举）。
/// </summary>
public class SlateLogItemSentinelTests
{
    private static SlateLogItem NewItem(int tk = 3, string tkNote = "S1 ShA Tk3") => new()
    {
        Scn = "1",
        Sht = "A",
        Tk = tk,
        FilenamePrefix = "260820",
        FilenameLinker = "-T",
        FilenameNum = 2,
        TkNote = tkNote,
        ShtNote = "Boom note",
        ScnNote = "scene note",
        OkTk = TkStatus.Ok,
        OkSht = ShtStatus.Ok,
    };

    [Fact]
    public void Fake_Take_Sentinels_RoundTrip_Verbatim()
    {
        var item = NewItem(tk: 999, tkNote: "Fake Take");
        item.OkTk = TkStatus.Bad;

        var json = JsonSerializer.Serialize(item, VoiSlateJson.Options);
        Assert.Contains("\"tk\":999", json);
        Assert.Contains("\"okTk\":\"bad\"", json);
        Assert.Contains("\"tkNote\":\"Fake Take\"", json);

        var back = JsonSerializer.Deserialize<SlateLogItem>(json, VoiSlateJson.Options);
        Assert.NotNull(back);
        Assert.Equal(999, back!.Tk);
        Assert.Equal(TkStatus.Bad, back.OkTk);
        Assert.Equal("Fake Take", back.TkNote);
    }

    [Fact]
    public void Wild_Take_Sentinels_RoundTrip_Verbatim()
    {
        var item = NewItem(tk: 0, tkNote: "wild track S1 ShA Tk3");

        var json = JsonSerializer.Serialize(item, VoiSlateJson.Options);
        Assert.Contains("\"tk\":0", json);
        Assert.Contains("\"tkNote\":\"wild track S1 ShA Tk3\"", json);

        var back = JsonSerializer.Deserialize<SlateLogItem>(json, VoiSlateJson.Options);
        Assert.NotNull(back);
        Assert.Equal(0, back!.Tk);
        Assert.StartsWith("wild track ", back.TkNote);
    }

    [Fact]
    public void Serialized_Export_Contains_No_Id_Or_FileName()
    {
        var json = JsonSerializer.Serialize(NewItem(), VoiSlateJson.Options);
        Assert.DoesNotContain("\"id\"", json);
        Assert.DoesNotContain("\"fileName\"", json);
        Assert.DoesNotContain("\"Id\"", json);
        Assert.DoesNotContain("\"FileName\"", json);
    }

    [Fact]
    public void Export_Property_Names_Are_CamelCase()
    {
        var json = JsonSerializer.Serialize(NewItem(), VoiSlateJson.Options);
        Assert.Contains("\"scn\":\"1\"", json);
        Assert.Contains("\"sht\":\"A\"", json);
        Assert.Contains("\"filenamePrefix\":\"260820\"", json);
        Assert.Contains("\"filenameLinker\":\"-T\"", json);
        Assert.Contains("\"filenameNum\":2", json);
        Assert.Contains("\"shtNote\":\"Boom note\"", json);
        Assert.Contains("\"okSht\":\"ok\"", json);
    }

    [Fact]
    public void FileName_Pads_To_Three_And_Preserves_Overflow()
    {
        var item = NewItem();
        item.FilenamePrefix = "260820";
        item.FilenameLinker = "-T";
        item.FilenameNum = 2;
        Assert.Equal("260820-T002", item.FileName);

        item.FilenameNum = 1234; // padLeft(3) 对超位数不截断
        Assert.Equal("260820-T1234", item.FileName);
    }

    [Fact]
    public void Note_ToString_Matches_Original_Format()
    {
        var note = new Note(["缪尔赛斯", "塞雷娅"], "近景", "小插曲");
        Assert.Equal("Note{objects: 缪尔赛斯, 塞雷娅, type: 近景, append: 小插曲}", note.ToString());
    }
}