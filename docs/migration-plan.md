# VoiSlate → Avalonia 迁移计划（migration-plan.md）

> 版本：v0.2（完整版，三份源码分析报告已整合）｜最后更新：2026-08-20
> 状态：🔄 进入五轮 Review 迭代（第 1 轮即将开始）

---

## 1. 项目目标

将 Flutter 应用 **voislate**（声音场记无纸化 App，拍板场记工具）的领域模型、状态管理、业务逻辑、页面结构与交互行为，迁移为符合 **Avalonia 架构习惯（MVVM + CommunityToolkit.Mvvm + DI）** 的全新桌面/Android 应用。

**核心约束：**
- ❌ 绝不运行 / 调试 / 截图 / 自动化测试原 Flutter 应用——只做源码分析（已完成，三份独立分析报告）
- 🔇 ifly 语音识别（ASR）与录音能力 **Mock**（接口先行，新 App 后续接入其他服务）
- ✅ 复用已搭好的环境：.NET 10.0.400 + Avalonia 12.1.1（`avalonia-android-lab`，含 Android SDK/workload）
- ✅ 通用能力优先使用成熟 NuGet 库（见 §4），不重复造轮子

## 2. Flutter 项目现状（源码分析结论）

### 2.1 领域定位
声音场记：拍摄现场记录**每一条录音**的 场/镜/次、文件名编号、备注与 OK/NG 评价；支持拍摄计划（场→镜→标签/对象/备注）、按日期查改场记、JSON 导出/备份、补录（Wild）与收工（End）等拍板语义。

### 2.2 模块规模
40 dart 文件 / 6665 行。分层：models(9+2 gen) / data(3) / helper(3) / providers(4) / pages(6) / widgets(12)。

### 2.3 核心业务规则（必须保留的语义——S2/S3 报告确认）

| # | 规则 | 来源 |
|---|---|---|
| B1 | 日志 key = **上一拍文件名** `prevFileName()`；文件号=1 时首按不写日志（先 1→2，第二按记文件 1） | addItem/addNewLog |
| B2 | 收工(End/'OK')：**不递增文件号**，history 记 'OK'，之后 normal 记录被拦截，撤回收工只回填备注不删日志 | shotEndBtn / drawBackItem |
| B3 | 假拍(Fake)：tk=**999**、okTk=bad、tkNote='Fake Take'，文件号照常递增 | addItem(fake) |
| B4 | 野拍(Wild)：tk=**0**、tkNote='wild track …'，仅补录模式（!isLinked）触发 | addItem 强制 wild |
| B5 | 镜头切换时自动 tk=ok、sht=nice；评价先落 TkPending → 同步 SlateStatus（oktk/oksht）→ 记条时写入 SlateLogItem，记条后重置 notChecked | record_page |
| B6 | 文件名 = `prefix + intervalSymbol(-T) + 编号(D3)`；prefix 三模式：默认日期 `yymmdd` / 声音设备 `yyYmMd`（如 26Y08M20）/ 自定义 | RecordFileNum |
| B7 | 文件号下限 1；`recordCount`（持久化）与 fileNum.number 三处双向同步（记条/撤回/手动编辑） | FileCounter |
| B8 | shtNote 携带麦克风对象协议 `正文<对象1/><对象2/>`；MicObjectsExtractor 解析（split '<'、剥 '/>'） | mic_objects_extractor |
| B9 | 跨天（新日期）：recordCount 重置 1、picker_history 清空、dates 箱登记新日期 | main.dart |
| B10 | SlatePickerState.numList 重复抛异常；DataList 按 name 唯一，add/insert/update 抛 DuplicateItemException | 计划树 |
| B11 | 撤回用**空 catch 吞 RangeError**（状态漂移缺陷）——迁移时改为显式异常处理 | drawBackItem |

### 2.4 状态管理与业务流（S2 报告浓缩）

**装配**：两个根级 ChangeNotifier 注入（MultiProvider）：`SlateStatusNotifier`（13 字段会话状态，全部 setter 即时写 Hive 'scn_sht_tk'）、`SlateLogNotifier`（今日日志双写内存+Hive）；`SlatePickerState×3 / RecordFileNum / ScrollValueController / TkPending` 是页面内对象（record_page 持有）。

