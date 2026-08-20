using System.Text.Json;
using VoiSlate.Models;

namespace VoiSlate.Services;

/// <summary>
/// 场记导出（契约 §3 IExportService；语义对齐原版 slate_log_page.dart 的 JsonMapper.serialize + 设置页导出全部）。
/// - SerializeLogs：VoiSlateJson.Options（camelCase 属性 + camelCase 短名枚举），与原件 JSON 格式兼容
///   （无日期字段、无 _id；Id/FileName 计算属性 [JsonIgnore]，ADR-005）。
/// - SaveToFileAsync：写文件（目录不存在则创建）。
/// 导出全部（跨日合并）由调用方（B 的 SlateLogPageViewModel/SettingsViewModel）收集各日期条目后调 SerializeLogs。
/// </summary>
public interface IExportService
{
    /// <summary>序列化为 camelCase JSON 数组（含 fake/wild 哨兵值；无日期字段，F1）。</summary>
    string SerializeLogs(IEnumerable<SlateLogItem> logs);

    /// <summary>写入文件（自动建目录）。</summary>
    Task SaveToFileAsync(string dir, string name, string content);
}

public sealed class ExportService : IExportService
{
    public string SerializeLogs(IEnumerable<SlateLogItem> logs) =>
        JsonSerializer.Serialize(logs, VoiSlateJson.Options);

    public Task SaveToFileAsync(string dir, string name, string content)
    {
        Directory.CreateDirectory(dir);
        return File.WriteAllTextAsync(Path.Combine(dir, name), content);
    }
}