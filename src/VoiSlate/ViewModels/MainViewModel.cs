using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VoiSlate.ViewModels;

/// <summary>
/// 主导航页描述（契约 §6 MainWindow：左侧导航 + ContentControl ↔ CurrentPage；VM→View 映射经 App.axaml
/// DataTemplate（DataType={x:Type vm}），C 负责映射表注册）。
/// </summary>
public sealed class MainPageDescriptor
{
    public required string Title { get; init; }

    /// <summary>导航键（"record"/"schedule"/"slatelog"/"settings"）。</summary>
    public required string Key { get; init; }

    /// <summary>单例页实例（非 Record 页）。</summary>
    public object? Instance { get; init; }

    /// <summary>Scoped 页工厂（仅 Record 页：每次导航创建新实例，R15）。</summary>
    public Func<object>? Factory { get; init; }

    public object Resolve() => Factory?.Invoke() ?? Instance!;
}

/// <summary>
/// 主导航 VM（契约 §4 MainViewModel；B 产出——导航状态归 B，避免 C 自造 VM）。
/// Record 页 Scoped（每次进入经工厂创建、R15）；其余页复用单例。
/// 记录页激活钩子 Activate/Deactivate 由 C 在 RecordView.Loaded/Unloaded 调用（契约 B5），
/// 本 VM 不代劳，仅保证每次导航到记录页拿到全新实例。
/// </summary>
public partial class MainViewModel : ObservableObject
{
    public const string RecordPageKey = "record";
    public const string SchedulePageKey = "schedule";
    public const string SlateLogPageKey = "slatelog";
    public const string SettingsPageKey = "settings";

    private readonly Dictionary<string, MainPageDescriptor> _byKey;

    public MainViewModel(
        Func<RecordViewModel> recordPageFactory,
        ScheduleViewModel schedulePage,
        SlateLogViewModel slateLogPage,
        SettingsViewModel settingsPage)
    {
        Pages =
        [
            new MainPageDescriptor { Title = "记录", Key = RecordPageKey, Factory = recordPageFactory },
            new MainPageDescriptor { Title = "计划", Key = SchedulePageKey, Instance = schedulePage },
            new MainPageDescriptor { Title = "场记", Key = SlateLogPageKey, Instance = slateLogPage },
            new MainPageDescriptor { Title = "设置", Key = SettingsPageKey, Instance = settingsPage },
        ];
        _byKey = Pages.ToDictionary(p => p.Key);

        // 原版启动即记录页（TabBarView 初始记录页）
        CurrentPage = Resolve(RecordPageKey);
        CurrentPageKey = RecordPageKey;
    }

    public IReadOnlyList<MainPageDescriptor> Pages { get; }

    /// <summary>当前页 VM 实例（ContentControl.Content；VM→View 经 DataTemplate）。</summary>
    [ObservableProperty]
    private object? _currentPage;

    /// <summary>当前页导航键（C 的左侧导航高亮 PageKeyToBrushConverter 消费）。</summary>
    [ObservableProperty]
    private string _currentPageKey = string.Empty;

    [RelayCommand]
    private void Navigate(string? key)
    {
        if (key is null || !_byKey.TryGetValue(key, out var page))
        {
            return;
        }

        CurrentPage = page.Factory is null ? page.Instance : page.Factory();
        CurrentPageKey = key;
    }

    private object Resolve(string key) => _byKey[key].Resolve();
}