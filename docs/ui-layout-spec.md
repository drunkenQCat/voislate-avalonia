# VoiSlate UI 布局复刻规范（供 Avalonia 实现）

> 依据 `/home/quantumcat/my_repo/voislate-html/styles.css`（699 行）与 `app.js`（884 行）精读提取，逐条只报事实；
> 涉及 `index.html` 骨架的要素仅在被 CSS/JS 直接引用时列出。所有数值、类名、选择器、函数名均照抄源码。
> 复刻对象：**手机壳形态**（`.phone` 390×860），桌面场景下居中展示，无 JS/CSS 缩放。

---

## 1. 设计令牌（styles.css 的 `:root` 变量与全局）

### 1.1 `:root` 变量（第 9–31 行）

| 变量 | 值 | 语义（源码注释） |
|---|---|---|
| `--primary` | `#266489` | flex_color_scheme FlexScheme.bahamaBlue 近似主色 |
| `--primary-dark` | `#1d4f6e` | 主色深一档（设置页 AppBar 背景） |
| `--scaffold` | `#f7f7f7` | 全局页面背景 |
| `--m3-nav-pill` | `#d6e6ef` | **本复刻中未在任何规则中使用（死令牌）** |
| `--notice` | `#f2f5de` | 记录页摘要卡背景（淡黄绿） |
| `--digi-bg` | `rgba(124, 106, 10, 0.63)` | 数码管数字框背景（对应 Flutter 0xA07C6A0A） |
| `--purple-300` | `#ba68c8` | chips 主紫 |
| `--purple-100` | `#e1bee7` | sheet 内 selected toggle、qt-head、name-chip 底 |
| `--purple-50` | `#f3e5f5` | scn-info 底、knob 底 |
| `--sel-scn` | `#d1c4e9` | 计划页左侧选中场次底色 |
| `--sel-sht` | `#e0e0e0` | 计划页右侧选中镜头底色 |
| `--add-purple` | `#63326e` | “+” 按钮底 |
| `--fake-brown` | `#291711` | Fake Take 圆形按钮底 |
| `--notes-blue` | `#8eb1c7` | 场记速览 FAB 底（灰蓝） |
| `--bluegrey-100` | `#cfd8dc` | filecounter 底 |
| `--joystick-red` | `#ef9a9a` | red.shade200（摇杆左半） |
| `--joystick-green` | `#a5d6a7` | green.shade200（摇杆右半） |
| `--knob` | `#f3e5f5` | purple.shade50（摇杆旋钮） |
| `--divider` | `#e0e0e0` | 分割线 |

### 1.2 图标 / 文字色（裸色值）

| 用途 | 类 | 值 |
|---|---|---|
| 红 | `.ms.red` / `.ms.danger` | `#f44336` |
| 绿 | `.ms.green` / `.ms.ok` | `#4caf50` |
| 蓝 | `.ms.blue` | `#2196f3` |
| 浅绿（摇杆右箭头） | `.ms.g300` | `#81c784` |
| 浅红（摇杆左箭头） | `.ms.r300` | `#e57373` |
| 正文深灰 | — | `#212121` / `#333` / `#222` / `#1c1c1c` |
| 次文字 | — | `#666` / `#555` / `#757575` / `#999`(placeholder) |

### 1.3 字体

- 全局：`"Roboto", "Noto Sans SC", "PingFang SC", "Hiragino Sans GB", "Microsoft YaHei", "Segoe UI", system-ui, sans-serif`；HTML 侧通过 Google Fonts 加载 `Roboto:wght@400;500;700`。
- 数码字、文件名、filecounter、log 标题等“数字类”文本显式 `.font-family: "Roboto", sans-serif`。
- 图标字体：`"Material Symbols Outlined"`，基准字号 24px，`font-variation-settings: "FILL" 0, "wght" 400, "GRAD" 0, "opsz" 24`，`line-height: 1`，`user-select: none`；`.ms.tiny` 为 19px（实际 19px 小红点/绿图标均用内联 `style="font-size:19px"`）。

### 1.4 全局重置 / 圆角 / 阴影 / 间距体系

- `* { box-sizing: border-box; margin: 0; padding: 0; }`；`[hidden] { display: none !important; }`（**hidden 属性 = 完全隐藏，不占位**）。
- 无全局间距变量；间距全部硬编码（14px 页边距、16px 卡片上边距最常见）。
- 阴影惯例：卡片轻阴影 `0 1px 3px rgba(0,0,0,0.12~0.16)`；强浮起 `0 3px 10px rgba(0,0,0,0.2~0.35)`；弹层 `0 24px 60px rgba(0,0,0,0.4)`；口袋 `0 -10px 40px rgba(0,0,0,0.35)`；手机壳 `0 40px 90px rgba(0,0,0,0.55)`。
- 圆角惯例：卡片 12px；按钮 8px；圆形 50%/圆角胶囊 22~35px；弹窗 28px；底部 sheet 上圆角 26px。

### 1.5 手机壳 `.phone` 与 `.stage`（49–66 行）

- `body`：`radial-gradient(1200px 700px at 50% -10%, #2b3a4a, #12161b 70%)` 深色渐变底；`display:flex; align-items:center; justify-content:center; padding: 28px 12px;`。
- `.stage`：flex column，居中，`gap: 14px`（包手机壳 + 下方图例）。
- `.phone`：`position:relative; width:390px; height:860px; background: var(--scaffold)（#f7f7f7); border:7px solid #161c22; border-radius:42px; overflow:hidden; box-shadow: 0 40px 90px rgba(0,0,0,0.55), inset 0 0 0 1px rgba(255,255,255,0.06); display:flex; flex-direction:column;`。
- 刘海 `.phone::before`：绝对定位 `top:0; left:50%; translateX(-50%)`，128×24px，`#161c22`，下圆角 16px，`z-index:60`。
- 小屏适配（`@media (max-width:540px)`）：body padding 0；`.phone` 变 `width:100%; height:100vh/dvh; border:none; border-radius:0`；刘海与图例隐藏。

---

## 2. 全局框架

### 2.1 `.appbar`（95–105 行）

- 高 **40px**，`flex:none`，背景 `var(--primary)` `#266489`，文字 `#fff`，`display:flex; align-items:center; justify-content:space-between; padding:0 2px; z-index:10`。
- `.appbar-title`：17px / weight 500，`padding-left:10px`。主 AppBar 标题「VoiSlate」，右侧一个 40×40 圆形 icon-btn（settings 齿轮，`#btn-settings`）。
- `.appbar.sub`：背景 `var(--primary-dark)` `#1d4f6e`（设置页），左侧返回 icon-btn（back 图标，`#btn-back-settings`）+ 标题「VoiSlate 设置」+ 右侧 40px 占位空 span。
- `.icon-btn`（86–92 行）：40×40 圆形，无边框透明背景，`color:inherit`，hover `rgba(255,255,255,0.12)`、active `rgba(255,255,255,0.22)`（注意：hover 底色公式固定为白色半透明，放在浅色 bar 上不可见）。

### 2.2 `.view` 与 `.page`（108–115 行）

- `.view`：`position:relative; flex:1; min-height:0;`（AppBar 与 bottomnav 之间）。
- `.page`：`position:absolute; inset:0; display:none; background:var(--scaffold);`；`.page.active { display:block; }`；**例外 `.page#page-log`**：`display:none; flex-direction:column;`，`.active` 时 `display:flex`。
- 页面切换 = `setupNav()`：点击 `.nav-item` → 全体移除 `.nav-item.active` → 给点击项加 `.active` → 按 `dataset.tab` 在 `#page-plan/#page-record/#page-log/#page-test` 上切换 `.page.active`。**纯 display 切换，无动画、无过渡**。

