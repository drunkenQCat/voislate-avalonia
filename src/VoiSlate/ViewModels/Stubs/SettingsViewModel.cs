using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiSlate.Models;
using VoiSlate.Services;

namespace VoiSlate.ViewModels;

/// <summary>
/// Agent C stub：设置页 VM（契约 §4 SettingsViewModel —— Agent B 产出，本文件为编译用占位，合并后删除）。
/// 契约成员：ProjectName/TodayCount + ClearToday/ExportAll（重置类需 DialogService 确认，契约 R12）。
/// stub 附加：链接符/前缀/联动/ASR Mock 状态展示与保存（对齐原版 settings_configue_page）。
/// 链接符/前缀经 ITakeFlowService 唯一写入口（C-2/BLOCKER-1）。
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISessionSettingsStore _store;
    private readonly ITakeFlowService _flow;
    private readonly ILogRepository _logs;
    private readonly IAsrService _asr;
    private readonly ITimeProvider _time;
    private readonly IToastService _toast;

    [ObservableProperty]
    private string _projectName = "VoiSlate";

    [ObservableProperty]
    private int _todayCount;

    [ObservableProperty]
    private string _recordLinker = "-T";

    [ObservableProperty]
    private PrefixType _prefixMode = PrefixType.Default;

    [ObservableProperty]
    private string _customPrefix = "custom";

    [ObservableProperty]
    private bool _isLinked = true;

    [ObservableProperty]
    private string _asrStatus = "Mock ASR（ifly 识别由 Mock 替代：接口先行，无真实识别）";

    /// <summary>前缀模式可选值（组合框 ItemsSource）。</summary>
    public IReadOnlyList<PrefixType> PrefixModes { get; } = Enum.GetValues<PrefixType>();

    /// <summary>自定义前缀输入是否可用（仅 Custom 模式）。</summary>
    public bool IsCustomPrefixEnabled => PrefixMode == PrefixType.Custom;

    public RelayCommand SaveProjectCommand { get; }

    public RelayCommand SaveLinkerCommand { get; }

    public RelayCommand SavePrefixCommand { get; }

    public RelayCommand ExportAllCommand { get; }

    public AsyncRelayCommand ClearTodayCommand { get; }

    public SettingsViewModel(
        ISessionSettingsStore store,
        ITakeFlowService flow,
        ILogRepository logs,
        IAsrService asr,
        ITimeProvider time,
        IToastService toast)
    {
        _store = store;
        _flow = flow;
        _logs = logs;
        _asr = asr;
        _time = time;
        _toast = toast;

        SaveProjectCommand = new RelayCommand(SaveProject);
        SaveLinkerCommand = new RelayCommand(SaveLinker);
        SavePrefixCommand = new RelayCommand(SavePrefix);
        ExportAllCommand = new RelayCommand(() => _toast.Show("导出所有场记（占位：待 IExportService，契约 §3；原版=跨日合并导出，F2）"));
        ClearTodayCommand = new AsyncRelayCommand(ClearTodayAsync);

        _ = HydrateAsync();
    }

    partial void OnIsLinkedChanged(bool value) => _ = _store.SetAsync(SessionKeys.IsLinked, value);

    partial void OnPrefixModeChanged(PrefixType value) => OnPropertyChanged(nameof(IsCustomPrefixEnabled));

    private async Task HydrateAsync()
    {
        try
        {
            RecordLinker = await _store.GetStringAsync(SessionKeys.RecordLinker, "-T");
            PrefixMode = PrefixTypeExtensions.ParseSettings(await _store.GetStringAsync(SessionKeys.PrefixType, "default"));
            CustomPrefix = await _store.GetStringAsync(SessionKeys.CustomPrefix, "custom");
            IsLinked = await _store.GetBoolAsync(SessionKeys.IsLinked, true);
            ProjectName = await _store.GetStringAsync("project", "VoiSlate");
            AsrStatus = _asr.IsAvailable
                ? "Mock ASR（可用；ifly 识别由 Mock 替代，接口先行）"
                : "Mock ASR（不可用）";
            await RefreshTodayCountAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Settings hydrate failed: {ex.Message}");
        }
    }

    private async Task RefreshTodayCountAsync()
    {
        try
        {
            var logs = await _logs.GetByDateAsync(VoiSlateDates.TodayKey(_time.Now));
            TodayCount = logs.Count;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Settings today count failed: {ex.Message}");
        }
    }

    private void SaveProject()
    {
        _ = _store.SetAsync("project", ProjectName);
        _toast.Show("工程名已保存");
    }

    private void SaveLinker()
    {
        _ = _flow.SetLinkerAsync(RecordLinker, CancellationToken.None);
        _toast.Show($"链接符已保存：{RecordLinker}（经 ITakeFlowService）");
    }

    private void SavePrefix()
    {
        _ = _flow.SetPrefixAsync(PrefixMode, CustomPrefix, CancellationToken.None);
        _toast.Show($"前缀模式已保存：{PrefixMode}（经 ITakeFlowService）");
    }

    private async Task ClearTodayAsync()
    {
        // 占位：正式实现 = DialogService 强确认（契约 R12）+ ITakeFlowService 清今日 API（E 演进）。
        _toast.Show("清空今日场记（占位：确认对话框 + 清空 API 待 E/B 交付）");
        await Task.CompletedTask;
    }
}