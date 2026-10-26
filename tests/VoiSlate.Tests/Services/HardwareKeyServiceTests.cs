using VoiSlate.Services;
using Xunit;

namespace VoiSlate.Tests.Services;

/// <summary>硬件音量键通道（契约 §3 HardwareKey；静态事件，与平台注册时机无关）。</summary>
public class HardwareKeyServiceTests
{
    [Fact]
    public void Enum_Values_Are_Explicit()
    {
        Assert.Equal(0, (int)HardwareKey.VolumeUp);
        Assert.Equal(1, (int)HardwareKey.VolumeDown);
    }

    [Fact]
    public void Raise_Reaches_Subscribers_And_Stops_After_Unsubscribe()
    {
        var raised = new List<HardwareKey>();
        void Handler(HardwareKey key) => raised.Add(key);
        Assert.False(HardwareKeys.HasHandlers);

        HardwareKeys.Pressed += Handler;
        try
        {
            Assert.True(HardwareKeys.HasHandlers);
            HardwareKeys.Raise(HardwareKey.VolumeUp);
            HardwareKeys.Raise(HardwareKey.VolumeDown);
            Assert.Equal([HardwareKey.VolumeUp, HardwareKey.VolumeDown], raised);
        }
        finally { HardwareKeys.Pressed -= Handler; }

        HardwareKeys.Raise(HardwareKey.VolumeUp);
        Assert.Equal(2, raised.Count);
        Assert.False(HardwareKeys.HasHandlers);
    }
}
