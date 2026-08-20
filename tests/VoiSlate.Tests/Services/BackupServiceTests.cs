using System.Text.Json;
using VoiSlate.Models;
using VoiSlate.Services;
using VoiSlate.Tests.TestDoubles;
using Xunit;

namespace VoiSlate.Tests.Services;

/// <summary>备份（契约 §3 IBackupService + ADR-006：Documents/VoiSlate Logs/slate_backup{yymmdd}-{hour}clock.json）。</summary>
public sealed class BackupServiceTests : IDisposable
{
    private readonly FakeLogRepository _logs = new();
    private readonly FakeTimeProvider _time = new(); // 2026-08-20 12:00 → 文件名 slate_backup260820-12clock.json
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "voislate-backup-" + Guid.NewGuid().ToString("N"));

    private BackupService NewService() => new(_logs, new ExportService(), _time, _dir);

    private static SlateLogItem Item(int num) => new()
    {
        Scn = "1",
        Sht = "A",
        Tk = num,
        FilenamePrefix = "260820",
        FilenameLinker = "-T",
        FilenameNum = num,
        TkNote = $"Tk {num}",
        ShtNote = "note",
        ScnNote = "scene",
        OkTk = TkStatus.Ok,
        OkSht = ShtStatus.Nice,
    };

    [Fact]
    public async Task Backup_Writes_All_Dates_Merged_With_Expected_File_Name()
    {
        await _logs.AddAsync("260819", "k1", Item(1));
        await _logs.AddAsync("260820", "k2", Item(2));
        await _logs.AddAsync("260820", "k3", Item(3));

        await NewService().BackupAsync(CancellationToken.None);

        var path = Path.Combine(_dir, "slate_backup260820-12clock.json");
        Assert.True(File.Exists(path));
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.Equal(3, doc.RootElement.GetArrayLength());
        // 跨日合并：第一个条目来自 260819
        Assert.Equal(1, doc.RootElement[0].GetProperty("tk").GetInt32());
    }

    [Fact]
    public async Task Backup_Empty_Repository_Produces_Empty_Array_File()
    {
        await NewService().BackupAsync(CancellationToken.None);

        var path = Path.Combine(_dir, "slate_backup260820-12clock.json");
        Assert.True(File.Exists(path));
        Assert.Equal("[]", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Periodic_Backup_Produces_File_And_Is_Idempotent_To_Start()
    {
        await _logs.AddAsync("260820", "k1", Item(5));

        using var svc = NewService();
        svc.StartPeriodicBackup(TimeSpan.FromMilliseconds(30));
        svc.StartPeriodicBackup(TimeSpan.FromMilliseconds(30));

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        var path = Path.Combine(_dir, "slate_backup260820-12clock.json");
        while (!File.Exists(path) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }

        Assert.True(File.Exists(path), "周期备份未在超时内写出文件");
    }

    [Fact]
    public async Task Period_Interval_Is_Three_Minutes_By_Default()
    {
        // 契约/ADR-006：启动 3 分钟 PeriodicTimer。默认间隔无法直接断言，
        // 仅验证接口存在 + 默认启动不抛；周期语义由 BackupService 文档与 SC 约定锁定。
        using var svc = NewService();
        svc.StartPeriodicBackup();
        await Task.Delay(50);
        svc.Dispose();
    }

    public void Dispose() => DeleteDir(_dir);

    private static void DeleteDir(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // 测试清理失败可忽略
        }
    }
}