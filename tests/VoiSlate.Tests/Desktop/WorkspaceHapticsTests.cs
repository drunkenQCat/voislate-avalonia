using Avalonia.Controls;
using VoiSlate.Services;
using Xunit;

namespace VoiSlate.Tests.Desktop;

/// <summary>
/// 触觉反馈（原版 Flutter <c>vibration</c> 包）：记条 128/150、假条 240/900、撤回 240/900
/// （见 <c>/home/quantumcat/my_repo/voislate-repo/lib/pages/record_page.dart:296-299,347-348</c>）。
/// 桌面没有振子，<see cref="Haptics.Impl"/> 为 null 时全部静默丢弃；这里用替身捕获调用序列。
/// </summary>
public sealed partial class DesktopWorkspaceTests
{
    /// <summary>替身实现：把每次震动记下来，Dispose 时把注册点清空（避免污染其他用例）。</summary>
    private sealed class FakeHaptics : IDisposable
    {
        public List<(int Amplitude, int Duration)> Calls { get; } = [];

        public FakeHaptics() => Haptics.Impl = (amplitude, duration) => Calls.Add((amplitude, duration));

        public void Dispose() => Haptics.Impl = null;
    }

    [Fact]
    public void Haptics_use_the_original_amplitudes_and_durations()
    {
        using var fake = new FakeHaptics();

        Haptics.TakeSaved(fake: false); // 正常记条
        Haptics.TakeSaved(fake: true);  // 假条
        Haptics.Undone();               // 撤回

        Assert.Equal(new[] { (128, 150), (240, 900), (240, 900) }, fake.Calls.ToArray());
    }

    [Fact]
    public void Haptics_are_silent_when_no_platform_implementation_is_installed()
    {
        Haptics.Impl = null;

        // 桌面路径：不抛异常、不做事（原版也是靠能力探测置 _canVibrate=false）。
        Haptics.TakeSaved(false);
        Haptics.Undone();

        Assert.False(Haptics.Available);
    }

    [Fact]
    public Task Saving_a_take_vibrates_short_and_undoing_vibrates_long() => RunUi(async () =>
    {
        using var fake = new FakeHaptics();
        using var f = await Create();
        var window = await WindowFor(f);
        try
        {
            Invoke(Control<Button>(window, "add-take"));
            await Until(() => f.Workspace.Logs.Count == 1);
            Assert.Equal(new[] { (128, 150) }, fake.Calls.ToArray());

            Invoke(Control<Button>(window, "undo-take"));
            await Until(() => f.Workspace.Logs.Count == 0);
            Assert.Equal(new[] { (128, 150), (240, 900) }, fake.Calls.ToArray());
        }
        finally { window.Close(); }
    });
}