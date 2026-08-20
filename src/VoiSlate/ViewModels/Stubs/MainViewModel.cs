using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VoiSlate.ViewModels;

/// <summary>
/// Agent C stub：导航页描述（契约 §6 左侧导航条目；仅显示数据）。
/// B 交付正式 MainViewModel 后删除本文件（Stubs/ 整目录随 B/D 合入一并清理）。
/// </summary>
public sealed record NavPageItem(string Key, string Title);

/// <summary>
/// Agent C stub：主窗口导航 VM（契约 §4/§6——Agent B 产出，本文件为编译用占位，合并后删除）。
/// - CurrentPage（VM 实例）+ Pages + NavigateCommand；
/// - 记录页为 Scoped：进入经工厂创建并 Activate，离开 Deactivate 释放（契约 C-6/R15）；
/// - 其余页为单例（stub 简化；B 决定最终生命周期）。
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly Func<RecordViewModel> _recordPageFactory;
    private readonly RecordingSessionViewModel _session;
    private readonly ScheduleViewModel _schedule;
    private readonly SlateLogViewModel _slateLog;
    private readonly SettingsViewModel _settings;

    private RecordViewModel? _activeRecord;

    /// <summary>契约 §4：导航页集合（记录/计划/场记/设置）。</summary>
    public ObservableCollection<NavPageItem> Pages { get; } = [];

    [ObservableProperty]
    private object? _currentPage;

    /// <summary>当前页键（记录/计划/场记/设置；供左侧导航高亮）。</summary>
    [ObservableProperty]
    private string _currentPageKey = "Record";

    /// <summary>契约 §4：导航命令（参数 = 页键字符串）。</summary>
    public RelayCommand<string> NavigateCommand { get; }

    public MainViewModel(
        Func<RecordViewModel> recordPageFactory,
        RecordingSessionViewModel session,
        ScheduleViewModel schedule,
        SlateLogViewModel slateLog,
        SettingsViewModel settings)
    {
        _recordPageFactory = recordPageFactory;
        _session = session;
        _schedule = schedule;
        _slateLog = slateLog;
        _settings = settings;

        Pages = new ObservableCollection<NavPageItem>
        {
            new("Record", "记录"),
            new("Schedule", "计划"),
            new("SlateLog", "场记"),
            new("Settings", "设置"),
        };

        NavigateCommand = new RelayCommand<string>(NavigateTo);
        NavigateTo("Record"); // 初始记录页（对齐原版 initialIndex=1）
    }

    private void NavigateTo(string? key)
    {
        key ??= "Record";
        if (key == CurrentPageKey && CurrentPage != null) return;

        // 离开记录页：Scoped 释放（契约 C-6：进入创建 / 退出释放）
        if (key != "Record" && _activeRecord != null)
        {
            _activeRecord.Deactivate();
            _activeRecord = null;
            CurrentPage = null;
        }

        switch (key)
        {
            case "Schedule":
                CurrentPage = _schedule;
                break;
            case "SlateLog":
                CurrentPage = _slateLog;
                break;
            case "Settings":
                CurrentPage = _settings;
                break;
            default:
                key = "Record";
                var record = _recordPageFactory();
                _activeRecord = record;
                record.Activate();
                CurrentPage = record;
                break;
        }

        CurrentPageKey = key;
        _session.ResetOkStatus();
    }
}