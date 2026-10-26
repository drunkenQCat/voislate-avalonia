# ScheduleViewModel 覆盖度对照

> **审计对象**：`src/VoiSlate/ViewModels/ScheduleViewModel.cs`（495 行，P7 手机壳残留 VM）
> **对照目标**：P8 现役 `DesktopWorkspaceService`（+ 两个分部类 / 工作台视图）
> **只读审计**：未修改 `src/` 与 `tests/` 下任何文件。
> **死代码确认**：`App.axaml.cs:209-212` 仍注册 `ScheduleViewModel`，但唯一消费者 `MainViewModel`（`App.axaml.cs:222-226`）在桌面/Android 两条启动路径上都不被解析——两条路径都直接 `CreateWorkspace(...)` 造 `DesktopWorkspaceService` 并交给 `DesktopWorkspaceWindow` / `DesktopWorkspaceView`（`App.axaml.cs:75-78`、`App.axaml.cs:101-106`）。运行时确实无人解析本 VM 及其依赖 `IScheduleStore`/`ICsvScheduleParser`。同理 `ScheduleService`（`Services/IScheduleService.cs:50`，含 20 步 undo 与"至少 1 场 1 镜"守卫）也**未注册进 DI**，只在测试里使用——它不是现役实现，迁移时只能当参考实现读。

**路径图例**（下表一律用短名，映射见下）：

| 短名 | 绝对/仓库相对路径 |
|---|---|
| `ScheduleViewModel.cs` | `src/VoiSlate/ViewModels/ScheduleViewModel.cs` |
| `DesktopWorkspaceService.cs` | `src/VoiSlate/Desktop/DesktopWorkspaceService.cs` |
| `DesktopWorkspaceEditing.cs` | `src/VoiSlate/Desktop/DesktopWorkspaceEditing.cs` |
| `DesktopWorkspaceView.cs` | `src/VoiSlate/Desktop/DesktopWorkspaceView.cs` |
| `WorkspaceModels.cs` | `src/VoiSlate/Desktop/WorkspaceModels.cs` |
| `SlateSchedule.cs` | `src/VoiSlate/Models/SlateSchedule.cs` |
| `ScheduleStore.cs` | `src/VoiSlate/Services/ScheduleStore.cs` |
| `ScheduleCsvParser.cs` | `src/VoiSlate/Services/ScheduleCsvParser.cs` |
| `RecordingSessionViewModel.cs` | `src/VoiSlate/ViewModels/RecordingSessionViewModel.cs` |

## 逐方法对照

统计：**覆盖 1 / 部分覆盖 26 / 缺失 2**（共 29 个成员行；其中一行合并了 4 个私有 partial 成员）。

