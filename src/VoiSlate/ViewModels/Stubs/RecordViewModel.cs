using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiSlate.Models;
using VoiSlate.Services;

namespace VoiSlate.ViewModels;

/// <summary>
/// Agent C stub：记录页 VM（契约 §4 —— Agent B 产出，本文件为编译用占位，合并后删除）。
/// 契约成员：SceneCol/ShotCol/TakeCol、CurrentFileNumber、IsLinked、IsRecording、AsrStatus、
/// DescText/ShotNoteText(TwoWay)、PreviewHint、AdvanceTakeCommand(TakeType)/RewindTakeCommand/
/// ToggleLinkCommand/EditFileNumber/EditLinker/EditPrefix（命令触发事件由视图开对话框并经
/// ITakeFlowService 写文件号，BLOCKER-1/C-2）；Scoped 生命周期 + Activate/Deactivate(B5)。
/// stub 额外注入 IScheduleBook（列数据源）、RecordingSessionViewModel（评价/联动）、
/// IAsrService、ILogRepository（场记速览）——B 正式版本以契约 ctor(ISessionSettingsStore, ITakeFlowService)
/// 为准，合并时对齐。
/// </summary>
public partial class RecordViewModel : ObservableObject, IDisposable
{
    private readonly ISessionSettingsStore _settings;
    private readonly ITakeFlowService _flow;
    private readonly IScheduleBook _book;
    private readonly RecordingSessionViewModel _session;
    private readonly IAsrService _asr;
    private readonly ILogRepository _logs;

    private bool _active;
    private bool _disposed;

    /// <summary>场/镜/次三列（契约 §4）。</summary>
    public SlateColumnViewModel SceneCol { get; } = new();

    public SlateColumnViewModel ShotCol { get; } = new();

    public SlateColumnViewModel TakeCol { get; } = new();

    /// <summary>会话 VM（stub 引用；B 合入后按 B 的设计接线）。</summary>
    public RecordingSessionViewModel Session => _session;

    [ObservableProperty]
    private int _currentFileNumber = 1;

    [ObservableProperty]
    private string _currentFilePrefix = string.Empty;

    [ObservableProperty]
    private string _currentFileLinker = "-T";

    [ObservableProperty]
    private string _currentFileNumberText = "001";

    [ObservableProperty]
    private bool _isLinked = true;

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private string _asrStatus = "Mock ASR（未启动）";

    [ObservableProperty]
    private string _descText = string.Empty;

    [ObservableProperty]
    private string _shotNoteText = string.Empty;

    /// <summary>上一拍提示（prev_file_monitor 语义：上一拍文件名 + 录音标注提示）。</summary>
    [ObservableProperty]
    private string _previewHint = "录音标注...";

    /// <summary>场记速览（quick_view_log_dialog；sublist(40) 已知行为保留）。</summary>
    public ObservableCollection<QuickNoteItem> QuickNotes { get; } = [];

    /// <summary>文件号编辑请求（视图经 ITakeFlowService 写回；C-2 唯一写入口）。</summary>
    public event Action? FileNumberEditRequested;

    public event Action? LinkerEditRequested;

    public event Action? PrefixEditRequested;

    public AsyncRelayCommand<TakeType> AdvanceTakeCommand { get; }

    public AsyncRelayCommand RewindTakeCommand { get; }

    public RelayCommand ToggleLinkCommand { get; }

    public RelayCommand EditFileNumberCommand { get; }

    public RelayCommand EditLinkerCommand { get; }

    public RelayCommand EditPrefixCommand { get; }

    public RelayCommand ToggleAsrCommand { get; }

    public RecordViewModel(
        ISessionSettingsStore settings,
        ITakeFlowService flow,
        IScheduleBook book,
        RecordingSessionViewModel session,
        IAsrService asr,
        ILogRepository logs)
    {
        _settings = settings;
        _flow = flow;
        _book = book;
        _session = session;
        _asr = asr;
        _logs = logs;

        AdvanceTakeCommand = new AsyncRelayCommand<TakeType>(AdvanceTakeAsync);
        RewindTakeCommand = new AsyncRelayCommand(RewindTakeAsync);
        ToggleLinkCommand = new RelayCommand(ToggleLink);
        EditFileNumberCommand = new RelayCommand(() => FileNumberEditRequested?.Invoke());
        EditLinkerCommand = new RelayCommand(() => LinkerEditRequested?.Invoke());
        EditPrefixCommand = new RelayCommand(() => PrefixEditRequested?.Invoke());
        ToggleAsrCommand = new RelayCommand(ToggleAsr);

        SceneCol.PropertyChanged += OnColumnChanged;
        ShotCol.PropertyChanged += OnColumnChanged;
        TakeCol.PropertyChanged += OnColumnChanged;

        TakeCol.Items = Enumerable.Range(1, 200).Select(i => i.ToString()).ToList(); // N2：take 1..200
        _ = InitializeColumnsAsync();
    }

