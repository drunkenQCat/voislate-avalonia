using Avalonia;

namespace VoiSlate;

internal static class Program
{
    // Avalonia 配置。勿用 *Attribute 内联扩展点，保持与 DI 生命周期解耦（契约 v0.5 ADR-008）。
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}