using VoiSlate.Controls;
using Xunit;

namespace VoiSlate.Tests.Controls;

/// <summary>契约 §5 FileCounter 编号格式化/校验（FileNumberFormat）锁定测试。</summary>
public class FileNumberFormatTests
{
    // ---- D3 补零 ----

    [Theory]
    [InlineData(1, "001")]
    [InlineData(7, "007")]
    [InlineData(42, "042")]
    [InlineData(999, "999")]
    [InlineData(1234, "1234")]
    public void Pad3_PadsToThreeDigits(int number, string expected)
    {
        Assert.Equal(expected, FileNumberFormat.Pad3(number));
    }

    // ---- 编号解析（"不输入 0"、拒绝非数字与前导零） ----

    [Theory]
    [InlineData("1", 1, true)]
    [InlineData("7", 7, true)]
    [InlineData("42", 42, true)]
    [InlineData("999", 999, true)]
    public void TryParseNumber_AcceptsPositiveIntegers(string text, int expected, bool ok)
    {
        var parsed = FileNumberFormat.TryParseNumber(text, out var number);
        Assert.Equal(ok, parsed);
        Assert.Equal(expected, number);
    }

    [Theory]
    [InlineData("0")]     // 不输入 0（编号下限 1）
    [InlineData("00")]    // 前导零
    [InlineData("03")]    // 原版 int.Parse 不允许的格式
    [InlineData("-1")]    // 负数
    [InlineData("abc")]   // 非数字
    [InlineData("1.5")]   // 非整数
    [InlineData("")]      // 空串
    [InlineData("   ")]   // 空白
    [InlineData(null)]
    public void TryParseNumber_RejectsInvalid(string? text)
    {
        Assert.False(FileNumberFormat.TryParseNumber(text, out _));
    }

    // ---- 前缀标签判定（原版 regex ^[0-9]+$：纯数字 → Date，否则 Custom） ----

    [Theory]
    [InlineData("250816", true)]
    [InlineData("123", true)]
    [InlineData("custom", false)]
    [InlineData("T-01", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsDatePrefix_MatchesOriginalRule(string? prefix, bool expected)
    {
        Assert.Equal(expected, FileNumberFormat.IsDatePrefix(prefix));
    }
}