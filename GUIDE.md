# VoiSlate 开发导览地图（GUIDE.md）

> 用途：新开发者（含 AI agent）的仓库导航。回答「项目是什么、代码在哪、怎么构建运行、有什么坑、改哪里不踩雷」。
> 最后更新：2026-08-21 ｜ 对应提交：`a326284`（Android 目标合入）。本文件由主 Agent 维护。

---

## Windows 工作台更新

Windows 默认入口已按 `voislate_style` 更新为原生工作台。运行、规则变化、写入边界与验证记录见 [windows-workspace.md](docs/windows-workspace.md)。桌面采用“立即保存当前条、完整撤回、结束保存、各镜草稿、连续分组时间轴”；下文关于旧 UI、首按不写日志与缺陷复刻的描述属于旧迁移路径。此次按用户要求仅验证 Windows。新工作台已接入百炼实时语音与 Token 设置（DPAPI 加密），并补齐计划排序 / 插入 / 单项删除、假条、命名规则、轨道快照、多步撤回及 JSON 备份恢复；下文 Mock 说明仅适用于旧界面。

## 1. 项目速览

VoiSlate 是**声音场记无纸化工具**（拍板场记）：记录每一条录音的场/镜/次、文件名编号、备注与 OK/NG 评价；支持拍摄计划（场→镜→标签/对象/备注）、按日期查改场记、JSON 导出/备份、补录（Wild）与收工（End）语义。

| 项 | 值 |
|---|---|
| 性质 | 从 Flutter 应用迁移重写的 Avalonia 桌面应用（**不运行原 Flutter 应用**，只做源码复刻，缺陷按 B1-B11 清单原样复刻并单测锁定） |
| 技术栈 | .NET 10（SDK 10.0.400）+ Avalonia 12.1.1 + CommunityToolkit.Mvvm 8.4.2 + LiteDB 5.0.21 + CsvHelper 33 + Serilog + Microsoft.Extensions.DependencyInjection |
| 目标平台 | 桌面 `net10.0`（✅ 主目标）+ Android `net10.0-android`（✅ 真机已验证，`a326284`） |
| ASR/录音/震动/Toast | 接口先行，Mock 实现（`MockAsrService`/`NoopHapticsService`/`NoopToastService` 等） |
| 测试 | 274 通过 / 6 跳过 / 280（xUnit，单测试项目 `tests/VoiSlate.Tests`，无 Moq 全手写替身；跳过的 6 条是 `WindowsOnlyFactAttribute`） |
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
│   │   ├── ViewModels/        # 仅剩 RecordingSessionViewModel（= ISessionState，现役在用）；P7 页面 VM 已于 2026-10-26 删除
│   │   ├── Desktop/           # ★ 唯一界面与数据面：DesktopWorkspaceView(UserControl) + 5 分部类 + 薄壳 Window + WorkspaceDialog + Legacy
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
3. **桌面 / Android 共用同一套工作台控件**（2026-10 迁移）：`Desktop/DesktopWorkspaceView : UserControl` 是唯一界面实现，桌面由 33 行的 `DesktopWorkspaceWindow`（薄壳 Window）承载，Android 由
   ```csharp
   var view = new Desktop.DesktopWorkspaceView(workspace, speechSettings, captureFactory: null);
   androidLifetime.MainViewFactory = () => view;                 // Android 不能 new Window
   _ = Task.Run(async () => { await workspace.InitializeAsync(); Dispatcher.UIThread.Post(view.Refresh); }); // deferred init
   ```
   承载。**不再有第二份界面**——改界面只改 `DesktopWorkspaceView`，两端自动一致。
