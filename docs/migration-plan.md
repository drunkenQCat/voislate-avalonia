# VoiSlate → Avalonia 迁移计划（migration-plan.md）

> 版本：v0.3（已闭环 Review 第 1 轮全部 14 条建议）｜最后更新：2026-08-20
> 状态：🔄 五轮 Review 迭代中（已完成 1/5）

---

## 1. 项目目标

将 Flutter 应用 **voislate**（声音场记无纸化 App，拍板场记工具）的领域模型、状态管理、业务逻辑、页面结构与交互行为，迁移为符合 **Avalonia 架构习惯（MVVM + CommunityToolkit.Mvvm + DI）** 的全新应用。

**核心约束：**
- ❌ 绝不运行 / 调试 / 截图原 Flutter 应用——只做源码分析（已完成三份独立分析报告）
- 🔇 ifly ASR 与录音能力 **Mock**（接口先行，新 App 后续接其他服务）
- ✅ 复用已搭好的环境：.NET 10.0.400 + Avalonia 12.1.1（`avalonia-android-lab`）
- ✅ 通用能力优先使用成熟 NuGet 库，不重复造轮子

## 2. Flutter 项目现状（源码分析结论）

### 2.1 领域定位
声音场记：拍摄现场记录每一条录音的场/镜/次、文件名编号、备注与 OK/NG 评价；支持拍摄计划（场→镜→标签/对象/备注）、按日期查改场记、JSON 导出/备份、补录（Wild）与收工（End）拍板语义。

### 2.2 模块规模
40 dart 文件 / 6665 行。分层：models(9+2 gen) / data(3) / helper(3) / providers(4) / pages(6) / widgets(12)。

### 2.3 核心业务规则（精确版——含 Review 1 时序修正）

| # | 规则 | 说明 |
|---|---|---|
| B1 | 日志 key = 上一拍文件名 `prevFileName()`；文件号=1 时首按不写日志 | 先 1→2，第二按记文件 1 |
| B2 | 收工(End)：**不递增文件号**，history 记 'OK'；**'OK' 拦截只生效一次**——第二次按 + 恢复记条，且该条 tk 与收工条相同（原缺陷）。迁移决策：**保留原行为**（复刻），记录到已知行为清单 | shotEndBtn |
| B3 | 假拍/野拍语义：**fake/wild 判定读 picker_history 尾关键字而非本次入参**——首按假拍/野拍实际写 normal 日志，第二次才按 fake/wild 生效（原缺陷）。迁移决策：**保留原行为**（复刻），写单测锁定 | addItem |
| B4 | 假拍条：tk=999、okTk=bad、tkNote='Fake Take'，文件号照常递增；野拍条：tk=0、tkNote='wild track …'，仅补录模式触发 | — |
| B5 | 镜头切换自动 tk=ok/sht=nice；评价先落 TkPending → 同步存储（oktk/oksht）→ 记条写入 → 记条后重置 notChecked | — |
| B6 | 文件名 = prefix + linker(-T) + 编号(D3)；prefix 三模式：默认日期 yymmdd / 声音设备 yyYmMd / 自定义 | RecordFileNum |
| B7 | 文件号下限 1；recordCount（持久化）与 fileNum.number 三处双向同步 | FileCounter 双绑 |
| B8 | shtNote 麦克风对象协议 `正文<对象1/>…`；MicObjectsExtractor 解析 | split '<'、剥 '/>' |
| B9 | 跨天：recordCount 重置 1、picker_history 清空、新日期登记 | main.dart |
| B10 | SlatePickerState.numList 重复抛异常；DataList 按 name 唯一，add/insert/update 抛 DuplicateItemException | — |
| B11 | 撤回空 catch 吞 RangeError → 状态漂移（原缺陷）；**迁移决策：改为显式异常处理 + 回滚顺序调整（先查可删再回退文件号）** | — |

