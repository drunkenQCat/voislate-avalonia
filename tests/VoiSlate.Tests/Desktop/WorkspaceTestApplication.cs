using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;


namespace VoiSlate.Tests.Desktop;

public sealed class WorkspaceTestApplication : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
}
public static class WorkspaceTestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<WorkspaceTestApplication>()
        .WithInterFont().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
