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
                _services.GetRequiredService<ISeedService>().EnsureSeededAsync(ct).GetAwaiter().GetResult();
                _services.GetRequiredService<RecordingSessionViewModel>().Initialization.GetAwaiter().GetResult();
                _services.GetRequiredService<ITakeFlowService>().InitializeAsync(ct).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Startup failed");
            }
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
            //  3) 不能把 MainWindow（Window）交给 MainViewFactory：Android 的 IWindowingPlatform 是
            //     WindowingPlatformStub，其 CreateWindow() 抛 NotSupportedException（new Window() 即崩），
            //     必须返回普通 Control——见 CreateAndroidMainView（与 MainWindow.axaml 同构的导航壳）。
            //  4) 时序：AvaloniaAndroidApplication<TApp>.OnCreate（[Application] 类）先于 Activity 执行，
            //     SetupWithLifetime 触发本方法时 ApplicationLifetime 已是 IActivityApplicationLifetime，
            //     Activity 的 InitializeAvaloniaView 必然晚于 MainViewFactory 赋值（官方样本同此依赖）。
            var mainViewModel = _services.GetRequiredService<MainViewModel>();
            androidLifetime.MainViewFactory = () => CreateAndroidMainView(mainViewModel);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Android 主视图：与 Views/MainWindow.axaml 同构的手机壳导航壳（AppBar 40px + 页面宿主 +
    /// 底部 3 Tab）。设置入口在 AppBar 右上角（settings 齿轮 → NavigateCommand(settings)；
    /// 设置页打开时 AppBar/底部导航按 IsAppBarVisible/IsTabBarVisible 隐藏，设置页自带返回头）。
    /// 页面本身由 App.axaml 的 VM→View DataTemplate 渲染——与桌面共用同一套页面。
    /// 维护约定：后续改动 MainWindow.axaml 的壳结构时须同步本方法。
    /// </summary>
    private static Control CreateAndroidMainView(MainViewModel mainViewModel)
    {
        // 与 MainWindow.axaml 三枚底部导航 PathIcon 逐字一致（保持视觉同一；顺序：计划/记录/场记，
        // 识别测试页按迁移清单不迁移）。设置齿轮图标同 MainWindow.axaml。
        IReadOnlyDictionary<string, string> iconData = new Dictionary<string, string>
        {
            [MainViewModel.SchedulePageKey] =
                "M19,4H18V2H16V4H8V2H6V4H5C3.9,4 3,4.9 3,6V20C3,21.1 3.9,22 5,22H19C20.1,22 21,21.1 21,20V6C21,4.9 20.1,4 19,4Z M19,20H5V9H19V20Z",
            [MainViewModel.RecordPageKey] =
                "M12,14C13.66,14 15,12.66 15,11V5C15,3.34 13.66,2 12,2C10.34,2 9,3.34 9,5V11C9,12.66 10.34,14 12,14Z M18,11C18,14.53 15.06,17.44 11.53,17.5C8.08,17.56 5,14.93 5,11H3.13C3.13,14.66 6.31,17.66 10,17.9V21H14V17.9C17.69,17.66 20.87,14.66 20.87,11H18Z",
            [MainViewModel.SlateLogPageKey] =
                "M3,13H11V11H3ZM3,17H11V15H3ZM3,9H11V7H3ZM13,13H21V11H13ZM13,17H21V15H13ZM13,9H21V7H13Z",
        };
        const string gearIcon =
            "M19.43,12.98C19.47,12.66 19.5,12.34 19.5,12C19.5,11.66 19.47,11.34 19.43,11.02L21.54,9.37C21.73,9.22 21.78,8.95 21.66,8.73L19.66,5.27C19.54,5.05 19.27,4.97 19.05,5.05L16.56,6.05C16.04,5.66 15.48,5.32 14.87,5.07L14.5,2.42C14.46,2.18 14.25,2 14,2L10,2C9.75,2 9.54,2.18 9.5,2.42L9.13,5.07C8.52,5.32 7.96,5.66 7.44,6.05L4.95,5.05C4.73,4.97 4.46,5.05 4.34,5.27L2.34,8.73C2.22,8.95 2.27,9.22 2.46,9.37L4.57,11.02C4.53,11.34 4.5,11.66 4.5,12C4.5,12.34 4.53,12.66 4.57,12.98L2.46,14.63C2.27,14.78 2.22,15.05 2.34,15.27L4.34,18.73C4.46,18.95 4.73,19.03 4.95,18.95L7.44,17.95C7.96,18.34 8.52,18.68 9.13,18.93L9.5,21.58C9.54,21.82 9.75,22 10,22L14,22C14.25,22 14.46,21.82 14.5,21.58L14.87,18.93C15.48,18.68 16.04,18.34 16.56,17.95L19.05,18.95C19.27,19.03 19.54,18.95 19.66,18.73L21.66,15.27C21.78,15.05 21.73,14.78 21.54,14.63ZM12,15.5C10.07,15.5 8.5,13.93 8.5,12C8.5,10.07 10.07,8.5 12,8.5C13.93,8.5 15.5,10.07 15.5,12C15.5,13.93 13.93,15.5 12,15.5Z";

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(new GridLength(40)),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
            },
            DataContext = mainViewModel,
        };

        // ---- AppBar（40px #266489；标题 + 右上角设置齿轮）----
        var appBar = new Border { Background = new SolidColorBrush(Color.Parse("#266489")), ZIndex = 5 };
        Grid.SetRow(appBar, 0);
        var appBarGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
        };
        appBarGrid.Children.Add(new TextBlock
        {
            Text = "VoiSlate",
            FontSize = 17,
            FontWeight = FontWeight.Medium,
            Foreground = Brushes.White,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
        var settingsButton = new Button
        {
            Width = 40,
            Height = 40,
            Margin = new Thickness(0, 0, 2, 0),
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Content = new PathIcon
            {
                Data = StreamGeometry.Parse(gearIcon),
                Width = 20,
                Height = 20,
                Foreground = Brushes.White,
            },
        };
        settingsButton.Command = mainViewModel.NavigateCommand;
        settingsButton.CommandParameter = MainViewModel.SettingsPageKey;
        Grid.SetColumn(settingsButton, 1);
        appBarGrid.Children.Add(settingsButton);
        appBar.Child = appBarGrid;
        appBar[!Visual.IsVisibleProperty] = new Binding(nameof(MainViewModel.IsAppBarVisible));
        root.Children.Add(appBar);

        // ---- 页面宿主：Content ↔ MainViewModel.CurrentPage ----
        var pageHost = new ContentControl();
        pageHost.Bind(ContentControl.ContentProperty, new Binding(nameof(MainViewModel.CurrentPage)));
        Grid.SetRow(pageHost, 1);
        root.Children.Add(pageHost);

        // ---- 底部导航（3 Tab：计划 / 记录 / 场记；active 高亮同 MainWindow.axaml）----
        var bottomNav = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#E0E0E0")),
            BorderThickness = new Thickness(0, 1, 0, 0),
            ZIndex = 30,
        };
        Grid.SetRow(bottomNav, 2);
        bottomNav[!Visual.IsVisibleProperty] = new Binding(nameof(MainViewModel.IsTabBarVisible));

        var highlight = new PageKeyToBrushConverter();
        var navGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
            },
        };
        string[] tabOrder = [MainViewModel.SchedulePageKey, MainViewModel.RecordPageKey, MainViewModel.SlateLogPageKey];
        for (var i = 0; i < tabOrder.Length; i++)
        {
            var key = tabOrder[i];
            var title = key == MainViewModel.RecordPageKey ? "记录"
                : key == MainViewModel.SchedulePageKey ? "计划" : "场记";
            var button = new Button
            {
                Command = mainViewModel.NavigateCommand,
                CommandParameter = key,
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(5, 3),
                Padding = new Thickness(0, 3),
                Content = new StackPanel
                {
                    Children =
                    {
                        new PathIcon
                        {
                            Data = StreamGeometry.Parse(iconData[key]),
                            Width = 20,
                            Height = 20,
                            HorizontalAlignment = HorizontalAlignment.Center,
                        },
                        new TextBlock
                        {
                            Text = title,
                            FontSize = 11,
                            HorizontalAlignment = HorizontalAlignment.Center,
                        },
                    },
                },
            };
            // 与 MainWindow.axaml 同款“当前页高亮”（active 淡蓝底）。
            button.Bind(
                Button.BackgroundProperty,
                new Binding(nameof(MainViewModel.CurrentPageKey))
                {
                    Converter = highlight,
                    ConverterParameter = key,
                });
            Grid.SetColumn(button, i);
            navGrid.Children.Add(button);
        }

        bottomNav.Child = navGrid;
        root.Children.Add(bottomNav);
        return root;
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
        // 计划页数据面：真实实现（LiteDb 落库 + SeedData 播种：1A 万星园 / 2A 洛肯实验室）。
        // 换用 LiteDbScheduleStore 后计划页不再空表（对齐 voislate-html 计划页数据）。
        services.AddSingleton<IScheduleStore, LiteDbScheduleStore>();
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