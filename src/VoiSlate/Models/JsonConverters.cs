using System.Text.Json;
using System.Text.Json.Serialization;

namespace VoiSlate.Models;

/// <summary>
/// camelCase 短名枚举转换器（A#16：默认 JsonStringEnumConverter 输出 PascalCase 成员名，
/// 与原版 dart_json_mapper 短名枚举（notChecked/ok/bad/nice）不等价，必须显式 CamelCase）。
/// </summary>
public sealed class JsonStringEnumConverterCamelCase : JsonStringEnumConverter
{
    public JsonStringEnumConverterCamelCase()
        : base(JsonNamingPolicy.CamelCase, allowIntegerValues: true)
    {
    }
}

/// <summary>导出/导入共用：与原版 JSON 格式（camelCase 属性 + camelCase 短名枚举）一致的序列化选项。</summary>
public static class VoiSlateJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverterCamelCase() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };
}