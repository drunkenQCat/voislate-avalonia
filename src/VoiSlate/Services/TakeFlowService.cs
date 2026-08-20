using VoiSlate.Models;

namespace VoiSlate.Services;

/// <summary>
/// 记录流服务实现（B1-B5/B7/B11 全部时序在此；逐行对齐原版 record_page.addItem/drawBackItem/shotEndBtn）。
/// P0.5 预产，演进权归 E（契约 C-3）。
/// </summary>
public sealed class TakeFlowService : ITakeFlowService, IDisposable
{
    private readonly ILogRepository _logs;
    private readonly IPickerHistoryStore _history;
    private readonly ISessionState _session;
    private readonly IFileNamingService _fileNum;
    private readonly ISessionSettingsStore _settings;
    private readonly ITimeProvider _time;
    private readonly IHapticsService _haptics;
    private readonly IToastService _toast;

    private TkStatus _pendingTk = TkStatus.NotChecked;
    private ShtStatus _pendingSht = ShtStatus.NotChecked;

    /// <summary>当前场/镜标签提供器（P0.5 由组合根接 ScheduleBook；B 阶段由 ScheduleService 提供）。</summary>
    public Func<string?>? SceneLabelProvider { get; set; }
    public Func<string?>? ShotLabelProvider { get; set; }

    /// <summary>当前场/镜的 objects 列表（写入 picker_history 尾；缺省为空）。</summary>
    public Func<IReadOnlyList<string>>? CurrentObjectsProvider { get; set; }

    public TakeFlowService(
        ILogRepository logs,
        IPickerHistoryStore history,
        ISessionState session,
        IFileNamingService fileNum,
        ISessionSettingsStore settings,
        ITimeProvider time,
        IHapticsService haptics,
        IToastService toast)
    {
        _logs = logs;
        _history = history;
        _session = session;
        _fileNum = fileNum;
        _settings = settings;
        _time = time;
        _haptics = haptics;
        _toast = toast;
        _fileNum.NumberChanged += OnFileNumberChanged;
    }

    public event Action? LogsChanged;
    public event Action<int>? FileNumberChanged;
    public event Action? HistoryChanged;

    public string Today => VoiSlateDates.TodayKey(_time.Now);

    public async Task InitializeAsync(CancellationToken ct)
    {
        var today = Today;
        var savedDate = await _settings.GetStringAsync(SessionKeys.Date, string.Empty);
        var recordCount = savedDate == today
            ? await _settings.GetIntAsync(SessionKeys.RecordCount, 1)
            : 1;

        _fileNum.Linker = await _settings.GetStringAsync(SessionKeys.RecordLinker, "-T");
        _fileNum.PrefixMode = PrefixTypeExtensions.ParseSettings(
            await _settings.GetStringAsync(SessionKeys.PrefixType, "default"));
        _fileNum.CustomPrefix = await _settings.GetStringAsync(SessionKeys.CustomPrefix, "custom");
        _fileNum.SetValue(recordCount);

        _pendingTk = (TkStatus)(await _settings.GetIntAsync(SessionKeys.OkTk, 0));
        _pendingSht = (ShtStatus)(await _settings.GetIntAsync(SessionKeys.OkSht, 0));
        ct.ThrowIfCancellationRequested();
    }

    public async Task AddItemAsync(TakeType type, CancellationToken ct,
        string? tkNoteOverride = null, string? shtNoteOverride = null)
    {
        var today = Today;

        // ---- 取上一拍（B2：空栈 → ['0','0','0']）----
        var hist = await _history.GetLastAsync();
        var scn = hist.Count > 0 ? hist[0] : "0";
        var sht = hist.Count > 0 ? hist[1] : "0";
        var sign = hist.Count > 2 ? hist[2] : "0";
        var prevObjs = hist.Count > 3 ? hist.Skip(3).ToList() : [];

        // ---- 镜头结束按钮前置守卫（原版 shotEndBtn；放入服务保证 B11 一致性）----
        if (type == TakeType.End &&
            (_fileNum.PrevFileName().Length == 0 || hist.Count == 0 || sign == "OK" || sign == "F"))
        {
            return;
        }

        // ---- 写场记（守卫：number==1 时 prevFileName 为空 → 不写日志，原版语义）----
        if (_fileNum.PrevFileName().Length > 0)
        {
            var isFake = sign == "F";
            var isWild = sign == "W";
            var trackLogs = string.Concat(prevObjs.Select(o => $"<{o}/>"));

            // 联动：shot 变更或 end → 自动优良（B3）
            var currentShot = ShotLabelProvider?.Invoke();
            if ((currentShot != null && currentShot != sht) || type == TakeType.End)
            {
                _pendingTk = TkStatus.Ok;
                _pendingSht = ShtStatus.Nice;
            }

            var desc = tkNoteOverride ?? string.Empty;
            var shotNote = shtNoteOverride ?? string.Empty;
            var tk = isFake ? 999 : isWild ? 0 : int.Parse(sign);

            var item = new SlateLogItem
            {
                Scn = scn,
                Sht = sht,
                Tk = tk,
                FilenamePrefix = _fileNum.Prefix,
                FilenameLinker = _fileNum.Linker,
                FilenameNum = _fileNum.PrevFileNum(),
                TkNote = !isFake
                    ? (desc.Length == 0 ? $"S{scn} Sh{sht} Tk{sign}" : desc)
                    : "Fake Take",
                ShtNote = $"{shotNote}{trackLogs}",
                ScnNote = string.Empty, // 场备注由 ScheduleService 提供；P0.5 占位
                OkTk = !isFake ? _pendingTk : TkStatus.Bad,
                OkSht = !isFake ? _pendingSht : ShtStatus.NotChecked,
            };
            if (isWild)
            {
                item.TkNote = $"wild track {item.TkNote}";
            }

            await _logs.AddAsync(today, _fileNum.PrevFileName(), item);
            LogsChanged?.Invoke();
        }

        // ---- 未联动：除 end 外当前次转为 wild（原版转换发生在日志构造之后，仅影响历史关键字）----
        if (!_session.IsLinked && type != TakeType.End)
        {
            type = TakeType.Wild;
        }

        // ---- 写 picker_history 尾 ----
        var keyword = type switch
        {
            TakeType.End => "OK",
            TakeType.Fake => "F",
            TakeType.Wild => "W",
            _ => (_session.TakeIndex + 1).ToString(), // normal：take 列标签（1..200）
        };
        var entry = new List<string>
        {
            SceneLabelProvider?.Invoke() ?? scn,
            ShotLabelProvider?.Invoke() ?? sht,
            keyword,
        };
        entry.AddRange(CurrentObjectsProvider?.Invoke() ?? []);
        await _history.AddAsync(entry);
        HistoryChanged?.Invoke();

        // ---- 收尾（B7）----
        if (type != TakeType.End)
        {
            _fileNum.Increment();
        }

        await ResetOkEnumsAsync(ct);
        await _settings.SetAsync(SessionKeys.RecordCount, _fileNum.Number);
        await _settings.SetAsync(SessionKeys.Date, today);

        _haptics.Vibrate(type == TakeType.Fake ? 240 : 128, type == TakeType.Fake ? 900 : 150);
        ct.ThrowIfCancellationRequested();
    }

