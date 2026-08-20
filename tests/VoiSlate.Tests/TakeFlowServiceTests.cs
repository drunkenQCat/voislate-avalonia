using VoiSlate.Models;
using VoiSlate.Services;
using VoiSlate.Tests.TestDoubles;
using Xunit;

namespace VoiSlate.Tests;

/// <summary>
/// 记录流核心时序测试（N1：B2/B3 语义在此成文锁定——首次按写 normal、空栈视为 ['0','0','0']、
/// shot 变更/end 自动优良、fake/wild 哨兵值、end 前置守卫、撤回两级行为、B7 文件号 + BLOCKER-1 API）。
/// 逐行对齐原版 record_page.addItem / drawBackItem / shotEndBtn。
/// </summary>
public class TakeFlowServiceTests
{
    private sealed record FlowFixture(
        TakeFlowService Service,
        FakeLogRepository Logs,
        FakePickerHistoryStore History,
        FakeSessionSettingsStore Settings,
        FileNumberingService FileNum,
        FakeSessionState Session);

    private static TakeFlowService Create(out FakeLogRepository logs, out FakePickerHistoryStore history,
        out FakeSessionSettingsStore settings, out FileNumberingService fileNum, out FakeSessionState session)
    {
        logs = new FakeLogRepository();
        history = new FakePickerHistoryStore();
        settings = new FakeSessionSettingsStore();
        fileNum = new FileNumberingService(new FakeTimeProvider());
        session = new FakeSessionState();
        var svc = new TakeFlowService(
            logs, history, session, fileNum, settings, new FakeTimeProvider(),
            new NoopHapticsService(), new NoopToastService())
        {
            SceneLabelProvider = () => "1A",
            ShotLabelProvider = () => "1A",
            CurrentObjectsProvider = () => ["缪尔赛斯", "塞雷娅"],
        };
        return svc;
    }

    private static async Task<FlowFixture> NewFlowAsync(
        Action<FakeSessionSettingsStore>? seedSettings = null)
    {
        var svc = Create(out var logs, out var history, out var settings, out var fileNum, out var session);
        seedSettings?.Invoke(settings);
        await svc.InitializeAsync(CancellationToken.None);
        return new FlowFixture(svc, logs, history, settings, fileNum, session);
    }

    [Fact]
    public async Task First_Press_With_Empty_History_Writes_No_Log_But_Pushes_Normal_Keyword()
    {
        var fx = await NewFlowAsync();

        await fx.Service.AddItemAsync(TakeType.Normal, CancellationToken.None);

        // number==1 → prevFileName 空 → 不写日志（原版守卫）
        Assert.Empty(fx.Logs.Items);
        Assert.Single(fx.History.Entries);
        Assert.Equal(["1A", "1A", "1", "缪尔赛斯", "塞雷娅"], fx.History.Entries[0]);
        Assert.Equal(2, fx.FileNum.Number);
        Assert.Equal(2, await fx.Settings.GetIntAsync(SessionKeys.RecordCount, 0));
    }

    [Fact]
    public async Task Second_Press_Logs_Item_Derived_From_History_Tail()
    {
        var fx = await NewFlowAsync();
        var svc = fx.Service;

        await svc.AddItemAsync(TakeType.Normal, CancellationToken.None);
        await svc.AddItemAsync(TakeType.Normal, CancellationToken.None);

        var item = Assert.Single(fx.Logs.Items).Item;
        Assert.Equal("1A", item.Scn);
        Assert.Equal("1A", item.Sht);
        Assert.Equal(1, item.Tk);                          // sign='1'
        Assert.Equal(1, item.FilenameNum);                 // prevFileNum = 2-1
        Assert.Equal("260820-T001", item.FileName);
        Assert.Equal("260820-T001", fx.Logs.Items[0].Key); // key = prevFileName
        Assert.Equal("S1A Sh1A Tk1", item.TkNote);          // desc 空 → 缺省格式
        Assert.Equal("<缪尔赛斯/><塞雷娅/>", item.ShtNote); // shotNote 空 + 上一拍 objects 的 trackLogs
        Assert.Equal(TkStatus.NotChecked, item.OkTk);
        Assert.Equal(ShtStatus.NotChecked, item.OkSht);
        Assert.Equal(3, fx.FileNum.Number);
    }

