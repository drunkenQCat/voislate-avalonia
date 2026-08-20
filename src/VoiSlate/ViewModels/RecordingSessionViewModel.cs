using CommunityToolkit.Mvvm.ComponentModel;
using VoiSlate.Models;
using VoiSlate.Services;

namespace VoiSlate.ViewModels;

/// <summary>
/// 会话状态单例（契约 §4 RecordingSessionViewModel；实现 ISessionState — C-1：ITakeFlowService 只依赖本接口）。
/// 对齐原版 SlateStatusNotifier：13 键即时写 ISessionSettingsStore（键名 <see cref="SessionKeys"/>；
/// 默认值 isLinked=true、recordLinker="-T"、prefixType="default"、customPrefix="custom"、
/// date=TodayKey；当日 recordCount 恢复、跨日归 1 —— 构造函数加载）。
/// On{X}Changed 持久化；desc/note 修复原版 "setNote 不 notify" 缺陷（绑定天然通知）。
/// </summary>
public partial class RecordingSessionViewModel : ObservableObject, ISessionState
{
    /// <summary>N2：take 列范围常量 1..200（对齐原版 take 1..200）。</summary>
    public const int DefaultTakeCount = 200;

    private readonly ISessionSettingsStore _settings;
    private bool _suppressPersist;

    /// <summary>
    /// 初始化加载任务（LiteDB/内存存储均为同步外表，通常构造内即完成；C 启动序可 await 保证加载完备）。
    /// </summary>
    public Task Initialization { get; }

    public RecordingSessionViewModel(ISessionSettingsStore settings, ITimeProvider time)
    {
        _settings = settings;
        Date = VoiSlateDates.TodayKey(time.Now);
        Initialization = LoadAsync();
    }

    /// <summary>只读（构造时固定为 TodayKey，对齐原版 _date）。</summary>
    public string Date { get; }

    /// <summary>ISessionState.TakeCount = 200。</summary>
    public int TakeCount => DefaultTakeCount;

    [ObservableProperty]
    private int _selectedSceneIndex;

    [ObservableProperty]
    private int _selectedShotIndex;

    [ObservableProperty]
    private int _selectedTakeIndex;

    [ObservableProperty]
    private bool _isLinked;

    [ObservableProperty]
    private int _recordCount = 1;

    [ObservableProperty]
    private string _recordLinker = "-T";

    /// <summary>字符串模式："default" / "sound devices" / "custom"（对齐原版 prefixType）。</summary>
    [ObservableProperty]
    private string _prefixType = "default";

    [ObservableProperty]
    private string _customPrefix = "custom";

    [ObservableProperty]
    private string _currentDesc = string.Empty;

