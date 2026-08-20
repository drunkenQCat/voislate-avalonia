using VoiSlate.Models;

namespace VoiSlate.Services;

/// <summary>
/// B6 文件名规则（契约 §3 IFileNamingService 语义：GetPrefix/FormatFileName 静态规则，供 FileNumberingService 调用）。
/// 注意：契约 §3 命名 "IFileNamingService" 与 P0.5 已交付的 IFileNamingService（文件号状态机，供 ITakeFlowService 持用）
/// 存在命名冲突（见报告）；本类以 "FileNamingRules" 名承载 B6 纯规则，参数用已交付 PrefixType（RecorderType 尚未交付，归 A）。
/// </summary>
public static class FileNamingRules
{
    /// <summary>
    /// 前缀规则：Default → yymmdd（260820）；SoundDevices → yyYMMdd（26Y08M20）；Custom → customPrefix（缺省 "custom"）。
    /// </summary>
    public static string GetPrefix(PrefixType mode, DateTime now, string? customPrefix = null) => mode switch
    {
        PrefixType.Custom => string.IsNullOrEmpty(customPrefix) ? "custom" : customPrefix,
        PrefixType.SoundDevices => VoiSlateDates.SoundDevicesKey(now),
        _ => VoiSlateDates.TodayKey(now),
    };

    /// <summary>文件名 = prefix + linker + number 补零 3 位（原版 filenameNum.toString().padLeft(3, '0')）。</summary>
    public static string FormatFileName(string prefix, string linker, int number) => $"{prefix}{linker}{number:D3}";
}