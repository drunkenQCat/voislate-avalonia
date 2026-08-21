using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiSlate.Models;
using VoiSlate.Services;

namespace VoiSlate.ViewModels;

/// <summary>
/// 记录页 VM（契约 §4 RecordViewModel；Scoped：每次进入记录页经工厂创建、退出释放，R15）。
/// 注入：ISessionSettingsStore + ITakeFlowService + IAsrService + IHardwareKeyService + 会话单例 + ITimeProvider
/// （后两者为契约外补充，用于列↔会话联动与文件名前缀显示；不注入 FileNumberingService——唯一实例在
/// ITakeFlowService 内 C-2，文件号经 FileNumberChanged 显示）。
/// B5 激活钩子：Activate() 订阅 IHardwareKeyService + 恢复 13 键 + 经 FileNumberChanged 同步文件号；
/// Deactivate() 取消订阅 + 释放（C 在 RecordView.Loaded/Unloaded 调用）。
/// 音量键：VolumeUp → AdvanceTake(Normal)+TakeCol.ScrollNext；VolumeDown → RewindTake（契约映射）。
/// </summary>
public partial class RecordViewModel : ObservableObject, IDisposable
{
    /// <summary>假拍 hint（对齐原版 setDescNewText）。</summary>
    public const string HintFake = "这条跑了";

    /// <summary>收工 hint。</summary>
    public const string HintEnd = "收工了，这一镜结束了";

    private const string HintSuffix = "\n 录音标注...";

    private readonly ISessionSettingsStore _settings;
    private readonly ITakeFlowService _takeFlow;
    private readonly IAsrService _asr;
    private readonly IHardwareKeyService _hardwareKeys;
    private readonly RecordingSessionViewModel _session;
    private readonly ITimeProvider _time;
    private readonly ILogRepository _logs;

    private bool _isActive;
    private bool _suppressColumnSync;
    private string _prefixMode = "default";
    private string _customPrefix = "custom";

    public RecordViewModel(
        ISessionSettingsStore settings,
        ITakeFlowService takeFlow,
        IAsrService asr,
        IHardwareKeyService hardwareKeys,
        RecordingSessionViewModel session,
        ITimeProvider time,
        ILogRepository logs)
    {
        _settings = settings;
        _takeFlow = takeFlow;
        _asr = asr;
        _hardwareKeys = hardwareKeys;
        _session = session;
        _time = time;
        _logs = logs;

        SceneCol = new SlateColumnViewModel();
        ShotCol = new SlateColumnViewModel();
        TakeCol = new SlateColumnViewModel();
        TakeCol.SetItems(Enumerable.Range(1, RecordingSessionViewModel.DefaultTakeCount)
            .Select(i => i.ToString()).ToArray());

        _asr.FinalResult += OnAsrFinalResult;
        _asr.ErrorOccurred += OnAsrError;
    }

    /// <summary>场列（Items 由 C 按 IScheduleBook 场景名填充）。</summary>
    public SlateColumnViewModel SceneCol { get; }

    /// <summary>镜列（Items 由 C 按当前场镜头名填充；场切换时 C 重置并调用 SetItems）。</summary>
    public SlateColumnViewModel ShotCol { get; }

    /// <summary>次列（1..200，TakeColumn 范围常量 N2）。</summary>
    public SlateColumnViewModel TakeCol { get; }

    /// <summary>场记速览（quick_view_log_dialog 语义：fileName → tkNote；取当日末 40 条，对齐原版 sublist(40)）。</summary>
    public ObservableCollection<SlateLogItem> QuickNotes { get; } = [];

    /// <summary>刷新速览（C 在速览按钮点击时调用）。</summary>
    public async Task RefreshQuickNotesAsync()
    {
        var today = VoiSlateDates.TodayKey(_time.Now);
        var items = await _logs.GetByDateAsync(today);
        QuickNotes.Clear();
        foreach (var item in items.TakeLast(40))
        {
            QuickNotes.Add(item);
        }
    }

    /// <summary>声音评价（DialFAB→TkStatus；镜像会话，原版 dial 语义）。</summary>
    public void SetOkTake(TkStatus status) => _session.SetOkTake(status);

    /// <summary>画面评价（DialFAB→ShtStatus）。</summary>
    public void SetOkShot(ShtStatus status) => _session.SetOkShot(status);

    // ---- 显示状态 ----

    [ObservableProperty]
    private string _descText = string.Empty;

    [ObservableProperty]
    private string _shotNoteText = string.Empty;

    [ObservableProperty]
    private bool _isLinked;

    [ObservableProperty]
    private string _previewHint = string.Empty;