### 2.3 底部导航 `.bottomnav`（index.html 271–276 行）

- **重要事实：styles.css 中不存在 `.bottomnav`、`.nav-item`、`.icon-holder` 任何规则**（grep 证实；index.html 亦无内联 `<style>`）。该复刻的底部导航 = 4 个无样式 `<button class="nav-item">`，仅由 flex 容器 `.phone` 推到最底部，图标用 `.ms`（24px），文字为普通按钮默认字体。**Avalonia 复刻时其视觉（高度、active 高亮、图标容器）属自由实现**，但需还原以下结构性事实：
  - 4 项顺序与 data-tab：`plan`（计划，图标 `edit_calendar`）、`record`（记录，图标 `record_voice_over`）、`log`（场记，图标 `format_list_bulleted`）、`test`（识别测试，图标 `mic`）。
  - 初始 `active` 在 **record** 项上。
  - 每个 `.nav-item` 结构：`<span class="icon-holder"><span class="ms">图标</span></span><span>标签文字</span>`。
  - 切换逻辑仅依赖 `data-tab` 与 `.active`（见 2.2）。

### 2.4 桌面展示 / 屏幕适配

- 桌面大屏：无缩放逻辑；`.phone` 固定 390×860 居中于深色渐变 body 上（1.5）。`.stage` 下附 `.legend` 图例（深底浅字，未参与 App 布局，可忽略）。
- 窄屏（≤540px）：手机壳铺满视口、无边框圆角（Avalonia 桌面版可忽略，但窗口缩放逻辑可参考“手机壳=唯一画布”）。

### 2.5 滚动容器体系

| 容器 | 规则 |
|---|---|
| `.record-scroll` | `position:absolute; inset:0; overflow-y:auto; padding-bottom:26px;`（记录页整页纵向滚动） |
| `.sch-left .list` / `.sch-right .list` | `flex:1; overflow-y:auto;`（计划页左右两列独立滚动） |
| `.log-scroll` | `flex:1; overflow-y:auto; padding-bottom:84px;`（场记页列表，留出 share-fab 空间） |
| `.settings-body` | `overflow-y:auto; padding:8px 4px 24px;` |
| `.quick-table` | `max-height:420px; overflow-y:auto;` |
| `.log-tabs` / `.chip-row` | 横向滚动且隐藏滚动条（`scrollbar-width:none`; `::-webkit-scrollbar{display:none}`） |

---

## 3. 记录页规范（page-record，逐组件从上到下）

record-scroll 内顺序：notecard → tile(NEXT) → divider → lockable( add-row / divider / mini-row / input-area / controls )。

### 3.1 `.notecard`（buildCurrentTkNoticeCard）

- 容器（121–127 行）：`margin:16px 14px 0;`背景 `var(--notice)` `#f2f5de`；圆角 12px；阴影 `0 4px 12px rgba(0,0,0,0.18)`；内边距 `12px 10px`。
- `.digi-row`：`display:flex; justify-content:center; gap:6px;`。
- `.digi`：宽 **108px**，flex column，居中。三格单位：场 / 镜 / 次。
- `.digi-box`：`width:100%; background:var(--digi-bg)(rgba(124,106,10,0.63)); border-radius:12px; padding:8px 4px; min-height:54px;`内容居中。
- `.digi-num`：Roboto **40px / weight 500 / `#000`**，`line-height:1`。三个值：`#cur-scn`（初始 1A）、`#cur-sht`（初始 1A）、`#cur-tk`（初始 1）。
- `.digi-unit`：**18px / `#212121`** / `margin-top:4px`。
- `.file-monitor`（141–149 行）：`margin-top:10px; background:#fff; border-radius:12px; box-shadow:0 1px 3px rgba(0,0,0,0.12); padding:8px 14px; display:flex; align-items:center; justify-content:center; gap:13px;`。内容 = 19px 红点 `.ms.red radio_button_checked`（内联 font-size:19px）+ `.file-name`（`#cur-file`）。
- `.file-name`：`font-size:20px; font-family:"Roboto",sans-serif;`（无颜色声明→继承黑色）。

### 3.2 `.tile`（ExpansionTile NEXT）

- 容器（152–158 行）：`margin:16px 14px 0; background:#fff; border-radius:12px; box-shadow:0 1px 3px rgba(0,0,0,0.16); overflow:hidden;`。
- `.tile-head`（159–164 行）：整宽 button，`display:flex; align-items:center; justify-content:space-between; padding:12px 14px;` hover 底 `#fafafa`。
  - 左侧 `.next-row`（gap:6px）：`play_arrow`（.ms.blue）+ 标签 + `skip_next`（.ms.blue）三个元素。
  - `.next-label`：15px / weight 500；元素上另挂 class `blue`，**但 `.blue` 仅对 `.ms.blue` 生效，`.next-label.blue` 无匹配规则 → 标签实际渲染为继承色（近黑）**。`.next-label.card-bulu`（补录态）：12px / weight 400 / `#000` / 白底 / `padding:4px 10px` / 圆角 5px / 阴影 `0 1px 3px rgba(0,0,0,0.3)`。
  - 右侧 `.tile-trailing`（gap:10px）按展开态隐藏/显示三选一：
    - `.preview.pre-wheels`（`#preview-wheels`）：块级，`overflow:hidden; border-radius:8px; width:120px; height:42px;`（内容见 3.3 mini wheels）。
    - `.preview.pre-counter`（`#preview-counter`，初始 hidden）：150×38px，内含 0.52 缩放的 filecounter（`renderFileCounter(el, 0.52)`：`transform:scale(0.52); transform-origin:top left; width:150/0.52≈288.5px;`并移除内层 box-shadow）。
    - `.next-text`（`#next-text`，初始 hidden）：13px / `#333`，文案由 JS 生成 `1A场1A镜1次`。
    - `.chev`：`expand_more`，`color:#666`，`transition:transform 0.2s`；`.tile.open .chev` 旋转 180°。
  - **展开/收起逻辑**（app.js 367–374）：点击 `#next-head` → 切换 `#next-tile.open` → `#next-body.hidden = !open`；`#next-text.hidden = !open`；`#preview-wheels.hidden = open`；`#preview-counter.hidden = open || isLinked`。
- `.tile-body`（#next-body，181 行）：`border-top:1px solid #eee; background:#f5f5f5;`。

### 3.3 NEXT 监控区 `.monitor`（buildNextTakeMonitor）

- `.monitor`（184 行）：`position:relative; padding:10px 8px 12px;`。
- `.link-pill`（185–194 行）：绝对定位 `left:0; top:10%; z-index:5;`，**34×58px**，底 `rgba(255,255,255,0.85)`，圆角 10px，阴影 `0 2px 8px rgba(0,0,0,0.25)`，居中图标（`#link-icon` 初始 `link`），颜色 `#37474f`；`.off` 态：底 `#9e9e9e`、图标白。点击切换 `isLinked`：图标 `link`↔`link_off`；`.monitor-card.unlinked`（底 `#e0e0e0`、去阴影）；标签 `NEXT`↔`补录`（补录态挂 `card-bulu`）；toast `已取消补录模式` / `进入补录模式，Take 号与文件号解绑`。
- `.monitor-card`（195–202 行）：`position:relative; background:#fff; border-radius:12px; box-shadow:0 3px 10px rgba(0,0,0,0.2); padding:6px 0 4px;` flex column 居中。
- `.next-col`（203–208 行）：绝对定位 `left:19px; top:50%; translateY(-50%); z-index:3;`，`color:#2196f3; font-size:11px; line-height:1.25;`纵向排列 `fast_forward` 图标 + 下/一/条（即“下一条”），`pointer-events:none`。
- 主滚轮挂载点 `#monitor-wheels`（`buildPicker`，见 3.4）。
- `.hint`（`#shot-changed-hint`，209 行）：**11px / `#c62828`/ padding:2px 0**，文案「长按修改当前镜」。显示条件：`setShotHint(true)`（滚轮改了场或镜）→ 移除 hidden；`onAdd()` 后 → 隐藏。初始 hidden。
- `.filecounter`（`#filecounter`，见 3.5）挂在 monitor 底部。