**Review 1 新增事实（需决策项）**：
- F1 原应用**无日志导入功能**（deserialize 被注释）；三处导出（速览/设置导出全部/备份）**均不含日期字段**——**导出格式保持原样**（无日期字段），文件名带日期区分
- F2 '导出全部' = 跨日合并导出，**不可逆**——保留
- F3 '清空所有场记' 后 picker_history **仍残留**——保留原行为
- F4 日期 Tab 是**启动时静态快照**，运行中不跨天——DayRolloverService 只在启动时补偿 + 定时检测（新能力，触发=启动 + PeriodicTimer）

### 2.4 状态管理与业务流（S2 报告浓缩）
装配：两个根级 ChangeNotifier（SlateStatusNotifier 13 字段即时写 Hive 'scn_sht_tk'、SlateLogNotifier 今日日志双写）；SlatePickerState×3 / RecordFileNum / ScrollValueController / TkPending 为页面级对象。
核心流：**记条** addItem（读 history → B1-B4 守卫与分支 → 构造 LogItem → add(prevFileName,item) → history 追加 → 非 end 递增 → 重置评价 → setIndex(count) → 震动）；**撤回** drawBackItem（'OK' 分支回填备注；否则 decrement→回填→删 history→removeLast）；**评价** Dial（声音可/弃、画面保/过）；**音量键** ↑记条 / ↓撤回（仅 record_page 接线）；**文件号编辑** 三卡片长按；**备份** 3 分钟全量 JSON；**计划页** 场/镜选择驱动会话索引、增删/拖拽/CSV 导入全量重写；**场记查改** 日期 Tab → 树 → LogEditor 直写盒（旁路，收敛）。

### 2.5 页面与交互（S3 报告浓缩）
主框架：底部 3 Tab（计划/记录/场记，debug +识别测试），TabBarView 禁滑动初始记录页，设置页 push。
记录页：三列 CupertinoPicker 滚轮（take=1..200）+ 文件号三卡片 + **水平滑动确认条**（右滑>slideLength 写镜头标注、左滑<0 写录音标注、松手 200ms 回弹、背景红→绿）+ **SpeedDial 评价弹钮**（声音可/弃、画面保/过）+ 补录开关（切 !isLinked 走 FileCounter、记条强制 wild）+ 内置 ifly（Mock）。
计划页：场→镜→详情；点选驱动索引；拖拽排序；CSV 导入；NoteEditor 60% bottom sheet（Tags 增删/类型/概要）。
场记页：日期 Tab → ExpansionTile 树（行色=okTk 灰/绿/红、trailing=okSht）；LogEditor 全屏；共享 JSON。
设置页：工程名/清空今日/重置（关应用式退出）/清空计划（退出）/导出全部。
主题：flex_color_scheme **bahamaBlue** 亮色 M3，无暗色；全项目无 CustomPaint。

### 2.6 持久化布局（Hive → LiteDB）

| Hive 盒 | 内容 | .NET 落点 |
|---|---|---|
| scn_sht_tk | 13 标量 key | LiteDB 单文档（设置集合） |
| dates + 每日期盒 | 日期登记 + 每日 List<SlateLogItem>（key=上一拍文件名） | LiteDB `slate_logs` 集合（Date 字段） |
| scenes_box | 计划树 | LiteDB `scenes` |
| picker_history | [场,镜,tk关键字,...对象] | 内存 + LiteDB 备份 |
| settings | project | 设置集合 |

**存储访问纪律（Review 1 采纳）**：所有读写只经 Repository/Store 接口（`ILogRepository`/`ScheduleStore`/`ISessionSettingsStore`/`IPickerHistoryStore`），禁止 View/VM 直连存储。

