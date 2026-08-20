using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiSlate.Models;
using VoiSlate.Services;

namespace VoiSlate.ViewModels;

/// <summary>
/// Agent C stub：场记页 VM（契约 §4 SlateLogViewModel/SlateLogPageViewModel —— Agent B 产出，
/// 本文件为编译用占位，合并后删除）。
/// 契约：TodayLogs/Dates/Today（只读展示，订阅 ITakeFlowService.LogsChanged 刷新）。
/// stub 附加：SelectedDate 切换、DeleteCommand（经 ITakeFlowService.DeleteItemAsync，唯一写入口）、
/// ExportCommand（占位，待 IExportService）、EditRequested 事件（视图开 LogEditor 对话框）。
/// </summary>
public partial class SlateLogViewModel : ObservableObject
{
    private readonly ILogRepository _repo;
    private readonly ITakeFlowService _flow;
    private readonly ITimeProvider _time;
    private readonly IToastService _toast;

    public ObservableCollection<SlateLogItem> TodayLogs { get; } = [];

    public ObservableCollection<string> Dates { get; } = [];

    [ObservableProperty]
    private string _today = string.Empty;

    [ObservableProperty]
    private string _selectedDate = string.Empty;

    /// <summary>视图打开 LogEditor 对话框所需（stub 暴露；B 以 DialogService 规范）。</summary>
    public ITakeFlowService TakeFlowService => _flow;

    /// <summary>编辑请求（item + 当日列表索引）。</summary>
    public event Action<SlateLogItem, int>? EditRequested;

    public RelayCommand? ExportCommand { get; }

    public RelayCommand<SlateLogItem>? DeleteCommand { get; }

    public SlateLogViewModel(ILogRepository repo, ITakeFlowService flow, ITimeProvider time, IToastService toast)
    {
        _repo = repo;
        _flow = flow;
        _time = time;
        _toast = toast;

        ExportCommand = new RelayCommand(DoExport);
        DeleteCommand = new RelayCommand<SlateLogItem>(DoDelete);

        _flow.LogsChanged += OnLogsChanged;
        _ = ReloadDatesAsync();
    }

    /// <summary>请求打开编辑对话框（由视图装载 LogEditorWindow/LogEditorViewModel；含跨日守卫）。</summary>
    public void RequestEdit(SlateLogItem item)
    {
        var index = TodayLogs.IndexOf(item);
        if (index < 0) return;

        // ITakeFlowService 的编辑/删除为“今日”作用域（TakeFlowService.Today）；
        // 跨日编辑由 E 的 ScheduleService 演进（此处 stub 提示）。
        if (SelectedDate != Today)
        {
            _toast.Show("跨日编辑/删除暂未开放（stub 限制：ITakeFlowService 今日作用域，待 E 演进）");
            return;
        }

        EditRequested?.Invoke(item, index);
    }

    partial void OnSelectedDateChanged(string value) => _ = LoadSelectedAsync();

    private async Task ReloadDatesAsync()
    {
        try
        {
            var dates = (await _repo.GetDatesAsync()).ToList();
            Today = VoiSlateDates.TodayKey(_time.Now);
            Dates.Clear();
            if (dates.Count == 0)
            {
                dates.Add(Today);
            }

            // 原版 slate_log_tabs：tabs = dates.reversed（新日期在前）
            foreach (var d in dates.OrderByDescending(x => x))
            {
                Dates.Add(d);
            }

            if (!Dates.Contains(SelectedDate))
            {
                SelectedDate = Today;
            }

            await LoadSelectedAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SlateLog reload failed: {ex.Message}");
        }
    }

    private async Task LoadSelectedAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(SelectedDate))
            {
                SelectedDate = Today;
            }

            var items = await _repo.GetByDateAsync(SelectedDate);
            TodayLogs.Clear();
            foreach (var item in items)
            {
                TodayLogs.Add(item);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SlateLog load failed: {ex.Message}");
        }
    }

    private void OnLogsChanged() => _ = LoadSelectedAsync();

    private void DoDelete(SlateLogItem? item)
    {
        if (item == null) return;
        var index = TodayLogs.IndexOf(item);
        if (index < 0) return;

        // ITakeFlowService 的编辑/删除为“今日”作用域（TakeFlowService.Today）；
        // 跨日编辑由 E 的 ScheduleService 演进（此处 stub 提示）。
        if (SelectedDate != Today)
        {
            _toast.Show("跨日编辑/删除暂未开放（stub 限制：ITakeFlowService 今日作用域，待 E 演进）");
            return;
        }

        _ = _flow.DeleteItemAsync(index, CancellationToken.None);
    }

    private void DoExport() => _toast.Show("导出 JSON（占位：待 IExportService，契约 §3）");
}