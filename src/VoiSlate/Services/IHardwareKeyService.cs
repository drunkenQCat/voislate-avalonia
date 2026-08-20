namespace VoiSlate.Services;

#pragma warning disable CS0067 // no-op 实现事件暂不触发（Android 增强后续）

/// <summary>硬件键（契约 §3 HardwareKey；原版 android_physical_buttons 死依赖不迁移，接口先行）。</summary>
public enum HardwareKey
{
    VolumeUp = 0,
    VolumeDown = 1,
}

/// <summary>
/// 硬件音量键服务（契约 §3 IHardwareKeyService；仅记录页激活时订阅，contracts §4 B5 Scoped 钩子）。
/// 桌面 no-op（不产生事件）；Android 增强后续（R10）。
/// </summary>
public interface IHardwareKeyService
{
    event Action<HardwareKey>? KeyPressed;
}

/// <summary>桌面 no-op 实现。</summary>
public sealed class NoopHardwareKeyService : IHardwareKeyService
{
    public event Action<HardwareKey>? KeyPressed;
}