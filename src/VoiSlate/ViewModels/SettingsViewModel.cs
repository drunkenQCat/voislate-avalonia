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

    /// <summary>初始化加载任务（存储同步外表，通常构造内即完成）。</summary>
    public Task Initialization { get; }

    public SettingsViewModel(
        ISessionSettingsStore settings,
        ILogRepository logs,
        ITakeFlowService takeFlow,
        IExportService exporter,
        ITimeProvider time)
    {
        _settings = settings;
        _logs = logs;
        _takeFlow = takeFlow;
        _exporter = exporter;
        _time = time;
        Initialization = InitializeAsync();
    }

    [ObservableProperty]
    private string _projectName = string.Empty;

    /// <summary>今日场记条数（首次进入设置页时刷新）。</summary>
    [ObservableProperty]
    private int _todayCount;

    private async Task InitializeAsync()
    {
        ProjectName = await _settings.GetStringAsync(ProjectKey, string.Empty);
        await LoadAsync();
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
}