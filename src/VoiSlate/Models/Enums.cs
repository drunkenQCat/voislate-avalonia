using System.Text.Json.Serialization;

namespace VoiSlate.Models;

/// <summary>
/// 录音文件状态（对齐原版 TkStatus；显式数值 0/1/2，LiteDB BSON 保留 int 语义，C-13）。
/// JSON 序列化用 camelCase 短名：notChecked / ok / bad（A#16，见 JsonConverters.cs）。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverterCamelCase))]
public enum TkStatus
{
    NotChecked = 0,
    Ok = 1,
    Bad = 2,
}

/// <summary>画面状态（对齐原版 ShtStatus）。notChecked / ok / nice。</summary>
[JsonConverter(typeof(JsonStringEnumConverterCamelCase))]
public enum ShtStatus
{
    NotChecked = 0,
    Ok = 1,
    Nice = 2,
}

/// <summary>
/// 录入类型（对齐原版 TakeType）。
/// - normal：正常条（文件号递增）；fake：假条（tk=999、tkNote="Fake Take"）
/// - end：本镜结束（写 OK 哨兵，不递增文件号）；wild：未联动时的 W 条（tk=0、"wild track " 前缀）
/// </summary>
public enum TakeType
{
    Normal,
    Fake,
    End,
    Wild,
}

/// <summary>文件名前缀模式（对齐原版 RecordFileNum.recorderType 字符串："custom"/"sound devices"/"default"）。</summary>
public enum PrefixType
{
    Custom,
    SoundDevices,
    Default,
}

public static class PrefixTypeExtensions
{
    public static string ToSettingsValue(this PrefixType type) => type switch
    {
        PrefixType.Custom => "custom",
        PrefixType.SoundDevices => "sound devices",
        _ => "default",
    };

    public static PrefixType ParseSettings(string? value) => value switch
    {
        "custom" => PrefixType.Custom,
        "sound devices" => PrefixType.SoundDevices,
        _ => PrefixType.Default,
    };
}