    [Fact]
    public async Task Fake_Mark_Pushes_F_And_Following_Press_Logs_Fake_Sentinel_Values()
    {
        var fx = await NewFlowAsync();
        var svc = fx.Service;

        await svc.AddItemAsync(TakeType.Normal, CancellationToken.None); // num2, tail '1', 无日志
        await svc.AddItemAsync(TakeType.Fake, CancellationToken.None);   // log1(sign '1') + tail 'F'
        await svc.AddItemAsync(TakeType.Normal, CancellationToken.None); // log2(sign 'F') → 假条哨兵

        Assert.Equal(2, fx.Logs.Items.Count);
        var fakeItem = fx.Logs.Items[^1].Item;
        Assert.Equal(999, fakeItem.Tk);
        Assert.Equal("Fake Take", fakeItem.TkNote);
        Assert.Equal(TkStatus.Bad, fakeItem.OkTk);
        Assert.Equal(ShtStatus.NotChecked, fakeItem.OkSht);
        Assert.Equal("F", fx.History.Entries[1][2]);
        Assert.Equal(4, fx.FileNum.Number); // fake 也递增（非 end）
    }

    [Fact]
    public async Task End_Take_Guard_Returns_Silently_On_F_Or_OK_Tail_Or_Number_One()
    {
        var fx = await NewFlowAsync();
        var svc = fx.Service;

        await svc.AddItemAsync(TakeType.End, CancellationToken.None); // number==1 + 空历史 → 返回
        Assert.Empty(fx.Logs.Items);
        Assert.Empty(fx.History.Entries);
        Assert.Equal(1, fx.FileNum.Number);

        await svc.AddItemAsync(TakeType.Fake, CancellationToken.None); // tail 'F'
        var before = fx.History.Entries.Count;
        await svc.AddItemAsync(TakeType.End, CancellationToken.None);  // F 尾 → 返回
        Assert.Equal(before, fx.History.Entries.Count);
        Assert.Equal(2, fx.FileNum.Number);
        Assert.Empty(fx.Logs.Items);
    }

    [Fact]
    public async Task End_Take_Logs_With_Auto_Best_And_Does_Not_Increment()
    {
        var fx = await NewFlowAsync();
        var svc = fx.Service;

        fx.Session.TakeIndex = 0;
        await svc.AddItemAsync(TakeType.Normal, CancellationToken.None); // num 2, 无日志
        fx.Session.TakeIndex = 1;
        await svc.AddItemAsync(TakeType.Normal, CancellationToken.None); // num 3, log1 sign 1
        fx.Session.TakeIndex = 2;
        await svc.AddItemAsync(TakeType.End, CancellationToken.None);    // log2 sign 2

        Assert.Equal(2, fx.Logs.Items.Count);
        var endItem = fx.Logs.Items[^1].Item;
        Assert.Equal(2, endItem.Tk);                       // sign='2'
        Assert.Equal(TkStatus.Ok, endItem.OkTk);           // end → 自动优良（B3）
        Assert.Equal(ShtStatus.Nice, endItem.OkSht);
        Assert.Equal("OK", fx.History.Entries[^1][2]);
        Assert.Equal(3, fx.FileNum.Number);                // end 不递增
        Assert.Equal("260820-T002", fx.Logs.Items[^1].Key);
    }

