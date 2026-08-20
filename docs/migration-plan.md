# VoiSlate → Avalonia 迁移计划（migration-plan.md）

> 版本：v0.1（初稿）｜最后更新：2026-08-20
> 状态：🔄 五轮 Review 迭代中

---

## 1. 项目目标

将 Flutter 应用 **voislate**（声音场记无纸化 App）的业务模型、状态管理、页面结构与交互逻辑，迁移为符合 **Avalonia 架构习惯（MVVM + CommunityToolkit.Mvvm）** 的全新应用。

**核心约束：**
- ❌ 绝不运行 / 调试 / 截图原 Flutter 应用（只做源码分析）
- 🔇 ifly 语音识别（ASR）部分 **Mock**（新 App 将接其他服务）
- ✅ 复用已搭建好的环境：.NET 10 + Avalonia 12.1.1 模板（`avalonia-android-lab`）
- ✅ 优先使用成熟 NuGet 库，不重复造轮子

## 2. Flutter 项目现状（源码分析结论）

### 2.1 领域定位
声音场记（Sound Slate / Sound Logging）：拍摄现场通过按键 + 语音识别快速记录 **每一条录音** 的 场（Scene）/ 镜（Shot）/ 次（Take）、文件名编号、备注与 OK/NG 评价；支持拍摄计划（Schedule）、按日期查改场记、JSON 导出。

### 2.2 模块清单（40 dart 文件 / 6665 行）

| 层 | 文件 | 职责 |
|---|---|---|
| 入口 | `main.dart` | Hive 初始化、种子数据、日期箱管理、`runApp(VoiSlate())` |
| models | recorder_file_num / recorder_type / slate_log_item / slate_schedule / tag_editing_message / take_type / tk_pending | 领域模型 + 枚举 |
| data | dummy_data / ifly_key_example / my_ifly_key | 种子数据、IFLY 密钥占位 |
| helper | local_notification(实为日期变更检测 stub) / mic_objects_extractor / schedule_csv_parser | 工具 |
| providers | slate_log_notifier / slate_picker_notifier / slate_status_notifier / value_scroll_control | ChangeNotifier 状态管理 |
| pages | main_page / record_page / scene_schedule_page(_test) / settings_configue_page / slate_log_page / slate_log_tabs | 页面 |
| widgets | record_page ×9 / scene_schedule_page ×2 / slate_log_page ×1 | 组件 |

### 2.3 数据模型清单（S1 子代理报告 + 主会话精读）

| Flutter 模型 | 关键成员 | 说明 |
|---|---|---|
| `SlateLogItem` | scn / sht / tk / filenamePrefix / filenameLinker / filenameNum / tkNote / shtNote / scnNote / okTk(TkStatus) / okSht(ShtStatus) | 核心日志项；`fileName` 计算属性 = prefix+linker+3位补零编号 |
| `TkStatus` | notChecked / ok / bad | 次状态 |
| `ShtStatus` | notChecked / ok / nice | 镜状态 |
| `ScheduleItem` | scn / sht / desc（含 fromJson 空壳——已发现缺陷） | 计划项 |
| `Note` / `DataList<T>` / `SceneSchedule` | 场景计划容器、重复检测（`DuplicateItemException`、`==[]` 引用比较缺陷） | see schedule |
| `RecorderType` + `Recorder` | default / sound devices / custom 等 | 录音机类型与配置 |
| `RecordFileNum` | customPrefix / recorderType(String) / intervalSymbol / number / value 流 | 前缀三模式（默认日期 YYMMDD / 声音设备 yYMM-DD 变体 / 自定义）；`fullName()` = 前缀+`-T`+3位编号；`prevFileName()` |
| `TakeType` | 补录模式等拍摄类型枚举 | 补录会单独标记 |
| `TagEditingMessage` / `TkPending` | 标签编辑消息 / 待处理次 | 页面间消息传递 |

### 2.4 状态管理（S2 子代理进行中 → 待补）
### 2.5 页面与交互（S3 子代理进行中 → 待补）

## 3. Flutter → Avalonia 技术映射