**核心流程（record_page 890 行编排）**：
- **记条** `addItem(TakeType)`：读 picker_history 上一条 → 守卫(prevFileName 非空/'OK' 拦截) → 构造 SlateLogItem（fake/wild/normal 分支）→ `logNotifier.add(prevFileName, item)` → history 追加 [场,镜,tk关键字,...对象] → 非 end 则 fileNum.increment() → 重置评价 → `setIndex(count: fileNum.number)` → 振动反馈
- **撤回** `drawBackItem`：'OK' 分支回填备注+删 history 不动文件号；否则 decrement → setIndex(count) → 回填备注 → history 删尾 → logNotifier.removeLast()，空 catch 吞错
- **评价**：TakeOkDial('声音可/声音弃') / ShotOkDial('画面保/画面过') → TkPending + setOkStatus
- **音量键**：↑=记条+take 前进；↓=take 回退+撤回（仅 record_page 接线；`android_physical_buttons` 死依赖）
- **文件号编辑**：三卡片（prefix/分隔符/编号），长按编辑；编号↔recordCount 双向
- **备份**：每 3 分钟全量 JSON 备份到 外部存储/Documents/VoiSlate Logs/slate_backup{yymmdd}-{hour}clock.json
- **计划页**：场/镜选择驱动 SlateStatus.setIndex；增删/拖拽排序/CSV 导入全量重写 scenes_box；NoteEditor 校验重复弹窗
- **场记查改**：按日期 Tab，ExpansionTile 树（场→镜→条），行色=okTk（灰/绿/红）、trailing=okSht；LogEditor 直接改 Hive 盒（旁路，迁移时收敛）

### 2.5 页面与交互（S3 报告浓缩 + 修正）

| 页面 | 关键结构 | 交互要点 |
|---|---|---|
| 主框架 | 底部导航 3 Tab（计划/记录/场记，debug +识别测试）| TabBarView 禁滑动，初始=记录页；设置页 push |
| 记录页 | 场/镜/次 三列滚轮 + 文件号卡片 + 滑动确认条 + SpeedDial 评价 + 备注输入 | ① 三列 **CupertinoPicker 滚轮**（take 列=1..200）② recorder_joystick = **水平滑动确认条**（按下滑动；右滑>slideLength→识结果写镜头标注；左滑<0→写录音标注；松手 200ms 回弹；背景红→绿插值）内含 ifly ASR（Mock）③ shot_ok_dial/take_ok_dial = **SpeedDial 浮动按钮弹窗**（非转盘！）取值 TkStatus/ShtStatus ④ 补录开关：切 !isLinked 后走 FileCounter 输入、记条强制 wild |
| 计划页 | 场列表→镜列表→详情 | 点场/镜 → setIndex；拖拽排序；CSV 导入；NoteEditor 60% 高 bottom sheet（Tags 增删/类型/概要） |
| 场记页 | 日期 Tab → 场/镜 分组 ExpansionTile | 行点击 → LogEditor 全屏编辑（就地改+putAt）；共享 JSON |
| 设置页 | 工程名 / 清空今日 / 重置全删(重启) / 清空计划(重启) / 导出全部 | 输入即存 |

**主题**：flex_color_scheme **bahamaBlue** 亮色 M3（无暗色、无自定义字体）、`assets/bookmark.png`。
**全项目无 CustomPaint**；无 debounce/throttle；无 shared_preferences 实际使用（声明未用）。

### 2.6 持久化布局（Hive → LiteDB/JSON 映射依据）

| Hive 盒 | 内容 | .NET 落点 |
|---|---|---|
| `scn_sht_tk` | 13 个标量 key（索引/开关/计数/命名/备注/评价） | 设置存储（LiteDB 单文档 或 JSON 文件） |
| `dates` + 每日期盒 | 日期登记 + 每日 `List<SlateLogItem>`（key=上一拍文件名） | LiteDB：`slate_logs` 集合（date 字段）或 JSON 文件按日 |
| `scenes_box` | 计划树（SceneSchedule 列表） | LiteDB `scenes` 集合 |
| `picker_history` | 最近一条 [场,镜,tk关键字,...对象] | 内存 + 落盘（重启可恢复） |
| `settings` | project 名等 | 设置存储 |

