# P7 ViewModel 覆盖度审计 — 与 P8 现役实现对照

> 审计日期 2026-10-26。目的：决定 P7「手机壳」时代那批 ViewModel（视图已在 `b4876db` 删除、DI 仍注册、运行时无人解析）是**直接删**还是**先迁移逻辑**。
> 对照基线：P8 的 `src/VoiSlate/Desktop/DesktopWorkspaceService.cs` 及其分部类 + `DesktopWorkspaceView.cs`。
> 明细见 `docs/p7-vm-coverage/{record,schedule,latelog,settings-main}.md`（逐成员表格、每条都带两侧 `文件:行号`）。

## 1. 结论速览

| 审计对象 | 成员数 | 覆盖 | 部分覆盖 | 缺失 |
|---|---:|---:|---:|---:|
| `RecordViewModel`（+`SlateColumnViewModel`） | 54 | 15 | 34 | 5 |
| `ScheduleViewModel` | 29 | 1 | 26 | 2 |
| `SlateLogViewModel` + `SlateLogPageViewModel` | 32 | 16 | 14 | 2 |
| `MainViewModel` | 17 | 3 | 10 | 4 |
| `SettingsViewModel` | 17 | 8 | 7 | 2 |
| **合计** | **149** | **43** | **91** | **15** |

一句话结论：**没有一条是"整个 VM 可以直接删而无副作用"的**，但也**没有一条是"必须保留整个 VM"的**——真正卡住删除的是 7 条外围逻辑（见 §3），其中 2 条会**写错数据**，5 条会让现场操作能力退化。

## 2. 红线：会写错 / 会丢数据的 3 条

| # | 问题 | 现状 | 影响 |
|---|---|---|---|
| R1 | **记条后不清空录音标注** | `RecordViewModel.cs:367` 记条后无条件清 `DescText`；现役 `DesktopWorkspaceService.cs:170-174` 保留草稿备注 | 连拍时上一条备注被带进下一条 → **标注写错** |
| R2 | **老工程名数据无迁移** | `SettingsViewModel.cs:18,133` 把工程名写 LiteDB `settings` 集合 `_id="project"`；现役写同集合另一行 `_id="desktop.workspace.v1"` 的 `WorkspaceSession.Project`（`DesktopWorkspaceService.cs:15,87`，默认 `"NewProject"`）。两者互不读取，全仓无第二处读 `"project"` | 删 VM 即**永久丢失**用户工程名，且无迁移路径 |
| R3 | **旧 13 键会话状态只迁了 2 个** | 只有 `Date`/`RecordCount` 被现役读一次（`DesktopWorkspaceService.cs:47-49`）；`MigratePlanAndLogs`（`DesktopWorkspaceEditing.cs:31-57`）只管计划与日志 | 老用户的标注、前缀、联动设置**静默丢失** |

## 3. 删除前必须先迁移的 7 条（按风险降序）

1. **音量键通道**（`RecordViewModel.cs:461-487`，消费 `IHardwareKeyService`）。现役 `DesktopWorkspaceView.cs:60-72` 只有键盘加速键，且 `App.axaml.cs:185` 只注册了 Noop → 删 VM 后现场物理音量键（记条+滚次号 / 撤回）彻底失效。属「交互不可替代」。
2. **计划删除守卫**（`ScheduleViewModel.cs:319-323`、`:365-369` 的"至少保留 1 场 / 1 镜"）。`DesktopWorkspaceEditing.cs:73-92` 完全没有 → 可把计划删空，随后 `SaveTakeAsync`（`DesktopWorkspaceService.cs:149`）抛"请先添加并选择拍摄计划"，**当场无法记录**。
3. **`NextKey` 自动递增编号**（`ScheduleViewModel.cs:484-488`，用于 `:218`/`:259`）。现役无等价物：新建必须手输并过 `^(\d{1,4})([A-Z]?)$`（`DesktopWorkspaceService.cs:263-264`），误输即抛；「连点场次+ 得 1/2/3」的工作流消失。
4. **删计划条目会清空场记撤回栈**（`DesktopWorkspaceEditing.cs:91` 的 `_undo.Clear()` 一次抹掉最多 100 步，栈容量见 `DesktopWorkspaceService.cs:168`）。
5. **手改文件号语义反转 + 撤回返回值缺失**：VM 是 `<1` 静默 clamp（`RecordViewModel.cs:262-265`、`:409-418`，由 `EditFileNumber_Clamps_To_One` 保护）；现役 `SetNumberAsync`（`:130-136`）改为抛异常并禁止回退到已用号。撤回侧现役 `UndoAsync`（`:176-187`）恢复整份快照但**不返回被恢复的备注**，而 VM 的 `RewindTakeAsync` 正靠返回值回填两个输入框。
6. **文件序号候选去重**：`SlateLogViewModel.cs:166` / `SlateLogPageViewModel.cs:180` → `LogEditorViewModel.cs:50`；桌面端换成裸 `NumericUpDown`（`DesktopWorkspaceView.cs:578`），**可填已占用号造成重号**。
7. **按单日导出**：仅 `SlateLogPageViewModel.cs:186-191` 有；现役导出永远全量（`DesktopWorkspaceBackup.cs:27`、`BackupUi.cs:71`），时间轴的单日 Tab 不影响导出范围（`DesktopWorkspaceView.cs:396-400` vs `:633`）。

