using System.ComponentModel;
using VoiSlate.Controls;
using VoiSlate.Models;
using Xunit;

namespace VoiSlate.Tests.Controls;

/// <summary>
/// 控件公共表面（契约 §5 签名）无头验证：DP 默认值 / 事件 / 简洁行为。
/// 仅构造与 DP 读写（不启动 Avalonia 平台、不触发动画定时器）。
/// </summary>
public class ControlSurfaceTests
{
    // ---- SlateWheel ----

    [Fact]
    public void SlateWheel_Defaults_MatchContract()
    {
        var wheel = new SlateWheel();
        Assert.Null(wheel.Items);
        Assert.Equal(0, wheel.SelectedIndex);
        Assert.Equal(48.0, wheel.ItemHeight);
        Assert.False(wheel.IsLoop);
        Assert.Null(wheel.SelectedItem);
    }

    [Fact]
    public void SlateWheel_ItemsSet_RaisesSelectedItemChanged_ForInitialSelection()
    {
        var wheel = new SlateWheel();
        var events = new List<string>();
        wheel.SelectedItemChanged += events.Add;

        wheel.Items = new[] { "1", "2", "3" };

        Assert.Equal(new[] { "1" }, events);
        Assert.Equal("1", wheel.SelectedItem);
    }

    [Fact]
    public void SlateWheel_ExternalSelectedIndex_BoundaryClamped()
    {
        var wheel = new SlateWheel { Items = new[] { "A", "B", "C" } };
        wheel.SelectedIndex = 99;
        Assert.Equal("C", wheel.SelectedItem); // 越界收敛到末项

        wheel.SelectedIndex = -5;
        Assert.Equal("A", wheel.SelectedItem);
    }

    [Fact]
    public void SlateWheel_ScrollNext_Linked()
    {
        var wheel = new SlateWheel { Items = new[] { "1", "2", "3" } };
        wheel.ScrollNext(isLinked: true);
        Assert.Equal(1, wheel.SelectedIndex);
    }

    [Fact]
    public void SlateWheel_ScrollNext_NotLinked_Stays()
    {
        var wheel = new SlateWheel { Items = new[] { "1", "2", "3" } };
        wheel.SelectedIndex = 1;
        wheel.ScrollNext(isLinked: false);
        Assert.Equal(1, wheel.SelectedIndex);
    }

    [Fact]
    public void SlateWheel_ScrollNext_AtLast_Stops()
    {
        var wheel = new SlateWheel { Items = new[] { "1", "2", "3" } };
        wheel.SelectedIndex = 2;
        wheel.ScrollNext(isLinked: true);
        Assert.Equal(2, wheel.SelectedIndex);
    }

    [Fact]
    public void SlateWheel_ScrollPrev_AtFirst_Stops()
    {
        var wheel = new SlateWheel { Items = new[] { "1", "2", "3" } };
        wheel.ScrollPrev(isLinked: true);
        Assert.Equal(0, wheel.SelectedIndex);
    }

    [Fact]
    public void SlateWheel_LoopMode_Wraps()
    {
        var wheel = new SlateWheel { Items = new[] { "1", "2", "3" }, IsLoop = true };
        wheel.SelectedIndex = 2;
        wheel.ScrollNext(isLinked: true);
        Assert.Equal(0, wheel.SelectedIndex);

        wheel.ScrollPrev(isLinked: true);
        Assert.Equal(2, wheel.SelectedIndex);
    }

    // ---- FileCounter ----

    [Fact]
    public void FileCounter_Defaults_MatchContract()
    {
        var counter = new FileCounter();
        Assert.Equal("1", counter.NumberText);
        Assert.Equal(1, counter.NumberValue);
        Assert.Equal("-T", counter.Linker);
        Assert.Null(counter.Prefix);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("7", 7)]
    [InlineData("1234", 1234)]
    public void FileCounter_NumberTextDrivesReadOnlyNumberValue(string text, int expected)
    {
        var counter = new FileCounter { NumberText = text };
        Assert.Equal(expected, counter.NumberValue);
    }

    [Theory]
    [InlineData("0")] // 不输入 0
    [InlineData("03")] // 前导零
    [InlineData("abc")]
    [InlineData("-3")]
    [InlineData("")]
    public void FileCounter_InvalidNumberText_FallsBackToOne(string text)
    {
        var counter = new FileCounter { NumberText = text };
        Assert.Equal(1, counter.NumberValue);
    }

    // ---- DialFAB / DialOption ----

    [Fact]
    public void DialFAB_Defaults_MatchContract()
    {
        var fab = new DialFAB();
        Assert.Null(fab.Options);
        Assert.Null(fab.SelectedOption);
    }

    [Fact]
    public void DialFAB_SelectedOption_IsTwoWaySurface()
    {
        var fab = new DialFAB();
        var ok = new DialOption("声音可", "✔", TkStatus.Ok);
        fab.SelectedOption = ok;
        Assert.Same(ok, fab.SelectedOption);

        fab.SelectedOption = null; // C 的 ResetOkStatus 通路
        Assert.Null(fab.SelectedOption);
    }

    [Fact]
    public void DialOption_CarriesDisplayDataOnly()
    {
        var option = new DialOption("画面过", "👍", ShtStatus.Nice);
        Assert.Equal("画面过", option.Label);
        Assert.Equal("👍", option.Icon);
        Assert.Equal(ShtStatus.Nice, option.EnumValue);
    }

    // ---- ToastService ----

    [Fact]
    public void ToastService_CurrentMessage_RaisesPropertyChanged()
    {
        var service = new ToastService();
        var changed = new List<string?>();
        ((INotifyPropertyChanged)service).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        service.CurrentMessage = "hello";
        service.CurrentMessage = null;

        Assert.Contains(nameof(ToastService.CurrentMessage), changed);
        Assert.Null(service.CurrentMessage);
    }

    [Fact]
    public void ToastHost_Defaults_MatchContract()
    {
        var host = new ToastHost();
        Assert.Null(host.Message);
    }
}