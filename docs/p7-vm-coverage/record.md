# RecordViewModel 覆盖度对照

> 前置事实（影响判定口径）：`src/VoiSlate/ViewModels/RecordViewModel.cs` 是纯外壳 —— 记条/撤回/文件号写入全部委托 `ITakeFlowService`（实现 `src/VoiSlate/Services/TakeFlowService.cs`），它自己只做「13 键会话状态的恢复/镜像持久化 + 挑列联动 + 提示文案 + 音量键」。
> 运行时现状：桌面启动分支 `src/VoiSlate/App.axaml.cs:57-71` 明确跳过 legacy 写入器（注释 `The Windows workspace owns its session; do not initialize the legacy take writer.`），`MainViewModel` 仅在 `src/VoiSlate/App.axaml.cs:222` 注册、全仓无 `GetRequiredService<MainViewModel>`；Android 分支（`src/VoiSlate/App.axaml.cs:83-121`）挂的也是 `DesktopWorkspaceView`。即：**两个平台的 UI 都是 P8 工作台，RecordViewModel 无任何存活消费者**。因此下表的「现役对应物」一律指 `DesktopWorkspaceService` / 其分部类 / `DesktopWorkspaceView` 的 UI 层实现，而不是同样已成孤儿的 `TakeFlowService`。

## 逐方法对照

