using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiSlate.Models;
using VoiSlate.Services;

namespace VoiSlate.ViewModels;

/// <summary>
/// 设置页 VM（契约 §4 SettingsViewModel：ProjectName / TodayCount | ClearToday / ExportAll）。
/// ClearToday 经 ITakeFlowService.DeleteItemAsync 循环（保持“日志写唯一入口”纪律；E 可演进为服务内 ClearTodayAsync）。
/// ExportAll 经 IExportService（B 补桩 Noop，演进权 E）。
/// 原版“清空所有场记/清空所有拍摄计划”（重置 + 退出应用）不在 v0.5 VM 契约行，由 C/E 后续覆盖（报告注明）。
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    /// <summary>工程名键（原版 Hive box 'settings' key 'project' → 本存储扩展键，非 SessionKeys 13 键）。</summary>
    public const string ProjectKey = "project";

    private readonly ISessionSettingsStore _settings;
    private readonly ILogRepository _logs;
    private readonly ITakeFlowService _takeFlow;
    private readonly IExportService _exporter;
    private readonly ITimeProvider _time;
    private readonly RecordingSessionViewModel _session;

    /// <summary>初始化加载任务（存储同步外表，通常构造内即完成）。</summary>
    public Task Initialization { get; }

    public SettingsViewModel(
        ISessionSettingsStore settings,
        ILogRepository logs,
        ITakeFlowService takeFlow,
        IExportService exporter,
        ITimeProvider time,
        RecordingSessionViewModel session)
    {
        _settings = settings;
        _logs = logs;
        _takeFlow = takeFlow;
        _exporter = exporter;
        _time = time;
        _session = session;
        _session.PropertyChanged += OnSessionPropertyChanged;
        Initialization = InitializeAsync();
    }

    [ObservableProperty]
    private string _projectName = string.Empty;

    /// <summary>今日场记条数（首次进入设置页时刷新）。</summary>
    [ObservableProperty]
    private int _todayCount;

    /// <summary>链接符（文本框缓冲；保存经 ITakeFlowService.SetLinkerAsync 唯一写入口 B1）。</summary>
    [ObservableProperty]
    private string _recordLinker = string.Empty;

    /// <summary>前缀模式显示串（三模式 B6）。</summary>
    [ObservableProperty]
    private string _prefixMode = DefaultPrefixMode;

    /// <summary>自定义前缀文本（custom 模式生效）。</summary>
    [ObservableProperty]
    private string _customPrefix = "custom";

    /// <summary>自定义前缀输入是否可用（PrefixMode=="自定义"）。</summary>
    [ObservableProperty]
    private bool _isCustomPrefixEnabled;

    /// <summary>补录联动（直连会话单例，单一事实来源；保存即写 SessionKeys）。</summary>
    public bool IsLinked
    {
        get => _session.IsLinked;
        set => _session.SetLink(value);
    }

    private const string DefaultPrefixMode = "默认（日期 yymmdd）";
    private const string SoundDevicesPrefixMode = "声音设备（yyYmMd）";
    private const string CustomPrefixMode = "自定义";

    /// <summary>前缀模式下拉选项（三模式 B6；显示串 ↔ SessionKeys.PrefixType）。</summary>
    public IReadOnlyList<string> PrefixModes { get; } =
        [DefaultPrefixMode, SoundDevicesPrefixMode, CustomPrefixMode];

    private static string PrefixModeOf(string settingsValue) => settingsValue switch
    {
        "sound devices" => SoundDevicesPrefixMode,
        "custom" => CustomPrefixMode,
        _ => DefaultPrefixMode,
    };

    private async Task InitializeAsync()
    {
        ProjectName = await _settings.GetStringAsync(ProjectKey, string.Empty);
        RecordLinker = _session.RecordLinker;
        CustomPrefix = _session.CustomPrefix;
        PrefixMode = PrefixModeOf(_session.PrefixType);
        IsCustomPrefixEnabled = PrefixMode == CustomPrefixMode;
        await LoadAsync();
    }

    partial void OnPrefixModeChanged(string value) => IsCustomPrefixEnabled = value == CustomPrefixMode;

    private void OnSessionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RecordingSessionViewModel.IsLinked):
                OnPropertyChanged(nameof(IsLinked));
                break;
            case nameof(RecordingSessionViewModel.RecordLinker):
                if (RecordLinker != _session.RecordLinker)
                {
                    RecordLinker = _session.RecordLinker;
                }

                break;
            case nameof(RecordingSessionViewModel.PrefixType):
                PrefixMode = PrefixModeOf(_session.PrefixType);
                break;
        }
    }

    /// <summary>刷新今日计数（C 于设置页激活时调用）。</summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        var today = VoiSlateDates.TodayKey(_time.Now);
        TodayCount = (await _logs.GetByDateAsync(today)).Count;
        ct.ThrowIfCancellationRequested();
    }

    partial void OnProjectNameChanged(string value) => _ = _settings.SetAsync(ProjectKey, value);

    /// <summary>清空今日场记（对齐原版 clearTodayLogs：仅清日志、不动文件号/picker_history；DialogService 强确认后由 C 触发）。</summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task ClearToday()
    {
        var today = VoiSlateDates.TodayKey(_time.Now);
        var count = (await _logs.GetByDateAsync(today)).Count;
        for (var i = count - 1; i >= 0; i--)
        {
            await _takeFlow.DeleteItemAsync(i, CancellationToken.None);
        }

        await LoadAsync();
    }

    /// <summary>导出所有场记（跨日合并，不可逆；F2。原名带日期区分——E 的 IExportService 落盘实现）。</summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task ExportAll()
    {
        var all = new List<SlateLogItem>();
        foreach (var date in await _logs.GetDatesAsync())
        {
            all.AddRange(await _logs.GetByDateAsync(date));
        }

        var json = _exporter.SerializeLogs(all);
        await _exporter.SaveToFileAsync(string.Empty, "all.json", json); // 文件名带日期由 E 落盘时补充
    }

    /// <summary>显式保存工程名（OnProjectNameChanged 已即时持久化；本命令为兜底确认）。</summary>
    [RelayCommand]
    private void SaveProject()
    {
        if (!string.IsNullOrWhiteSpace(ProjectName))
        {
            _ = _settings.SetAsync(ProjectKey, ProjectName);
        }
    }

    /// <summary>保存链接符（唯一写入口 ITakeFlowService；会话/记录页实时联动）。</summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SaveLinker()
    {
        await _takeFlow.SetLinkerAsync(RecordLinker, CancellationToken.None);
        _session.SetRecordLinker(RecordLinker);
    }

    /// <summary>保存前缀模式（唯一写入口 ITakeFlowService；custom 模式同时持久化自定义文本）。</summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SavePrefix()
    {
        var mode = PrefixMode switch
        {
            SoundDevicesPrefixMode => PrefixType.SoundDevices,
            CustomPrefixMode => PrefixType.Custom,
            _ => PrefixType.Default,
        };
        await _takeFlow.SetPrefixAsync(mode, CustomPrefix, CancellationToken.None);
        _session.SetPrefixType(mode.ToSettingsValue());
        _session.SetCustomPrefix(CustomPrefix);
    }

    /// <summary>清理订阅（App 收尾）。</summary>
    public void Dispose() => _session.PropertyChanged -= OnSessionPropertyChanged;
}