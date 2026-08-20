using VoiSlate.Controls;
using VoiSlate.Models;
using Xunit;

namespace VoiSlate.Tests.Controls;

/// <summary>契约 §5 DialFAB 状态回显色映射（DialStatusPalette）锁定测试。</summary>
public class DialStatusPaletteTests
{
    [Fact]
    public void NotChecked_IsLight()
    {
        Assert.Equal(DialStatusPalette.NotChecked, DialStatusPalette.StatusColor(TkStatus.NotChecked));
        Assert.Equal(DialStatusPalette.NotChecked, DialStatusPalette.StatusColor(ShtStatus.NotChecked));
        Assert.Equal(DialStatusPalette.NotChecked, DialStatusPalette.StatusColor(null));
    }

    [Fact]
    public void Ok_IsGreen()
    {
        Assert.Equal(DialStatusPalette.Ok, DialStatusPalette.StatusColor(TkStatus.Ok));
        Assert.Equal(DialStatusPalette.Ok, DialStatusPalette.StatusColor(ShtStatus.Ok));
    }

    [Fact]
    public void Bad_IsRed()
    {
        Assert.Equal(DialStatusPalette.Bad, DialStatusPalette.StatusColor(TkStatus.Bad));
    }

    [Fact]
    public void Nice_IsGold()
    {
        Assert.Equal(DialStatusPalette.Nice, DialStatusPalette.StatusColor(ShtStatus.Nice));
    }

    [Fact]
    public void UnknownValue_FallsBackToNeutral()
    {
        Assert.Equal(DialStatusPalette.Neutral, DialStatusPalette.StatusColor(TakeType.Normal));
        Assert.Equal(DialStatusPalette.Neutral, DialStatusPalette.StatusColor("anything"));
        Assert.Equal(DialStatusPalette.Neutral, DialStatusPalette.StatusColor(12345));
    }

    [Fact]
    public void ResourceKeys_MapToContractColors()
    {
        Assert.Equal(DialStatusPalette.NotCheckedResourceKey, DialStatusPalette.StatusResourceKey(TkStatus.NotChecked));
        Assert.Equal(DialStatusPalette.OkResourceKey, DialStatusPalette.StatusResourceKey(ShtStatus.Ok));
        Assert.Equal(DialStatusPalette.BadResourceKey, DialStatusPalette.StatusResourceKey(TkStatus.Bad));
        Assert.Equal(DialStatusPalette.NiceResourceKey, DialStatusPalette.StatusResourceKey(ShtStatus.Nice));
    }

    [Fact]
    public void PaletteConstants_MatchOriginalSources()
    {
        // 原版取色核对：record_page.dart 背景卡 0xFFF2F5DE = DialNotChecked/NotChecked。
        Assert.Equal(ColorHex("#F2F5DE"), DialStatusPalette.NotChecked);
        // 语义色与原版 Colors.green / Colors.red 的 Material 值一致。
        Assert.Equal(ColorHex("#4CAF50"), DialStatusPalette.Ok);
        Assert.Equal(ColorHex("#F44336"), DialStatusPalette.Bad);
    }

    private static Avalonia.Media.Color ColorHex(string hex) => Avalonia.Media.Color.Parse(hex);
}