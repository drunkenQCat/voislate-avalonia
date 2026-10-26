using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace VoiSlate.Tests.Desktop;

// 390×840（手机宽度）下的布局体检：没有真机也要能自动判定「没有底部 Tab、翻页手势有效、内容没被切掉」。
public sealed partial class DesktopWorkspaceTests
{
    private static Rect InWindow(Visual element, Visual window)
    {
        // 两个角都过一遍 TranslatePoint：Viewbox/ScaleTransform 下的局部尺寸会骗人（PathIcon 的几何bounds 可能上千）。
        var tl = element.TranslatePoint(new Point(0, 0), window) ?? new Point(0, 0);
        var br = element.TranslatePoint(new Point(element.Bounds.Width, element.Bounds.Height), window) ?? tl;
        return new Rect(tl, br);
    }

    private static string Chain(Visual c)
    {
        var parts = new List<string>();
        for (Visual? v = c; v is not null && parts.Count < 5; v = v.GetVisualParent())
        {
            var label = v.GetType().Name;
            if (v is TextBlock tb && tb.Text is { Length: > 0 } text) label += $"[{text[..Math.Min(16, text.Length)]}]";
            if (v is ContentControl { Content: string s } && s.Length > 0) label += $"[{s[..Math.Min(16, s.Length)]}]";
            parts.Add(label);
        }
        return string.Join(" < ", parts);
    }

    private static void Drag(Window window, double fromY, double toY, double x = 195)
    {
        window.MouseDown(new Point(x, fromY), MouseButton.Left);
        window.MouseMove(new Point(x, (fromY + toY) / 2));
        window.MouseMove(new Point(x, toY));
        window.MouseUp(new Point(x, toY), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task Ui_phone_page_for_swipe_follows_the_vertical_order() => RunUi(async () =>
    {
        using var f = await Create(); var window = await WindowFor(f);
        try
        {
            window.Width = 390; window.Height = 840; Dispatcher.UIThread.RunJobs();
            var view = window.View;
            Assert.Equal("record", view.CurrentPage);
            Assert.Equal("plan", view.PageForSwipe(120));    // 下滑 → 上一页（拍摄计划）
            Assert.Equal("log", view.PageForSwipe(-120));    // 上滑 → 下一页（场记时间轴）
            Assert.Null(view.PageForSwipe(60));              // 没到阈值：回弹，不翻页
            Invoke(Control<Button>(window, "open-plan"));
            Assert.Equal("plan", view.CurrentPage);
            Assert.Null(view.PageForSwipe(120));             // 已经在最上面，没有上一页
            Invoke(Control<Button>(window, "nav-back"));
            Invoke(Control<Button>(window, "open-timeline"));
            Assert.Equal("log", view.CurrentPage);
            Assert.Null(view.PageForSwipe(-120));            // 已经在最下面，没有下一页
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task Ui_phone_drag_switches_page_without_a_bottom_tab() => RunUi(async () =>
    {
        using var f = await Create(); var window = await WindowFor(f);
        try
        {
            window.Width = 390; window.Height = 840; Dispatcher.UIThread.RunJobs();
            CaptureFrame(window, "phone-record");
            var view = window.View;

            // 1) 底部多选 Tab 必须不存在（用户明确不要）
            var navButtons = window.GetVisualDescendants().OfType<Button>()
                .Select(AutomationProperties.GetAutomationId).Where(id => id?.StartsWith("navb-") == true).ToList();
            Assert.True(navButtons.Count == 0, "还留着底部 Tab：" + string.Join(",", navButtons));

            // 2) 右侧要有三颗指示点，且不吃点击（否则会挡住翻页手势）
            var dots = window.GetVisualDescendants().OfType<StackPanel>()
                .Single(p => AutomationProperties.GetAutomationId(p) == "pager-dots");
            Assert.Equal(3, dots.Children.Count);
            Assert.False(dots.IsHitTestVisible);

            // 3) 真手势：从上往下拖 = 回拍摄计划（记录页在顶部，边界允许翻页）
            Drag(window, 170, 330);
            Assert.True(view.CurrentPage == "plan", $"下滑后应该在拍摄计划，实际 {view.CurrentPage}");
            CaptureFrame(window, "phone-plan-by-swipe");

            // 4) 计划页已在最上一页：继续下滑不该翻页（边界回弹），回记录页走页头 ✕
            Drag(window, 170, 330);
            Assert.True(view.CurrentPage == "plan", $"最上一页再下滑不该翻页，实际 {view.CurrentPage}");
            Invoke(Control<Button>(window, "nav-back"));
            Assert.Equal("record", view.CurrentPage);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task Ui_phone_width_keeps_controls_inside_viewport() => RunUi(async () =>
    {
        using var f = await Create(); var window = await WindowFor(f);
        try
        {
            await f.Workspace.SetTracksAsync("Boom，演员甲"); await f.Workspace.SaveDraftAsync("声音干净", "视线接上"); await f.Workspace.SaveTakeAsync();
            window.Width = 390; window.Height = 840; Dispatcher.UIThread.RunJobs();
            CaptureFrame(window, "phone-record");

            // 三页都不许有内容跑到 390 之外（滚动条自身零件不计：贴着视口右缘是正常的）
            foreach (var (id, name) in new[] { ("open-plan", "phone-plan"), ("open-timeline", "phone-timeline") })
            {
                Invoke(Control<Button>(window, id)); Dispatcher.UIThread.RunJobs(); CaptureFrame(window, name);
                var offenders = new List<string>();
                foreach (var c in window.GetVisualDescendants().OfType<Control>())
                {
                    if (!c.IsVisible || c.Bounds.Width <= 0 || c.Bounds.Height <= 0) continue;
                    if (c is ScrollBar || c.FindAncestorOfType<ScrollBar>() is not null) continue;
                    var rect = InWindow(c, window);
                    if (rect.Right > 391 || rect.Left < -1) offenders.Add($"{name}: {Chain(c)} rect={rect}");
                }
                Assert.True(offenders.Count == 0, $"client={window.ClientSize}\n" + string.Join("\n", offenders.Take(10)));
                Invoke(Control<Button>(window, "nav-back")); // 页头的 ✕ 回记录页，下一轮从记录页出发
            }

            // 3) 窄屏也要能直接进设置（左栏隐藏后只剩记录页头那颗齿轮）
            Invoke(Control<Button>(window, "open-settings"));
            var dialog = Assert.Single(window.OwnedWindows);
            Assert.Contains(dialog.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("工程名称") == true);

            // 4) 弹窗内部同样不许溢出（横向 StackPanel 按钮排在 390 宽下会把最后一个按钮挤到窗口外）
            Dispatcher.UIThread.RunJobs();
            var spill = new List<string>();
            foreach (var c in dialog.GetVisualDescendants().OfType<Control>())
            {
                if (!c.IsVisible || c.Bounds.Width <= 0 || c.Bounds.Height <= 0) continue;
                if (c is ScrollBar || c.FindAncestorOfType<ScrollBar>() is not null) continue;
                var rect = InWindow(c, dialog);
                if (rect.Right > dialog.ClientSize.Width + 1 || rect.Left < -1) spill.Add($"dialog: {Chain(c)} rect={rect}");
            }
            Assert.True(spill.Count == 0, $"dialog client={dialog.ClientSize}\n" + string.Join("\n", spill.Take(10)));
            dialog.Close();
        }
        finally { window.Close(); }
    });
}
