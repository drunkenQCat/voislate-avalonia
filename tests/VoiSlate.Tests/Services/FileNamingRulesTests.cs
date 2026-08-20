using VoiSlate.Models;
using VoiSlate.Services;
using Xunit;

namespace VoiSlate.Tests.Services;

/// <summary>B6 静态命名规则（契约 §3 IFileNamingService 语义以 FileNamingRules 承载，见接口注）。</summary>
public class FileNamingRulesTests
{
    private static readonly DateTime Fixed = new(2026, 8, 20, 12, 0, 0);

    [Fact]
    public void GetPrefix_Default_Is_YYMMDD()
    {
        Assert.Equal("260820", FileNamingRules.GetPrefix(PrefixType.Default, Fixed));
    }

    [Fact]
    public void GetPrefix_SoundDevices_Is_YYYMMDD()
    {
        // 对齐 VoiSlateDates.SoundDevicesKey：26Y08M20
        Assert.Equal("26Y08M20", FileNamingRules.GetPrefix(PrefixType.SoundDevices, Fixed));
    }

    [Fact]
    public void GetPrefix_Custom_Uses_Custom_Value_Or_Fallback()
    {
        Assert.Equal("project-x", FileNamingRules.GetPrefix(PrefixType.Custom, Fixed, "project-x"));
        Assert.Equal("custom", FileNamingRules.GetPrefix(PrefixType.Custom, Fixed));
    }

    [Fact]
    public void FormatFileName_Pads_Number_To_Three_Digits()
    {
        Assert.Equal("260820-T007", FileNamingRules.FormatFileName("260820", "-T", 7));
        Assert.Equal("260820-T001", FileNamingRules.FormatFileName("260820", "-T", 1));
        Assert.Equal("26Y08M20T123", FileNamingRules.FormatFileName("26Y08M20", "T", 123));
    }
}