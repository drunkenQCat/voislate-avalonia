using VoiSlate.Services;
using Xunit;

namespace VoiSlate.Tests.Services;

/// <summary>硬件音量键服务（契约 §3 IHardwareKeyService；桌面 no-op + 枚举显式数值）。</summary>
public class HardwareKeyServiceTests
{
    [Fact]
    public void Enum_Values_Are_Explicit()
    {
        Assert.Equal(0, (int)HardwareKey.VolumeUp);
        Assert.Equal(1, (int)HardwareKey.VolumeDown);
    }

    [Fact]
    public void Noop_Service_Subscribable_And_Never_Raises()
    {
        var svc = new NoopHardwareKeyService();
        var raised = 0;
        svc.KeyPressed += _ => raised++;

        // 桌面 no-op：无触发源，不抛异常
        Assert.Equal(0, raised);
    }
}