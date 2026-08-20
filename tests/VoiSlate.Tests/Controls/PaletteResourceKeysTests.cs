using System.IO;
using VoiSlate.Controls;
using Xunit;

namespace VoiSlate.Tests.Controls;

/// <summary>
/// 契约 §6 主题资源键逐字核对（纯文件扫描，不需要 Avalonia headless 平台）。
/// 读取源工程 Themes/VoiSlatePalette.axaml 与 Themes/Controls.axaml 断言键/选择器存在。
/// </summary>
public class PaletteResourceKeysTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "VoiSlate.slnx")) ||
                Directory.Exists(Path.Combine(dir.FullName, "src", "VoiSlate")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("无法定位仓库根目录（VoiSlate.slnx）。");
    }

    private static string PalettePath => Path.Combine(RepoRoot, "src", "VoiSlate", "Themes", "VoiSlatePalette.axaml");

    private static string ControlsPath => Path.Combine(RepoRoot, "src", "VoiSlate", "Themes", "Controls.axaml");

    private static string ReadPalette() => File.ReadAllText(PalettePath);

    [Fact]
    public void PaletteFile_Exists()
    {
        Assert.True(File.Exists(PalettePath), "VoiSlatePalette.axaml 缺失。");
    }

    [Theory]
    [MemberData(nameof(ContractRequiredKeys))]
    public void Palette_ContainsContractRequiredKey(string key)
    {
        var axaml = ReadPalette();
        Assert.Contains($"x:Key=\"{key}\"", axaml);
    }

    public static TheoryData<string> ContractRequiredKeys()
    {
        var data = new TheoryData<string>();
        foreach (var key in VoiSlatePalette.ContractRequiredKeys)
        {
            data.Add(key);
        }

        return data;
    }

    [Theory]
    [InlineData("VoiSlate.WheelItemBackground")]
    [InlineData("VoiSlate.CardBackground")]
    [InlineData("VoiSlate.SlatePurple")]
    [InlineData("VoiSlate.DialNotChecked")]
    [InlineData("VoiSlate.RecordPageBackground")]
    [InlineData("VoiSlate.NeutralColor")]
    [InlineData("VoiSlate.AccentColor")]
    public void Palette_ContainsControlSupportKeys(string key)
    {
        var axaml = ReadPalette();
        Assert.Contains($"x:Key=\"{key}\"", axaml);
    }

    [Fact]
    public void ControlsStyles_DefineAllCustomControls()
    {
        var axaml = File.ReadAllText(ControlsPath);
        Assert.True(File.Exists(ControlsPath), "Controls.axaml 缺失。");

        foreach (var selector in new[]
                 {
                     "controls|SlideConfirmBar",
                     "controls|DialFAB",
                     "controls|FileCounter",
                     "controls|TagChips",
                     "controls|ToastHost",
                     "controls|LoadingOverlay",
                 })
        {
            Assert.Contains(selector, axaml);
        }
    }

    [Fact]
    public void Palette_KeysUseContractNamingPrefix()
    {
        var axaml = ReadPalette();
        var lines = axaml.Split('\n');
        var keys = lines
            .Where(l => l.Contains("x:Key=\"VoiSlate."))
            .Select(l => l.Split("x:Key=\"")[1].Split('"')[0])
            .ToList();

        Assert.NotEmpty(keys);
        Assert.All(keys, key => Assert.StartsWith("VoiSlate.", key));
    }
}