    [ObservableProperty]
    private string _asrStatus = "空闲";

    /// <summary>原始文件号（FileNumberChanged 事件驱动；下限 1，B7）。</summary>
    [ObservableProperty]
    private int _fileNumber = 1;

    /// <summary>前缀显示（按 PrefixType 模式计算：custom/sound devices/default）。</summary>
    [ObservableProperty]
    private string _prefixText = string.Empty;

    /// <summary>链接符显示（recordLinker）。</summary>
    [ObservableProperty]
    private string _linkerText = "-T";

    /// <summary>补零 3 位显示（FileCounter.NumberText 语义）。</summary>
    public string NumberText => FileNumber.ToString("D3");

    /// <summary>当前文件名显示 = prefix + linker + 补零（对齐 CurrentFileMonitor。经 FileNumberChanged 更新）。</summary>
    public string CurrentFileNumber => $"{PrefixText}{LinkerText}{FileNumber:D3}";

    /// <summary>下一个文件号（FileCounter 三卡片 Num 段：HTML 语义 pad3(recCount)）。</summary>
    public string NextFileNumber => (FileNumber + 1).ToString("D3");

    /// <summary>录音状态（IAsrService.IsListening 镜像）。</summary>
    public bool IsRecording => _asr.IsListening;

    partial void OnFileNumberChanged(int value)
    {
        OnPropertyChanged(nameof(NumberText));
        OnPropertyChanged(nameof(CurrentFileNumber));
        OnPropertyChanged(nameof(NextFileNumber));
    }

    partial void OnPrefixTextChanged(string value) => OnPropertyChanged(nameof(CurrentFileNumber));

    partial void OnLinkerTextChanged(string value) => OnPropertyChanged(nameof(CurrentFileNumber));

    partial void OnDescTextChanged(string value)
    {
        if (_isActive)
        {
            _session.SetDesc(value); // 镜像会话（原版 setNote 语义：即写即存）
        }
    }

    partial void OnShotNoteTextChanged(string value)
    {
        if (_isActive)
        {
            _session.SetNote(value);
        }
    }

    partial void OnIsLinkedChanged(bool value)
    {
        if (_isActive)
        {
            _session.SetLink(value);
        }
    }

    // ---- 生命周期（契约 B5）----

    /// <summary>初始化任务（Activate 触发的 13 键恢复；完成即可安全读取各属性）。</summary>
    public Task HydrationTask { get; private set; } = Task.CompletedTask;

    /// <summary>进入记录页：订阅硬件键/文件号/会话/列事件 + 恢复 13 键 + 同步文件号。C 在 RecordView.Loaded 调用。</summary>
    public void Activate()
    {
        if (_isActive)
        {
            return;
        }

        _isActive = true;

        _hardwareKeys.KeyPressed += OnHardwareKeyPressed;
        _takeFlow.FileNumberChanged += OnFileNumberChangedFromFlow;
        _session.PropertyChanged += OnSessionPropertyChanged;
        SceneCol.PropertyChanged += OnColumnPropertyChanged;
        ShotCol.PropertyChanged += OnColumnPropertyChanged;
        TakeCol.PropertyChanged += OnColumnPropertyChanged;

        HydrationTask = HydrateAsync();
    }

    /// <summary>退出记录页：取消订阅 + 释放（幂等）。C 在 RecordView.Unloaded 调用。</summary>
    public void Deactivate()
    {
        if (!_isActive)
        {
            return;
        }

        _isActive = false;

        _hardwareKeys.KeyPressed -= OnHardwareKeyPressed;
        _takeFlow.FileNumberChanged -= OnFileNumberChangedFromFlow;
        _session.PropertyChanged -= OnSessionPropertyChanged;
        SceneCol.PropertyChanged -= OnColumnPropertyChanged;
        ShotCol.PropertyChanged -= OnColumnPropertyChanged;
        TakeCol.PropertyChanged -= OnColumnPropertyChanged;
    }

    public void Dispose()
    {
        Deactivate();
        _asr.FinalResult -= OnAsrFinalResult;
        _asr.ErrorOccurred -= OnAsrError;
        GC.SuppressFinalize(this);
    }

    // ---- 13 键恢复（对齐原版 initPickerAndFileNumWidget + InitializeAsync 语义）----

