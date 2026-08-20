using System.Text.Json.Serialization;
using LiteDB;

namespace VoiSlate.Models;

/// <summary>
/// 场记条目（对齐原版 SlateLogItem；字段逐一对齐，含 fake/wild 哨兵值语义）。
/// LiteDB 用 <see cref="Id"/>（[BsonId]，JSON 导出忽略）；JSON 用 camelCase 短名（VoiSlateJson）。
/// </summary>
public sealed class SlateLogItem
{
    [BsonId]
    [JsonIgnore]
    public int Id { get; set; }

    public string Scn { get; set; } = string.Empty;
    public string Sht { get; set; } = string.Empty;
    public int Tk { get; set; }
    public string FilenamePrefix { get; set; } = string.Empty;
    public string FilenameLinker { get; set; } = string.Empty;
    public int FilenameNum { get; set; }

    /// <summary>文件名 = prefix + linker + num 补零 3 位（对齐原版 fileName getter）。</summary>
    [JsonIgnore]
    public string FileName => FilenamePrefix + FilenameLinker + FilenameNum.ToString("D3");

    public string TkNote { get; set; } = string.Empty;
    public string ShtNote { get; set; } = string.Empty;
    public string ScnNote { get; set; } = string.Empty;
    public TkStatus OkTk { get; set; } = TkStatus.NotChecked;
    public ShtStatus OkSht { get; set; } = ShtStatus.NotChecked;

    public override string ToString() =>
        $"SlateLogItem{{scn: {Scn}, sht: {Sht}, tk: {Tk}, fileName: {FileName}, tkNote: {TkNote}, shtNote: {ShtNote}, scnNote: {ScnNote}, okTk: {OkTk}, okSht: {OkSht}}}";
}