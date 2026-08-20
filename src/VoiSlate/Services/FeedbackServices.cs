using VoiSlate.Models;

namespace VoiSlate.Services;

/// <summary>
/// 提示/反馈（原版 Fluttertoast；Avalonia 侧由 C 提供 ToastHost 承载，E 演进）。
/// P0.5 提供 Noop 实现。
/// </summary>
public interface IToastService
{
    void Show(string message);
}

public sealed class NoopToastService : IToastService
{
    public void Show(string message)
    {
        // P0.5: no-op（C 阶段由 ToastHost 接管）
    }
}

/// <summary>触觉反馈（原版 Vibration；桌面无触觉，抽象为可注入）。P0.5 提供 Noop 实现。</summary>
public interface IHapticsService
{
    void Vibrate(int amplitude, int durationMs);
}

public sealed class NoopHapticsService : IHapticsService
{
    public void Vibrate(int amplitude, int durationMs)
    {
        // P0.5: no-op
    }
}