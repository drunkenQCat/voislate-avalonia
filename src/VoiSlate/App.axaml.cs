using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LiteDB;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using VoiSlate.Infrastructure;
using VoiSlate.Models;
using VoiSlate.Services;
using VoiSlate.ViewModels;
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
            _services.GetRequiredService<RecordingSessionViewModel>().Initialization.GetAwaiter().GetResult();
            _services.GetRequiredService<ITakeFlowService>().InitializeAsync(ct).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Startup failed");
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Agent C：MainWindow 壳 + 契约 MainViewModel（stub 版；B 合入后替换 DI 注册即可）。
            desktop.MainWindow = new MainWindow
            {
                DataContext = _services.GetRequiredService<MainViewModel>(),
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
        services.AddSingleton(sp => new RecordingSessionViewModel(
            sp.GetRequiredService<ISessionSettingsStore>(),
            sp.GetRequiredService<ITimeProvider>()));
        services.AddSingleton<ISessionState>(sp => sp.GetRequiredService<RecordingSessionViewModel>());
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

        // ==== 集成接线（C 合入后按 B 真实 VM 构造器重写；新增缺补服务生产实现）====
        services.AddSingleton<IHardwareKeyService, NoopHardwareKeyService>();
        services.AddSingleton<IExportService, ExportService>();
        services.AddSingleton<IScheduleStore, NoopScheduleStore>();
        services.AddSingleton<ICsvScheduleParser, CsvScheduleParserService>();

        // RecordViewModel：Scoped 生命周期（契约 C-6：进入创建 / 退出释放）——经工厂按页创建。
        services.AddSingleton<Func<RecordViewModel>>(sp => () => new RecordViewModel(
            sp.GetRequiredService<ISessionSettingsStore>(),
            sp.GetRequiredService<ITakeFlowService>(),
            sp.GetRequiredService<IAsrService>(),
            sp.GetRequiredService<IHardwareKeyService>(),
            sp.GetRequiredService<RecordingSessionViewModel>(),
            sp.GetRequiredService<ITimeProvider>(),
            sp.GetRequiredService<ILogRepository>()));

        // 场记页：扁平列表 VM（C 的 SlateLogView 数据面；MainViewModel 导航该实例）。
        services.AddSingleton<SlateLogViewModel>(sp => new SlateLogViewModel(
            sp.GetRequiredService<ITakeFlowService>(),
            sp.GetRequiredService<ILogRepository>(),
            sp.GetRequiredService<ITimeProvider>(),
            sp.GetRequiredService<IExportService>()));

        services.AddSingleton<ScheduleViewModel>(sp => new ScheduleViewModel(
            sp.GetRequiredService<IScheduleStore>(),
            sp.GetRequiredService<ICsvScheduleParser>(),
            sp.GetRequiredService<RecordingSessionViewModel>()));

        services.AddSingleton<SettingsViewModel>(sp => new SettingsViewModel(
            sp.GetRequiredService<ISessionSettingsStore>(),
            sp.GetRequiredService<ILogRepository>(),
            sp.GetRequiredService<ITakeFlowService>(),
            sp.GetRequiredService<IExportService>(),
            sp.GetRequiredService<ITimeProvider>(),
            sp.GetRequiredService<RecordingSessionViewModel>()));

        services.AddSingleton<MainViewModel>(sp => new MainViewModel(
            sp.GetRequiredService<Func<RecordViewModel>>(),
            sp.GetRequiredService<ScheduleViewModel>(),
            sp.GetRequiredService<SlateLogViewModel>(),
            sp.GetRequiredService<SettingsViewModel>()));
    }

    private void OnShutdown()
    {
        // 退出序（契约 v0.5 ADR-008）：停 PeriodicTimer → BackupService 备份 → 关库。
        _services?.Dispose();
        Log.CloseAndFlush();
    }
}