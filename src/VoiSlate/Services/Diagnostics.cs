using System;

namespace VoiSlate.Services;

/// <summary>
/// 极简诊断输出口：核心工程不能直接引用 Android API，平台层在启动最早的时刻挂上 Sink
/// （Android 写 logcat，桌面默认写控制台），现场排障时 `adb logcat -s VoiSlateTrace` 就能看到手势/语音链路。
/// </summary>
public static class Diagnostics
{
    public static Action<string>? Sink { get; set; }

    public static void Log(string message) => (Sink ?? Console.WriteLine)(message);
}
