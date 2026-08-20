using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
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
    private bool _refreshing;

    public SlateLogViewModel(ITakeFlowService takeFlow, ILogRepository logs, ITimeProvider time)
    {
        _takeFlow = takeFlow;
        _logs = logs;
        _time = time;
        Today = VoiSlateDates.TodayKey(time.Now);
        _takeFlow.LogsChanged += OnLogsChanged;
    }

    public ObservableCollection<SlateLogItem> TodayLogs { get; } = [];

    public ObservableCollection<string> Dates { get; } = [];

    /// <summary>今日（yyMMdd；跨天由调用方触发 LoadAsync 补偿）。</summary>
    [ObservableProperty]
    private string _today;

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

            var todayItems = await _logs.GetByDateAsync(Today);
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

    public void Dispose() => _takeFlow.LogsChanged -= OnLogsChanged;
}