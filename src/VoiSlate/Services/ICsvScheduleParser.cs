using VoiSlate.Models;

namespace VoiSlate.Services;

/// <summary>
/// 计划 CSV 解析（契约 v0.5 §3 CsvScheduleParser；CsvHelper 7 列：场景号/内容/镜头号/补充/景别(默认"近景")/镜头内容/补充；
/// objects 无列时默认 ['Boom'] 并写入文档说明）。
/// P0.5 未产 → B 补桩，演进权归 E。
/// </summary>
public interface ICsvScheduleParser
{
    Task<IReadOnlyList<SceneSchedule>> ParseAsync(Stream stream, CancellationToken ct);
}

/// <summary>B 补桩（演进权归 E）：返回空计划。</summary>
public sealed class NoopCsvScheduleParser : ICsvScheduleParser
{
    public Task<IReadOnlyList<SceneSchedule>> ParseAsync(Stream stream, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<SceneSchedule>>([]);
}