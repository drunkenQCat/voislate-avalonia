# VoiSlate Avalonia 模块契约规范（contracts.md）

> 版本：v0.3（闭环 Review 第 3 轮：JSON 枚举 CamelCase 修正 + B1-B5 契约矛盾）｜最后更新：2026-08-20
> 用途：第三阶段并行开发唯一契约依据。**契约未达签名级前不开工**（风险 R8）。
> 变更纪律：契约变更仅由主 Agent 更新；各 worktree 同步时 rebase，冲突概率近零。

---

## 1. 命名与编码约定

| 项 | 规则 |
|---|---|
| 命名空间 | `VoiSlate.Models` / `VoiSlate.Services` / `VoiSlate.ViewModels` / `VoiSlate.Views` / `VoiSlate.Controls` / `VoiSlate.Infrastructure` |
| 枚举 | PascalCase；**显式数值**：`TkStatus { NotChecked=0, Ok=1, Bad=2 }`、`ShtStatus { NotChecked=0, Ok=1, Nice=2 }`、`TakeType { Normal, Fake, End, Wild }`（0-3）、`RecorderType { DefaultRecorder, SoundDevices, Custom }`（0-2） |
| Service 事件 | `event Action<T>? {Thing}Changed`（统一采用 Action；**不用 EventHandler**——本规约唯一事件约定） |
| 命令 | `RelayCommand`，命名 `{Action}Command` |
| JSON | camelCase 属性名 + **`JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`**（必须显式命名策略——默认输出 PascalCase 成员名与原件短名**不等价**）；导出格式与原件一致（无日期字段、含 fake/wild 哨兵值） |
| 存储访问 | **只经 Repository/Store 接口**；View/VM 禁止直连存储（F1 纪律） |

## 2. Models 契约（Agent A 产出；其余 Agent 只读）

- `SlateLogItem`：`Scn`/`Sht`/`Tk`(int)/`FilenamePrefix`/`FilenameLinker`/`FilenameNum`/`TkNote`/`ShtNote`/`ScnNote`/`OkTk: TkStatus`/`OkSht: ShtStatus`；计算属性 `FileName => $"{FilenamePrefix}{FilenameLinker}{FilenameNum:D3}"`
- `TkStatus`/`ShtStatus`/`TakeType`/`RecorderType`：见 §1 显式数值
- `ScheduleItem`：`Key`(string 数字)/`Fix`(string 字母)/`Name`（计算=Key+Fix，setter 同步重算）/`Note`；无 fromJson 空壳
- `Note`：`Objects: List<string>`/`Type`/`Append`
- `DataList<T>`：`Items` + `Add/Insert/Update/Remove/Indexer(set 也走校验)`；name 重复抛 `DuplicateItemException`；**修复**：先查重再赋值（无引用比较）
- `SceneSchedule : DataList<ScheduleItem>`：`Info` + 索引器
- `TkPending`：**由 `RecordingSessionViewModel.PendingTakeOk/PendingShotOk` 承载**（不另设类）
- `FileNumberingService`（**Agent A 产出，纯状态+规则，不写存储**）：`Number`(int, 起始 1)、`Prefix`(string, 按 RecorderType+ITimeProvider)、`Linker`(string)、`event Action<int>? NumberChanged`、`void SetValue(int)`、`int Increment()`、`int Decrement()`（下限 1）、`string FullName()`、`string PrevFileName()`（Number==1 → ""）、`int PrevFileNum()`；构造注入 `ITimeProvider`

## 3. Services 契约（Agent E 产出；签名级）

