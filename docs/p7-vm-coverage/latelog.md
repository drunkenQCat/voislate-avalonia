# SlateLogViewModel / SlateLogPageViewModel 覆盖度对照

**审计对象（待判定，均无视图消费者）**

- `src/VoiSlate/ViewModels/SlateLogViewModel.cs`（203 行）
- `src/VoiSlate/ViewModels/SlateLogPageViewModel.cs`（215 行）

**现役实现（对照基准）**

- `src/VoiSlate/Desktop/DesktopWorkspaceService.cs`（`QueryLogs` :212、`FindLogIndex` :238、`EditLogAsync` :245、`DeleteLogAsync` :251、`ClearLogsAsync` :252）
- `src/VoiSlate/Desktop/WorkspaceModels.cs`（`WorkspaceLog` :35、`WorkspaceGroup` :43、`LogFilter` :45）
- `src/VoiSlate/Desktop/DesktopWorkspaceView.cs`（时间轴页 `BuildTimeline` :371、`RenderTimelineContents` :389、`ShowLogEditor` :574）
- `src/VoiSlate/Desktop/DesktopWorkspaceEditing.cs`（`MatchesShot` :19、`EditLogDetailsAsync` :58）
- `src/VoiSlate/Services/IExportService.cs`（`ExportService` :22-31）

**判定口径**：`覆盖` = 现役有等价能力；`部分覆盖` = 现役有近似实现但有可指出的差异；`缺失` = 现役完全无对应物。

**旁证（本轮已确认）**：桌面启动只创建 `Desktop.DesktopWorkspaceWindow`（`src/VoiSlate/App.axaml.cs:78`），从不解析 `MainViewModel`；`SlateLogViewModel` 的唯一解析点是 `MainViewModel` 的构造函数（`src/VoiSlate/ViewModels/MainViewModel.cs:44`，作为"场记"页 `Instance` :51）。因此**两个 VM 在运行时都不会被构造**。`SlateLogPageViewModel` 连 DI 注册都没有（`src/VoiSlate/App.axaml.cs:202-207` 只注册了 `SlateLogViewModel`）。

---

## SlateLogViewModel 逐方法对照