| VM 成员 | 作用（一句话） | 判定 | 现役对应物 / 差异 |
|---|---|---|---|
| `Scenes`（`ScheduleViewModel.cs:38`） | 计划集合，视图绑定源 | 部分覆盖 | `DesktopWorkspaceService.cs:21` `List<SceneSchedule> Scenes` + `Changed` 事件（`DesktopWorkspaceService.cs:23`）。无可观察集合、无逐项变更通知；视图靠 `Render()` 整页重绘（`DesktopWorkspaceView.cs:309-311`）。差异见 D1 |
| `SelectedSceneIndex`（`ScheduleViewModel.cs:40-41`） | 当前场（索引语义） | 部分覆盖 | 改为**按名字**：`State.Scene`（`WorkspaceModels.cs:21`、`DesktopWorkspaceService.cs:21/25`）。SoT 是会话存档 JSON（`DesktopWorkspaceService.cs:87`），不是视图索引。差异见 D1 |
| `SelectedShotIndex`（`ScheduleViewModel.cs:43-44`） | 当前镜（索引语义） | 部分覆盖 | `State.Shot`（`WorkspaceModels.cs:22`）+ `SelectShotAsync`（`DesktopWorkspaceService.cs:188-197`）。差异见 D1、D3 |
| `CanUndo`（`ScheduleViewModel.cs:46`） | 是否有可撤的"上一步删除" | 部分覆盖 | 拆成两个互不相干的栈：场记级 `CanUndo`（`DesktopWorkspaceService.cs:36`，`_undo` 上限 100，`DesktopWorkspaceService.cs:168`）与计划级 `CanRestorePlan`（`DesktopWorkspaceEditing.cs:72`，单槽 `_deletedPlan`，`DesktopWorkspaceEditing.cs:71`）。VM 的 `CanUndo` 只覆盖计划删除 |
| `HasScenes`（`ScheduleViewModel.cs:48`） | 空计划判定 | 覆盖 | 现役直接在视图里判 `_service.Scenes.Count`（`DesktopWorkspaceView.cs:314`、`DesktopWorkspaceView.cs:331`），无需派生属性 |
| `SelectedScene`（`ScheduleViewModel.cs:51-52`） | 当前场对象（越界返回 null） | 部分覆盖 | `Scene`（`DesktopWorkspaceService.cs:25`）按 `State.Scene` 名字查，越界/改名后为 null，由 `InitializeAsync` 兜底（`DesktopWorkspaceService.cs:57-61`）。VM 是"索引夹取→null"，语义不同但都为空安全 |
| `SelectedSceneShots`（`ScheduleViewModel.cs:55`） | 当前场镜列表（右列 ItemsSource） | 部分覆盖 | 现役视图自行 `selected.Where(sh => Matches(...))` 过滤后逐条建卡（`DesktopWorkspaceView.cs:345-369`）；搜索/状态筛选在 UI 层（`DesktopWorkspaceView.cs:304-307`），不进 service |
| `AddSceneCommand`（`ScheduleViewModel.cs:60`） | "场次+"命令壳 | 部分覆盖 | `＋ 场次` 按钮（`DesktopWorkspaceView.cs:295`）→ `ShowPlanEditor(null,true)`（`DesktopWorkspaceView.cs:533`）→ `SavePlanItemAsync`（`DesktopWorkspaceService.cs:261-292`）。差异见 D4、D5 |
| `AddShotCommand`（`ScheduleViewModel.cs:63`） | "镜头+"命令壳 | 部分覆盖 | `＋ 添加镜头`（`DesktopWorkspaceView.cs:369`）→ `SavePlanItemAsync(..., isScene:false)`（`DesktopWorkspaceService.cs:276-280`）。差异见 D4 |
| `DeleteItemCommand`（`ScheduleViewModel.cs:66`） | 按 kind 删场/镜 | 部分覆盖 | `DeletePlanItemAsync`（`DesktopWorkspaceEditing.cs:73-92`）。差异见 D2 |
| `EditItemCommand`（`ScheduleViewModel.cs:69`） | 编辑命令——**本身是空壳**（`ScheduleViewModel.cs:33` `_ => { }`） | 部分覆盖 | 编辑能力由视图弹窗 `ShowPlanEditor` 承担（`DesktopWorkspaceView.cs:533-573`）+ `SavePlanItemAsync`（`DesktopWorkspaceService.cs:261`）。VM 侧无逻辑，删除不丢功能 |
| `MoveItemCommand`（`ScheduleViewModel.cs:72`） | 下移（`SelectedIndex+1`） | 部分覆盖 | `MovePlanItemAsync(scene,shot,+1)`（`DesktopWorkspaceEditing.cs:109-126`）。差异见 D7 |
| `OnSelectedSceneIndexChanged`（`ScheduleViewModel.cs:100-113`） | 选场→会话场/镜/次归位 | 部分覆盖 | `SelectShotAsync`（`DesktopWorkspaceService.cs:188-197`）。差异见 D3 |
| `OnSelectedShotIndexChanged`（`ScheduleViewModel.cs:115-125`） | 选镜→次归 0 | 部分覆盖 | `SelectShotAsync` 重算次号：`DefaultIfEmpty(0).Max() + 1`（`DesktopWorkspaceService.cs:194`），比"次归 0"更强 |
| （合并行）会话反向回填：`OnSessionPropertyChanged`（`ScheduleViewModel.cs:127-138`）+ `SyncSelection`（`ScheduleViewModel.cs:140-171`） | 会话改选→VM 索引跟随（双向联动、`_syncingSelection` 防回环） | 缺失 | 现役没有"第二份视图索引"这个概念：`State.Scene/State.Shot` 就是唯一真相，`Changed` 事件触发重绘（`DesktopWorkspaceService.cs:109`）。VM 那套防回环机制无等价物，也不需要 |
| `LoadAsync`（`ScheduleViewModel.cs:176-192`） | 重载 + 按会话夹取选择 + `HasScenes` 通知 | 部分覆盖 | `InitializeAsync`（`DesktopWorkspaceService.cs:42-64`）+ `MigratePlanAndLogs`（`DesktopWorkspaceEditing.cs:31-57`）+ `EnsureDate`（`DesktopWorkspaceService.cs:70-77`）。现役多出：StableId 补发、草稿键从 `场/镜` 迁到 `StableId`、场记归属回填、高水位文件号。差异见 D8 |
| `ImportCsvAsync`（`ScheduleViewModel.cs:195-207`） | CSV 导入（**全量替换**） | 部分覆盖 | `ParsePlan`（`DesktopWorkspaceService.cs:310-345`）+ `MergePlanAsync`（`DesktopWorkspaceService.cs:293-309`）。**替换 vs 合并**是本行最大差异，见 D6 |
| `ImportCsv` `[RelayCommand]`（`ScheduleViewModel.cs:209-210`） | 流式导入命令壳（`AllowConcurrentExecutions=false`） | 部分覆盖 | 视图 `ImportPlan`（`DesktopWorkspaceView.cs:620-632`）：文件选择 + "解析到 N 场 M 镜"确认 + Toast；**串行保护无等价物**（service 靠 `_gate`，`DesktopWorkspaceService.cs:16/92`） |
| `AddScene()`（`ScheduleViewModel.cs:215-226`） | 克隆当前场 note、`NextKey` 自动编号、塞占位镜 | 部分覆盖 | `SavePlanItemAsync` 新建场分支（`DesktopWorkspaceService.cs:268-273`）。差异见 D4、D5 |
| `FinalizeNewScene(int)`（`ScheduleViewModel.cs:229-242`） | 二阶段新建第 3 步：按 objects 合成 `从{a}的【正面】拍【近景】` 重写占位镜 | 缺失 | 无等价物。`DesktopWorkspaceService.cs:272` 新建场只塞 `new ScheduleItem("1","A", new Note([], "近景", ""))`，**不做文案合成**。单弹窗流程（`DesktopWorkspaceView.cs:533-546`）把两阶段压成一次提交，因此"第 3 步"无处安放，规则本身丢了 |
| `AddShot()`（`ScheduleViewModel.cs:245-263`） | 克隆末镜 + `NextKey` 编号 + 追加到末尾 | 部分覆盖 | `SavePlanItemAsync` 非场分支（`DesktopWorkspaceService.cs:276-280`）：`oldName=null` 时 `index<0` → `target.Insert(clamp(insertIndex ?? Count))`，默认也追加到末尾（语义一致）；但**编号靠手输**且必须过正则（`DesktopWorkspaceService.cs:263-264`）。差异见 D4 |
| `ApplySceneEdit(int, ScheduleItem)`（`ScheduleViewModel.cs:267-286`） | 原子替换场信息；**不查重名**（只接住 DataList 的镜级重名） | 部分覆盖 | `SavePlanItemAsync(isScene:true, oldName:...)`（`DesktopWorkspaceService.cs:270-273`）：**会**查场号重名（"场号已存在"），并保留 `existing.Info.StableId`（`DesktopWorkspaceService.cs:273`）。VM 侧整体替换 `SceneSchedule` 会**丢掉 Info.StableId**。差异见 D9 |
| `ApplyShotEdit(int,int,ScheduleItem)`（`ScheduleViewModel.cs:288-312`） | 回写单镜；重名返回 false | 部分覆盖 | `SavePlanItemAsync(isScene:false, oldName:...)`（`DesktopWorkspaceService.cs:278-279`）：重名由 `DataList.Insert`/索引器抛出并被 `Change` 回滚（`DesktopWorkspaceService.cs:102-107`）；VM 是静默 `false`（`ScheduleViewModel.cs:305-308`），差异见 D9 |
| `DeleteScene(int, out string? message)`（`ScheduleViewModel.cs:317-353`） | 删场：**至少保留一场守卫** + 选择上移 + 镜/次归 0 + 单槽 undo + 中文提示 | 部分覆盖 | `DeletePlanItemAsync(sceneName, null)`（`DesktopWorkspaceEditing.cs:73-79`）。**无"至少保留 1 场"守卫**；提示语义、选择复位、undo 槽语义均不同。差异见 D2 |
| `DeleteShot(int, out string? message)`（`ScheduleViewModel.cs:356-396`） | 删镜：**至少保留一镜守卫** + 选择上移 + 次归 0 + 单槽 undo + 中文提示 | 部分覆盖 | `DeletePlanItemAsync(sceneName, shotName)`（`DesktopWorkspaceEditing.cs:80-85`）。**无"至少保留 1 镜"守卫**。其余同 D2 |
| `Undo()` `[RelayCommand]`（`ScheduleViewModel.cs:399-417`） | 撤销上一步删除（场/镜两种记录） | 部分覆盖 | `RestorePlanItemAsync`（`DesktopWorkspaceEditing.cs:93-108`）：插入位置同为 `min(index, count)`（`DesktopWorkspaceEditing.cs:99` / `:104`），但**多两道守卫**——同编号场已存在则拒（`DesktopWorkspaceEditing.cs:98`）、所属场已删则拒（`DesktopWorkspaceEditing.cs:103`）；恢复后仅在 `Shot is null` 时才回填选择（`DesktopWorkspaceEditing.cs:107`）。VM 无这些守卫也无选择回填。差异见 D7 |
| `MoveScene(int,int)`（`ScheduleViewModel.cs:421-445`） | **任意目标索引**重排（Reorderable 语义：`newIndex>oldIndex` 先 -1）+ 选中随拖拽项 + 镜/次归 0 | 部分覆盖 | `MovePlanItemAsync(scene, null, ±1)`（`DesktopWorkspaceEditing.cs:113-118`）：只支持相邻 ±1、无 Reorderable 减一语义、不跟随选择（因为按名字选，移动不影响选中）。差异见 D7 |
| `MoveShot(int,int)`（`ScheduleViewModel.cs:447-475`） | 同上（镜级） | 部分覆盖 | `MovePlanItemAsync(scene, shot, ±1)`（`DesktopWorkspaceEditing.cs:119-124`）。同上 |
| `PersistAsync()`（`ScheduleViewModel.cs:479`） | 显式落库：`_store.SaveAllAsync(Scenes.ToList())` | 部分覆盖 | `Change(...)` 包裹（`DesktopWorkspaceService.cs:90-110`）+ `Persist()`（`DesktopWorkspaceService.cs:84-89`）。差异是本报告最实质的一条，见 D8 |

