using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using LiteDB;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using VoiSlate.Infrastructure;
using VoiSlate.Models;
using VoiSlate.Services;
using VoiSlate.ViewModels;

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

        if (ApplicationLifetime is IActivityApplicationLifetime)
        {
            // Android（Avalonia 12）：进程启动 ANR 预算（约 40s）内不能同步做 seed/初始化——
            // TCG 软件模拟下 attach 阶段极慢，同步等待必超线被杀。改为后台线程推迟执行，
            // 主线程只做轻量接线（下方分支立即挂 MainViewFactory），UI 先出、数据后到。
            // 桌面路径（else 分支）语义与原先完全一致：同步 seed 后才建 MainWindow。
            _ = Task.Run(async () =>
            {
                try
                {
                    var ct = CancellationToken.None;
                    using var scope = _services.CreateScope();
                    await _services.GetRequiredService<ISeedService>().EnsureSeededAsync(ct);
                    await _services.GetRequiredService<RecordingSessionViewModel>().Initialization;
                    await _services.GetRequiredService<ITakeFlowService>().InitializeAsync(ct);
                    Log.Information("Android deferred startup seeding completed");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Startup failed (android deferred)");
                }
            });
        }
        else
        {
            try
            {
                var ct = CancellationToken.None;
                using var scope = _services.CreateScope();
                if (_services.GetRequiredService<ISessionSettingsStore>().GetStringAsync(Desktop.DesktopWorkspaceService.SessionKey).GetAwaiter().GetResult() is null)
                    _services.GetRequiredService<ISeedService>().EnsureSeededAsync(ct).GetAwaiter().GetResult();
                // The Windows workspace owns its session; do not initialize the legacy take writer.
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Startup failed");
            }
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 桌面：工作台（时间轴 + 百炼语音）。控件本体 DesktopWorkspaceView 与 Android 共用。
            var workspace = CreateWorkspace(_services);
            workspace.InitializeAsync().GetAwaiter().GetResult();
            desktop.MainWindow = new Desktop.DesktopWorkspaceWindow(workspace, CreateSpeechSettings(_services));

            // DI-REGISTRATION (C keep) — Agent C 接管 App.axaml.cs 时全量保留本区域。
            desktop.Exit += (_, _) => OnShutdown();
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime androidLifetime)
        {
            // Android 内容接线（Avalonia 12.1.1，逐条依据见下）：
            //  1) IActivityApplicationLifetime.MainViewFactory（Func<Control>?）是官方唯一的内容接线点：
            //     AvaloniaMainActivity（12.1.1 源，非泛型）在 OnCreate 时经
            //     InitializeAvaloniaView(object? initialContent) 调用
            //     `initialContent ??= lifetime.MainViewFactory?.Invoke()` 并把产物挂进 AvaloniaView；
            //     本路由与官方 ControlCatalog/App.xaml.cs 的 IActivityApplicationLifetime 分支逐字同构。
            //  2) 本接口定义在核心 Avalonia.Controls 包
            //     （Avalonia.Controls.ApplicationLifetimes.IActivityApplicationLifetime）——主工程是纯
            //     net10.0 桌面 TFM、不引用 Avalonia.Android，桌面 lifetime（ClassicDesktop…）不实现该接口，
            //     故此分支对桌面完全惰性（桌面行为零变化）。
            //  3) 不能把 Window 交给 MainViewFactory：Android 的 IWindowingPlatform 是
            //     WindowingPlatformStub，其 CreateWindow() 抛 NotSupportedException（new Window() 即崩），
            //     必须返回普通 Control——内容是 DesktopWorkspaceView（UserControl），弹窗走同控件的 overlay 层。
            //  4) 时序：AvaloniaAndroidApplication<TApp>.OnCreate（[Application] 类）先于 Activity 执行，
            //     SetupWithLifetime 触发本方法时 ApplicationLifetime 已是 IActivityApplicationLifetime，
            //     Activity 的 InitializeAvaloniaView 必然晚于 MainViewFactory 赋值（官方样本同此依赖）。
            // 内容 = 与桌面同一套工作台界面（DesktopWorkspaceView），窄屏时它自己切成底部 Tab。
            var workspace = CreateWorkspace(_services);
            var speechSettings = CreateSpeechSettings(_services);
            var view = new Desktop.DesktopWorkspaceView(workspace, speechSettings,
                // 采集器由平台工程注入（Android 在 MainActivity.OnCreate 里注册 AudioRecord 实现）；
                // 用惰性 lambda 而不是取当下的 PcmCapture.Factory 值，避免注册时机早/晚造成的空引用。
                captureFactory: deviceNumber => Desktop.Speech.PcmCapture.Create(deviceNumber));
            androidLifetime.MainViewFactory = () => view;
            // 初始化（读库 + 迁移 + 首次 seed）放到后台，完成后再让视图重画一次：
            // 进程启动 ANR 预算内不能同步做 IO，但空白壳先出、数据后到对用户是可接受的。
            _ = Task.Run(async () =>
            {
                try
                {
                    await workspace.InitializeAsync();
                    Avalonia.Threading.Dispatcher.UIThread.Post(view.Refresh);
                    Log.Information("Android workspace initialized");
                }
                catch (Exception ex) { Log.Error(ex, "Android workspace initialization failed"); }
            });
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>工作台服务图：桌面与 Android 共用，避免两个平台各自拼装导致行为漂移。</summary>
    private static Desktop.DesktopWorkspaceService CreateWorkspace(IServiceProvider services) => new(
        new LiteDbScheduleStore(services.GetRequiredService<LiteDbStore>()),
        services.GetRequiredService<ILogRepository>(),
        services.GetRequiredService<ISessionSettingsStore>(),
        services.GetRequiredService<ITimeProvider>(),
        new Desktop.LiteDbWorkspaceTransaction(services.GetRequiredService<LiteDbStore>()));

    /// <summary>百炼设置：Windows 用 DPAPI 加密 Token，其它平台退到 base64 混淆（设置页会提示）。</summary>
    private static Desktop.Speech.BailianSettingsStore CreateSpeechSettings(IServiceProvider services) => new(
        services.GetRequiredService<ISessionSettingsStore>(),
        OperatingSystem.IsWindows() ? new Desktop.Speech.WindowsTokenProtector() : new Desktop.Speech.PlainTokenProtector());

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
        // 计划页数据面：真实实现（LiteDb 落库 + SeedData 播种：1A 万星园 / 2A 洛肯实验室）。
        // 换用 LiteDbScheduleStore 后计划页不再空表（对齐 voislate-html 计划页数据）。
        services.AddSingleton<IScheduleStore, LiteDbScheduleStore>();
        services.AddSingleton<ICsvScheduleParser, CsvScheduleParserService>();

        // P7 页面 ViewModel（Record/Schedule/SlateLog/SlateLogPage/Settings/Main/LogEditor/SlateColumn/Placeholder）
        // 已于 2026-10-26 整批删除：桌面与 Android 共用 Desktop/DesktopWorkspaceView，数据面由
        // DesktopWorkspaceService（+ Editing/Backup/Speech/Preferences/Legacy 分部类）承担。
        // 删除前的能力对照与迁移记录见 docs/p7-vm-coverage.md。
    }

    private void OnShutdown()
    {
        // 退出序（契约 v0.5 ADR-008）：停 PeriodicTimer → BackupService 备份 → 关库。
        _services?.Dispose();
        Log.CloseAndFlush();
    }
}