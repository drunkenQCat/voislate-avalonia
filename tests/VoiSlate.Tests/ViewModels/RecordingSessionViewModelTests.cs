using VoiSlate.Models;
using VoiSlate.Services;
using VoiSlate.Tests.TestDoubles;
using VoiSlate.ViewModels;
using Xunit;

namespace VoiSlate.Tests.VM;

/// <summary>
/// RecordingSessionViewModel：13 键默认值/加载/持久化 + ISessionState 映射（契约 §4）。
/// 键名与默认值对齐原版 slate_status_notifier.dart。
/// </summary>
public class RecordingSessionViewModelTests
{
    private static Task<RecordingSessionViewModel> NewAsync(Action<FakeSessionSettingsStore>? seed = null)
    {
        var settings = new FakeSessionSettingsStore();
        seed?.Invoke(settings);
        var vm = new RecordingSessionViewModel(settings, new FakeTimeProvider());
        return vm.Initialization.ContinueWith(_ => vm);
    }

    [Fact]
    public async Task Empty_Store_Yields_Original_Defaults()
    {
        var vm = await NewAsync();

        Assert.Equal(0, vm.SelectedSceneIndex);
        Assert.Equal(0, vm.SelectedShotIndex);
        Assert.Equal(0, vm.SelectedTakeIndex);
        Assert.True(vm.IsLinked);
        Assert.Equal("-T", vm.RecordLinker);
        Assert.Equal("default", vm.PrefixType);
        Assert.Equal("custom", vm.CustomPrefix);
        Assert.Equal("", vm.CurrentDesc);
        Assert.Equal("", vm.CurrentNote);
        Assert.Equal(TkStatus.NotChecked, vm.OkTk);
        Assert.Equal(ShtStatus.NotChecked, vm.OkSht);
        Assert.Equal(200, vm.TakeCount);
        Assert.Equal("260820", vm.Date);
        Assert.Equal(1, vm.RecordCount); // 无日期记录 → 跨日语义 → 1
    }

    [Fact]
    public async Task Constructor_Loads_All_Keys_From_Store()
    {
        var vm = await NewAsync(s =>
        {
            s.Data[SessionKeys.SceneIndex] = 2;
            s.Data[SessionKeys.ShotIndex] = 1;
            s.Data[SessionKeys.TakeIndex] = 3;
            s.Data[SessionKeys.IsLinked] = false;
            s.Data[SessionKeys.Date] = "260820";
            s.Data[SessionKeys.RecordCount] = 7;
            s.Data[SessionKeys.RecordLinker] = "-X";
            s.Data[SessionKeys.PrefixType] = "custom";
            s.Data[SessionKeys.CustomPrefix] = "MY";
            s.Data[SessionKeys.Desc] = "描述";
            s.Data[SessionKeys.Note] = "标注";
            s.Data[SessionKeys.OkTk] = 1;
            s.Data[SessionKeys.OkSht] = 2;
        });

        Assert.Equal(2, vm.SelectedSceneIndex);
        Assert.Equal(1, vm.SelectedShotIndex);
        Assert.Equal(3, vm.SelectedTakeIndex);
        Assert.False(vm.IsLinked);
        Assert.Equal(7, vm.RecordCount);
        Assert.Equal("-X", vm.RecordLinker);
        Assert.Equal("custom", vm.PrefixType);
        Assert.Equal("MY", vm.CustomPrefix);
        Assert.Equal("描述", vm.CurrentDesc);
        Assert.Equal("标注", vm.CurrentNote);
        Assert.Equal(TkStatus.Ok, vm.OkTk);
        Assert.Equal(ShtStatus.Nice, vm.OkSht);
    }

    [Fact]
    public async Task Same_Day_RecordCount_Restores_Cross_Day_Resets_To_One()
    {
        var sameDay = await NewAsync(s =>
        {
            s.Data[SessionKeys.Date] = "260820"; // 与 FakeTimeProvider.Fixed 同日
            s.Data[SessionKeys.RecordCount] = 42;
        });
        Assert.Equal(42, sameDay.RecordCount);

        var crossDay = await NewAsync(s =>
        {
            s.Data[SessionKeys.Date] = "260819"; // 昨天
            s.Data[SessionKeys.RecordCount] = 42;
        });
        Assert.Equal(1, crossDay.RecordCount);
    }

