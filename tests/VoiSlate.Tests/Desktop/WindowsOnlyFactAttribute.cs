using Xunit;

namespace VoiSlate.Tests.Desktop;

/// <summary>
/// 上游 P8 的工作台用例依赖 Windows 专属能力（DPAPI Token 保护、NAudio WinMM 采集）。
/// 非 Windows 自动 Skip，保持 ubuntu CI 全绿；Windows 上照常运行，不改变作者测试语义。
/// </summary>
public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "依赖 Windows 专属能力（DPAPI/WinMM），仅 Windows 运行";
    }
}

public sealed class WindowsOnlyTheoryAttribute : TheoryAttribute
{
    public WindowsOnlyTheoryAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "依赖 Windows 专属能力（DPAPI/WinMM），仅 Windows 运行";
    }
}
