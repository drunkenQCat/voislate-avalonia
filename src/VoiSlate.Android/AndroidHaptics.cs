using Android.Content;
using Android.OS;
using VoiSlate.Services;

namespace VoiSlate.Android;

/// <summary>
/// Android 触觉实现：把 <see cref="Haptics.Vibrate"/> 落到 <c>Vibrator</c> / <c>VibrationEffect</c>。
///
/// <para>
/// 由 <see cref="AndroidApp"/> 的构造函数调用 <see cref="Install()"/> —— 那是进程里最早能跑托管代码的地方，
/// 早于任何 Activity 与 Avalonia 初始化，避开了「视图构造时实现还没注册」这个坑。
/// 振子句柄在首次使用时惰性解析，所以 <see cref="Install"/> 本身不碰 Context。
/// </para>
/// </summary>
internal static class AndroidHaptics
{
    private static Vibrator? _vibrator;
    private static bool _resolved;

    /// <summary>把本实现挂到 <see cref="Haptics.Impl"/>。</summary>
    public static void Install() => Haptics.Impl = Vibrate;

    private static Vibrator? Resolve()
    {
        if (_resolved) return _vibrator;
        _resolved = true;
        try
        {
            var context = global::Android.App.Application.Context;
            // API 31 起 Vibrator 归 VibratorManager 管；旧版本直接取 VibratorService。
            _vibrator = OperatingSystem.IsAndroidVersionAtLeast(31)
                ? (context.GetSystemService(Context.VibratorManagerService) as VibratorManager)?.DefaultVibrator
                : context.GetSystemService(Context.VibratorService) as Vibrator;
            if (_vibrator is not null && !_vibrator.HasVibrator) _vibrator = null;
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("VoiSlateHaptics", $"振子不可用：{ex.Message}");
            _vibrator = null;
        }
        return _vibrator;
    }

    private static void Vibrate(int amplitude, int durationMs)
    {
        var vibrator = Resolve();
        if (vibrator is null || durationMs <= 0) return;
        try
        {
            // 原版 vibration 包语义就是「固定幅度一次性震动」：直接把请求幅度交给系统。
            // 注意别用 HasAmplitudeControl 去「纠正」——RMX5060 上该系统探针返回 false，
            // 但显式幅度照样被 HAL 采纳（真机 amplitude 由 1.0 变成 0.50）。探测不可靠，就别用它守着。
            var level = Math.Clamp(amplitude, 1, 255);
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
                vibrator.Vibrate(VibrationEffect.CreateOneShot(durationMs, level));
            else
#pragma warning disable CA1422 // 旧 API 仅作 API 23..25 兜底
                vibrator.Vibrate(durationMs);
#pragma warning restore CA1422
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("VoiSlateHaptics", $"震动失败：{ex.Message}");
        }
    }
}