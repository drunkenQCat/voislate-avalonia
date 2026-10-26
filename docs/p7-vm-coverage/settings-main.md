# MainViewModel / SettingsViewModel 覆盖度对照

审计对象：`voislate-avalonia/src/VoiSlate/ViewModels/MainViewModel.cs`（110 行）、`src/VoiSlate/ViewModels/SettingsViewModel.cs`（198 行）。
对照目标：`src/VoiSlate/Desktop/DesktopWorkspaceView.cs`、`DesktopWorkspaceService.cs`、`DesktopWorkspacePreferences.cs`、`DesktopWorkspaceBackupUi.cs`、`WorkspaceModels.cs`（同目录）。
路径缩写（后文表格的「文件:行号」均相对仓库 `voislate-avalonia/`）：

| 缩写 | 文件 |
|---|---|
| `VM/Main` | `src/VoiSlate/ViewModels/MainViewModel.cs` |
| `VM/Settings` | `src/VoiSlate/ViewModels/SettingsViewModel.cs` |
| `View` | `src/VoiSlate/Desktop/DesktopWorkspaceView.cs` |
| `Svc` | `src/VoiSlate/Desktop/DesktopWorkspaceService.cs` |
| `Pref` | `src/VoiSlate/Desktop/DesktopWorkspacePreferences.cs` |
| `BackupUi` | `src/VoiSlate/Desktop/DesktopWorkspaceBackupUi.cs` |
| `Models` | `src/VoiSlate/Desktop/WorkspaceModels.cs` |

## 审计基线（判定的前提事实）

- **两个 VM 确实零消费者**：`sp.GetRequiredService<MainViewModel>()` 在整仓只出现 0 次；`SettingsViewModel` 的唯一解析点是 `App.axaml.cs:226`（MainViewModel 工厂内部），`MainViewModel` 的唯一解析点不存在。二者只在 `App.axaml.cs:214-226` 注册。
- **消费它们的 XAML 已全部删除**：`git show b4876db` 删掉 16 个 `.axaml(.cs)`（`Views/MainWindow.axaml`、`SettingsView.axaml`、`RecordView/ScheduleView/SlateLogView` 等）。`App.axaml:30-33` 明示「P7 手机壳的 4 条 VM→View 隐式模板已删除」。
- **页键的旧消费方式**：已删除的 `MainWindow.axaml`（`git show b4876db^:src/VoiSlate/Views/MainWindow.axaml`）用 `CommandParameter="Settings"`(:56)、`"Schedule"`(:81)、`"Record"`(:95)、`"SlateLog"`(:109) 消费页键——混合大小写，正是 `VM/Main:89-90` 那句 `key?.ToLowerInvariant()` 归一化的来由。那份 XAML **没有任何 nav AutomationId**。
- **`navb-*` / `nav-*` 是 P8 自造字面量**，与 `MainViewModel` 的页键无继承关系：`View:156` `"navb-" + key`（key 取值见 `View:151`）、`View:163` `"navb-settings"`、`View:181` `"nav-" + key`、`View:188` `"settings"`。现役页键集合是 `record/plan/log/settings`（**3 页 + 1 模态**），而 VM 的集合是 `record/schedule/slatelog/settings`（4 页）。
- **设置已从「页」变成「模态」**：`View:598-619 ShowSettings()` 走 `Dialog(...)`（`View:450-453`，Window 宿主真弹窗 / Android 走 overlay 层）；`_page` 字段的取值域只有 `record/plan/log`（`View:28`、`View:141`）。这一条决定了下文一半的判定。
- **数据落库位置**：`ISessionSettingsStore` 实现 = LiteDB 集合 **`"settings"`**（`Services/ISessionSettingsStore.cs:41`，`BsonDocument {_id, v}`）；`ILogRepository` = LiteDB 集合 **`"logs"`**（`Services/ILogRepository.cs:25`）。VM 与现役实现**同集合、不同 `_id`**：VM 工程名 = `_id="project"`（`VM/Settings:18,95,133,169`），现役工程名 = `_id="desktop.workspace.v1"`（`Svc:15,87`）里 `WorkspaceSession.Project` 字段（`Models:30`）。