### 3.4 SlatePicker 三列滚轮 `.wheels`（211–251 行 + Wheel 类）

CSS 变量（主滚轮）：

| 变量 | 值 |
|---|---|
| `--ph`（列高） | **112px** |
| `--ih`（行高） | **50px** |
| `--cw`（列宽） | **96px** |
| `--fs`（普通字号） | 20px |
| `--fs-sel`（选中） | **25px / weight 600** |
| `--fs-adj`（相邻） | 18px / `opacity:0.5` |

- 结构：`.wheels`（flex、居中、`align-items:flex-start`）内含 3× `.wheel-col`（宽 96、column 居中），列之间插 `.wheel-sep`（宽 1px、`#bdbdbd`、`margin-top:14px`、`align-self:stretch` 撑满列高；**仅当 `ph>58` 才创建**），列下方 `.wheel-unit`（18px / `#212121` / `margin-top:3px`，单位 Scene / Shot / Take）。
- `.wheel`：`width:100%; height:var(--ph)=112px; overflow:hidden; cursor:pointer; user-select:none; touch-action:pan-y;`。
  - `.band`：绝对定位 `left:6%; right:6%; top:50%` 居中，`height: calc(ih+8px)=58px;` 底 `rgba(209,196,233,0.31)`（等宽紫 80 alpha），圆角 10px，`pointer-events:none` —— 选中行高亮带。
  - `.list`：绝对定位全宽，`transition: transform 0.16s ease-out;`；`.wheel.dragging .list` **去掉过渡**。`translateY(y)`，`y = (ph-ih)/2 - sel*ih = 31 - sel*50`。
  - `.item`：高 50px，居中，`#222` Roboto；`.sel` 25px/600；`.adj` 18px/`opacity:.5`。
- “迷你滚轮” `.wheels.mini`（243–251 行，NEXT 头部预览用，`opts:{ph:42, ih:20, interactive:false}`）：`--ph:42px; --ih:20px; --cw:39px; --fs:11px; --fs-sel:14px; --fs-adj:10px;`整体 `transform:scale(0.92); transform-origin:left center;`（配合 `.pre-wheels` 120×42 裁切）；隐藏 `.wheel-unit`；`.wheel-sep margin-top:10px`（因 ph≤58 实际不创建）；`.wheel` `pointer-events:none`。
- 行为（Wheel 类，app.js 83–161）：
  - `pointerdown` → `dragging=true; moved=false; startY=clientY; startSel=sel;`加 `.dragging`（临时禁过渡）并 `setPointerCapture`。
  - `pointermove`：`delta = clientY-startY`；`|delta|>4px` 置 `moved=true`；`sel = startSel - round(delta/ih)`（每 50px 滑动一格，向上拖减小）。
  - `pointerup`：移除监听、去 `.dragging`；**若从未移动（点击）→ `set(sel+1)`（即“点击滚到下一项”，对应 take 列 addItem 后的 scrollToNext）**；最后触发 `onChange(values[sel], sel)`。
  - 选项变更回调 `onWheelChange`：scene → 重设 `state.scn`、`state.sht=0`、rebuild shot 列（主+预览），`setShotHint(true)`；shot → 更新 `state.sht`、`setShotHint(true)`；take → `state.tk = index+1`；最后 `syncRecordUI()`。

### 3.5 FileCounter 三卡片 `.filecounter`（254–269 行 + renderFileCounter）

- 容器：`margin:10px 6px 4px; background:var(--bluegrey-100)(#cfd8dc); border-radius:12px; border:1px solid rgba(0,0,0,0.05);`flex 居中，`gap:10px; padding:8px 12px;`（buildPicker 外侧 `.monitor` 内、monitor-card 之下）。
- 三段 `.fc-seg`（column、gap:3px）：
  1. `.fc-tag` **Date** + `.fc-card.fc-prefix`：`PREFIX`（230522）。
  2. `.fc-tag` **Divider** + `.fc-card.dim.fc-linker`：`LINKER`（-T）；`.dim`：字号 25px / weight 400 / `rgba(0,0,0,0.5)`。
  3. `.fc-tag` **Num** + `.fc-card.fcid.fc-num`：`pad3(recCount)`（初始 **002**，注意是“下一个”文件号）；`.fcid` 圆角 7px。
- `.fc-card` 基准：白底、圆角 4px、阴影 `0 2px 5px rgba(0,0,0,0.28)`、`padding:2px 6px`、Roboto **24px / `#222`**。
- 同一函数 `renderFileCounter(target, scale=1)` 渲染两处：主 `#filecounter`（scale 1）与头部预览 `#preview-counter`（scale 0.52，见 3.2）。

### 3.6 `.divider`（272 行）

`height:1px; background:var(--divider)(#e0e0e0); margin:0;`——记录页共 2 处（tile 后、add-row 后）。

### 3.7 `.lockable` 与 `.add-row`

- `.lockable`（274–278 行）：`transition:filter 0.2s;`；`.locked`：`filter:grayscale(1); opacity:0.9;` 且禁用指针事件于 `.add-btn`、`.mini-btn`、`.fields`、`.controls .dial-wrap`、`.joy`。由 lock-switch 切换（见 3.12）。
- `.add-row`（280 行）：`position:relative; padding:10px 12px;`。
  - `.add-btn`（`#btn-add`，281–287 行）：整宽 × **58px**，`background:var(--add-purple)(#63326e); color:#fff; border-radius:8px;`阴影 `0 3px 8px rgba(99,50,110,0.45)`；图标 `add`；`:active` `scale(0.99)`。语义 = 源码 addItem()：新建一条场记、文件号 +1（`onAdd`）。
  - `.fake-btn`（`#btn-fake`，288–294 行）：**绝对定位 `left:20px; top:50%; translateY(-50%)`，46×46 圆形**，底 `var(--fake-brown)(#291711)`，白图标 `.ms.red move_down`，阴影 `0 3px 8px rgba(0,0,0,0.4)`；`title="Fake Take"`。语义 = Fake Take（假镜头）：清空 desc 输入框、占位符改「这条跑了」（`onFakeTake`）。
- `.mini-row`（296 行）：`display:flex; justify-content:space-evenly; padding:12px 10px 6px;`。
  - `.mini-btn`：**92×50px**、圆角 8、底 `#e9edf1`、`#212121`、阴影 `0 2px 4px rgba(0,0,0,0.18)`；`:active` `scale(0.98)`。`.mini-btn.shadow` 阴影加厚 `0 5px 12px rgba(0,0,0,0.25)`。
  - `#btn-dec`：图标 `remove`（.ms.danger 红）；**长按**语义 = 撤回上一条场记（`onDec`/`onDecUp`，见 §10）。
  - `#btn-save`：`shadow` 变体，图标 `save`（.ms.ok 绿）；语义 = 镜头结束（`onShotEnd`）。

### 3.8 `.input-area` / `.fields` / `.field`（inputArea: PrevTakeEditor / PrevShotNote / RecorderJoystick）