| VM 成员 | 作用（一句话） | 判定 | 现役对应物 / 差异 |
|---|---|---|---|
| `HintFake` :22 | 假条提示文案「这条跑了」 | 缺失 | 无任何对应物：`DesktopWorkspaceView.ShowMore` 的「记录假条 F」（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:482-497`）只调 `SaveTakeAsync(false, true)`，不产生该提示；`WorkspaceDraft`/`WorkspaceLog` 里没有提示字段（`src/VoiSlate/Desktop/WorkspaceModels.cs:5-15`）。删掉即失去「按 F 后给操作者的确认话术」。 |
| `HintEnd` :25 | 收工文案「收工了，这一镜结束了」 | 缺失 | 同上：`SaveTakeAsync(end:true, …)`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:147-175`）置 `d.Ended = true` 后只在界面上把次号位置显示为 `OK`（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:219` 一带），无文案。 |
| `HintSuffix` :27 | 提示后缀「\n 录音标注...」，说明下一步文件名会吃掉语音标注 | 部分覆盖 | 现役把「下一个文件名」做成独立一行 `NextFile`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:38`，渲染在 `src/VoiSlate/Desktop/DesktopWorkspaceView.cs:231-232`）。差异点：①现役显示**完整文件名**（含前缀+分隔符），VM 只显示三位序号（`PrevFileNameHint` :456-457）；②现役无「录音标注会写进这一条」的语义提示；③现役在 `Draft.Ended` 时把该行改成 `FileNumber-1`（已用号），VM 靠 `UpdatePreviewHintNormal` 回到「下一个号」。 |
| `SceneCol` :70 | 场次选择列（200 项？不，场次来自 plan） | 部分覆盖 | 场次选择现役由计划树承担（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:309-370` `RenderPlanContents`）+ `SelectShotAsync(scene, shot)`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:188-197`）。差异点：**无独立列控件**、无 `ScrollTo(index, animate)` 动画语义、切换场不会触发「镜像到会话索引」的二次通知（现役只改 `State.Scene/State.Shot`）。 |
| `ShotCol` :73 | 镜头选择列 | 部分覆盖 | 同上；额外差异：现役 `SelectShotAsync` 在「同一场同一镜且未收工」时是 no-op（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:191`），且会强制 `State.Linked = true` 并清空 undo 栈（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:192`）——VM 的挑列没有「切镜即退出补录模式」这条副作用。 |
| `TakeCol` :76 | 次号选择列，`SetItems(Enumerable.Range(1, RecordingSessionViewModel.DefaultTakeCount))`（1..200，构造器 :59-63） | 部分覆盖 | 现役次号由 `SetTakeAsync(value)`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:137-141`）与 `SetRetakeIndexAsync(index)`（`:142-146`）承担，界面是 NumericUpDown（手改号 `src/VoiSlate/Desktop/DesktopWorkspaceView.cs:498-506`）与「指定文件序号」对话框（`:507-532`）。差异点：①取值域 **1..9999**（service 校验）vs VM 硬编码 200 项；②无 `ScrollNext/ScrollPrev` 的「到顶不循环」语义（`src/VoiSlate/ViewModels/SlateColumnViewModel.cs:63-95`）；③补录次号走后缀字母 `RetakeLetter`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:39`）而不是数字列。 |
| `QuickNotes` :79 | 最近 40 条今日日志的快照集合 | 覆盖 | `QueryLogs(date, …, LogFilter.All)`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:212-237`）+ 时间轴渲染（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:371-443`）。差异点：现役渲染**当日全部**条目（可筛选/可编辑），VM 只取 `TakeLast(40)`（:88）。语义上更强，不构成缺口。 |
| `RefreshQuickNotesAsync()` :82 | 按今日 key 拉最近 40 条填充 `QuickNotes` | 覆盖 | 现役每次 `Changed` 事件整表重建（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:100` 附近 `OnChanged` → `Render`）+ `ReloadLogs()`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:78-83`）。差异点：现役是「事件驱动全量重建」，VM 是「调用方显式拉取」，无 40 条上限。 |
| `SetOkTake(TkStatus)` :94 | 预设本条「领夹/录音」优良状态（pending，记条时生效） | 部分覆盖 | 现役的优良只在**收工瞬间自动升级**（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:147-175` 内 `OkSht/OkTk` 自动 promote）与事后编辑（`EditLogAsync` :245-250 显式传入 `ShtStatus picture, TkStatus sound`）。差异点：**缺失「记条前预置、记条时落库」的工作流**；`WorkspaceDraft`（`src/VoiSlate/Desktop/WorkspaceModels.cs:5-15`）没有 pending 评价字段，`WorkspaceSession`（`:17-33`）也不再持久化 `SessionKeys.OkTk/OkSht`。 |
| `SetOkShot(ShtStatus)` :97 | 预设本条「画面」优良状态 | 部分覆盖 | 同上一行（同一机制的另一个维度）。 |
| `DescText` :102 | 本次条目的录音标注输入缓冲，`OnDescTextChanged`（:151-157）激活时即写 `SessionKeys.Desc` | 覆盖 | `Draft.RecordingNote` + `SaveDraftAsync`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:111`）→ `ChangeNotes`（`:112-117`）；界面绑定 `_recNote`（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:245-250`、`:278-282`）。差异点：持久化目标从 13 键换成 `WorkspaceSession.Drafts[shotKey]` 的 JSON（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:84-89` Persist），旧值不会自动迁移。 |
| `ShotNoteText` :105 | 镜头标注输入缓冲，`OnShotNoteTextChanged`（:159-165）即写 `SessionKeys.Note` | 覆盖 | `Draft.ShotNote` + 同上 `SaveDraftAsync`；语音转写也写这里（`src/VoiSlate/Desktop/DesktopWorkspaceSpeech.cs:94`）。差异点同上一行（存储位置变更）。 |
| `IsLinked` :108 | 「联动」开关：开=次号随记条自增，关=补录模式 | 部分覆盖 | `State.Linked`（`src/VoiSlate/Desktop/WorkspaceModels.cs:24`）+ `ToggleRetakeAsync()`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:125-129`）。差异点：①**语义反转边**：现役 `Linked=false` 才是补录模式，并且会**禁止手改文件号**（`SetNumberAsync` :130-136 抛「请先进入补录模式再手动修改文件号」）与禁止设补录次号（`SetRetakeIndexAsync` :142-146）；②现役 `Draft.Ended` 时禁止切换；③现役切换会清空 undo 栈（`:128`），VM 无此副作用；④持久化时机由 `Change` 事务统一 `Persist`，VM 是属性 setter 立即写。 |
| `PreviewHint` :111 | 记条后给操作者的「下一个文件名」提示 | 部分覆盖 | 见 `HintSuffix` 行；现役无独立 `PreviewHint` 属性，`NextFile`/`CurrentFile`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:37-38`）由 view 直接读（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:231-232`）。差异点：缺少 fake/end 两个分支的差异化文案（`HintFake`/`HintEnd`）。 |
| `AsrStatus` :114 | 语音状态文本，默认「空闲」，流转为「识别中」/「识别完成」/「识别失败：{msg}」 | 缺失 | 现役状态是 `preview.Text` 的一次性临时文案（`src/VoiSlate/Desktop/DesktopWorkspaceSpeech.cs:44`「已取消；按住话筒重新标注」、`:67`「正在聆听，左滑录音 / 右滑镜头；回中松手取消」、`:84`「正在整理识别结果…」、`:93`「未识别到语音，请重试」），**没有可绑定、可断言的持久状态属性**，也没有「识别失败：{message}」这种带异常文本的形态。 |
| `FileNumber` :118 | 当前文件序号（内存镜像，默认 1） | 覆盖 | `State.FileNumber`（`src/VoiSlate/Desktop/WorkspaceModels.cs:23`）+ `InitializeAsync` 的 `FileNumber = Math.Max(FileNumber, NextAvailableNumber())`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:42-64`）。差异点：VM 只靠 `SetCurrentNumber` clamp 到 ≥1（:262-265），现役还额外拒绝 `int.MaxValue` 与「回退到已用号」。 |
| `PrefixText` :122 | 当前文件名前缀（日期 / 自定义 / Sound Devices） | 覆盖 | `FilePrefix => State.Naming.Prefix(Today)`（`src/VoiSlate/Desktop/DesktopWorkspaceEditing.cs:8`）。逻辑等价（自定义→原样、sound devices→`YYYMMMDD` 形态、默认→`yyMMdd`），实现分别在 `src/VoiSlate/ViewModels/RecordViewModel.cs:255-260` 与 `src/VoiSlate/Desktop/WorkspaceModels.cs:52-56`。 |
| `LinkerText` :126 | 文件名分隔符，默认 `-T` | 覆盖 | `State.Naming.Linker`（`src/VoiSlate/Desktop/WorkspaceModels.cs:51`，默认 `-T`）+ `SetNamingAsync`（`src/VoiSlate/Desktop/DesktopWorkspaceEditing.cs:10-14`）。差异点：现役经 `WorkspaceNaming.Validate()`（`src/VoiSlate/Desktop/WorkspaceModels.cs:57-63`）限制长度 ≤20 且不得含文件名非法字符；VM 的 `EditLinkerAsync`（:423-428）不校验。 |
| `NumberText` :129 | 纯三位序号 `FileNumber.ToString("D3")` | 覆盖 | `State.FileNumber.ToString("D3")` 融进 `CurrentFile`/`NextFile`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:37-38`）；界面无「裸序号」独立控件。语义被包含，不构成缺口。 |
| `CurrentFileNumber` :132 | 当前完整文件名 `前缀+分隔符+三位号` | 覆盖 | `CurrentFile`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:37`）。差异点：收工后 view 会显示 `FileNumber-1`（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:231-232`），VM 的 `FileNumber` 由 `FileNumberChanged` 事件驱动（:139-145 / :339-348），两者取号口径一致。 |
| `NextFileNumber` :135 | 下一条的三位号 | 部分覆盖 | `NextFile`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:38`）返回**完整文件名**（前缀+分隔符+`(long)FileNumber+1` 三位号），比 VM 多了前缀拼接；边界上现役用 `long` 转换避免 `int.MaxValue` 溢出，VM 直接 `FileNumber + 1`（:135）。功能上更宽，但**形态不同**（下游若按三位数解析会变）。 |
| `IsRecording` :138 | 录音中标志（镜像 `_asr.IsListening`） | 缺失 | 现役无「正在录音」布尔属性；语音是按住话筒的一次性采集（`src/VoiSlate/Desktop/DesktopWorkspaceSpeech.cs:58-96`），状态只体现在 `preview.Text` 与按钮视觉。 |
| `HydrationTask` :178 | 暴露水合完成的任务，供宿主 await | 部分覆盖 | `InitializeAsync()`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:42-64`）是等价入口，且宿主是同步等待（`src/VoiSlate/App.axaml.cs:77`）。差异点：VM 的水合只读 13 键、不碰计划与日志；现役初始化还要读计划、迁移计划/日志、恢复 undo 栈、确保跨天（`EnsureDate` :70-77）。 |
| `Activate()` :181 | 激活订阅：硬件键、`FileNumberChanged`、会话 `PropertyChanged`、三列 `PropertyChanged`，然后启动水合 | 部分覆盖 | 现役没有「激活/反激活」概念：`DesktopWorkspaceView` 在构造器里永久订阅 `_service.Changed`（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:39-55`）。差异点：①现役无硬件键订阅；②无 13 键↔列的镜像订阅（改为 `Changed` 事件整表重建）；③现役初始化必须在宿主显式 `await workspace.InitializeAsync()`。 |
| `Deactivate()` :201 | 反激活：幂等退订全部事件 | 部分覆盖 | 现役靠 view 生命周期（控件销毁即失去引用），没有显式退订 API；`DesktopWorkspaceService.Changed` 是弱约束（订阅者不注销会由 view 存活期兜底）。差异点：无幂等退订契约，也没有「退出时 flush 草稿」动作。 |
| `Dispose()` :218 | 反激活 + 退订 ASR 两个事件 | 覆盖 | 无需对应物：现役 view 不订阅 `IAsrService`（`src/VoiSlate/App.axaml.cs:163` 的 `MockAsrService` 与工作台无关），`DesktopWorkspaceSpeech` 自成一体。 |
| `HydrateAsync()` :228 | 从 13 键恢复：日期→RecordCount、前缀模式/自定义前缀、分隔符、场/镜/次索引、联动、录音标注、镜头标注、文件号，并算提示 | 部分覆盖 | `InitializeAsync()`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:42-64`）从 `WorkspaceSession` JSON 恢复（`SessionKey = "desktop.workspace.v1"`，:15），并且**只借鉴旧键两处**：`SessionKeys.Date == Today` 时 `FileNumber = Math.Max(1, RecordCount)`（:53-55）。差异点：旧 13 键里的 `Desc/Note/IsLinked/PrefixType/CustomPrefix/RecordLinker/SceneIndex/ShotIndex/TakeIndex/OkTk/OkSht` **不会迁移**（`MigratePlanAndLogs` 只搬计划与日志，`src/VoiSlate/Desktop/DesktopWorkspaceEditing.cs:31-57`）；默认值也不同（如 `Project = "NewProject"`、`Linked = true` 见 `src/VoiSlate/Desktop/WorkspaceModels.cs:24-27`）。 |
| `ComputePrefix(string)` :255 | 按模式算前缀（custom / sound devices / 日期） | 覆盖 | `WorkspaceNaming.Prefix(Today)`（`src/VoiSlate/Desktop/WorkspaceModels.cs:52-56`）。差异点：VM 用 `VoiSlateDates.SoundDevicesKey(_time.Now)` 取平台化文本，现役从 6 位日期串切分，**两者输出形态一致但来源不同**（若 `SoundDevicesKey` 改格式，现役不受影响）。 |
| `SetCurrentNumber(int)` :262 | 文件号写入并 clamp 到 ≥1 | 部分覆盖 | `SetNumberAsync(value)`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:130-136`）。差异点：①现役对 `<1` **抛异常**而不是静默 clamp（VM `:262-265`、测试 `EditFileNumber_Clamps_To_One` 保护的就是静默 clamp）；②现役禁止「回退到已使用或已删除的编号」（`value < State.FileNumber`）；③现役要求当前非联动（`State.Linked`）才能手改；④现役 `int.MaxValue` 报「文件号超出范围」；⑤现役走 `Change` 事务（undo 清空 + Persist + ReloadLogs）。 |
| `OnColumnPropertyChanged` :269 | 挑列变化 → 写会话索引；切场重置镜/次，切镜重置次 | 部分覆盖 | 现役的选择联动是 `State.Scene/State.Shot` + `Draft.Take` 一份状态（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:188-197` `SelectShotAsync`）。差异点：现役**切镜即 `Linked = true` 并清空 undo**；VM 的「切场→镜/次归零、切镜→次归零」在现役里由 `SelectShotAsync` 重置整份 draft（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:194`）近似实现，但没有「切场」这一级（场由计划树点击直接给到镜）。 |
| `OnSessionPropertyChanged` :298 | 会话索引变化 → 回写列；联动开关双向镜像 | 部分覆盖 | 现役是单向：service 是唯一真源，view 每次 `Changed` 全量重建（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:100`、`:206-277`）。差异点：**没有反向镜像**（没有外部改会话再回写 UI 的通道），因此也不存在 `_suppressColumnSync`（:328-336）这类防回环逻辑。 |
| `SyncColumn` :326 | 带抑制标志地把索引推到列并滚动可见 | 部分覆盖 | 无对应物；现役重建后由 `ScrollIntoView` 类行为在时间轴/计划树上体现（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:371-443`）。差异点：无 `ScrollTo(index, animate)` 的动画/延迟语义（`src/VoiSlate/ViewModels/SlateColumnViewModel.cs:52-60`）。 |
| `OnFileNumberChangedFromFlow` :339 | 订阅 `ITakeFlowService.FileNumberChanged` 同步显示与 `RecordCount` | 部分覆盖 | `Changed` 事件（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:23`）+ `Persist`（`:84-89`）里写 `FileCounters`；`RecordCount` 概念在现役变成 `FileCounters[前缀+分隔符]` 字典。差异点：现役没有「全局 RecordCount」单值，改为按命名组合计数；`SessionKeys.RecordCount` 只在初始化时读一次。 |
| `AdvanceTakeAsync(TakeType, bool scrollTake)` :353 | 记条：以 `DescText/ShotNoteText` 作备注 override → `AddItemAsync` → 清 OkTk → 更新提示 → 清空录音标注 → 按需滚动次号列 | 部分覆盖 | `SaveTakeAsync(bool end, bool fake)`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:147-175`）。差异点：①现役 **remark 取自 `Draft.RecordingNote/ShotNote`**，不接受 override 参数；②现役记条后**不清空草稿备注**（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:170-174` 只推进 Take/RetakeIndex，仅 `end` 时 `d.Ended = true`），VM 在 `:367` 无条件 `DescText = string.Empty`；③type→参数映射需调用方自行翻译（`Fake`→`(false,true)`、`End`→`(true,false)`）；④现役 `end` 时自动升级 `OkSht/OkTk` 并把 `Picture/Sound` 拷进条目（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:155-165`）；⑤现役对重复文件名/`Take>=9999`/`FileNumber==int.MaxValue`/已收工 直接抛异常（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:150-152`），VM 没有这些守卫（异常原样冒泡到 `OnHardwareKeyPressed` 的 catch，:481-486）；⑥`scrollTake` 的滚动副作用无对应物。 |
| `AdvanceTakeCommand` :372 | 无参命令 → `AdvanceTakeAsync(Current)` | 覆盖 | UI 按钮「记一条」（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:253-266`）+ 快捷键 Ctrl+Enter（`:60-72`，口袋锁时提示而不记条）。 |
| `RewindTakeAsync()` :376 | 撤回：调 `RewindAsync` 并回填 `RestoredDesc/RestoredShotNote` | 部分覆盖 | `UndoAsync()`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:176-187`）。差异点：①**undo 语义更强**：现役恢复整份快照（`State = Clone(snapshot.State)`，含 `FileNumber/Scene/Shot/Linked/Draft`），并用「偏好元组」保护 `Project/PocketLock/LeftHanded/AutoBackup/BackupDirectory` 不被回滚（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:182-184`）；②现役是**多级 undo 栈**（最多 100 层，`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:167-168`），VM 只有单层；③现役校验「该条仍是当天最后一条」，否则抛「历史记录已变化，无法撤回」（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:180`）；④现役无 `WasOkMarkerOnly` 分支（`src/VoiSlate/Services/ITakeFlowService.cs:6` 的 OK 尾只弹哨兵语义在现役里表现为：收工条也可撤回，快照里 `Ended=false`）；⑤现役不返回被恢复的备注文本（调用方靠 `Changed` 重绘）。 |
| `RewindTakeCommand` :384 | 无参命令 → `RewindTakeAsync` | 覆盖 | 快捷键 Ctrl+Shift+Z（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:60-72`）+ 撤回按钮（`:252`，`Enabled = CanUndo`）+ 带确认的 `AskUndo`（`:474-481`「当前草稿将替换为上一条的编号、评价和标注，是否继续？」）。 |
| `SetDesc(string?)` :387 | 命令：把文本写进录音标注并持久化 | 覆盖 | `SaveDraftAsync(recording, shot)`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:111`）+ `NotesChanged` 接线（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:278-282`）。差异点：现役是「两个输入框一起提交」，VM 是单字段命令。 |
| `SetShotNote(string?)` :396 | 命令：写镜头标注 | 覆盖 | 同上（同一对参数的另一维）。 |
| `ToggleLink()` :405 | 命令：翻转联动开关 | 部分覆盖 | `ToggleRetakeAsync()`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:125-129`）。差异点：现役在 `Draft.Ended` 时抛异常、切换后清空 undo 栈、且下一次手改号会因「补录模式」被拒（反向：联动时拒绝手改）。VM 只翻转并持久化一个布尔（:405 + `RecordingSessionViewModel` 13 键）。 |
| `EditFileNumberAsync(int)` :409 | 手改文件号（先 clamp 到 ≥1，再经唯一写入口） | 部分覆盖 | `SetNumberAsync(value)`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:130-136`）。差异点见 `SetCurrentNumber` 行：clamp vs 抛错、禁止回退、要求非联动、上限校验、事务/undo 清空/持久化时机。 |
| `EditFileNumberCommand` :421 | 命令：手改文件号 | 部分覆盖 | 「手动改号」入口（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:482-497` → `ShowFileNumber` `:498-506`）。差异点：现役入口在 `State.Linked` 时**直接拒绝打开**并提示先进入补录模式，NumericUpDown 的 `Minimum = State.FileNumber`（即从根上不可能回退）。 |
| `EditLinkerAsync(string)` :423 | 改分隔符并同步会话 | 部分覆盖 | `SetNamingAsync(new WorkspaceNaming{…})`（`src/VoiSlate/Desktop/DesktopWorkspaceEditing.cs:10-14`）。差异点：现役 `Validate()` 限制长度 ≤20 / 非法字符，并且**改命名会重算文件号**（`State.FileNumber = NextAvailableNumber()`）并清空 undo（`:11-13`）；VM 只改后缀显示，不动文件号。 |
| `EditLinkerCommand` :431 | 命令：改分隔符 | 部分覆盖 | 「文件命名」设置页（`src/VoiSlate/Desktop/DesktopWorkspacePreferences.cs:8-25`）。差异点：现役是「模式+自定义前缀+分隔符」一次性提交，VM 是单字段。 |
| `EditPrefixAsync(PrefixType, string?)` :434 | 改前缀模式与自定义前缀，重算 `PrefixText` | 部分覆盖 | `SetNamingAsync`（`src/VoiSlate/Desktop/DesktopWorkspaceEditing.cs:10-14`）+ `WorkspaceNaming.Validate`（`src/VoiSlate/Desktop/WorkspaceModels.cs:57-63`）。差异点：①现役校验自定义前缀非空/≤100/无非法字符；②现役改前缀会按新组合重算可用号（`NextAvailableNumber`，`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:65-68`）并清 undo，VM 只是把 `FileNumber` 原样保留；③模式字符串契约一致（`"default"/"sound devices"/"custom"`）。 |
| `UpdatePreviewHint(TakeType)` :446 | 按 fake/end/normal 三态刷新提示 | 部分覆盖 | 见 `PreviewHint` 行：现役只有 `NextFile` 一种呈现，三态文案中的 fake/end 两个分支丢失。 |
| `UpdatePreviewHintNormal()` :453 | 回到「下一个文件名」提示 | 部分覆盖 | 同上；现役靠 `Draft.Ended` 判断显示 `CurrentFile` 还是 `NextFile`（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:231-232`）。 |
| `PrevFileNameHint()` :456 | 拼「上一个文件名 + \u201c录音标注...\u201d」，`FileNumber<=1` 时只给后缀 | 部分覆盖 | `NextFile`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:38`）拼完整文件名但**没有 `FileNumber<=1` 的特例、没有后缀文案**；现役提示的是「下一个」而非「上一个」。 |
| `OnHardwareKeyPressed(HardwareKey)` :461 | 音量上=记条并滚次号、音量下=撤回；异常吞掉交上层 Toast | 部分覆盖 | 现役**没有任何 `IHardwareKeyService` 消费者**（`src/VoiSlate/App.axaml.cs:185` 注册的是 `NoopHardwareKeyService`），替代通道是键盘加速键 Ctrl+Enter / Ctrl+Shift+Z / Ctrl+1·2·3（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:60-72`）与长按 650ms 口袋锁（`:34`、`:50`）。差异点：①**触发源不同**（音量键 vs 键盘），拍摄现场单手/戴手套场景不可替代；②VM 的 `OperationCanceledException` 静默与其它异常「留给上层 Toast」（`:481-486`）在现役里由 `Run()` 的统一 try/catch + Toast 承担（`:101-107`）；③`scrollTake:true` 的次号滚动无对应物。 |
| `StartAsr()` :491 | 启动识别并置「识别中 / 空闲」 | 部分覆盖 | 百炼实时转写的「按住话筒」起点（`src/VoiSlate/Desktop/DesktopWorkspaceSpeech.cs:58-96`，`preview.Text = "正在聆听，左滑录音 / 右滑镜头；回中松手取消"`）。差异点：①现役是**按住说话 + 左右滑动选目标框**（录音标注/镜头标注），VM 只写镜头标注；②VM 依赖 `IAsrService`（`src/VoiSlate/App.axaml.cs:163` 的 `MockAsrService`，mock 实现），现役依赖 `BailianRealtimeClient` + `PcmCapture`；③`IsRecording` 通知（:494）无对应物。 |
| `StopAsr()` :498 | 停止识别并复位状态 | 部分覆盖 | 松手提交/回中取消（`src/VoiSlate/Desktop/DesktopWorkspaceSpeech.cs:44`「已取消；按住话筒重新标注」）。差异点：现役取消路径会丢弃这次转写且不写任何状态属性。 |
| `ToggleAsrCommand` :506 | 命令：翻转识别开关 | 部分覆盖 | 话筒热区按钮（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:267-270`）。差异点：现役是 press/release 语义而非 toggle；且安卓端在无麦克风时直接降级为「请手动记录」（`src/VoiSlate/Desktop/DesktopWorkspaceSpeech.cs:21`）。 |
| `OnAsrFinalResult(string)` :519 | 识别最终结果**替换**镜头标注；空文本忽略；状态置「识别完成」 | 部分覆盖 | `src/VoiSlate/Desktop/DesktopWorkspaceSpeech.cs:84-96`。差异点：①现役是**追加**（`box.Text = previous + (previous.Length > 0 ? "；" : "") + text`，:94），不是替换；②现役在提交前校验「场景/镜头/标注内容未变」，否则抛「标注内容或镜头已变化，本次转写未写入，请重新标注」（:90-92）——VM 完全没有这层竞态保护；③现役空文本走「未识别到语音，请重试」（:93）；④现役有 60 秒上限与 `limit` 计时器（:67）。 |
| `OnAsrError(string)` :534 | 识别失败时置「识别失败：{message}」 | 缺失 | 现役无错误状态属性；异常经 `Run()`/Toast 提示（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:101-114`）或 `preview.Text` 文案，**不保留 message 文本、也没有可断言的状态机**。 |