    private async Task HydrateAsync()
    {
        var today = VoiSlateDates.TodayKey(_time.Now);
        var savedDate = await _settings.GetStringAsync(SessionKeys.Date, string.Empty);
        var recordCount = savedDate == today
            ? await _settings.GetIntAsync(SessionKeys.RecordCount, 1)
            : 1;

        _prefixMode = await _settings.GetStringAsync(SessionKeys.PrefixType, "default");
        _customPrefix = await _settings.GetStringAsync(SessionKeys.CustomPrefix, "custom");
        PrefixText = ComputePrefix(_prefixMode);
        LinkerText = await _settings.GetStringAsync(SessionKeys.RecordLinker, "-T");

        SyncColumn(SceneCol, await _settings.GetIntAsync(SessionKeys.SceneIndex, 0));
        SyncColumn(ShotCol, await _settings.GetIntAsync(SessionKeys.ShotIndex, 0));
        SyncColumn(TakeCol, await _settings.GetIntAsync(SessionKeys.TakeIndex, 0));

        IsLinked = await _settings.GetBoolAsync(SessionKeys.IsLinked, true);
        DescText = await _settings.GetStringAsync(SessionKeys.Desc, string.Empty);
        ShotNoteText = await _settings.GetStringAsync(SessionKeys.Note, string.Empty);

        // 当日 recordCount 恢复、跨日归 1；文件号经 B7 与 RecordCount 双向同步
        SetCurrentNumber(recordCount);
        _session.SetRecordCount(recordCount);
        UpdatePreviewHintNormal();
    }

    private string ComputePrefix(string mode) => mode switch
    {
        "custom" => _customPrefix,
        "sound devices" => VoiSlateDates.SoundDevicesKey(_time.Now),
        _ => VoiSlateDates.TodayKey(_time.Now),
    };

    private void SetCurrentNumber(int number)
    {
        FileNumber = number < 1 ? 1 : number; // 生成属性 setter → OnFileNumberChanged → 显示刷新
    }

    // ---- 列 ↔ 会话联动（对齐原版 pickerNumSync 的索引时序）----