- `.input-area`（307 行）：`position:relative; padding:14px 12px 4px;`。
- `.fields`：`display:flex; gap:8px;`两列 `.field`（`flex:1; min-width:0;`）。
- `.field-title`（310–314 行）：`display:flex; align-items:center; justify-content:flex-start; gap:5px; font-size:13.5px; color:#333; margin-bottom:4px; padding-left:2px;`。`.field-title.right`：`justify-content:flex-end;`。
  - 左列（正在录制）：19px `.ms.red radio_button_checked` + `#rec-title`（「正在录制:T001」，动态）。下方 `#desc-input`（textareas 见下）。
  - 右列（右对齐）：`#sht-title`（「S1A Sh1A Tk」，动态）+ `.tk-badge#tk-badge` + 19px `.ms.green movie`。
  - `.tk-badge`（315–316 行）：`padding:0 4px; border-radius:4px;`；`.tk-badge.ok`：底 `rgba(167,199,130,0.55)`（对应 0xA87BA782）。内容 = `state.tk` 或镜头结束后 `OK`。
- `.field textarea`（317–324 行）：`width:100%; height:76px; resize:none; border:1px solid #bdbdbd; border-radius:4px; padding:8px; font-size:13px; color:#222; background:#fff;`placeholder `#999`（`white-space:pre`——支持占位符内换行），focus `border-color:var(--primary)`。`#desc-input` 占位符由 JS 动态写 `230522-T001\n 录音标注...`（初值同款）；`#note-input` 固定占位符 `Shot Note`。
- **`.joy-scaler` / `.joy` /`.joy-knob`（joystick，326–350 行）**：
  - `.joy-scaler`：`position:absolute; top:26px; left:50%; transform:translateX(-50%) scale(0.8); transform-origin:center top; z-index:6;`——**常驻显示，永远 0.8 缩放；CSS/JS 中均无“按住才显现”或动画改变该比例**（无 transition、无 JS 修改）。
  - `.joy`：**120×70px，圆角 35px**，`padding:5px`，`cursor:grab; touch-action:none;`背景 `linear-gradient(90deg, var(--joystick-red)(#ef9a9a), var(--joystick-green)(#a5d6a7))`（左红右绿），阴影 `0 3px 10px rgba(0,0,0,0.25)`。
  - `.joy-arrows`：`position:absolute; inset:0; z-index:1;`flex `space-evenly` 居中，`pointer-events:none;`。**左箭头 = `arrow_left`（.ms.r300 浅红 #e57373）；右箭头 = `arrow_right`（.ms.g300 浅绿 #81c784）**（HTML 顺序：arrow_right 在前 → 左位绿右箭头、右位红左箭头）。
  - `.joy-knob#joy-knob`：`position:absolute; top:5px; left:32px;（基准 base）z-index:2;`**60×60 圆形**，底 `var(--knob)(#f3e5f5)`，阴影 `0 2px 8px rgba(0,0,0,0.3)`，居中白 `mic` 图标（内联 `color:#fff`）。
  - **交互（`setupJoystick()`，app.js 464–502）**：`minL=5, maxL=55, base=32`（reach=50）。
    - knob `pointerdown`：`active=true; startX=e.clientX;`knob `transition:none`；`setPointerCapture`；toast「开始录音…」**900ms**。
    - `joy` 元素 `pointermove`：`dx=clientX-startX; pos=clamp(base+dx,5,55); knob.style.left=pos+'px'`（knob 平移，主体不移动）。
    - knob `pointerup/pointercancel`（release）：若 `pos > 55-50*0.25=42.5` → **右滑**：`#note-input.value='录音内容已转为镜头标注（语音识别演示）'`，toast「滑到右侧：结束录音并转写」；若 `pos < 5+50*0.25=17.5` → **左滑**：`#desc-input.value='录音内容已转为录音描述（语音识别演示）'`，toast「滑到左侧：结束录音并转写」；中间 → toast「已停止录音」。随后 knob `transition:left .2s ease` 回弹 `left:32px`。
    - **注意：joystick 并不负责“添加场记/撤回”**——添加 = `.add-btn` 点击（onAdd），撤回 = `#btn-dec` 600ms 长按（onDec）。摇杆仅是“拖动两端触发语音识别演示”的 RecorderJoystick 复刻。

### 3.9 `.controls`（bottomControlButtons）+ SpeedDial + lock-switch

- `.controls`（353 行）：`display:flex; justify-content:space-between; align-items:center; padding:20px 18px 8px;`——从左到右：notes FAB、take dial-wrap、shot dial-wrap、lock-switch。
- `.fab`（355–368 行）：**56×56 圆形**、`#fff`、阴影 `0 4px 10px rgba(0,0,0,0.3)`；`:active` `scale(0.95)`。
  - `.fab.notes`：底 `var(--notes-blue)(#8eb1c7)`，图标 `notes`；点击开“场记速览”弹窗（`openQuickView`）。
  - `.fab.dial`：`color:#000; transition:background 0.2s;`。`.tk` / `.sht` 常态底 `var(--notice)(#f2f5de)`。
  - 状态色与图标（点击 dial 菜单后切换 `className` 与图标文本）：

    | 状态 | 类 | 底 | 图标 |
    |---|---|---|---|
    | take 初始 | `fab dial tk` | #f2f5de | `headphones` |
    | take bad（声音弃） | `fab dial tk bad` | **#f44336** 白字 | `hearing_disabled` |
    | take ok（声音可） | `fab dial tk ok` | **#4caf50** 白字 | `gpp_good` |
    | shot 初始 | `fab dial sht` | #f2f5de | `videocam` |
    | shot ok（画面保） | `fab dial sht ok` | **#2196f3** 白字 | `movie_filter` |
    | shot nice（画面过） | `fab dial sht nice` | **#4caf50** 白字 | `thumb_up` |

  - `.dial-wrap`（371 行）：`position:relative;`。`.dial-menu`（372–376 行）：`position:absolute; bottom:64px; left:50%; translateX(-50%);`column，`gap:12px; z-index:9;`**默认 `display:none`；`.dial-wrap.open .dial-menu { display:flex; }`**（无动画）。
  - `.dial-opt`（377–385 行）：**46×46 圆形**、白图标、阴影 `0 3px 10px rgba(0,0,0,0.35)`；`.red #f44336` / `.green #4caf50` / `.blue #2196f3`。菜单项：
    - take 菜单：`hearing_disabled` + 标签「声音弃」（red）；`gpp_good` +「声音可」（green）。
    - shot 菜单：`movie_filter` +「画面保」（blue）；`thumb_up` +「画面过」（green）。
  - `.dial-label`（386–390 行）：`position:absolute; right:56px; top:50%; translateY(-50%); white-space:nowrap; background:rgba(0,0,0,0.78); color:#fff; font-size:12px; padding:4px 9px; border-radius:6px;`（标签浮在按钮左侧）。
  - 互斥逻辑：点 `#btn-take` 切换 take-dial 的 `.open` 并收起 shot-dial；点 `#btn-shot` 反之。菜单项点击后：写 `tkStatus/shtStatus`、换 FAB 类与图标、收菜单。**注意：状态仅为前端演示，不写入场记历史**。
- `.lock-switch`（393–407 行）：`display:flex; align-items:center; border:1px solid rgba(0,0,0,0.18); border-radius:22px; overflow:hidden; background:#fff; cursor:pointer; height:44px;`。
  - `.lock-opt`：`width:50px; height:100%;`column 居中，`gap:1px; font-size:10px; color:#777;`图标 17px。两项：`lock_open`+「触控」（`data-v="0"`，初始 `.active`）、`lock`+「锁定」（`data-v="1"`）。
  - `.lock-opt.active`：`background:#fff; color:#212121; font-weight:500; box-shadow:0 1px 5px rgba(0,0,0,0.22); border-radius:20px;`（滑块高亮）。
  - 行为（app.js 449–455）：点击任一 `.lock-opt` → `isLocked = dataset.v==='1'`；切换两段 `.active`；`#lockable` 加 `.locked`（灰度滤镜，见 3.7）。

