using VoiSlate.Services;
using VoiSlate.Tests.TestDoubles;
using VoiSlate.ViewModels;
using Xunit;

namespace VoiSlate.Tests.VM;

/// <summary>
/// MainViewModel：CurrentPage/Pages/NavigateCommand（契约 §4/§6）。Record 页每次导航新建实例（R15），其余复用单例。
/// </summary>
public class MainViewModelTests
{
    private sealed record Harness(MainViewModel Main, List<RecordViewModel> Created, ScheduleViewModel Schedule);

    private static Harness NewHarness()
    {
        var time = new FakeTimeProvider();
        var settings = new FakeSessionSettingsStore();
        var session = new RecordingSessionViewModel(settings, time);
        session.Initialization.GetAwaiter().GetResult();

        var logs = new FakeLogRepository();
        var book = new StubScheduleBook();
        var flow = new TakeFlowService(logs, new FakePickerHistoryStore(), session,
            new FileNumberingService(time), settings, time, new NoopHapticsService(), new NoopToastService());
        flow.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        var keys = new TestHardwareKeyService();

        var created = new List<RecordViewModel>();
        RecordViewModel Factory()
        {
            var vm = new RecordViewModel(settings, flow, new MockAsrService(), keys, session, time);
            created.Add(vm);
            return vm;
        }

        var schedule = new ScheduleViewModel(new StubScheduleStore(), new StubCsvScheduleParser(), session);
        var slateLog = new SlateLogPageViewModel(logs, new SpyExportService(), flow, session, book, time);
        var settingsVm = new SettingsViewModel(settings, logs, flow, new SpyExportService(), time);
        settingsVm.Initialization.GetAwaiter().GetResult();

        var main = new MainViewModel(Factory, schedule, slateLog, settingsVm);
        return new Harness(main, created, schedule);
    }

    [Fact]
    public void Starts_On_Record_Page_With_Fresh_Instance()
    {
        var h = NewHarness();
        Assert.IsType<RecordViewModel>(h.Main.CurrentPage);
        Assert.Single(h.Created);
    }

    [Fact]
    public void Pages_Expose_Four_Entries_In_Order()
    {
        var h = NewHarness();
        Assert.Equal(
            [MainViewModel.RecordPageKey, MainViewModel.SchedulePageKey, MainViewModel.SlateLogPageKey, MainViewModel.SettingsPageKey],
            h.Main.Pages.Select(p => p.Key));
        Assert.Equal(["记录", "计划", "场记", "设置"], h.Main.Pages.Select(p => p.Title));
    }

    [Fact]
    public void Navigate_Record_Creates_New_Instance_Each_Time()
    {
        var h = NewHarness();
        var first = h.Main.CurrentPage;

        h.Main.NavigateCommand.Execute(MainViewModel.RecordPageKey);
        Assert.NotSame(first, h.Main.CurrentPage);
        Assert.Equal(2, h.Created.Count);
    }

    [Fact]
    public void Navigate_To_Singleton_Pages_Reuses_Instances()
    {
        var h = NewHarness();

        h.Main.NavigateCommand.Execute(MainViewModel.SchedulePageKey);
        Assert.Same(h.Schedule, h.Main.CurrentPage);

        h.Main.NavigateCommand.Execute(MainViewModel.SchedulePageKey);
        Assert.Same(h.Schedule, h.Main.CurrentPage);
        Assert.Single(h.Created); // 记录页工厂未被再次调用
    }

    [Fact]
    public void Unknown_Key_Is_Ignored()
    {
        var h = NewHarness();
        var before = h.Main.CurrentPage;

        h.Main.NavigateCommand.Execute("???");

        Assert.Same(before, h.Main.CurrentPage);
    }
}