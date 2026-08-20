using VoiSlate.Models;

namespace VoiSlate.Tests.TestDoubles;

#pragma warning disable CS0067 // Fake 事件暂无订阅者

/// <summary>固定时间（B/Service 确定性单测；对齐 C-12 ITimeProvider 注入要求）。</summary>
public sealed class FakeTimeProvider(DateTime now) : VoiSlate.Services.ITimeProvider
{
    public static readonly DateTime Fixed = new(2026, 8, 20, 12, 0, 0);

    public FakeTimeProvider() : this(Fixed)
    {
    }

    public DateTime Now => now;
}

/// <summary>内存日志仓（P0.5 产 Fake 集，C-4 归属规则：E 后续接管演进）。</summary>
public sealed class FakeLogRepository : VoiSlate.Services.ILogRepository
{
    private readonly List<(string Date, string Key, SlateLogItem Item)> _items = [];
    private int _nextId = 1;

    public IReadOnlyList<(string Date, string Key, SlateLogItem Item)> Items => _items;

    public Task<IReadOnlyList<string>> GetDatesAsync()
        => Task.FromResult<IReadOnlyList<string>>(_items.Select(x => x.Date).Distinct().OrderBy(x => x).ToList());

    public Task<IReadOnlyList<SlateLogItem>> GetByDateAsync(string date)
        => Task.FromResult<IReadOnlyList<SlateLogItem>>(_items.Where(x => x.Date == date).Select(x => Clone(x.Item)).ToList());

    public Task AddAsync(string date, string key, SlateLogItem item)
    {
        var copy = Clone(item);
        copy.Id = _nextId++;
        _items.Add((date, key, copy));
        item.Id = copy.Id;
        return Task.CompletedTask;
    }

    public Task ReplaceAtAsync(string date, int index, SlateLogItem item)
    {
        var idx = IndexOf(date, index);
        var target = _items[idx];
        var copy = Clone(item);
        copy.Id = target.Item.Id;
        _items[idx] = (target.Date, target.Key, copy);
        return Task.CompletedTask;
    }

    public Task<SlateLogItem> RemoveAtAsync(string date, int index)
    {
        var idx = IndexOf(date, index);
        var removed = _items[idx];
        _items.RemoveAt(idx);
        return Task.FromResult(Clone(removed.Item));
    }

    public Task<SlateLogItem> RemoveLastAsync(string date)
    {
        var idx = _items.FindLastIndex(x => x.Date == date);
        if (idx < 0) throw new InvalidOperationException("no items for date");
        var removed = _items[idx];
        _items.RemoveAt(idx);
        return Task.FromResult(Clone(removed.Item));
    }

    public Task RemoveByKeyAsync(string date, string key)
    {
        _items.RemoveAll(x => x.Date == date && x.Key == key);
        return Task.CompletedTask;
    }

    public Task ClearAsync(string date)
    {
        _items.RemoveAll(x => x.Date == date);
        return Task.CompletedTask;
    }

    private int IndexOf(string date, int index)
    {
        var list = _items.Where(x => x.Date == date).ToList();
        if (index < 0 || index >= list.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _items.IndexOf(list[index]);
    }

    private static SlateLogItem Clone(SlateLogItem src) => new()
    {
        Id = src.Id,
        Scn = src.Scn,
        Sht = src.Sht,
        Tk = src.Tk,
        FilenamePrefix = src.FilenamePrefix,
        FilenameLinker = src.FilenameLinker,
        FilenameNum = src.FilenameNum,
        TkNote = src.TkNote,
        ShtNote = src.ShtNote,
        ScnNote = src.ScnNote,
        OkTk = src.OkTk,
        OkSht = src.OkSht,
    };
}

/// <summary>内存 picker_history（P0.5 Fake）。</summary>
public sealed class FakePickerHistoryStore : VoiSlate.Services.IPickerHistoryStore
{
    private readonly List<List<string>> _entries = [];

    public IReadOnlyList<List<string>> Entries => _entries;

    public Task<int> CountAsync() => Task.FromResult(_entries.Count);

    public Task<IReadOnlyList<string>> GetLastAsync() =>
        Task.FromResult<IReadOnlyList<string>>(_entries.Count == 0 ? [] : _entries[^1]);

    public Task AddAsync(IReadOnlyList<string> entry)
    {
        _entries.Add(entry.ToList());
        return Task.CompletedTask;
    }

    public Task RemoveLastAsync()
    {
        if (_entries.Count > 0)
        {
            _entries.RemoveAt(_entries.Count - 1);
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        _entries.Clear();
        return Task.CompletedTask;
    }
}

/// <summary>内存会话设置（P0.5 Fake）。</summary>
public sealed class FakeSessionSettingsStore : VoiSlate.Services.ISessionSettingsStore
{
    private readonly Dictionary<string, object?> _data = new();

    public Dictionary<string, object?> Data => _data;

    public Task<string?> GetStringAsync(string key) =>
        Task.FromResult(_data.TryGetValue(key, out var v) ? Convert.ToString(v) : null);

    public Task<int?> GetIntAsync(string key) =>
        Task.FromResult(_data.TryGetValue(key, out var v) && v is int i ? (int?)i : null);

    public Task<bool?> GetBoolAsync(string key) =>
        Task.FromResult(_data.TryGetValue(key, out var v) && v is bool b ? (bool?)b : null);

    public Task<string> GetStringAsync(string key, string defaultValue) =>
        Task.FromResult(_data.TryGetValue(key, out var v) ? Convert.ToString(v) ?? defaultValue : defaultValue);

    public Task<int> GetIntAsync(string key, int defaultValue) =>
        Task.FromResult(_data.TryGetValue(key, out var v) && v is int i ? i : defaultValue);

    public Task<bool> GetBoolAsync(string key, bool defaultValue) =>
        Task.FromResult(_data.TryGetValue(key, out var v) && v is bool b ? b : defaultValue);

    public Task SetAsync(string key, object? value)
    {
        _data[key] = value;
        return Task.CompletedTask;
    }
}

/// <summary>内存会话状态（P0.5 Fake）。</summary>
public sealed class FakeSessionState : VoiSlate.Services.ISessionState
{
    public int SceneIndex { get; set; }
    public int ShotIndex { get; set; }
    public int TakeIndex { get; set; }
    public int TakeCount => 200;
    public bool IsLinked { get; set; } = true;
    public event Action? SessionChanged;
}