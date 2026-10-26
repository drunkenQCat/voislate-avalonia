using Xunit;

namespace VoiSlate.Tests;

/// <summary>M0 冒烟测试：验证测试项目与主项目引用链路可用（契约 v0.5 §7 测试基线）。</summary>
public class SmokeTests
{
    [Fact]
    public void Main_Project_Types_Resolvable()
    {
        Assert.Equal("VoiSlate.Services.FileNumberingService", typeof(VoiSlate.Services.FileNumberingService).FullName);
        var ctor = typeof(VoiSlate.Desktop.DesktopWorkspaceService).GetConstructor(new[]
        {
            typeof(VoiSlate.Services.IScheduleStore),
            typeof(VoiSlate.Services.ILogRepository),
            typeof(VoiSlate.Services.ISessionSettingsStore),
            typeof(VoiSlate.Services.ITimeProvider),
            typeof(VoiSlate.Desktop.IWorkspaceTransaction),
        });
        Assert.NotNull(ctor);
    }
}
