using VoiSlate.Models;

namespace VoiSlate.Services;

/// <summary>
/// 会话状态只读访问（C-1：ITakeFlowService 只依赖本接口，不依赖 VM 具体类型）。
/// 由 RecordingSessionViewModel 实现（B）；P0.5 提供 SessionStateImpl 最小实现占位。
/// N2：TakeCount = 200（take 列范围常量，对齐原版 take 1..200）。
/// </summary>
public interface ISessionState
{
    int SceneIndex { get; set; }
    int ShotIndex { get; set; }
    int TakeIndex { get; set; }
    int TakeCount { get; }
    bool IsLinked { get; set; }
    event Action? SessionChanged;
}

/// <summary>P0.5 最小实现（B 交付 RecordingSessionViewModel 后移除）。</summary>
public sealed class SessionStateImpl : ISessionState
{
    public const int DefaultTakeCount = 200;

    private int _sceneIndex;
    private int _shotIndex;
    private int _takeIndex;
    private bool _isLinked = true;

    public int SceneIndex { get => _sceneIndex; set { _sceneIndex = value; SessionChanged?.Invoke(); } }
    public int ShotIndex { get => _shotIndex; set { _shotIndex = value; SessionChanged?.Invoke(); } }
    public int TakeIndex { get => _takeIndex; set { _takeIndex = value; SessionChanged?.Invoke(); } }
    public int TakeCount => DefaultTakeCount;
    public bool IsLinked { get => _isLinked; set { _isLinked = value; SessionChanged?.Invoke(); } }
    public event Action? SessionChanged;
}