# VoiSlate Avalonia 模块契约规范（contracts.md）

> 版本：v0.1（草稿，随 migration-plan.md 五轮 review 同步演进）
> 用途：第三阶段并行开发时，各 Agent 严格按本契约实现，保证模块边界清晰、零冲突合并。

---

## 1. 命名约定

| 项 | 规则 |
|---|---|
| 命名空间 | `VoiSlate.Models` / `VoiSlate.Services` / `VoiSlate.ViewModels` / `VoiSlate.Views` / `VoiSlate.Controls` / `VoiSlate.Infrastructure` |
| 枚举命名 | PascalCase 成员；`TkStatus { NotChecked, Ok, Bad }`、`ShtStatus { NotChecked, Ok, Nice }`——**显式数值 0/1/2**（对齐 Hive/JSON 兼容） |
| 事件 | `event EventHandler<T>`；命名 `{Thing}Changed` |
| 命令 | `RelayCommand`；命名 `{Action}Command` |
| JSON | camelCase 属性名 + 枚举字符串转换（`JsonStringEnumConverter`） |

## 2. Models 契约（Agent A 产出，其余 Agent 只读）

- `SlateLogItem`：`Scn`/`Sht`/`Tk`(int)/`FilenamePrefix`/`FilenameLinker`/`FilenameNum`/`TkNote`/`ShtNote`/`ScnNote`/`OkTk`/`OkSht`；计算属性 `FileName`（Prefix+Linker+Num.ToString("D3")）
- `TkStatus`/`ShtStatus` 见上；`TakeType { Normal, Fake, End, Wild }`
- `ScheduleItem`：`Key`(数字串)/`Fix`(字母)/`Name`(计算=Key+Fix，setter 同步重算)/`Note`
- `Note`：`Objects: List<string>`/`Type`(位置或景别)/`Append`
- `DataList<T>`：`Items` 集合 + `Add/Insert/Update/Remove`（重复检测抛 `DuplicateItemException`）——**修复原缺陷**：显式"先查重再赋值"，不使用引用比较
- `SceneSchedule : DataList<ScheduleItem>`：`Info`(ScheduleItem) + 索引器
- `RecordFileNum`（模型） + 命名规则抽到 `IFileNamingService`（见 §3）
- `TkPending`/`TagEditingMessage`：按原语义

## 3. Services 契约（Agent E 产出）

| 接口 | 关键成员 | Mock 策略 |
|---|---|---|
| `IAsrService` | `Task StartAsync()`, `Task StopAsync()`, `event Action<string>? ResultReceived`, `event Action<string>? StatusChanged`, `bool IsListening` | `MockAsrService`：Start 1.2s 后触发 ResultReceived("示例转写：场记语音识别结果") |
| `IRecordingService` | `Task<bool> RequestPermissionAsync()`, `Task StartAsync()`, `Task StopAsync()`, `event Action<double>? LevelChanged` | `MockRecordingService`：定时推模拟电平 |
| `IFileNamingService` | `string GetPrefix(RecorderType, DateTime)`, `string FormatFileName(prefix, linker, number)` | 静态规则（B6） |
| `ISessionSettingsStore` | 13 键读写：`GetInt/GetBool/GetString/GetEnum<T>/Set...`, `Date`, `RecordCount` 等 | LiteDB 实现 |
| `ILogRepository` | `Task<IReadOnlyList<SlateLogItem>> GetByDateAsync(date)`, `AppendAsync(prevFileName, item)`, `RemoveLastAsync()`, `RemoveAtAsync(i)`, `RemoveByFileNameAsync(key)`, `PutAtAsync(i,item)`, `ClearAsync()`, `GetDatesAsync()`, `DeleteDayAsync(date)` | LiteDB `slate_logs` 集合 |
| `ScheduleStore` | `LoadAllAsync()`, `SaveAllAsync(list)`, `ClearAsync()` | LiteDB `scenes` |
| `IPickerHistoryStore` | `Last()`, `Append(entry)`, `RemoveLast()`, `Clear()` | 内存+LiteDB 备份 |
| `CsvScheduleParser` | `Task<IReadOnlyList<SceneSchedule>> ParseAsync(Stream)` | 纯函数（CsvHelper，7 列） |
| `MicObjectsExtractor` | `static (string Body, IReadOnlyList<string> Tracks) Extract(string shtNote)` | 协议 `<obj/>` |
| `IBackupService` | `Task BackupAsync(CancellationToken)`, 启动 3 分钟 PeriodicTimer | JSON `Documents/VoiSlate Logs/` |
| `ITimeProvider` | `DateTime Today`, `string TodayStamp`(yymmdd), `string SoundDevicesStamp`(yyYmMd) | 可注入固定时间（单测） |
| `IToastService` | `void Show(string)` | Toast 控件实现 |
| `IHapticsService` | `void Pulse(int ms)` | no-op |
| `IHardwareKeyService` | `event Action<HardwareKey>? KeyPressed` | 未来 Android 接入；桌面 no-op |
| `IDayRolloverService` | `bool IsNewDay()`, `Task OnRolloverAsync()` | recordCount=1 + 清 history |
| `IExportService` | `string SerializeLogs(IEnumerable<SlateLogItem>)`（camelCase JSON） | 兼容旧格式 |

**建议引包**：LiteDB、CsvHelper、System.Text.Json(BCL)、Serilog、CommunityToolkit.Mvvm、Microsoft.Extensions.DependencyInjection。

