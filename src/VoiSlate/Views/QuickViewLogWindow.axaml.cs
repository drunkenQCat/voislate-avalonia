using Avalonia.Controls;
using VoiSlate.Models;

namespace VoiSlate.Views;

/// <summary>
/// Agent C：场记速览对话框（quick_view_log_dialog 语义）。
/// DataContext/ItemsSource 由调用方注入 IReadOnlyList&lt;SlateLogItem&gt;。
/// </summary>
public partial class QuickViewLogWindow : Window
{
    public QuickViewLogWindow()
    {
        InitializeComponent();
    }

    public static Task ShowAsync(Window? owner, IReadOnlyList<SlateLogItem> notes)
    {
        var window = new QuickViewLogWindow
        {
            DataContext = notes,
        };
        window.NotesList.ItemsSource = notes;
        if (owner is { } windowOwner)
        {
            return window.ShowDialog(windowOwner);
        }

        window.Show();
        return Task.CompletedTask;
    }
}