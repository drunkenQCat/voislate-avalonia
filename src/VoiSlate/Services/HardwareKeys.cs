namespace VoiSlate.Services;

/// <summary>硬件键（契约 §3 HardwareKey；原版 android_physical_buttons 死依赖不迁移）。</summary>
public enum HardwareKey
{
    VolumeUp = 0,
    VolumeDown = 1,
}

/// <summary>
/// 硬件键通道：**静态事件**。平台实现（Android 的 MainActivity）收到音量键时调用 <see cref="Raise"/>，
/// 由订阅者（`Desktop/DesktopWorkspaceView`）负责业务。
///
/// 为什么不是「实例注册表 + Current 属性」：实测 Android 上 Avalonia 的 Application 初始化早于
/// `MainActivity.OnCreate`，视图构造时读 `Current` 必然拿到 null → 订阅不上，真机表现是
/// `handlers=0`（键到了 DispatchKeyEvent 却没人响应）。静态事件与注册时机无关。
/// </summary>
public static class HardwareKeys
{
    private static Action<HardwareKey>? _pressed;

    /// <summary>音量键按下。桌面无平台实现时永不触发，界面退回键盘快捷键。</summary>
    public static event Action<HardwareKey>? Pressed { add => _pressed += value; remove => _pressed -= value; }

    /// <summary>是否有订阅者（平台实现可据此决定是否消费按键、不让系统调音量）。</summary>
    public static bool HasHandlers => _pressed is not null;

    /// <summary>平台实现投递按键。</summary>
    public static void Raise(HardwareKey key) => _pressed?.Invoke(key);
}