| VM 成员 | 作用（一句话） | 判定 | 现役对应物 / 差异 |
|---|---|---|---|
| `LogSceneGroup` / `LogShotGroup`（`src/VoiSlate/ViewModels/SlateLogViewModel.cs:10-35`） | 两级分组的节点类型：`Title`/`Tag`(分组键)/`Items`//`Shots`/`IsOpen`{get;set;} | 部分覆盖 | `WorkspaceGroup(string Date, string Scene, string Shot, IReadOnlyList<WorkspaceLog> Rows)`（`src/VoiSlate/Desktop/WorkspaceModels.cs:43`）**是扁平单层 record**：无 `IsOpen`、无 `Title` 文案、无子集合。视图 `RenderTimelineContents` 把"日期标题 → 组头 → 条目卡"平铺输出（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:414-438`），**完全没有折叠**。差异：分组层级（VM 两层嵌套 ↔ 服务一层扁平，靠 `Date/Scene/Shot` 三元键表达）、默认展开策略、分组身份键（VM 用 `Tag` ↔ 服务靠三元组字符串 `row.Date+"/"+h.Scn+"/"+h.Sht`，见 `DesktopWorkspaceService.cs:219`） |
| 构造函数 `SlateLogViewModel(ITakeFlowService, ILogRepository, ITimeProvider, IExportService)`（同文件 `:50-59`） | 注入 4 依赖，初始化 `Today`/`SelectedDate`，订阅 `ITakeFlowService.LogsChanged` | 覆盖 | 服务主构造 `DesktopWorkspaceService(IScheduleStore, ILogRepository, …)`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:12-14`）+ `Changed` 事件（同文件 `:23`，`Change()` 末尾 `Changed?.Invoke()` :109），视图订阅于 `src/VoiSlate/Desktop/DesktopWorkspaceView.cs:48`。差异：VM 靠 `ITakeFlowService.LogsChanged`（`src/VoiSlate/Services/TakeFlowService.cs:51`）间接感知；服务是日志写入的唯一执行者，自己广播 |
| `Today`（`:69-70`） | 今日键（`yyMMdd`），跨天由调用方 `LoadAsync` 补偿 | 覆盖 | `public string Today => VoiSlateDates.TodayKey(clock.Now);`（`DesktopWorkspaceService.cs:24`）+ `EnsureDate()` 在写盘时切日（同文件 `:70-76`）。差异：服务每次读 `clock.Now`，无"缓存键 + 补偿刷新"这套 |
| `SelectedDate` / `OnSelectedDateChanged`（`:73-76`） | 选中日期变更即触发 `RefreshAsync` | 覆盖 | 视图字段 `private string? _logDate`（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:31`，初值 `_service.Today` :43），日期 Tab 点击后 `_logDate = date; RenderTimelineContents()`（同文件 `:400`）。差异：VM 无"全部日期"态（`SelectedDate` 是 `string` 非空语义，`RefreshAsync:104` 用 `string.IsNullOrEmpty` 兜底到今日）；视图是 `string?`，`null` 表示全部日期（`:396`） |
| `Dates`（`:63`） | 日期 Tab 数据源，来自 `ILogRepository.GetDatesAsync()` | 覆盖 | 视图自算 Tab：`_service.Logs.Select(l => l.Date).Append(_service.Today).Distinct().OrderDescending(StringComparer.Ordinal)`（`DesktopWorkspaceView.cs:397`），并带当日条数（`:399-400`）。差异：仓库返回升序（`src/VoiSlate/Services/ILogRepository.cs:115`），视图倒序 + 补今日 + 计数；VM 只做原样透传，无计数、无"今天"字样 |
| `TodayLogs`（`:61`） | 当前选中日期的条目集合（保持仓库插入序） | 覆盖 | `_service.Logs`（`List<WorkspaceLog>`，`DesktopWorkspaceService.cs:22`）+ `QueryLogs(date: …)`（`:212`）按行过滤。行对象 `WorkspaceLog` 带展示派生属性 `TakeLabel`/`PictureLabel`/`SoundLabel`/`TimeLabel`（`WorkspaceModels.cs:37-40`），比裸 `SlateLogItem` 更适合直接绑定 |
| `Groups`（`:66`） | 当前日期的场/镜两级分组树 | 部分覆盖 | `QueryLogs` 的返回值（`:212-236`）是 `WorkspaceGroup` 扁平列表，视图自己再插日期分隔标题（`DesktopWorkspaceView.cs:416`）。差异：**层级不同**（VM：场组→镜组→条目；服务：三元组组→条目），且服务跨日期一次返回、VM 只单日 |
| `RebuildGroups()`（`:123-151`） | 按条目 `Scn`/`Sht` 重建两级分组；组标题按组内序号 `"Scene n"`/`"Shot n"`（`:136`、`:145`）；默认展开第 1 场组、每场第 1 镜组（`:136` `IsOpen=!firstSceneExpanded`、`:145` `IsOpen=shtOrdinal==1`） | 部分覆盖 | 服务分组键连续判定 `row.Date + "/" + h.Scn + "/" + h.Sht`（`DesktopWorkspaceService.cs:219`），组标题用**真实场镜名**：`$"◉  {group.Scene} 场  /  {group.Shot} 镜"`（`DesktopWorkspaceView.cs:421`）。差异：① VM 把 `"1A"/"1B"` 重编号成 `"Scene 1/2"`（序数），服务保留原值；② 排序规则不同（见 PageVM 节：服务按拍摄顺序，VM 按遍历序）；③ 折叠/默认展开整层不存在；④ VM 的同名场镜在不同场组会重复编号（`shtOrdinal` 每场归零 :139，但 `scnOrdinal` 与真实 `Scn` 脱钩，跨日切换时序号会跳） |
| `OnLogsChanged()`（`:78`） | 订阅回调 → 无条件 `_ = RefreshAsync()`（fire-and-forget） | 覆盖 | 服务 `Change()` 内 `ReloadLogs()` + `Changed?.Invoke()`（`DesktopWorkspaceService.cs:78-83`、`:90-110`），视图 `OnChanged` 重渲染（`DesktopWorkspaceView.cs:48`）。差异：VM 是无条件全量刷新（含切日列表）；视图在桌面端同一时刻只有一个时间轴页，等价 |
| `LoadAsync()`（`:81-85`） | 重算 `Today` 后整体刷新（跨天补偿入口） | 覆盖 | `InitializeAsync()` 内 `EnsureDate()` + `ReloadLogs()`（`DesktopWorkspaceService.cs:42-64`、`:70-83`）。差异：服务把"跨天"绑在写盘时机，VM 需调用方在页面激活时调用 |
| `RefreshAsync()`（`:87-120`，含 `_refreshing` 重入保护 `:48`、`:89-94`） | 日期列表 + 当日条目 + 重建分组；`_refreshing` 防并发重入 | 部分覆盖 | 视图 `RenderTimelineContents()`（`DesktopWorkspaceView.cs:389-443`）每次全量 `Clear()` 重建，天然串行（UI 线程同步）；无重入保护概念，也无异步竞态（数据来自已加载的 `_service.Logs` 内存列表，不是每次打仓库）。差异：VM 每次刷新两次仓库 I/O（`GetDatesAsync` :97 + `GetByDateAsync` :105）；服务只在写入后 reload 一次全量 |
| `EditRequested` 事件（`:154`） | 把构造好的 `LogEditorViewModel` 上抛给 View 托管窗口 | 缺失 | 桌面端无事件式编辑契约：视图直接 `ShowLogEditor(row)`（`DesktopWorkspaceView.cs:574-596`）内联建对话框，保存走 `EditLogDetailsAsync`（`DesktopWorkspaceEditing.cs:58`）。差异：VM 的"VM 构造子编辑器 + 事件上抛"分层在桌面端被合并成 UI 层直连服务。**若还有别的宿主（Android/Flutter 桥）依赖这个事件签名，删除会断掉约定** |
| `RequestEdit(SlateLogItem)`（`:157-168`） | 日期守卫（仅 `listDate == Today` 才放行，否则静默 return `:161-164`）；按 `TodayLogs.IndexOf` 定位；用 `FilenameNum` 列表构造 `LogEditorViewModel` | 部分覆盖 | `EditLogDetailsAsync(WorkspaceLog row, int take, int file, string sceneNote, string tracks, string recording, string shot, ShtStatus, TkStatus)`（`DesktopWorkspaceEditing.cs:58`），按 `row.Date` 定位索引（`FindLogIndex` `DesktopWorkspaceService.cs:238-243`）→ **支持任意日期编辑**。差异：① 日期守卫被放宽（桌面可编辑历史日）；② VM 只传 position 索引（依赖"VM 内列表序 == 仓库序"），服务按 `Id` 查找更稳；③ VM 静默失败（`return` 无提示），服务抛 `InvalidOperationException("这条场记已被删除")`（`:241`）并经 `Toast(ex.Message,true)` 呈现 |
| `Delete(SlateLogItem?)` 命令（`:171-186`） | 直接经 `ILogRepository.RemoveAtAsync(选中日, 索引)` 删除并刷新（注释 `:170` 自陈"跨日维护属存储级操作"） | 覆盖 | `DeleteLogAsync(WorkspaceLog row)`（`DesktopWorkspaceService.cs:251`）：`FindLogIndex` + `RemoveAtAsync` + `_undo.Clear()`，包在 `Change()` 事务里。差异：VM **绕过 `ITakeFlowService`**（与本类头部契约注释 `:37-41`"无公开持久化写 API"自相矛盾）；服务版会清撤销栈、触发 `Changed` 广播 |
| `Export` 命令（`:189-200`） | 遍历所有日期合并成**一个裸 `SlateLogItem[]`**，`SerializeLogs` 后写 `"slatelog-all.json"` | 部分覆盖 | 现役导出是全量**快照对象**：`ExportJson()`（`src/VoiSlate/Desktop/DesktopWorkspaceBackup.cs:27`）→ `Snapshot()`（同文件 `:17`，含 `Version/Project/ExportedAt/Plan/Days/Session`），视图用系统文件选择器写 `VoiSlate-{Today}.json`（`DesktopWorkspaceView.cs:633-644`）。差异：① 载荷结构不同（裸数组 ↔ 备份对象）；② 文件名/落盘通道不同（`IExportService` ↔ `SaveFilePickerAsync`）；③ 服务额外含计划与草稿；④ **跨日合并这点两者都有**（`Snapshot` 的 `Days` 覆盖全部拍摄日）；⑤ 若下游 Flutter 迁移脚本吃"裸数组"，现役另有 `导出 Flutter 兼容场记 JSON`（`src/VoiSlate/Desktop/DesktopWorkspaceBackupUi.cs:71`）覆盖同一用途 |
| `Dispose()`（`:202`） | 退订 `LogsChanged` | 覆盖 | 视图 `DetachedFromVisualTree` 中 `_service.Changed -= OnChanged`（`DesktopWorkspaceView.cs:49`）。差异：无（服务是单例长生命，退订只为防幽灵刷新） |