    /// <summary>契约 B5：进入记录页（订阅文件号/日志事件 + 恢复 13 键 + 文件号同步）。</summary>
    public void Activate()
    {
        if (_active) return;
        _active = true;
        _flow.FileNumberChanged += OnFileNumberChanged;
        _flow.LogsChanged += OnLogsChanged;
        _asr.FinalResult += OnAsrResult;
        _ = RefreshFromSettingsAsync();
    }

    /// <summary>契约 B5：退出记录页（取消订阅 + 释放）。</summary>
    public void Deactivate()
    {
        if (!_active) return;
        _active = false;
        _flow.FileNumberChanged -= OnFileNumberChanged;
        _flow.LogsChanged -= OnLogsChanged;
        _asr.FinalResult -= OnAsrResult;
        if (IsRecording)
        {
            _asr.Stop();
            IsRecording = false;
        }
    }

    private async Task InitializeColumnsAsync()
    {
        var sceneNames = await Task.Run(() => _book.AllSceneNames());
        if (sceneNames.Count == 0)
        {
            sceneNames = ["1"];
        }

        SceneCol.Items = sceneNames;

        var sceneIdx = await _settings.GetIntAsync(SessionKeys.SceneIndex, 0);
        var shotIdx = await _settings.GetIntAsync(SessionKeys.ShotIndex, 0);
        var takeIdx = await _settings.GetIntAsync(SessionKeys.TakeIndex, 0);
        ReloadShots(sceneIdx);
        SceneCol.SelectedIndex = Math.Clamp(sceneIdx, 0, Math.Max(SceneCol.Items.Count - 1, 0));
        ShotCol.SelectedIndex = Math.Clamp(shotIdx, 0, Math.Max(ShotCol.Items.Count - 1, 0));
        TakeCol.SelectedIndex = Math.Clamp(takeIdx, 0, Math.Max(TakeCol.Items.Count - 1, 0));
    }