| 能力 | Flutter 方案 | Avalonia/.NET 方案 | NuGet/生态 |
|---|---|---|---|
| UI 框架 | Material 3 + flex_color_scheme | FluentTheme + 自定义资源 | Avalonia 12.1.1 |
| 状态管理 | provider(ChangeNotifier) | MVVM：ObservableObject + Messenger | CommunityToolkit.Mvvm 8.4.2 |
| 视图模型 | ChangeNotifier | ObservableObject / ObservableCollection / [ObservableProperty] | 同上 |
| 本地存储 | Hive（typeId 二进制） | **LiteDB**（文档库，最贴近 Hive 的 box/key 心智） | LiteDB |
| 键值配置 | Hive box 'settings' / shared_preferences | `SettingsService`（LiteDB 或 JSON 文件） | LiteDB / System.Text.Json |
| 序列化 | dart_json_mapper + reflectable | System.Text.Json + JsonPropertyName + JsonStringEnumConverter | BCL |
| CSV | csv ^6.0.0 | CsvHelper（读写，DateOnly/TimeSpan 支持） | CsvHelper |
| 文件选择 | file_picker | TopLevel.StorageProvider.OpenFilePickerAsync | Avalonia 内置 |
| 分享/导出 | share_plus | 保存对话框导出 JSON / 系统分享（Android） | Avalonia 内置 + mock |
| 录音 | record ^5.2.0（麦克风） | **第一阶段 Mock**（`IRecordingService` 接口 + 模拟实现，后续接 NAudio / 自定义服务） | 接口先行 |
| 语音识别 | ifly_speech_recognition | **Mock**：`IAsrService` 接口 + 假结果（用户指示） | 接口先行 |
| 通知 | local_notification（实为日期变更检测 stub） | 日期变更检测服务（Timer）+ 可选通知 | BCL PeriodicTimer |
| 物理按键 | android_physical_buttons / volume_keydown | Android 平台：按键事件（可选增强） | Avalonia Android |
| 日志 | logger | Serilog（文件 + 控制台） | Serilog |
| Toast | fluttertoast | 自绘轻量 Toast / 状态栏消息 | Avalonia 社区组件评估 |
| 加载指示 | flutter_easyloading | 全局覆盖层（自定义） | 自实现或社区 |
| 主题 | flex_color_scheme（seed） | FluentTheme + ResourceDictionary | Avalonia |

**决策记录（ADR 风格）：**
- ADR-001：本地存储选 **LiteDB**——单文件、无迁移、贴近 Hive 的按箱/按 key 模型，避免 SQL 复杂度；每日期一个 collection 或按 key 前缀分区。
- ADR-002：ASR 与录音全部走接口（`IAsrService` / `IRecordingService`），当前注入 Mock 实现，保证后续无缝替换真实服务。
- ADR-003：MVVM 用 CommunityToolkit.Mvvm（模板已内置，源生成器减少样板）。
- ADR-004：导航用窗口内页面切换（主窗口 + 左侧/顶部导航 或 TabControl），不使用多窗口。

## 4. Avalonia 新项目结构设计

```
voislate-avalonia/
├── VoiSlate.sln
├── src/
│   └── VoiSlate/
│       ├── App.axaml(.cs)              # 入口、DI 装配、主题
│       ├── Models/                     # 迁移模型
│       │   ├── SlateLogItem.cs, TkStatus.cs, ShtStatus.cs
│       │   ├── ScheduleItem.cs, SceneSchedule.cs, Note.cs, DataList.cs
│       │   ├── RecorderType.cs, RecordFileNum.cs
│       │   ├── TakeType.cs, TagEditingMessage.cs, TkPending.cs
│       ├── Services/                   # 服务层（接口 + 实现）
│       │   ├── IAsrService.cs / MockAsrService.cs
│       │   ├── IRecordingService.cs / MockRecordingService.cs
│       │   ├── IDateChangeDetector.cs
│       │   ├── CsvScheduleParser.cs
│       │   ├── ScheduleRepository.cs / SlateLogRepository.cs
│       │   ├── SettingsService.cs / DialogService.cs
│       ├── ViewModels/
│       │   ├── MainViewModel.cs        # 主导航
│       │   ├── RecordViewModel.cs      # 录音页
│       │   ├── ScheduleViewModel.cs    # 拍摄计划页
│       │   ├── SlateLogViewModel.cs    # 场记查改页
│       │   ├── SettingsViewModel.cs    # 设置页
│       │   └── (子 VM：SlatePickerVM / FileCounterVM 等)
│       ├── Views/
│       │   ├── MainWindow.axaml        # 主窗口 + 导航容器
│       │   ├── RecordView.axaml / ScheduleView.axaml / SlateLogView.axaml / SettingsView.axaml
│       │   └── Controls/               # 自定义控件：Joystick、ShotDial、TakeDial、SlatePicker…
│       └── Infrastructure/             # LiteDB 持久化、序列化
```

