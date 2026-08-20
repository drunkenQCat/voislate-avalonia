using VoiSlate.Services;
using Xunit;

namespace VoiSlate.Tests.Services;

/// <summary>麦克风对象提取（契约 §3 MicObjectsExtractor；B8 `<obj/>` 协议）。</summary>
public class MicObjectsExtractorTests
{
    [Fact]
    public void Extracts_Body_And_Tracks()
    {
        var (body, tracks) = MicObjectsExtractor.Extract("正文内容<麦克风/><boom 二号/>");

        Assert.Equal("正文内容", body);
        Assert.Equal(["麦克风", "boom 二号"], tracks);
    }

    [Fact]
    public void No_Tags_Returns_Empty_Tracks()
    {
        var (body, tracks) = MicObjectsExtractor.Extract("plain note");

        Assert.Equal("plain note", body);
        Assert.Empty(tracks);
    }

    [Fact]
    public void Empty_Input_Returns_Empty_Strings()
    {
        var (body, tracks) = MicObjectsExtractor.Extract(string.Empty);

        Assert.Equal(string.Empty, body);
        Assert.Empty(tracks);
    }

    [Fact]
    public void Stray_Slash_GT_Inside_Track_Name_Is_Stripped_Everywhere()
    {
        // 原版 replaceAll('/>', '')：段内所有 "/>" 均被剥除。
        var (_, tracks) = MicObjectsExtractor.Extract("a<麦克风/>b/>");

        Assert.Equal(["麦克风b"], tracks);
    }
}