# VoiSlate 开发导览地图（GUIDE.md）

> 用途：新开发者（含 AI agent）的仓库导航。回答「项目是什么、代码在哪、怎么构建运行、有什么坑、改哪里不踩雷」。
> 最后更新：2026-08-21 ｜ 对应提交：`a326284`（Android 目标合入）。本文件由主 Agent 维护。

---

## 1. 项目速览

VoiSlate 是**声音场记无纸化工具**（拍板场记）：记录每一条录音的场/镜/次、文件名编号、备注与 OK/NG 评价；支持拍摄计划（场→镜→标签/对象/备注）、按日期查改场记、JSON 导出/备份、补录（Wild）与收工（End）语义。

| 项 | 值 |
|---|---|
| 性质 | 从 Flutter 应用迁移重写的 Avalonia 桌面应用（**不运行原 Flutter 应用**，只做源码复刻，缺陷按 B1-B11 清单原样复刻并单测锁定） |
| 技术栈 | .NET 10（SDK 10.0.400）+ Avalonia 12.1.1 + CommunityToolkit.Mvvm 8.4.2 + LiteDB 5.0.21 + CsvHelper 33 + Serilog + Microsoft.Extensions.DependencyInjection |
| 目标平台 | 桌面 `net10.0`（✅ 主目标）+ Android `net10.0-android`（✅ 真机已验证，`a326284`） |
| ASR/录音/震动/Toast | 接口先行，Mock 实现（`MockAsrService`/`NoopHapticsService`/`NoopToastService` 等） |
| 测试 | 285/285 通过（xUnit，单测试项目 `tests/VoiSlate.Tests`，无 Moq 全手写替身） |
| 远程 | https://github.com/drunkenQCat/voislate-avalonia （main 分支） |

## 2. 目录地图

```
voislate-avalonia/
├── Directory.Build.props      # 共享构建配置（LangVersion latest / Nullable / ImplicitUsings）——禁改
├── VoiSlate.slnx              # 解决方案（slnx 新格式；CI 只构建这个，不含 Android 工程）
├── GUIDE.md                   # 本文件
├── docs/
│   ├── contracts.md           # 模块契约 v0.5（模型/服务/VM/控件签名、纪律）——开发的“宪法”
│   ├── migration-plan.md      # Flutter→Avalonia 迁移计划（业务规则 B1-B11、ADR 决策、缺陷复刻清单）
│   └── p2-integration-notes.md# Avalonia 12.1.1 三坑实录 + C 集成决策
├── scripts/p2-merge.sh        # P2 阶段 5 个 agent 分支并入 main 的驱动脚本（a→e→d→b→c）
├── .github/workflows/ci.yml   # CI：restore/build/test（ubuntu-latest；仅 slnx）
├── src/
│   ├── VoiSlate/              # 主工程（纯 net10.0，桌面 + Android 共用全部业务代码）
│   │   ├── Program.cs         # 入口：BuildAvaloniaApp().StartWithClassicDesktopLifetime
│   │   ├── App.axaml          # 应用级资源/样式 + VM→View DataTemplate 导航映射
│   │   ├── App.axaml.cs       # ★ 组合根：DI 注册表 + 启动时序 + Android 接线（信息密度最高，见 §4/§5）
│   │   ├── Models/            # 领域模型（SlateLogItem/SceneSchedule/枚举…）+ JsonConverters
│   │   ├── Services/          # 25 个文件：服务接口与实现（业务规则 B1-B11 全在 TakeFlowService）
│   │   ├── Infrastructure/    # LiteDbStore（开库/默认连接串/四集合）
│   │   ├── Data/              # SeedData（空库播种两份生产场表）
│   │   ├── ViewModels/        # 10 个 VM（其中 SlateLogPageViewModel 存在但未接入导航）
│   │   ├── Views/             # 9 个 View：4 页面 + 3 对话框/窗口 + Converters
│   │   ├── Controls/          # 16 个自研控件/逻辑（SlateWheel/SlideConfirmBar/DialFAB…）
│   │   └── Themes/            # VoiSlatePalette.axaml（色板）+ Controls.axaml（控件默认样式）
│   └── VoiSlate.Android/      # Android 壳工程（net10.0-android，仅入口，业务全在主工程）
│       ├── VoiSlate.Android.csproj   # ApplicationId=com.voislate.app、minSdk 23、NETSDK1150 处理
│       ├── MainActivity.cs           # AvaloniaMainActivity + @style/Theme.AppCompat.Light.NoActionBar
│       ├── AndroidApp.cs             # [Application] AvaloniaAndroidApplication<VoiSlate.App>
│       └── Properties/AndroidManifest.xml / Resources/mipmap/appicon.png
└── tests/VoiSlate.Tests/      # 唯一测试工程（Models/Services/ViewModels/Controls/TestDoubles）
```