### 差异明细

**D1 选择模型（索引 ↔ 名字）**
- VM：`SelectedSceneIndex`/`SelectedShotIndex` 是视图索引，越界靠夹取（`ScheduleViewModel.cs:186-188`、`:204`），选中变化要显式同步到 `RecordingSessionViewModel`（`ScheduleViewModel.cs:100-125`）。
- 现役：`State.Scene`/`State.Shot` 是**名字字符串**并随会话存盘（`DesktopWorkspaceService.cs:87`），`Scene`/`Shot` 为只读查表（`DesktopWorkspaceService.cs:25-26`）。副作用差异：改名后 VM 的索引不变但指向不同条目；现役在场次改名时**重写草稿键并保留选中**（`DesktopWorkspaceService.cs:282-287`），这是 VM 完全没有的能力。

**D2 删除：守卫、提示、副作用**（对应专项核查 ④）
- 触发条件与守卫：VM 在 `Scenes.Count <= 1` 时返回失败 + `"至少保留一个场"`（`ScheduleViewModel.cs:319-323`），镜级同理 `"至少保留一个镜"`（`ScheduleViewModel.cs:365-369`）。现役 `DeletePlanItemAsync` **没有任何数量守卫**（`DesktopWorkspaceEditing.cs:73-92`）：可以把场删到 0、把某场镜删到 0。后果不是立即崩溃，而是把不变量推给下游——`SaveTakeAsync` 抛 `"请先添加并选择拍摄计划"`（`DesktopWorkspaceService.cs:149`），`ParsePlan` 也要求每场至少一镜（`DesktopWorkspaceService.cs:343`）。**不存在**"还有镜次/场记引用就不许删"这类业务规则——两边都没有；现役只是保留了历史场记（弹窗文案："已保存场记会保留"，`DesktopWorkspaceView.cs:565`），并通过 `StableId` 让被删条目与其历史场记**解绑**（`MatchesShot`，`DesktopWorkspaceEditing.cs:19-23`：`planned?.StableId == row.Item.ShotId` 在 planned 为 null 时恒假）。
- 提示语义：VM 走 `out string? message` 且区分"守卫提示"（非空 + false）与"静默失败"（null + false，如越界，`ScheduleViewModel.cs:325-329`、`:371-375`），成功文案带条目名：`第 {removed.Info.Name} 场已删除`（`ScheduleViewModel.cs:350`）、`第 {removed.Name} 镜已删除`（`ScheduleViewModel.cs:393`）。现役统一抛 `InvalidOperationException("场次已不存在"/"镜头已不存在")`（`DesktopWorkspaceEditing.cs:75`、`:83`），成功提示是视图里的**静态** Toast（"计划条目已删除，可在计划页恢复"，`DesktopWorkspaceView.cs:566`）——不再回显被删条目编号。
- 选择/次号联动：VM 删除后按"删的是不是选中末项/是否在选中之前"分三种情形修正索引并 `SelectShot(0)`/`SelectTake(0)`（`ScheduleViewModel.cs:333-343`、`:378-388`）。现役只在删完后发现 `Scene is null || Shot is null` 才兜底选第一个有效场/镜（`DesktopWorkspaceEditing.cs:86-90`），**不重置草稿与次号**——因为草稿按 `StableId` 存（`DesktopWorkspaceService.cs:27`），删除不影响其他条目的次号，这其实更正确。
- 副作用（**新增风险**）：`DeletePlanItemAsync` 结尾 `_undo.Clear()`（`DesktopWorkspaceEditing.cs:91`）会**清空场记级撤回栈**——用户在计划页删掉一个条目，之前攒的最多 100 步"撤回上一条场记"全部失效（`CanUndo` 变 false，`UndoAsync` 抛 `"没有可以撤回的场记"`，`DesktopWorkspaceService.cs:178`）。VM 的删除只动计划，不触碰会话侧栈。
- undo 语义：VM 单槽且**互斥**——删场清 `_undoShot`、删镜清 `_undoScene`（`ScheduleViewModel.cs:346-347`、`:390-391`），只能撤最近一次删除。现役同样单槽（`_deletedPlan`，`DesktopWorkspaceEditing.cs:71`）但**不互斥也无覆盖保护**，且不进 `Persist()`（`DesktopWorkspaceService.cs:84-89` 只写 `State` 与 `_undo`）→ 重启即失效（VM 本来也不持久化，等价）。

