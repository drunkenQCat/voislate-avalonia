using VoiSlate.Models;

namespace VoiSlate.Services;

/// <summary>
/// 场记导出（契约 v0.5 §3 IExportService；camelCase JSON + JsonStringEnumConverter(CamelCase)，
/// 格式兼容原件——无日期字段、含 fake/wild 哨兵值，ADR-005）。
/// </summary>
public interface IExportService
{
    string SerializeLogs(IEnumerable<SlateLogItem> logs);

    Task SaveToFileAsync(string dir, string name, string content);
}

/// <summary>B 补桩（演进权归 E）：暂不执行任何导出。</summary>
public sealed class NoopExportService : IExportService
{
    public string SerializeLogs(IEnumerable<SlateLogItem> logs) => string.Empty;

    public Task SaveToFileAsync(string dir, string name, string content) => Task.CompletedTask;
}