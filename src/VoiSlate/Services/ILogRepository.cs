using VoiSlate.Models;

using LiteDB;
using VoiSlate.Infrastructure;
namespace VoiSlate.Services;

/// <summary>
/// 场记持久化（对齐原版 Hive 按日 box + 'dates' 列表；LiteDB 单集合 "logs"，
/// 按日期键筛选、按 _id 插入序排序，保持原版"当日追加序"语义）。
/// </summary>
public interface ILogRepository
{
    Task<IReadOnlyList<string>> GetDatesAsync();
    Task<IReadOnlyList<SlateLogItem>> GetByDateAsync(string date);
    Task AddAsync(string date, string key, SlateLogItem item);
    Task ReplaceAtAsync(string date, int index, SlateLogItem item);
    Task<SlateLogItem> RemoveAtAsync(string date, int index);
    Task<SlateLogItem> RemoveLastAsync(string date);
    Task RemoveByKeyAsync(string date, string key);
    Task ClearAsync(string date);
}

public sealed class LiteDbLogRepository(LiteDbStore store) : ILogRepository
{
    private ILiteCollection<LogDoc> Collection => store.Database.GetCollection<LogDoc>("logs");

    private sealed class LogDoc
    {
        [BsonId]
        public int Id { get; set; }
        public string Date { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Scn { get; set; } = string.Empty;
        public string Sht { get; set; } = string.Empty;
        public int Tk { get; set; }
        public string FilenamePrefix { get; set; } = string.Empty;
        public string FilenameLinker { get; set; } = string.Empty;
        public int FilenameNum { get; set; }
        public string TkNote { get; set; } = string.Empty;
        public string ShtNote { get; set; } = string.Empty;
        public string ScnNote { get; set; } = string.Empty;
        public TkStatus OkTk { get; set; }
        public ShtStatus OkSht { get; set; }
    }

    private static LogDoc ToDoc(SlateLogItem item) => new()
    {
        Date = string.Empty, // 由调用方填
        Key = string.Empty,
        Scn = item.Scn,
        Sht = item.Sht,
        Tk = item.Tk,
        FilenamePrefix = item.FilenamePrefix,
        FilenameLinker = item.FilenameLinker,
        FilenameNum = item.FilenameNum,
        TkNote = item.TkNote,
        ShtNote = item.ShtNote,
        ScnNote = item.ScnNote,
        OkTk = item.OkTk,
        OkSht = item.OkSht,
    };

    private static SlateLogItem ToItem(LogDoc d) => new()
    {
        Id = d.Id,
        Scn = d.Scn,
        Sht = d.Sht,
        Tk = d.Tk,
        FilenamePrefix = d.FilenamePrefix,
        FilenameLinker = d.FilenameLinker,
        FilenameNum = d.FilenameNum,
        TkNote = d.TkNote,
        ShtNote = d.ShtNote,
        ScnNote = d.ScnNote,
        OkTk = d.OkTk,
        OkSht = d.OkSht,
    };

    public Task<IReadOnlyList<string>> GetDatesAsync()
    {
        var dates = Collection.Query().Select(x => x.Date).ToList().Distinct().OrderBy(x => x).ToList();
        return Task.FromResult<IReadOnlyList<string>>(dates);
    }

    public Task<IReadOnlyList<SlateLogItem>> GetByDateAsync(string date)
    {
        var items = Collection.Query()
            .Where(x => x.Date == date)
            .OrderBy(x => x.Id)
            .ToList()
            .Select(ToItem)
            .ToList();
        return Task.FromResult<IReadOnlyList<SlateLogItem>>(items);
    }

    public Task AddAsync(string date, string key, SlateLogItem item)
    {
        var doc = ToDoc(item);
        doc.Date = date;
        doc.Key = key;
        Collection.Insert(doc);
        item.Id = doc.Id;
        return Task.CompletedTask;
    }

    public async Task ReplaceAtAsync(string date, int index, SlateLogItem item)
    {
        var docs = DocsOf(date);
        if (index < 0 || index >= docs.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var target = docs[index];
        item.Id = target.Id;
        var doc = ToDoc(item);
        doc.Id = target.Id;
        doc.Date = date;
        doc.Key = target.Key;
        Collection.Update(doc);
        await Task.CompletedTask;
    }

    public Task<SlateLogItem> RemoveAtAsync(string date, int index)
    {
        var docs = DocsOf(date);
        if (index < 0 || index >= docs.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var target = docs[index];
        Collection.Delete(target.Id);
        return Task.FromResult(ToItem(target));
    }

    public Task<SlateLogItem> RemoveLastAsync(string date)
    {
        var docs = DocsOf(date);
        var last = docs[^1];
        Collection.Delete(last.Id);
        return Task.FromResult(ToItem(last));
    }

    public Task RemoveByKeyAsync(string date, string key)
    {
        foreach (var doc in DocsOf(date).Where(d => d.Key == key))
        {
            Collection.Delete(doc.Id);
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync(string date)
    {
        foreach (var doc in DocsOf(date))
        {
            Collection.Delete(doc.Id);
        }

        return Task.CompletedTask;
    }

    private List<LogDoc> DocsOf(string date) =>
        Collection.Query().Where(x => x.Date == date).OrderBy(x => x.Id).ToList();
}