**D3 选择联动**（对应专项核查 ① 的"选择联动"维度）
- VM：双向。VM→会话：选场把镜/次归 0（`ScheduleViewModel.cs:110-112`）、选镜把次归 0（`ScheduleViewModel.cs:123-124`）；会话→VM：`OnSessionPropertyChanged` + `SyncSelection` 直写 backing field 并 `_syncingSelection` 防回环（`ScheduleViewModel.cs:140-171`）。
- 现役：单向阀门。`SelectShotAsync` 校验目标存在（不存在抛 `"镜头不在当前计划中"`，`DesktopWorkspaceService.cs:190-191`）、重复选中时短路（`DesktopWorkspaceService.cs:192`）、置 `State.Linked = true` 并**清空场记撤回栈**（`DesktopWorkspaceService.cs:193`）、按历史场记重算次号 `max(Tk)+1`（`DesktopWorkspaceService.cs:194-195`）。还多出 VM 没有的平移导航 `NextShotAsync`（`DesktopWorkspaceService.cs:198-204`，末尾抛 `"已到计划末尾..."`）与进度归类 `Progress`（`DesktopWorkspaceService.cs:205-211`）。VM 的"次归 0"变成"按历史重算"，这是行为性变化，但不属于计划页职责。

**D4 编号生成（`NextKey`）**（对应专项核查 ②）
- VM：`NextKey(keys) = (max(int.TryParse → max+1).ToString())`，非数字键按 0 计（`ScheduleViewModel.cs:484-488`），由 `AddScene`（`:218`）与 `AddShot`（`:259`）自动生成"最大序号+1、fix 空"。
- 现役：`SavePlanItemAsync` 要求**手输**名称并过正则 `^(\d{1,4})([A-Z]?)$`，不合规抛 `"编号需要 1–4 位数字加可选字母，例如 12A"`（`DesktopWorkspaceService.cs:263-264`）。视图输入框占位符就是"例如 12A"（`DesktopWorkspaceView.cs:537`）。**自动递增编号能力整体消失**，连点"场次+"就能得到 1、2、3 的工作流不复存在。

**D5 新建场的默认值与模板**（对应专项核查 ②）
- 克隆：VM 新场 Note 克隆"当前场"（`ScheduleViewModel.cs:217-219` + `CloneNote` `:490`）；现役 Note 全空（`DesktopWorkspaceService.cs:272`）。
- 占位镜命名：VM 是 `new ScheduleItem("1", "", ...)`（名 `1`，`ScheduleViewModel.cs:220`）；现役是 `new ScheduleItem("1", "A", ...)`（名 `1A`，`DesktopWorkspaceService.cs:272`）。这不是纯显示差异——`MatchesShot` 在缺 `ShotId` 时按 `Scn/Sht` 名字匹配（`DesktopWorkspaceEditing.cs:22`），老的"1 镜"场记与新的"1A 镜"对不上。
- StableId：VM 全程不碰 `StableId`（新建即空串）；现役每次新建都发新 GUID（`DesktopWorkspaceService.cs:266`、`:272`、`:299`、`:303`）。VM 路径下计划条目无稳定身份，这是它无法承担现役场记归属模型的结构性缺陷——不是"逻辑待迁移"，而是"逻辑已过时"。

**D6 CSV 导入：替换 vs 合并**（对应专项核查 ③）
- VM：`ImportCsvAsync` **无条件全量替换**——`Scenes.Clear()` 后逐场 Add（`ScheduleViewModel.cs:198-202`），再 `PersistAsync()` 整表重写。**同名场次不是覆盖也不是追加，而是旧的全部消失**。注释也写明"全量替换，对齐原版导入路径"（`ScheduleViewModel.cs:194`）。
- 现役：`MergePlanAsync`（`DesktopWorkspaceService.cs:293-309`）——同名场**保留旧场、镜级 upsert**：同名镜就地替换并继承旧 `StableId`（`:303`），新镜追加到该场末尾（`:303`），场备注仅在导入侧 `Note.Type` 或 `Note.Append` 非空时才覆盖（`:305`）；不存在的场整场追加到末尾并重新发 StableId（`:299`）。UI 弹窗文案与之相符："同编号更新内容，其余计划保留"（`DesktopWorkspaceView.cs:628`）。
- 解析器也不同：VM 依赖 `CsvScheduleParser`（`ScheduleCsvParser.cs:31-39`，**无条件丢首行** `:57-62`，7 列，场景/镜按 num-fix 去重，objects 默认 `["Boom"]`）；现役用静态 `ParsePlan`（`DesktopWorkspaceService.cs:310-345`，**仅当首行像表头才丢** `:319`，支持 4 列简表与 7 列工程两种格式 `:320-321`，末尾强制"每场至少一镜" `:343`）。同名场在 `ParsePlan` 内部已合并（`:327-333`），且重复镜号就地覆盖（`:340-341`）。