## 3. 架构总览（改代码前必读）

**分层与纪律**（契约 v0.5 §1/§7）：

- 分层：`Models / Services / ViewModels / Views / Controls / Infrastructure`；依赖方向只允许上层依赖下层，**Services 不依赖 ViewModels**（`ITakeFlowService` 只依赖 `ISessionState` 接口，由 `RecordingSessionViewModel` 实现——消除逆向依赖）。
- **存储访问只经 Repository/Store 接口**（`ILogRepository`/`IScheduleStore`/`ISessionSettingsStore`/`IPickerHistoryStore`），View/VM 禁止直连 LiteDB。
- **唯一写入口纪律**：`ITakeFlowService` 是 `ILogRepository`/`IPickerHistoryStore`/文件号（`FileNumberingService` + 持久化）的**唯一写者**——日志编辑/删除/文件号编辑全部经它（`SaveEditAsync`/`DeleteItemAsync`/`SetFileNumberAsync`/`SetLinkerAsync`/`SetPrefixAsync`）。
- 事件约定：Service 事件统一 `event Action<T>? {Thing}Changed`（**不用 EventHandler**）。
- JSON：camelCase + `JsonStringEnumConverter(CamelCase)` 显式命名策略（`VoiSlateJson.Options`，导入导出共用）；导出格式与原 Flutter 应用完全一致（无日期字段、含 fake/wild 哨兵值）。
- VM 命令一律用 Model 枚举做参数（View 层类型如 `DialOption` 不得泄漏进 VM；DialFAB 的 `EnumValue`→TkStatus 转换由 RecordView code-behind 映射表完成）。
- 实现风格：**无消息总线**，全部 C# 事件 + PropertyChanged 联动；MVVM Toolkit 源生成 `partial On{X}Changed`（持久化与派生属性）；IDisposable 链（TakeFlowService 退订 NumberChanged、RecordViewModel.Deactivate、各 VM 退订事件）。
- `Directory.Build.props` **任何 Agent 禁改**；契约变更仅由主 Agent 更新 docs/contracts.md。

**核心业务流程**（详见 docs/migration-plan.md §2.3，B1-B11）：
- 记条（AdvanceTake）：读 picker_history 尾 → B1-B4 守卫（首按只写 history 不写日志 / 收工 End 不递增文件号、'OK' 拦截只生效一次 / 假拍野拍判定读 history 尾关键字**而非本次入参**——复刻原缺陷）→ 构造 SlateLogItem（fake tk=999 / wild tk=0 + 'wild track …'）→ 写日志 → 追加 history → 非 End 递增文件号 → 重置评价 → 落 RecordCount/Date → 震动。
- 撤回（RewindTake）：OK 尾只弹哨兵 + toast「原来还没收工呢……」；否则递减 → 弹尾条目 → 删末条 → 恢复备注。
- 三个纯逻辑状态机（全部可单测）：`TakeFlowService`（业务流程编排）、`WheelSelectionLogic`（滚轮选择，Next 非联动不推进/循环回绕/SnapToNearest/DisplayIndex 负侧地板回卷）、`SlideConfirmLogic`（滑条确认，slideLength=宽−球、阈值边缘触发、TryCommit 幂等——同文本重复滑动只提交一次）。

## 4. 关键文件：《App.axaml.cs》一户通

