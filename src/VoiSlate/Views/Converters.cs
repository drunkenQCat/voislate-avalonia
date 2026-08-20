using System.Globalization;
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