## 删除会丢掉的东西

1. **音量键记条/撤回通道**：`src/VoiSlate/ViewModels/RecordViewModel.cs:461-487` 消费 `IHardwareKeyService`（`VolumeUp`→记条+滚次号、`VolumeDown`→撤回），现役 `DesktopWorkspaceView` 只绑键盘加速键（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:60-72`），删 VM 后**物理音量键在拍摄现场失效**（`IHardwareKeyService` 也无其他消费者：`src/VoiSlate/App.axaml.cs:185` 只注册了 Noop 实现）。
2. **记条后自动清空录音标注**：`src/VoiSlate/ViewModels/RecordViewModel.cs:367` 无条件 `DescText = string.Empty`；`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:170-174` 记条后保留草稿备注，连拍时上一条的录音标注会**留在输入框里被下一条复用**。
3. **「备注随本次条目 override 提交」的时序**：`src/VoiSlate/ViewModels/RecordViewModel.cs:355-359` 把 `DescText/ShotNoteText` 作为 `AddItemAsync` 的 override 参数一次性写入条目；现役只从 `Draft` 取（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:155-165`），若草稿持久化落后（`NotesChanged` 与点按之间丢一次写）就没有兜底。
4. **pending 评价（记条前预设优良）**：`src/VoiSlate/ViewModels/RecordViewModel.cs:94-100` → `RecordingSessionViewModel.SetOkTake/SetOkShot`（`src/VoiSlate/ViewModels/RecordingSessionViewModel.cs:132-134`、`:137-159`）+ 13 键持久化；现役 `WorkspaceDraft`（`src/VoiSlate/Desktop/WorkspaceModels.cs:5-15`）无此字段，只能在收工瞬间自动升级或事后编辑。
5. **记条前的三态提示文案**：`HintFake`（`src/VoiSlate/ViewModels/RecordViewModel.cs:22`）、`HintEnd`（`:25`）、`HintSuffix`（`:27`）与 `PrevFileNameHint`（`:456-457`）；现役只有「下一个文件名」一行（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:231-232`）。
6. **「上一个文件名 + 录音标注会写进这一条」的操作提示**：`src/VoiSlate/ViewModels/RecordViewModel.cs:456-457`（`FileNumber<=1` 时只给后缀的特例也在这一行）；现役无此提示（`NextFile` 是「下一个」）。
7. **语音状态机与错误文本**：`AsrStatus`（`src/VoiSlate/ViewModels/RecordViewModel.cs:114`）、`OnAsrFinalResult` 的「识别完成」（`:519-532`）、`OnAsrError` 的「识别失败：{message}」（`:534-542`）、`IsRecording`（`:138`）；现役只有临时 `preview.Text`（`src/VoiSlate/Desktop/DesktopWorkspaceSpeech.cs:44/67/84/93`）。
8. **语音结果覆盖 vs 追加的旧手感 + 无竞态校验**：`src/VoiSlate/ViewModels/RecordViewModel.cs:519-532` 直接替换镜头标注；现役追加并要求「场景/镜头/标注未变」（`src/VoiSlate/Desktop/DesktopWorkspaceSpeech.cs:90-96`）——迁移时若照搬现役，等于改了交互契约。
9. **手改文件号的「静默 clamp」契约**：`src/VoiSlate/ViewModels/RecordViewModel.cs:409-418` + `SetCurrentNumber`（`:262-265`）保证 `<1` 落回 1；现役 `SetNumberAsync`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:130-136`）改为抛异常，删 VM 同时**删掉了 `EditFileNumber_Clamps_To_One` 保护的宽松语义**（若还有外部调用方依赖）。
10. **改前缀/分隔符后文件号不重算**：`src/VoiSlate/ViewModels/RecordViewModel.cs:423-442` 只改显示；现役 `SetNamingAsync`（`src/VoiSlate/Desktop/DesktopWorkspaceEditing.cs:10-14`）会用 `NextAvailableNumber()`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:65-68`）重算并清 undo——行为已变，属于「删 VM 不会回退」，但要确认这是期望契约。
11. **挑列（场/镜/次）的滚动与到顶不循环语义**：`src/VoiSlate/ViewModels/SlateColumnViewModel.cs:52-95`（`ScrollNext` 到顶不回绕、`ScrollPrev` 无论是否联动都递减）；现役 UI 是计划树 + NumericUpDown（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:309-370`、`:507-532`），无此交互。
12. **次号硬上限 200 与「补录用字母后缀」的差异**：`src/VoiSlate/ViewModels/RecordViewModel.cs:59-63`（`DefaultTakeCount = 200`，`src/VoiSlate/ViewModels/RecordingSessionViewModel.cs:17`）vs 现役 1..9999 + `RetakeLetter`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:39`、`:142-146`）。
13. **打开时的激活/反激活生命周期**：`Activate()`（`:181-198`）/`Deactivate()`（`:201-216`）的幂等订阅与退订；现役 view 一次性构造订阅（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:39-55`），没有可复用的激活钩子。
14. **旧 13 键会话数据的读路径**：`HydrateAsync`（`src/VoiSlate/ViewModels/RecordViewModel.cs:228-253`）是 `SessionKeys.*` 唯一完整读取点；现役只读 `Date`/`RecordCount` 两处（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:47-49`），删 VM 后**其余 11 个键彻底无人读**（写入点还有 `src/VoiSlate/ViewModels/SettingsViewModel.cs`、`src/VoiSlate/ViewModels/ScheduleViewModel.cs` 等死 VM）。
15. **最近 40 条快照列表的记忆效应**：`QuickNotes`（`src/VoiSlate/ViewModels/RecordViewModel.cs:79`、`:82-91`）是固定窗口；现役时间轴渲染当日全部（`src/VoiSlate/Desktop/DesktopWorkspaceView.cs:371-443`），条目多时性能与可读性都会变。
16. **`ITakeFlowService` 与其 14 个测试的「孤儿化」**：删 VM 后 `src/VoiSlate/Services/TakeFlowService.cs` 仍被 `src/VoiSlate/App.axaml.cs:166-182` 注册并在安卓分支 `:48` 被 `InitializeAsync`，但**没有任何界面消费**，`tests/VoiSlate.Tests/TakeFlowServiceTests.cs`（14 个 `[Fact]`）继续保护一条已无 UI 的写入路径（本次不删，但需登记为后续清理项）。

## 必须先迁移的项

> 排序原则：越靠前＝越贴近拍摄现场的不可替代交互，或越可能造成**数据写错**而非仅仅「显示不同」。签名沿用现役 `public Task XxxAsync(...) => Change(async () => {...})` 风格（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:90-110`）。

