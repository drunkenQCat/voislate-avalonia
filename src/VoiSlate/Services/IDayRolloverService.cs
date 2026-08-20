using Serilog;

namespace VoiSlate.Services;

/// <summary>
/// 跨天补偿（契约 §3 IDayRolloverService + ADR-008 启动序第 3 步；对齐原版 main.dart 日期登记/清空逻辑
/// + DateChecker 定时检测）。
/// - IsNewDay：持久化"上一活动日"（ISessionSettingsStore.date，TakeFlowService 记条时同步）≠ 今天。
/// - OnStartup：新日志日 → recordCount 归 1 + picker_history 清空 + 日期登记（settings.date = today）（B9）；
///   同日启动不动作。
/// - 定时检测（PeriodicTimer 1min 起，契约"新能力"F4）：每次 tick 执行同一补偿。
/// - 事件 DayChanged：供 ITakeFlowService（演进权归 E）订阅以同步内存文件号（见报告缺口；当前不改既有文件）。
/// 说明：契约签名要求 OnStartup 为 void；内部存储操作同步完成（LiteDB 操作本身同步，组合根调用，
/// 与 App.axaml.cs 既有 .GetAwaiter().GetResult() 模式一致）。
/// </summary>
public interface IDayRolloverService : IDisposable
{
    /// <summary>持久化上一活动日 ≠ 今天（首次调用时以 settings.date 初始化基准日）。</summary>
    bool IsNewDay();

    /// <summary>启动补偿：跨天则 recordCount=1 + 清空 picker_history + 登记今天（B9）。同步完成。</summary>
    void OnStartup();

    /// <summary>启动定时检测（默认 1 分钟；可注入更短间隔便于测试）。幂等（已启动则忽略）。</summary>
    void StartPeriodicCheck(TimeSpan? interval = null);

    /// <summary>完成一次跨天补偿后触发（供 ITakeFlowService 同步内存文件号，后续演进）。</summary>
    event Action? DayChanged;
}

public sealed class DayRolloverService : IDayRolloverService
{
    private readonly IPickerHistoryStore _history;
    private readonly ISessionSettingsStore _settings;
    private readonly ITimeProvider _time;
    private readonly object _gate = new();

    private string _currentDay = string.Empty;
    private bool _initialized;
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;

    public DayRolloverService(
        IPickerHistoryStore history,
        ISessionSettingsStore settings,
        ITimeProvider time)
    {
        _history = history;
        _settings = settings;
        _time = time;
    }

    public event Action? DayChanged;

    public string Today => VoiSlateDates.TodayKey(_time.Now);

    public bool IsNewDay()
    {
        EnsureInitialized();
        return Today != _currentDay;
    }

    public void OnStartup()
    {
        EnsureInitialized();
        TryRollover();
    }

    public void StartPeriodicCheck(TimeSpan? interval = null)
    {
        lock (_gate)
        {
            if (_cts != null)
            {
                return; // 已启动
            }

            _cts = new CancellationTokenSource();
            _timer = new PeriodicTimer(interval ?? TimeSpan.FromMinutes(1));
            _ = Task.Run(() => RunPeriodicAsync(_timer, _cts.Token));
        }
    }

    private void EnsureInitialized()
    {
        lock (_gate)
        {
            if (_initialized)
            {
                return;
            }

            _currentDay = _settings.GetStringAsync(SessionKeys.Date, string.Empty)
                .GetAwaiter().GetResult() ?? string.Empty;
            _initialized = true;
        }
    }

    private void TryRollover()
    {
        lock (_gate)
        {
            EnsureInitialized(); // Monitor 可重入：同线程内嵌套 lock 安全
            var today = Today;
            if (today == _currentDay)
            {
                return;
            }

            // B9：recordCount 归 1 + picker_history 清空 + 日期登记（对齐 main.dart:49-53）。
            _settings.SetAsync(SessionKeys.RecordCount, 1).GetAwaiter().GetResult();
            _history.ClearAsync().GetAwaiter().GetResult();
            _settings.SetAsync(SessionKeys.Date, today).GetAwaiter().GetResult();
            _currentDay = today;
            DayChanged?.Invoke();
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
                    TryRollover();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "DayRollover periodic check failed");
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