4. **弹层双后端**（`Desktop/WorkspaceDialog.cs`）：`Dialog(...)` 在 Window 宿主下走 `ShowDialog`（桌面保留真实子窗口，13 条界面用例依赖它），无窗口宿主（Android）下走 `ForOverlay(_overlayLayer, ...)` 的遮罩层。`_overlayLayer` 挂在 `Render()` 之外，重渲染不丢弹层。
5. **`IStorageProvider` 来自 `TopLevel.GetTopLevel(this)?.StorageProvider`**：桌面是系统文件选择器，Android 是 `Avalonia.Android.Platform.Storage.AndroidStorageProvider`（SAF）。保存/读取一律用 `OpenReadAsync`/`OpenWriteAsync` 流，**不要用 `TryGetLocalPath()`**（SAF 文档 URI 返回 null，仅备份目录选择这一处例外且已给出提示）。

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
| `IHardwareKeyService → NoopHardwareKeyService`、`IExportService → ExportService`、`IScheduleStore → LiteDbScheduleStore`、`ICsvScheduleParser → CsvScheduleParserService` | ⚠️ NoopHardwareKeyService 只是占位：真实音量键走 `HardwareKeys.Current`（Android 在 `MainActivity.OnCreate` 注册） |
| ~~`Func<RecordViewModel>` / 4 个页面 VM / `MainViewModel`~~ | **已删除**（2026-10-26）：界面与数据面统一由 `DesktopWorkspaceService` 承担 |

**存在但未注册（要接线时从这里开始）**：`IDayRolloverService`、`IBackupService`、`IImportService`、`IRecordingService`、`IScheduleService`（已实现「至少 1 场 1 镜」守卫 / 任意索引 Move / 20 步整表 undo，可当参考实现）、`ToastService`（真 Toast 实现）。

## 6. 构建与运行命令

```bash
# 前提：.NET SDK 10.0.400；Android 需 workload：dotnet workload install android
# 桌面构建 / 测试（CI 同此）
dotnet build VoiSlate.slnx -c Release        # 期望 0 warning 0 error
dotnet test tests/VoiSlate.Tests -c Release  # 期望 274 passed / 6 skipped（共 280）

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
3. **Android 不能 `new Window()`**：`IWindowingPlatform` 是 `WindowingPlatformStub`，CreateWindow 抛异常；`MainViewFactory` 必须返回普通 Control（为此界面全部落在 `DesktopWorkspaceView : UserControl` 上，弹窗走 `WorkspaceDialog.ForOverlay`）。
4. **Android 进程启动 ANR 预算**：TCG 软件模拟（无 KVM）下任何 .NET Android 应用都超线被杀（已用冒烟应用证实是环境性问题，与代码无关）；真机无此问题。Android 分支的 deferred init 是为该场景保留的保险。
5. **NETSDK1150**：WinExe 主工程被 Android 工程引用 → 见 §6 csproj 处理。
6. **Avalonia.Diagnostics 版本滞后**：11.x 与 Avalonia 12.1.1 不配套，暂未引入（csproj 注释说明）。
7. **已收口的业务缺口（2026-10-26 更新，勿再按旧文档排查）**：
   - 导出：工作台的「导出 JSON」走 `DesktopWorkspaceBackup.ExportFlutterJson()`，选定日期时走 `ExportDayJson(date)`（Flutter 兼容字段，camelCase、非 ASCII 转义）；老 `ExportService.SaveToFileAsync("")` 那条空目录路径已不参与 UI。
   - `LiteDbScheduleBook` / `IScheduleBook` / `ITakeFlowService` / `TakeFlowService` **已删除**（`docs/p7-vm-coverage.md` §11），计划读写只剩 `LiteDbScheduleStore`，场记写入只剩 `DesktopWorkspaceService`；`SceneScheduleDoc` 已独立成 `Services/SceneScheduleDoc.cs`。
   - 仍存疑的只有 `IScheduleStore` 的读取顺序（旧实现按 Key 字符串排，"10A" < "2A"），改计划排序前先写用例钉住期望。
8. **存储线程安全**：LiteDB 线程安全注意点（ADR-001 风险 CR3）。

## 8. 页面 / 窗口 / 转换器 / 主题速查

**工作台外壳**（`Desktop/DesktopWorkspaceView.cs#Render`）：`_root` 内一层 `frame = Grid`；`_compact = Bounds.Width < 820`。
- 宽屏：左栏 184px（`BuildRail`：标题/副标题 + 记录/计划/场记/⚙工程与偏好 + 工程名/进度），右列 `_host.Content` 换页。
- 窄屏（手机/小窗）：左栏列宽归零并隐藏，**没有底部 Tab**（用户明确不要多选 tab，2026-10-26 移除 `BuildBottomNav`）。三页改成竖向整页翻页：`PageOrder = { "plan", "record", "log" }`，下滑＝上一页、上滑＝下一页，右侧 `BuildPagerDots()` 三颗指示点（`AutomationId = pager-dots`，`IsHitTestVisible = false`）。
  - 手势在 `AttachPager()`：三个 `PointerPressed/Moved/Released` 挂在**视图自身**（`RoutingStrategies.Tunnel` + `handledEventsToo: true`），因为页内 `ScrollViewer` 会先吃掉指针；落点在 `_joyArea` 内或 `_overlayLayer` 有子元素（弹窗开着）时直接跳过。
  - 边界优先：`AtScrollEdge(dy)` 只在当前页滚动器已在顶/底（或内容不足一屏）时才接管，否则让 `ScrollViewer` 正常滚动（iOS 分页手感）。
  - 跟手 0.5×（`_pagerShift = dy * 0.5`），松手按手指行程判阈值 `PagerCommit = 70`（`PageForSwipe(shift * 2)`），过阈值 `Navigate(target)` 后新页从 `-shift` 处滑入，未过阈值 `AnimateHost(shift)` 回弹（7 帧 16ms 三次缓出）。
  - 手机端**设置入口只剩记录页页头的 ⚙**（`AutomationId = open-settings`），计划/时间轴页用页头「返回拍摄 ×」（`nav-back`）回记录页。