---

## MainViewModel 逐方法对照

| VM 成员 | 作用（一句话） | 判定 | 现役对应物 / 差异 |
|---|---|---|---|
| `RecordPageKey = "record"`（`VM/Main:34`） | 「记录」页键 | `覆盖` | `View:28` `private string _page = "record"`；渲染分支 `View:141` `_ => BuildRecord()`；自动化 ID `View:156`/`View:181`。差异：字面量重复而非共享常量（`"record"` 在 `View` 出现 ≥8 处：28/64/71/141/151/179/362/409）。 |
| `SchedulePageKey = "schedule"`（`VM/Main:35`） | 「计划」页键 | `部分覆盖` | 现役同名页键叫 **`"plan"`**：`View:151` `("plan","▤","计划")`、`View:64` Ctrl+2、`View:179` rail 项。键名不兼容——任何按 `"schedule"` 定位的旧调用/脚本在现役壳里落空（`View:141` 的 `_ => BuildRecord()` 会静默回到记录页）。 |
| `SlateLogPageKey = "slatelog"`（`VM/Main:36`） | 「场记」页键 | `部分覆盖` | 现役叫 **`"log"`**：`View:151`、`View:64` Ctrl+3、`View:179`。同上报废风险。 |
| `SettingsPageKey = "settings"`（`VM/Main:37`） | 「设置」页键 | `部分覆盖` | 设置不再是页：`View:598-619` 是模态。`"settings"` 退化为按钮 AutomationId（`View:188`）与 `"navb-settings"`（`View:163`）。页键→页实例的语义在现役壳中不存在。 |
| `MainPageDescriptor`（`VM/Main:10-24`，字段 `Title/Key/Instance/Factory` + `Resolve()`） | 页描述符：单例实例或 Scoped 工厂 | `部分覆盖` | 现役导航表用内联元组：`View:151` `new (string Key, string Icon, string Text)[]`、`View:179` `new[] { ("record","◉  当前记录"), … }`。有 Title/Key，**无 Instance/Factory/Resolve**——现役没有「页实例」概念。 |
| 构造器建表 + `Pages`（`VM/Main:47-53,61`） | 注册 4 页（记录/计划/场记/设置） | `部分覆盖` | 宽屏 3 页 + 偏好（`View:179-188`），窄屏 3 页 + 偏好（`View:148-169`）。四页 → 三页 + 一模态；且宽/窄两套表各自硬编码，无单一事实来源。 |
| 构造器 `CurrentPage = Resolve(RecordPageKey)` / `CurrentPageKey = RecordPageKey`（`VM/Main:57-58`） | 启动落在记录页 | `覆盖` | `View:28` 默认 `_page="record"`；`View:141` 默认分支 `BuildRecord()`。 |
| `CurrentPage`（`VM/Main:64-65`） | 当前页 VM 实例（ContentControl.Content） | `部分覆盖` | 现役 `_host.Content = _page switch {…}`（`View:128,141`）每次 `Render()` 重建控件树；无 VM 实例承载页状态（状态全在 `Svc`）。语义从「切引用」变成「重画」。 |
| `CurrentPageKey`（`VM/Main:68-69`） | 当前页键（导航高亮） | `部分覆盖` | 私有字段 `_page`（`View:28`），无属性通知；高亮在渲染时内联判断：`View:155` `var on = _page == key;`、`View:181` `_page == key ? "selected" : "quiet"`。 |
| `OnCurrentPageKeyChanged`（`VM/Main:80-84`） | 键变更 → 通知两个可见性属性 | `部分覆盖` | 由 `Render()` 全量重画取代（`View:123-142`）；无 `IsTabBarVisible/IsAppBarVisible` 可通知（二者已不存在，见下）。 |
| `IsTabBarVisible`（`VM/Main:75`） | 设置页全屏时隐藏底部导航 | `缺失` | 现役底栏唯一门控是屏宽：`View:51` `Bounds.Width < 820` → `_compact`，`View:132-134` `if (_compact) { …BuildBottomNav()… }`。三页恒显示底栏、偏好是模态 → **不存在「某页隐藏 Tab 栏」的状态**。窄屏壳里该属性无意义。 |
| `IsAppBarVisible`（`VM/Main:78`） | 设置页隐藏全局 AppBar | `缺失` | 现役无全局 AppBar：每页自带 `PageHeader(...)`（`View:191-197`，调用点 `View:209`/`286`/`374`）；模态自带标题（`View:455-466` DialogWindow、`WorkspaceDialog.ForOverlay`）。概念已消失。 |
| `Navigate(string? key)`（`VM/Main:86-103`） | 归一化键 + 校验 + 切页（工厂/单例二选一） | `部分覆盖` | `View:115-120 private void Navigate(string page)`：**无键校验、无未知键忽略**（未命中落到 `View:141` 的 `_ => BuildRecord()`）、无工厂/单例区分；多出两个副作用：`_finishVoice?.Invoke()`（`View:117`）与进入计划页时惰性初始化场景 `if (page=="plan" && _planScene.Length==0) _planScene = _service.State.Scene;`（`View:118`）。 |
| `Navigate` 的 Record Scoped 语义（`VM/Main:49,101`；R15「每次导航新建实例」） | 记录页每次进入重建、其余复用单例 | `缺失` | 现役记录页由 `BuildRecord()`（`View:206-277`）每次 `Render` 现造，但**没有页实例可复用/丢弃**——拍摄状态在 `Svc.Draft`（`Svc:28-35`）与 `State`（`Svc:20`）。「Scoped vs Singleton」这对概念在现役架构里不成立。 |
| `_lastPageKey`（`VM/Main:72`） | 设置页返回目标 | `缺失` | 无任何「上一页」记录。现役返回全是硬编码回记录页：`View:202` `返回当前拍摄 →`、`View:286`/`374` `返回拍摄 ×`、`View:71` `Key.Escape → Navigate("record")`。 |
| `GoBack()`（`VM/Main:106-107`） | 返回进入设置页之前的页 | `缺失` | **无返回栈**。现役设置是模态（`View:598`），关窗即回原页（`View:618 Render()` 收尾），所以「返回上一页」的需求被模态消解；但一旦多级页导航回归（如计划→镜头详情→时间轴），现役壳**无历史可回**，`Footer` 只能回记录页（`View:198-205`）。这是删 `MainViewModel` **真正丢掉的唯一导航语义**。 |
| `Resolve(string key)`（`VM/Main:109`） | 按键取实例 | `部分覆盖` | 由 `View:141` 的 switch 承担（键→构建方法）。 |
| （连带）`Pages` 的 4 条键被 XAML/自动化 ID 消费 | 旧 MainWindow 用 `CommandParameter="Settings"/"Schedule"/"Record"/"SlateLog"` 定位 | `缺失` | 消费方随 `b4876db` 全删（`App.axaml:30-33`）。现存 `navb-record/navb-plan/navb-log/navb-settings`（`View:156,163`）、`nav-record/nav-plan/nav-log/settings`（`View:181,188`）由测试断言：`tests/VoiSlate.Tests/Desktop/WorkspacePhoneLayoutTests.cs:46,56,71`、`DesktopWorkspaceUiTests.cs:240,260-261`——**它们不引用任何 `PageKey` 常量，且其中 `plan/log` 与 VM 的 `schedule/slatelog` 不同名**。结论：四个 PageKey 常量已无 XAML/自动化 ID 依赖。 |

