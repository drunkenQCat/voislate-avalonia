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
#pragma warning disable CS0067 // no-op 实现事件永不触发
    public event Action<HardwareKey>? KeyPressed;
#pragma warning restore CS0067
}

/// <summary>
/// 平台硬件键通道的进程级注册表（与 <see cref="VoiSlate.Desktop.Speech.PcmCapture"/> 同构）。
/// Android 在 MainActivity.OnCreate 里注入真实实现；其它平台保持 null，界面退回键盘快捷键。
/// </summary>
public static class HardwareKeys
{
    public static IHardwareKeyService? Current { get; set; }
}