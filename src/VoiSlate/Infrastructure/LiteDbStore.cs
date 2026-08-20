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
        _db = new LiteDatabase(connectionString);
    }

    /// <summary>应用数据目录下的默认库（macOS: ~/Library/Application Support/VoiSlate/voislate.db）。</summary>
    public static string DefaultConnectionString()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoiSlate");
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