---

## SettingsViewModel 逐方法对照

| VM 成员 | 作用（一句话） | 判定 | 现役对应物 / 差异 |
|---|---|---|---|
| `ProjectKey = "project"`（`VM/Settings:18`） | 工程名持久化键（原版 Hive `settings`/`project` 迁移键） | `缺失` | 现役工程名存 `WorkspaceSession.Project`（`Models:30`，默认 `"NewProject"`），随整段 JSON 落 `_id="desktop.workspace.v1"`（`Svc:15,87`）→ 同一 LiteDB `"settings"` 集合（`Services/ISessionSettingsStore.cs:41`）的**另一行**。全仓**没有任何代码读 `"project"` 键**（`SeedService` 不写 settings），删掉 VM 该键即成孤儿数据，且现役无迁移逻辑。 |
| `ProjectName`（`VM/Settings:48-49`）+ `OnProjectNameChanged`（`:133` 即时写） | 工程名输入 + 每次改即落库 | `覆盖` | `View:602` TextBox ← `_service.State.Project`；保存按钮 `View:612-616` → `Svc:120-124 SetPreferencesAsync`。差异：现役是**显式保存**而非逐字符写；`Svc:122` 空串归一为 `"未命名工程"` 并 `Trim()`，VM 无归一。 |
| `SaveProject`（`VM/Settings:164-171`） | 兜底显式保存 | `覆盖` | 同一「保存偏好」按钮（`View:612-616`）。 |
| `TodayCount`（`VM/Settings:52-53`） | 今日场记条数 | `部分覆盖` | 等价数字只存在于**时间轴页**的指标「已存场记」：`View:392-395`（`var all = _service.Logs.Where(l => (_logDate is null || l.Date == _logDate) …)`，`all.Count`）。口径：默认 `_logDate = service.Today`（`View:43`）即今天，但用户点「全部日期」（`View:396`）后该数字变成全量——**口径随筛选漂移**；且偏好弹窗（`View:606-616`）根本不显示今日条数。VM 无筛选、恒为今天，口径更稳。 |
| `LoadAsync(ct)`（`VM/Settings:126-131`） | 刷新今日计数（进入设置页时） | `部分覆盖` | 每次 `Render()` 重算（`View:123`→`RenderTimelineContents` `View:389-395`）；无显式刷新入口、无 `CancellationToken`。 |
| `RecordLinker`（`VM/Settings:56-57`） | 链接符输入缓冲 | `覆盖` | `WorkspaceNaming.Linker`（`Models:55`，默认 `"-T"`）← 输入框 `Pref:13`；`Pref:14` 还实时预览 `_service.CurrentFile`。 |
| `SaveLinker`（`VM/Settings:174-179`） | 经 `ITakeFlowService.SetLinkerAsync` 唯一写入口 | `覆盖` | `Pref:16-24`「保存文件命名」→ `Svc:…SetNamingAsync`（`Desktop/DesktopWorkspaceEditing.cs:10-14`）：先 `naming.Validate()`，再 `State.FileNumber = NextAvailableNumber()` 重算文件号（VM 改了 linker 不重算号）。落库不同：VM → `SessionKeys.RecordLinker`（`Services/TakeFlowService.cs:252-256`）；现役 → `State.Naming` 整段 JSON（`Svc:87`）。二者互不读取。 |
| `PrefixMode`（`VM/Settings:60-61`）+ `PrefixModes`（`:83-84`）+ `PrefixModeOf`（`:86-91`） | 三模式前缀（显示串 ↔ `PrefixType`） | `部分覆盖` | `Pref:11` ComboBox 三档「日期 / Sound Devices / 自定义」，模式串 `"default"/"sound devices"/"custom"`（`Pref:20`）与 `PrefixType.ToSettingsValue()`（`Models/Enums.cs:49-54`）逐字一致。差异一：VM 的 SoundDevices 显示串写「声音设备（yyYmMd）」（`VM/Settings:79`），实际格式是 `yy + "Y" + MM + "M" + dd`（`Services/ITimeProvider.cs:24-26` → `23Y05M22`；`Models:57` 同式），**标签是错的**；现役用实时文件名预览（`Pref:14,21`）代替文字描述。差异二：现役模式值存在 `State.Naming.Mode`，不写 `SessionKeys.PrefixType`。 |
| `CustomPrefix`（`VM/Settings:64-65`） | 自定义前缀文本 | `部分覆盖` | `Pref:12` TextBox ← `naming.CustomPrefix`。**无 `IsCustomPrefixEnabled` 等价物**——现役前缀框恒可用（不随模式禁用），校验推迟到保存：`Models:58-65 WorkspaceNaming.Validate()`（custom 模式非空、≤100、无非法文件名字符）。 |
| `IsCustomPrefixEnabled`（`VM/Settings:68-69`）+ `OnPrefixModeChanged`（`:103`） | 仅「自定义」模式启用前缀框 | `缺失` | 同上：现役不做启用联动（`Pref:12` 无 `IsEnabled` 绑定）。属 UX 细节，非数据风险。 |
| `SavePrefix`（`VM/Settings:182-194`） | 经 `SetPrefixAsync` 写模式 + 自定义文本 | `覆盖` | `Pref:16-24`「保存文件命名」，额外带 `WorkspaceNaming.Validate()` 与文件号重算。落库：VM → `SessionKeys.PrefixType`/`CustomPrefix`（`Services/TakeFlowService.cs:265-269`）；现役 → `State.Naming`（`Svc:87`）。 |
| `IsLinked`（`VM/Settings:72-76`） | 补录联动开关（直连会话单例，即时写 `SessionKeys.IsLinked`） | `部分覆盖` | 数据侧有等价物 `WorkspaceSession.Linked`（`Models:25`，默认 `true`），但**只能在记录页「当前条操作」弹窗切换**：`View:488` → `Svc:125-129 ToggleRetakeAsync()`（`Draft.Ended` 时抛「请先选择下一镜或再拍一条」），且 `Svc:193 SelectShotAsync` 每次选镜强制置 `true`。**偏好弹窗里没有这个开关**（`View:603-607` 只有 pocket/left）——设置页入口缺失。 |
| `ClearToday`（`VM/Settings:136-147`） | 清空今日场记（循环 `DeleteItemAsync(i)`，保持「日志写唯一入口」） | `覆盖` | `View:608`「清空今日场记」+ 强确认（`Confirm` `View:467-473`）→ `Svc:252-256 ClearLogsAsync(false)`（`foreach day in new[]{Today} await logs.ClearAsync(day)`）。**现役更优**：单次 `ClearAsync`、包在 `Change()` 事务里（快照/回滚/`ReloadLogs`，`Svc:90-110`）、清完 `_undo.Clear()`。反观 VM 的循环有两个问题：(1) O(n) 次独立写；(2) 循环用 VM 的 `today = VoiSlateDates.TodayKey(_time.Now)`（`VM/Settings:139`）算索引，而 `TakeFlowService.DeleteItemAsync` 内部删的是**它自己的** `Today`（`Services/TakeFlowService.cs:55,239-244`）——跨日边界（00:00 前后）会删错日期。 |
| `ExportAll`（`VM/Settings:150-161`） | 跨日合并全部场记 → `IExportService` 序列化落盘 | `覆盖`（格式有差） | `View:633-644 Export()`：`StorageProvider.SaveFilePickerAsync` 选路径 → `_service.ExportJson()`（`BackupUi:…`，实为 `DesktopWorkspaceBackup.cs:15-28 Snapshot()`：**WorkspaceBackup v2** = project + ExportedAt + Session + Plan + Days，跨日合并于 `Days = Logs.GroupBy(l => l.Date)`）。若需 VM 那种「纯场记数组 / Flutter 兼容」格式，等价物是 `ExportFlutterJson()`（`DesktopWorkspaceBackup.cs:29-38`：补录 `Tk→0`、tracks 回填成 `<x/>` 到 `ShtNote`）→ `BackupUi:107-117 ExportFlutter()`。**VM 侧的真实缺陷**：`SaveToFileAsync(string.Empty, "all.json", json)`（`VM/Settings:160`）在真实 `ExportService`（`Services/IExportService.cs:28-32`：`Directory.CreateDirectory(dir)` + `Path.Combine`）下会抛 `ArgumentException`——**这条路在生产从未可用**，只有测试里的 `SpyExportService` 能过。 |
| `Dispose`（`VM/Settings:197`） | 退订 `RecordingSessionViewModel.PropertyChanged` | `部分覆盖` | 现役无 per-VM 订阅可泄：视图只订阅 `_service.Changed`，退订在 `View:49`（`DetachedFromVisualTree += (_, _) => _service.Changed -= OnChanged;`）。等价清理存在，对象不同。 |
| `Initialization`（`VM/Settings:28,93-101`）+ `OnSessionPropertyChanged`（`:105-123`） | 构造即加载 + 会话变更联动 | `覆盖`（联动为 `缺失`且不需要） | 加载：`Svc:42-64 InitializeAsync()` 由宿主 await（`App.axaml.cs:77` 桌面 / `:111-120` Android，完成后 `view.Refresh()`），异常落日志（`App.axaml.cs:69,119`）。会话→UI 联动：现役用 `_service.Changed → Render()`（`Svc:109`、`View:48,100`）全量重画替代逐属性订阅。 |

