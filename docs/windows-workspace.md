# VoiSlate Windows 工作台

Windows 桌面入口使用原生 Avalonia 工作台，覆盖当前记录、拍摄计划和场记时间轴。数据继续使用 LiteDB。Android 分支不在本次验证范围内。

## 运行

需要 Windows 和 .NET 10。Release 输出为 `artifacts/windows/VoiSlate.exe`，请保留整个发布目录。

```powershell
dotnet run --project src/VoiSlate/VoiSlate.csproj
dotnet publish src/VoiSlate/VoiSlate.csproj -c Release -r win-x64 --self-contained false -o artifacts/windows
```

数据库默认位于 `%APPDATA%\VoiSlate\voislate.db`。环境变量 `VOISLATE_DATA_DIR` 可隔离数据库及默认备份目录；界面验证使用 `artifacts/ui-bailian`。

## 百炼语音

在“工程与偏好 → 百炼语音识别”填写北京地域百炼 API Key，选择麦克风，点击“保存语音设置”。留空保留已有 Token；“移除 Token”删除保存值。业务空间 ID 可选。“测试连接”验证 WebSocket 握手，实际识别还取决于模型权限、额度和麦克风。

按住话筒采集音频，左滑选择录音标注，右滑选择镜头标注，松手提交；回中松手、失去捕获、导航或关闭窗口会取消。键盘聚焦话筒后左右键开始并选侧，Enter 提交，Esc 取消。最长录音 60 秒。识别过程只显示预览，成功结束后追加一次；内容或镜头在等待期间变化时不会覆盖草稿。

Token 使用 Windows DPAPI 当前用户加密，独立存于 settings，不进入桌面会话、JSON 导出或备份。复制数据库到其他 Windows 用户后需要重新填写 Token。程序不记录 Token 或服务端原始错误正文。

协议使用 `paraformer-realtime-v2`，16 kHz、16 bit、单声道 PCM，等待 `task-started` 后发送音频，停止采集后发送尾部音频及 `finish-task`，接收最终结果直至 `task-finished`。按 `begin_time` 替换中间句，跳过心跳，防止重复追加。