    private void OnColumnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SlateColumnViewModel.SelectedIndex)) return;

        if (ReferenceEquals(sender, SceneCol))
        {
            _session.SelectScene(SceneCol.SelectedIndex);
            ReloadShots(SceneCol.SelectedIndex);
            ShotCol.SelectedIndex = 0;
            _ = _settings.SetAsync(SessionKeys.SceneIndex, SceneCol.SelectedIndex);
            _ = _settings.SetAsync(SessionKeys.ShotIndex, 0);
        }
        else if (ReferenceEquals(sender, ShotCol))
        {
            _session.SelectShot(ShotCol.SelectedIndex);
            _ = _settings.SetAsync(SessionKeys.ShotIndex, ShotCol.SelectedIndex);
        }
        else if (ReferenceEquals(sender, TakeCol))
        {
            _session.SelectTake(TakeCol.SelectedIndex);
            _ = _settings.SetAsync(SessionKeys.TakeIndex, TakeCol.SelectedIndex);
        }
    }

    private void ReloadShots(int sceneIndex)
    {
        IReadOnlyList<string> shots;
        try
        {
            var scene = _book.GetScene(sceneIndex);
            shots = scene.Select(s => s.Name).ToList();
        }
        catch
        {
            shots = ["1"];
        }

        if (shots.Count == 0)
        {
            shots = ["1"];
        }

        ShotCol.Items = shots;
        OnPropertyChanged(nameof(ShotCol));
    }

    private async Task AdvanceTakeAsync(TakeType type)
    {
        try
        {
            await _flow.AddItemAsync(type, CancellationToken.None, DescText, ShotNoteText);
            if (type == TakeType.Normal)
            {
                TakeCol.ScrollNext(IsLinked); // 原版 col3IncBtn：记条后 take 列滚动下一
            }

            DescText = string.Empty; // 原版 setDescNewText：清空录音标注
            _session.ResetOkStatus();
            await RefreshFromSettingsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AdvanceTake failed: {ex.Message}");
        }
    }

    private async Task RewindTakeAsync()
    {
        try
        {
            var result = await _flow.RewindAsync(CancellationToken.None);
            if (!result.WasOkMarkerOnly)
            {
                if (IsLinked)
                {
                    TakeCol.ScrollPrev(IsLinked); // 原版：非 OK 尾撤回时 take 列回退
                }

                DescText = result.RestoredDesc;      // B11 回填备注
                ShotNoteText = result.RestoredShotNote;
            }

            _session.ResetOkStatus();
            await RefreshFromSettingsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RewindTake failed: {ex.Message}");
        }
    }

    private void ToggleLink()
    {
        IsLinked = !IsLinked;
        _session.SetLink(IsLinked);
        _ = _settings.SetAsync(SessionKeys.IsLinked, IsLinked);
    }

    /// <summary>DialFAB → TkStatus（B-6 转换由 C 在视图 code-behind 完成；此处接会话）。</summary>
    public void SetOkTake(TkStatus status) => _session.SetOkTake(status);

    /// <summary>DialFAB → ShtStatus（同上）。</summary>
    public void SetOkShot(ShtStatus status) => _session.SetOkShot(status);

    /// <summary>编辑文件号（BLOCKER-1：唯一写入口 ITakeFlowService.SetFileNumberAsync）。</summary>
    public async Task SetFileNumberAsync(int value)
    {
        await _flow.SetFileNumberAsync(value, CancellationToken.None);
        await RefreshFromSettingsAsync();
    }

    /// <summary>编辑链接符（唯一写入口 ITakeFlowService.SetLinkerAsync）。</summary>
    public async Task SetLinkerAsync(string linker)
    {
        await _flow.SetLinkerAsync(linker, CancellationToken.None);
        await RefreshFromSettingsAsync();
    }

    /// <summary>编辑前缀（唯一写入口 ITakeFlowService.SetPrefixAsync；custom 可空）。</summary>
    public async Task SetPrefixAsync(PrefixType mode, string? customPrefix)
    {
        await _flow.SetPrefixAsync(mode, customPrefix, CancellationToken.None);
        await RefreshFromSettingsAsync();
    }

    private void ToggleAsr()
    {
        if (!_asr.IsListening)
        {
            _asr.Start();
            IsRecording = true;
            AsrStatus = "Mock ASR：识别中（Mock 无真实结果，FinalResult 事件接入后生效）";
        }
        else
        {
            _asr.Stop();
            IsRecording = false;
            AsrStatus = "Mock ASR（未启动）";
        }
    }

    private void OnAsrResult(string text)
    {
        ShotNoteText = string.IsNullOrEmpty(ShotNoteText) ? text : $"{ShotNoteText}\n{text}";
    }

    private void OnFileNumberChanged(int number)
    {
        CurrentFileNumber = number;
        CurrentFileNumberText = number.ToString("D3");
        UpdatePreviewHint();
    }

    private void OnLogsChanged() => _ = RefreshQuickNotesAsync();

    /// <summary>场记速览数据（对齐原版 exportQuickNotes：>40 条时取 sublist(40) 段，已知行为）。</summary>
    public async Task RefreshQuickNotesAsync()
    {
        try
        {
            var logs = await _logs.GetByDateAsync(VoiSlateDates.TodayKey(DateTime.Now));
            var notes = logs.Select(l => new QuickNoteItem(l.FileName, l.TkNote)).ToList();
            if (notes.Count > 40)
            {
                notes = notes.Skip(40).ToList();
            }

            QuickNotes.Clear();
            foreach (var n in notes)
            {
                QuickNotes.Add(n);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshQuickNotes failed: {ex.Message}");
        }
    }

    private async Task RefreshFromSettingsAsync()
    {
        try
        {
            var count = await _settings.GetIntAsync(SessionKeys.RecordCount, 1);
            var linker = await _settings.GetStringAsync(SessionKeys.RecordLinker, "-T");
            var mode = PrefixTypeExtensions.ParseSettings(await _settings.GetStringAsync(SessionKeys.PrefixType, "default"));
            var custom = await _settings.GetStringAsync(SessionKeys.CustomPrefix, "custom");
            var linked = await _settings.GetBoolAsync(SessionKeys.IsLinked, true);

            CurrentFileNumber = count;
            CurrentFileNumberText = count.ToString("D3");
            CurrentFileLinker = linker;
            IsLinked = linked;
            CurrentFilePrefix = mode switch
            {
                PrefixType.Custom => custom,
                PrefixType.SoundDevices => VoiSlateDates.SoundDevicesKey(DateTime.Now),
                _ => VoiSlateDates.TodayKey(DateTime.Now),
            };
            UpdatePreviewHint();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshFromSettings failed: {ex.Message}");
        }
    }

    private void UpdatePreviewHint()
    {
        var prev = CurrentFileNumber > 1
            ? $"{CurrentFilePrefix}{CurrentFileLinker}{CurrentFileNumber - 1:D3}"
            : string.Empty;
        PreviewHint = string.IsNullOrEmpty(prev) ? "录音标注..." : $"{prev}\n录音标注...";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Deactivate();
        SceneCol.PropertyChanged -= OnColumnChanged;
        ShotCol.PropertyChanged -= OnColumnChanged;
        TakeCol.PropertyChanged -= OnColumnChanged;
        GC.SuppressFinalize(this);
    }
}