**Settings 侧五条硬问题的直答（要求逐条给结论）**

1. **`ProjectName` 持久化键是否与 service 偏好同一份存储？** —— 同一**集合**（LiteDB `"settings"`），不同 **`_id` 行**：VM 写 `"project"`（`VM/Settings:18`），service 把工程名塞进 `"desktop.workspace.v1"` 的 JSON（`Svc:15,87` + `Models:30`）。**两者互不读取** → 不是同一份存储记录。`"project"` 键现在无读者、无迁移。
2. **`PrefixModes` 三模式是否有等价物？** —— 有（`Pref:11` + `Models:8-11,56-65`），模式串逐字一致；但 VM 的「yyYmMd」标签错误，且现役不写 `SessionKeys.PrefixType`。
3. **今日条数统计口径？** —— VM = `GetByDateAsync(TodayKey(now)).Count`（`VM/Settings:128-129`），含假条/补录。现役 = `View:392-395` 的 `all.Count`，`all` 受 `_logDate`/`_scopeScene` 筛选影响（默认今天，切「全部日期」后变全量）→ **口径漂移**，且偏好弹窗不显示。
4. **「清空今日」的循环写入口？** —— 现役不用循环：`Svc:252-256` 单次 `logs.ClearAsync(Today)`，在有事务/快照的 `Change()` 内（`Svc:90-110`）。VM 的 `for (i = count-1; i>=0; i--) _takeFlow.DeleteItemAsync(i)`（`VM/Settings:141-144`）是历史负担，且有跨日删错日的隐患。
5. **「导出全部」（跨日合并）是否有等价物？** —— 有两条：`ExportJson()`（`DesktopWorkspaceBackup.cs:15-28`，备份 v2 全量）与 `ExportFlutterJson()`（`:29-38`，纯场记数组 + 兼容改写）。差异在**格式契约**：VM 走 `IExportService.SerializeLogs`（camelCase、无日期字段、`VoI…` JSON 选项）；现役 `ExportJson` 是包装对象，`ExportFlutterJson` 才接近裸数组。**落库位置**：三者都读同一 `"logs"` 集合（`Services/ILogRepository.cs:25`），无落库差异，只有 envelope 差异。