### 3.10 文件号 / 状态显示位置与联动（syncFileUI / syncRecordUI）

显示“文件号”的 5 处 + 数据源（`PREFIX='230522'`、`LINKER='-T'`、`recCount` 初始 **2**，`state.tk` 初始 1）：

| 位置 | 显示值 | 公式 |
|---|---|---|
| `#cur-file`（notecard 红点行） | `230522-T001` | `PREFIX+LINKER+pad3(recCount-1)`（当前录制 = 上一号） |
| `#rec-title`（左 field-title） | `正在录制:T001` | `'正在录制:T'+pad3(recCount-1)` |
| `#desc-input` placeholder | `230522-T001\n 录音标注...` | 同上拼接 + 换行 |
| filecounter 三卡片 | `230522` / `-T`(dim) / **`002`** | `pad3(recCount)`（下一个号） |
| `#tk-badge` | 1…20 或 `OK` | `state.tk`（onShotEnd 后为 OK） |

联动：`syncFileUI()` 批量写 `.fc-prefix/.fc-linker/.fc-num`（querySelectorAll → 主计数器与 0.52 预览计数器同步）、`#cur-file`、`#rec-title`、`#desc-input` placeholder；`syncRecordUI()` 写 `#cur-scn/#cur-sht/#cur-tk`、`#next-text`（`1A场1A镜1次`）、`#sht-title`（`S1A Sh1A Tk`）、`#tk-badge`、迷你预览滚轮 `pScene/pShot/pTake.set()`。
触发点：`onAdd`（recCount++）、`onDec`（recCount--，仅当 recCount>2）、`onShotEnd`（tk→OK）、滚轮变化。

### 3.11 双击 / 长按交互

- 记录页**长按 `.monitor-card` 600ms** → `openSheet('shot', state.scn, state.sht)`（“长按修改当前镜”，与 hint 文案一致）；`isLocked` 时忽略。实现 `bindLongPress(el, fn)`：pointerdown 起 600ms 定时器，pointerup/pointerleave/pointermove 清除。
- 计划页**双击**场次/镜头行 → `openSheet`（见 §4）。
- 场记速览：`#btn-quick` 单击 → `openQuickView()`（见 §8）。

---

## 4. 计划页规范（page-plan 由 JS 渲染）

- 渲染：`renderSchedulePage('page-plan', {isTest:false})`（`#page-plan` 在 HTML 中为空 section）。容器 `.sch`：`position:absolute; inset:0; display:flex;`左右两栏：
  - `.sch-left`（`flex:1; min-width:0;`column；`border-right:1px solid #e4e4e4;`）：上 `.list`（滚动）+ 下 `.add-scn-tile`。
  - `.sch-right`（`flex:3;`column）：上 `.list`（滚动）+ 下 `.scn-info`。
- **场次行** `.scn-row`：全宽 button，`padding:8px 6px;`column 居中，`gap:3px; border-bottom:1px solid #f0f0f0; background:#fff;`；`.sel` 底 `var(--sel-scn)(#d1c4e9)`。内容：`.avatar`（**40×40 圆形**、底 `#e3e7ea`、`#222`、15px/500、阴影 `0 1px 3px rgba(0,0,0,0.15)`，显示名称如 `1A`）+ `.type-tag`（10px / `#555` / `line-height:1.2`，如 `万星园`）。点击：选中该场、重置镜选择；**双击**：`openSheet('scene', i)`。
- **镜头行** `.sht-row`：`padding:8px 10px;`flex，`gap:10px; border-bottom:1px solid #f0f0f0;`；`.sel` 底 `#e0e0e0`。内容：`.avatar`（镜名 `1A/2B/3C`）+ `.sht-body`：
  - `.chip-row`（横向滚动、隐藏滚动条）：**objects 人物 chips** —— `.chip`：底 `var(--purple-300)(#ba68c8)`、白字、圆角 **20px**、`padding:2px 9px; font-size:12px; nowrap`（如 `缪尔赛斯`）。
  - `.sht-type`：13px / `#333` / `margin-top:3px`，`"${type},"`（如 `近景,`）。
  - `.sht-append`：12px / `#666` / `line-height:1.4`，如 `小插曲`。
  - 点击：仅选中；**双击**：`openSheet('shot', selScn, j)`。
- **底部信息条** `.scn-info`：`flex:none; background:var(--purple-50)(#f3e5f5); cursor:pointer; padding:10px 12px;`flex `gap:10px`；`border-top:1px solid #ead3ee;`。内：`.name-chip`（底 `#e1bee7`、圆角 8、`padding:6px 9px`、13.5px/700，`1A场`）+ `.info-append`（11.5px / `#757575` / lh 1.4，场概要）+ `.info-type`（11.5px / `#555`、下划线 `text-underline-offset:3px`，`场景：万星园`）。**单击** → `openSheet('scene', selScn)`。
- **`.add-scn-tile`**（左列底部）：`flex:none;`全宽 button，居中，`padding:10px 0; color:#666;`，`add` 图标 26px / `#444` + 文字 `场次+` 12px；**单击** → `openSheet('sceneOne', SCENES.length-1)`。
- **右下浮动 FAB** `.sch-fab`：`position:absolute; bottom:96px; right:-14px; z-index:8;`flex `gap:6px`，底 `#e6e9ec`、`#1c1c1c`、13px、`padding:10px 16px; border-radius:26px;`阴影 `0 3px 10px rgba(0,0,0,0.3)`；`.low` 变体 `bottom:-12px`（溢出到底部导航区）；`:active` `scale(0.97)`。计划页两个：`add_business`「镜头+」（bottom 96px）与 `read_more`「导入」（.low）；点击仅 toast 演示。
- 双击开 sheet 数据流：`openSheet(mode, scnIdx, shtIdx)` 从 `SCENES[scnIdx]` / `.shots[shtIdx]` 取 `key/fix/note{objects,type,append}` 填充表单（见 §8 细节）。

---

## 5. 场记页规范（page-log）

