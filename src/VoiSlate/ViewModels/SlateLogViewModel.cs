using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiSlate.Models;
using VoiSlate.Services;

namespace VoiSlate.ViewModels;

/// <summary>场记页两级分组：镜组（ExpansionTile 内层，标题 "Shot n"）。</summary>
public sealed partial class LogShotGroup : ObservableObject
{
    public string Title { get; init; } = string.Empty;

    /// <summary>分组键（条目 Sht 值）。</summary>
    public string Tag { get; init; } = string.Empty;

    public ObservableCollection<SlateLogItem> Items { get; } = [];

    [ObservableProperty]
    private bool _isOpen;
}

/// <summary>场记页两级分组：场组（ExpansionTile 外层，标题 "Scene n"；HTML 默认展开第 1 场组与其第 1 镜组）。</summary>
public sealed partial class LogSceneGroup : ObservableObject
{
    public string Title { get; init; } = string.Empty;

    /// <summary>分组键（条目 Scn 值）。</summary>
    public string Tag { get; init; } = string.Empty;

    public ObservableCollection<LogShotGroup> Shots { get; } = [];

    [ObservableProperty]
    private bool _isOpen;
}

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

    /// <summary>当前日期两级分组（HTML renderLogs 语义：场组 Scene n / 镜组 Shot n / 条目；行色按 OkTk）。</summary>
    public ObservableCollection<LogSceneGroup> Groups { get; } = [];

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

            RebuildGroups();
        }
        finally
        {
            _refreshing = false;
        }

        ct.ThrowIfCancellationRequested();
    }

    /// <summary>按条目 Scn/Sht 重建两级分组（组标题按组内序号 Scene n / Shot n；默认展开第 1 场组与其第 1 镜组）。</summary>
    private void RebuildGroups()
    {
        Groups.Clear();
        LogSceneGroup? scnGroup = null;
        LogShotGroup? shtGroup = null;
        var scnOrdinal = 0;
        var shtOrdinal = 0;
        var firstSceneExpanded = false;

        foreach (var item in TodayLogs)
        {
            if (scnGroup is null || scnGroup.Tag != item.Scn)
            {
                scnGroup = new LogSceneGroup { Title = $"Scene {++scnOrdinal}", Tag = item.Scn, IsOpen = !firstSceneExpanded };
                firstSceneExpanded = true;
                shtGroup = null;
                shtOrdinal = 0;
                Groups.Add(scnGroup);
            }

            if (shtGroup is null || shtGroup.Tag != item.Sht)
            {
                shtGroup = new LogShotGroup { Title = $"Shot {++shtOrdinal}", Tag = item.Sht, IsOpen = shtOrdinal == 1 };
                scnGroup.Shots.Add(shtGroup);
            }

            shtGroup.Items.Add(item);
        }
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