**D7 重排与 undo**（对应专项核查 ①）
- 序号重排：**两边都不重编号**。VM 的 `MoveScene`/`MoveShot` 只做集合搬移（`ScheduleViewModel.cs:438-440`、`:470-471`），`Key/Fix` 原样保留；所谓"序号重排"= 显示顺序，落库靠 `LiteDbScheduleStore` 的 `Seq` 字段（`ScheduleStore.cs:60-77`，`OrderBy(Seq)` `:53`）。现役 `MovePlanItemAsync` 同样只搬移（`DesktopWorkspaceEditing.cs:117`、`:123`），落库同样走 `SaveAllAsync`→`Seq`。
- 能力差：VM 支持**任意目标索引**并实现 Reorderable 的 `newIndex>oldIndex → newIndex-1` 修正（`ScheduleViewModel.cs:428-431`、`:460-463`）；现役只支持 `direction ∈ {-1,1}` 的相邻移动，非法方向抛 `"移动方向无效"`（`DesktopWorkspaceEditing.cs:111`），越界则**静默 return**（`DesktopWorkspaceEditing.cs:116`、`:122`）。现役视图因此只提供"上移/下移"按钮（`DesktopWorkspaceView.cs:559-560`），**拖拽式任意重排的入口没有了**（要任意插位只能用"前面插入/后面插入"再填编号，`DesktopWorkspaceView.cs:561-562`）。
- undo：两边**都不能撤移动**。VM 的移动不清空 `_undoScene/_undoShot`（`ScheduleViewModel.cs:421-445` 未触碰），所以"删场→移动→再点撤销"会把移动一起跳过去，属于 VM 自身的语义瑕疵；现役 `MovePlanItemAsync` 也不碰 `_deletedPlan` 与 `_undo`，表现类似但至少不会清空场记栈。

**D8 写库时机与原子性**（对应专项核查 ⑤）
- VM：`PersistAsync()` 就是一句 `_store.SaveAllAsync(Scenes.ToList())`（`ScheduleViewModel.cs:479`）。调用点除 `ImportCsvAsync` 外**全部没有 await**（`ScheduleViewModel.cs:224`、`:241`、`:261`、`:284`、`:310`、`:351`、`:394`、`:416`、`:444`、`:474`）→ fire-and-forget：异常被吞（`LiteDbScheduleStore.SaveAllAsync` 是 `DeleteAll()`+逐条 `Insert`（`ScheduleStore.cs:60-77`），场号重复会直接抛 LiteDB 键冲突，而 `ApplySceneEdit` 允许重名场，见 D9），多次写入也无序竞争（无门闩，`DeleteAll` 与 `Insert` 可能交错，留下半张表）。
- 现役：每次变更都在 `Change(...)` 里（`DesktopWorkspaceService.cs:90-110`）：`SemaphoreSlim` 串行（`:16`、`:92`）、事务 `Begin/Commit/Rollback`（`:98-104`）、失败回滚 `State` + `Scenes` + `_undo` + `_deletedPlan`（`:105`）、成功后才 `Changed?.Invoke()`（`:109`）。落库还多写两份会话 JSON：`State`（含 `Scene/Shot/Drafts/FileCounters`）与 `_undo`（`DesktopWorkspaceService.cs:84-89`），并 `ReloadLogs()`（`:99`）。另有 `EnsureDate` 跨日切换（`:70-77`）与 `NextAvailableNumber` 高水位（`:65-69`），VM 完全没有。
- 结论：VM 是"每次改都重写整表，但不保证成功也不保证顺序"；现役是"每次改都原子、有序、可回滚，且顺带维护会话/草稿/流水号"。

**D9 异常面与重名规则**
- 场名重名：VM **不检查**（`ApplySceneEdit` 只 `try/catch DuplicateItemException`，而 `new SceneSchedule([..], newInfo)` 只校验镜级重名，`SlateSchedule.cs:99-105`；测试 `ScheduleViewModelTests.cs:222-225` 明确记录"场级编辑不做重名检测"）；现役检查并抛 `"场号已存在"`（`DesktopWorkspaceService.cs:270`）。
- 镜名重名：VM 返回 `false`（`ScheduleViewModel.cs:305-308`、`:279-282`），由视图提示"本镜号已存在"；现役让 `DuplicateItemException("Duplicate items in the list")` 逃出 `Change`（`SlateSchedule.cs:117/126`），视图直接把英文 `ex.Message` 写进错误标签（`DesktopWorkspaceView.cs:550`）。**用户可见文案发生退化**（英文化）。
- objects 输入：VM 收 `Note.Objects` 列表（`ScheduleViewModel.cs:267`）；现役收字符串并按 `,`/`，`/`/`/`、` 拆分+去空+Trim（`DesktopWorkspaceService.cs:265`）。
- 空名/非法号：VM 靠 `NextKey` 保证合法；现役靠正则（`DesktopWorkspaceService.cs:263-264`）。

### 专项核查逐条结论

