using Serilog;
using VoiSlate.Models;

namespace VoiSlate.Services;

/// <summary>
/// 场记备份（契约 §3 IBackupService + ADR-006；对齐原版 record_page.dart backupSlateLogs）。
/// - JSON 输出到 Documents/VoiSlate Logs/slate_backup{yymmdd}-{hour}clock.json
///   （Android 原版为外部存储 Documents/VoiSlate Logs；桌面映射为 MyDocuments/VoiSlate Logs，可注入基目录便于测试）。
/// - 内容：全量跨日场记合并（迁移计划 §2.4"备份 3 分钟全量 JSON"；原版 serializeSlate 仅今日——
///   见报告说明，如需严格复刻可演进为仅今日）。
/// - 触发：启动 3 分钟 PeriodicTimer + 退出前（App 退出序调用 BackupAsync）+ 手动。
/// - 错误：IO/存储异常 Log.Error 后向上抛（ADR-009 由调用方兜底：周期循环捕获、退出钩子捕获、手动路径由 VM 转 Toast）。
/// </summary>
public interface IBackupService : IDisposable
{
    /// <summary>立即备份全量场记；失败抛异常（已记录日志）。</summary>
    Task BackupAsync(CancellationToken ct);

    /// <summary>启动每 3 分钟周期备份（可注入更短间隔便于测试）。幂等（已启动则忽略）。</summary>
    void StartPeriodicBackup(TimeSpan? interval = null);
}

public sealed class BackupService : IBackupService
{
    public const string BackupFolderName = "VoiSlate Logs";

    private readonly ILogRepository _logs;
    private readonly IExportService _export;
    private readonly ITimeProvider _time;
    private readonly string _baseDirectory;

    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;
    private readonly object _gate = new();

    public BackupService(ILogRepository logs, IExportService export, ITimeProvider time, string? baseDirectory = null)
    {
        _logs = logs;
        _export = export;
        _time = time;
        _baseDirectory = baseDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), BackupFolderName);
    }

    public async Task BackupAsync(CancellationToken ct)
    {
        var dates = await _logs.GetDatesAsync();
        var all = new List<SlateLogItem>();
        foreach (var date in dates)
        {
            ct.ThrowIfCancellationRequested();
            all.AddRange(await _logs.GetByDateAsync(date));
        }

        var now = _time.Now;
        var name = $"slate_backup{VoiSlateDates.TodayKey(now)}-{now.Hour}clock.json";
        var content = _export.SerializeLogs(all);
        await _export.SaveToFileAsync(_baseDirectory, name, content);
    }

    public void StartPeriodicBackup(TimeSpan? interval = null)
    {
        lock (_gate)
        {
            if (_cts != null)
            {
                return; // 已启动
            }

            _cts = new CancellationTokenSource();
            _timer = new PeriodicTimer(interval ?? TimeSpan.FromMinutes(3));
            _ = Task.Run(() => RunPeriodicAsync(_timer, _cts.Token));
        }
    }

    private async Task RunPeriodicAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    await BackupAsync(ct);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Periodic backup failed");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停止
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _cts?.Cancel();
            _timer?.Dispose();
            _cts?.Dispose();
            _cts = null;
            _timer = null;
        }
    }
}