接口依据：
- [百炼 WebSocket 接入指南](https://help.aliyun.com/zh/model-studio/websocket-for-paraformer-real-time-service)
- [客户端事件](https://help.aliyun.com/zh/model-studio/paraformer-client-events)
- [服务端事件](https://help.aliyun.com/zh/model-studio/paraformer-server-events)

## 已实现的业务

- 当前记录：场 / 镜 / 次、双标注、双评价、当前及下一个文件号、下一条、结束本镜、下一镜、手动改号、A/B 补录、假条 F、人物 / 轨道快照。
- 文件命名：日期、Sound Devices 日期格式、自定义前缀及分隔符。命名空间分别记录文件号高水位，自定义前缀跨日接续，删除和清空记录不回收文件号。
- 撤回：最近 100 步保存操作的完整会话快照持久化，重启后仍可连续撤回；切镜、手改编号、切命名规则和历史编辑等操作会清除撤回链，防止撤销过期上下文。
- 计划：进度筛选、场次导航、概要与人物、场镜新增 / 编辑 / 删除、恢复最近删除、上移 / 下移、前后插入、选择开拍、完成镜再拍、查看对应历史。
- 时间轴：日期、连续场镜分组、时间、双评价 / 双标注、人物轨道、补录与结束标识、搜索 / 筛选、定位最新、待保存条、查看对应计划。
- 历史编辑：文件序号、次号、场信息、人物轨道、双标注与评价；重复文件名被拒绝。按原日期与记录 Id 定位，跨日编辑不依赖显示位置。
- 稳定关联：计划场镜使用稳定 ID，改号后保留草稿和历史关联；删除后新建同号镜头不会继承旧镜头场记。历史标签保留拍摄时快照。
- 旧数据：兼容空字符串及无时间记录；解析旧 Flutter `<人物/>` 轨道标记。不能直接读取 Hive 二进制数据库，需要先从 Flutter 导出 JSON。
- 偏好：工程名、左手布局、650ms 长按防误触。宽 / 窄窗口均有导航。Ctrl+1/2/3 切记录 / 计划 / 场记，Ctrl+Enter 记条（遵守口袋锁），Ctrl+Shift+Z 撤回。

## 导入、导出及备份

CSV 支持四列简表与工程七列格式，含 BOM、引号、转义和字段内换行；预览后按编号合并。

桌面 JSON v2 包含工程、计划、按日期的场记、草稿和命名状态。兼容导入此前桌面 v1 和 Flutter 的场记数组。Flutter 日期优先由时间元数据、日期前缀或 Sound Devices 前缀推断；自定义前缀无法推断时，用户在预览中指定归档日期。

合并保留现有计划内容，同日期同文件名且内容相同的记录跳过，内容冲突则整体停止。恢复会替换计划和全部场记；Token、本机备份偏好及文件号高水位保留。操作前先生成备份，写入在 LiteDB 事务中完成，失败回滚。导入后清空撤回链。

默认每三分钟及退出时备份，支持选择目录、关闭自动备份及立即备份。默认目录为“文档/VoiSlate Logs/backups”；设置隔离数据目录时改为该目录下 backups。每份文件独立命名，先写临时文件再原子重命名。没有自动清理旧备份。

“导出 Flutter 兼容场记 JSON”输出扁平数组及字符串枚举，轨道还原为旧标记；补录使用旧 W=0 约定。建议完整恢复使用桌面 v2，避免丢失桌面独有的补录字母、草稿等信息。

## Windows 验证

```powershell
dotnet test tests/VoiSlate.Tests/VoiSlate.Tests.csproj --filter 'FullyQualifiedName~BailianSpeechTests|FullyQualifiedName~DesktopWorkspaceTests|FullyQualifiedName~RepositoryTests|FullyQualifiedName~SlateLogItemTests|FullyQualifiedName~ScheduleStoreTests'
```

测试覆盖百炼事件顺序 / 中间句替换 / 错误及取消清理 / 音频尾部 / DPAPI，计划及稳定关联、命名高水位、假条、多步撤回、轨道快照、完整历史编辑、备份往返、旧版导入、重复冲突和数据库写入失败回滚。

当前选定的 Windows 测试共 65 项通过，包含 13 个 Avalonia 界面用例。界面测试使用真实工作台控件和样式，以 Skia 离屏渲染，模拟输入；音频源和百炼传输使用测试替身，不上传麦克风音频。覆盖 Token 输入 / 加密保存 / 重开、无 Token 提示、键盘和鼠标两侧提交、回中与等待最终结果时取消、迟到结果保护、口袋锁短按 / 右键不记条、鼠标 / 键盘长按只记一条、Ctrl+Enter、计划移动 / 插入、退出备份及宽窄布局。

离屏回归修复了 Button 默认事件先处理 Enter 和指针事件的问题，防止键盘提交、拖动识别或长按未执行。另修复服务端完成事件先于本地 finish-task 发送操作返回时被误判为提前结束的时序。

生成离屏渲染图：

```powershell
$env:VOISLATE_RENDER_DIR = "$PWD/artifacts/rendered-ui"
dotnet test tests/VoiSlate.Tests/VoiSlate.Tests.csproj --filter 'FullyQualifiedName~DesktopWorkspaceTests.Ui_'
```

已人工查看设置、录音页、500px 窄布局、计划、计划编辑操作和时间轴的离屏图，见 `temp/screenshots/rendered-*.png`（本机临时目录，不入库）。这些是离屏渲染图，不是原生窗口截图。

Release 发布成功。已完成真实 Windows 窗口的视觉和主要操作验收：百炼设置与实际麦克风列表、空 Token 的测试连接 / 语音提示、语音设置保存反馈、设置滚动、立即备份、记条推进、计划进度 / 历史联动、计划编辑操作布局、时间轴回读、历史轨道修改，以及正常退出备份和重启持久化。截图见 `temp/screenshots/windows-bailian-*.png`（本机临时目录，不入库）。

验收使用隔离数据库 `artifacts/ui-bailian`。手动备份及退出备份已检查为 v2，退出备份保留一条原生验收场记、Boom / 演员甲轨道及下一文件号 2；重启后在真实窗口再次确认。三分钟自动备份文件也已在运行时观察到。具体证据与剩余验收见 [windows-verification.md](windows-verification.md)。

没有提供真实 API Key，因此尚未做账户上的云端转写验证。未构建、运行或验证 Android。原有 `Views/RecordView.axaml` 的两处 Watermark 过时警告仍存在。