1. **`MoveScene`/`MoveShot` 的序号重排与 undo**：重排能力**部分覆盖且退化**——现役只有相邻 ±1（`DesktopWorkspaceEditing.cs:109-126`），无任意索引、无 Reorderable 减一语义、无拖拽入口；`SavePlanItemAsync(..., insertIndex)` 只是"插入位置"（`DesktopWorkspaceService.cs:272`、`:279`，`Math.Clamp` 到 `[0,Count]`），**不能替代移动**，且编辑既有条目时 `insertIndex` 被忽略（走 `existing.Info = replacement` / `target[index] = replacement`）。序号本身两边都不重编号。undo：VM 与现役**都不支持撤销移动**；VM 靠单槽删除快照，现役靠单槽 `_deletedPlan`，都不持久化。
2. **`AddScene`/`FinalizeNewScene` 两阶段新建**：流程被**压平为一次弹窗提交**（`DesktopWorkspaceView.cs:533-546`），因此两阶段的"中间态草稿场"概念消失；随之丢失三件事——克隆当前场 Note（`ScheduleViewModel.cs:219`）、`NextKey` 自动编号（`:218`）、按 objects 合成占位镜文案 `从{a}的【正面】拍【近景】`（`:238-239`，判定**缺失**）。
3. **`ImportCsvAsync` 合并策略**：VM 是**全量替换**（`ScheduleViewModel.cs:197-202`），同名场次直接消失；现役是**合并 + 同名镜覆盖 + 异名镜追加**（`DesktopWorkspaceService.cs:298-306`），且场备注按"导入非空才覆盖"规则写回（`:305`）。方向相反，迁移时不能简单等价替换。
4. **`DeleteScene`/`DeleteShot` 的 `out string? message` 语义**：三件事随 VM 一起消失——① "至少保留一个场/镜"守卫（`ScheduleViewModel.cs:319-323`、`:365-369`）现役**完全没有**；② 成功文案回显条目编号（`:350`、`:393`）退化为静态 Toast（`DesktopWorkspaceView.cs:566`）；③ 删除后的选择上移与镜/次归零（`:333-343`、`:378-388`）退化为"仅当选中悬空才兜底"（`DesktopWorkspaceEditing.cs:86-90`）。**没有**任何"本条还有场记/还有镜次就不许删"的业务规则——两版都没有；差异只在于现役保留历史场记并让它们与计划解绑（`DesktopWorkspaceEditing.cs:19-23`）。另需注意现役删除会顺手清空场记撤回栈（`:91`）。
5. **`PersistAsync` 与 service 写库时机**：VM 是**未 await 的全表重写**（`ScheduleViewModel.cs:479`，调用点 `:224`/`:241`/`:261`/`:284`/`:310`/`:351`/`:394`/`:416`/`:444`/`:474`），失败无回滚、并发无序；现役是**门闩 + 事务 + 回滚 + 会话 JSON 同写 + 场记重载**（`DesktopWorkspaceService.cs:90-110`、`:84-89`）。这不是"覆盖度"问题而是"现役严格更强"，`PersistAsync` 无需迁移。

## 删除会丢掉的东西

1. **计划编号自动递增**：`NextKey`（`ScheduleViewModel.cs:484-488`）及两处调用（`ScheduleViewModel.cs:218`、`:259`）。删除后"场次+/镜头+"必须手输编号，且受 `^(\d{1,4})([A-Z]?)$` 强约束（`DesktopWorkspaceService.cs:263-264`）。
2. **"至少保留一个场"守卫**（`ScheduleViewModel.cs:319-323`）与**"至少保留一个镜"守卫**（`ScheduleViewModel.cs:365-369`）。现役 `DeletePlanItemAsync` 无数量检查（`DesktopWorkspaceEditing.cs:73-92`），可把计划删空，随后 `SaveTakeAsync` 抛 `"请先添加并选择拍摄计划"`（`DesktopWorkspaceService.cs:149`）。
3. **新场克隆当前场备注**：`ScheduleViewModel.cs:217-219`（+ `CloneNote` `:490`）；现役新场 `Note` 全空（`DesktopWorkspaceService.cs:272`）。
4. **新场占位镜命名 `1`（无 fix）**：`ScheduleViewModel.cs:220`；现役为 `1A`（`DesktopWorkspaceService.cs:272`）——影响与历史场记的名字匹配（`DesktopWorkspaceEditing.cs:22`）。
5. **`FinalizeNewScene` 的镜头文案合成规则**：`从{objects}的【正面】拍【近景】`（`ScheduleViewModel.cs:238-239`）。现役无等价实现。
6. **删除后的选择上移 + 镜/次归零**：`ScheduleViewModel.cs:333-343`（场）、`:378-388`（镜）。现役只做悬空兜底（`DesktopWorkspaceEditing.cs:86-90`）。
7. **删除成功提示回显条目编号**：`第 {removed.Info.Name} 场已删除`（`ScheduleViewModel.cs:350`）、`第 {removed.Name} 镜已删除`（`ScheduleViewModel.cs:393`）；现役为静态 Toast（`DesktopWorkspaceView.cs:566`）。
8. **守卫提示与静默失败可区分**的 `out message` 契约（非空=业务拒绝，null=越界静默；`ScheduleViewModel.cs:325-329`、`:371-375`）。现役一律抛 `InvalidOperationException`（`DesktopWorkspaceEditing.cs:75`、`:83`）。
9. **单槽删除 undo 的互斥语义**：删场清镜槽、删镜清场槽（`ScheduleViewModel.cs:346-347`、`:390-391`），配合两种记录类型 `DeletedScene`/`DeletedShot`（`ScheduleViewModel.cs:492-494`）。现役单槽无互斥，但多出两道恢复守卫（`DesktopWorkspaceEditing.cs:98`、`:103`），净效果不劣。
10. **场级改名不查重名的宽松语义**：`ScheduleViewModel.cs:274-282`（测试 `ScheduleViewModelTests.cs:222-225` 把"原版 saveChanges 不做重名检测"固化为契约）。现役更严（`DesktopWorkspaceService.cs:270`）——方向上是改善，但若有人依赖旧行为（同名场并存）会感知为被拒。
11. **CSV 全量替换导入**：`ScheduleViewModel.cs:198`；现役是合并（`DesktopWorkspaceService.cs:293-309`）。
12. **选择索引双向回填与防回环**：`ScheduleViewModel.cs:127-171`。现役以 `State.Scene/State.Shot` 为唯一真相，机制层面没有对应物。
13. **镜级重名的静默 false + 中文提示**：`ScheduleViewModel.cs:305-308`；现役抛英文 `Duplicate items in the list` 并显示在对话框错误行（`DesktopWorkspaceView.cs:550`）。

