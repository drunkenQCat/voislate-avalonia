using LiteDB;

namespace VoiSlate.Infrastructure;

/// <summary>
/// LiteDB 存储句柄（单例）。P0.5 预产，演进权归 E（契约 C-3）。
/// 测试可用 ":memory:"" 内存库；生产用 ApplicationData 目录 voislate.db。
/// </summary>
public sealed class LiteDbStore : IDisposable
{
    private readonly LiteDatabase _db;
    private bool _disposed;

    public bool IsMemory { get; }

    public string FilePath { get; }

    public LiteDatabase Database => _db;

    public LiteDbStore(string connectionString)
    {
        FilePath = connectionString;
        IsMemory = connectionString == ":memory:";
        // LiteDB 对相同连接串（含 ":memory:"）会返回进程内共享实例——多个并行测试会互踩数据。
        // 内存库改用独立 MemoryStream，保证每个 :memory: store 完全隔离（生产走文件库，行为不变）。
        // BsonMapper.EmptyStringToNull=false：空字符串是合法标注值，不得折叠成 null（P8 上游语音标注依赖）。
        var mapper = new BsonMapper { EmptyStringToNull = false };
        _db = IsMemory
            ? new LiteDatabase(new MemoryStream(), mapper)
            : new LiteDatabase(connectionString, mapper);
    }

    /// <summary>应用数据目录下的默认库（macOS: ~/Library/Application Support/VoiSlate/voislate.db）。</summary>
    public static string DefaultConnectionString()
    {
        var dir = Environment.GetEnvironmentVariable("VOISLATE_DATA_DIR");
        if (string.IsNullOrWhiteSpace(dir)) dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VoiSlate");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "voislate.db");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _db.Dispose();
    }
}