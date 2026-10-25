using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace VoiSlate.Desktop;

/// <summary>
/// 工作台弹窗的统一句柄。桌面（TopLevel 是 Window）走独立窗口 + ShowDialog，保持既有观感；
/// Android 等无法创建 Window 的宿主降级为组件内 overlay（遮罩 + 卡片），调用方 API 完全一致，
/// 因此同一套界面代码可以在两个平台编译并运行。
/// </summary>
public abstract class WorkspaceDialog
{
    public abstract Task ShowAsync(Window? owner = null);
    public abstract Task<T> ShowAsync<T>(Window? owner = null);
    public abstract void Close();
    public abstract void Close<T>(T result);

    /// <summary>桌面：独立窗口，按 Window.ShowDialog 语义返回关闭结果（X 关闭得到 default）。</summary>
    public static WorkspaceDialog ForWindow(Window dialog, Window owner) => new WindowDialog(dialog, owner);

    /// <summary>无窗口宿主：在 host 里叠一层遮罩 + 卡片。所有 dialog 结束前会从 host 移除。</summary>
    public static WorkspaceDialog ForOverlay(Panel host, string title, Control body, double width) =>
        new OverlayDialog(host, title, body, width);

    private sealed class WindowDialog(Window dialog, Window ownerWindow) : WorkspaceDialog
    {
        public override Task ShowAsync(Window? owner = null) => dialog.ShowDialog(owner ?? ownerWindow);
        public override Task<T> ShowAsync<T>(Window? owner = null) => dialog.ShowDialog<T>(owner ?? ownerWindow);
        public override void Close() => dialog.Close();
        public override void Close<TResult>(TResult result) => dialog.Close(result);
    }

    private sealed class OverlayDialog : WorkspaceDialog
    {
        private readonly Panel _host;
        private readonly Grid _layer;
        private readonly TaskCompletionSource<object?> _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _done;

        public OverlayDialog(Panel host, string title, Control body, double width)
        {
            _host = host;
            var card = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#F6F8F4")),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(20),
                Margin = new Thickness(12),
                MaxWidth = width,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var content = new StackPanel { Spacing = 16 };
            content.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 20,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse("#35523D")),
                TextWrapping = TextWrapping.Wrap,
            });
            content.Children.Add(body);
            card.Child = new ScrollViewer
            {
                Content = content,
                MaxHeight = 620,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            };
            var scrim = new Border { Background = new SolidColorBrush(Color.Parse("#73101618")) };
            scrim.PointerPressed += (_, e) => { e.Handled = true; };
            _layer = new Grid();
            _layer.Children.Add(scrim);
            _layer.Children.Add(card);
            host.Children.Add(_layer);
        }

        public override Task ShowAsync(Window? owner = null) => _closed.Task;

        public override async Task<T> ShowAsync<T>(Window? owner = null)
        {
            var result = await _closed.Task;
            return result is T typed ? typed : default!;
        }

        public override void Close() => Finish(null);
        public override void Close<TResult>(TResult result) => Finish(result);

        private void Finish(object? result)
        {
            if (_done) return;
            _done = true;
            _host.Children.Remove(_layer);
            _closed.TrySetResult(result);
        }
    }
}