1. **音量键记条/撤回通道**（风险最高：现场单手操作、现役完全缺失）
   ```csharp
   // DesktopWorkspaceService.cs
   public Task<bool> AdvanceByHardwareKeyAsync(bool scrollTake = true)   // 音量上
       => Change(async () => { /* 复用 SaveTakeAsync 的守卫 + Take 自增 + 滚动请求 */ });
   public Task RewindByHardwareKeyAsync()                               // 音量下
       => Change(async () => { /* 复用 UndoAsync 的「末条校验」语义 */ });
   ```
   附带：`src/VoiSlate/App.axaml.cs:185` 的 `IHardwareKeyService` 仍是 `NoopHardwareKeyService`，迁移后应由 `DesktopWorkspaceView` 订阅并把事件转给上述方法。

2. **记条后清空/推进草稿备注**（会写错数据：备注串到下一条）
   ```csharp
   public Task SaveTakeAsync(bool end = false, bool fake = false,
                             string? recordingNote = null, string? shotNote = null)
       => Change(async () => { /* 现有 147-175 主体；null 时取 Draft，非 null 时作为本条 override；
                                记条后除非 end，否则 Draft.RecordingNote = "" */ });
   ```

3. **撤回后的备注回填与「已收工也允许撤回」语义对齐**（undo 语义差异，影响现场救急）
   ```csharp
   public Task<bool> UndoAsync(bool restoreNotes = true)   // 返回是否成功，供 UI 回填预览
   ```
   现役 `UndoAsync`（`src/VoiSlate/Desktop/DesktopWorkspaceService.cs:176-187`）已恢复整份快照，需明确取舍：保留「多层 undo」还是收敛回「单层 + 只回填备注」。**不要**在未确认前改现有行为——只补一个「撤回后把 `Draft.RecordingNote/ShotNote` 显式写回界面」的返回值/事件。