- 测试：`VoiSlate.csproj` 加 `<InternalsVisibleTo Include="VoiSlate.Tests" />`，`WorkspacePhoneLayoutTests` 用 `Avalonia.Headless` 的 `MouseDown/MouseMove/MouseUp` 做真拖拽，断言 `CurrentPage` 与「不残留 `navb-*` 按钮」。
- 页面切换 = `Navigate(key)`（把 key 归一为小写后置 `_page` 再 `Render()`）；键盘 `HandleShortcut`（Ctrl+1/2/3、Ctrl+Shift+Z、Ctrl+Enter、Esc）由薄壳 Window 以 Tunnel 方式转交，Android 无键盘路径。
- 窄屏留白统一走 `PagePad(horizontal, vertical)`（compact 时夹到 14/12）。

**P7 遗留缺口收口（2026-10-26，`docs/p7-vm-coverage.md` §8）**——删 P7 ViewModel 前补齐的现场能力，改这几处前先看该文档的回归用例表：
- 音量键：`Services/HardwareKeys.cs` 的**静态事件** `HardwareKeys.Pressed`（`Raise/HasHandlers`）← `src/VoiSlate.Android/AndroidHardwareKeyService.cs` 映射 `Keycode` 后 `Raise`，由 `MainActivity.DispatchKeyEvent` 消费（**不是 `OnKeyDown`**：Avalonia 的 Android 视图会先吃按键）；上键记条、下键撤回，仅记录页且 `_busy=false`。视图侧订阅在 `DesktopWorkspaceView` ctor，`DetachedFromVisualTree` 解绑。
- 旧会话迁移：`Desktop/DesktopWorkspaceLegacy.cs` 的 `AdoptLegacySessionAsync()` 把老 `settings` 集合的 `project` 键 + `SessionKeys` 13 键一次性并入 `WorkspaceSession`；命名非法时保留默认而不阻塞启动。
- 按日导出：`DesktopWorkspaceBackup.ExportDayJson(date)` / `DayCount(date)`；时间轴表头按钮在选定日期时变「导出当日」。
- 计划守卫与编号：`DesktopWorkspaceEditing.DeletePlanItemAsync` 保留「至少 1 场 1 镜」且不再 `_undo.Clear()`；`UndoAsync` 会把已删除的场地重定位到存活条目；`NextPlanKey(scene, isScene)` 给新建预填下一个编号；`UsedFileNumbers(prefix, linker)` 在场记编辑框里提示已占用序号。

> **2026-10-25 迁移**：下表描述的是**已删除**的 P7 手机壳（`Views/{MainWindow,RecordView,ScheduleView,SlateLogView,SettingsView,LogEditorWindow,QuickViewLogWindow,SmallEditDialog}.axaml`，16 个文件）。桌面与 Android 现统一走 `Desktop/DesktopWorkspaceView`（记录/计划/时间轴 + 工程与偏好四项），下表仅作历史对照与交互行为参考——这些交互行为在 `DesktopWorkspace*.cs` 里以代码构建的方式一一对应。

