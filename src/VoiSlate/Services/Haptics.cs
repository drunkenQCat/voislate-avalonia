namespace VoiSlate.Services;

/// <summary>
/// 触觉反馈（原版 Flutter 用 <c>vibration</c> 包的 <c>Vibration.vibrate(amplitude, duration)</c>）。
///
/// <para>
/// 平台实现通过 <see cref="Impl"/> 注册（Android 在 <c>AndroidApp</c> 里装），而**不是** DI 单例：
/// 与 <see cref="HardwareKeys"/> / <c>PcmCapture</c> 同一教训 —— Avalonia 的 Application 初始化
/// 早于 MainActivity，视图构造时从 DI 取实例实现会拿到 Noop；静态注册点则不受初始化顺序影响。
/// 桌面没有振子，<see cref="Impl"/> 为 null，全部调用静默丢弃。
/// </para>
/// </summary>
public static class Haptics
{
    /// <summary>平台实现：amplitude 1..255（255 = 满幅），durationMs 毫秒。</summary>
    public static Action<int, int>? Impl { get; set; }

    /// <summary>当前平台是否有触觉能力（供界面决定是否给相关提示，不参与功能分支）。</summary>
    public static bool Available => Impl is not null;

    /// <summary>原始调用。参数与 Flutter <c>Vibration.vibrate</c> 一致。</summary>
    public static void Vibrate(int amplitude, int durationMs) => Impl?.Invoke(amplitude, durationMs);

    /// <summary>
    /// 记一条拍板：正常次 128/150ms，假条 240/900ms。
    /// 对齐原版 <c>lib/pages/record_page.dart:296-299</c> 的
    /// <c>currentTkType == TakeType.fake ? Vibration.vibrate(240, 900) : Vibration.vibrate(128, 150)</c>。
    /// 「保存并结束本镜」走正常分支（原版 end 落在 else 支），所以调用方传 <paramref name="fake"/>=false 即可。
    /// </summary>
    public static void TakeSaved(bool fake) => Vibrate(fake ? 240 : 128, fake ? 900 : 150);

    /// <summary>撤回上一条：240/900ms（原版 <c>record_page.dart:347-348</c> 的 <c>drawBackItem()</c>）。</summary>
    public static void Undone() => Vibrate(240, 900);
}