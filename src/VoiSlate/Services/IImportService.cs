using System.Text;
using System.Text.Json;
using VoiSlate.Models;

namespace VoiSlate.Services;

/// <summary>
/// 场记导入（原版无导入功能——deserialize 被注释（F1）；本服务为新能力，语义见实现注）。
/// - 反序列化 VoiSlateJson.Options 格式的 JSON 数组（与 IExportService.SerializeLogs 互逆）。
/// - 按 key/日期落库：导出格式不含日期字段（F1），故全部导入到"今天"（ITimeProvider）；
///   key 取条目自身 FileName（prefix+linker+num 补零 3 位）——导出无"上一拍 key"，自身文件名是唯一可辨识键。
/// - 不去重（重复导入产生重复条目，原版行为未定义；文档注明）。
/// </summary>
public interface IImportService
{
    /// <summary>解析 JSON 并落库；返回导入条数。非法 JSON 抛 JsonException（ADR-009 由 VM 层处理）。</summary>
    Task<int> ImportAsync(string json, CancellationToken ct);

    /// <summary>从流读取（支持 UTF-8 BOM）后导入。</summary>
    Task<int> ImportAsync(Stream stream, CancellationToken ct);
}

public sealed class ImportService(ILogRepository logs, ITimeProvider time) : IImportService
{
    public Task<int> ImportAsync(string json, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var items = JsonSerializer.Deserialize<List<SlateLogItem>>(json, VoiSlateJson.Options) ?? [];
        return ImportCoreAsync(items, ct);
    }

    public async Task<int> ImportAsync(Stream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var json = await reader.ReadToEndAsync(ct);
        return await ImportAsync(json, ct);
    }

    private async Task<int> ImportCoreAsync(List<SlateLogItem> items, CancellationToken ct)
    {
        var today = VoiSlateDates.TodayKey(time.Now);
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            await logs.AddAsync(today, item.FileName, item);
        }

        return items.Count;
    }
}