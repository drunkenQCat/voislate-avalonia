using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using VoiSlate.Models;

namespace VoiSlate.Services;

/// <summary>
/// 计划 CSV 解析（契约 §3 CsvScheduleParser；逐行对齐原版 helper/schedule_csv_parser.dart）。
/// 列格式（7 列）：0 场景号，1 场景内容，2 镜头号，3 补充，4 景别（默认"近景"），5 镜头内容，6 补充。
/// - 首行为表头，无条件丢弃（原版 parseCSVData removeAt(0)）。
/// - 场景：col0 的数字=Key、首个字母=Fix、其余字符=场景 Type（如 "1A万星园" → Key"1" Fix"A" Type"A万星园"）；
///   场景 append = col1 + "，" + col3（col1 为空则 append 为空串）；objects 默认 ['Boom']（CSV 无对象列，契约注明）。
/// - 镜：col2 的数字=Key、首个字母=Fix；镜 Type = col4 或 "近景"；镜 append = col5 + col6（直接拼接）；
///   objects 默认 ['Boom']；镜按 "num-fix" 在场内去重。
/// - 场景按 "num-fix" 全局去重（重复场景号整行丢弃，原版 processedInfo 语义）；
///   纯镜行（col0 空）追加到上一场景组；双空行跳过。
/// - 修复（ADR-009）：原版对"文件首行即纯镜行"会空引用崩溃；此处丢弃并继续，不复制崩溃。
/// </summary>
public static class CsvScheduleParser
{
    private const int ColumnCount = 7;

    private static readonly Regex NumRegExp = new(@"\d+", RegexOptions.Compiled);
    private static readonly Regex FixRegExp = new(@"[a-zA-Z]", RegexOptions.Compiled);
    private static readonly Regex LocRegExp = new(@"[^\d、\n]+", RegexOptions.Compiled);

    /// <summary>解析 CSV 流为场次计划列表（表头丢弃、去重、默认对象等语义见类注）。</summary>
    public static Task<IReadOnlyList<SceneSchedule>> ParseAsync(Stream stream, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var rows = ReadRows(stream);
        var groups = DivideScns(rows);
        var scenes = GenerateScenes(groups);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<SceneSchedule>>(scenes);
    }

    private static List<string[]> ReadRows(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            // 原版 csv 包不跳过空白行（由逻辑层跳过）；保持原始行进入逻辑层。
            IgnoreBlankLines = true,
        };
        using var csv = new CsvReader(reader, config);
        var rows = new List<string[]>();
        while (csv.Read())
        {
            rows.Add(csv.Parser.Record ?? Array.Empty<string>());
        }

        // 原版 parseCSVData: csvData.removeAt(0) —— 无条件丢弃表头行。
        if (rows.Count > 0)
        {
            rows.RemoveAt(0);
        }

