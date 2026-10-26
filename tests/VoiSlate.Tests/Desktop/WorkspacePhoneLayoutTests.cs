using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace VoiSlate.Tests.Desktop;

// 390×840（手机宽度）下的布局体检：没有真机也要能自动判定「底部导航在、内容没被切掉」。
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

    [Fact]
    public Task Ui_phone_width_keeps_bottom_nav_and_controls_inside_viewport() => RunUi(async () =>
    {
        using var f = await Create(); var window = await WindowFor(f);
        try
        {
            await f.Workspace.SetTracksAsync("Boom，演员甲"); await f.Workspace.SaveDraftAsync("声音干净", "视线接上"); await f.Workspace.SaveTakeAsync();
            window.Width = 390; window.Height = 840; Dispatcher.UIThread.RunJobs();
            CaptureFrame(window, "phone-record");

            // 1) 窄屏必须有底部 Tab（P8 原生实现是「左栏整列归零」= 完全没导航）
            foreach (var key in new[] { "navb-record", "navb-plan", "navb-log", "navb-settings" })
            {
                var tab = Control<Button>(window, key);
                Assert.True(tab.IsEffectivelyVisible, $"{key} 应可见");
                Assert.True(tab.Bounds.Height >= 44, $"{key} 触屏目标过小：{tab.Bounds.Height}");
                // 四等分撑满 390：主题里 Button 默认 HorizontalAlignment=Left，漏掉 Stretch 会只剩文字宽（实测 24px，触屏几乎点不中）
                Assert.True(tab.Bounds.Width >= 87, $"{key} 没有横向撑满四等分：{tab.Bounds.Width}");
            }

            // 2) 三页都不许有内容跑到 390 之外（滚动条自身零件不计：贴着视口右缘是正常的）
            foreach (var (tabId, name) in new[] { ("navb-plan", "phone-plan"), ("navb-log", "phone-timeline"), ("navb-record", "phone-back") })
            {
                Invoke(Control<Button>(window, tabId)); Dispatcher.UIThread.RunJobs(); CaptureFrame(window, name);
                var offenders = new List<string>();
                foreach (var c in window.GetVisualDescendants().OfType<Control>())
                {
                    if (!c.IsVisible || c.Bounds.Width <= 0 || c.Bounds.Height <= 0) continue;
                    if (c is ScrollBar || c.FindAncestorOfType<ScrollBar>() is not null) continue;
                    var rect = InWindow(c, window);
                    if (rect.Right > 391 || rect.Left < -1) offenders.Add($"{name}: {Chain(c)} rect={rect}");
                }
                Assert.True(offenders.Count == 0, $"client={window.ClientSize}\n" + string.Join("\n", offenders.Take(10)));
            }

            // 3) 窄屏也要能直接进设置（左栏隐藏后原本只有记录页头有齿轮）
            Invoke(Control<Button>(window, "navb-settings"));
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