**序列化双轨**：JSON（dart_json_mapper）仅覆盖 SlateLogItem 枚举（短名序列化）；日程仅 Hive。**JSON 导出格式 = 兼容关键点**：字段名 camelCase、枚举字符串（ok/bad/nice）。

### 2.7 已知缺陷（迁移时不复制）
setNote 不 notify；空 catch 吞 RangeError 状态漂移；`_data == []` const 引用比较；ScheduleItem.fromJson 空壳；`notes.sublist(40)` 疑似取错段；RecordFileNum.dispose 未调用（流泄漏）；死依赖 android_physical_buttons；local_notification.dart 实为日期检测 stub；CSV 导入 objects 硬编码 ['Boom']。

## 3. Flutter → Avalonia 架构映射

| Flutter | Avalonia/.NET | 库 |
|---|---|---|
| Material 3 + flex_color_scheme(bahamaBlue) | FluentTheme + 自定义 bahamaBlue 色板资源 | Avalonia 12.1.1 |
| provider/ChangeNotifier ×2 根级 | DI 单例 ViewModel（ObservableObject） | CommunityToolkit.Mvvm 8.4.2 |
| SlateStatusNotifier(13 字段) | `RecordingSessionViewModel`（[ObservableProperty]×13） | 同上 |
| SlateLogNotifier | `SlateLogViewModel` + `ObservableCollection<SlateLogItem>` | 同上 |
| SlatePickerState×3 | `SlateColumnViewModel`（每列实例）+ 自绘滚轮控件 | 自研控件（核心工作量） |
| ScrollValueController(音量键) | `ShortcutInputService` + `IHardwareKeyService`(Android 增强) | 接口 |
| RecordFileNum + StreamController | `FileNumberingService`（event NumberChanged） | — |
| TkPending | RecordingSessionViewModel 的 Pending 属性 | — |
| Hive | **LiteDB**（单文件文档库） | LiteDB 5.x |
| dart_json_mapper | System.Text.Json（camelCase + 枚举字符串） | BCL |
| csv ^6 | CsvHelper（7 列结构照搬） | CsvHelper |
| file_picker / share_plus | StorageProvider 保存/打开对话框 | Avalonia 内置 |
| fluttertoast / easyloading | 轻量 Toast 服务 + 覆盖层（自定义小组件） | 自研（简单）或社区 |
| logger | Serilog（控制台+文件） | Serilog + Sinks |
| record / ifly_speech_recognition | `IRecordingService` / `IAsrService` **Mock 实现** | 接口先行 |
| flutter_speed_dial | 自定义弹出 FAB（Button + Popup/Flyout） | 自研小组件 |
| vibration / permission | IHapticsService(no-op) / 权限抽象 | 接口 + Android 平台项 |

## 4. 技术选型决策（ADR）

- **ADR-001 存储：LiteDB**。单文件、免迁移、文档集合天然映射 Hive 盒子模型；`settings`/`scn_sht_tk` → 单文档；每日日志 → `slate_logs` 集合带 `Date` 字段；计划 → `scenes` 集合。数据落 `%AppData%/VoiSlate/voislate.db`。**理由**：SQLite 需建表迁移且无必要（无关系查询）；JSON 文件并发/事务弱。
- **ADR-002 ASR/录音 Mock**：`IAsrService`（Start/Stop/Result 事件流/可识别中状态）、`IRecordingService`（权限/Start/Stop/Level），现注入 Mock；录音流驱动状态机与真实一致。
- **ADR-003 MVVM=CommunityToolkit.Mvvm**（模板内置）：[ObservableProperty] 源生成、RelayCommand、WeakReferenceMessenger（页面间消息：TagEditingMessage/历史跳转）。
- **ADR-004 导航**：单主窗口 + 左侧导航（TabControl 风格）四页：记录/计划/场记/设置；滚轮/Dial/Joystick 为自绘 Custom Control；LogEditor/NoteEditor 为对话框（Window）。
- **ADR-005 序列化**：`JsonSerializerOptions {PropertyNamingPolicy = CamelCase, Converters = {JsonStringEnumConverter}}` 对齐旧导出；导出对象含版本字段 `"schema": 1`。
- **ADR-006 备份**：`IBackupService`，PeriodicTimer(3min) + 退出前 + 手动，JSON 写入 `Documents/VoiSlate Logs/`。
- **ADR-007 日志**：Serilog 文件滚动 + Debug 控制台；`ITimeProvider` 抽象注入所有日期逻辑（可测性）。