> 没有信息量的成员：`EditItemCommand` 的 `Execute` 是空实现（`ScheduleViewModel.cs:33`），删除它不丢任何行为；`SyncSelection`/`ValidSelection`/`CloneNote`/`DeleteByKind`/`MoveByKind` 均为上面条目的从属私有实现。

## 必须先迁移的项

按风险从高到低。每条给建议签名（均落在 `DesktopWorkspaceService` / `DesktopWorkspaceEditing`）。

1. **【高】删除守卫：至少保留 1 场 1 镜**
   现役可把计划删空，直接把记录页打瘫（`DesktopWorkspaceService.cs:149` 抛错；`DesktopWorkspaceService.cs:343` 的导入不变量也就此可被绕过）。这是唯一会造成"当场没法干活"的缺口。
   建议：把守卫放进写边界，保持异常面统一。
   ```csharp
   // DesktopWorkspaceEditing.cs
   public Task DeletePlanItemAsync(string sceneName, string? shotName)   // 内部加守卫：
   //   shotName is null && Scenes.Count <= 1 → InvalidOperationException("至少保留 1 场，请先新增场次")
   //   shotName is not null && scene.Count <= 1 → InvalidOperationException("至少保留 1 镜，请先新增镜头")
   ```
   若需要"可判断不抛异常"的 UI 预检，再加只读谓词：
   ```csharp
   public bool CanDeletePlanItem(string sceneName, string? shotName);
   ```

2. **【高】新建编号自动递增（`NextKey` 等价物）**
   现役强制手输 + 正则（`DesktopWorkspaceService.cs:263-264`），是日常最高频操作的退化；且误输即抛异常。
   ```csharp
   // DesktopWorkspaceService.cs —— 供 ShowPlanEditor 预填输入框
   public string NextSceneName();                       // max(数字场号)+1，非数字按 0
   public string NextShotName(string sceneName);        // 该场 max(数字镜号)+1
   // 期望"一键追加"时：
   public Task<SceneSchedule> AppendSceneAsync(SceneSchedule? template = null);  // template 用于克隆 note
   public Task<ScheduleItem> AppendShotAsync(string sceneName);                 // 克隆末镜 note
   ```

3. **【高】删计划条目不应清空场记撤回栈**
   `DesktopWorkspaceEditing.cs:91` 的 `_undo.Clear()` 会把最多 100 步场记撤回一次清光（`DesktopWorkspaceService.cs:167-168`），用户感知为"撤回按钮突然灰了/抛'没有可以撤回的场记'"。
   ```csharp
   public Task DeletePlanItemAsync(string sceneName, string? shotName, bool clearTakeUndo = false);
   // 默认 false：删计划不再牵连场记撤回栈；备份导入等真正的重写路径显式传 true
   ```

4. **【中】新建场的模板语义：克隆当前场备注 + 占位镜文案合成**
   现役新场是空白 Notes + `1A` 占位镜（`DesktopWorkspaceService.cs:272`），连带丢了 `从{a}的【正面】拍【近景】`（`ScheduleViewModel.cs:238-239`）。若"场次+"仍是主要建计划方式，这属于可见的功能缺失。
   ```csharp
   public Task<SceneSchedule> AppendSceneAsync(string? cloneNoteFromScene = null);
   public Task FinalizeNewSceneAsync(string sceneName);   // 依据 Info.Note.Objects 重写 "1" 镜 Append
   ```

5. **【中】导入语义：补一条"全量替换"入口（或明确废弃该用法）**
   VM 是替换（`ScheduleViewModel.cs:198`），现役只有合并（`DesktopWorkspaceService.cs:293-309`）。若仍有用户/脚本按"替换"心智导表，会把旧场次残留下来，且无法通过重导修正编号。
   ```csharp
   // DesktopWorkspaceService.cs
   public Task ReplacePlanAsync(IReadOnlyList<SceneSchedule> imported);  // 清空后写入，内部复用 Change
   ```
   或者在导入弹窗里显式提供"合并 / 替换"二选一（当前文案只承诺合并，`DesktopWorkspaceView.cs:628`）。

6. **【低】删除提示文案与失败原因回传**
   现役成功 Toast 不含条目编号（`DesktopWorkspaceView.cs:566`），失败只靠异常文本。
   ```csharp
   public readonly record struct PlanDeleteResult(bool Ok, string Scene, string? Shot, string? Message);
   public Task<PlanDeleteResult> TryDeletePlanItemAsync(string sceneName, string? shotName);
   ```

7. **【低】任意索引重排（仅当要恢复拖拽交互）**
   现役只 ±1（`DesktopWorkspaceEditing.cs:109-126`）；若产品仍要"拖到第 N 位"，需要在 service 加索引版，避免 UI 循环调用 ±1 造成 N 次落库与 N 次 `Changed`。
   ```csharp
   public Task MovePlanItemAsync(string sceneName, string? shotName, int fromIndex, int toIndex); // Reorderable 语义在服务端归一
   ```

8. **【低】镜级重名提示本地化**
   现役把 `Duplicate items in the list` 直接显示给用户（`SlateSchedule.cs:126` → `DesktopWorkspaceView.cs:550`）；VM 侧对应的是"本镜号已存在"（`ScheduleViewModel.cs:281`）。
   ```csharp
   // SavePlanItemAsync 内把 DataList 抛出的 DuplicateItemException 转成：
   throw new InvalidOperationException(isScene ? "场号已存在" : "本镜号已存在");
   ```