### 2.7 已知缺陷与决策（Review 1 补充）
| 缺陷 | 决策 |
|---|---|
| setNote 不 notify | 修复（绑定天然通知） |
| 空 catch 吞 RangeError 状态漂移 | 修复（B11） |
| `_data == []` const 引用比较 | 修复 |
| `data[]=` 绕过重复校验 | **修复**（索引器也走校验） |
| ScheduleItem.fromJson 空壳 | 删除 |
| notes.sublist(40) 疑似取错段 | 保留原行为（复刻） |
| RecordFileNum.dispose 未调用 | 修复（IDisposable 统一释放） |
| android_physical_buttons 死依赖 | 不迁移 |
| local_notification 实为日期 stub | 归并 DayRolloverService |
| CSV 导入 objects 硬编码 ['Boom'] | **修复**（CSV 无对象列 → 保留默认但写入文档说明） |
| 计划页删除无 undo | **新增简化 undo**（SnackBar 式撤销上一步，Avalonia 自实现小工具） |
| 计划页删除至空 | 保留「至少 1 场 1 镜」约束 |
| 设置页"音量键控制/操作模式"stub | 不迁移 |
| 日期 Tab 静态快照 | 启动时补偿 + 定时检测（F4） |
| 重置操作 = 退出应用 | 桌面化为 DialogService 确认 + 重置后退出（保留语义） |

## 3. Flutter → Avalonia 架构映射（Review 1 修订）

| Flutter | Avalonia/.NET | 库 |
|---|---|---|
| Material3 + flex_color_scheme(bahamaBlue) | FluentTheme + bahamaBlue 资源 | Avalonia 12.1.1 |
| provider/ChangeNotifier ×2 | DI 单例 VM（ObservableObject） | CommunityToolkit.Mvvm |
| SlateStatusNotifier | `RecordingSessionViewModel`（13 ObservableProperty） | 同上 |
| SlateLogNotifier | `SlateLogViewModel` + ObservableCollection | 同上 |
| SlatePickerState×3 | `SlateColumnViewModel`（数据/索引，**不含滚动实现**） | — |
| CupertinoPicker | `SlateWheel` 自绘控件（**滚动状态归控件**） | 自研（核心工作量） |
| ScrollValueController | `IShortcutInputService` + `IHardwareKeyService` | 接口 |
| RecordFileNum | `FileNumberingService`（**RecordViewModel Scoped，非全局**） | — |
| addItem/drawBack 编排 | **`ITakeFlowService`（新增，Review 1 采纳）**：记条/撤回/收工/假野拍全流程 + 状态机，可单测 | — |
| picker_history | `IPickerHistoryStore`（新增） | — |
| Hive | LiteDB | LiteDB 5.x |
| dart_json_mapper | System.Text.Json（camelCase + JsonStringEnumConverter） | BCL |
| csv ^6 | CsvHelper（7 列） | CsvHelper |
| file_picker/share_plus | StorageProvider | Avalonia 内置 |
| toast/easyloading | `IToastService` + LoadingOverlay | 自研小组件 |
| logger | Serilog | Serilog |
| record/ifly | `IRecordingService`/`IAsrService` Mock | 接口先行 |
| SpeedDial | 弹出 FAB（Flyout） | 自研 |
| vibration/permission | IHapticsService(no-op)/权限抽象 | 接口 |

## 4. 技术选型决策（ADR）