## 5. 新项目结构与 Agent 分工

```
voislate-avalonia/  (git, main 分支)
├── docs/migration-plan.md
├── .github/workflows/ci.yml          # build + test + (android apk 可选)
├── src/
│   └── VoiSlate/                     # Avalonia 应用（net10.0）
│       ├── App.axaml(.cs)            # DI 装配、主题
│       ├── Models/                   # SlateLogItem/TkStatus/ShtStatus/ScheduleItem/Note/
│       │                             # SceneSchedule/DataList/RecorderType/RecordFileNum/
│       │                             # TakeType/TagEditingMessage/TkPending
│       ├── Services/                 # IAsrService(Mock)/IRecordingService(Mock)/
│       │                             # IFileNamingService/ISessionSettingsStore/ILogRepository/
│       │                             # ScheduleStore/CsvScheduleParser/MicObjectsExtractor/
│       │                             # IBackupService/ITimeProvider/IToastService/IHapticsService/
│       │                             # IShortcutInputService/SeedService/DayRolloverService
│       ├── ViewModels/               # MainViewModel/RecordingSessionViewModel/SlateLogViewModel/
│       │                             # SlateColumnViewModel/RecordViewModel/ScheduleViewModel/
│       │                             # SlateLogPageViewModel/SettingsViewModel/LogEditorViewModel/
│       │                             # NoteEditorViewModel
│       ├── Views/                    # MainWindow + Page Views
│       ├── Controls/                 # SlateWheel(滚轮)/SlideConfirmBar(确认条)/DialFAB/
│       │                             # FileCounter/TagChips/Toast/LoadingOverlay
│       ├── Infrastructure/           # LiteDbStore、JsonOptions 等
│       └── Assets/
└── tests/VoiSlate.Tests             # xUnit：编号器/CSV/提取器/会话状态机/回滚流程
```

**Agent 分工（git worktree，每人独立分支+commit）**：

