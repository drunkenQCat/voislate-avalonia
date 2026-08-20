using System.Text.Json.Serialization;

namespace VoiSlate.Models;

/// <summary>
/// 录音机类型（契约 §1/§2 显式数值；对齐原版 models/recorder_type.dart：
/// <c>enum RecorderType { defaultRecorder, soundDevices, custom }</c>）。
/// JSON 序列化用 camelCase 短名：defaultRecorder / soundDevices / custom（A#16，见 JsonConverters.cs）。
/// 对应原版 RecordFileNum.recorderType 字符串：<c>"default"</c> / <c>"sound devices"</c> / <c>"custom"</c>。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverterCamelCase))]
public enum RecorderType
{
    DefaultRecorder = 0,
    SoundDevices = 1,
    Custom = 2,
}

/// <summary>
/// RecorderType 互操作（原版持久化字符串 + P0.5 PrefixType 兼容映射）。
/// P0.5 的 <see cref="PrefixType"/>（Enums.cs）是内部发明、非契约成员；契约收敛后应统一为
/// <see cref="RecorderType"/>（E 演进 IFileNamingService/ITakeFlowService 时随契约 bump 切换）。
/// </summary>
public static class RecorderTypeExtensions
{
    /// <summary>设置持久化值（对齐原版 recorderType 字符串：default / sound devices / custom）。</summary>
    public static string ToSettingsValue(this RecorderType type) => type switch
    {
        RecorderType.Custom => "custom",
        RecorderType.SoundDevices => "sound devices",
        _ => "default",
    };

    /// <summary>从设置字符串解析（未知值回退 DefaultRecorder；与原版精确字符串比较语义一致）。</summary>
    public static RecorderType ParseSettings(string? value) => value switch
    {
        "custom" => RecorderType.Custom,
        "sound devices" => RecorderType.SoundDevices,
        _ => RecorderType.DefaultRecorder,
    };

    /// <summary>P0.5 兼容映射（PrefixType → RecorderType）。</summary>
    public static RecorderType ToRecorderType(this PrefixType type) => type switch
    {
        PrefixType.Custom => RecorderType.Custom,
        PrefixType.SoundDevices => RecorderType.SoundDevices,
        _ => RecorderType.DefaultRecorder,
    };

    /// <summary>P0.5 兼容映射（RecorderType → PrefixType）。</summary>
    public static PrefixType ToPrefixType(this RecorderType type) => type switch
    {
        RecorderType.Custom => PrefixType.Custom,
        RecorderType.SoundDevices => PrefixType.SoundDevices,
        _ => PrefixType.Default,
    };
}