- 结构：`.log-appbar` + `.log-scroll`（+`#btn-share-log` share-fab）。注意本页 `.page` 是 flex column（见 2.2）。
- `.log-appbar`（484–489 行）：`flex:none; height:52px; background:#fafafa;`flex 居中，`border-bottom:1px solid #e0e0e0; color:#333;`。左右各一 40×40 `.icon-btn`（`arrow_back_ios` `#log-prev` / `arrow_forward_ios` `#log-next`），中间 `.log-tabs`。
- `.log-tabs` / `.log-tab`（490–499 行）：tabs 容器 `flex:1; align-items:flex-end; overflow-x:auto; height:100%;`隐藏滚动条。`.log-tab`：`height:100%; padding:0 14px; font-size:13px; color:#222;`居中，`border-bottom:3px solid transparent;`；`.active`：`color:#2196f3; font-weight:500; border-bottom-color:#2196f3;`。**日期 Tab 内容由 `renderLogTabs()` 从 `DATES=['260821','260820','260819']`（yymmdd）生成**，active 索引 `logDateIdx`；prev/next 按钮把 `logDateIdx` 在 0..len-1 内加减并重渲染 tabs+列表。
- `.log-scroll`（501 行）：`flex:1; overflow-y:auto; padding-bottom:84px;`（给 share-fab 留位）。
- 列表结构（`renderLogs()`，两层 ExpansionTile，默认 Scene 1 与其 Shot 1 展开）：
  - `.log-scn`（场分组）：底 `#e0e0e0`、`margin:4px 0; overflow:hidden;`。`.grp-head`：全宽 button、`padding:7px 0;`column 居中 `gap:1px;`；`.grp-title` 15px/500（如 `Scene 1`）+ `.grp-sub` 11px/`#444`（固定「场」）+ `.grp-chev`（`expand_more`、`margin-left:auto; color:#666; font-size:20px; transition:transform .18s;`，`.grp-open` 旋转 180°）。
  - `.log-sht`（镜分组）：底 `#eeeeee`、`margin:2px 4px; overflow:hidden;`。`.grp-head`：`padding:6px 12px;`flex `gap:8px;`；`.grp-title` 14px/500（`Shot 1`）+ `.grp-sub` 11px/`#555`（「镜」）。
  - 展开/收起：点 `.grp-head` 切换 `.grp-open` 并 `body.hidden = !hidden`。
  - `.log-item`（524–537 行）：flex `gap:10px; padding:10px 12px; cursor:pointer; border-bottom:1px solid rgba(0,0,0,0.06);`（末项无 border）。行底色按声带状态：`log-bg-grey #9e9e9e`（notChecked）/ `log-bg-green #4caf50`（ok）/ `log-bg-red #f44336`（bad）——`TK_BG` 映射。**注意：行底色为实色，其上文字/图标均为深色，对比度由源码如此设计**。
  - 行内容：`.avatar`（**36×36**、字号 13、`background:rgba(255,255,255,0.85)`，显示 `it.tk` 序号）+ `.li-body`：
    - `.li-title`：14px/700/`#000` Roboto，`fileName = prefix+linker+pad3(num)`（如 `230522-T001`）。
    - `.li-line`：12px / `#1c1c1c` / `line-height:1.45` / `word-break:break-all`，依次 `TK Note: …`、`Shot Note: …`、`tracks:a,b`（仅当 tracks 非空）、`Scene Note: …`。
    - `.li-trail`：22px / `#212121`，按 `SHT_ICONS`：`check_circle`(ok) / `thumb_up_alt`(nice) / `check_box_outline_blank`(notChecked)。
- `#btn-share-log`：`.fab.share-fab`（539 行）：`position:absolute; right:20px; bottom:26px; z-index:8; background:#2196f3;`图标 `share`；点击 toast「导出场记 JSON（演示：Share）」。
- **编辑弹层**：场记列表项本身无编辑弹层（HTML 演示中 `.log-item` 无点击行为）；“编辑”仅由记录页/计划页的 `openSheet`（§8）承担；设置页清空有确认弹窗（§7）。

---

## 6. 识别测试页（page-test）

- 用**同一个** `renderSchedulePage('page-test', {isTest:true})` 渲染（app.js 860 行），结构与计划页完全一致（同样两栏场景/镜头列表、选中高亮、双击 sheet）。
- 唯一差异（`opts.isTest` 分支，582–595 行）：右下只挂 **1 个** FAB（非 `.low`，`bottom:96px`）：`add_business` 图标 + 文字「读取场记」；点击仅 toast「读取场记 CSV（演示：file_picker）」。无「镜头+/导入」按钮。
- 就是“复刻 scene_schedule_page_test.dart”的两栏列表演示页，无独立测试逻辑。

---

## 7. 设置页

- 结构：`.screen#screen-settings`（542–546 行）：`position:absolute; inset:0; z-index:45; background:var(--scaffold); display:none; flex-direction:column;`，`.open` → `display:flex;`。打开：主 AppBar 齿轮 `#btn-settings`（add open）；关闭：`.appbar.sub` 内 `#btn-back-settings`（remove open）。**无过渡动画**。
- 头部：`.appbar.sub`（背景 `#1d4f6e`，规格同 §2.1），标题「VoiSlate 设置」。
- `.settings-body`（547 行）：`overflow-y:auto; padding:8px 4px 24px;`。
- `.set-row`（548–551 行）：`display:flex; align-items:center; justify-content:space-between; padding:12px 16px; border-bottom:1px solid #ececec; min-height:56px;`。`.set-label` 15px。三行：
  1. **工程名**：`#project-name`（`value="NewProject"`）——右侧文本输入框，无边框、`border-bottom:1px dashed #bbb;`、`width:200px; text-align:right; font-size:15px; color:#333;`。`input` 事件 → toast `工程名：<值>`。
  2. **操作模式**：`#op-mode` select，选项 `左手/右手/中间`；右对齐、无边框、15px/`#333`，用双三角渐变背景画下拉箭头（`linear-gradient(45deg,…,transparent 50%,#666 50%) + linear-gradient(135deg,#666 50%,transparent 50%)`，5×5px 定位右下）。
  3. **音量键控制**：`.switch-widget#sw-volume`（566–576 行）：**46×26px、圆角 13px、底 `#c9c9c9`、`transition:background .2s`**；`.knob`：22×22 白圆、`top:2px; left:2px;`、阴影 `0 1px 3px rgba(0,0,0,0.3)`、`transition:left .2s`；`.on`：底 `var(--primary)(#266489)`、knob `left:22px`。初始 `on`；点击切换 `.on`。
- `.set-actions`（577 行）：`padding:10px 8px;`column `stretch`。四个 `.txt-btn`（578–585 行）：15px、`padding:13px 0; min-height:48px; border-radius:6px;`hover `rgba(0,0,0,0.04)`：
  - `#btn-export` `.txt-btn.blue`（`#1e88e5`）「导出所有场记」→ toast 演示。
  - 三个 `.txt-btn.red`（`#f44336`），带 `data-confirm`：`clearToday`「清空今日场记」、`clearAll`「清空所有场记」、`clearPlan`「清空所有拍摄计划」。
- **confirm 流程**（app.js 669–703）：点 data-confirm 按钮 → 查 `CONFIRM_MAP` 填 `#confirm-title/#confirm-body/#confirm-ok`（`clearToday`：清除场记 / 是否确认要清除场记？ / 确认；`clearAll`：重置场记 / …之后需手动重启App / 确认；`clearPlan`：重置拍摄计划 / …之后需手动重启App / **清空拍摄计划**），仅 `clearPlan` 给确认钮加 `.red`；开 `#overlay-confirm`。取消/确定关弹窗，确定后 toast（clearPlan 文案带“需手动重启App”）；点击蒙层空白处（`e.target===e.currentTarget`）也可关闭。

---

## 8. 弹层 / Toast

### 8.1 `.overlay`（588–595 行）

- `position:absolute; inset:0; z-index:50; background:rgba(0,0,0,0.42); display:none;`；`.open` → `display:flex`。**开合是纯 display 切换：无淡入/蒙层动画（CSS 无 transition/keyframes）**。
- `.overlay.centered`：`align-items:center; justify-content:center; padding:24px;`（quick 速览、confirm）。
- `.overlay.bottom`：`align-items:flex-end;`（info sheet）。
- 关闭：quick/sheet 的蒙层自身点击（target===currentTarget）关闭。

### 8.2 `.dialog`（597–611 行）

