using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using VoiSlate.Models;

namespace VoiSlate.Views;

/// <summary>颜色工具（占位：D 的 VoiSlatePalette 合入后改用主题资源键，契约 §6）。</summary>
internal static class Palette
{
    public static readonly IBrush Bg = Solid("#FBFBFB");
    public static readonly IBrush Primary = Solid("#0067A0");   // bahamaBlue
    public static readonly IBrush OkGreen = Solid("#2E7D32");
    public static readonly IBrush BadRed = Solid("#C62828");
    public static readonly IBrush NiceGold = Solid("#B8860B");
    public static readonly IBrush TextHint = Solid("#888888");

    internal static IBrush Solid(string hex) => new SolidColorBrush(Color.Parse(hex));
}

/// <summary>okTk → 色点（NotChecked 灰 / Ok 绿 / Bad 红）。</summary>
public sealed class TkStatusToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        TkStatus.Ok => Palette.OkGreen,
        TkStatus.Bad => Palette.BadRed,
        _ => Solid("#9E9E9E"),
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static IBrush Solid(string hex) => Palette.Solid(hex);
}

/// <summary>okSht → 色点（NotChecked 灰 / Ok 绿 / Nice 金黄）。</summary>
public sealed class ShtStatusToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ShtStatus.Ok => Palette.OkGreen,
        ShtStatus.Nice => Palette.NiceGold,
        _ => Solid("#9E9E9E"),
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static IBrush Solid(string hex) => Palette.Solid(hex);
}

/// <summary>当前页键 → 导航高亮（ConverterParameter 为页键；匹配时 bahamaBlue 浅底）。</summary>
public sealed class PageKeyToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value as string;
        var target = parameter as string;
        return string.Equals(key, target, StringComparison.Ordinal)
            ? Palette.Solid("#220067A0")
            : Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
/// <summary>值 == ConverterParameter（字符串"Ok"/"Nice"/"NotChecked"/"1"…）→ true。用于图标/元素按枚举显隐。</summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var v = value?.ToString();
        var p = parameter?.ToString();
        return string.Equals(v, p, StringComparison.Ordinal);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>日期 Tab 高亮：值 == 参数（yyMMdd）→ #2196F3，否则 #222。供 .log-tab 前景与底部 3px 边。</summary>
public sealed class TabForegroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal)
            ? new SolidColorBrush(Color.Parse("#2196F3"))
            : new SolidColorBrush(Color.Parse("#222222"));

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>日期 Tab 下边框：值 == 参数 → 3px #2196F3，否则透明。配合 TabForegroundConverter。</summary>
public sealed class TabBorderConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal)
            ? new Thickness(0, 0, 0, 3)
            : new Thickness(0);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>场记行底色（HTML TK_BG）：Ok→#4CAF50 / Bad→#F44336 / else→#9E9E9E（NotChecked 灰）。</summary>
public sealed class TakeRowBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            TkStatus.Ok => new SolidColorBrush(Color.Parse("#4CAF50")),
            TkStatus.Bad => new SolidColorBrush(Color.Parse("#F44336")),
            _ => new SolidColorBrush(Color.Parse("#9E9E9E")),
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>字符串非空 → true（行内容按值显隐，HTML li-line 空项省略）。</summary>
public sealed class StringHasValueConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !string.IsNullOrWhiteSpace(value as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>当前页键 == 参数 → true（底部导航高亮 pill 的 IsVisible；按钮自身保持静态背景保证可命中）。</summary>
public sealed class PageKeyToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