| 页面（已删除） | 原 ViewModel | 关键交互（行为仍在工作台里） |
|---|---|---|
| RecordView | RecordViewModel（Scoped） | Loaded/Unloaded → Activate/Deactivate；2×DialFAB（SetOkTake/SetOkShot）；FileCounter.EditRequested → SmallEditDialog → Edit{FileNumber,Linker,Prefix}Async；速览 → RefreshQuickNotesAsync + QuickViewLogWindow；AdvanceTake(Normal/Fake/End)/RewindTake/ToggleLink/ToggleAsr；双备注 TwoWay + SlideConfirmBar |
| ScheduleView | ScheduleViewModel | 导入 CSV（StorageProvider→ImportCsvAsync）、AddScene/AddShot、SmallEditDialog 编镜备注→ApplyShotEdit、DeleteItem/MoveItem、左右 ListBox 联动 |
| SlateLogView | SlateLogViewModel | 日期切换（Dates/SelectedDate）+ 导出 JSON；当日卡片（okTk/okSht 色点）编辑→vm.RequestEdit→LogEditorWindow、删除经 DeleteCommand |
| SettingsView | SettingsViewModel | 工程名/记录设置（IsLinked、RecordLinker、PrefixModes+CustomPrefix）/TodayCount/数据操作（ExportAll/ClearToday 占位，正式实现需 DialogService 确认） |

**窗口/对话框**：`LogEditorWindow`（模态编辑单条场记，保存/删除经 ITakeFlowService 唯一写入口；SlateLogView 打开）、`QuickViewLogWindow`（只读速览 540×620，sublist(40) 已知行为；RecordView 打开）、`SmallEditDialog`（通用单值编辑，有 choices 显 ComboBox；两个 View 共用）。

**转换器**：`Views/Converters.cs` 已随 P7 页面一并删除（2026-10-26）；工作台的配色/显隐都在 `Desktop/DesktopWorkspaceView.cs` 里直接用代码决定，不再经过 XAML 转换器。