- `background:#fff; border-radius:28px; padding:22px 24px 16px; width:100%; max-width:320px; box-shadow:0 24px 60px rgba(0,0,0,0.4);`。
- `.dialog-title`：21px/500/`margin-bottom:12px`（如「场记速览」/ 动态确认标题）。
- `.dialog-body`：14px / `#444` / `line-height:1.6`。
- `.dialog-actions`：`justify-content:flex-end; gap:8px; margin-top:18px;`。`.dbtn`：14px、`padding:8px 12px; border-radius:6px;`hover `rgba(0,0,0,0.05)`；`.dbtn.red` 文字 `#f44336`。
- 场记速览表 `.quick-table`（613–623 行 + `openQuickView`）：`max-height:420px; overflow-y:auto; border-radius:8px;`。`.qt-head`（sticky top）底 `#e1bee7`、13px/600、`padding:8px 12px`，两列头 **File Name | Note**。`.qt-row`：12.5px、`padding:8px 12px`；`.zebra` 底 `#eeeeee`（奇数行）；`.prompt`（末行）底 `#bbdefb`，内容 = 下一文件号 + 「等待输入...」；首列宽 46%、`word-break:break-all`。空态 `.qt-empty`：居中 `padding:26px 0; color:#666;`「尚未开始记录」。数据：`state.history` 末 40 条 → file 列 `PREFIX+LINKER+pad3(max(1, recCount - n + i))`、note 列 `S{scn} Sh{sht} Tk{tk}`。

### 8.3 `.sheet`（NoteEditor 底部信息修改，626–675 行）

- `.sheet`：`width:100%; height:54%;`白底、上圆角 **26px**、`padding:10px 18px 20px; overflow-y:auto; box-shadow:0 -10px 40px rgba(0,0,0,0.35);`。
- `.sheet-grip`：**40×4px、圆角 2、`#d5d5d5`、`margin:2px auto 12px`**。`.sheet-title`：20px/700/`margin-bottom:14px`（场次信息修改 / 镜头信息修改）。
- `.sheet-row`（key/fix 选择）：flex `gap:6px; margin-bottom:14px;`：两个 select（`#ed-key`：1–200；`#ed-fix`：空（显示「（无）」）+ A–Z，26 项）+ `.sheet-unit`（15px，场/镜）。
- `.sheet-sec`：column `gap:8px; margin-bottom:14px;`；`b` 14px；input/textarea：`border:1px solid #ccc; border-radius:6px; padding:8px 10px; font-size:14px; outline:none; width:100%;`focus 边框 `var(--primary)`。
  - **录音轨道** `#ed-chips`：`.chips`（flex wrap `gap:6px`）。`.chip-tag`：inline-flex `gap:4px`、底 `#ececec`、边框 `#d5d5d5`、圆角 **16px**、`padding:4px 10px; font-size:13px;`；`.x`（close 图标）15px/`#999`，hover `#e53935`，点击删除该 chip 并重渲染。`.chip-add`（`+`）：虚线边框 `#bbb`、圆角 16、宽 **30px**、`#666`，点击触发 **浏览器原生 `window.prompt('新增录音轨道：')`**（Avalonia 需替换为自绘输入框），确认后 push + 重渲染。
  - **类型编辑**：场次模式显示单行文本输入 `#ed-type`（标签「场地:」，值 = 场景 type）；镜头模式隐藏输入、显示 `.toggle-btns#ed-type-toggles`（标签「镜头类型:」）：`.tb-btn`（特写/近景/中景/全景/远景 五选一）——`border:1px solid #ccc; border-radius:6px; padding:7px 14px; font-size:13px; color:#333;`；`.on`：底 `#e1bee7`、边框 `#ba68c8`、weight 600。
  - **概要/内容** `#ed-append`：textarea rows=2。
- `.sheet-actions`（`space-evenly; margin-top:18px; padding:6px 0 2px;`）：`.sbtn`（gap:4px、边框 `#ccc`、底 `#f4f5f7`、圆角 8、13.5px、`padding:10px 14px`、`:active scale(0.97)`）：`arrow_upward`「向前添加」`#ed-prev`、`.sbtn.primary`（底/边框 `var(--primary)`、白字）「保存」`#ed-save`、`#ed-next`「向后添加」`arrow_downward`。`sceneOne` 模式隐藏 prev/next。
- 数据流：`openSheet(mode, scnIdx, shtIdx)`（app.js 732–838）——`mode` 以 `scene` 开头视为场次（`isScene`）；`sceneOne` 为“新增场次”（`blankShot = {key:'1', fix:'A', note:{type:'近景', append:'', objects:[]}}` 兜底）。场次：`key=infoKey || name[0]`、`fix=infoFix || name.slice(1)`；镜头：`key=name[0]`、`fix=name.slice(1)`。无实际保存——`#ed-save/#ed-prev/#ed-next` 用 `.onclick` 覆盖，点击即关弹层 + 演示 toast。

### 8.4 `.toast`（678–685 行 + showToast）

- `position:absolute; left:50%; bottom:84px; transform:translateX(-50%) translateY(12px); background:rgba(0,0,0,0.82); color:#fff; font-size:13px; padding:9px 16px; border-radius:8px; z-index:70; opacity:0; pointer-events:none; transition:opacity .22s, transform .22s; max-width:86%; text-align:center;`。
- `.toast.show`：`opacity:1; transform:translateX(-50%) translateY(0);`（上移 12px + 淡入，0.22s）。
- 逻辑（app.js 72–79）：`showToast(msg, ms=1800)` 设置文本、加 `.show`、清除旧定时器、`ms` 毫秒后移除。**默认 1800ms**；摇杆按下 toast 为 900ms。

---

## 9. app.js 数据与状态

### 9.1 常量与状态对象

```js
const PREFIX = '230522';        // RecordFileNum.today
const LINKER = '-T';            // recordLinker 默认
const TAKE_MAX = 20;
const state = { scn: 0, sht: 0, tk: 1, history: [['1A','1A','1']] };
// 模块级：recCount=2（>=2 时 prevFileName 有值）、isLinked=true、isLocked=false、
// tkStatus='notChecked'、shtStatus='notChecked'、shotChanged=false、shotEnded=false、
// logDateIdx=0、sheetMode/scn/sht、confirmAction='clearToday'
```

### 9.2 dummy 数据（lib/data/dummy_data.dart 摘录）

- `SCENES`：2 场。
  - `1A`（type 万星园，append `三人会面，缪尔赛斯提出了她的计划，塞雷娅和克里斯滕都表示了支持。`，objects `[缪尔赛斯, 塞雷娅, 克里斯滕]`）；shots：`1A 近景 小插曲 [缪尔赛斯,塞雷娅]`、`2B 特写 两人对峙 [克里斯滕,塞雷娅]`、`3C 中景 缪尔赛斯向塞雷娅介绍生态园 [缪尔赛斯,塞雷娅]`。
  - `2A`（type 洛肯实验室，append `三人准备准备会面洛肯`，objects `[Dr, 凯尔希, 迷迭香]`）；shots 与 1A 相同三条。
- `LOG_ROWS`：`{'Scene 1': {'Shot 1': [tk1..3（230522-T001..003，okTk ok / okSht ok，tracks 各异）, 'Shot 2': [tk4,5]}, 'Scene 2': {'Shot 2': [tk2..4（okTk bad / okSht nice，Prefix 230522）, 'Shot 1': [tk3（Prefix 3 / Linker 3, okTk notChecked, okSht ok）]}}`——条目字段：`{tk, prefix, linker, num, tkNote, shtNote, tracks[], scnNote, okTk, okSht}`。
- `DATES = ['260821','260820','260819']`（yymmdd，`RecordFileNum.today` 风格）。
- `SHT_ICONS = { notChecked:'check_box_outline_blank', ok:'check_circle', nice:'thumb_up_alt' }`；`TK_BG = { ok:'log-bg-green', bad:'log-bg-red', notChecked:'log-bg-grey' }`。

### 9.3 渲染函数清单（每个一句话）