        return rows;
    }

    private sealed record BasicInfo(string Num, string Fix, string Type)
    {
        public override string ToString() => $"{Num}-{Fix}";
    }

    private static string[] Pad(string[] row)
    {
        if (row.Length >= ColumnCount)
        {
            return row;
        }

        var padded = new string[ColumnCount];
        Array.Copy(row, padded, row.Length);
        for (var i = row.Length; i < ColumnCount; i++)
        {
            padded[i] = string.Empty;
        }

        return padded;
    }

    private static BasicInfo? GetScnNumAndLocation(string[] row)
    {
        if (row[0].Length == 0)
        {
            return null;
        }

        var num = FirstMatch(NumRegExp, row[0]) ?? "0";
        var fix = FirstMatch(FixRegExp, row[0]) ?? string.Empty;
        var loc = FirstMatch(LocRegExp, row[0]) ?? string.Empty;
        return new BasicInfo(num, fix, loc);
    }

    private static BasicInfo? GetShtNumAndType(string[] row)
    {
        if (row[2].Length == 0)
        {
            return null;
        }

        var num = FirstMatch(NumRegExp, row[2]) ?? "0";
        var fix = FirstMatch(FixRegExp, row[2]) ?? string.Empty;
        var shtType = row[4].Length == 0 ? "近景" : row[4];
        return new BasicInfo(num, fix, shtType);
    }

    private static string? FirstMatch(Regex regex, string input)
    {
        var m = regex.Match(input);
        return m.Success ? m.Value : null;
    }

    public const string SceneAppendSeparator = "，";

    internal static string? GetScnContent(string[] row) =>
        row[1].Length == 0 ? null : $"{row[1]}{SceneAppendSeparator}{row[3]}";

    internal static string GetShtContent(string[] row) => row[5];

    internal static string GetShtAppend(string[] row) => row[6];

    private static ScheduleItem SpawnSceneItem(BasicInfo info, string[] row) => new(
        info.Num,
        info.Fix,
        new Note(["Boom"], info.Type, GetScnContent(row) ?? string.Empty));

    private static ScheduleItem SpawnShotItem(BasicInfo info, string[] row) => new(
        info.Num,
        info.Fix,
        new Note(["Boom"], info.Type, GetShtContent(row) + GetShtAppend(row)));

    /// <summary>divideScns：按场景号分组（场景去重；纯镜行并入上一组；空行/双空行跳过）。</summary>
    private static List<List<string[]>> DivideScns(List<string[]> rows)
    {
        var groups = new List<List<string[]>>();
        var processedInfo = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rawRow in rows)
        {
            var row = Pad(rawRow);
            if (row.All(string.IsNullOrEmpty))
            {
                continue; // 空行跳过
            }

            var scnInfo = GetScnNumAndLocation(row);
            var shtInfo = GetShtNumAndType(row);
            if (scnInfo == null && shtInfo == null)
            {
                continue; // 场景与镜都为空的行跳过
            }

            if (scnInfo == null)
            {
                if (groups.Count > 0)
                {
                    groups[^1].Add(row); // 追加到上一场景组
                }

                // 文件首行即纯镜行：原版此处引用 null.scnBasicInfo 崩溃；修复为丢弃（ADR-009）。
                continue;
            }

            var infoKey = scnInfo.ToString()!;
            if (!processedInfo.Add(infoKey))
            {
                continue; // 场景号已处理过 → 整行丢弃（含其镜头，原版语义）
            }

            groups.Add([row]);
        }

        return groups;
    }

    /// <summary>generateScns + generateNewScn + getShtList：每个分组生成一个场次计划。</summary>
    private static List<SceneSchedule> GenerateScenes(List<List<string[]>> groups)
    {
        var result = new List<SceneSchedule>(groups.Count);
        foreach (var group in groups)
        {
            var head = group[0];
            var scnInfo = GetScnNumAndLocation(head)!; // 分组首行必有场景号
            var scnItem = SpawnSceneItem(scnInfo, head);

            var uniqueKeys = new HashSet<string>(StringComparer.Ordinal);
            var shots = new List<ScheduleItem>();
            foreach (var row in group)
            {
                var shtInfo = GetShtNumAndType(row);
                if (shtInfo == null)
                {
                    continue;
                }

                var key = shtInfo.ToString()!;
                if (!uniqueKeys.Add(key))
                {
                    continue; // 镜号（num-fix）已在场中 → 跳过
                }

                shots.Add(SpawnShotItem(shtInfo, row));
            }

            result.Add(new SceneSchedule(shots, scnItem));
        }

        return result;
    }
}
/// <summary>
/// 实例适配器：以 B 交付的契约接口形状 <see cref="ICsvScheduleParser"/> 暴露静态解析能力
/// （B 的 ScheduleViewModel 依赖该接口；DI 建议注册本实现以启用真实 CSV 解析代替 Noop 桩）。
/// </summary>
public sealed class CsvScheduleParserService : ICsvScheduleParser
{
    public Task<IReadOnlyList<SceneSchedule>> ParseAsync(Stream stream, CancellationToken ct) =>
        CsvScheduleParser.ParseAsync(stream, ct);
}