| 接口 | 关键成员 | 说明/Mock |
|---|---|---|
| `IAsrService` | `Task StartAsync(CancellationToken)`, `Task StopAsync()`, `event Action<string>? ResultReceived`, `event Action<string>? StatusChanged`, `bool IsListening` | Mock：Start 1.2s 后 ResultReceived("示例转写") |
| `IRecordingService` | `Task<bool> RequestPermissionAsync()`, `Task StartAsync()`, `Task StopAsync()`, `event Action<double>? LevelChanged` | Mock：模拟电平（500ms 周期） |
| `IFileNamingService` | `string GetPrefix(RecorderType, DateTime)`, `string FormatFileName(prefix, linker, int number)` | 静态规则（B6）；供 FileNumberingService 调用 |
| `ISessionSettingsStore` | `int GetInt(string key, int def)`, `bool GetBool(key, def)`, `string? GetString(key)`, `T GetEnum<T>(key, def)`, `void SetInt/SetBool/SetString/SetEnum(...)`, `string Date { get; set; }`, `string TodayStamp` | LiteDB 单文档；13 键 |
| `ILogRepository` | `Task<IReadOnlyList<SlateLogItem>> GetByDateAsync(string date)`, `Task AppendAsync(string prevFileName, SlateLogItem item)`, `Task<SlateLogItem?> RemoveLastAsync()`, `Task RemoveAtAsync(int i)`, `Task RemoveByFileNameAsync(string key)`, `Task PutAtAsync(int i, SlateLogItem item)`, `Task ClearAsync()`, `Task<IReadOnlyList<string>> GetDatesAsync()`, `Task DeleteDayAsync(string date)` | LiteDB `slate_logs`；保序 |
| `ScheduleStore` | `Task<IReadOnlyList<SceneSchedule>> LoadAllAsync()`, `Task SaveAllAsync(IReadOnlyList<SceneSchedule>)`, `Task ClearAsync()` | LiteDB `scenes` |
| `IPickerHistoryStore` | `IReadOnlyList<string[]>? Last()`, `void Append(string[] entry)`, `void RemoveLast()`, `void Clear()` | entry=[场,镜,tk关键字,...对象] |
| `CsvScheduleParser` | `Task<IReadOnlyList<SceneSchedule>> ParseAsync(Stream, CancellationToken)` | CsvHelper；7 列（0 场景号，1 场景内容，2 镜头号，3 补充，4 景别默认"近景"，5 镜头内容，6 补充）；objects 默认 ['Boom']（文档注明） |
| `MicObjectsExtractor` | `static (string Body, IReadOnlyList<string> Tracks) Extract(string shtNote)` | 协议 `<obj/>` |
| `IBackupService` | `Task BackupAsync(CancellationToken)`, 启动 3 分钟 PeriodicTimer + 退出前 + 手动 | JSON `Documents/VoiSlate Logs/slate_backup{yymmdd}-{hour}clock.json` |
| `ITimeProvider` | `DateTime Today`, `string TodayStamp`(yymmdd), `string SoundDevicesStamp`(yyYmMd) | 可注入固定时间 |
| `IToastService` | `void Show(string message)` | ToastHost 实现 |
| `IHapticsService` | `void Pulse(int durationMs, int amplitude = 128)` | no-op |
| `IHardwareKeyService` | `event Action<HardwareKey>? KeyPressed`；`enum HardwareKey { VolumeUp, VolumeDown }`；仅记录页激活时订阅 | 桌面 no-op；Android 增强后续 |
| `IDayRolloverService` | `bool IsNewDay()`, `void OnStartup()`, 定时检测（PeriodicTimer 1min 起） | 跨天：recordCount=1 + 清 history + 日期登记 |
| `IExportService` | `string SerializeLogs(IEnumerable<SlateLogItem>)`, `Task SaveToFileAsync(string dir, string name, string content)` | camelCase JSON；格式兼容原件 |
| `ITakeFlowService` | `Task AddItemAsync(TakeType type, CancellationToken)`, `Task RewindAsync(CancellationToken)`, `Task SaveEditAsync(SlateLogItem item, int index, CancellationToken)`, `Task DeleteItemAsync(int index, CancellationToken)`, `event Action? LogsChanged`, `event Action<int>? FileNumberChanged`, `event Action? HistoryChanged`；**唯一写入口纪律**：本服务是 ILogRepository / IPickerHistoryStore / 文件号存储变更的**唯一写者**（含日志编辑/删除——LogEditorViewModel **只**经本服务）；注入 RecordingSessionViewModel（会话）、FileNumberingService、ILogRepository、IPickerHistoryStore、IHapticsService、IToastService、ITimeProvider，业务规则 B1-B5/B11 全部在此实现 | P0.5 先实现核心；演进权归 E |
| `SeedService` | `Task EnsureSeededAsync()` | 空库播种两份生产场表（dummy_data 语义），归主 Agent/P0.5 |

## 4. ViewModels 契约（Agent B 产出，依赖 §2/§3）

