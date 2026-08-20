namespace VoiSlate.Services;

#pragma warning disable CS0067 // Mock 事件暂不触发（新 ASR 服务接入后实现）

/// <summary>
/// 语音识别服务（原版 ifly 识别）。按约束：**ifly 部分暂且 mock**——新应用将使用另外的服务，
/// 本接口保持稳定，实现为 Mock（P0.5 预产；E 演进时替换为真实服务）。
/// </summary>
public interface IAsrService
{
    bool IsAvailable { get; }
    bool IsListening { get; }
    event Action<string>? PartialResult;
    event Action<string>? FinalResult;
    event Action<string>? ErrorOccurred;
    void Start();
    void Stop();
}

/// <summary>Mock ASR：不做真实识别；Start 后立即回调一个占位 FinalResult（P0.5 行为，便于冒烟）。</summary>
public sealed class MockAsrService : IAsrService
{
    public bool IsAvailable => true;
    public bool IsListening { get; private set; }

    public event Action<string>? PartialResult;
    public event Action<string>? FinalResult;
    public event Action<string>? ErrorOccurred;

    public void Start()
    {
        IsListening = true;
        // P0.5 Mock：不联网、不识别（新服务接入后替换）。
    }

    public void Stop()
    {
        IsListening = false;
    }
}