## 5. ViewModel 划分方案（初稿，S2 报告后定稿）

| Flutter Notifier | Avalonia 对应 | 职责 |
|---|---|---|
| slate_status_notifier | `RecordSessionViewModel`（Service `RecordSessionService`） | 场景/镜/次索引、recordCount、prefix、链接状态、OK 评价、desc/note —— 持久的会话状态 |
| slate_log_notifier | `SlateLogViewModel` | 当日场记列表、增删改、补录标记 |
| slate_picker_notifier | `SlatePickerViewModel` | 拍板选择器的候选项与历史 |
| value_scroll_control | 复用为 `ValueScrollViewModel` | 数值滚轮状态 |
| （无） | `RecordViewModel` 编排层 | 串起 joystick/录音/ASR/文件编号/日志写入 |

## 6. 页面迁移顺序与 Agent 分工（五轮 review 后定稿）

| 顺序 | 模块 | 建议 Agent | 依赖 |
|---|---|---|---|
| 0 | 项目骨架 + DI + 主题 + 仓储接口 | 主 Agent | 无 |
| 1 | Models 层迁移 + 单测 | Agent A | 0 |
| 2 | Services（LiteDB 仓储、CSV、Mock ASR/录音、日期检测） | Agent E | 1 |
| 3 | ViewModels（会话/日志/拍板/记录编排） | Agent B | 1,2 |
| 4 | 主页面 UI（Record/Schedule/Log/Settings + 导航） | Agent C | 2,3 |
| 5 | 公共组件与主题（Joystick/Dial/SlatePicker/TagChips/Toast） | Agent D | 4 并行补充 |

## 7. 风险点

| # | 风险 | 缓解 |
|---|---|---|
| R1 | Hive 二进制数据无法直接复用 | 全新 LiteDB 存储；种子数据从 dummy_data 重建；既有用户数据迁移不在本期范围 |
| R2 | 摇杆/转盘等自定义交互在 Avalonia 重绘成本高 | 用 PointerPressed/Moved/Released + Canvas/Drawing，先还原核心行为 |
| R3 | ASR/录音是核心业务但需 Mock | 接口抽象 + Mock 数据流与真实服务一致（含状态机时序） |
| R4 | Flutter 布局坐标/尺寸语义与 Avalonia 不同 | 以功能与交互等价为准，不逐像素复刻 |
| R5 | 双序列化缺陷（fromJson 空壳、`==[]` 引用比较） | 迁移时用正确实现修复，不复制缺陷 |
| R6 | Android 物理按键/振动等平台能力 | 降级为可选增强，桌面端先行 |

## 8. 验收标准

1. `dotnet build` 无错误、无显著 warning（csproj 启用 TreatWarningsAsErrors=true 可选）
2. App 可独立运行，主窗口含四个顶级页面导航
3. 录音页：场/镜/次切换、文件编号自动生成（三种前缀模式）、OK/NG 评价、补录模式、备注编辑、模拟录音/ASR 全流程可走通并写入日志
4. 拍摄计划页：CSV 导入、场景/镜/备注增删改、标签编辑、重复检测
5. 场记查改页：按日期浏览、修改、JSON 导出
6. 设置页：项目名/录音机类型/自定义前缀/链接符配置
7. Android 目标可编译（复用既有环境验证一次）
8. GitHub 仓库 + CI（GitHub Actions：build + test）提交完成

## 9. 开发纪律

- 五轮独立 review Agent 迭代后方可编码
- git worktree 并行开发，每 Agent 独立 commit
- 重大决策追加到本文档 ADR
- 不复制 Flutter 缺陷；不实现无成熟库替代的通用能力

---
*（v0.1：待 S2 状态管理报告、S3 页面交互报告补全 2.4/2.5 后进入第一轮 review）*