---

## 删除会丢掉的东西

1. **旧工程名数据（LiteDB `"settings"` 集合、`_id="project"`）**：除 `SettingsViewModel` 自身外无任何读写者，`SeedService` 不写 settings。删 VM 后该值永久成为孤儿——数据还在库里，但再没有代码路径能把它读出来或迁进 `State.Project`（`Models:30`，默认 `"NewProject"`）。**这是唯一会静默丢用户数据的项。**
2. **9 个回归用例**：`MainViewModelTests` 5 个 + `SettingsViewModelTests` 4 个（详见下节）。它们是目前唯一锁住「页键集合/导航工厂语义」「清空只走服务层」「导出委派契约」的东西。
3. **返回栈语义 `_lastPageKey` / `GoBack`**：现役返回动作全部硬编码回记录页（`View:71,202,286,374`）。模态设置让「返回上一页」暂时不需要，但只要导航深度 >1 重新出现（计划→镜头详情→时间轴、未来把设置改回整屏页），**没有历史栈可依**。
4. **「页实例」契约（`MainPageDescriptor.Factory/Instance` + R15 记录页 Scoped）**：现役是无状态 builder + service 持有全部状态（`Svc:20,28-35`）。若将来要做「切页保活/复用页面控件」或「记录页每次进入重置 VM」，需从零重建。
5. **「设置作为页」的可见性语义（`IsTabBarVisible`/`IsAppBarVisible`）**：现役 Tab 栏只受 `_compact` 门控（`View:132-134`）、无全局 AppBar。若产品要求「设置页占满整屏但仍保留底部 Tab」，需要重新引入状态。
6. **`IExportService` 在设置链路上的唯一消费者 + 裸数组导出契约**：`Services/IExportService.cs:15-18` 的 `SerializeLogs` 语义只有 `SettingsViewModel`（与 `SlateLogPageViewModel`）消费；`ExportService` 注册在 `App.axaml.cs:186`，删 VM 后仅剩 `SlateLogViewModel`（`:203-207`）在用。
7. **级联死注册（不在你名下 VM 里，但同批变成无人解析）**：`Func<RecordViewModel>`（`App.axaml.cs:193-200`）、`ScheduleViewModel`（`:209-212`）、`SlateLogViewModel`（`:203-207`）——三者的**唯一** `GetRequiredService` 点在 `App.axaml.cs:222-226` 的 `MainViewModel` 工厂内。删 `MainViewModel` 后这三条也成死代码，需一并裁定（顺带：`ITakeFlowService`、`RecordingSessionViewModel` 仍被启动流程解析，`App.axaml.cs:47` / `:166-182`，不受影响）。
8. **四页导航表本身**（记录/计划/场记/设置 的顺序与中文标题）：现役宽窄两套表各自内联（`View:151`、`View:179`），顺序一致但无共享定义；删 VM 后标题/顺序只存在于两处字面量里，改一处易漏另一处。