4. **pending 优良评价（记条前预设）**（工作流缺失，但可延后到有需求时补）
   ```csharp
   public Task SetPendingEvaluationAsync(TkStatus? take = null, ShtStatus? shot = null)
       => Change(() => { Draft.PendingTk = take; Draft.PendingSht = shot; return Task.CompletedTask; });
   ```
   需同步给 `WorkspaceDraft`（`src/VoiSlate/Desktop/WorkspaceModels.cs:5-15`）加两个字段，并在 `SaveTakeAsync` 里优先于自动 promote 使用。

5. **记条提示文案三态 + 「上一个文件名」特例**（纯 UI 语义，最容易补，也最容易漏）
   ```csharp
   public string DescribeNextTake(bool fake = false, bool ended = false)   // 对应 HintFake/HintEnd/PrevFileNameHint
   ```

6. **语音结果的竞态校验下沉**（现役有、VM 无；若迁移现役逻辑则属「改进」，但需保留「替换 vs 追加」的产品决定）
   ```csharp
   public Task<bool> CommitSpeechAsync(string target, string text, string expectedScene,
                                       string expectedShot, string expectedNote)
       => Change(() => { /* 校验 scene/shot/note 未变，否则抛「标注内容或镜头已变化…」 */ });
   ```

7. **旧 13 键 → `WorkspaceSession` 的一次性迁移**（防止老用户丢标注/前缀/联动设置）
   ```csharp
   public Task MigrateLegacySessionAsync()   // 在 InitializeAsync（:42-64）末尾调用；
                                             // 读 SessionKeys.Desc/Note/IsLinked/PrefixType/CustomPrefix/
                                             // RecordLinker/SceneIndex/ShotIndex/TakeIndex/OkTk/OkSht
   ```

