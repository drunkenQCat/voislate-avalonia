namespace VoiSlate.Services;

#pragma warning disable CS0067 // 桌面 no-op 暂不触发事件（E 接入平台通道后实现）

/// <summary>音量键（契约 v0.5 §3 IHardwareKeyService；P0.5 未产 → B 补桩，演进权归 E）。</summary>
public enum HardwareKey
{
    VolumeUp,
    VolumeDown,
}

/// <summary>
/// 硬件音量键事件源（契约 §4 RecordViewModel：仅记录页激活时订阅，B5）。
/// 桌面 no-op；Android 平台通道增强后续（R10 平台能力降级）。
/// </summary>
public interface IHardwareKeyService
{
    event Action<HardwareKey>? KeyPressed;
}

/// <summary>桌面 no-op 实现（不产生事件；Android 增强由 E 演进）。</summary>
public sealed class NoopHardwareKeyService : IHardwareKeyService
{
    public event Action<HardwareKey>? KeyPressed;
}