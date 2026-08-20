using Xunit;

namespace VoiSlate.Tests;

/// <summary>M0 冒烟测试：验证测试项目与主项目引用链路可用（契约 v0.5 §7 测试基线）。</summary>
public class SmokeTests
{
    [Fact]
    public void Main_Project_Types_Resolvable()
    {
        var vm = new ViewModels.PlaceholderViewModel();
        Assert.False(string.IsNullOrWhiteSpace(vm.PlaceholderText));
        Assert.Equal("M0 skeleton compiles — P0.5 vertical slice next.", vm.PlaceholderText);
    }
}