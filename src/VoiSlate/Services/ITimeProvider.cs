namespace VoiSlate.Services;

/// <summary>
/// 时间源（可注入确定性时间；B/Service 单测依赖此接口，C-12）。
/// P0.5 预产接口桩，演进权归 E（契约 C-3）。
/// </summary>
public interface ITimeProvider
{
    DateTime Now { get; }
}

public sealed class SystemTimeProvider : ITimeProvider
{
    public DateTime Now => DateTime.Now;
}

/// <summary>日期格式工具（对齐原版 RecordFileNum：today = "yyMMdd"，soundDevicesToday = "yyYMM-dd" 除外）。</summary>
public static class VoiSlateDates
{
    /// <summary>yyMMdd，如 230522（对齐 RecordFileNum.today）。</summary>
    public static string TodayKey(DateTime now)
        => now.ToString("yyMMdd");

    /// <summary>yyYMMdd，如 23Y05M22（对齐 RecordFileNum.soundDevicesToday）。</summary>
    public static string SoundDevicesKey(DateTime now)
        => $"{now:yy}Y{now:MM}M{now:dd}";
}