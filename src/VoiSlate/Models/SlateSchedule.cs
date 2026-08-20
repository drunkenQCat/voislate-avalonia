using System.Collections;
using System.Text.Json.Serialization;
using LiteDB;

namespace VoiSlate.Models;

/// <summary>计划条目（对齐原版 ScheduleItem：key+fix，name=key+fix 计算属性；setter 同步重算）。</summary>
public class ScheduleItem
{
    [BsonId]
    [JsonIgnore]
    public int Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Fix { get; set; } = string.Empty;

    /// <summary>name = key + fix（原版为缓存字段；此处计算属性保持不变量，C-13：key/fix setter 同步重算）。</summary>
    public string Name
    {
        get => Key + Fix;
        set
        {
            // 兼容既有数据/序列化：name 为派生值，忽略直接赋值。
        }
    }

    public Note Note { get; set; } = new();

    public ScheduleItem()
    {
    }

    public ScheduleItem(string key, string fix, Note note)
    {
        Key = key;
        Fix = fix;
        Note = note;
    }

    public override string ToString() => $"ScheduleItem{{key: {Key}, fix: {Fix}, name: {Name}}}";
}

/// <summary>备注（对齐原版 Note：objects 列表 + type + append）。</summary>
public sealed class Note
{
    public List<string> Objects { get; set; } = [];
    public string Type { get; set; } = string.Empty;
    public string Append { get; set; } = string.Empty;

    public Note()
    {
    }

    public Note(List<string> objects, string type, string append)
    {
        Objects = objects;
        Type = type;
        Append = append;
    }

    public override string ToString() =>
        $"Note{{objects: {string.Join(", ", Objects)}, type: {Type}, append: {Append}}}";
}

/// <summary>重名计划异常（对齐原版 DuplicateItemException）。</summary>
public sealed class DuplicateItemException(string message) : Exception(message);

/// <summary>
/// 计划数据列表（对齐原版 DataList）：增/插/改/索引赋值均做按 name 查重
/// （C-13 修正：原版 []= 直赋不校验，契约统一为 set 也走校验；查重使用 name 一致而非引用比较）。
/// </summary>
public class DataList<T> : IEnumerable<T> where T : ScheduleItem
{
    private readonly List<T> _data = [];

    public IReadOnlyList<T> Items => _data;

    public int Length => _data.Count;

    public int Count => _data.Count;

    public T this[int index]
    {
        get => _data[index];
        set
        {
            ThrowIfDuplicate(Enumerable.Repeat(value, 1), _data, excludeIndex: index);
            _data[index] = value;
        }
    }

    public DataList()
    {
    }

    public DataList(IEnumerable<T>? shots)
    {
        if (shots == null) return;
        var list = shots.ToList();
        ThrowIfDuplicate(list);
        _data = list;
    }

    private static void ThrowIfDuplicate(IEnumerable<T> items, List<T>? existing = null, int excludeIndex = -1)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (existing != null)
        {
            for (var i = 0; i < existing.Count; i++)
            {
                if (i == excludeIndex) continue;
                if (!seen.Add(existing[i].Name))
                {
                    throw new DuplicateItemException("Duplicate items in the list");
                }
            }
        }

        foreach (var item in items)
        {
            if (!seen.Add(item.Name))
            {
                throw new DuplicateItemException("Duplicate items in the list");
            }
        }
    }

    public void Add(T item)
    {
        ThrowIfDuplicate(Enumerable.Repeat(item, 1), _data);
        _data.Add(item);
    }

    public bool Remove(T item) => _data.Remove(item);

    public T RemoveAt(int index)
    {
        var removed = _data[index];
        _data.RemoveAt(index);
        return removed;
    }

    public void Insert(int index, T item)
    {
        ThrowIfDuplicate(Enumerable.Repeat(item, 1), _data);
        _data.Insert(index, item);
    }

    public void Update(int oldIndex, T newItem)
    {
        ThrowIfDuplicate([newItem], _data, excludeIndex: oldIndex);
        _data[oldIndex] = newItem;
    }

    public IEnumerator<T> GetEnumerator() => _data.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>场次计划（对齐原版 SceneSchedule：info + 索引器）。</summary>
public sealed class SceneSchedule : DataList<ScheduleItem>
{
    public ScheduleItem Info { get; set; } = new();

    public SceneSchedule()
    {
    }

    public SceneSchedule(List<ScheduleItem> shots, ScheduleItem info)
        : base(shots)
    {
        Info = info;
    }
}