`src/VoiSlate/App.axaml.cs`（307 行）是本仓库信息密度最高的文件：
1. **启动时序**（注释 ADR-008）：LiteDbStore.Open → `SeedService.EnsureSeededAsync` → `RecordingSessionViewModel.Initialization` → `ITakeFlowService.InitializeAsync` → 显示 UI。
2. **DI 注册表**（`ConfigureServices`，摘要见 §5）。
3. **Android 分支**（`IActivityApplicationLifetime`，接口在核心 Avalonia.Controls 包）：桌面路径同步 seed；Android 路径把 seed/初始化放进后台 `Task.Run`（deferred init，规避进程启动 ANR 预算），主线程只挂 `MainViewFactory = () => CreateAndroidMainView(mainViewModel)`。
4. **`CreateAndroidMainView`**：与 `Views/MainWindow.axaml` **同构的代码化导航壳**（190px 左栏 `#F4F4F4` + 右侧 ContentControl↔CurrentPage；四枚 PathIcon 与桌面逐字一致；按钮背景经 `PageKeyToBrushConverter` 高亮）。**维护约定：改 MainWindow.axaml 的壳结构时必须同步本方法**。

## 5. DI 注册表摘要（App.axaml.cs ConfigureServices）

| 注册 | 说明 |
|---|---|
| `LiteDbStore`（单例） | `LiteDbStore.DefaultConnectionString()`；数据文件 `%AppData%/VoiSlate/voislate.db` |
| `ITimeProvider → SystemTimeProvider` | 可注入固定时间保证测试确定性 |
| `IFileNamingService → FileNumberingService` | 注入 ITimeProvider |
| `RecordingSessionViewModel`（单例） | 同时注册为 `ISessionState`；实现 `Initialization` Task |
| `ISessionSettingsStore / ILogRepository / IPickerHistoryStore / IScheduleBook` | LiteDB 实现 |
| `ISeedService → SeedService`、`IAsrService → MockAsrService` | |
| `IToastService → NoopToastService`、`IHapticsService → NoopHapticsService` | ⚠️ `Controls/ToastService.cs` 有真实现但**未注册** |
| `ITakeFlowService → TakeFlowService`（单例，唯一持用 FileNumberingService） | 注入 8 项 + 3 个 Provider 委托（SceneLabel/ShotLabel/ObjectsOf，**硬编码取第 0 场第 0 镜** `book.X(0,0)`） |
| `IHardwareKeyService → NoopHardwareKeyService`、`IExportService → ExportService`、`IScheduleStore → NoopScheduleStore`（⚠️ 计划页当前不落库）、`ICsvScheduleParser → CsvScheduleParserService` | |
| `Func<RecordViewModel>` 工厂 | Record 页 Scoped：进入创建、退出释放（契约 C-6） |
| `SlateLogViewModel / ScheduleViewModel / SettingsViewModel / MainViewModel` | 单例；MainViewModel 吃工厂 + 3 个页面 VM |

**存在但未注册（要接线时从这里开始）**：`IDayRolloverService`、`IBackupService`、`IImportService`、`IRecordingService`、`IScheduleService`、`SlateLogPageViewModel`（树形分组版场记，实现完整但未参与导航——导航用的是扁平 `SlateLogViewModel`）、`LiteDbScheduleStore`（有真实序实现，未注册）、`ToastService`（真 Toast 实现，未注册）。

## 6. 构建与运行命令

```bash
# 前提：.NET SDK 10.0.400；Android 需 workload：dotnet workload install android
# 桌面构建 / 测试（CI 同此）
dotnet build VoiSlate.slnx -c Release        # 期望 0 warning 0 error
dotnet test tests/VoiSlate.Tests -c Release  # 期望 285/285

# 桌面运行
dotnet run --project src/VoiSlate -c Release

# Android 发布 APK（产出在 src/VoiSlate.Android/bin/Release/net10.0-android/<rid>/）
dotnet publish src/VoiSlate.Android -c Release -f net10.0-android \
  -p:AndroidPackageFormat=apk -p:RuntimeIdentifier=android-arm64   # 真机（arm64-v8a）
# 模拟器 x86_64 用 -p:RuntimeIdentifier=android-x64
```

**Android 工程要点**（src/VoiSlate.Android/VoiSlate.Android.csproj）：
- `minSdk 23`（androidx.lifecycle 要求，低于 23 报 AMM0000）；`targetSdk 36`；`ApplicationId com.voislate.app`
- 主工程是 WinExe，Android 侧经 ProjectReference + `ValidateExecutableReferencesMatchSelfContained=false` + `GlobalPropertiesToRemove` 引用为纯程序集（NETSDK1150 处理）
- `AndroidPackageFormat` 不在 csproj 固定，发布时传参
- 启动 Activity：`com.voislate.app/crc{...}.MainActivity`（crc64 前缀 = Mono.Android 命名空间编码）

