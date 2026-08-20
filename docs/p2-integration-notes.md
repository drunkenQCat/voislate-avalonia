# P2 集成记录（C 合入后收尾）

> 记录 C 合入 main 后的集成决策与两个 **Avalonia 12.1.1 实测破坏性变更**（本项目的"拦路虎"）。
> 对应提交：`f228fc9`（C 集成）、`bf4f573`/`d437b5a`（D 主题 XAML 修复）。

## 1. Avalonia 12.1.1 XAML 三个关键点（踩坑实录）

### ① `<ControlTemplate TargetType>` 是类型引用，不是 Selector

- `Style Selector="controls|SlideConfirmBar"` —— `|` 管道是 **Selector 语法**，正确。
- `ControlTemplate TargetType="controls|SlideConfirmBar"` —— **错误**。TargetType 走 XamlIl
  类型系统解析，必须用**冒号类型引用**：
  ```xml
  <ControlTemplate TargetType="controls:SlideConfirmBar">
  ```
  或 `TargetType="{x:Type controls:SlideConfirmBar}"`。
  症状：AVLN2000 `Unable to resolve type controls|SlideConfirmBar from namespace
  https://github.com/avaloniaui`——XamlIl 把整串当成默认命名空间下的类型名。
  注意：同程序集**无需** `XmlnsDefinition`/urn 映射，`xmlns:controls="using:VoiSlate.Controls"`
  即够（见 ②）。

### ② `ResourceDictionary.Source` 在 Avalonia 12 被移除

- Avalonia 12.1.1 的 `ResourceDictionary` 只有 `MergedDictionaries` / `ThemeDictionaries`，
  没有 `Source` 属性（11.x 可用）。症状：AVLN2000 `Unable to resolve suitable regular or
  attached property Source on type ResourceDictionary`。
- 替代：`Avalonia.Markup.Xaml.Styling.MergeResourceInclude`（编译期合并）或
  `ResourceInclude`（运行时加载，AOT 下不安全）：
  ```xml
  <ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
      <MergeResourceInclude Source="avares://VoiSlate/Themes/VoiSlatePalette.axaml" />
    </ResourceDictionary.MergedDictionaries>
  </ResourceDictionary>
  ```
  `StyleInclude` 不受影响（D 的控件默认样式仍用 `<StyleInclude Source="avares://...Controls.axaml"/>`）。

### ③ 隐式 `DataTemplate` 必须进 `DataTemplates` 集合，不可进 `Resources`

- `<Window.Resources>/<Application.Resources>` 是**键值集合**，不接受无 `x:Key` 的隐式模板。
  症状：AVLN3000 `Unable to find suitable setter or adder for property Resources for argument
  DataTemplate`。
- VM→View 隐式导航模板放这里：
  ```xml
  <Application.DataTemplates>
    <DataTemplate DataType="{x:Type vm:RecordViewModel}"><views:RecordView /></DataTemplate>
  </Application.DataTemplates>
  ```

## 2. C 集成决策（Views ↔ B 真实 VM）

C 的 Views 是按 C 自己的 stub 成员面写的，与 B 的正式 VM 有命名/结构漂移。集成基准：
**View 适配 VM（视图依赖 VM，而非反向）**，B 的 VM 保持规范、已测。

- **RecordViewModel**（核心页）：补 `ToggleAsrCommand`、`SetOkTake/SetOkShot`（直连会话）、
  `QuickNotes + RefreshQuickNotesAsync`（场记速览，sublist(40)，注入 ILogRepository）；
  Views 绑定 `CurrentFile*` → 规范名 `PrefixText/LinkerText/NumberText`；
  code-behind `Set*Async` → `Edit*Async`；C 的 VM 层 `FileNumberEditRequested` 事件删除
  （与 FileCounter 控件自身 `EditRequested` 冗余，保留控件事件路径）。
- **MainViewModel**：补 `CurrentPageKey`（左侧导航高亮）；场记页导航切到**扁平**
  `SlateLogViewModel`（C 视图是扁平 TodayLogs 列表；B 的分组版 `SlateLogPageViewModel`
  保留但不参与导航）。
- **SlateLogViewModel**：补 `SelectedDate`（切换即刷新）、`DeleteCommand`、`ExportCommand`、
  `RequestEdit/EditRequested`——`LogEditorViewModel` 在 VM 内构建（其构造器需
  ITakeFlowService，跨日编辑 v0.x 不支持）。
- **ScheduleViewModel**：B 已有完整方法式 API；补 `IRelayCommand` 命令壳（AddScene/AddShot/
  DeleteItem/EditItem/MoveItem）+ 计算属性 `SelectedScene/SelectedSceneShots`。
  `ImportCsv` 由 View code-behind 走文件选择器后 `ImportCsvAsync`（Button 无法产生 Stream）。
  编辑经 `SmallEditDialog` 编辑镜头备注 → `ApplyShotEdit`。
- **SettingsViewModel**：补三模式前缀面（`PrefixModes/PrefixMode/CustomPrefix/
  IsCustomPrefixEnabled`）、`RecordLinker`、`IsLinked`（直连会话单例，单一事实来源）、
  `SaveProject/SaveLinker/SavePrefix` 命令。未注入 IAsrService（设置页 ASR 状态行删除）。
- **App.axaml.cs DI**：按 B 真实构造器重写；`ISessionState` → `RecordingSessionViewModel`
  （删除 SessionStateImpl 注册）；补 `NoopHardwareKeyService/ExportService/NoopScheduleStore/
  CsvScheduleParserService`。启动序 await 会话 `Initialization` 后再 `ITakeFlowService.InitializeAsync`。
- **QuickNoteItem** 类型删除：速览直接用 `SlateLogItem`（自带 FileName/TkNote）。

## 3. 终态

- `dotnet build`：**0w/0e**（干净重建验证）。
- `dotnet test`：**285/285 通过**（A 32 + E 53 + B 64 + D 136 合并去重后）。
- 启动冒烟：进程 28s 稳定、无异常输出、窗口正常渲染。
- 分支：main + agent-a..e 已推送 `github.com/drunkenQCat/voislate-avalonia`。
- CI：`.github/workflows/ci.yml`（restore/build/test，ubuntu-latest，.NET 10）。