using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Xunit;

namespace VoiSlate.Tests.Desktop;

/// <summary>
/// 原版 Flutter 的两套提示：
/// ①`Fluttertoast.showToast`（纯文本、约 2 秒）②`showSnackBar`+`SnackBarAction`（5 秒 + 「恢复」）。
/// 这里钉住第 ② 种的三个语义：删了就有提示、动作能真的恢复、窗口过去就再也恢复不了。
/// </summary>
public sealed partial class DesktopWorkspaceTests
{
    private static Button EditButtonFor(Window window, string shot)
    {
        var card = window.GetVisualDescendants().OfType<Border>()
            .Single(b => AutomationProperties.GetAutomationId(b) == $"shot-1A-{shot}");
        return card.GetVisualDescendants().OfType<Button>().First(b => (b.Content as string) == "•••");
    }

    private static void DeleteShot(Window window, string shot)
    {
        Invoke(EditButtonFor(window, shot));
        var dialog = Assert.Single(window.OwnedWindows);
        var delete = dialog.GetVisualDescendants().OfType<Button>()
            .First(b => (b.Content as string)?.StartsWith("删除", StringComparison.Ordinal) == true);
        Invoke(delete);
    }

    [Fact]
    public Task Plan_delete_offers_an_undo_toast_that_really_restores() => RunUi(async () =>
    {
        using var f = await Create();
        var window = await WindowFor(f);
        try
        {
            var before = f.Workspace.Scenes.Single(s => s.Info.Name == "1A").Count;
            Invoke(Control<Button>(window, "open-plan"));
            DeleteShot(window, "2B");
            await Until(() => f.Workspace.Scenes.Single(s => s.Info.Name == "1A").Count == before - 1);

            // 原版 scene_schedule_page.dart:94 的「第 X 镜已删除」+ SnackBarAction「恢复」。
            Assert.Equal("第 2B 镜已删除", window.View.ToastText);
            Assert.True(f.Workspace.CanRestorePlan);
            var action = Control<Button>(window, "toast-action");
            Assert.Equal("恢复", action.Content as string);
            CaptureFrame(window, "toast-undo");
            window.Width = 390; window.Height = 840; Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            CaptureFrame(window, "toast-undo-narrow");

            Invoke(action);
            await Until(() => f.Workspace.Scenes.Single(s => s.Info.Name == "1A").Count == before);
            Assert.Contains(f.Workspace.Scenes.Single(s => s.Info.Name == "1A"), s => s.Name == "2B");
        }
        finally { window.Close(); }
    });

    [Fact]
    public async Task Discarding_the_restore_point_makes_undo_impossible()
    {
        // 原版语义：SnackBar 消失后「恢复」就没了，DeletePlanItemAsync 的恢复点必须同时作废。
        using var f = await Create();
        await f.Workspace.DeletePlanItemAsync("1A", "2B");
        await Until(() => f.Workspace.CanRestorePlan);
        f.Workspace.DiscardRestore();
        Assert.False(f.Workspace.CanRestorePlan);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Workspace.RestorePlanItemAsync());
    }

    [Fact]
    public Task Shot_end_toast_keeps_the_original_wording() => RunUi(async () =>
    {
        using var f = await Create();
        var window = await WindowFor(f);
        try
        {
            Invoke(Control<Button>(window, "take-more"));
            var dialog = Assert.Single(window.OwnedWindows);
            Invoke(Button(dialog, "✓ 保存并结束本镜"));
            await Until(() => f.Workspace.Draft.Ended);
            Assert.Equal("镜头结束，画面与声音默认评价为优良", window.View.ToastText);
            CaptureFrame(window, "toast-shot-end");
        }
        finally { window.Close(); }
    });
}