| 函数 | 作用 |
|---|---|
| `syncRecordUI()` | 把 `state` 写满记录页所有文本（cur-scn/sht/tk、next-text、sht-title、tk-badge、预览滚轮位置） |
| `syncFileUI()` | 把 `PREFIX/LINKER/pad3(recCount)` 写满两处 filecounter、cur-file、rec-title、desc 占位符 |
| `setShotHint(show)` | 显示/隐藏「长按修改当前镜」提示 |
| `onWheelChange(ev)` | 滚轮变化 → 更新 state（scene 时重建 shot 列）→ syncRecordUI |
| `onAdd()` | “+” 按钮：push 历史（上限 40）、recCount++、tk+1（≤20） |
| `onDec()` / `onDecUp()` | 600ms 长按撤回：tk/recCount 递减 + toast；抬起只 toast 提示 |
| `onShotEnd()` | “保存（镜头结束）”：历史 push `OK`、tk-badge=OK、清空输入框、toast |
| `onFakeTake()` | Fake Take：清 desc 输入框、占位符「这条跑了」 |
| `bindLongPress(el, fn)` | 600ms 长按工具（pointerdown 计时，up/leave/move 取消） |
| `renderFileCounter(target, scale=1)` | 渲染三卡片文件号（支持 scale 变体） |
| `setupRecordPage()` | 建主/迷你滚轮、两处计数器、绑 NEXT 头/link-pill/+/-/save/长按/双拨盘/lock/速览/摇杆事件 |
| `setupJoystick()` | 摇杆拖拽 + 阈值判断 + 回弹 |
| `renderSchedulePage(parentId, opts)` | 计划页/测试页两栏列表 + add-scn-tile + scn-info + FAB（isTest 分支） |
| `renderLogTabs()` / `renderLogs()` | 日期 Tab 与两层级联列表渲染（含默认展开） |
| `setupSettings()` | 设置页开关/输入/导出/四个 data-confirm → 确认弹窗流 |
| `openQuickView()` | 填充速览表并开 `#overlay-quick` |
| `openSheet(mode, scnIdx, shtIdx)` | 填充并打开底部信息修改 sheet |
| `showToast(msg, ms=1800)` | 全局 toast |
| `setupNav()` | 底部导航切换、`.page.active` 切换 |
| `init()` | DOMContentLoaded 后装配全部页面与事件 |

（滚轮内部：`Wheel` 类 `rebuild/_down/set/layout`；`buildPicker` 组装三列+分隔符。）

---

## 10. 交互行为清单（app.js 事件绑定总表）

| 元素 / 事件 | 处理器 | 动作 | 影响渲染 / 参数 |
|---|---|---|---|
| `.nav-item` click | `setupNav` | 切 active、按 data-tab 切换 `.page.active` | 4 页 display 切换（无动画） |
| `#btn-settings` / `#btn-back-settings` click | `setupSettings` | 开/关 `.screen.open` | 设置页 display:flex（无动画） |
| `#next-head` click | 内联（367） | 切换 `.tile.open`；`#next-body/#next-text` 显隐与 preview 互换 | chev 旋转 `transition:transform .2s` |
| `#link-pill` click | 内联（377） | `isLinked` 翻转；pill/monitor-card 灰化；NEXT↔补录标签；toast | `#preview-counter.hidden = !isLinked || !body.hidden` 等 |
| `.wheel` pointerdown/move/up | `Wheel._down` | 拖选 / 点击下一项 | 列表 `translateY` `transition .16s ease-out`（拖拽中禁用）；4px 移动阈值；50px/格 |
| `#btn-add` click | `onAdd` | 历史 push、recCount++、tk++（≤20）、提示关 | `syncFileUI/syncRecordUI` |
| `#btn-fake` click | `onFakeTake` | 清输入 + 占位符「这条跑了」 | — |
| `#btn-dec` pointerdown/up/leave | `onDec/onDecUp` | **600ms 长按**撤回（tk--、recCount-- 当 >2）+ toast；抬起提示 | `syncFileUI/syncRecordUI` |
| `#btn-save` click | `onShotEnd` | 镜头结束：历史 push `OK`、tk 显示 OK、清空 desc、toast | `#tk-badge` 加 `.ok` 绿底 |
| `#monitor-card` 长按 | `bindLongPress` | **600ms** 后 `openSheet('shot', scn, sht)`（isLocked 时忽略） | 弹底部 sheet |
| `#btn-take`/`#btn-shot` click | 内联 | 互斥开/关各自 dial-menu `.open` | 显隐（无动画） |
| take 菜单 red/green、shot 菜单 blue/green click | 内联 | 写 `tkStatus/shtStatus`，换 FAB 底色/图标，收菜单 | FAB `transition:background .2s` |
| `#lock-switch` click | 内联（449） | `isLocked` 翻段、两段 `.active` 互换 | `#lockable.locked`：`filter:grayscale(1); opacity:.9`（`transition:filter .2s`），禁 `.add-btn/.mini-btn/.fields/.dial-wrap/.joy` |
| `#btn-quick` click | `openQuickView` | 填充速览表、开 `#overlay-quick` | — |
| `#joy-knob` pointerdown / `#joy` pointermove / knob up·cancel | `setupJoystick` | knob 拖动 5..55px；释放按 **12.5px（25% of reach=50）** 阈值判定左右写入 `#note-input/#desc-input` + toast；回弹 `left .2s ease` 至 32px | 常驻 `scale(0.8)` 不参与交互动画 |
| `.scn-row` click / dblclick | renderSchedulePage 内联 | 单选场、双击 `openSheet('scene', i)` | `.scn-row.sel` 底 `#d1c4e9` |
| `.sht-row` click / dblclick | 同上 | 单选镜、双击 `openSheet('shot', selScn, j)` | `.sht-row.sel` 底 `#e0e0e0` |
| `.scn-info` click | 同上 | `openSheet('scene', selScn)` | — |
| `.add-scn-tile` click | 同上 | `openSheet('sceneOne', SCENES.length-1)` | prev/next 隐藏 |
| plan/test FAB click | 同上 | toast 演示（镜头+/导入/读取场记 CSV） | — |
| `.log-tab` click | `renderLogTabs` | `logDateIdx` 更新、重渲染 tabs+列表 | `.log-tab.active` 蓝 3px 下边 |
| `#log-prev`/`#log-next` click | init 内联 | 索引 ±1（clamp 0..len-1）后重渲染 | — |
| `.grp-head` click | `renderLogs` 内联 | `.grp-open` 切换、`body.hidden` | chev 旋转 `transition:transform .18s` |
| `#btn-share-log` click | init 内联 | toast 导出演示 | — |
| `#sw-volume` click | `setupSettings` | `.on` 切换 | `background .2s`、knob `left .2s` |
| `#project-name` input | `setupSettings` | toast `工程名：…` | — |
| `#btn-export` click | `setupSettings` | toast 导出 JSON 演示 | — |
| `.txt-btn[data-confirm]` click | `setupSettings` | 填 `CONFIRM_MAP` 文案、开确认弹窗 | clearPlan 时确认钮 `.red` |
| `#confirm-cancel` / `#confirm-ok` click | `setupSettings` | 关弹窗；ok 另发 toast | — |
| `#overlay-quick` / `#overlay-sheet`（蒙层） click | init 内联（877） | `target===currentTarget` 时关闭 | — |
| `#overlay-confirm`（蒙层） click | `setupSettings`（701） | 同上关闭 | — |

**动画参数汇总（ms）**：toast 显隐 `0.22s`（默认时长 1800ms，摇杆按下 900ms）；滚轮列表 `0.16s ease-out`；chev `0.2s`、grp-chev `0.18s`；lockable 灰度 `0.2s`；fab.dial 底色 `0.2s`；switch 底/knob `0.2s`；摇杆回弹 `0.2s ease`；长按判定（撤回/改镜）`600ms`；按钮按压 `scale(0.95~0.99)` 即时无过渡。