**真机部署验证流程**（无线 adb，实测 RMX5060 / Android 16 / arm64）：
```bash
adb pair IP:PAIR_PORT            # 无线调试→使用配对码配对（一次性）
adb connect IP:PORT              # 连接（如 192.168.39.24:39713）
adb install -r <arm64-apk>
adb shell am start -n com.voislate.app/crc64478679047a91a2fc.MainActivity
adb exec-out screencap -p > shot.png
adb logcat -d | grep -aE "FATAL|AndroidRuntime"   # 崩溃必查
```

## 7. 已知的坑（每一条都是真金白银踩出来的）

1. **Avalonia 12 三坑**（详见 docs/p2-integration-notes.md）：
   - `<ControlTemplate TargetType>` 是**类型引用不是 Selector**：用 `controls:SlideConfirmBar` 冒号，不是 `|` 管道（AVLN2000）
   - **`ResourceDictionary.Source` 已移除**：用 `MergeResourceInclude`（编译期合并；AOT 下不要用运行时 ResourceInclude）
   - **隐式 DataTemplate 必须放 `Application.DataTemplates`**，放 Resources 报 AVLN3000
2. **Android 必须 AppCompat 主题**：`AvaloniaMainActivity` 继承 AppCompatActivity，Activity 主题必须 `@style/Theme.AppCompat.Light.NoActionBar`（平台主题 → 启动即 `IllegalStateException` 闪退）。这是真机验证才暴露的 bug（模拟器死于更早的 ANR 阶段）。
3. **Android 不能 `new Window()`**：`IWindowingPlatform` 是 `WindowingPlatformStub`，CreateWindow 抛异常；`MainViewFactory` 必须返回普通 Control（`CreateAndroidMainView` 就是为此存在的）。
4. **Android 进程启动 ANR 预算**：TCG 软件模拟（无 KVM）下任何 .NET Android 应用都超线被杀（已用冒烟应用证实是环境性问题，与代码无关）；真机无此问题。Android 分支的 deferred init 是为该场景保留的保险。
5. **NETSDK1150**：WinExe 主工程被 Android 工程引用 → 见 §6 csproj 处理。
6. **Avalonia.Diagnostics 版本滞后**：11.x 与 Avalonia 12.1.1 不配套，暂未引入（csproj 注释说明）。
7. **已知业务缺口（如实标注，勿当成品）**：
   - 桌面 UI 点击"导出 JSON/导出全部"会抛 `ArgumentException`（`ExportService.SaveToFileAsync("", …)` 目录为空串，落盘补充从未实现；单测用 Spy 未覆盖 UI 路径）
   - `scene_schedules` 集合按 Key **字符串**排序（"10A" < "2A"，LiteDbScheduleBook 注释自认缺陷；LiteDbScheduleStore 有序实现存在但未注册）
   - `ITakeFlowService` 的场/镜标签 Provider 硬编码第 0 场第 0 镜（`book.X(0,0)`）
8. **存储线程安全**：LiteDB 线程安全注意点（ADR-001 风险 CR3）。

## 8. 页面 / 窗口 / 转换器 / 主题速查

**导航壳**（`Views/MainWindow.axaml` + App.axaml.cs 的 Android 代码镜像）：Grid `190,*`；左栏 `#F4F4F4` Border + DockPanel（标题 VoiSlate `#0067A0` + 4 个导航按钮：记录/计划/场记/设置，PathIcon 18×18）；右侧 `ContentControl ↔ CurrentPage`。**页面切换 = MainViewModel.NavigateCommand 换 CurrentPage → App.axaml DataTemplate 渲染**；Record 页每次新建（Scoped 工厂），其余页复用单例。**无 TabControl、无键盘快捷键、无顶部栏**（仅 IHardwareKeyService 音量键路径，桌面 Noop）。