## 必须先迁移的项（按风险排序）

| 序 | 风险 | 迁移项 | 建议签名 / 落点 |
|---|---|---|---|
| **1** | **高（用户数据）** | 旧 `"project"` 键 → `State.Project` | `public async Task MigrateLegacyProjectNameAsync()`：读 `settings.GetStringAsync("project")`；非空 **且** `State.Project` 仍是默认 `"NewProject"` 时写入并 `Persist()`。挂在 `Svc:42-64 InitializeAsync()` 末尾（幂等，只迁一次）。不迁则用户工程名在 UI 上直接消失。 |
| **2** | **中高（外部格式）** | 「导出全部」的裸数组 / Flutter 兼容格式 | `public string ExportAllLogsJson()`（= 现 `ExportFlutterJson` 语义）与 `public Task<string> ExportAllLogsAsync(string directory, string fileName)`；或在设置弹窗明示「导出 = 备份 v2 包装对象，不是裸数组」。同时修掉 `VM/Settings:160` 的 `SaveToFileAsync("")` 缺陷（真实 `ExportServiceImpl` 会抛 `ArgumentException`，`Services/IExportService.cs:28-32`）。 |
| **3** | 中（口径） | 今日条数 | `public int TodayTakeCount => Logs.Count(l => l.Date == Today);` + 在 `View:606` 设置弹窗内显示；否则「今日 N 条」在现役只存在于时间轴页，且选「全部日期」后口径变化（`View:396`）。 |
| **4** | 中（功能缺失） | 设置页的补录联动开关 | `public Task SetLinkedAsync(bool value)`（内部复用 `Svc:125-129` 的 `Draft.Ended` 前置校验，但**不要**复用 `SelectShotAsync` 的强制置 true，`Svc:193`），挂到 `View:603-607` 的偏好区。 |
| **5** | 低中（可维护性） | 页键常量收口 | `public static class WorkspacePages { public const string Record="record", Plan="plan", Log="log", Settings="settings"; }`，替换 `View` 中 ≥12 处字面量（`28,64,71,141,151,156,163,179,181,188,362,409`）。**注意**：若同时把 `plan/log` 改名成 `schedule/slatelog`，自动化 ID `navb-*`/`nav-*` 会一起变，必须同步 `WorkspacePhoneLayoutTests.cs:46,56,71` 与 `DesktopWorkspaceUiTests.cs:240,260-261`。 |
| **6** | 低（未来需求） | 返回栈 | `private readonly Stack<string> _history = new();`，`Navigate` 内压栈、新增 `GoBack()` 弹栈；等「导航深度 >1」需求出现再做（当前模态设置不需要）。 |
| **7** | 低（UX 细节） | 自定义前缀启用联动 | `Pref:12` 加 `prefix.IsEnabled = mode.SelectedIndex == 2;` + `mode.SelectionChanged` 联动（对齐 `VM/Settings:68-69,103`）。 |

