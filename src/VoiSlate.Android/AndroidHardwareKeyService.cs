using Android.Views;
using VoiSlate.Services;

namespace VoiSlate.Android;

/// <summary>
/// 音量键 → 工作台记条/撤回。Android 的音量键默认调系统音量，只有 Activity 在 OnKeyDown 里
/// 消费掉（返回 true）才不会被系统接管；原版（Flutter）也是这么做的，现场单手/戴手套记条靠它。
/// </summary>
public sealed class AndroidHardwareKeyService : IHardwareKeyService
{
    public event Action<HardwareKey>? KeyPressed;

    /// <summary>返回 true 表示本键已被应用消费（不再调系统音量）。</summary>
    public bool Handle(Keycode keyCode)
    {
        var key = keyCode switch
        {
            Keycode.VolumeUp => HardwareKey.VolumeUp,
            Keycode.VolumeDown => HardwareKey.VolumeDown,
            _ => (HardwareKey?)null,
        };
        if (key is null) return false;
        KeyPressed?.Invoke(key.Value);
        return true;
    }
}