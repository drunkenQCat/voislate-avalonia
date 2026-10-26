using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace VoiSlate.Android;

/// <summary>
/// Android application class. Avalonia 12.1.1 requires the Avalonia <see cref="App"/> type to be
/// wired through AvaloniaAndroidApplication&lt;TApp&gt; (AvaloniaMainActivity is non-generic in 12.x).
/// </summary>
[Application]
public class AndroidApp : AvaloniaAndroidApplication<App>
{
    protected AndroidApp(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
        // 进程里最早能跑托管代码的位置（早于任何 Activity 与 Avalonia 初始化）。
        // 触觉反馈必须在这里挂上：视图是在 Avalonia 初始化期间构造的，那时 DI 实例还没建好。
        AndroidHaptics.Install();
        // 现场排障出口：核心工程的手势/语音链路日志经此写 logcat（adb logcat -s VoiSlateTrace）。
        VoiSlate.Services.Diagnostics.Sink = message => global::Android.Util.Log.Info("VoiSlateTrace", message);
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        => base.CustomizeAppBuilder(builder).WithInterFont();
}