| VM | 关键可观察成员 | 命令 | 备注 |
|---|---|---|---|
| `RecordingSessionViewModel`（DI 单例） | `SelectedSceneIndex/SelectedShotIndex/SelectedTakeIndex`(int)、`IsLinked`(bool)、`RecordCount`(int)、`RecordLinker`/`PrefixType`/`CustomPrefix`/`CurrentDesc`/`CurrentNote`(string)、`OkTk`/`OkSht`(enum)、`Date`(只读)、`PendingTakeOk`/`PendingShotOk`(enum) | `SelectScene/SelectShot/SelectTake/SetRecordCount/SetLink/SetOkTake/SetOkShot/ResetOkStatus` | On{X}Changed 写入 ISessionSettingsStore |
| `SlateLogViewModel` | `TodayLogs: ObservableCollection<SlateLogItem>`、`Dates: ObservableCollection<string>`、`Today` | **只读展示**：订阅 ITakeFlowService.LogsChanged 刷新；**无公开持久化写 API** | 修复原旁路 |
| `SlateColumnViewModel`（每列实例） | `Items: IReadOnlyList<string>`、`SelectedIndex`(int TwoWay)、`SelectedItem`(计算) | `ScrollTo(int index, bool animate = true)`、`ScrollNext(bool isLinked)`、`ScrollPrev(bool isLinked)`（边界/循环语义） | 不含滚动实现 |
| `RecordViewModel` | `SceneCol/ShotCol/TakeCol`、`FileNumberingService`（A 产出）、`IsLinked`、`IsRecording`、`AsrStatus`、`DescText`/`ShotNoteText`(TwoWay)、`PreviewHint`、`CurrentFileNumber` | `AdvanceTakeCommand(TakeType)`、`RewindTakeCommand`、`SetDesc/SetShotNote`、`ToggleLinkCommand`、`EditFileNumber/EditLinker/EditPrefix`；**订阅 IHardwareKeyService.KeyPressed**（VolumeUp→AdvanceTake(normal)+TakeCol.ScrollNext、VolumeDown→RewindTake） | 注入 FileNumberingService + ISessionSettingsStore + ITakeFlowService；**Scoped 生命周期**：每次进入记录页经工厂创建、退出释放、创建时从 ISessionSettingsStore 恢复 13 键 + 文件号 |
| `ScheduleViewModel` | `Scenes: ObservableCollection<SceneSchedule>`、选中索引 | `ImportCsv/AddScene/AddShot/EditItem/DeleteItem/MoveItem`（删后索引随动、至少 1 场 1 镜、undo 上一步） | 选择联动 RecordingSessionViewModel |
| `SlateLogPageViewModel` | `Dates`、`SelectedDate`、分组树 | `EditLog`、`ExportJson` | 当前场镜高亮取自会话 VM |
| `LogEditorViewModel` | 编辑副本 + 可用文件号（1..500 减已用） | `Save/Delete` | **保存/删除只经 ITakeFlowService.SaveEditAsync/DeleteItemAsync**（维护唯一写入口纪律） |
| `SettingsViewModel` | `ProjectName`、`TodayCount` | `ClearToday/ExportAll`（重置类：DialogService 确认） | |
| `MainViewModel` | `CurrentPage`、`Pages` | `NavigateCommand` | **Agent B 产出**（导航状态，避免 C 自造 VM） |

**命令参数规则（B-6）**：VM 命令一律用 Model 枚举（TkStatus/ShtStatus 等）；D 控件的 `DialOption`/`EditRequestedSection` 等 View 层类型**不得泄漏进 VM**。DialFAB 的 `DialOption.EnumValue`（object）→ TkStatus/ShtStatus 的转换由 **C 在 RecordView code-behind 映射表**完成（或 D 改为直接携带 Model 枚举——二选一，默认前者）。

**Scoped 激活钩子（B5）**：`RecordViewModel` 实现 `void Activate()` / `void Deactivate()`（进入记录页：订阅 IHardwareKeyService + hydrate 13 键与文件号；退出：取消订阅 + 释放）；**C 在 RecordView Loaded/Unloaded 调用**，防止音量键泄漏/漏订。

## 5. 控件契约（Agent D 产出；绑定协议 B/C 依赖）