## 4. ViewModels 契约（Agent B 产出，依赖 §2/§3）

| VM | 关键可观察成员 | 命令 |
|---|---|---|
| `RecordingSessionViewModel`（DI 单例） | `SelectedSceneIndex/SelectedShotIndex/SelectedTakeIndex`(int)、`IsLinked`(bool)、`RecordCount`(int)、`RecordLinker`/`PrefixType`/`CustomPrefix`/`CurrentDesc`/`CurrentNote`(string)、`OkTk`/`OkSht`(enum)、`Date`(只读)、`PendingTakeOk`/`PendingShotOk` | `SelectScene/SelectShot/SelectTake/SetRecordCount/SetLink/SetOkTake/SetOkShot/ResetOkStatus` |
| `SlateLogViewModel` | `TodayLogs: ObservableCollection<SlateLogItem>`、`Dates: ObservableCollection<string>`、`Today` | `Add(prevFileName,item)`、`RemoveLast/RemoveAt`、`Clear`、`SaveEdit` |
| `SlateColumnViewModel`（每列实例） | `Items: IReadOnlyList<string>`、`SelectedIndex`(int TwoWay)、`SelectedItem`(计算) | `ScrollTo/ScrollNext/ScrollPrev`（含边界） |
| `RecordViewModel`（页面编排） | `SceneCol/ShotCol/TakeCol: SlateColumnViewModel`、`FileNumber: FileNumberingService`、`IsLinked`、`IsRecording`、`AsrStatus`、`DescText`/`ShotNoteText`、`PreviewHint` | `AdvanceTakeCommand(TakeType)`、`RewindTakeCommand`、`SetDesc/SetShotNote`、`ToggleLinkCommand`、`EditFileNumber/EditLinker/EditPrefix` |
| `ScheduleViewModel` | `Scenes: ObservableCollection<SceneSchedule>`、选中索引 | `ImportCsv/AddScene/AddShot/EditItem/DeleteItem/MoveItem` |
| `SlateLogPageViewModel` | `Dates`、`SelectedDate`、分组树 | `EditLog(LogEditorViewModel)`、`ExportJson` |
| `LogEditorViewModel` | 编辑中的 `SlateLogItem` 副本 + 可用文件号列表 | `Save/Delete` |
| `SettingsViewModel` | `ProjectName`、`TodayCount` | `ClearToday/ExportAll`（重置类：确认对话框） |

## 5. 控件契约（Agent D 产出；绑定协议 B/C 依赖）

| 控件 | 依赖属性（方向） | 事件 | 说明 |
|---|---|---|---|
| `SlateWheel` | `Items`(OneWay)、`SelectedIndex`(TwoWay) | `SelectedItemChanged` | 自绘滚轮：指针拖拽 + 鼠标滚轮 + 选中缩放/高亮 |
| `SlideConfirmBar` | `State`(Idle/Pressed/Armed→OneWay)、`IsRecording`、`RecordDuration`(string)、`Transcription` | `SlideLeft`、`SlideRight` | 水平滑动确认条，红→绿渐变背景 |
| `DialFAB` | `Options: IReadOnlyList<DialOption>` | `SelectionChanged(DialOption)` | 弹出式评价按钮（声音可/弃、画面保/过） |
| `FileCounter` | `Prefix`/`Linker`/`Number`(string, TwoWay) | `EditRequested(section)` | 三卡片 + 长按编辑 |
| `TagChips` | `Tags: IReadOnlyList<string>` | `AddRequested/EditRequested(tag)/DeleteRequested(tag)` | 标签流式排列 |
| `ToastHost` | `Message`(OneWay) | — | 全局底部 Toast |
| `LoadingOverlay` | `IsActive`(OneWay) | — | 全局覆盖层 |

## 6. 页面与导航契约（Agent C 产出）

- `MainWindow`：左侧导航（记录/计划/场记/设置），`ContentControl` 承载当前页 → `MainViewModel.CurrentPage`
- 页面：`RecordView` / `ScheduleView` / `SlateLogView` / `SettingsView`（UserControl）
- 对话框：`LogEditorWindow` / `NoteEditorWindow`（Owner=MainWindow，DialogService 统一打开）
- 主题资源键（Themes/VoiSlatePalette.axaml）：`VoiSlate.Bg` / `VoiSlate.Primary`(bahamaBlue #0067A0) / `VoiSlate.OkGreen` / `VoiSlate.BadRed` / `VoiSlate.NiceGold` / `VoiSlate.TextHint`
- App.axaml.cs：DI 容器（Microsoft.Extensions.DependencyInjection）：单例 Services + 单例 Session/SlateLog VM、Scoped Record/Schedule/Settings VM

## 7. 编译与质量基线

- `TargetFramework`：`net10.0`（主）/ Android 目标由独立 csproj 后续验证
- `TreatWarningsAsErrors` 不强制开启，但 `dotnet build` 无显著 warning（默认阈值）
- 全部 Services 与纯逻辑可单测（xUnit）；`ITimeProvider` 注入保证确定性
- Git：每个 worktree 独立分支（`agent-a-models` 等），模块完成即 commit；合并顺序 A→E→B→D→C

---
*本契约 v0.1 与 migration-plan.md 同步演进；五轮 review 中如有契约变更，主 Agent 更新本文件并通知对应 Agent。*