- **ADR-001 存储 LiteDB**：单文件、无迁移、文档集合映射 Hive 盒；数据 `%AppData%/VoiSlate/voislate.db`；**所有访问经 Repository 接口（G1 纪律）**；LiteDB 线程安全注意点记录（风险 CR3）。
- **ADR-002 ASR/录音 Mock**：IAsrService（Start/Stop/Result/Status 事件）Mock 1.2s 返回确定性转写；IRecordingService 模拟电平。
- **ADR-003 MVVM=CommunityToolkit.Mvvm**：[ObservableProperty]/RelayCommand/WeakReferenceMessenger。
- **ADR-004 导航**：单主窗口 + 左侧导航四页；SlateWheel/SlideConfirmBar/DialFAB 自绘控件；LogEditor/NoteEditor 对话框。
- **ADR-005 序列化**：camelCase + 枚举字符串；**导出格式与原件一致（无日期字段、含 fake/wild 哨兵值）**；`JsonSerializer` 注释指明兼容对象。
- **ADR-006 备份**：IBackupService，PeriodicTimer(3min) + 退出前 + 手动；`Documents/VoiSlate Logs/slate_backup{yymmdd}-{hour}clock.json`。
- **ADR-007 日志**：Serilog 滚动文件 + Debug 控制台；ITimeProvider 注入。
- **ADR-008 生命周期**（Review 1 新增）：DI 容器统一管理 IDisposable（LiteDB/PeriodicTimer/事件订阅），应用退出顺序：停定时器 → 备份 → 关库。
- **ADR-009 错误策略**（Review 1 新增）：业务异常（DuplicateItemException 等）由 VM 捕获转对话框/Toast；IO/存储异常统一 ILogger.Error + Toast，不崩溃；不复制原空 catch。

## 5. 新项目结构与 Agent 分工（Review 1 修订）

```
voislate-avalonia/
├── docs/{migration-plan.md, contracts.md}
├── .github/workflows/ci.yml
├── src/VoiSlate/
│   ├── App.axaml(.cs)            # DI、主题
│   ├── Models/                   # SlateLogItem/TkStatus/ShtStatus/ScheduleItem/Note/
│   │                             # SceneSchedule/DataList/RecorderType/TakeType/…
│   ├── Services/                 # IAsrService(Mock)/IRecordingService(Mock)/IFileNamingService/
│   │                             # ITakeFlowService/SessionSettingsStore/ILogRepository/
│   │                             # ScheduleStore/IPickerHistoryStore/CsvScheduleParser/
│   │                             # MicObjectsExtractor/IBackupService/ITimeProvider/IToastService/
│   │                             # IHapticsService/IHardwareKeyService/DayRolloverService/IExportService
│   ├── ViewModels/               # 见 contracts.md §4
│   ├── Views/                    # MainWindow + 四页 + 对话框
│   ├── Controls/                 # SlateWheel/SlideConfirmBar/DialFAB/FileCounter/TagChips/Toast/LoadingOverlay
│   ├── Infrastructure/           # LiteDbStore/JsonOptions
│   └── Assets/
└── tests/VoiSlate.Tests         # xUnit
```

**Agent 分工（git worktree）**：

