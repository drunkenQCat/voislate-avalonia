using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LiteDB;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using VoiSlate.Infrastructure;
using VoiSlate.Models;
using VoiSlate.Services;
using VoiSlate.Views;

namespace VoiSlate;

public partial class App : Application
{
    private ServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // ---- 启动时序（契约 v0.5 ADR-008，P0.5 落实）----
        // LiteDbStore.Open → SeedService.EnsureSeededAsync → IDayRolloverService.OnStartup(P0.5 占位跳过)
        // → 装配 DI → ITakeFlowService.InitializeAsync → MainWindow。
        var services = new ServiceCollection();
        ConfigureServices(services);
        _services = services.BuildServiceProvider();

        try
        {
            var ct = CancellationToken.None;
            using var scope = _services.CreateScope();
            _services.GetRequiredService<ISeedService>().EnsureSeededAsync(ct).GetAwaiter().GetResult();
            _services.GetRequiredService<ITakeFlowService>().InitializeAsync(ct).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Startup failed");
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new ViewModels.PlaceholderViewModel(),
            };

            // DI-REGISTRATION (C keep) — Agent C 接管 App.axaml.cs 时全量保留本区域。
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

        // ==== DI-REGISTRATION (C keep) — P0.5 垂直切片注册（Agent C 全量保留） ====
        services.AddSingleton(new LiteDbStore(LiteDbStore.DefaultConnectionString()));
        services.AddSingleton<ITimeProvider, SystemTimeProvider>();
        services.AddSingleton<IFileNamingService>(sp =>
        {
            var fn = new FileNumberingService(sp.GetRequiredService<ITimeProvider>());
            return fn;
        });
        services.AddSingleton<ISessionState, SessionStateImpl>();
        services.AddSingleton<ISessionSettingsStore, LiteDbSessionSettingsStore>();
        services.AddSingleton<ILogRepository, LiteDbLogRepository>();
        services.AddSingleton<IPickerHistoryStore, LiteDbPickerHistoryStore>();
        services.AddSingleton<IScheduleBook, LiteDbScheduleBook>();
        services.AddSingleton<ISeedService, SeedService>();
        services.AddSingleton<IAsrService, MockAsrService>();
        services.AddSingleton<IToastService, NoopToastService>();
        services.AddSingleton<IHapticsService, NoopHapticsService>();
        services.AddSingleton<ITakeFlowService>(sp =>
        {
            var svc = new TakeFlowService(
                sp.GetRequiredService<ILogRepository>(),
                sp.GetRequiredService<IPickerHistoryStore>(),
                sp.GetRequiredService<ISessionState>(),
                sp.GetRequiredService<IFileNamingService>(),
                sp.GetRequiredService<ISessionSettingsStore>(),
                sp.GetRequiredService<ITimeProvider>(),
                sp.GetRequiredService<IHapticsService>(),
                sp.GetRequiredService<IToastService>());
            var book = sp.GetRequiredService<IScheduleBook>();
            svc.SceneLabelProvider = () => book.SceneLabel(0);
            svc.ShotLabelProvider = () => book.ShotLabel(0, 0);
            svc.CurrentObjectsProvider = () => book.ObjectsOf(0, 0);
            return svc;
        });
    }

    private void OnShutdown()
    {
        // 退出序（契约 v0.5 ADR-008）：停 PeriodicTimer → BackupService 备份 → 关库。
        _services?.Dispose();
        Log.CloseAndFlush();
    }
}