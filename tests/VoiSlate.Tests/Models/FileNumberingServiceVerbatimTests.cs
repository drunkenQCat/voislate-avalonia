using System.Text.Json;
using VoiSlate.Models;
using VoiSlate.Services;
using VoiSlate.Tests.TestDoubles;
using Xunit;

namespace VoiSlate.Tests.Models;

/// <summary>
/// FileNumberingService 逐行核对测试（A 演进；锁定原版 models/recorder_file_num.dart 行为：
/// today=yyMMdd、soundDevicesToday=yyYMMdd、decrement 下限 1 不发事件、补零 3 位、
/// prevFileName 在 1 时为空串、prefix 实时计算 + 契约 §2 RecorderType 表面）。
/// </summary>
public class FileNumberingServiceVerbatimTests
{
    private static FileNumberingService NewService(DateTime? now = null) =>
        new(new FakeTimeProvider(now ?? FakeTimeProvider.Fixed));

    [Fact]
    public void Prefix_Default_Is_Today_yyMMdd()
    {
        // 2026-08-20 → "260820"（对齐 RecordFileNum.today：year.substring(2)+MM+dd）。
        Assert.Equal("260820", new FileNumberingService(new FakeTimeProvider(new DateTime(2026, 8, 20, 12, 0, 0))).Prefix);
    }

    [Fact]
    public void Prefix_SoundDevices_Is_yyYMMdd()
    {
        // 对齐 RecordFileNum.soundDevicesToday："26Y08M20"。
        var svc = NewService();
        svc.RecorderType = RecorderType.SoundDevices;
        Assert.Equal("26Y08M20", svc.Prefix);
    }

    [Fact]
    public void Prefix_Custom_Returns_CustomPrefix()
    {
        var svc = NewService();
        svc.RecorderType = RecorderType.Custom;
        svc.CustomPrefix = "projectX";
        Assert.Equal("projectX", svc.Prefix);
    }

    [Fact]
    public void Prefix_Precedence_Matches_Original_Custom_First()
    {
        // 原版 getter 顺序：custom → sound devices → default；判定模式而非顺序，三态各自独立成立。
        var svc = NewService();
        Assert.Equal("260820", svc.Prefix);                    // default
        svc.RecorderType = RecorderType.SoundDevices;
        Assert.Equal("26Y08M20", svc.Prefix);                  // sound devices
        svc.RecorderType = RecorderType.Custom;
        svc.CustomPrefix = "custom";
        Assert.Equal("custom", svc.Prefix);                    // custom
    }

    [Fact]
    public void RecorderType_And_PrefixMode_Are_Single_State_Mapping()
    {
        var svc = NewService();
        svc.RecorderType = RecorderType.SoundDevices;
        Assert.Equal(PrefixType.SoundDevices, svc.PrefixMode); // 写 RecorderType 反映到 PrefixMode
        Assert.Equal("26Y08M20", svc.Prefix);

        svc.PrefixMode = PrefixType.Custom;
        Assert.Equal(RecorderType.Custom, svc.RecorderType);   // 写 PrefixMode 反映回 RecorderType
        Assert.Equal("custom", svc.Prefix);
    }

    [Fact]
    public void Linker_Default_Is_Dash_T_And_Is_Used_In_FullName()
    {
        var svc = NewService();
        Assert.Equal("-T", svc.Linker);
        svc.Increment(); // number 2
        Assert.Equal("260820-T002", svc.FullName());
        svc.Linker = "-X";
        Assert.Equal("260820-X002", svc.FullName());
    }

    [Fact]
    public void SetValue_And_Increment_Raise_NumberChanged()
    {
        var svc = NewService();
        var seen = new List<int>();
        svc.NumberChanged += seen.Add;

        svc.SetValue(7);
        Assert.Equal(7, svc.Number);
        Assert.Equal([7], seen);

        svc.Increment();
        Assert.Equal(8, svc.Number);
        Assert.Equal([7, 8], seen);
    }

    [Fact]
    public void Decrement_At_One_Keeps_One_And_Does_Not_Raise()
    {
        var svc = NewService();
        var raised = 0;
        svc.NumberChanged += _ => raised++;

        Assert.Equal(1, svc.Decrement());
        Assert.Equal(1, svc.Number);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void Decrement_Guard_Is_Number_Minus_One_Verbatim()
    {
        // 原版守卫为 `_number - 1 < 1`（非 `_number < 1`）：即使被 setValue(0) 置零也不递减。
        var svc = NewService();
        svc.SetValue(0);
        var raised = 0;
        svc.NumberChanged += _ => raised++;

        Assert.Equal(0, svc.Decrement());
        Assert.Equal(0, svc.Number);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void PrevFileName_Empty_At_One_Then_Prefix_Linker_PrevPadded()
    {
        var svc = NewService();
        Assert.Equal(string.Empty, svc.PrevFileName());

        svc.Increment(); // number 2
        Assert.Equal("260820-T001", svc.PrevFileName());
        Assert.Equal(1, svc.PrevFileNum());
    }

    [Fact]
    public void PrevFileName_Recomputes_Prefix_At_Call_Time()
    {
        // 原版 prevFileName 每次调用经 prefix getter 实时取 today/soundDevicesToday：改模式即变。
        var svc = NewService();
        svc.Increment(); // number 2
        Assert.Equal("260820-T001", svc.PrevFileName());

        svc.RecorderType = RecorderType.SoundDevices;
        Assert.Equal("26Y08M20-T001", svc.PrevFileName());
    }

    [Fact]
    public void FullName_Pads_Number_To_Three_Digits()
    {
        var svc = NewService();
        Assert.Equal("260820-T001", svc.FullName()); // number 1

        svc.SetValue(12);
        Assert.Equal("260820-T012", svc.FullName());

        svc.SetValue(123);
        Assert.Equal("260820-T123", svc.FullName());
    }

    [Fact]
    public void FullName_Overflow_Preserves_Digits_Like_PadLeft()
    {
        // 原版 padLeft(3,'0') 对超 3 位数字不截断；D3 语义一致。
        var svc = NewService();
        svc.SetValue(1234);
        Assert.Equal("260820-T1234", svc.FullName());
    }
}