| 页面 | ViewModel | 关键交互 |
|---|---|---|
| RecordView | RecordViewModel（Scoped） | Loaded/Unloaded → Activate/Deactivate；2×DialFAB（SetOkTake/SetOkShot）；FileCounter.EditRequested → SmallEditDialog → Edit{FileNumber,Linker,Prefix}Async；速览 → RefreshQuickNotesAsync + QuickViewLogWindow；AdvanceTake(Normal/Fake/End)/RewindTake/ToggleLink/ToggleAsr；双备注 TwoWay + SlideConfirmBar |
| ScheduleView | ScheduleViewModel | 导入 CSV（StorageProvider→ImportCsvAsync）、AddScene/AddShot、SmallEditDialog 编镜备注→ApplyShotEdit、DeleteItem/MoveItem、左右 ListBox 联动 |
| SlateLogView | SlateLogViewModel | 日期切换（Dates/SelectedDate）+ 导出 JSON；当日卡片（okTk/okSht 色点）编辑→vm.RequestEdit→LogEditorWindow、删除经 DeleteCommand |
| SettingsView | SettingsViewModel | 工程名/记录设置（IsLinked、RecordLinker、PrefixModes+CustomPrefix）/TodayCount/数据操作（ExportAll/ClearToday 占位，正式实现需 DialogService 确认） |

**窗口/对话框**：`LogEditorWindow`（模态编辑单条场记，保存/删除经 ITakeFlowService 唯一写入口；SlateLogView 打开）、`QuickViewLogWindow`（只读速览 540×620，sublist(40) 已知行为；RecordView 打开）、`SmallEditDialog`（通用单值编辑，有 choices 显 ComboBox；两个 View 共用）。

**转换器**（`Views/Converters.cs`）：`TkStatusToBrushConverter`（Ok绿/Bad红/灰）、`ShtStatusToBrushConverter`（Ok绿/Nice金/灰）、`PageKeyToBrushConverter`（导航高亮，值==参数→半透明 bahamaBlue）；`Palette` 静态类为内联色值占位（待切主题资源键）。

