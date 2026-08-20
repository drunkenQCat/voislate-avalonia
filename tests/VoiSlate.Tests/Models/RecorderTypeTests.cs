using System.Text.Json;
using VoiSlate.Models;
using Xunit;

namespace VoiSlate.Tests.Models;

/// <summary>
/// RecorderType 模型测试（A 拥模型 fixture；契约 §1/§2 + 原版 models/recorder_type.dart 与
/// RecordFileNum.recorderType 字符串语义）。
/// </summary>
public class RecorderTypeTests
{
    [Fact]
    public void Numeric_Values_Match_Contract_Explicit_Codes()
    {
        Assert.Equal(0, (int)RecorderType.DefaultRecorder);
        Assert.Equal(1, (int)RecorderType.SoundDevices);
        Assert.Equal(2, (int)RecorderType.Custom);
    }

    [Fact]
    public void Json_Names_Are_CamelCase_Short_Names()
    {
        // 对齐原版枚举成员名 defaultRecorder/soundDevices/custom（A#16 camelCase 短名）。
        Assert.Equal("\"defaultRecorder\"", JsonSerializer.Serialize(RecorderType.DefaultRecorder, VoiSlateJson.Options));
        Assert.Equal("\"soundDevices\"", JsonSerializer.Serialize(RecorderType.SoundDevices, VoiSlateJson.Options));
        Assert.Equal("\"custom\"", JsonSerializer.Serialize(RecorderType.Custom, VoiSlateJson.Options));

        var back = JsonSerializer.Deserialize<RecorderType>("\"soundDevices\"", VoiSlateJson.Options);
        Assert.Equal(RecorderType.SoundDevices, back);
    }

    [Fact]
    public void Settings_Strings_RoundTrip_Like_Original_RecorderType_Field()
    {
        Assert.Equal("default", RecorderType.DefaultRecorder.ToSettingsValue());
        Assert.Equal("sound devices", RecorderType.SoundDevices.ToSettingsValue());
        Assert.Equal("custom", RecorderType.Custom.ToSettingsValue());

        Assert.Equal(RecorderType.DefaultRecorder, RecorderTypeExtensions.ParseSettings("default"));
        Assert.Equal(RecorderType.SoundDevices, RecorderTypeExtensions.ParseSettings("sound devices"));
        Assert.Equal(RecorderType.Custom, RecorderTypeExtensions.ParseSettings("custom"));
    }

    [Fact]
    public void ParseSettings_Falls_Back_To_DefaultRecorder_And_Is_Case_Sensitive()
    {
        // 原版比较精确字符串（"default"/"sound devices"/"custom"）；未知值回退默认。
        Assert.Equal(RecorderType.DefaultRecorder, RecorderTypeExtensions.ParseSettings(null));
        Assert.Equal(RecorderType.DefaultRecorder, RecorderTypeExtensions.ParseSettings(""));
        Assert.Equal(RecorderType.DefaultRecorder, RecorderTypeExtensions.ParseSettings("CUSTOM"));
        Assert.Equal(RecorderType.DefaultRecorder, RecorderTypeExtensions.ParseSettings("SoundDevices"));
    }

    [Fact]
    public void PrefixType_Mapping_RoundTrips()
    {
        Assert.Equal(PrefixType.Default, RecorderType.DefaultRecorder.ToPrefixType());
        Assert.Equal(PrefixType.SoundDevices, RecorderType.SoundDevices.ToPrefixType());
        Assert.Equal(PrefixType.Custom, RecorderType.Custom.ToPrefixType());

        Assert.Equal(RecorderType.DefaultRecorder, PrefixType.Default.ToRecorderType());
        Assert.Equal(RecorderType.SoundDevices, PrefixType.SoundDevices.ToRecorderType());
        Assert.Equal(RecorderType.Custom, PrefixType.Custom.ToRecorderType());
    }
}