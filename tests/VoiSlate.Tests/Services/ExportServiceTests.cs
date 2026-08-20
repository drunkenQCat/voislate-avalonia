using System.Text.Json;
using VoiSlate.Models;
using VoiSlate.Services;
using Xunit;

namespace VoiSlate.Tests.Services;

/// <summary>JSON 导出（契约 §3 IExportService；VoiSlateJson 语义 + 文件写出）。</summary>
public class ExportServiceTests
{
    private static SlateLogItem NewItem(int tk, int num, TkStatus okTk, ShtStatus okSht) => new()
    {
        Scn = "1",
        Sht = "A",
        Tk = tk,
        FilenamePrefix = "260820",
        FilenameLinker = "-T",
        FilenameNum = num,
        TkNote = $"Tk {tk}",
        ShtNote = "note<麦克风/>",
        ScnNote = "scene",
        OkTk = okTk,
        OkSht = okSht,
    };

    [Fact]
    public void Serialize_Uses_CamelCase_Keys_And_Short_Enum_Names()
    {
        var svc = new ExportService();

        var json = svc.SerializeLogs([NewItem(2, 12, TkStatus.Ok, ShtStatus.Nice)]);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        var item = root[0];

        // 键：camelCase（对齐原版字段名）
        Assert.Equal("1", item.GetProperty("scn").GetString());
        Assert.Equal("A", item.GetProperty("sht").GetString());
        Assert.Equal(2, item.GetProperty("tk").GetInt32());
        Assert.Equal("260820", item.GetProperty("filenamePrefix").GetString());
        Assert.Equal("-T", item.GetProperty("filenameLinker").GetString());
        Assert.Equal(12, item.GetProperty("filenameNum").GetInt32());
        Assert.Equal("Tk 2", item.GetProperty("tkNote").GetString());
        Assert.Equal("note<麦克风/>", item.GetProperty("shtNote").GetString());
        Assert.Equal("scene", item.GetProperty("scnNote").GetString());

        // 枚举短名（JsonStringEnumConverterCamelCase，等价原版 EnumConverterShort）
        Assert.Equal("ok", item.GetProperty("okTk").GetString());
        Assert.Equal("nice", item.GetProperty("okSht").GetString());
    }

    [Fact]
    public void Serialize_Does_Not_Include_Id_FileName_Or_Date_Fields()
    {
        var json = new ExportService().SerializeLogs([NewItem(1, 1, TkStatus.Bad, ShtStatus.NotChecked)]);
        using var doc = JsonDocument.Parse(json);
        var item = doc.RootElement[0];
        var names = item.EnumerateObject().Select(p => p.Name).ToArray();

        // 键集合与原件一致：无 id / fileName（计算属性）/ date 字段（F1）
        Assert.DoesNotContain("id", names, StringComparer.Ordinal);
        Assert.DoesNotContain("fileName", names, StringComparer.Ordinal);
        Assert.DoesNotContain("date", names, StringComparer.Ordinal);
        Assert.Equal(
            ["scn", "sht", "tk", "filenamePrefix", "filenameLinker", "filenameNum", "tkNote", "shtNote", "scnNote", "okTk", "okSht"],
            names);

        // 未被忽略的字段仍在（F1：无日期字段 ≠ 缺字段）；枚举短名
        Assert.Equal("bad", item.GetProperty("okTk").GetString());
        Assert.Equal("notChecked", item.GetProperty("okSht").GetString());
    }

    [Fact]
    public void Serialize_Empty_List_Produces_Empty_Array()
    {
        Assert.Equal("[]", new ExportService().SerializeLogs([]));
    }

    [Fact]
    public async Task SaveToFile_Creates_Directory_And_Writes_Content()
    {
        var dir = Path.Combine(Path.GetTempPath(), "voislate-export-" + Guid.NewGuid().ToString("N"));
        var svc = new ExportService();

        await svc.SaveToFileAsync(dir, "out.json", "[1,2]");

        var path = Path.Combine(dir, "out.json");
        Assert.True(File.Exists(path));
        Assert.Equal("[1,2]", await File.ReadAllTextAsync(path));

        Directory.Delete(dir, recursive: true);
    }
}