## 4. 可以放心丢的（独有能力为零）

- **`IsAppBarVisible` / `IsTabBarVisible`**（`MainViewModel.cs:75,78`）：现役底栏只受 `_compact` 屏宽门控（`DesktopWorkspaceView.cs:51,132-134`），三页恒显示；也没有全局 AppBar，各页自带 `PageHeader`。
- **四个 PageKey 常量**：现存 `navb-*`/`nav-*` 是 `DesktopWorkspaceView` 自造字面量（`:156,163,181,188`），页键已改名 `schedule→plan`、`slatelog→log`，与 VM 常量无关。
- **`GoBack` 的形态**（但不丢"返回栈"这个需求本身——见 §5）：现役返回全部硬编码回记录页（`DesktopWorkspaceView.cs:71,202,286,374`）；设置已改成模态（`:598-619`），暂无需返回栈。
- **VM 的导出实现路径本身是死的**：`SlateLogPageViewModel.cs:190`、`SlateLogViewModel.cs:199`、`SettingsViewModel.cs:160` 都传 `string.Empty` 当目录，而 `ExportService.SaveToFileAsync` 首句 `Directory.CreateDirectory(dir)`（`IExportService.cs:29`）在 .NET 10 实测抛 `ArgumentException: The value cannot be an empty string. (Parameter 'path')`。测试里的 `SpyExportService`（`SlateLogPageViewModelTests.cs:99-108`）把这条盖住了 → 这几条"能力"生产环境从未可用，只有**意图**（按日导出）值得补。
- **`SlateLogPageViewModel` 的字符串排序**（`:135,138` 按 `Scn`/`Sht` 字典序）反而违背现役「按拍摄顺序」分组（`DesktopWorkspaceService.cs:212-236`）——删掉更一致。
- `ScheduleViewModel.FinalizeNewScene` 的文案合成（`:238-239` `从{a}的【正面】拍【近景】`）与「会话→VM 索引回填」(`OnSessionPropertyChanged`+`SyncSelection`)：现役以 `State.Scene/State.Shot` 名字为唯一真相，机制上不需要。

## 5. 连带清理清单（删的时候必须一起动）

| 对象 | 位置 | 说明 |
|---|---|---|
| 6 条 DI 注册 | `App.axaml.cs:193,203,209,214,222-226`（`Func<RecordViewModel>`/`SlateLogViewModel`/`ScheduleViewModel`/`SettingsViewModel`/`MainViewModel`，以及仅测试用的 `IScheduleBook` `:161`） | 唯一解析点就在 `MainViewModel` 的工厂里，删主 VM 后全是死注册 |
| 4 处过期注释 | `IScheduleService.cs:46`、`IScheduleStore.cs:6`、`ScheduleStore.cs:24`、`ScheduleCsvParser.cs:220` | 仍写着「ScheduleViewModel 唯一写入口」 |
| `ITakeFlowService` | 桌面分支从不初始化（`App.axaml.cs:57-71` 注释明确跳过 legacy 写入器），只有 Android 分支 `:48` 调 `InitializeAsync` | 删 VM 后成为「无界面仍在跑」的孤儿，仍被 14 个测试保护，建议单独立项裁定 |
| `IScheduleBook` | `App.axaml.cs:161` 注册、`:177` 只在 `RecordViewModel` provider 里用到 | 桌面路径无消费者 |
| 56 个测试用例 | `ViewModels/{Record 13, Schedule 12, Main 5, Settings 4, SlateLog 3, SlateLogPage 6, SlateColumn 8, LogEditor 5}.cs` + `SmokeTests.cs` 1 条（`PlaceholderViewModel`） | 全量 336 的 17% |
| `ViewModelTestDoubles.cs` | 同时被 `TakeFlowServiceTests`/`BackupServiceTests`/`DayRolloverServiceTests`/`ImportServiceTests` 使用 | **不能整文件删**，需逐 double 判断 |
| `PlaceholderViewModel.cs`（11 行） | 只有 `SmokeTests` 用 | 可一并删（冒烟改用任意真实类型） |

## 6. 建议路线（三阶段，别一刀切）

