using LiteDB;

using VoiSlate.Infrastructure;
namespace VoiSlate.Services;

/// <summary>
/// 会话设置存储（对齐原版 Hive box 'scn_sht_tk' 13 键 + 文件号；键名见 <see cref="SessionKeys"/>）。
/// 抽象接口，实现用 LiteDB "settings" 集合（BsonDocument: _id=key, v=value）。
/// </summary>
public interface ISessionSettingsStore
{
    Task<string?> GetStringAsync(string key);
    Task<int?> GetIntAsync(string key);
    Task<bool?> GetBoolAsync(string key);
    Task<string> GetStringAsync(string key, string defaultValue);
    Task<int> GetIntAsync(string key, int defaultValue);
    Task<bool> GetBoolAsync(string key, bool defaultValue);
    Task SetAsync(string key, object? value);
}

/// <summary>原版持久化键名（Hive box 'scn_sht_tk'）。</summary>
public static class SessionKeys
{
    public const string SceneIndex = "scnIndex";
    public const string ShotIndex = "shtIndex";
    public const string TakeIndex = "tkIndex";
    public const string IsLinked = "isLinked";
    public const string Date = "date";
    public const string RecordCount = "recordCount";
    public const string RecordLinker = "recordLinker";
    public const string PrefixType = "prefixType";
    public const string CustomPrefix = "customPrefix";
    public const string Desc = "desc";
    public const string Note = "note";
    public const string OkTk = "oktk";
    public const string OkSht = "oksht";
}

public sealed class LiteDbSessionSettingsStore(LiteDbStore store) : ISessionSettingsStore
{
    private ILiteCollection<BsonDocument> Collection => store.Database.GetCollection("settings");

    public Task<string?> GetStringAsync(string key) => Task.FromResult(ReadValue(key)?.AsString);

    public Task<int?> GetIntAsync(string key)
    {
        var v = ReadValue(key);
        return Task.FromResult(v?.IsInt32 == true ? v.AsInt32 : (int?)null);
    }

    public Task<bool?> GetBoolAsync(string key)
    {
        var v = ReadValue(key);
        return Task.FromResult(v?.IsBoolean == true ? v.AsBoolean : (bool?)null);
    }

    public Task<string> GetStringAsync(string key, string defaultValue) =>
        Task.FromResult(ReadValue(key)?.AsString ?? defaultValue);

    public Task<int> GetIntAsync(string key, int defaultValue) =>
        Task.FromResult(ReadValue(key)?.IsInt32 == true ? ReadValue(key)!.AsInt32 : defaultValue);

    public Task<bool> GetBoolAsync(string key, bool defaultValue) =>
        Task.FromResult(ReadValue(key)?.IsBoolean == true ? ReadValue(key)!.AsBoolean : defaultValue);

    public Task SetAsync(string key, object? value)
    {
        var doc = new BsonDocument
        {
            ["_id"] = key,
            ["v"] = value switch
            {
                null => BsonValue.Null,
                int i => i,
                bool b => b,
                _ => (string)Convert.ToString(value)!,
            },
        };
        Collection.Upsert(doc);
        return Task.CompletedTask;
    }

    private BsonValue? ReadValue(string key)
    {
        var doc = Collection.FindById(key);
        return doc?["v"];
    }
}