> 明确**不需要**迁移：`PersistAsync`（现役 `Change`+`Persist` 严格更强）、`SyncSelection`/`OnSessionPropertyChanged`（索引回填模型已被名字模型取代）、`EditItemCommand`（空壳）、`LoadAsync` 的 `ct` 使用（现役 `Change` 已收敛）。

## 测试锚点

**`tests/VoiSlate.Tests/ViewModels/ScheduleViewModelTests.cs`** —— 12 个 `[Fact]`，全部直接构造 `ScheduleViewModel`（harness 见 `:20-33`：`StubScheduleStore` + `StubCsvScheduleParser` + 真实 `RecordingSessionViewModel`）。删除 VM 时这些用例会一起失败/消失：

| 用例（行号） | 保护的逻辑 |
|---|---|
| `Load_Populates_Scenes_And_Syncs_Selection`（`:43-54`） | 载入填充 `Scenes`，且选中以会话现有选择为准（会话 SelectScene(1) 后重载仍取 1） |
| `ImportCsv_Replaces_Scenes_And_Persists`（`:56-68`） | CSV 导入是**整表替换**（导入前 1 场、导入后只剩 "2A"）且写库快照含新场——即 D6 的替换语义 |
| `AddScene_Clones_Current_Scene_With_Next_Key_And_Placeholder_Shot`（`:70-86`） | `NextKey` 自动编号（新增 "3"、fix 空）、**克隆当前场 Note**（Append="过场"）、占位镜 key "1" 且 Type="近景"、落库 |
| `FinalizeNewScene_Rebuilds_Default_Shot_From_Objects`（`:88-101`） | 二阶段第 3 步：按 objects 合成 `从缪尔赛斯，塞雷娅的【正面】拍【近景】`（正是现役**缺失**的那条规则） |
| `AddShot_Clones_Last_Shot_With_Next_Key`（`:103-116`） | 镜头+ 克隆末镜 note、`NextKey` 递增（"3"）、fix 空、追加到末尾 |
| `DeleteScene_Guards_Last_Scene_And_Adjusts_Session_Index`（`:122-141`） | 删场的三种索引修正分支（非末位删除 → 选中减 1；删"选中末场" → 上移并镜/次归 0；剩 1 场 → 返回 false 且 message="至少保留一个场"） |
| `DeleteHook_Undo_Flag_And_Undo_Restores`（`:143-156`） | 删场后 `CanUndo` 为真，`UndoCommand` 复原到原索引（"2A" 回到 index 1）并清标志 |
| `DeleteShot_Guards_Last_Shot_And_Undo_Restores`（`:158-173`） | 删镜守卫（message="至少保留一个镜"）+ 删镜 undo 复原 |
| `MoveScene_Adjusts_Session_To_Dragged_Index`（`:175-187`） | Reorderable 语义：`MoveScene(0,2)` 实际插到 1，且会话选中跟到 1、镜归 0 |
| `MoveShot_Adjusts_Session_Shot`（`:189-201`） | 镜级 Reorderable 同上，选中镜=1、次归 0 |
| `Selection_Change_Links_Session_And_Back`（`:203-215`） | 双向联动与防回环：VM 选场→会话场=1/镜=0/次=0；会话 `SelectShot(2)`→VM `SelectedShotIndex=2` |
| `Scene_Edit_Applies_And_Shot_Edit_Rejects_Duplicate_Name`（`:217-232`） | 场级改名**不查重**（"9B" 直接生效）；镜级改名重名返回 false，改名成功后 Name 更新 |

**现役侧对应锚点**（删除 VM 后这几条是"能力已被接住"的证据）：
- `tests/VoiSlate.Tests/Desktop/DesktopWorkspaceAlignmentTests.cs:69` `Plan_insertion_movement_delete_and_restore_preserve_order_and_history`：insertIndex 插入到第 0 位、`MovePlanItemAsync` ±1、`DeletePlanItemAsync` 不删场记且 `CanRestorePlan`、`RestorePlanItemAsync` 回到原索引、重启后顺序保持（覆盖 D7 + 单槽 undo）。
- `tests/VoiSlate.Tests/Desktop/DesktopWorkspaceAlignmentTests.cs:13` `Stable_identity_keeps_history_and_draft_after_rename_and_distinguishes_reused_names`：改名后 `StableId` 与草稿保持、删镜后重建同名镜**不**继承历史（证明 D5 的 StableId 模型是现役的承重结构）。
- `tests/VoiSlate.Tests/Desktop/DesktopWorkspaceTests.cs:129` `Csv_parses_both_formats_quoted_fields_and_merges_without_duplicate_shots`：合并式导入（D6 现役行为）。
- `tests/VoiSlate.Tests/Desktop/DesktopWorkspaceTests.cs:141` `Empty_plan_survives_reload_and_can_be_rebuilt_without_losing_logs`：**空计划可存活**——反证现役确实没有"至少 1 场"守卫（风险项 1），且删空后场记不丢。
- `tests/VoiSlate.Tests/Desktop/DesktopWorkspaceTests.cs:160` `Renaming_current_plan_keeps_current_draft_and_duplicate_edits_are_rejected`：现役重名编辑被拒（D9）。

> 另注：`Services/ScheduleServiceTests.cs`（`tests/VoiSlate.Tests/Services/ScheduleServiceTests.cs:1-22`）覆盖的是 `ScheduleService`——它**没有注册进 DI**，但已实现"至少 1 场 1 镜"守卫、任意索引 `MoveSceneAsync/MoveShotAsync` 与 20 步 undo（`src/VoiSlate/Services/IScheduleService.cs:190-258`、`:294-308`）。若不想在 `DesktopWorkspaceService` 里重写这些语义，可参考该类的既有实现，但其 undo 是整表快照、与现役的会话/草稿模型不同构。