    [Fact]
    public async Task Rewind_On_OK_Tail_Pops_Marker_Only_And_Restores_Notes()
    {
        var fx = await NewFlowAsync();
        var svc = fx.Service;

        fx.Session.TakeIndex = 0;
        await svc.AddItemAsync(TakeType.Normal, CancellationToken.None); // num 2
        fx.Session.TakeIndex = 1;
        await svc.AddItemAsync(TakeType.Normal, CancellationToken.None); // log1, tail '2'
        fx.Session.TakeIndex = 2;
        await svc.AddItemAsync(TakeType.End, CancellationToken.None);    // log2, tail OK
        var countBefore = fx.Logs.Items.Count;
        var numBefore = fx.FileNum.Number;

        var result = await svc.RewindAsync(CancellationToken.None);

        Assert.True(result.WasOkMarkerOnly);
        Assert.Equal(countBefore, fx.Logs.Items.Count);       // 不删日志
        Assert.Equal(numBefore, fx.FileNum.Number);           // 不递减
        Assert.Equal("2", fx.History.Entries[^1][2]);         // OK 哨兵已弹，尾回退到 sign '2'
        Assert.Equal(fx.Logs.Items[^1].Item.TkNote, result.RestoredDesc);
    }

    [Fact]
    public async Task Regular_Rewind_Decrements_Pops_Deletes_And_Restores_Notes()
    {
        var fx = await NewFlowAsync();
        var svc = fx.Service;

        await svc.AddItemAsync(TakeType.Normal, CancellationToken.None); // num 2
        await svc.AddItemAsync(TakeType.Normal, CancellationToken.None); // log1 num 3
        var lastLog = fx.Logs.Items[^1].Item;

        var result = await svc.RewindAsync(CancellationToken.None);

        Assert.False(result.WasOkMarkerOnly);
        Assert.Equal(2, fx.FileNum.Number);
        Assert.Empty(fx.Logs.Items);                           // 末条已删
        Assert.Single(fx.History.Entries);                     // 尾已弹
        Assert.Equal(lastLog.TkNote, result.RestoredDesc);
        Assert.Equal(lastLog.ShtNote.Split('<').First(), result.RestoredShotNote);
    }

    [Fact]
    public async Task Unlinked_Press_Converts_To_Wild_Keyword_And_Next_Log_Is_Wild()
    {
        var fx = await NewFlowAsync();
        fx.Session.IsLinked = false;
        var svc = fx.Service;

        await svc.AddItemAsync(TakeType.Normal, CancellationToken.None);
        Assert.Equal("W", fx.History.Entries[^1][2]);          // 未联动 → 关键字 'W'
        Assert.Empty(fx.Logs.Items);

        await svc.AddItemAsync(TakeType.Normal, CancellationToken.None);
        var item = Assert.Single(fx.Logs.Items).Item;          // 尾 sign='W' → wild：tk=0 + 前缀
        Assert.Equal(0, item.Tk);
        Assert.StartsWith("wild track ", item.TkNote);
        Assert.Equal("W", fx.History.Entries[^1][2]);
    }

    [Fact]
    public async Task Wild_Log_Prefixes_Override_Note()
    {
        var fx = await NewFlowAsync();
        fx.Session.IsLinked = false;
        var svc = fx.Service;

        await svc.AddItemAsync(TakeType.Normal, CancellationToken.None); // tail W, no log
        await svc.AddItemAsync(TakeType.Normal, CancellationToken.None, tkNoteOverride: "hello");
        var item = Assert.Single(fx.Logs.Items).Item;
        Assert.Equal("wild track hello", item.TkNote);
    }

    [Fact]
    public async Task Empty_History_Second_Press_Writes_Zero_Logs_When_Number_Is_Greater_Than_One()
    {
        // B2 锁定：空栈视为 ['0','0','0']；若 number>1（例如恢复过 recordCount），首按即写 '0' 日志
        var fx = await NewFlowAsync(s =>
        {
            s.Data[SessionKeys.Date] = "260820";
            s.Data[SessionKeys.RecordCount] = 5;
        });
        Assert.Equal(5, fx.FileNum.Number);

        await fx.Service.AddItemAsync(TakeType.Normal, CancellationToken.None);

        var item = Assert.Single(fx.Logs.Items).Item;
        Assert.Equal("0", item.Scn);
        Assert.Equal("0", item.Sht);
        Assert.Equal(0, item.Tk);
        Assert.Equal("S0 Sh0 Tk0", item.TkNote);
        Assert.Equal(4, item.FilenameNum); // prevFileNum = 5-1
        Assert.Equal(6, fx.FileNum.Number);
    }

