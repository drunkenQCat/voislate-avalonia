using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiSlate.Models;
using VoiSlate.Services;

namespace VoiSlate.ViewModels;

/// <summary>场记页“场”节点（C 树绑定：ExpansionTile 场 → 镜 → 条目）。</summary>
public sealed class SlateLogSceneGroup
{
    public required string Scn { get; init; }

    public ObservableCollection<SlateLogShotGroup> Shots { get; } = [];
}

/// <summary>场记页“镜”节点。</summary>
public sealed class SlateLogShotGroup
{
    public required string Sht { get; init; }

    public ObservableCollection<SlateLogItem> Items { get; } = [];
}

/// <summary>
/// 场记页 VM（契约 §4 SlateLogPageViewModel）。日期 Tab（dates 倒序＝新日期在前）→ 场/镜树；
/// “当前场镜高亮”取自会话 VM（经 IScheduleBook 得名称）；编辑/导出：
/// 编辑经 ITakeFlowService.SaveEditAsync/DeleteItemAsync 唯一入口（B1），导出经 IExportService（B 补桩，演进权 E）。
/// </summary>
public partial class SlateLogPageViewModel : ObservableObject, IDisposable
{
    private readonly ILogRepository _logs;
    private readonly IExportService _exporter;
    private readonly ITakeFlowService _takeFlow;
    private readonly RecordingSessionViewModel _session;
    private readonly IScheduleBook _scheduleBook;
    private readonly ITimeProvider _time;

    private readonly List<SlateLogItem> _currentLogs = [];
    private bool _loading;

    public SlateLogPageViewModel(
        ILogRepository logs,
        IExportService exporter,
        ITakeFlowService takeFlow,
        RecordingSessionViewModel session,
        IScheduleBook scheduleBook,
        ITimeProvider time)
    {
        _logs = logs;
        _exporter = exporter;
        _takeFlow = takeFlow;
        _session = session;
        _scheduleBook = scheduleBook;
        _time = time;
        Today = VoiSlateDates.TodayKey(time.Now);
        _takeFlow.LogsChanged += OnLogsChanged;
        _session.PropertyChanged += OnSessionPropertyChanged;
        UpdateHighlight();
    }

    public ObservableCollection<string> Dates { get; } = [];

    public ObservableCollection<SlateLogSceneGroup> Groups { get; } = [];

    /// <summary>当前日期（默认最近日期或今日；倒序 Tab 新日期在前）。</summary>
    [ObservableProperty]
    private string _selectedDate = string.Empty;

    [ObservableProperty]
    private string _today;

    /// <summary>当前场名（高亮；取自会话 VM + IScheduleBook）。</summary>
    [ObservableProperty]
    private string _currentScene = string.Empty;

    /// <summary>当前镜名（高亮）。</summary>
    [ObservableProperty]
    private string _currentShot = string.Empty;

    /// <summary>是否可编辑当前日期（P0.5 约束：ITakeFlowService.SaveEdit/Delete 仅以今日为准）。</summary>
    public bool CanEditSelectedDate => SelectedDate == Today;

    // ---- 加载（日期 → 分组树）----

    /// <summary>加载日期列表并默认选中最近日期（原版 SlateLogTabs 初始 index=0 即最新日期）。</summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        _loading = true;
        try
        {
            Today = VoiSlateDates.TodayKey(_time.Now);
            var dates = await _logs.GetDatesAsync();
            Dates.Clear();
            foreach (var d in dates)
            {
                Dates.Add(d);
            }

            if (string.IsNullOrEmpty(SelectedDate) || !Dates.Contains(SelectedDate))
            {
                SelectedDate = Dates.Count > 0 ? Dates[^1] : Today;
            }
        }
        finally
        {
            _loading = false;
        }

        await RefreshSelectedAsync(ct);
    }

    partial void OnSelectedDateChanged(string value)
    {
        OnPropertyChanged(nameof(CanEditSelectedDate));
        if (!_loading)
        {
            _ = RefreshSelectedAsync();
        }
    }

    /// <summary>按 SelectedDate 重建分组树（_currentLogs 保持仓库插入序，供编辑索引定位）。</summary>
    public async Task RefreshSelectedAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(SelectedDate))
        {
            return;
        }

        var items = await _logs.GetByDateAsync(SelectedDate);
        _currentLogs.Clear();
        _currentLogs.AddRange(items);

        Groups.Clear();
        foreach (var sceneGroup in items.GroupBy(i => i.Scn).OrderBy(g => g.Key))
        {
            var sceneNode = new SlateLogSceneGroup { Scn = sceneGroup.Key };
            foreach (var shotGroup in sceneGroup.GroupBy(i => i.Sht).OrderBy(g => g.Key))
            {
                var shotNode = new SlateLogShotGroup { Sht = shotGroup.Key };
                foreach (var item in shotGroup)
                {
                    shotNode.Items.Add(item);
                }

                sceneNode.Shots.Add(shotNode);
            }

            Groups.Add(sceneNode);
        }

        ct.ThrowIfCancellationRequested();
    }

    private void OnLogsChanged()
    {
        // 今日变更才需要刷新（LogsChanged 语义：服务只写今日；Dates 列表跨日由 LoadAsync 补偿）
        if (SelectedDate == Today)
        {
            _ = RefreshSelectedAsync();
        }
    }

    // ---- 编辑（唯一入口 ITakeFlowService）----

    /// <summary>按条目创建编辑器（仅今日可编辑——P0.5 服务 SaveEdit/Delete 以 Today 为落点）。</summary>
    public LogEditorViewModel? CreateLogEditor(SlateLogItem item)
    {
        if (!CanEditSelectedDate)
        {
            return null;
        }

        var index = _currentLogs.FindIndex(i => ReferenceEquals(i, item) || i.Id == item.Id);
        if (index < 0)
        {
            return null;
        }

        var used = _currentLogs.Select(i => i.FilenameNum).ToList();
        return new LogEditorViewModel(_takeFlow, _currentLogs[index], index, used);
    }

    // ---- 导出（IExportService；B 补桩 Noop，演进权 E）----

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task ExportJson()
    {
        var json = _exporter.SerializeLogs(_currentLogs);
        await _exporter.SaveToFileAsync(string.Empty, $"{SelectedDate}.json", json);
    }

    // ---- 高亮（会话联动）----

    private void OnSessionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RecordingSessionViewModel.SelectedSceneIndex)
            or nameof(RecordingSessionViewModel.SelectedShotIndex))
        {
            UpdateHighlight();
        }
    }

    private void UpdateHighlight()
    {
        CurrentScene = _scheduleBook.SceneLabel(_session.SelectedSceneIndex);
        CurrentShot = _scheduleBook.ShotLabel(_session.SelectedSceneIndex, _session.SelectedShotIndex);
    }

    public void Dispose()
    {
        _takeFlow.LogsChanged -= OnLogsChanged;
        _session.PropertyChanged -= OnSessionPropertyChanged;
    }
}