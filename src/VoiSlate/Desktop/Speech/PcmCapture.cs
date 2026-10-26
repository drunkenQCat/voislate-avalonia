namespace VoiSlate.Desktop.Speech;

/// <summary>
/// 麦克风采集的平台注册表。核心工程（net10.0）只自带 Windows 实现（NAudio 的 WaveIn）；
/// 其它平台（Android）在自己的工程里实现 <see cref="IPcmCapture"/> 并在启动时注入工厂，
/// 这样同一套工作台界面在两个平台上都能拿到可用的采集器，而不必在主工程里 if/else 平台。
/// </summary>
public static class PcmCapture
{
    /// <summary>由平台工程注入：给定设备序号返回采集器。</summary>
    public static Func<int, IPcmCapture>? Factory { get; set; }

    /// <summary>由平台工程注入：可用输入设备名称列表。</summary>
    public static Func<IReadOnlyList<string>>? DeviceList { get; set; }

    /// <summary>由平台工程注入：运行时权限申请（Android 的 RECORD_AUDIO）；返回 false 表示被拒。</summary>
    public static Func<Task<bool>>? PermissionRequest { get; set; }

    public static IReadOnlyList<string> Devices() => DeviceList?.Invoke() ?? WindowsPcmCapture.Devices();

    public static IPcmCapture Create(int deviceNumber) => Factory?.Invoke(deviceNumber)
        ?? (OperatingSystem.IsWindows()
            ? new WindowsPcmCapture(deviceNumber)
            : throw new PlatformNotSupportedException("当前平台未注册麦克风采集实现"));
}