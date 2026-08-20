using System.Text;
using System.Text.Json;
using VoiSlate.Models;
using VoiSlate.Services;
using VoiSlate.Tests.TestDoubles;
using Xunit;

namespace VoiSlate.Tests.Services;

/// <summary>场记导入（IImportService：反序列化并按 key/日期落库——今天 + 条目自身 FileName 为 key）。</summary>
public class ImportServiceTests
{
    private readonly FakeLogRepository _logs = new();
    private readonly FakeTimeProvider _time = new(); // 固定 2026-08-20 → today=260820

    private ImportService NewService() => new(_logs, _time);

    private static string SerializeOne(int tk, int num, TkStatus okTk) =>
        new ExportService().SerializeLogs([new SlateLogItem
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
            OkSht = ShtStatus.NotChecked,
        }]);

    [Fact]
    public async Task Import_Stores_Under_Today_With_Item_FileName_As_Key()
    {
        var svc = NewService();
        var json = new ExportService().SerializeLogs([
            new SlateLogItem { Scn = "1", Sht = "A", Tk = 1, FilenamePrefix = "260820", FilenameLinker = "-T", FilenameNum = 1, TkNote = "N1", ShtNote = "S1", ScnNote = "C1", OkTk = TkStatus.Bad },
            new SlateLogItem { Scn = "2", Sht = "B", Tk = 2, FilenamePrefix = "260820", FilenameLinker = "-T", FilenameNum = 2, TkNote = "N2", ShtNote = "S2", ScnNote = "C2", OkTk = TkStatus.Ok },
        ]);

        var count = await svc.ImportAsync(json, CancellationToken.None);

        Assert.Equal(2, count);
        Assert.Equal(2, _logs.Items.Count);
        Assert.All(_logs.Items, e => Assert.Equal("260820", e.Date));          // 今天落库
        Assert.Equal("260820-T001", _logs.Items[0].Key);                        // key = 自身文件名
        Assert.Equal("260820-T002", _logs.Items[1].Key);
        Assert.Equal(TkStatus.Bad, _logs.Items[0].Item.OkTk);                   // 枚举往返
        Assert.Equal("S1", _logs.Items[0].Item.ShtNote);
    }

    [Fact]
    public async Task Import_Empty_Array_Returns_Zero()
    {
        var svc = NewService();
        var count = await svc.ImportAsync("[]", CancellationToken.None);

        Assert.Equal(0, count);
        Assert.Empty(_logs.Items);
    }

    [Fact]
    public async Task Import_From_Stream_With_Utf8_Bom()
    {
        var json = SerializeOne(3, 3, TkStatus.Ok);
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(json)).ToArray();
        using var stream = new MemoryStream(bytes);

        var count = await NewService().ImportAsync(stream, CancellationToken.None);

        Assert.Equal(1, count);
        Assert.Single(_logs.Items);
        Assert.Equal("260820-T003", _logs.Items[0].Key);
    }

    [Fact]
    public async Task Malformed_Json_Throws_JsonException()
    {
        var svc = NewService();

        await Assert.ThrowsAsync<JsonException>(() => svc.ImportAsync("{not json", CancellationToken.None));
        Assert.Empty(_logs.Items);
    }
}