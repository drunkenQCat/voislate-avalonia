using VoiSlate.Models;

namespace VoiSlate.Services;

/// <summary>
/// 文件号服务（对齐原版 RecordFileNum + C-2：唯一实例由 ITakeFlowService 持用，DI 单例）。
/// 行为：number 从 1 起；decrement 不低于 1（为 1 时不发事件）；prefix 按模式计算；
/// prevFileName 在 number==1 时返回空串（原版守卫依赖此语义）。
/// </summary>
public interface IFileNamingService
{
    int Number { get; }
    string Prefix { get; }
    string Linker { get; set; }
    PrefixType PrefixMode { get; set; }
    string CustomPrefix { get; set; }

    /// <summary>number 变更事件（C-2：ITakeFlowService 转发为 FileNumberChanged）。</summary>
    event Action<int>? NumberChanged;

    void SetValue(int newValue);
    int Increment();
    int Decrement();

    /// <summary>prefix + linker + number 补零 3 位。</summary>
    string FullName();

    /// <summary>number==1 → ""；否则 prefix+linker+(number-1) 补零 3 位（同时作为场记 key）。</summary>
    string PrevFileName();

    int PrevFileNum();
}

public sealed class FileNumberingService : IFileNamingService
{
    private readonly ITimeProvider _time;
    private int _number = 1;

    public FileNumberingService(ITimeProvider time)
    {
        _time = time;
    }

    public int Number => _number;

    public string Prefix
    {
        get
        {
            var now = _time.Now;
            return PrefixMode switch
            {
                PrefixType.Custom => CustomPrefix,
                PrefixType.SoundDevices => VoiSlateDates.SoundDevicesKey(now),
                _ => VoiSlateDates.TodayKey(now),
            };
        }
    }

    public string Linker { get; set; } = "-T";

    public PrefixType PrefixMode { get; set; } = PrefixType.Default;

    public string CustomPrefix { get; set; } = "custom";

    public event Action<int>? NumberChanged;

    public void SetValue(int newValue)
    {
        _number = newValue;
        NumberChanged?.Invoke(_number);
    }

    public int Increment()
    {
        _number++;
        NumberChanged?.Invoke(_number);
        return _number;
    }

    public int Decrement()
    {
        if (_number - 1 < 1) return _number; // 原版：已经是 1 不再递减（且不发事件）
        _number--;
        NumberChanged?.Invoke(_number);
        return _number;
    }

    public string FullName() => $"{Prefix}{Linker}{_number:D3}";

    public string PrevFileName() =>
        _number == 1 ? string.Empty : $"{Prefix}{Linker}{_number - 1:D3}";

    public int PrevFileNum() => _number - 1;
}