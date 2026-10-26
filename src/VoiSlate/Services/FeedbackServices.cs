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

/// <summary>
/// 触觉反馈已改为静态注册点 <see cref="Haptics"/>（Android 装真实现，桌面为 null）。
/// 原 <c>IHapticsService</c>/<c>NoopHapticsService</c> 的唯一消费者是已删除的 legacy
/// <c>TakeFlowService</c>；而 DI 实例实现会在「Avalonia 初始化早于平台注册」时拿到 null，
/// 所以不再走 DI 而走静态注册点。
/// </summary>