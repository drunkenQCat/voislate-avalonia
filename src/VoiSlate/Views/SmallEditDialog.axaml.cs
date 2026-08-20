using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace VoiSlate.Views;

/// <summary>
/// Agent C：通用单值编辑对话框（文件号/链接符/前缀模式编辑共用；C 职责区 UI 组件）。
/// choices 非空时显示下拉（ValueBox 隐藏），否则显示文本输入框。
/// </summary>
public partial class SmallEditDialog : Window
{
    private readonly bool _useChoices;
    private (bool Ok, string Value) _result;

    /// <summary>Runtime-loader 可达性（AVLN3001 消音）；正常入口为 ShowAsync。</summary>
    public SmallEditDialog()
    {
        InitializeComponent();
    }

    public SmallEditDialog(string title, string label, string initial, IReadOnlyList<string>? choices)
    {
        InitializeComponent();
        Title = title;
        LabelText.Text = label;
        if (choices is { Count: > 0 })
        {
            _useChoices = true;
            ModeCombo.IsVisible = true;
            ValueBox.IsVisible = false;
            ModeCombo.ItemsSource = choices;
            ModeCombo.SelectedIndex = 0;
            if (initial.Length > 0)
            {
                var idx = choices.ToList().IndexOf(initial);
                if (idx >= 0)
                {
                    ModeCombo.SelectedIndex = idx;
                }
            }
        }
        else
        {
            ValueBox.Text = initial;
            ValueBox.Focus();
        }

        CancelButton.Click += OnCancelClick;
        OkButton.Click += OnOkClick;
    }

    /// <summary>打开对话框；返回 (是否确认, 值文本)。owner 为空时独立窗口展示（fallback）。</summary>
    public static Task<(bool Ok, string Value)> ShowAsync(
        Window? owner,
        string title,
        string label,
        string initial = "",
        IReadOnlyList<string>? choices = null)
    {
        var dialog = new SmallEditDialog(title, label, initial, choices);
        if (owner is { } window)
        {
            return dialog.ShowDialog<(bool, string)>(window);
        }

        var tcs = new TaskCompletionSource<(bool Ok, string Value)>();
        dialog.Closed += (_, _) => tcs.TrySetResult(dialog._result);
        dialog.Show();
        return tcs.Task;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        _result = (false, string.Empty);
        Close(_result);
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        var value = _useChoices
            ? (ModeCombo.SelectedItem as string) ?? string.Empty
            : ValueBox.Text ?? string.Empty;
        _result = (true, value);
        Close(_result);
    }
}