> **附带发现（缺陷）**：`Export()` 第 `:199` 行传 `string.Empty` 作目录，而 `ExportService.SaveToFileAsync` 第一句就是 `Directory.CreateDirectory(dir)`（`src/VoiSlate/Services/IExportService.cs:29`）。本机 .NET 10 实测：`Directory.CreateDirectory("")` 抛 `ArgumentException: The value cannot be an empty string. (Parameter 'path')`。即**这两个 VM（以及 `SettingsViewModel.ExportAll` 第 `:160` 行的同款写法）的导出即使接线也必然失败**——测试里用的是 Spy 桩（`tests/VoiSlate.Tests/.../SlateLogPageViewModelTests.cs:108` 断言 `"[]"` 内容），把这条真实缺陷盖住了。这对"直接删除是否有行为损失"是有利证据：该导出路径从未在真机跑通。

---

## SlateLogPageViewModel 逐方法对照

| VM 成员 | 作用（一句话） | 判定 | 现役对应物 / 差异 |
|---|---|---|---|
| `SlateLogSceneGroup` / `SlateLogShotGroup`（`src/VoiSlate/ViewModels/SlateLogPageViewModel.cs:10-23`） | 场节点（`Scn` + `Shots`）/ 镜节点（`Sht` + `Items`），**无 `IsOpen`** | 部分覆盖 | `WorkspaceGroup`（`WorkspaceModels.cs:43`）。差异：与 `LogSceneGroup` 一样是层级差；但本类型连折叠属性都没有，比 `LogSceneGroup` 更弱（说明本 VM 的"树"是纯数据结构，折叠交给别的层） |
| 构造函数（`:42-60`） | 6 依赖注入 + 双订阅（`LogsChanged` `:57`、`session.PropertyChanged` `:58`）+ 立即 `UpdateHighlight()` `:59` | 部分覆盖 | 服务只注入存储/设置/时钟（`DesktopWorkspaceService.cs:12-14`），当前场镜由 `State.Scene`/`State.Shot`（`WorkspaceModels.cs:21-22`）与 `Scene`/`Shot`（`DesktopWorkspaceService.cs:25-26`）承担。差异：`RecordingSessionViewModel` + `IScheduleBook` 这组依赖在桌面端**无消费方**（见下方"两套 VM 关系"），属于 P7 期"会话驱动高亮"架构 |
| `Today`（`:70-71`） | 今日键 | 覆盖 | `DesktopWorkspaceService.cs:24` |
| `Dates`（`:62`） | 日期 Tab（倒序＝新日期在前，`:26` 注释） | 覆盖 | 视图 `OrderDescending`（`DesktopWorkspaceView.cs:397`）。差异：VM 自身未排序，只是把仓库升序结果原样装（靠注释声称倒序，实际依赖调用方/仓库实现），视图显式倒序 |
| `SelectedDate` + 默认选中最近日期（`:67-68`、`:100-103`）+ `OnSelectedDateChanged`（`:113-120`，`_loading` 抑制重入 `:40/:89/:107/:116`） | 首次加载默认选 `Dates[^1]`（最新一天，不是今日）；换日即刷新；`_loading` 防止加载期重复刷新 | 覆盖 | 视图 `_logDate = _service.Today`（`DesktopWorkspaceView.cs:43`）+ 日期 Tab（`:396-400`）。差异：**默认策略不同**——VM 默认"最近有记录的日期"，视图默认"今天"；视图多一个"全部日期"Tab |
| `CanEditSelectedDate`（`:82`） | 选中日 == 今日才可编辑（P0.5 服务限制） | 缺失 | 现役**没有这个限制**：`EditLogDetailsAsync` 按 `row.Date` 定位（`DesktopWorkspaceEditing.cs:58` + `FindLogIndex`），历史日照样可改。属"VM 保留了旧约束，现役已放宽"，即**能力未被继承但已过期**，不构成迁移理由 |
| `Groups`（`:64`）+ `RefreshSelectedAsync()`（`:123-153`） | 拉当日条目存 `_currentLogs`（保持插入序 `:130-132`），再用 `GroupBy(Scn).OrderBy(g=>g.Key)`（`:135`）、`GroupBy(Sht).OrderBy(g=>g.Key)`（`:138`）**按字符串字典序**重建场/镜树 | 部分覆盖 | `QueryLogs` 遍历 `Logs`（`Logs` 由 `ReloadLogs` 按日期升序 + 日内插入序填充，`DesktopWorkspaceService.cs:78-83`）**保持拍摄顺序**，不做任何重排；视图文案明确 `"按拍摄顺序"`（`DesktopWorkspaceView.cs:411`）。差异（**行为冲突**）：VM 会按 `Scn`/`Sht` 字典序重排（`"10A"` 排在 `"2A"` 之前），而现役按拍摄顺序；若迁移过去会改变用户可见顺序，反之从 VM 删掉则**排序规则无损失**（现役更符合场记习惯） |
| `_currentLogs`（`:39`、`:130-132`、`:174`、`:180`） | 当日条目缓存，兼作编辑定位与文件号候选来源 | 覆盖 | 服务的 `Logs`（全日期，`DesktopWorkspaceService.cs:22`）+ `FindLogIndex`（`:238-243`，按 `Id` 在当日集合里找位置）。差异：服务按 `Id` 定位更抗并发/重排；VM 靠引用/`Id` 混合定位（`:174` `ReferenceEquals(i,item) || i.Id == item.Id`） |
| `OnLogsChanged()`（`:155-162`） | 仅当 `SelectedDate == Today` 才刷新（避免历史日被无谓刷新） | 覆盖 | 视图 `_service.Changed` → 全量渲染（`DesktopWorkspaceView.cs:48`），服务每次写入后 `ReloadLogs`（全部日期）。差异：VM 的"今日守卫"是省 I/O 的优化；服务全量重载更慢但语义更强（撤销/清空也会广播） |
| `LoadAsync()`（`:87-111`） | 日期列表 + 默认选中 + 首次刷新；`_loading` 包裹 | 覆盖 | `InitializeAsync` + `ReloadLogs`（`DesktopWorkspaceService.cs:42-83`）。差异：VM 有"首次自动选最新日期"这一步，视图固定选今天 |
| `CreateLogEditor(SlateLogItem)`（`:167-182`） | 非今日返回 `null`（`:169-172`）；`_currentLogs` 中定位索引；用全部 `FilenameNum` 构造 `LogEditorViewModel`（`:180-181`） | 部分覆盖 | `ShowLogEditor(row)`（`DesktopWorkspaceView.cs:574-596`）建对话框：文件序号 `NumericUpDown { Minimum=1, Maximum=int.MaxValue-1 }`（`:578`）、次号 `0..9999`（`:577`）、场信息、人物/轨道、录音/镜头标注、画/声下拉，保存走 `EditLogDetailsAsync`。差异：① **文件序号候选集丢失**——VM 侧 `LogEditorViewModel` 会用 `used` 去重并让当前值可选（`src/VoiSlate/ViewModels/LogEditorViewModel.cs:50`，`AvailableFileNumbers` :68），桌面端是无上限裸数字框，可填任意值（含已占用号）；② VM 禁止历史日编辑，桌面允许；③ 桌面端多出 `Tracks` 逗号解析（`SetTracksAsync`，`DesktopWorkspaceEditing.cs:15`）与 `ScnNote` 编辑 |
| `ExportJson` 命令（`:186-191`） | 只导出**当前选中日**的裸数组，文件名 `$"{SelectedDate}.json"` | 部分覆盖 | 现役只有两个导出粒度：全量快照 `ExportJson()`（`DesktopWorkspaceBackup.cs:27`）与"导出 Flutter 兼容场记"（`DesktopWorkspaceBackupUi.cs:71`），**都是全量/跨日**。差异（**真缺口**）：**按单日导出**在现役时间轴上不存在——界面有单日 Tab 与 `_logDate` 过滤（`DesktopWorkspaceView.cs:396-400`），但 `Export()`（`:633-644`）永远导全部。另：`:190` 的 `SaveToFileAsync(string.Empty, …)` 同前必抛 |
| `CurrentScene` / `CurrentShot`（`:74-79`） | 当前场/镜名（用于在树里高亮） | 部分覆盖 | 服务有 `Scene`/`Shot`（`DesktopWorkspaceService.cs:25-26`）+ `State.Scene/State.Shot`（`WorkspaceModels.cs:21-22`），但视图**不做分组高亮**；只在无筛选时追加一条静态提示条 `"● 当前拍摄 · {State.Scene} 场 / {State.Shot} 镜"`（`DesktopWorkspaceView.cs:441-442`）。差异：VM 的高亮会随分组树重算（可在对应场/镜组上打标记），现役只有一处文字提示、且条件苛刻（无日期筛选、无场镜筛选、搜索为空、`LogFilter.All`、`Shot is not null`） |
| `OnSessionPropertyChanged`（`:195-202`） | 监听 `RecordingSessionViewModel.SelectedSceneIndex/SelectedShotIndex` 变化 → `UpdateHighlight()` | 部分覆盖 | 现役无会话 VM 参与；当前场镜变化由服务 `SelectShotAsync`/`NextShotAsync`（`DesktopWorkspaceService.cs:188-204`）改 `State` 并经 `Changed` 广播，视图整页重绘。差异：**联动机制不同**（属性通知 ↔ 事件 + 全量重绘），能力等价但 VM 的路由依赖一个桌面端不存在的对象图 |
| `UpdateHighlight()`（`:204-208`） | `_scheduleBook.SceneLabel(SelectedSceneIndex)` / `ShotLabel(...)` 把索引翻成场镜名 | 部分覆盖 | 桌面端直接持有计划对象：`Scene`/`Shot`（`:25-26`）、`FindPlan(row)`（`DesktopWorkspaceEditing.cs:22-27`）、`MatchesShot`（`:19-25`）。差异：`IScheduleBook.SceneLabel/ShotLabel`（`src/VoiSlate/Services/IScheduleBook.cs`）这套"索引→标签"抽象在桌面路径**无消费者**——删除本 VM 后 `IScheduleBook` 只剩 DI 注册（`App.axaml.cs:161`）与 `RecordViewModel` 的标签 provider 用途（`App.axaml.cs:177`） |
| `Dispose()`（`:210-214`） | 退订 `LogsChanged` + `session.PropertyChanged` | 覆盖 | `DesktopWorkspaceView.cs:49` 退订 `Changed`。差异：无 |