| Agent | 模块 | 依赖 | 输出 |
|---|---|---|---|
| A | Models 层 + 常量/枚举 + 缺陷修复版集合逻辑 | 无 | Models/*.cs + 单测 |
| E | Services 层（存储/CSV/提取器/Mock 服务/备份/种子） | A | Services + Infra + 单测 |
| B | ViewModels（会话/日志/记录编排/列/页面 VM） | A,E | ViewModels + 单测 |
| D | 公共控件与主题（滚轮/确认条/DialFAB/FileCounter/TagChips/Toast/色板） | 无（契约先行：绑定属性名按计划 §8） | Controls/ + Themes/ |
| C | 页面视图与导航（MainWindow/四页/对话框） | B,D | Views/ + App.axaml |

**契约先行**：计划定稿后定义控件绑定协议（例如 SlateWheel 暴露 `Items/SelectedIndex/SelectedItemChanged`；SlideConfirmBar 暴露 `SlideResult(SlideDirection, string)`），使 B/C/D 可并行。

## 6. 开发顺序（主 Agent 串行骨架，模块并行）

1. **M0 骨架**（主 Agent）：sln + csproj（net10.0 + Avalonia 12.1.1 + CommunityToolkit.Mvvm + LiteDB + CsvHelper + Serilog）+ DI + 主题色板 + git 分支策略 + CI 骨架
2. **P1 并行**：worktree A(E) → B → C/D（依赖链 A→E→B→C，D 可全程并行）
3. **P2 合并**：按 A→E→B→D→C 顺序 merge，解冲突，统一命名
4. **P3 验证**：`dotnet build`（0 error / 无显著 warning）→ `dotnet test` → 运行冒烟（窗口可开、四页可切、模拟记条流程可写日志）
5. **P4 收尾**：Android 目标编译验证一次 + README + CI
6. **P5 发布**：git 提交规范 + gh 创建私有仓库 + push + 输出 commit hash

## 7. 风险点

| # | 风险 | 缓解 |
|---|---|---|
| R1 | Hive 旧数据无法直接读取 | 全新 LiteDB；种子从 dummy_data 重建；旧数据迁移不在本期（文档注明） |
| R2 | 三列滚轮自绘工作量最大 | 精简实现：基本滚动+缩放+选中高亮，动画从简；契约先行隔离 |
| R3 | Mock ASR 与原交互时序差异 | Mock 按 B1-B8 规则返回确定性结果（1.2s 后返回 '示例转写'） |
| R4 | 音量键/振动等平台能力 | 抽象后 no-op/Mock，Android 平台注入为后续增强 |
| R5 | 并行冲突 | worktree + 契约 + 模块边界（Models/Services/ViewModels/Views/Controls 无重叠） |
| R6 | 绑定遗漏导致运行期无响应 | 冒烟清单（§9 验收 3-6）逐项手测 |

## 8. 契约（控件绑定协议草案，B/C/D 并行依据）

- `SlateWheel`：`Items:IReadOnlyList<string>`、`SelectedIndex:int`（TwoWay）、`SelectedItemChanged` 事件、可滚动（滚轮/滑条/触屏拖拽）
- `SlideConfirmBar`：`state:Idle/Pressed/Armed`、`Result(SlideSide)` 事件（Left=写左侧录标注 / Right=写右镜头标注）、`IsRecording`、`RecordDuration`、`TranscriptionHint`
- `DialFAB`：`Options:枚举集合` + `SelectionChanged` 事件（对应 声音可/弃、画面保/过）
- `FileCounter`：`Prefix/Linker/Number` 可编辑栏 + `NumberChanged`
- `Toast`：`IToastService.Show(message)`；`LoadingOverlay`：`IsActive/Dismiss`

## 9. 验收标准

1. `dotnet build` 0 错误、无显著 warning；`dotnet test` 全绿（覆盖：文件编号规则 B6、CSV 解析、MicExtractor、会话状态机 B5/B7/B9、记条/撤回/收工/假拍/野拍流程 B1-B4）
2. App 可独立运行：主窗口 + 四页导航（记录/计划/场记/设置）
3. 记录页：三列滚轮切换、文件号三卡片编辑、滑动确认条（左右滑动写备注）、评价 FAB（声音可/弃、画面保/过）、补录开关、记条/撤回全流程落库、模拟 ASR 结果写入备注
4. 计划页：CSV 导入（7 列）、场/镜增删改、标签编辑、重复检测弹窗、选择联动记录页
5. 场记页：按日期查看、展开树、LogEditor 编辑保存、JSON 导出格式兼容（camelCase + 枚举字符串）
6. 设置页：工程名、清空今日、导出全部；（重置类操作本期可为禁用或确认对话框）
7. 备份服务：3 分钟定时 + 手动备份 JSON 生成
8. Android 目标 `net10.0-android` 可编译（复用 avalonia-android-lab 环境），桌面端为验收主形态
9. GitHub：仓库创建、CI 通过、提交记录完整（含五轮 review 的计划演进历史）

## 10. 阶段门禁（Gate）

- **Gate 0**：本计划完成五轮独立 review 且全部意见闭环 → 才允许 M0 编码
- **Gate 1**：每模块 worktree 内 `dotnet build` + 本模块单测通过 → 才允许进入合并
- **Gate 2**：合并后全量 build + test + 冒烟清单 → 才允许发布

---
*（v0.2：整合 S1 模型层 / S2 状态管理 / S3 页面交互三份分析报告；下一步：五轮 review）*