## 测试锚点

`tests/VoiSlate.Tests/ViewModels/MainViewModelTests.cs`（5 个）：

| 用例 | 保护的逻辑 | 删除 VM 后失去的护栏 |
|---|---|---|
| `Starts_On_Record_Page_With_Fresh_Instance` | 构造即落在 `RecordPageKey`，且工厂**只被调用一次**（`Asserts.Single(h.Created)`） | 「启动页 = 记录页」+「构造期不重复建页」 |
| `Pages_Expose_Four_Entries_In_Order` | `Pages` 的键序 = `[record, schedule, slatelog, settings]`、标题 = `["记录","计划","场记","设置"]`（`VM/Main:49-52`） | 导航表的**顺序与文案**契约（现役靠两处内联元组维持，`View:151,179`） |
| `Navigate_Record_Creates_New_Instance_Each_Time` | 每次导航记录页都 `NotSame` 且 `Created.Count == 2` | R15「记录页 Scoped、每次进入重建」（`VM/Main:101`） |
| `Navigate_To_Singleton_Pages_Reuses_Instances` | 计划页两次导航 `Same`，且记录页工厂**未被再调用**（`Single(h.Created)`） | 「非记录页复用单例」这条语义（`VM/Main:50-52,101`） |
| `Unknown_Key_Is_Ignored` | `Execute("???")` 后 `CurrentPage` 不变（`VM/Main:91-94`） | 未知键静默忽略。**现役没有这条护栏**：未命中键会落到 `View:141` 的 `_ => BuildRecord()`，即「静默跳回记录页」——这是行为回退点，值得在迁移时补一个等价测试 |