### 两套 VM 的关系（完整回答）

**是同一个页面的两套实现，且是一次重构的前后两版**：`SlateLogPageViewModel`（日期 Tab → 场/镜树 + 高亮 + 按日导出 + `CanEditSelectedDate` 守卫，6 个依赖）是更接近原版 Flutter `slate_log_page.dart` 的"页面级"实现；`SlateLogViewModel`（"今日"单日数据面 + 两级分组 + 删除 + 跨日合并导出 + `EditRequested` 事件，4 个依赖）是随后按"VM 只管数据、View 管窗口"分层重写的"扁平数据面"。证据：两者都订阅 `ITakeFlowService.LogsChanged`、都用 `ILogRepository`、都出 `Groups` + `Dates`，且 `SlateLogViewModel` 的类注释自称"契约 §4 SlateLogViewModel；DI 单例"（`:37-41`），`Pages` 表里占的是同一个 `"slatelog"` 键（`MainViewModel.cs:36`、`:51`）。

**哪套更完整**：各有胜场，不能简单说一套包含另一套。
`SlateLogPageViewModel` 独占：① 当前场/镜**高亮**与会话联动（`:74-79`、`:195-208`）；② **默认选中最近日期**而非今日（`:100-103`）；③ `CanEditSelectedDate` 日期守卫（`:82`，已被现役放宽，属过期能力）；④ **按单日导出**（`:186-191`）。
`SlateLogViewModel` 独占：① **折叠 + 默认展开策略**（`LogSceneGroup.IsOpen` `:34`、`:136`/`:145`）；② **删除命令**（`:171-186`）；③ **`EditRequested` 事件**（`:154`）——把编辑器构造留在 VM、窗口托管交给 View 的分层；④ `_refreshing` 重入保护（`:48`、`:89-94`）；⑤ 组标题序数重编号 `"Scene n"/"Shot n"`（`:136`/`:145`）。

