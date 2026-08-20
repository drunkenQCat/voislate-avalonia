using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiSlate.Models;
using VoiSlate.Services;

namespace VoiSlate.ViewModels;

/// <summary>
/// 场记“今日”数据 VM（契约 §4 SlateLogViewModel；DI 单例）。
/// 只读展示：订阅 ITakeFlowService.LogsChanged 刷新 TodayLogs/Dates；无公开持久化写 API——
/// 日志写唯一入口为 ITakeFlowService（B1 纪律）。LogsChanged 由服务在记条/撤回/编辑/删除后触发。
/// </summary>
public partial class SlateLogViewModel : ObservableObject, IDisposable
{
    private readonly ITakeFlowService _takeFlow;
    private readonly ILogRepository _logs;
    private readonly ITimeProvider _time;
    private readonly IExportService _exporter;
    private bool _refreshing;

    public SlateLogViewModel(ITakeFlowService takeFlow, ILogRepository logs, ITimeProvider time, IExportService exporter)
    {
        _takeFlow = takeFlow;
        _logs = logs;
        _time = time;
        _exporter = exporter;
        Today = VoiSlateDates.TodayKey(time.Now);
        SelectedDate = Today;
        _takeFlow.LogsChanged += OnLogsChanged;
    }

    public ObservableCollection<SlateLogItem> TodayLogs { get; } = [];

    public ObservableCollection<string> Dates { get; } = [];

    /// <summary>今日（yyMMdd；跨天由调用方触发 LoadAsync 补偿）。</summary>
    [ObservableProperty]
    private string _today;

    /// <summary>当前选中日期（日期切换条 TwoWay；变化即刷新列表）。</summary>
    [ObservableProperty]
    private string _selectedDate = string.Empty;

    partial void OnSelectedDateChanged(string value) => _ = RefreshAsync();

    private void OnLogsChanged() => _ = RefreshAsync();

    /// <summary>全量刷新（C 于场记页激活/启动后调用；LogsChanged 自动刷新）。</summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        Today = VoiSlateDates.TodayKey(_time.Now);
        await RefreshAsync(ct);
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        try
        {
            var dates = await _logs.GetDatesAsync();
            Dates.Clear();
            foreach (var d in dates)
            {
                Dates.Add(d);
            }

            var listDate = string.IsNullOrEmpty(SelectedDate) ? Today : SelectedDate;
            var todayItems = await _logs.GetByDateAsync(listDate);
            TodayLogs.Clear();
            foreach (var item in todayItems)
            {
                TodayLogs.Add(item);
            }
        }
        finally
        {
            _refreshing = false;
        }

        ct.ThrowIfCancellationRequested();
    }

    /// <summary>编辑请求（承载子编辑器 VM；View 仅负责托管 LogEditorWindow——构造器需 ITakeFlowService，故在 VM 内构建）。</summary>
    public event Action<LogEditorViewModel>? EditRequested;

    /// <summary>请求编辑条目（日期守卫：仅所选日期；索引即 TodayLogs 内位置）。</summary>
    public void RequestEdit(SlateLogItem item)
    {
        var listDate = string.IsNullOrEmpty(SelectedDate) ? Today : SelectedDate;
        var index = TodayLogs.IndexOf(item);
        if (index < 0 || listDate != Today)
        {
            return; // 跨日编辑不在 v0.x 支持面（原版 stub 同限制）
        }

        var used = TodayLogs.Select(x => x.FilenameNum).ToList();
        EditRequested?.Invoke(new LogEditorViewModel(_takeFlow, item, index, used));
    }

    /// <summary>删除选日期下的一条（LogEditor 的删除；跨日维护属存储级操作，直接经 ILogRepository，随后刷新）。</summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task Delete(SlateLogItem? item)
    {
        if (item is null)
        {
            return;
        }

        var listDate = string.IsNullOrEmpty(SelectedDate) ? Today : SelectedDate;
        var idx = TodayLogs.IndexOf(item);
        if (idx >= 0)
        {
            await _logs.RemoveAtAsync(listDate, idx);
            await RefreshAsync();
        }
    }

    /// <summary>导出全部场记 JSON（IExportService；跨日合并）。</summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task Export()
    {
        var all = new List<SlateLogItem>();
        foreach (var date in await _logs.GetDatesAsync())
        {
            all.AddRange(await _logs.GetByDateAsync(date));
        }

        var json = _exporter.SerializeLogs(all);
        await _exporter.SaveToFileAsync(string.Empty, "slatelog-all.json", json);
    }

    public void Dispose() => _takeFlow.LogsChanged -= OnLogsChanged;
}