`tests/VoiSlate.Tests/ViewModels/SettingsViewModelTests.cs`（4 个）：

| 用例 | 保护的逻辑 | 删除 VM 后失去的护栏 |
|---|---|---|
| `ProjectName_Loads_And_Persists_On_Change` | 用 `SettingsViewModel.ProjectKey` 播种 → 加载出「我的项目」；改值后仓储里即为新值（`VM/Settings:95,133`） | **`"project"` 键名本身**。这是唯一钉住「工程名写在哪个键」的断言——也是迁移第 1 项必须保留的证据 |
| `Load_Refreshes_TodayCount` | 播种 260820 两条 + 260819 一条 → `TodayCount == 2`（昨日不计） | 「今日条数按 `TodayKey` 过滤」的口径（`VM/Settings:128-129`） |
| `ClearToday_Removes_Todays_Logs_Through_Service_Only` | 清后今日 0 条、昨日仍在、`TodayCount==0`、且 **`SessionKeys.RecordCount` 仍为 5**（文件号不受清空影响） | ①「清空只走服务层」纪律；②**文件号必须保留**这一不可逆操作的安全边界（现役 `Svc:252-256` 同样不动 `State.FileNumber`，但没有测试钉它） |
| `ExportAll_Collects_All_Dates_And_Delegates_To_IExportService` | `LastLogs.Count == 3`（跨日 260819+260820 合并）、`LastName == "all.json"`、`LastContent == "[]"`（Spy 桩） | 「跨日合并」语义 + 「导出必须委派 `IExportService` 而非自己写文件」。注意 `LastContent == "[]"` 说明**真实序列化从未被测**——迁移时应补一条对 `ExportJson()`/`ExportFlutterJson()` 的真断言 |