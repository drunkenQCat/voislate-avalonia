using System.Text;
using VoiSlate.Models;
using VoiSlate.Services;
using Xunit;

namespace VoiSlate.Tests.Services;

/// <summary>计划 CSV 解析（契约 §3 CsvScheduleParser；逐行对齐 schedule_csv_parser.dart 的列语义）。</summary>
public class ScheduleCsvParserTests
{
    private static Task<IReadOnlyList<SceneSchedule>> Parse(string csv)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return CsvScheduleParser.ParseAsync(stream, CancellationToken.None);
    }

    [Fact]
    public async Task Parses_Seven_Columns_With_Header_Skipped()
    {
        var csv = """
            场号,场景内容,镜头号,补充,景别,镜头内容,补充
            1A万星园,三人会面,1,小插曲,,进门,咖啡
            """;

        var scenes = await Parse(csv);

        var scene = Assert.Single(scenes);
        // 场景：col0 "1A万星园" → Key"1" Fix"A" Type"A万星园"（首个字母 + 其余字符，原版 locRegExp）
        Assert.Equal("1", scene.Info.Key);
        Assert.Equal("A", scene.Info.Fix);
        Assert.Equal("1A", scene.Info.Name);
        Assert.Equal("A万星园", scene.Info.Note.Type);
        Assert.Equal(["Boom"], scene.Info.Note.Objects);
        // 场景 append = col1 + "，" + col3（原版 getScnContent）
        Assert.Equal("三人会面，小插曲", scene.Info.Note.Append);
        // 镜：col2 "1" → Key"1"；col4 空 → Type"近景"；append = col5 + col6 直接拼接
        var shot = Assert.Single(scene.Items);
        Assert.Equal("1", shot.Key);
        Assert.Equal("近景", shot.Note.Type);
        Assert.Equal(["Boom"], shot.Note.Objects);
        Assert.Equal("进门咖啡", shot.Note.Append);
    }

    [Fact]
    public async Task Shots_Without_Scene_Column_Join_Last_Scene_And_Deduplicate()
    {
        var csv = """
            场号,场景内容,镜头号,补充,景别,镜头内容,补充
            1A万星园,三人会面,1,,,进门,
            ,,2,,特写,两人对峙,
            ,,2,,,重复镜头号,
            """;

        var scenes = await Parse(csv);
        var scene = Assert.Single(scenes);
        // 纯镜行并入上一场；同 "num-fix" 去重（镜 2 只出现一次）
        Assert.Equal(2, scene.Count);
        Assert.Equal("近景", scene[0].Note.Type);
        Assert.Equal("特写", scene[1].Note.Type);
        Assert.Equal("两人对峙", scene[1].Note.Append);
    }

    [Fact]
    public async Task Duplicate_Scene_Number_Is_Dropped_Entirely_With_Its_Shots()
    {
        var csv = """
            场号,场景内容,镜头号,补充,景别,镜头内容,补充
            1A万星园,第一场,1,,,A1,
            1A万星园,重复场景号,2,,,B1,
            2B洛肯实验室,第二场,3,,,C1,
            """;

        var scenes = await Parse(csv);
        // 场景 1A 只生成一次；"1A" 第二行整行丢弃（原版 processedInfo continue，含其镜头）
        Assert.Equal(2, scenes.Count);
        Assert.Equal(["1A", "2B"], scenes.Select(s => s.Info.Name).ToArray());
        Assert.Single(scenes[0].Items);
    }

    [Fact]
    public async Task Empty_Or_Blank_Rows_Are_Skipped_And_Empty_Scene_Content_Yields_Empty_Append()
    {
        var csv = """
            场号,场景内容,镜头号,补充,景别,镜头内容,补充

            1A,,1,,,only shot,
            ,,,,,
            """;

        var scenes = await Parse(csv);
        var scene = Assert.Single(scenes);
        // col1 为空 → getScnContent 返回 null → append 空串（不带 "，" 前缀）
        Assert.Equal(string.Empty, scene.Info.Note.Append);
        Assert.Equal("1", scene.Info.Key);
        Assert.Equal("only shot", Assert.Single(scene.Items).Note.Append);
    }

    [Fact]
    public async Task Short_Rows_Are_Padded_Instead_Of_Crashing()
    {
        // 原版少于 7 列会越界崩溃（ADR-009 修复：补空列）。
        var csv = """
            场号,场景内容,镜头号
            1A,场景,1
            """;

        var scenes = await Parse(csv);
        var scene = Assert.Single(scenes);
        Assert.Equal("1A", scene.Info.Name);
        Assert.Equal("近景", Assert.Single(scene.Items).Note.Type);
    }

    [Fact]
    public async Task Quoted_Fields_With_Commas_Are_Handled_By_CsvHelper()
    {
        var csv = """
            场号,场景内容,镜头号,补充,景别,镜头内容,补充
            1A万星园,"三人会面，缪尔赛斯",1,补充甲,,进门,咖啡
            """;

        var scenes = await Parse(csv);
        var scene = Assert.Single(scenes);
        Assert.Equal("三人会面，缪尔赛斯，补充甲", scene.Info.Note.Append);
    }

    [Fact]
    public async Task Shot_Fix_Letter_Is_Extracted_From_Shot_Number_Column()
    {
        var csv = """
            场号,场景内容,镜头号,补充,景别,镜头内容,补充
            1A,第一场,2B,,,特写镜头,
            """;

        var scenes = await Parse(csv);
        var scene = Assert.Single(scenes);
        var shot = Assert.Single(scene.Items);
        Assert.Equal("2", shot.Key);
        Assert.Equal("B", shot.Fix);
        Assert.Equal("2B", shot.Name);
    }

    [Fact]
    public async Task Cancelled_Token_Throws()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("a,b,c,d,e,f,g\n1,2,3,4,5,6,7\n"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CsvScheduleParser.ParseAsync(stream, cts.Token));
    }
}