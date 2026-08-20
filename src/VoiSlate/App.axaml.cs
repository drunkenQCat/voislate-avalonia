using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using VoiSlate.Views;

namespace VoiSlate;

public partial class App : Application
{
    private ServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // ---- 启动时序（契约 v0.5 ADR-008 / N4）----
        // LiteDbStore.Open → SeedService.EnsureSeededAsync → IDayRolloverService.OnStartup
        // →（M0 占位：P0.5 在此插入上述调用）→ 装配 DI → MainWindow。
        var services = new ServiceCollection();
        ConfigureServices(services);
        _services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new ViewModels.PlaceholderViewModel(),
            };

            // DI-REGISTRATION (C keep) — Agent C 接管 App.axaml.cs 时全量保留本区域。
            // 后续 VM/Service 注册在此追加（契约 v0.5：FILE C-5 / C-3 移交清单）。
            desktop.ShutdownRequested += (_, _) => OnShutdown();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();

        // DI-REGISTRATION (C keep) — P0.5 起在此追加：LiteDbStore / ITakeFlowService /
        // ISessionState / SeedService / IDayRolloverService / IAsrService(Mock) / ... 
    }

    private void OnShutdown()
    {
        // 退出序（契约 v0.5 ADR-008）：停 PeriodicTimer → BackupService 备份 → 关库。
        _services?.Dispose();
        Log.CloseAndFlush();
    }
}