- **阶段 1（修数据正确性，先做）**：R1 记条清备注、（R2/R3 一次性键迁移 `MigrateLegacyStateAsync`）、文件号候选去重、单日导出。这四条做完，剩下的差异就只是"交互形态"了。
- **阶段 2（补现场能力）**：音量键通道（`IHardwareKeyService` 从 Noop 换成真实实现或明确弃用）、计划删除守卫（可直接参考已实现但**未注册进 DI** 的 `ScheduleService`，见 `IScheduleService.cs:50`：它已有"至少 1 场 1 镜"守卫、任意索引 Move、20 步整表 undo）、`NextKey` 自动编号、`_undo.Clear()` 的收窄。
- **阶段 3（清场）**：删 8 个 VM + `PlaceholderViewModel` + 56 个用例 + 6 条注册 + 4 处注释；`ITakeFlowService` / `IScheduleBook` 单独裁定。

## 7. 明细索引

- `docs/p7-vm-coverage/record.md` — `RecordViewModel` 54 个成员逐条；含 16 条「删除会丢掉的东西」、8 条迁移建议（带签名）
- `docs/p7-vm-coverage/schedule.md` — `ScheduleViewModel` 29 个成员，D1–D9 九组差异（选择模型/删除守卫/选择联动/NextKey/新建默认值/导入语义/重排与 undo/写库时机/异常面）；含 13 条丢失项、8 条迁移项
- `docs/p7-vm-coverage/latelog.md` — 两个场记页 VM 的对照 + 两版关系判定（PageVM 更贴原版 `slate_log_page.dart`，SlateLogVM 是扁平重写；各 5 处独占能力）
- `docs/p7-vm-coverage/settings-main.md` — 导航壳与设置页；含 `ProjectKey` 孤儿键、`ExportAll` 契约冲突、`SettingsViewModel.cs:79` 的音量标签勘误（实际格式 `yy+"Y"+MM+"M"+dd`）
## 8. 实施状态（阶段 1+2 已落地，2026-10-26）

阶段 1（数据正确性）与阶段 2（现场能力）的 7 条缺口已全部收口进 P8 现役实现，配套 11 条回归用例；其中 R1 经实测判定为**审计误报**（详见下表）。阶段 3（删 VM）尚未开工。

| 缺口 | 落点 | 回归用例 |
|---|---|---|
| R1 记条备注串味 | **实测不存在**：`SaveTakeAsync` 建的新草稿备注为空，`Changed → Render()` 会用空草稿重建两个 `TextBox`（且 `TextChanged` 在赋完 `Text` 之后才订阅），旧文本没有回写路径。已加回归用例把该行为钉住，未改产品代码 | `Record_page_notes_stay_with_their_own_take` + `Notes_go_to_the_saved_take_and_the_next_draft_starts_empty` |
| R2 老工程名孤儿键 | `src/VoiSlate/Desktop/DesktopWorkspaceLegacy.cs` `LegacyProjectKey = "project"` + `AdoptLegacySessionAsync()` | `Legacy_project_naming_linker_and_draft_are_adopted_on_first_launch` |
| R3 旧 13 键只迁 2 个 | 同上：`isLinked`/`prefixType`/`customPrefix`/`recordLinker`/`desc`/`note`/`oktk`/`oksht` 一次性并入 `WorkspaceSession`，非法命名回退默认而不阻塞启动 | 同上 + `Invalid_legacy_naming_falls_back_instead_of_blocking_startup` |
| 文件号候选去重 | `DesktopWorkspaceService.UsedFileNumbers(prefix, linker)` + `ShowLogEditor` 的「已占用：…」提示（校验仍由 `EditLogDetailsAsync` 的「文件名已被其他场记使用」兜底） | `Used_file_numbers_list_existing_rows_for_the_same_naming` |
| 按单日导出 | `DesktopWorkspaceBackup.cs` `ExportDayJson(date)` / `DayCount(date)`；时间轴表头按钮在选定日期时变「导出当日」 | `Day_export_contains_only_that_day` |
| 音量键通道 | `src/VoiSlate/Services/IHardwareKeyService.cs` 新增 `HardwareKeys.Current` 平台注册表；`src/VoiSlate.Android/AndroidHardwareKeyService.cs` + `MainActivity.OnKeyDown`；视图 `OnHardwareKey`（上键记条、下键撤回，仅记录页且非忙碌） | `Volume_up_saves_a_take_and_volume_down_undoes_it` |
| 计划删除守卫 | `DesktopWorkspaceEditing.DeletePlanItemAsync` 保留「至少 1 场 1 镜」；删条目不再 `_undo.Clear()`；`UndoAsync` 重定位到存活的场/镜 | `Plan_deletion_keeps_at_least_one_scene_and_one_shot` / `Deleting_a_plan_item_keeps_the_take_undo_history` / `Undo_after_the_current_shot_is_deleted_lands_on_a_live_shot` |
| NextKey 自动编号 | `DesktopWorkspaceLegacy.NextPlanKey(scene, isScene)`，`ShowPlanEditor` 新建时预填 | `Next_plan_key_continues_the_existing_numbering` |

口径补充：`RecordViewModel` 的「文件号 <1 静默 clamp」**未**照搬——现役抛异常并禁止回退到已占用编号，是有意的收紧；只补了「候选去重」这一半。