**主题**（`Themes/`）：
- `VoiSlatePalette.axaml` 契约必选键（**键名不得改**，PaletteResourceKeysTests 校验前缀 `VoiSlate.`）：`VoiSlate.Bg` / `VoiSlate.Primary`(#0067A0 bahamaBlue) / `VoiSlate.OkGreen` / `VoiSlate.BadRed` / `VoiSlate.NiceGold` / `VoiSlate.TextHint`；补充键（Wheel/Dial/Card 取色自原版）。
- `Controls.axaml`：SlideConfirmBar/DialFAB/FileCounter/TagChips/ToastHost/LoadingOverlay 的默认样式模板（PART_* 命名，代码按名查找）。

**CI / 脚本**：`.github/workflows/ci.yml` 单 job（restore/build/test，仅 slnx，**不含 Android**）；`scripts/p2-merge.sh` P2 历史脚本（check + merge-a/e/d/b/c + verify）。

## 9. 模块地图（Services / ViewModels / Controls / 数据层）

### 9.1 Services（25 个文件；接口与实现多在同文件）

| 接口 | 实现 | 职责 |
|---|---|---|
| `ITakeFlowService` | `TakeFlowService`（283 行，核心） | 记条/撤回/收工/假拍野拍全流程 + B1-B11 + 文件号唯一写者 |
| `ILogRepository` | `LiteDbLogRepository` | 按日期读写日志（`logs` 集合，保序） |
| `ISessionSettingsStore` | `LiteDbSessionSettingsStore` | 13 键会话设置 + 扩展键（`settings` 集合） |
| `IPickerHistoryStore` | `LiteDbPickerHistoryStore` | 场镜历史（`picker_history` 集合） |
| `IScheduleBook` / `IScheduleStore` | `LiteDbScheduleBook` / `LiteDbScheduleStore` | 计划树读写（`scene_schedules` 集合；Book 字符串排序缺陷见 §7） |
| `ISeedService` | `SeedService` | 空库播种两份生产场表 |
| `IAsrService` | `MockAsrService` | Mock：Start 1.2s 后 ResultReceived |
| `IRecordingService` | （Mock 曲线电平） | 未注册 |
| `IDayRolloverService` | （跨天补偿 + PeriodicTimer） | 未注册 |
| `IBackupService` | （3 分钟全量 JSON 备份） | 未注册 |
| `IImportService` / `IScheduleService` | （导入/计划服务） | 未注册 |
| `IExportService` | `ExportService` | camelCase JSON 序列化（落盘 dir 为 "" → 见 §7） |
| `ICsvScheduleParser` | `CsvScheduleParserService` | CsvHelper 7 列解析 |
| `IFileNamingService` | `FileNumberingService` | 三前缀模式（yymmdd / yyYmMd / 自定义）+ 编号状态机 |
| `ITimeProvider` | `SystemTimeProvider` | Today / TodayStamp / SoundDevicesStamp |
| `IHardwareKeyService` | `NoopHardwareKeyService` | 音量键（Android 增强待做） |
| `IToastService` / `IHapticsService` | `NoopToastService` / `NoopHapticsService` | Toast 真实现存在未注册 |
| `MicObjectsExtractor` | 静态 | shtNote 麦克风对象协议 `<obj/>` 解析 |

### 9.2 ViewModels（10 个；层级见 §2 目录地图注释）

- `MainViewModel`：导航（Pages/CurrentPage/CurrentPageKey/NavigateCommand），页键常量 `record/schedule/slatelog/settings`。
- `RecordingSessionViewModel`：**实现 ISessionState**（Scene/Shot/TakeIndex + TakeCount=200 + SessionChanged），13 个 ObservableProperty 即时持久化到 ISessionSettingsStore；`Initialization` Task；Select{Scene,Shot,Take}/SetLink/SetOk{Take,Shot} 等。
- `RecordViewModel`（Scoped）：三列（SceneCol/ShotCol/TakeCol `SlateColumnViewModel`）+ 文件号显示 + Activate/Deactivate 钩子 + 音量键。
- `ScheduleViewModel` / `SlateLogViewModel`（扁平，导航中）/ `SlateLogPageViewModel`（树形分组，未接线）/ `SettingsViewModel` / `LogEditorViewModel` / `PlaceholderViewModel`（冒烟）。

### 9.3 Controls（16 个文件；控件代码 + 纯逻辑分离，逻辑可单测）

| 控件/逻辑 | 一句话职责 | 使用者 |
|---|---|---|
| `SlateWheel` + `WheelSelectionLogic` | 三列滚轮自绘（拖拽连续更新/吸附、循环、ScrollTo 动画） | RecordView ×3 |
| `SlideConfirmBar` + `SlideConfirmLogic` | 水平滑动确认条（红→绿、双备注 TwoWay、幂等提交） | RecordView |
| `DialFAB` + `DialOption` + `DialStatusPalette` | 评价弹钮（声音可/弃、画面保/过，状态色回显） | RecordView ×2 |
| `FileCounter` + `FileNumberFormat` | 文件号三卡片（前缀/链接符/编号，D3 补零） | RecordView |
| `TagChips` / `ToastHost` + `ToastService` / `LoadingOverlay` | 标签流式排布 / 全局 Toast（未注册）/ 加载遮罩 | 各处 / 全局 / 全局 |
| `EditRequestedSection` / `SlideConfirmState` / `VoiSlatePalette` | 枚举与调色板类型 | — |

### 9.4 数据层（Infrastructure/LiteDbStore.cs，LiteDB 四集合）

| 集合 | 内容 |
|---|---|
| `logs` | LogDoc{ Date, Key=上一拍文件名, SlateLogItem 字段+Id } |
| `picker_history` | BsonDocument{ e: string[] }（场,镜,tk关键字,...对象） |
| `settings` | { _id=key, v=value }；13 会话键 + `project` 等扩展键 |
| `scene_schedules` | SceneScheduleDoc{ Key/Items/Info } |

`SeedData`：空库播种两份生产场表（dummy_data 语义）——1A「万星园」objects=[缪尔赛斯,塞雷娅,克里斯滕]、2A「洛肯实验室」objects=[Dr,凯尔希,迷迭香]，各 3 镜。`SlateLogItem` 有 BsonId `Id` 与 `[JsonIgnore]` 计算属性（FileName/Type）；导出经 `VoiSlateJson.Options`。

**已实现但未接线（别误以为全局生效）**：`ToastHost`/`ToastService`（Toast 宿主与实现）、`TagChips`、`LoadingOverlay`——三个控件代码与样式齐全，但无任何 View 实例化、Toast 服务 DI 未注册；`IRecordingService`/`IDayRolloverService`/`IBackupService`/`IImportService`/`IScheduleService` 接口与实现齐全但均未注册，`BackupService` 等 IDisposable 周期任务在组合根里实际不会执行。

## 10. 测试矩阵（tests/VoiSlate.Tests，xUnit，无 Moq 全手写 Fake/Stub/Spy）

约 228 `[Fact]` + 11 `[Theory]`（≈51 行 InlineData），合计 **285 用例全绿**。

| 组 | 文件（节选） | 覆盖 |
|---|---|---|
| Models | ModelTests / DataListVerbatim / FileNumberingServiceVerbatim / RecorderType / SlateLogItemSentinel | DataList 查重、文件号三前缀模式、枚举显式值与 JSON 短枚举名、Fake/Wild 哨兵逐字往返 |
| Services | TakeFlowServiceTests + 13 个模块测试 | B1-B11 业务规则复刻锁定（首按不写/收工守卫/假拍延迟一拍）、备份/导出/CSV 解析/跨日回滚/MicObjects |
| ViewModels | 9 个 VM 测试组 | 导航 Scoped 语义、Activate 水合 13 键、音量键行为、列联动、跨日重置、导出委托 |
| Controls | ControlSurface / WheelSelectionLogic / SlideConfirmLogic / DialStatusPalette / FileNumberFormat / PaletteResourceKeys | 滚轮/滑条纯逻辑状态机、控件契约默认值、主题资源键存在性与前缀校验 |
| RepositoryTests | LiteDB 仓储 + Seed + MockAsr | 保序往返、类型化设置、播种仅一次 |

**TestDoubles**：`TestDoubles/TestDoubles.cs`（FakeTimeProvider 固定 2026-08-20、FakeLogRepository、FakePickerHistoryStore、FakeSessionSettingsStore、FakeSessionState）、`TestDoubles/ScheduleDoubles.cs`（MutableFakeTimeProvider 跨天、FakeScheduleStore）、`ViewModels/ViewModelTestDoubles.cs`（TestHardwareKeyService 手动 Raise、StubScheduleStore/CsvParser/Book、SpyExportService、ScheduleFactory）。

## 11. 文档索引（新增开发前先读）

| 文档 | 内容 | 何时读 |
|---|---|---|
| `docs/contracts.md` | 模块契约 v0.5：Models/Services/VM/Controls 签名、编号规则、纪律 | 改任何业务代码前 |
| `docs/migration-plan.md` | 迁移计划 + B1-B11 + ADR 决策 + 缺陷复刻清单 | 了解业务语义 |
| `docs/p2-integration-notes.md` | Avalonia 12.1.1 三坑实录 + C 集成决策（View 适配 VM） | 写 XAML 前必读 |

## 12. 开发检查清单

- [ ] 改业务前读 contracts.md 对应模块契约；改签名须同步契约版本
- [ ] 存储读写走 Repository/Store 接口；日志/文件号变更只经 ITakeFlowService
- [ ] 新增 XAML：ControlTemplate 用 `:` 类型引用；字典合并用 MergeResourceInclude；模板进 DataTemplates
- [ ] Android Manifest 主题保持 AppCompat 系列
- [ ] 改了 MainWindow.axaml 壳 → 同步 `App.axaml.cs#CreateAndroidMainView`
- [ ] 新增源文件放 src/VoiSlate 对应子目录（SDK glob 免改 csproj）；测试放 tests/VoiSlate.Tests 对应子目录，**只新增文件不改既有文件**
- [ ] 桌面回归：`dotnet build` 0w/0e + `dotnet test` 285/285
- [ ] Android 改动 → 发 arm64 APK → 真机安装启动验证（崩溃查 logcat FATAL）

## 13. 当前状态里程碑

- ✅ Gate 0：五轮 review + contracts v0.5
- ✅ Gate 1：P0.5 垂直切片（33/33 测试、启动+种子入库）
- ✅ P2：A/E/D/B/C 五 Agent 合入（Avalonia 12 三坑全修复）；build 0w/0e、**285/285**
- ✅ Android：工具链 + net10.0-android 工程 + arm64/x64 APK + **真机跑通**（`a326284`，RMX5060/Android 16）
- 🔲 待办（按需，现状缺口见 §7.7）：导出落盘实现（`SaveToFileAsync` 目录参数）、计划页落库（注册 LiteDbScheduleStore）、Toast 真实现注册、SlateLogPageViewModel 接线或删除、Android 真机 UI 细测