| Agent | 模块 | 依赖 | 输出 |
|---|---|---|---|
| A | Models + 枚举 + 集合校验 | 无 | Models/*.cs + 单测 |
| E | Services 全层（含 ITakeFlowService/IPickerHistoryStore/Mocks） | A | Services + Infra + 单测 |
| B | ViewModels | A,E | ViewModels + 单测 |
| D | 公共控件与主题 | 契约（contracts.md） | Controls + Themes |
| C | 页面视图与导航 | B,D | Views + App.axaml |

## 6. 开发顺序（含 P0.5 垂直切片——Review 1 采纳）

- **M0 骨架**（主 Agent）：sln + csproj（net10.0 + Avalonia 12.1.1 + CommunityToolkit.Mvvm + LiteDB + CsvHelper + Serilog）+ DI + 主题色板 + CI 骨架
- **P0.5 垂直切片**（主 Agent）：**一条记录链路贯通**——Models(SlateLogItem) + ITakeFlowService(记条/撤回核心时序 B1-B5) + LiteDB 存储 + 单测 + 冒烟（可运行最小 UI 触发一次记条落库）。验证契约与存储保序，再开五 worktree
- **P1 并行**：worktree A / E / B / C / D（依赖 A→E→B→C；D 全程并行）
- **P2 合并**：A→E→B→D→C，解冲突，统一命名
- **P3 验证**：dotnet build 0 错误无显著 warning → dotnet test 全绿 → 冒烟（含 **重启恢复** 与 **跨天假时钟**（ITimeProvider 注入）两项 Review 1 补充）
- **P4 收尾**：Android 目标编译验证 + README + CI
- **P5 发布**：git + gh 推送

## 7. 风险点（含 Review 1 CR1-CR13）

| # | 风险 | 缓解 |
|---|---|---|
| R1 | Hive 旧数据不可直接读取 | 全新 LiteDB；种子重建；旧数据迁移不在本期 |
| R2 | 三列滚轮自绘成本 | 精简实现 + 契约先行 |
| R3 | LiteDB 线程安全（多定时器/命令并发） | 单实例 + 串行化访问 + Repository 单入口 |
| R4 | 每小时备份文件同名覆盖 | 备份文件名含时间戳（小时级）已有；文档注明 |
| R5 | 导出无日期字段的兼容歧义 | 文件名带日期 + 文档标注 |
| R6 | B2/B3 原时序缺陷复刻带来的困惑 | 单测锁定 + 已知行为清单 |
| R7 | 记录页 Tab 缓存导致计划数据陈旧 | 页面激活时刷新计划数据（MessageBus 通知） |
| R8 | 契约覆盖不全（控件事件签名漂移） | contracts.md 为唯一依据，变更须更新 |
| R9 | 并行冲突 | worktree + 模块边界 + 契约 |
| R10 | Android 平台能力降级 | 接口 no-op/Mock，Android 增强后续 |
| R11 | 绑定遗漏导致运行期无响应 | 冒烟清单逐项手测（§9） |
| R12 | 重置类操作误触 | DialogService 强确认 |
| R13 | B11 回滚顺序改动引入回归 | ITakeFlowService 单测覆盖撤回全分支 |

## 8. 契约

见 `docs/contracts.md`（v0.1）：Services 接口签名、VM 成员与命令、控件绑定协议（SlateWheel/SlideConfirmBar/DialFAB/FileCounter/TagChips/Toast/LoadingOverlay）、导航与主题资源键、编译质量基线。

## 9. 验收标准

1. `dotnet build` 0 错误、无显著 warning；`dotnet test` 全绿（覆盖：B1-B11 时序含首按语义、CSV 7 列解析、MicExtractor、命名规则、跨天补偿、撤回全分支）
2. App 可独立运行：主窗口 + 四页导航
3. 记录页：三列滚轮切换、文件号编辑、滑动确认条左右写备注、评价 FAB、补录开关、记条/撤回全流程落库、模拟 ASR 写备注
4. 计划页：CSV 导入、场/镜增删改、标签编辑、重复检测、undo、至少 1 场 1 镜、选择联动记录页
5. 场记页：按日期查看、树展开、LogEditor 编辑保存、JSON 导出兼容
6. 设置页：工程名、清空今日、导出全部、重置确认
7. 备份：3 分钟定时 + 手动 + 退出前；**重启后数据完整（P3 补）**
8. **P0.5 垂直切片在五 worktree 前验证通过（Gate 1 前置）**
9. Android net10.0-android 可编译
10. GitHub：仓库 + CI 通过 + 完整提交记录

## 10. 阶段门禁（Gate）

- **Gate 0**：五轮 review 全部闭环 → M0
- **Gate 1**：M0 + **P0.5 垂直切片**（记条链路 build+test+冒烟）→ 开五 worktree
- **Gate 2**：各 worktree 模块 build + 单测 → 合并
- **Gate 3**：合并后全量 build + test + 冒烟（含重启恢复/跨天假时钟）→ 发布

---
*（v0.3：闭环 Review 1 的 14 条建议——新增 ITakeFlowService/IPickerHistoryStore、P0.5 垂直切片、B2/B3 时序修正、F1-F4 事实、ADR-008/009、CR1-CR13、存储访问纪律、undo 与索引随动；下一步：Review 第 2 轮——拆分与 Agent 分工审查）*