8. **最近 40 条快照接口**（低风险，仅当需要「快速回顾」浮层时）
   ```csharp
   public IReadOnlyList<WorkspaceLog> RecentLogs(int take = 40)   // 内部复用 QueryLogs(date: Today)
   ```

## 测试锚点

文件：`tests/VoiSlate.Tests/ViewModels/RecordViewModelTests.cs`（13 个 `[Fact]`）。夹具用**真实 `TakeFlowService`** + 既有 Fake（`FakeSessionSettingsStore`/`FakeLogRepository`/`FakePickerHistoryStore`/`FileNumberingService`/`NoopHaptics`/`NoopToast`，见 `:29-55`），因此断言同时覆盖 VM 外壳与 `TakeFlowService` 内部时序。

| 用例（行号） | 实际保护哪条逻辑 | 删 VM 会一并删掉的断言 |
|---|---|---|
| `Activate_Hydrates_Thirteen_Keys_And_File_Display` :58 | 13 键水合：`IsLinked`、`Desc/ShotNote`、`FileNumber/CurrentFileNumber/PrefixText/LinkerText`、`PreviewHint`（含 `FileNumber<=1` 之外的前一条号）、`Session.RecordCount`、`TakeCol` 200 项 | `MY-X007`、`MY-X006\n 录音标注...`、`TakeCol.Items.Count == 200` 等 9 条断言（:76-85）——**唯一覆盖「旧 13 键如何映射到显示」的测试** |
| `AdvanceTake_Writes_Log_Clears_Desc_And_Sets_Hint` :89 | 记条把备注写进条目（`TkNote="hello"`）、清空 `DescText`、提示切到下一个号、`OkTk` 复位、`RecordCount/FileNumber` 同步、`SessionKeys.Desc` 落库为空 | :102-109 ——**保护「记条后清空备注」这条最容易在迁移中丢的行为** |
| `AdvanceTake_Fake_And_End_Set_Dedicated_Hints` :113 | fake/end 两态提示文案 | `HintFake`/`HintEnd` 两条断言（:119、:122）——**删 VM 后这两个常量连字符串都没了** |
| `RewindTake_Restores_Desc_And_ShotNote` :126 | 撤回回填 `RestoredDesc/RestoredShotNote` + 文件号回落 | :139-141 ——现役 `UndoAsync` 不返回备注，若要保此能力必须加返回值 |
| `Volume_Up_Advances_Take_And_Scrolls_Next` :145 | 音量上→记条 + 次号列滚动 + 会话次号 +1 + 清空备注 + 历史关键字 `TakeIndex+1` | :151-156 ——**唯一覆盖硬件键通道与其滚动副作用的测试** |
| `Volume_Down_Rewinds_And_Does_Not_Scroll` :160 | 音量下→只撤回、**不滚动**次号列 | :172-174 ——契约级差异（上滚下不滚） |
| `ToggleLink_Flips_And_Persists` :178 | 联动开关翻转并写 `SessionKeys.IsLinked` | :182-187 |
| `FileNumber_Edits_Go_Through_Service_And_Update_Display` :191 | 改号/改分隔符/改前缀后显示串、`RecordCount`、`CustomPrefix` 落库 | :197-208 ——现役改为 `SetNamingAsync` 会**重算文件号与清 undo**，此处契约不同 |
| `EditFileNumber_Clamps_To_One` :212 | `<1` 静默 clamp 到 1 | :216 ——现役同输入会抛异常 |
| `Column_Change_Syncs_Session_And_Scene_Change_Resets_Shot_Take` :220 | 挑列→会话索引；切场重置镜/次；切镜重置次 | :226-237 |
| `Session_Change_Syncs_Columns_Back` :241 | 会话索引变化反向回写列 | :246-249 ——现役无反向镜像机制 |
| `Deactivate_Unsubscribes_Hardware_Keys_And_File_Number` :253 | 反激活后音量键不再记条（`History` 为空） | :259 ——**唯一覆盖生命周期退订的测试** |
| `Activate_Is_Idempotent` :263 | 重复 `Activate()` 不重复订阅/不抛 | :269 |

