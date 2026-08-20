// LoadingOverlay — 全局加载覆盖层（契约 §5 LoadingOverlay 行；对齐原版 EasyLoading 语义）。
//
// 依赖属性：
//   IsActive bool  OneWay   激活时铺满父容器、半透明遮罩 + 居中加载圈，
//                           并拦截指针事件（IsHitTestVisible 跟随 IsActive）。
//
// 放置：C 在页面/窗口根层叠的最上层放置；激活期间阻断下层交互（对齐"absorbing"语义）。

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace VoiSlate.Controls;

/// <summary>全局加载覆盖层。</summary>
public class LoadingOverlay : TemplatedControl
{
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<LoadingOverlay, bool>(
            nameof(IsActive), defaultValue: false, defaultBindingMode: Avalonia.Data.BindingMode.OneWay);

    private Panel? _root;

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _root = e.NameScope.Find<Panel>("PART_Root");
        SyncActive();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsActiveProperty)
        {
            SyncActive();
        }
    }

    private void SyncActive()
    {
        if (_root is null)
        {
            return;
        }

        _root.IsVisible = IsActive;
        IsHitTestVisible = IsActive;
    }
}