    private void OnColumnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isActive || _suppressColumnSync)
        {
            return;
        }

        if (e.PropertyName != nameof(SlateColumnViewModel.SelectedIndex))
        {
            return;
        }

        if (ReferenceEquals(sender, SceneCol))
        {
            _session.SelectScene(SceneCol.SelectedIndex);
            _session.SelectShot(0);
            _session.SelectTake(0);
        }
        else if (ReferenceEquals(sender, ShotCol))
        {
            _session.SelectShot(ShotCol.SelectedIndex);
            _session.SelectTake(0);
        }
        else if (ReferenceEquals(sender, TakeCol))
        {
            _session.SelectTake(TakeCol.SelectedIndex);
        }
    }

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isActive)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(RecordingSessionViewModel.SelectedSceneIndex):
                SyncColumn(SceneCol, _session.SelectedSceneIndex);
                break;
            case nameof(RecordingSessionViewModel.SelectedShotIndex):
                SyncColumn(ShotCol, _session.SelectedShotIndex);
                break;
            case nameof(RecordingSessionViewModel.SelectedTakeIndex):
                SyncColumn(TakeCol, _session.SelectedTakeIndex);
                break;
            case nameof(RecordingSessionViewModel.IsLinked):
                if (IsLinked != _session.IsLinked)
                {
                    IsLinked = _session.IsLinked;
                }

                break;
        }
    }

    private void SyncColumn(SlateColumnViewModel col, int index)
    {
        _suppressColumnSync = true;
        try
        {
            col.SelectedIndex = index;
        }
        finally
        {
            _suppressColumnSync = false;
        }
    }

    private void OnFileNumberChangedFromFlow(int number)
    {
        if (!_isActive)
        {
            return;
        }

        SetCurrentNumber(number);
        _session.SetRecordCount(number); // B7：recordCount ↔ 文件号双向同步
    }

    // ---- 记条 / 撤回（唯一写入口 ITakeFlowService；B1-B5/B7 时序在服务内）----

    /// <summary>记条。scrollTake=true 时记条后次列滚动下一项（原版 "+"/音量键路径）。</summary>
    public async Task AdvanceTakeAsync(TakeType type, bool scrollTake = false)
    {
        await _takeFlow.AddItemAsync(
            type,
            CancellationToken.None,
            tkNoteOverride: DescText,
            shtNoteOverride: ShotNoteText);

        // B5：记条后重置评价（Dial 回显 NotChecked；服务已落 0，此处幂等）
        _session.ResetOkStatus();
        UpdatePreviewHint(type);
        DescText = string.Empty; // 原版 setDescNewText：清空录音标注（随 setter 持久化）

        if (scrollTake)
        {
            TakeCol.ScrollNext(IsLinked);
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task AdvanceTake(TakeType type) => AdvanceTakeAsync(type);

    /// <summary>撤回：服务返回恢复的 desc/note（B11），VM 回填（RestoreNotes 语义）。</summary>
    public async Task RewindTakeAsync()
    {
        var result = await _takeFlow.RewindAsync(CancellationToken.None);
        DescText = result.RestoredDesc;
        ShotNoteText = result.RestoredShotNote;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task RewindTake() => RewindTakeAsync();

    [RelayCommand]
    private void SetDesc(string? desc)
    {
        if (desc != null)
        {
            DescText = desc;
        }
    }

    [RelayCommand]
    private void SetShotNote(string? note)
    {
        if (note != null)
        {
            ShotNoteText = note;
        }
    }

    [RelayCommand]
    private void ToggleLink() => _session.SetLink(!_session.IsLinked);

    // ---- 文件号编辑（BLOCKER-1：一律经 ITakeFlowService 唯一写入口）----

    public async Task EditFileNumberAsync(int value)
    {
        if (value < 1)
        {
            value = 1; // B7 下限 1
        }

        await _takeFlow.SetFileNumberAsync(value, CancellationToken.None);
        // 文件号经 FileNumberChanged → 显示 + RecordCount 同步
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task EditFileNumber(int value) => EditFileNumberAsync(value);

    public async Task EditLinkerAsync(string linker)
    {
        await _takeFlow.SetLinkerAsync(linker, CancellationToken.None);
        LinkerText = linker;
        _session.SetRecordLinker(linker);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task EditLinker(string? linker) => EditLinkerAsync(linker ?? "-T");

    /// <summary>前缀编辑（FileCounter EditRequested(Prefix) → C 弹三模式对话框后调用本方法）。</summary>
    public async Task EditPrefixAsync(Models.PrefixType mode, string? customPrefix)
    {
        await _takeFlow.SetPrefixAsync(mode, customPrefix, CancellationToken.None);
        _prefixMode = mode.ToSettingsValue();
        _customPrefix = customPrefix ?? "custom";
        PrefixText = ComputePrefix(_prefixMode);
        _session.SetPrefixType(_prefixMode);
        _session.SetCustomPrefix(_customPrefix);
    }

    // ---- PreviewHint（对齐原版 setDescNewText 的三态 hint）----

    private void UpdatePreviewHint(TakeType type) => PreviewHint = type switch
    {
        TakeType.Fake => HintFake,
        TakeType.End => HintEnd,
        _ => PrevFileNameHint(),
    };

    private void UpdatePreviewHintNormal() => PreviewHint = PrevFileNameHint();

    /// <summary>上一拍文件名提示：number==1 → 空首行（对齐原版 prevFileName() 守卫）。</summary>
    private string PrevFileNameHint()
        => FileNumber <= 1 ? HintSuffix : $"{PrefixText}{LinkerText}{FileNumber - 1:D3}{HintSuffix}";

    // ---- 音量键映射（契约 §4：VolumeUp→AdvanceTake(Normal)+TakeCol.ScrollNext；VolumeDown→RewindTake）----

    private async void OnHardwareKeyPressed(HardwareKey key)
    {
        if (!_isActive)
        {
            return;
        }

        try
        {
            switch (key)
            {
                case HardwareKey.VolumeUp:
                    await AdvanceTakeAsync(TakeType.Normal, scrollTake: true);
                    break;
                case HardwareKey.VolumeDown:
                    await RewindTakeAsync();
                    break;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            // 硬件键回调不崩溃（ADR-009：业务/存储异常由上层统一 Toast）
        }
    }

    // ---- ASR（IAsrService Mock；FinalResult 写入镜头标注——预览“模拟 ASR 写备注”）----

    public void StartAsr()
    {
        _asr.Start();
        AsrStatus = _asr.IsListening ? "识别中" : "空闲";
        OnPropertyChanged(nameof(IsRecording));
    }

    public void StopAsr()
    {
        _asr.Stop();
        AsrStatus = "空闲";
        OnPropertyChanged(nameof(IsRecording));
    }

    /// <summary>ASR 开关（C 的 Mock ASR 按钮；ToggleAsrCommand 生成）。</summary>
    [RelayCommand]
    private void ToggleAsr()
    {
        if (_asr.IsListening)
        {
            StopAsr();
        }
        else
        {
            StartAsr();
        }
    }

    private void OnAsrFinalResult(string text)
    {
        if (!_isActive)
        {
            return;
        }

        if (!string.IsNullOrEmpty(text))
        {
            ShotNoteText = text;
        }

        AsrStatus = "识别完成";
    }

    private void OnAsrError(string message)
    {
        if (!_isActive)
        {
            return;
        }

        AsrStatus = $"识别失败：{message}";
    }
}