另需一并登记：`tests/VoiSlate.Tests/ViewModels/SlateColumnViewModelTests.cs` 的 8 个用例（`SelectedItem_Tracks_Index` :15、`SetItems_With_Duplicates_Throws` :25、`SetItems_Shrinking_Clamps_SelectedIndex` :32、`PropertyChanged_Raised_For_SelectedIndex_And_SelectedItem` :42、`ScrollTo_Clamps_Out_Of_Range` :55、`ScrollNext_Stops_At_Last_And_Respects_Link` :69、`ScrollPrev_Stops_At_Zero_And_Decs_Even_When_Unlinked` :85、`Empty_Column_Is_NoOp` :98）——`SlateColumnViewModel` 的**唯一消费者就是 `RecordViewModel`**（`src/VoiSlate/ViewModels/RecordViewModel.cs:59-76`），删 VM 必然连带删掉该文件与这 8 个用例，现役没有等价物（挑列语义在 `DesktopWorkspaceView` 的 ComboBox/NumericUpDown 里，且「到顶不循环」这条契约无人保护）。

结论性提示：`TakeFlowServiceTests`（`tests/VoiSlate.Tests/TakeFlowServiceTests.cs`）不依赖 VM，删 VM 后仍绿，但保护的是**已无界面消费**的写入路径——不要因为「测试还绿」就认为覆盖已闭环。