    [Fact]
    public async Task B7_FileNumber_Edits_Persist_And_Raise_FileNumberChanged()
    {
        var fx = await NewFlowAsync();
        var events = new List<int>();
        fx.Service.FileNumberChanged += n => events.Add(n);

        await fx.Service.SetFileNumberAsync(50, CancellationToken.None);
        Assert.Equal(50, fx.FileNum.Number);
        Assert.Equal(50, await fx.Settings.GetIntAsync(SessionKeys.RecordCount, 0));
        Assert.Contains(50, events);

        await fx.Service.SetLinkerAsync("-X", CancellationToken.None);
        Assert.Equal("-X", fx.FileNum.Linker);
        Assert.Equal("-X", await fx.Settings.GetStringAsync(SessionKeys.RecordLinker, ""));

        await fx.Service.SetPrefixAsync(PrefixType.Custom, "MY", CancellationToken.None);
        Assert.Equal(PrefixType.Custom, fx.FileNum.PrefixMode);
        Assert.Equal("MY", fx.FileNum.CustomPrefix);
        Assert.Equal("custom", await fx.Settings.GetStringAsync(SessionKeys.PrefixType, ""));
        Assert.Equal("MY", await fx.Settings.GetStringAsync(SessionKeys.CustomPrefix, ""));
    }

    [Fact]
    public async Task Initialize_Restores_Linker_Prefix_And_Count_From_Settings()
    {
        var fx = await NewFlowAsync(s =>
        {
            s.Data[SessionKeys.RecordLinker] = "-Z";
            s.Data[SessionKeys.PrefixType] = "sound devices";
            s.Data[SessionKeys.CustomPrefix] = "custom";
            s.Data[SessionKeys.Date] = "260820";
            s.Data[SessionKeys.RecordCount] = 7;
        });

        await fx.Service.InitializeAsync(CancellationToken.None);

        Assert.Equal("-Z", fx.FileNum.Linker);
        Assert.Equal(PrefixType.SoundDevices, fx.FileNum.PrefixMode);
        Assert.Equal(7, fx.FileNum.Number);
        Assert.StartsWith("26Y08M20", fx.FileNum.Prefix);    // sound devices 前缀
    }

    [Fact]
    public async Task Initialize_Resets_Count_To_One_When_Date_Is_Old()
    {
        var fx = await NewFlowAsync(s =>
        {
            s.Data[SessionKeys.Date] = "260819";              // 昨天
            s.Data[SessionKeys.RecordCount] = 9;
        });

        Assert.Equal(1, fx.FileNum.Number);                   // NewFlowAsync 已初始化（旧日期 → 1）
    }

    [Fact]
    public async Task SaveEdit_And_Delete_Go_Through_Service_And_Raise_LogsChanged()
    {
        var fx = await NewFlowAsync();
        var svc = fx.Service;

        await svc.AddItemAsync(TakeType.Normal, CancellationToken.None); // no log, num 2
        await svc.AddItemAsync(TakeType.Normal, CancellationToken.None); // log1
        var count = 0;
        svc.LogsChanged += () => count++;

        var item = fx.Logs.Items[0].Item;
        item.TkNote = "edited";
        await svc.SaveEditAsync(item, 0, CancellationToken.None);
        Assert.Equal("edited", fx.Logs.Items[0].Item.TkNote);

        await svc.DeleteItemAsync(0, CancellationToken.None);
        Assert.Empty(fx.Logs.Items);
        Assert.Equal(2, count);
    }
}