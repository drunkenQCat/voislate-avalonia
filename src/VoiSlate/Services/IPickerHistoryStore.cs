using LiteDB;

using VoiSlate.Infrastructure;
namespace VoiSlate.Services;

/// <summary>
/// 上一拍信息栈（对齐原版 Hive box 'picker_history'：每行 [scn, sht, keyword, objs...]）。
/// </summary>
public interface IPickerHistoryStore
{
    Task<int> CountAsync();
    Task<IReadOnlyList<string>> GetLastAsync();
    Task AddAsync(IReadOnlyList<string> entry);
    Task RemoveLastAsync();
    Task ClearAsync();
}

public sealed class LiteDbPickerHistoryStore(LiteDbStore store) : IPickerHistoryStore
{
    private ILiteCollection<BsonDocument> Collection => store.Database.GetCollection("picker_history");

    public Task<int> CountAsync() => Task.FromResult(Collection.Count());

    public Task<IReadOnlyList<string>> GetLastAsync()
    {
        var list = Entries();
        return Task.FromResult<IReadOnlyList<string>>(list.Count == 0 ? Array.Empty<string>() : list[^1].Values);
    }

    public Task AddAsync(IReadOnlyList<string> entry)
    {
        var arr = new BsonArray();
        foreach (var e in entry)
        {
            arr.Add(e);
        }

        Collection.Insert(new BsonDocument { ["e"] = arr });
        return Task.CompletedTask;
    }

    public Task RemoveLastAsync()
    {
        var list = Entries();
        if (list.Count > 0)
        {
            Collection.Delete(list[^1].Id);
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        Collection.DeleteAll();
        return Task.CompletedTask;
    }

    private List<(ObjectId Id, List<string> Values)> Entries()
    {
        var result = new List<(ObjectId, List<string>)>();
        foreach (var doc in Collection.FindAll().OrderBy(d => d["_id"].AsObjectId))
        {
            var values = new List<string>();
            foreach (var v in doc["e"].AsArray)
            {
                values.Add(v.AsString);
            }

            result.Add((doc["_id"].AsObjectId, values));
        }

        return result;
    }
}