| 控件 | 依赖属性（方向） | 事件 | 行为协议 |
|---|---|---|---|
| `SlateWheel` | `Items`(OneWay)、`SelectedIndex`(TwoWay)、`ItemHeight`(double,默认48)、`IsLoop`(bool,默认false) | `SelectedItemChanged(string)` | 手势拖拽**连续更新** SelectedIndex、松手**吸附**最近项；鼠标滚轮滚动；fling 惯性可选；`ScrollTo(index, animate)` 由 VM 调用，控件负责动画（200ms ease-in）；程序化滚动与手势共用同一状态通路 |
| `SlideConfirmBar` | `State`(Idle/Pressed/Armed, OneWay)、`IsRecording`、`RecordDuration`(string)、`Transcription`(OneWay)、`TextLeft`/`TextRight`(**TwoWay** ↔ DescText/ShotNoteText) | `SlideRight`、`SlideLeft` | 文本为**实时 TwoWay**（键入即回源）；滑动触发时对同属性做**幂等补提交**（不覆盖未保存输入）；水平拖动：右滑过 `slideLength` 阈值触发 SlideRight、左滑过 0 触发 SlideLeft；松手 200ms 回弹；背景按位置红→绿插值；**行为决策（Review 2）**：SlideRight/SlideLeft 触发时确认当前备注输入（幂等） |
| `DialFAB`（×2 实例） | `Options: IReadOnlyList<DialOption>`、`SelectedOption`(TwoWay) | `SelectionChanged(DialOption)`、`Opened/Closed` | `DialOption`（D 产出，仅显示数据：`Label/Icon/EnumValue: object`）；实例1：声音可(ok)/声音弃(bad)；实例2：画面保(ok)/画面过(nice)；选中后**背景色/图标随状态回显**（NotChecked=浅色/Ok=绿/Bad=红/Nice=金黄）；展开点外部关闭；接线 `SetOkTake/SetOkShot/ResetOkStatus` |
| `FileCounter` | `Prefix`/`Linker`/`NumberText`(string, TwoWay)、`NumberValue`(int 只读) | `EditRequested(EditRequestedSection)` | `EditRequestedSection { Prefix, Linker, Number }`（D 产出）；编辑对话框：Prefix=三模式 Toggle(Date/Sound Devices/Custom)+custom 文本；Linker=文本；Number=整型（不输入 0，显示 D3 补零）；**B7 三向同步归属 RecordViewModel**（RecordCount ↔ FileNumberingService.Number ↔ FileNumberingService.SetValue → 编辑文件号→ISessionSettingsStore 同步） |
| `TagChips` | `Tags: IReadOnlyList<string>` | `AddRequested/EditRequested(string)/DeleteRequested(string)` | 流式排列；Add/Edit 共用编辑对话框（TagEditingMessage 语义） |
| `ToastHost` | `Message`(OneWay) | — | 全局底部 Toast；IToastService 实现 |
| `LoadingOverlay` | `IsActive`(OneWay) | — | 全局覆盖层 |

## 6. 页面与导航契约（Agent C 产出）

- **App.axaml.cs 所有权归 C**（M0 只放占位并注明 C 接管）
- DI：Microsoft.Extensions.DependencyInjection；单例 Services + Session/SlateLog VM；Scoped VM 经工厂解析（记录页进入/退出）
- `MainWindow`：左侧导航（记录/计划/场记/设置）+ `ContentControl` ↔ `MainViewModel.CurrentPage`（**CurrentPage 为 ViewModel 实例**；VM→View 映射用 App.axaml 资源 `DataTemplate`（DataTemplate DataType={x:Type vm}）——C 负责映射表注册，B 只产 VM 类型）
- 页面：`RecordView` / `ScheduleView` / `SlateLogView` / `SettingsView`
- 对话框：`LogEditorWindow` / `NoteEditorWindow`（DialogService 统一注入 DataContext、Owner=MainWindow）
- 主题资源键（Themes/VoiSlatePalette.axaml）：`VoiSlate.Bg` / `VoiSlate.Primary`(bahamaBlue #0067A0) / `VoiSlate.OkGreen` / `VoiSlate.BadRed` / `VoiSlate.NiceGold` / `VoiSlate.TextHint`

## 7. 编译与测试基线

- `TargetFramework`：`net10.0`；Android 目标独立验证
- **测试策略（C-12）**：集中单测试项目 `tests/VoiSlate.Tests`，固定子目录 `Tests/{Models,Services,ViewModels,Controls,Infrastructure,TestDoubles}`；每 Agent **只新增文件不改既有文件**（SDK glob 免改 csproj）；共享 `Directory.Build.props`（M0 提供，任何 Agent 禁改）；**TestDoubles**（P0.5 产 FakeLogRepository/FakePickerHistoryStore 等）供 B 复用；CI 只在主分支/合并后跑
- 全部 Services/纯逻辑可单测（xUnit）；ITimeProvider 注入保证确定性
- Git：worktree 分支 `agent-a-models` / `agent-e-services` / `agent-b-viewmodels` / `agent-d-controls` / `agent-c-views`；**main 冻结（仅 docs 契约提交）**；合并序 A→E→B→D→C，**D 可提前合入**（A 合入后即可）

---
*v0.3 变更（闭环 Review 3）：JSON 枚举**必须** `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`（A#16）；ITakeFlowService 增 SaveEditAsync/DeleteItemAsync 并定为日志编辑唯一入口（B1）；DialFAB EnumValue→枚举转换归属 C（B2）；CurrentPage 为 VM + DataTemplate 映射（B3）；SlideConfirmBar 实时 TwoWay + 幂等补提交（B4）；RecordViewModel.Activate/Deactivate 激活钩子（B5）。*