**主题**（`Themes/`）：
- `VoiSlatePalette.axaml` 契约必选键（**键名不得改**，PaletteResourceKeysTests 校验前缀 `VoiSlate.`）：`VoiSlate.Bg` / `VoiSlate.Primary`(#0067A0 bahamaBlue) / `VoiSlate.OkGreen` / `VoiSlate.BadRed` / `VoiSlate.NiceGold` / `VoiSlate.TextHint`；补充键（Wheel/Dial/Card 取色自原版）。
- `Controls.axaml`：SlideConfirmBar/DialFAB/FileCounter/TagChips/ToastHost/LoadingOverlay 的默认样式模板（PART_* 命名，代码按名查找）。

**CI / 脚本**：`.github/workflows/ci.yml` 单 job（restore/build/test，仅 slnx，**不含 Android**）；`scripts/p2-merge.sh` P2 历史脚本（check + merge-a/e/d/b/c + verify）。

#### 音量键（Android）两个真机级陷阱

1. **只重写 `OnKeyDown` 收不到音量键**——Avalonia 的 Android 视图会先消费按键。必须在 `MainActivity.DispatchKeyEvent` 里拦：`if (e is not null && e.Action == KeyEventActions.Down && HardwareKeysService.Handle(e.KeyCode)) return true;`（返回 true 才不触发系统音量条）。
2. **不能用「实例注册表 + `Current` 属性」**——Android 上 Avalonia 的 `Application` 初始化早于 `MainActivity.OnCreate`，视图构造时读 `Current` 必然是 null，真机表现是 `handlers=0`（键到了 `DispatchKeyEvent` 却没人响应，界面毫无反应）。现在 `VoiSlate.Services.HardwareKeys` 是**静态事件**（`Pressed` / `Raise` / `HasHandlers`），与平台实现何时注册无关；`AndroidHardwareKeyService` 只负责把 `Keycode.VolumeUp/Down` 映射后 `HardwareKeys.Raise(...)`。

#### 触觉反馈（Android）

原版 Flutter 用 `vibration` 包主动震动（`lib/pages/record_page.dart:296-299,347-348`），语义只有三种，现已 1:1 对齐：

| 交互 | 幅度 / 时长 | 入口 |
|---|---|---|
| 记一条（含音量上键、口袋锁长按、`Ctrl+Enter`） | 128 / 150ms | `Haptics.TakeSaved(fake: false)` |
| 记假条（`记录假条 F`） | 240 / 900ms | `Haptics.TakeSaved(fake: true)` |
| 撤回（撤回按钮、音量下键、`Ctrl+Shift+Z`） | 240 / 900ms | `Haptics.Undone()` |

- **`saveTake` 成功之后才震**：`DesktopWorkspaceView.SaveTake(...)` / `UndoTake()` 把 `Haptics.*` 放在 `await _service.SaveTakeAsync(...)` 之后，失败（如「文件号已存在」）不震。
- **注册点在静态类 `VoiSlate.Services.Haptics.Impl`**，不是 DI：Android 在 `AndroidApp` 构造函数里 `AndroidHaptics.Install()`（进程里最早可跑托管代码处）。**别再改回 DI 实例**——Avalonia 的 Application 初始化早于 MainActivity，视图构造时会拿到 Noop（见上条音量键陷阱 2）。
- 实现：API 31+ 走 `VibratorManager.DefaultVibrator`，26..30 走 `Vibrator`，`VibrationEffect.CreateOneShot(duration, amplitude)`。桌面 `Impl` 为 null，调用静默丢弃（原版也是探测 `hasVibrator()` 后置 `_canVibrate=false`）。
- **不要用 `HasAmplitudeControl` 去「纠正」幅度**：RMX5060 上该探针返回 false，但显式幅度照样被 HAL 采纳（真机日志 `amplitude` 由 `1.0` 变成 `0.50`）。传请求值即可。
- manifest 必须声明 `android.permission.VIBRATE`（普通权限，装包即授予，无需运行时弹窗）。

#### 语音摇杆（Android）：两个只在真机触摸上暴露的坑

摇杆实现在 `Desktop/DesktopWorkspaceSpeech.cs` 的 `BuildVoice()` + `AttachJoyGestures()`。
> **来历（读 Flutter 原版得出，`voislate-repo/lib/widgets/record_page/recorder_joystick.dart`，387 行）**：这个控件不是新写的，是把一个第三方「滑动确认（slide to confirm）」滑杆**整份拷进来改名**的——字段注释还留着滑杆的原始文档（`Height of the slider. Defaults to 70`、`sliderButtonContent` 默认 `Icons.unfold_more`、`text` 默认 `"Slide to confirm"`、`stickToEnd`/`foregroundShape`/`backgroundShape`），几何也是滑杆的 `slideLength = width - height`、`initValue = (width - height) / 2`；`pubspec.yaml` 里**没有**任何 slide 包依赖，属 vendored copy。改造做了四件事：①球体内容换成 `Icon(Icons.mic)`、抽掉确认文案；②把「右滑到端点＝确认」拆成两支——`< 0` 写 `leftTextController`（录音标注）、`> slideLength` 写 `rightTextController`（镜头标注），两个 `onXEdge` 在调用点都是空闭包；③塞进整条讯飞识别链（`permission_handler` 要权限、`EasyLoading` 提示、`resultStream.take(1)` 等结果、回来 `controller.text = value`）；④改了动画节奏：拖动时 `_duration = 0`（零延迟跟手）、松手 `_duration = 200`（回中）。调用点 `record_page.dart:649-666`：`width: 120`（height 默认 70）、外层 `Transform.scale(0.8)`、底色 `Colors.red.shade200` → `backgroundColorEnd: Colors.green.shade200`、`foregroundColor: purple.shade50`。据此对齐我方实现：拖动时**红→绿填充条**随行程线性插值（`#EF9A9A`→`#A5D6A7`，原版 `calculateBackground()` 的视觉契约）、松手回中 **200ms + `Curves.easeInExpo`**（原版 `AnimatedPositioned` 参数）。原版那一行 `Visibility(visible: !isVisible, child: slideBackground())` 是取反的（一按下反而藏掉进度条），我方按「拖动时才显示」实现。语义：按住话筒 → 左滑写「录音标注」、右滑写「镜头标注」、回中松手取消；松手后才把识别文本补进对应输入框（内容或镜头被改动则拒写并提示）。三个快捷键等价：`←`/`→` 选栏、`Enter`/`Space` 提交、`Esc` 取消。

1. **摇杆按钮自己挂的处理器收不到触摸**——记录页整页包在 `ScrollViewer` 里（`DesktopWorkspaceView.cs:302`），真机按下时 ScrollViewer 的手势识别器先吃掉 `PointerPressed` 并抢走指针；挂在按钮上的 `AddHandler(..., Tunnel)` 默认 `handledEventsToo: false`，于是一个事件都收不到（**鼠标不触发滚动识别器，所以桌面手测和 headless 用例全绿**）。现在处理器挂在**视图自己**（ScrollViewer 的祖先，任何指针路由必经），`RoutingStrategies.Tunnel, handledEventsToo: true`，并用 `e.GetPosition(_joyArea)` 判断是否落在摇杆矩形内。
2. **触摸必须显式 `e.Pointer.Capture(area)`**——Avalonia 不会因为「你处理了按下」就自动把后续 `PointerMoved` 送给同一元素（鼠标有隐式捕获，触摸没有）。不捕获的表现是：`[JOY] press` 有日志、`[JOY] move` 一条没有，松手时 `target=<none>` ⇒ 界面像「滑不动」。松手时记得 `e.Pointer.Capture(null)`。

排障：`adb logcat -s VoiSlateTrace`（`Services/Diagnostics.cs` 的静态 Sink，`AndroidApp` 构造里接到 `Android.Util.Log`），正常一次拖动应看到 `[JOY] press …` → `[JOY] move delta=… target=rec` → `[JOY] release target=rec`。

3. **松手处理器一定要先判断「这次手势是我的」** —— 处理器挂在视图上 + `Tunnel` + `handledEventsToo`，意味着它会收到**全页每一次**按下/移动/松手。两个在此踩过的坑，都已加 `_joyActive` 守卫与回归用例：
   - 松手时无条件 `e.Pointer.Capture(null)`：会在 `Button` 自己被派发 `PointerReleased` **之前**摘掉捕获 → Button 立刻 `PointerCaptureLost` → `IsPressed=false` → `Click` 永不触发。表现是**整个 App 所有按钮都点不动**（真机 + headless 同时复现；此前所有 UI 用例都用 `Invoke()` 直接 `RaiseEvent(Button.ClickEvent)`，把命中和捕获整条链路绕过去了，所以测试全绿）。用例：`Ui_buttons_receive_real_pointer_clicks`。
   - 用 `e.GetPosition(_joyArea)` + `IsEffectivelyVisible` 判命中：切到计划页/时间轴后 `_joyArea` 还指向上一页那个**已脱离可视树**的摇杆，`IsEffectivelyVisible` 无父级时按 `true` 算、`GetPosition` 对不在树上的控件返回 `(0,0)` → 位置检查全过 → **任何一次点击都被当成按住摇杆**，每点一下就开始录一嗓子，真机表现为「界面卡死」。现在 `Render()` 开头把 `_joyArea/_joyPress/_joyMove/_joyRelease` 全部清空、只由记录页重新装填，命中判定统一走 `InsideJoy(e.GetPosition(this))`（内部要求 `joy.GetVisualAncestors().Contains(this)`）。用例：`Ui_voice_joystick_target_is_cleared_when_leaving_the_record_page`。

#### 语音采集停止（Android）

`AndroidPcmCapture.Pump()` 的循环条件必须是 `while (!_stopRequested)`：真机实测 **`AudioRecord.Read` 在 `Stop()` 瞬间返回 0（不是负数）**，原先的 `if (read <= 0) continue;` 会让采集线程空转不退，`StopAsync()` 等 3 秒后抛 .NET 英文 `TimeoutException`，被 `Finish()` 的兜底 catch 原样弹成 toast（用户看到的就是那句莫名的 `timeout`）。现在 `read < 0` 视为致命（中文错误）、`read == 0` 睡 5ms 再试、`StopAsync` 超时则只 `TryComplete()` 收口（**不 Release**），并把 `TimeoutException` 映射成中文提示。`Dispose()` 必须「先 `Stop()` → 等采集线程退出（最多 2s）→ 才 `Release()` 原生对象」：采集线程还阻塞在原生 `Read()` 里时并发 `Release()` 是 SIGSEGV，进程无 Java 异常直接消失（logcat 只剩 `Death received`）；等待失败时宁可泄漏 `AudioRecord` 也不 Release。

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

### 9.2 ViewModels（仅 1 个：`RecordingSessionViewModel`）

> 2026-10-26：P7 的 9 个页面 VM（Main/Record/Schedule/Settings/SlateLog/SlateLogPage/SlateColumn/LogEditor/Placeholder）已整批删除，其能力要么迁移进 `Desktop/DesktopWorkspace*`、要么判定为审计误报，逐条记录在 [docs/p7-vm-coverage.md](docs/p7-vm-coverage.md) §8。**新功能不要再往 ViewModel 层加**。
- `RecordingSessionViewModel`：**实现 ISessionState**（Scene/Shot/TakeIndex + TakeCount=200 + SessionChanged），13 个 ObservableProperty 即时持久化到 ISessionSettingsStore；`Initialization` Task；Select{Scene,Shot,Take}/SetLink/SetOk{Take,Shot} 等。

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

约 200 `[Fact]` + 11 `[Theory]`（≈51 行 InlineData），合计 **276 用例全绿 / 6 跳过（共 282）**（2026-10-26：删 P7 的 56 条 VM 用例、legacy TakeFlowService 的 15 条用例，新增迁移/音量键/触觉/窄屏/翻页用例）。

| 组 | 文件（节选） | 覆盖 |
|---|---|---|
| Models | ModelTests / DataListVerbatim / FileNumberingServiceVerbatim / RecorderType / SlateLogItemSentinel | DataList 查重、文件号三前缀模式、枚举显式值与 JSON 短枚举名、Fake/Wild 哨兵逐字往返 |
| Services | TakeFlowServiceTests + 13 个模块测试 | B1-B11 业务规则复刻锁定（首按不写/收工守卫/假拍延迟一拍）、备份/导出/CSV 解析/跨日回滚/MicObjects |
| Desktop 工作台 | Desktop/Workspace{Service,Editing,Backup,LegacyAndSafety,PhoneLayout,Haptics,Ui}Tests | 记条/撤回/假条/命名、计划读写与守卫、旧会话迁移、按日导出、390×840 布局、翻页手势、音量键通道、震动幅度 |
| Controls | ControlSurface / WheelSelectionLogic / SlideConfirmLogic / DialStatusPalette / FileNumberFormat / PaletteResourceKeys | 滚轮/滑条纯逻辑状态机、控件契约默认值、主题资源键存在性与前缀校验 |
| RepositoryTests | LiteDB 仓储 + Seed + MockAsr | 保序往返、类型化设置、播种仅一次 |

**TestDoubles**：`TestDoubles/TestDoubles.cs`（FakeTimeProvider 固定 2026-08-20、FakeLogRepository、FakePickerHistoryStore、FakeSessionSettingsStore、FakeSessionState）、`TestDoubles/ScheduleDoubles.cs`（MutableFakeTimeProvider 跨天、FakeScheduleStore）（`ViewModels/ViewModelTestDoubles.cs` 已随 P7 的 VM 用例删除；音量键用例改用 `WorkspaceLegacyAndSafetyTests.cs` 内的 `FakeHardwareKeys`）。

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
- [ ] 改界面只改 `Desktop/DesktopWorkspaceView.cs`（+ 4 个分部类）；窄屏改动后跑 `WorkspacePhoneLayoutTests`（390×840 下底部 Tab 必须 4 等分、高 ≥44、三页无控件越界）
- [ ] 新增源文件放 src/VoiSlate 对应子目录（SDK glob 免改 csproj）；测试放 tests/VoiSlate.Tests 对应子目录，**只新增文件不改既有文件**
- [ ] 桌面回归：`dotnet build` 0w/0e + `dotnet test` 274/280（6 跳过属正常）
- [ ] Android 改动 → 发 arm64 APK → 真机安装启动验证（崩溃查 logcat FATAL）

## 13. 当前状态里程碑

- ✅ Gate 0：五轮 review + contracts v0.5
- ✅ Gate 1：P0.5 垂直切片（33/33 测试、启动+种子入库）
- ✅ P2：A/E/D/B/C 五 Agent 合入（Avalonia 12 三坑全修复）；build 0w/0e、**285/285**
- ✅ Android：工具链 + net10.0-android 工程 + arm64/x64 APK + **真机跑通**（`a326284`，RMX5060/Android 16）
- 🔲 待办（按需，现状缺口见 §7.7）：导出落盘实现（`SaveToFileAsync` 目录参数）、计划页落库（注册 LiteDbScheduleStore）、Toast 真实现注册、SlateLogPageViewModel 接线或删除、Android 真机 UI 细测