**`SlateLogPageViewModel` 有没有 `SlateLogViewModel` 和 service 都没有的能力？** 有，恰好三处，其中一处是真缺口：
1. **按单日导出**（`:186-191`）——service 只有全量导出，UI 也只有全量按钮（`DesktopWorkspaceView.cs:633`）。**唯一现役缺失能力**。
2. **场/镜高亮的"分组级"呈现**（`:74-79` + `:204-208`）——service 有当前场镜字段，但时间轴不把高亮落到分组树上（只有一条静态提示条 `:441-442`）。
3. **"可编辑日期"守卫**（`:82`）——两处都没有，但这是旧约束，随服务放宽应作废，不算需要保留的能力。

---

## 删除会丢掉的东西

逐条列出"删掉这两个文件后，代码库里就再也找不到"的具体能力（`文件:行号`）：

1. **两级分组 + 折叠/默认展开策略**：`LogSceneGroup.IsOpen` / `LogShotGroup.IsOpen`（`src/VoiSlate/ViewModels/SlateLogViewModel.cs:19-20`、`:33-34`）与"展开第 1 场组 + 每场第 1 镜组"的初值逻辑（`:136`、`:145`）。现役时间轴全展开、无折叠控件（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:414-438`）。
2. **组标题的序数重编号**：`$"Scene {++scnOrdinal}"` / `$"Shot {++shtOrdinal}"`（`SlateLogViewModel.cs:136`、`:145`）。现役用真实场镜名（`DesktopWorkspaceView.cs:421`）。
3. **组内 `FilenameNum` 候选去重集**：`used = TodayLogs.Select(x => x.FilenameNum).ToList()`（`SlateLogViewModel.cs:166`）与 `used = _currentLogs.Select(i => i.FilenameNum)`（`SlateLogPageViewModel.cs:180`），最终落到 `LogEditorViewModel.AvailableFileNumbers`（`src/VoiSlate/ViewModels/LogEditorViewModel.cs:50`，属性 `:68`）。桌面端换成无约束 `NumericUpDown { Minimum = 1, Maximum = int.MaxValue - 1 }`（`DesktopWorkspaceView.cs:578`）——**重号风险**。
4. **`EditRequested` 事件契约**：`public event Action<LogEditorViewModel>? EditRequested;`（`SlateLogViewModel.cs:154`）+ `RequestEdit`（`:157-168`）。这是"VM 造编辑器、View 只负责开窗"的分层接缝，桌面端合并成 UI 直连（`DesktopWorkspaceView.cs:574`）。**若外部宿主/未来 Android 壳按此签名接线，会一起断**。
5. **跨日合并的裸数组导出格式与固定文件名**：`SerializeLogs(all)` + `"slatelog-all.json"`（`SlateLogViewModel.cs:192-199`）、`$"{SelectedDate}.json"`（`SlateLogPageViewModel.cs:189-190`）。现役是带 `Version/Project/ExportedAt/Plan/Days/Session` 的快照对象（`src/VoiSlate/Desktop/DesktopWorkspaceBackup.cs:17`、`:27`）——结构不兼容；但"Flutter 兼容场记 JSON"另有出口（`src/VoiSlate/Desktop/DesktopWorkspaceBackupUi.cs:71`），故**载荷用途不丢、格式会丢**。
6. **按单日导出这一粒度**（`SlateLogPageViewModel.cs:186-191`）：现役只有全量导出，`_logDate` 只影响显示不影响导出（`DesktopWorkspaceView.cs:396-400` vs `:633-644`）。
7. **日期可编辑守卫 `CanEditSelectedDate`**（`SlateLogPageViewModel.cs:82`）：现役允许编辑历史日（`DesktopWorkspaceEditing.cs:58`）。这不是"能力丢失"，而是"旧约束消失"——记录在案以免误判为退步。
8. **`IScheduleBook.SceneLabel/ShotLabel` 的唯一消费路径**（`SlateLogPageViewModel.cs:206-207`）：删除后该抽象只剩 DI 注册（`src/VoiSlate/App.axaml.cs:161`）与 `RecordViewModel` 的标签 provider 用途（`:177`）。
9. **`_refreshing` / `_loading` 的重入与加载期抑制**（`SlateLogViewModel.cs:48`、`:89-94`；`SlateLogPageViewModel.cs:40`、`:89`、`:107`、`:116`）：现役视图同步重绘（`DesktopWorkspaceView.cs:389-443`），无需该保护，属平台差异而非损失。
10. **`Dispose()` 退订约定**（`SlateLogViewModel.cs:202`、`SlateLogPageViewModel.cs:210-214`）：现役已在视图侧实现同一退订（`DesktopWorkspaceView.cs:49`），删除无损失，仅少一条"VM 生命周期归 VM"的样板。
11. **今日守卫的增量刷新优化**（`SlateLogPageViewModel.cs:155-162`）：现役每次写入 `ReloadLogs` 全量重载（`DesktopWorkspaceService.cs:78-83`），历史日也会被读一遍。属性能细节。

**明确不丢的**（避免过度保守）：分组本身（服务 `QueryLogs` 有）、筛选/搜索（视图 `_logFilter`/`_logQuery` + `LogFilter` 五态，`WorkspaceModels.cs:45`、`DesktopWorkspaceService.cs:225-232`）、编辑/删除（`EditLogDetailsAsync`/`DeleteLogAsync`）、清空（`ClearLogsAsync`，视图 `:608-609`）、日期 Tab、空态文案（视图 `:440` 的 `"这个范围还没有场记"` / `"没有匹配的场记"`，比 VM 更细）、撤销（`UndoAsync`）。

---

## 必须先迁移的项

按"删了会不会真出问题"从高到低。前两条是**真缺口**，其余是可选增强。

**1)（高）按单日导出的粒度**
唯一来自 `SlateLogPageViewModel.cs:186-191`（现役只有全量）。建议加在服务上，复用现有序列化：

```csharp
// src/VoiSlate/Desktop/DesktopWorkspaceService.cs
/// <summary>按单日导出裸 SlateLogItem 数组（与 IExportService.SerializeLogs 同格式）；date 为 null 时跨日合并。</summary>
public string ExportLogsJson(string? date = null);
```

并在 `DesktopWorkspaceView.Export()`（`:633`）把 `_logDate` 作为默认范围（标题相应改成"导出当前日期/全部"）。**不要**沿用 `IExportService.SaveToFileAsync(string.Empty, …)`：空目录必抛（`src/VoiSlate/Services/IExportService.cs:29`），应继续走 `SaveFilePickerAsync`。

**2)（高）文件序号候选去重，防重号**
来自 `SlateLogViewModel.cs:166` / `SlateLogPageViewModel.cs:180` 喂给 `LogEditorViewModel.AvailableFileNumbers`（`src/VoiSlate/ViewModels/LogEditorViewModel.cs:50`）。桌面端现在是裸数字框（`DesktopWorkspaceView.cs:578`），用户可填一个已被别的场记占用的文件号。建议：

```csharp
// src/VoiSlate/Desktop/DesktopWorkspaceEditing.cs
/// <summary>指定日期已被占用的文件序号（升序），供编辑器做候选/冲突校验。</summary>
public IReadOnlyList<int> UsedFileNumbers(string date);
```

**3)（中）分组折叠 + 默认展开第 1 场/镜**
来自 `LogSceneGroup.IsOpen`/`LogShotGroup.IsOpen`（`SlateLogViewModel.cs:19-20`、`:33-34`）与 `:136`/`:145` 的初值。属纯 UI 状态，建议直接落在视图（不必污染服务）：

```csharp
// src/VoiSlate/Desktop/DesktopWorkspaceView.cs
private readonly HashSet<string> _collapsed = [];   // 键：date/scn/sht
private bool IsExpanded(string key);
```

**4)（中）当前场/镜的分组级高亮**
来自 `SlateLogPageViewModel.cs:74-79`、`:195-208`、`:204-208`。服务已有字段（`Scene`/`Shot`，`DesktopWorkspaceService.cs:25-26`），只需把 `:441-442` 的静态提示条升级为在组头（`:420-422`）比较 `group.Scene == _service.State.Scene && group.Shot == _service.State.Shot` 加视觉标记。无需新服务方法。

**5)（低）组标题序数重编号 `"Scene n"/"Shot n"`**
`SlateLogViewModel.cs:136`、`:145`。现役显示真实场镜名（`DesktopWorkspaceView.cs:421`），更符合场记习惯，**建议不迁移**；若产品确认要"第几场第几镜"序号，再在视图层按组序生成。

**6)（低）`EditRequested` 事件分层**
`SlateLogViewModel.cs:154`。只有当仓库外还有宿主按此签名接线时才需要；建议先全库确认真无消费者（`grep SlateLogViewModel` 只命中 `App.axaml.cs:203`、`MainViewModel.cs:44`、`:51`），再直接删。

**7)（低）今日守卫的增量刷新**
`SlateLogPageViewModel.cs:155-162`。现役全量重载（`DesktopWorkspaceService.cs:78-83`），数据量到几千条时才有感知，可延后。

---

## 测试锚点

### `tests/VoiSlate.Tests/ViewModels/SlateLogViewModelTests.cs`（3 个用例）

| 用例（行） | 保护的逻辑 |
|---|---|
| `Load_Exposes_Today_Dates_And_TodayLogs`（`:47-59`） | `LoadAsync` 把 `Today` 定为注入时钟的今日键（断言 `"260820"`，`:56`）、`Dates` 原样透传仓库升序（`["260819","260820"]`，`:57`）、`TodayLogs` 取 `SelectedDate` 当日条目（`Single` + `Scn=="1A"`，`:58-59`）。即保护"构造期 `Today=SelectedDate` 初值"（`SlateLogViewModel.cs:56-57`）与 `RefreshAsync` 的两段读取（`:97`、`:105`） |
| `LogsChanged_Refreshes_TodayLogs_Automatically`（`:62-77`） | 构造期订阅（`:58`）+ `OnLogsChanged` 无条件刷新（`:78`）：新增后 `Single` 且 `FileName=="260820-T001"`（`:71-73`），撤回后 `Empty`（`:76`） |
| `Dispose_Unsubscribes_LogsChanged`（`:79-89`） | `Dispose`（`:202`）真退订：退订后再写不刷新（断言 `Empty`，`":已退订 → 不刷新"`，`:89`） |

> 覆盖缺口：**3 个用例都没碰** `RebuildGroups`（分组/展开）、`RequestEdit`/`EditRequested`、`Delete` 命令、`Export` 命令。也就是说删除这个 VM 后，能挡回归的只有"日期列表/当日列表/退订"三条。

### `tests/VoiSlate.Tests/ViewModels/SlateLogPageViewModelTests.cs`（6 个用例）

| 用例（行） | 保护的逻辑 |
|---|---|
| `Load_Selects_Latest_Date_And_Builds_Groups`（`:56-69`） | 默认选中**最近日期**而非今日（`SelectedDate=="260820"`，`:66`）+ 场/镜两层树结构（`Single(Groups)`、`Scn=="1A"`、`Shots.Count==2`，`:67-69`），对应 `SlateLogPageViewModel.cs:100-103` 与 `:135-150` |
| `SelectDate_Reloads_Groups`（`:73-84`） | `SelectedDate` 变更经 `OnSelectedDateChanged`（`:113-120`）触发 `RefreshSelectedAsync`，切到 `260819` 后场组变成 `"2A"`（`:84`） |
| `LogsChanged_Refreshes_When_Selected_Date_Is_Today`（`:88-95`） | 今日守卫（`:155-162`）：仅 `SelectedDate == Today` 时才重建 `Groups`（`:95`） |
| `ExportJson_Delegates_To_IExportService`（`:99-108`） | 导出只取当前日 `_currentLogs`（`:189`）+ 文件名 `$"{SelectedDate}.json"`（断言 `"260820.json"`，`:107`）。**注意**：Spy 桩直接回 `"[]"`（`:108`），因此掩盖了 `SaveToFileAsync(string.Empty, …)` 在真实 `ExportService`（`src/VoiSlate/Services/IExportService.cs:29`）上的 `ArgumentException` |
| `CreateLogEditor_Works_Only_For_Today`（`:112-128`） | `CanEditSelectedDate`（`:82`）守卫：历史日返回 `null`（`:121-122`），今日返回非空且 `Scn=="1A"`（`:125-128`）；顺带保护 `_currentLogs` 定位（`:174-181`） |
| `Highlight_Follows_Session_Selection`（`:132-141`） | 会话属性变化 → `UpdateHighlight`（`:195-208`）→ `IScheduleBook` 标签：`CurrentScene=="1A"`、`CurrentShot=="1"`（`:135`、`:140-141`，注释说明 Stub 标签固定） |

> 覆盖缺口：**6 个用例都没碰** `RefreshSelectedAsync` 的排序规则（`OrderBy(g => g.Key)`，`:135`/`:138`）、`LoadAsync` 的 `_loading` 抑制、以及历史日"全部日期"态（VM 本身就没有）。

---

**统计**：共 32 个成员判定 —— `覆盖` 16 条、`部分覆盖` 14 条、`缺失` 2 条（`EditRequested` 事件、`CanEditSelectedDate` 守卫）。两处"缺失"都不是现役能力空缺所必需：前者是分层接缝（需确认无外部接线即可删），后者是应作废的旧约束。真正**只有这两个 VM 提供、现役没有**的只有"按单日导出"与"分组级高亮"两条。