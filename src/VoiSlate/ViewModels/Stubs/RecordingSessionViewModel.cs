using CommunityToolkit.Mvvm.ComponentModel;
using VoiSlate.Models;
using VoiSlate.Services;

namespace VoiSlate.ViewModels;

/// <summary>
/// Agent C stub：会话状态 VM（契约 §4 —— Agent B 产出，本文件为编译用占位，合并后删除）。
/// 实现 ISessionState（C-1：ITakeFlowService 只依赖接口不依赖具体类型）。
/// stub 仅承载可观察状态；13 键持久化/音量键/待定评价流转归 B 的正式实现。
/// 注意：P0.5 的 ISessionState 注册仍指向 SessionStateImpl（App.axaml.cs 既有注册块），
/// B 合入后由 B 将 ISessionState 指向本类（RecordingSessionViewModel）。
/// </summary>
public partial class RecordingSessionViewModel : ObservableObject, ISessionState
{
    [ObservableProperty]
    private int _sceneIndex;

    [ObservableProperty]
    private int _shotIndex;

    [ObservableProperty]
    private int _takeIndex;

    [ObservableProperty]
    private bool _isLinked = true;

    [ObservableProperty]
    private int _recordCount = 1;

    [ObservableProperty]
    private string _recordLinker = "-T";

    [ObservableProperty]
    private PrefixType _prefixType = PrefixType.Default;

    [ObservableProperty]
    private string _customPrefix = "custom";

    [ObservableProperty]
    private string _currentDesc = string.Empty;

    [ObservableProperty]
    private string _currentNote = string.Empty;

    [ObservableProperty]
    private TkStatus _okTk;

    [ObservableProperty]
    private ShtStatus _okSht;

    [ObservableProperty]
    private TkStatus _pendingTakeOk;

    [ObservableProperty]
    private ShtStatus _pendingShotOk;

    /// <summary>ISessionState：take 列范围常量（N2 = 200）。</summary>
    public int TakeCount => 200;

    /// <summary>只读日期（stub：直接取当前时间；B 注入 ITimeProvider）。</summary>
    public string Date => VoiSlateDates.TodayKey(DateTime.Now);

    /// <summary>ISessionState 会话变更事件。</summary>
    public event Action? SessionChanged;

    partial void OnSceneIndexChanged(int value) => SessionChanged?.Invoke();
    partial void OnShotIndexChanged(int value) => SessionChanged?.Invoke();
    partial void OnTakeIndexChanged(int value) => SessionChanged?.Invoke();
    partial void OnIsLinkedChanged(bool value) => SessionChanged?.Invoke();

    /// <summary>契约 §4 命令（stub：仅更新状态；B 的实现写入 ISessionSettingsStore）。</summary>
    public void SelectScene(int index) => SceneIndex = index;

    public void SelectShot(int index) => ShotIndex = index;

    public void SelectTake(int index) => TakeIndex = index;

    public void SetRecordCount(int count) => RecordCount = count;

    public void SetLink(bool linked) => IsLinked = linked;

    public void SetOkTake(TkStatus status)
    {
        OkTk = status;
        PendingTakeOk = status;
    }

    public void SetOkShot(ShtStatus status)
    {
        OkSht = status;
        PendingShotOk = status;
    }

    public void ResetOkStatus()
    {
        PendingTakeOk = TkStatus.NotChecked;
        PendingShotOk = ShtStatus.NotChecked;
    }
}