    public async Task<RewindResult> RewindAsync(CancellationToken ct)
    {
        var today = Today;

        var restoreNotes = async () =>
        {
            var logs = await _logs.GetByDateAsync(today);
            if (logs.Count == 0)
            {
                return new RewindResult(string.Empty, string.Empty, false);
            }

            var last = logs[^1];
            var shotNote = last.ShtNote.Split('<').First();
            return new RewindResult(last.TkNote, shotNote, false);
        };

        var hist = await _history.GetLastAsync();

        // OK 尾：只弹哨兵 + 恢复备注 + 提示（不递减、不删日志，原版 B11）
        if (hist.Count > 2 && hist[2] == "OK")
        {
            await _history.RemoveLastAsync();
            HistoryChanged?.Invoke();
            var r = await restoreNotes();
            _toast.Show("原来还没收工呢……");
            return r with { WasOkMarkerOnly = true };
        }

        // 常规撤回：递减 + 弹尾 + 删末条 + 恢复备注
        _fileNum.Decrement();
        try
        {
            await _history.RemoveLastAsync();
        }
        catch
        {
            // 原版空 catch；保持行为
        }

        var result = await restoreNotes();
        var logsAfter = await _logs.GetByDateAsync(today);
        if (logsAfter.Count > 0)
        {
            await _logs.RemoveLastAsync(today);
        }

        await _settings.SetAsync(SessionKeys.RecordCount, _fileNum.Number);
        LogsChanged?.Invoke();
        HistoryChanged?.Invoke();
        _haptics.Vibrate(240, 900);
        ct.ThrowIfCancellationRequested();
        return result;
    }

    public async Task SaveEditAsync(SlateLogItem item, int index, CancellationToken ct)
    {
        await _logs.ReplaceAtAsync(Today, index, item);
        LogsChanged?.Invoke();
        ct.ThrowIfCancellationRequested();
    }

    public async Task DeleteItemAsync(int index, CancellationToken ct)
    {
        await _logs.RemoveAtAsync(Today, index);
        LogsChanged?.Invoke();
        ct.ThrowIfCancellationRequested();
    }

    public Task SetFileNumberAsync(int value, CancellationToken ct)
    {
        _fileNum.SetValue(value);
        return _settings.SetAsync(SessionKeys.RecordCount, value);
    }

    public Task SetLinkerAsync(string linker, CancellationToken ct)
    {
        _fileNum.Linker = linker;
        return _settings.SetAsync(SessionKeys.RecordLinker, linker);
    }

    public Task SetPrefixAsync(PrefixType mode, string? customPrefix, CancellationToken ct)
    {
        _fileNum.PrefixMode = mode;
        _fileNum.CustomPrefix = customPrefix ?? "custom";
        return SetPrefixAsyncCore(mode, customPrefix);
    }

    private async Task SetPrefixAsyncCore(PrefixType mode, string? customPrefix)
    {
        await _settings.SetAsync(SessionKeys.PrefixType, mode.ToSettingsValue());
        await _settings.SetAsync(SessionKeys.CustomPrefix, customPrefix ?? "custom");
    }

    private async Task ResetOkEnumsAsync(CancellationToken ct)
    {
        _pendingTk = TkStatus.NotChecked;
        _pendingSht = ShtStatus.NotChecked;
        await _settings.SetAsync(SessionKeys.OkTk, 0);
        await _settings.SetAsync(SessionKeys.OkSht, 0);
        ct.ThrowIfCancellationRequested();
    }

    private void OnFileNumberChanged(int number) => FileNumberChanged?.Invoke(number);

    public void Dispose() => _fileNum.NumberChanged -= OnFileNumberChanged;
}