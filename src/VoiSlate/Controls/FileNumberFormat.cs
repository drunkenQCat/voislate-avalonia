// FileNumberFormat — FileCounter 编号相关的纯格式化/校验逻辑（可单测）。
//
// 对齐契约 §5 FileCounter 行为协议与 Flutter 原版 file_counter.dart / recorder_file_num.dart：
//   * 显示 D3 补零（number.toString().padLeft(3, '0')）；
//   * "Number=整型（不输入 0，显示 D3 补零）"：编号下限 1（原版 FileNumberingService 下限 1）；
//   * 前缀卡片标签：全数字前缀显示 Date，否则 Custom（原版 regex ^[0-9]+$ 判定）。

using System.Globalization;

namespace VoiSlate.Controls;

/// <summary>FileCounter 编号格式化与校验的纯逻辑。</summary>
public static class FileNumberFormat
{
    /// <summary>D3 补零展示。</summary>
    public static string Pad3(int number) => number.ToString("D3", CultureInfo.InvariantCulture);

    /// <summary>
    /// 解析用户输入的数字串。规则：
    ///   空串/非数字 → false；
    ///   值为 0 → false（"不输入 0"，编号下限 1）；
    ///   带前导零（如 "03"）→ false（原版对话框直接 int.Parse，不允许前导零）；
    ///   上界校验由调用方（服务/VM）决定，这里按 int 上限。
    /// </summary>
    public static bool TryParseNumber(string? text, out int number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (text.Length > 1 && text[0] == '0')
        {
            return false;
        }

        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        if (parsed < 1)
        {
            return false;
        }

        number = parsed;
        return true;
    }

    /// <summary>前缀是否为"纯数字"（日期型 yymmdd）：是 → Date 标签，否 → Custom 标签。</summary>
    public static bool IsDatePrefix(string? prefix) =>
        !string.IsNullOrEmpty(prefix) && prefix.All(char.IsAsciiDigit);
}