    [Fact]
    public async Task SelectMethods_Persist_Keys_And_Force_Today_Date()
    {
        var settings = new FakeSessionSettingsStore();
        var vm = new RecordingSessionViewModel(settings, new FakeTimeProvider());
        await vm.Initialization;

        vm.SelectScene(3);
        vm.SelectShot(4);
        vm.SelectTake(5);
        vm.SetRecordCount(9);

        Assert.Equal(3, await settings.GetIntAsync(SessionKeys.SceneIndex, 0));
        Assert.Equal(4, await settings.GetIntAsync(SessionKeys.ShotIndex, 0));
        Assert.Equal(5, await settings.GetIntAsync(SessionKeys.TakeIndex, 0));
        Assert.Equal(9, await settings.GetIntAsync(SessionKeys.RecordCount, 0));
        Assert.Equal("260820", await settings.GetStringAsync(SessionKeys.Date, ""));
    }

    [Fact]
    public async Task SetNote_And_Mirrors_Persist_Desc_Note_Linker_Prefix()
    {
        var settings = new FakeSessionSettingsStore();
        var vm = new RecordingSessionViewModel(settings, new FakeTimeProvider());
        await vm.Initialization;

        vm.SetDesc("描述A");
        vm.SetNote("标注B");
        vm.SetLink(false);
        vm.SetRecordLinker("-Z");
        vm.SetPrefixType("sound devices");
        vm.SetCustomPrefix("PRE");

        Assert.Equal("描述A", await settings.GetStringAsync(SessionKeys.Desc, ""));
        Assert.Equal("标注B", await settings.GetStringAsync(SessionKeys.Note, ""));
        Assert.False(await settings.GetBoolAsync(SessionKeys.IsLinked, true));
        Assert.Equal("-Z", await settings.GetStringAsync(SessionKeys.RecordLinker, ""));
        Assert.Equal("sound devices", await settings.GetStringAsync(SessionKeys.PrefixType, ""));
        Assert.Equal("PRE", await settings.GetStringAsync(SessionKeys.CustomPrefix, ""));
    }

    [Fact]
    public async Task OkStatus_Persists_As_Int_And_Reset_Clears()
    {
        var settings = new FakeSessionSettingsStore();
        var vm = new RecordingSessionViewModel(settings, new FakeTimeProvider());
        await vm.Initialization;

        vm.SetOkTake(TkStatus.Bad);
        vm.SetOkShot(ShtStatus.Ok);
        Assert.Equal((int)TkStatus.Bad, await settings.GetIntAsync(SessionKeys.OkTk, 0));
        Assert.Equal((int)ShtStatus.Ok, await settings.GetIntAsync(SessionKeys.OkSht, 0));
        Assert.Equal(TkStatus.Bad, vm.PendingTakeOk);
        Assert.Equal(ShtStatus.Ok, vm.PendingShotOk);

        vm.ResetOkStatus();
        Assert.Equal((int)TkStatus.NotChecked, await settings.GetIntAsync(SessionKeys.OkTk, 0));
        Assert.Equal((int)ShtStatus.NotChecked, await settings.GetIntAsync(SessionKeys.OkSht, 0));
        Assert.Equal(TkStatus.NotChecked, vm.PendingTakeOk);
    }

    [Fact]
    public async Task Implements_ISessionState_With_200_Take_Count()
    {
        var vm = await NewAsync();
        ISessionState session = vm;

        session.SceneIndex = 11;
        session.ShotIndex = 12;
        session.TakeIndex = 13;
        session.IsLinked = false;

        Assert.Equal(11, vm.SelectedSceneIndex);
        Assert.Equal(12, vm.SelectedShotIndex);
        Assert.Equal(13, vm.SelectedTakeIndex);
        Assert.False(vm.IsLinked);
        Assert.Equal(200, session.TakeCount);
    }

    [Fact]
    public async Task SessionChanged_Fires_On_Index_And_Ok_Changes()
    {
        var vm = await NewAsync();
        var count = 0;
        vm.SessionChanged += () => count++;

        vm.SelectScene(1);
        vm.SetOkTake(TkStatus.Ok);
        vm.ResetOkStatus();
        vm.SetDesc("x"); // desc 不触发 SessionChanged（原版 setNote 无 notify 语义的收敛）

        Assert.Equal(3, count);
    }
}