    [ObservableProperty]
    private string _currentNote = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PendingTakeOk))]
    private TkStatus _okTk = TkStatus.NotChecked;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PendingShotOk))]
    private ShtStatus _okSht = ShtStatus.NotChecked;

    /// <summary>§2 TkPending 承载：pending 与持久化 OkTk 同值（原版 tkPending ↔ setOkStatus 双向同步）。</summary>
    public TkStatus PendingTakeOk
    {
        get => OkTk;
        set => SetOkTake(value);
    }

    public ShtStatus PendingShotOk
    {
        get => OkSht;
        set => SetOkShot(value);
    }

    // ---- 方法（对齐原版 setIndex/setNote/setLink/setRecordLinker/setPrefixType/setCustomPrefix/setOkStatus）----

    public void SelectScene(int index) => SelectedSceneIndex = index;

    public void SelectShot(int index) => SelectedShotIndex = index;

    public void SelectTake(int index) => SelectedTakeIndex = index;

    public void SetRecordCount(int count) => RecordCount = count;

    public void SetLink(bool linked) => IsLinked = linked;

    public void SetRecordLinker(string linker) => RecordLinker = linker;

    public void SetPrefixType(string? value)
    {
        if (value != null)
        {
            PrefixType = value;
        }
    }

    public void SetCustomPrefix(string value) => CustomPrefix = value;

    public void SetDesc(string? desc)
    {
        if (desc != null)
        {
            CurrentDesc = desc;
        }
    }

    public void SetNote(string? note)
    {
        if (note != null)
        {
            CurrentNote = note;
        }
    }

    public void SetOkTake(TkStatus status) => OkTk = status;

    public void SetOkShot(ShtStatus status) => OkSht = status;

    /// <summary>对齐原版 setOkStatus：任意组合“取/存 + 重置”。</summary>
    public void SetOkStatus(TkStatus? currentTk = null, ShtStatus? currentSht = null, bool doReset = false)
    {
        if (currentTk != null)
        {
            OkTk = currentTk.Value;
        }

        if (currentSht != null)
        {
            OkSht = currentSht.Value;
        }

        if (doReset)
        {
            ResetOkStatus();
        }
    }

    public void ResetOkStatus()
    {
        OkTk = TkStatus.NotChecked;
        OkSht = ShtStatus.NotChecked;
    }

    // ---- On{X}Changed 持久化（原版键名与默认值语义）----

    partial void OnSelectedSceneIndexChanged(int value)
    {
        if (!_suppressPersist)
        {
            Persist(SessionKeys.SceneIndex, value);
            Persist(SessionKeys.Date, Date);
        }

        SessionChanged?.Invoke();
    }

    partial void OnSelectedShotIndexChanged(int value)
    {
        if (!_suppressPersist)
        {
            Persist(SessionKeys.ShotIndex, value);
            Persist(SessionKeys.Date, Date);
        }

        SessionChanged?.Invoke();
    }

    partial void OnSelectedTakeIndexChanged(int value)
    {
        if (!_suppressPersist)
        {
            Persist(SessionKeys.TakeIndex, value);
            Persist(SessionKeys.Date, Date);
        }

        SessionChanged?.Invoke();
    }

    partial void OnIsLinkedChanged(bool value)
    {
        if (!_suppressPersist)
        {
            Persist(SessionKeys.IsLinked, value);
        }

        SessionChanged?.Invoke();
    }

    partial void OnRecordCountChanged(int value)
    {
        if (!_suppressPersist)
        {
            Persist(SessionKeys.RecordCount, value);
            Persist(SessionKeys.Date, Date);
        }

        SessionChanged?.Invoke();
    }

    partial void OnRecordLinkerChanged(string value)
    {
        if (!_suppressPersist)
        {
            Persist(SessionKeys.RecordLinker, value);
        }

        SessionChanged?.Invoke();
    }

    partial void OnPrefixTypeChanged(string value)
    {
        if (!_suppressPersist)
        {
            Persist(SessionKeys.PrefixType, value);
        }

        SessionChanged?.Invoke();
    }

    partial void OnCustomPrefixChanged(string value)
    {
        if (!_suppressPersist)
        {
            Persist(SessionKeys.CustomPrefix, value);
        }

        SessionChanged?.Invoke();
    }

    partial void OnCurrentDescChanged(string value)
    {
        if (!_suppressPersist)
        {
            Persist(SessionKeys.Desc, value);
        }
    }

    partial void OnCurrentNoteChanged(string value)
    {
        if (!_suppressPersist)
        {
            Persist(SessionKeys.Note, value);
        }
    }

    partial void OnOkTkChanged(TkStatus value)
    {
        if (!_suppressPersist)
        {
            Persist(SessionKeys.OkTk, (int)value);
        }

        SessionChanged?.Invoke();
    }

    partial void OnOkShtChanged(ShtStatus value)
    {
        if (!_suppressPersist)
        {
            Persist(SessionKeys.OkSht, (int)value);
        }

        SessionChanged?.Invoke();
    }

    // ---- 构造函数加载（对齐原版字段初始化；当日恢复/跨日归 1）----

    private async Task LoadAsync()
    {
        _suppressPersist = true;
        try
        {
            SelectedSceneIndex = await _settings.GetIntAsync(SessionKeys.SceneIndex, 0);
            SelectedShotIndex = await _settings.GetIntAsync(SessionKeys.ShotIndex, 0);
            SelectedTakeIndex = await _settings.GetIntAsync(SessionKeys.TakeIndex, 0);
            IsLinked = await _settings.GetBoolAsync(SessionKeys.IsLinked, true);

            var savedDate = await _settings.GetStringAsync(SessionKeys.Date, string.Empty);
            RecordCount = savedDate == Date
                ? await _settings.GetIntAsync(SessionKeys.RecordCount, 1)
                : 1;

            RecordLinker = await _settings.GetStringAsync(SessionKeys.RecordLinker, "-T");
            PrefixType = await _settings.GetStringAsync(SessionKeys.PrefixType, "default");
            CustomPrefix = await _settings.GetStringAsync(SessionKeys.CustomPrefix, "custom");
            CurrentDesc = await _settings.GetStringAsync(SessionKeys.Desc, string.Empty);
            CurrentNote = await _settings.GetStringAsync(SessionKeys.Note, string.Empty);
            OkTk = (TkStatus)await _settings.GetIntAsync(SessionKeys.OkTk, 0);
            OkSht = (ShtStatus)await _settings.GetIntAsync(SessionKeys.OkSht, 0);
        }
        finally
        {
            _suppressPersist = false;
        }
    }

    private void Persist(string key, object value) => _ = _settings.SetAsync(key, value);

    // ---- ISessionState 显式实现（C-1）----

    int ISessionState.SceneIndex
    {
        get => SelectedSceneIndex;
        set => SelectScene(value);
    }

    int ISessionState.ShotIndex
    {
        get => SelectedShotIndex;
        set => SelectShot(value);
    }

    int ISessionState.TakeIndex
    {
        get => SelectedTakeIndex;
        set => SelectTake(value);
    }

    bool ISessionState.IsLinked
    {
        get => IsLinked;
        set => SetLink(value);
    }

    /// <summary>会话变更事件（ISessionState；索引/联动/评价/计数等变更时触发）。</summary>
    public event Action? SessionChanged;
}