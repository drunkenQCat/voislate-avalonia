using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using VoiSlate.Desktop.Speech;

namespace VoiSlate.Desktop;

public sealed partial class DesktopWorkspaceView
{
    private readonly BailianSettingsStore _speechSettings;
    private readonly BailianRealtimeClient _speechClient;
    private readonly Func<int, IPcmCapture> _captureFactory;
    private bool _joyGesturesAttached;
    private Control? _joyArea;
    private Control? _joyKnob;
    private IPointer? _joyPointer;

    /// <summary>摇杆手势目标是否已装填（只有记录页为真；测试用来钉住「切页后不能拿旧控件做命中判定」）。</summary>
    internal bool VoiceGestureArmed => _joyArea is not null;
    private Action<double>? _joyPress;
    private Action<double>? _joyMove;
    private Func<Task>? _joyRelease;
    private bool _micPermissionGranted;

    /// <summary>
    /// 摇杆手势统一挂在视图层（Tunnel + handledEventsToo），而不是挂在话筒按钮上。
    /// 真机上摇杆位于 ScrollViewer 内，ScrollViewer 的手势识别器会先把 PointerPressed 标成 Handled
    /// 并抢走指针捕获；按钮上的处理器默认 handledEventsToo:false，因此**鼠标在桌面能滑、手指在真机上滑不动**。
    /// 视图是 ScrollViewer 的祖先，任何指针事件的路由都会经过它，所以在这里接管必定收得到，
    /// 同时把 Handled 置真，ScrollViewer 就不会把这次拖动当成翻页。
    /// </summary>
    private void AttachJoyGestures()
    {
        if (_joyGesturesAttached) return;
        _joyGesturesAttached = true;
        AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (_joyPress is null || _joyArea is not { } area) return;
            if (_joyActive || _joyKnob is not { } handle) return;
            if (e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(area).Properties.IsLeftButtonPressed) return;
            // 命中判定必须用「相对视图」的坐标 + TranslatePoint：
            // 切到计划页/时间轴后 _joyArea 可能还指向上一页那个已经脱离可视树的摇杆，
            // 此时 area.IsEffectivelyVisible 仍是 true（无父级时按 true 算），而 e.GetPosition(area)
            // 对不在树上的控件返回 (0,0) → 位置检查全过 → **每一次点击都被当成按了摇杆**，
            // 于是随便点一下就开始录一嗓子（真机表现为「整个界面卡死」）。
            if (!InsideJoy(e.GetPosition(this))) return;
            var hit = e.GetPosition(handle);
            if (!new Rect(handle.Bounds.Size).Contains(hit)) return;
            var p = e.GetPosition(area);
            _joyActive = true; // 只有真正落在摇杆上的这次按下才算摇杆手势
            _joyPointer = e.Pointer;
            e.Handled = true;
            // 触摸必须显式捕获：Avalonia 的触摸指针不会因为「谁处理了按下」就自动把后续移动送给谁，
            // 不捕获的话 PointerMoved 一次都收不到（鼠标有隐式捕获，所以桌面/headless 测试永远发现不了）。
            e.Pointer.Capture(area);
            _joyPress(p.X);
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.PointerMovedEvent, (_, e) =>
        {
            if (!_joyActive || e.Pointer != _joyPointer || _joyMove is null || _joyArea is not { } area) return;
            _joyMove(e.GetPosition(area).X);
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.PointerReleasedEvent, (_, e) =>
        {
            // 关键：这条处理器挂在视图上、Tunnel + handledEventsToo，会收到**全页每一次**松手。
            // 原来无条件 Capture(null) 会在 Button 自己收到 PointerReleased 之前就把捕获摘掉，
            // 于是 Button 立刻 PointerCaptureLost → IsPressed=false → 松手不再触发 Click，
            // 表现为「整个界面所有按钮都点不动」（真机与 headless 同时复现）。
            if (!_joyActive || e.Pointer != _joyPointer) return;
            if (e.Pointer.Type == PointerType.Mouse && e.InitialPressMouseButton != MouseButton.Left) return;
            if (_joyArea is { } area) _joyMove?.Invoke(e.GetPosition(area).X);
            _joyActive = false;
            _joyPointer = null;
            e.Pointer.Capture(null);
            if (_joyRelease is not null) _ = _joyRelease();
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
    }
    private Control BuildVoice()
    {
        var preview = T(_voiceCaptureAvailable
            ? "按住话筒，左滑录音标注 / 右滑镜头标注，松手提交"
            : "语音标注需要本机麦克风采集，本平台暂未接入；可直接在下面两个输入框手动记录", 11, "#83919C");
        AutomationProperties.SetAutomationId(preview, "voice-preview");
        // Flutter call site: 120 x 70, scaled to 80%; 50 units of travel.
        const double halfTravel = 25;
        var joy = Box(new Grid(), "#EF9A9A", 5, radius: 70);
        joy.Width = 120; joy.Height = 70;
        var track = (Grid)joy.Child!;
        var arrows = T("▸  ◂", 28, "#A5D6A7");
        arrows.HorizontalAlignment = HorizontalAlignment.Center;
        arrows.VerticalAlignment = VerticalAlignment.Center;
        arrows.IsHitTestVisible = false; track.Children.Add(arrows);
        var knob = B("开始语音标注", () => { }, "purple", "voice-joystick");
        Control Mic() => new PathIcon { Data = Geometry.Parse("M12,14C13.66,14 15,12.66 15,11V5C15,3.34 13.66,2 12,2C10.34,2 9,3.34 9,5V11C9,12.66 10.34,14 12,14 M18,11C18,14.53 15.06,17.44 11.53,17.5C8.08,17.56 5,14.93 5,11H3.13C3.13,14.66 6.31,17.66 10,17.9V21H14V17.9C17.69,17.66 20.87,14.66 20.87,11H18"), Width = 25, Height = 25, Foreground = Brushes.White };
        knob.Content = Mic(); knob.Width = 60; knob.Height = 60; knob.CornerRadius = new CornerRadius(30);
        knob.HorizontalAlignment = HorizontalAlignment.Center; knob.VerticalAlignment = VerticalAlignment.Center;
        knob.Padding = new Thickness(0); knob.Background = Brush("#F3E5F5"); track.Children.Add(knob);
        // Fluent Button transitions must not interpolate a pointer-driven transform.
        knob.Transitions = new Avalonia.Animation.Transitions();
        if (!_voiceCaptureAvailable) knob.Opacity = 0.45;
        BailianMicrophoneSession? session = null;
        bool active = false, processing = false; int generation = 0; double start = 0; string? target = null;
        string beforeRec = "", beforeShot = "", scene = "", shot = "";
        var limit = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        DispatcherTimer? spring = null;
        void StopSpring() { spring?.Stop(); spring = null; }
        void Paint(double delta)
        {
            knob.RenderTransform = new TranslateTransform(Math.Clamp(delta, -halfTravel, halfTravel), 0);
            var progress = Math.Clamp((delta + halfTravel) / (halfTravel * 2), 0, 1);
            var red = Color.Parse("#EF9A9A"); var green = Color.Parse("#A5D6A7");
            byte Blend(byte a, byte b) => (byte)Math.Round(a + (b - a) * progress);
            joy.Background = Math.Abs(delta) < 0.001 ? Brushes.Transparent
                : new SolidColorBrush(Color.FromRgb(Blend(red.R, green.R), Blend(red.G, green.G), Blend(red.B, green.B)));
        }
        void Reset()
        {
            var from = knob.RenderTransform?.Value.M31 ?? 0;
            active = processing = false; limit.Stop(); knob.Content = Mic();
            knob.Background = Brush("#F3E5F5"); knob.Opacity = _voiceCaptureAvailable ? 1 : 0.45;
            SpringBack(from);
        }
        // 松手回弹：沿用 Flutter 原版 AnimatedPositioned 的 200ms + Curves.easeInExpo（起步慢、收尾快）。
        // 用逐帧 DispatcherTimer 而不是 Transitions，是为了拖动期间零延迟（挂了过渡滑块会追不上手指）。
        void SpringBack(double from)
        {
            StopSpring();
            if (Math.Abs(from) < 0.5) { Paint(0); return; }
            const int durationMs = 200, frameMs = 16;
            var elapsed = 0;
            var ticker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(frameMs) };
            spring = ticker;
            ticker.Tick += (_, _) =>
            {
                elapsed += frameMs;
                var t = Math.Min(1d, elapsed / (double)durationMs);
                var eased = t <= 0 ? 0 : Math.Pow(2, 10 * (t - 1)); // Curves.easeInExpo
                Paint(from * (1 - eased));
                if (t < 1) return;
                ticker.Stop(); spring = null; Paint(0);
            };
            ticker.Start();
        }
        void Cancel()
        {
            if (!active && !processing && session is null) return;
            generation++;
            _joyActive = false; var pointer = _joyPointer; _joyPointer = null; pointer?.Capture(null);
            Reset(); var old = session; session = null;
            if (old is not null) { old.Cancel(); _ = old.DisposeAsync(); }
            preview.Text = "已取消；按住话筒重新标注";
        }
        _finishVoice = Cancel;
        async Task Begin()
        {
            Services.Diagnostics.Log($"[JOY] begin busy={_busy} avail={_voiceCaptureAvailable}");
            if (!_voiceCaptureAvailable) { preview.Text = "语音标注需要本机麦克风采集，本平台暂未接入"; return; }
            if (active || processing || _busy || _service.Draft.Ended || _service.Shot is null) return;
            StopSpring(); Paint(0);
            active = true; var current = ++generation;
            if (!_micPermissionGranted && PcmCapture.PermissionRequest is { } ask)
            {
                try { _micPermissionGranted = await ask(); }
                catch (Exception ex)
                {
                    if (current != generation) return;
                    Cancel();
                    Services.Diagnostics.Log($"[VOICE] permission {ex.GetType().Name}: {ex.Message}");
                    preview.Text = "等待麦克风授权超时，请重试"; Toast(preview.Text, true); return;
                }
                if (current != generation) return;
                if (!_micPermissionGranted)
                {
                    Cancel();
                    preview.Text = "需要麦克风权限才能语音标注，请在系统设置里允许 VoiSlate 录音";
                    Toast(preview.Text, true);
                    return;
                }
            }
            if (current != generation) return;
            beforeRec = _recNote?.Text ?? ""; beforeShot = _shotNote?.Text ?? "";
            scene = _service.State.Scene; shot = _service.State.Shot;
            preview.Text = "正在连接百炼…"; knob.Content = "■";
            try
            {
                var options = await _speechSettings.LoadAsync(); options.Validate();
                if (current != generation) return;
                session = new BailianMicrophoneSession(options, _speechClient, _captureFactory(options.DeviceNumber), text =>
                    Dispatcher.UIThread.Post(() => { if (current == generation && (active || processing)) preview.Text = text.Length == 0 ? "正在聆听…" : text; }));
                preview.Text = "正在聆听，左滑录音 / 右滑镜头；回中松手取消"; limit.Start();
                var observed = session;
                _ = ObserveFailure(observed, current);
            }
            catch (Exception ex) { Services.Diagnostics.Log($"[VOICE] begin {ex.GetType().Name}: {ex.Message}"); if (current == generation) { Cancel(); preview.Text = ex.Message; Toast(ex.Message, true); } }
        }
        async Task ObserveFailure(BailianMicrophoneSession observed, int current)
        {
            try { await observed.Completion; }
            catch (Exception ex) { Services.Diagnostics.Log($"[VOICE] observe {ex.GetType().Name}: {ex.Message}"); if (current == generation && active) { Cancel(); preview.Text = ex.Message; Toast(ex.Message, true); } }
        }
        async Task Finish()
        {
            if (!active) return;
            if (target is null || session is null) { Cancel(); return; }
            active = false; processing = true; limit.Stop(); knob.Content = "…";
            var current = generation; var completing = session; var side = target;
            preview.Text = "正在整理识别结果…";
            try
            {
                await completing.StopAudioAsync();
                var text = await completing.Completion;
                if (current != generation) return;
                var box = side == "rec" ? _recNote : _shotNote; var previous = side == "rec" ? beforeRec : beforeShot;
                if (_service.State.Scene != scene || _service.State.Shot != shot || box is null || (box.Text ?? "") != previous)
                    throw new InvalidOperationException("标注内容或镜头已变化，本次转写未写入，请重新标注");
                if (text.Length == 0) { preview.Text = "未识别到语音，请重试"; return; }
                box.Text = text;
                preview.Text = "已写入" + (side == "rec" ? "录音标注" : "镜头标注"); Toast(preview.Text);
            }
            catch (TimeoutException) { if (current == generation) { preview.Text = "录音停止超时，本次标注已放弃，请重试"; Toast(preview.Text, true); } }
            catch (Exception ex) { Services.Diagnostics.Log($"[VOICE] finish {ex.GetType().Name}: {ex.Message}"); if (current == generation) { preview.Text = ex.Message; Toast(ex.Message, true); } }
            finally { await completing.DisposeAsync(); if (current == generation) { session = null; Reset(); } }
        }
        limit.Tick += async (_, _) => await Finish();
        AttachJoyGestures();
        _joyArea = joy; _joyKnob = knob;
        joy.PointerCaptureLost += (_, _) => { if (_joyActive) Cancel(); };
        joy.DetachedFromVisualTree += (_, _) => { Cancel(); StopSpring(); limit.Stop(); };
        _joyPress = async x =>
        {
            Services.Diagnostics.Log($"[JOY] press x={x:F1} active={active} processing={processing} ended={_service.Draft.Ended}");
            if (active || processing) return;
            target = null; start = x; knob.Focus(); await Begin();
            Services.Diagnostics.Log($"[JOY] press handled active={active} session={session is not null}");
        };
        _joyMove = x =>
        {
            if (!active) return;
            var delta = x - start;
            Paint(delta);
            var next = delta < -halfTravel ? "rec" : delta > halfTravel ? "shot" : null;
            if (next == target) return;
            target = next;
            preview.Text = next is null ? "回中松手取消 · 左滑录音 / 右滑镜头" : "松手写入" + (next == "rec" ? "录音标注" : "镜头标注");
            Services.Diagnostics.Log($"[JOY] move delta={delta:F1} target={next ?? "<none>"}");
        };
        _joyRelease = async () =>
        {
            if (!active && !processing) return;
            Services.Diagnostics.Log($"[JOY] release target={target ?? "<none>"}");
            await Finish();
        };
        knob.AddHandler(InputElement.KeyDownEvent, async (_, e) =>
        {
            if (e.KeyModifiers != KeyModifiers.None) return;
            if (e.Key is Key.Left or Key.Right)
            {
                e.Handled = true;
                if (!active && !processing) await Begin();
                target = e.Key == Key.Left ? "rec" : "shot";
                if (active) preview.Text = "正在录制" + (target == "rec" ? "录音标注" : "镜头标注") + "；Enter 提交，Esc 取消";
            }
            else if (e.Key is Key.Enter or Key.Space) { e.Handled = true; if (active) await Finish(); }
            else if (e.Key == Key.Escape && (active || processing)) { e.Handled = true; Cancel(); }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        ToolTip.SetTip(knob, "按住并左右滑动选择标注，松手提交；键盘左右键开始，Enter 提交，Esc 取消");
        preview.MaxWidth = 200; preview.TextWrapping = TextWrapping.Wrap;
        return Stack(6, new Viewbox { Width = 96, Height = 56, Child = joy,
            HorizontalAlignment = HorizontalAlignment.Center }, preview);
    }
    /// <summary>
    /// Token 存储说明必须如实：Windows 走 DPAPI 是真加密，其它平台（Android/Linux）
    /// 只是 base64 混淆，能被读到本地存储的人解出 —— 不能一律说「已加密保存」。
    /// </summary>
    private string SavedNote(string suffix) =>
        (_speechSettings.TokenIsEncrypted ? "Token 已加密保存" : "Token 已保存（本平台只做混淆，未加密）") + suffix;
    private Control BuildSpeechSettings()
    {
        var token = new TextBox { PasswordChar = '●', PlaceholderText = "粘贴北京地域百炼 API Key；留空保留原 Token" };
        AutomationProperties.SetName(token, "百炼 Token");
        var workspace = new TextBox { PlaceholderText = "可选：业务空间 ID" };
        AutomationProperties.SetName(workspace, "百炼业务空间 ID");
        var microphones = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        var status = T("", 12, "#83919C");
        BailianOptions? loaded = null;
        try
        {
            var devices = PcmCapture.Devices(); microphones.ItemsSource = devices;
            loaded = _speechSettings.LoadAsync().GetAwaiter().GetResult();
            workspace.Text = loaded.WorkspaceId;
            microphones.SelectedIndex = loaded.DeviceNumber < devices.Count ? loaded.DeviceNumber : devices.Count > 0 ? 0 : -1;
            status.Text = (loaded.Token.Length > 0 ? SavedNote("；输入新值可替换。") : "尚未配置 Token。") + (devices.Count == 0 ? "未发现麦克风。" : "");
        }
        catch (Exception ex) { status.Text = ex.Message; }
        var save = B("保存语音设置", async () =>
        {
            try
            {
                await _speechSettings.SaveAsync(token.Text, workspace.Text ?? "", Math.Max(0, microphones.SelectedIndex));
                token.Text = ""; loaded = await _speechSettings.LoadAsync();
                status.Text = loaded.Token.Length > 0 ? SavedNote("，语音设置立即生效。") : "语音设置已保存；请填写 Token 后开始识别。";
            }
            catch (Exception ex) { status.Text = ex.Message; }
        });
        var test = B("测试连接", async () =>
        {
            status.Text = "正在测试连接…";
            try
            {
                var key = string.IsNullOrWhiteSpace(token.Text) ? (await _speechSettings.LoadAsync()).Token : token.Text.Trim();
                if (key.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) key = key[7..].Trim();
                await _speechClient.CheckConnectionAsync(new(key, workspace.Text?.Trim() ?? "", Math.Max(0, microphones.SelectedIndex)));
                status.Text = "连接成功；实际识别还需要模型权限、额度和可用麦克风。";
            }
            catch (Exception ex) { status.Text = ex.Message; }
        });
        var clear = B("移除 Token", async () =>
        {
            try { await _speechSettings.ClearTokenAsync(); token.Text = ""; status.Text = "Token 已移除。"; }
            catch (Exception ex) { status.Text = ex.Message; }
        }, "quiet");
        var meter = B("测试麦克风", async () =>
        {
            try
            {
                if (PcmCapture.PermissionRequest is { } ask && !await ask()) { status.Text = "没有麦克风权限，无法采集。"; return; }
                using var capture = PcmCapture.Create(Math.Max(0, microphones.SelectedIndex));
                capture.Start();
                status.Text = "正在采集 2 秒…";
                var peak = 0; var blocks = 0;
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try
                {
                    await foreach (var block in capture.ReadAsync(cts.Token))
                    {
                        blocks++;
                        for (var i = 0; i + 1 < block.Length; i += 2)
                        {
                            var sample = Math.Abs((short)(block[i] | (block[i + 1] << 8)));
                            if (sample > peak) peak = sample;
                        }
                    }
                }
                catch (OperationCanceledException) { /* 2 秒到点 */ }
                try { await capture.StopAsync(); } catch { /* 已停止 */ }
                status.Text = blocks == 0
                    ? "没有收到音频帧：麦克风可能被其它应用占用"
                    : $"麦克风正常：收到 {blocks} 帧，峰值 {peak * 100 / 32768}%";
            }
            catch (Exception ex) { status.Text = ex.Message; }
        });
        // 横向 StackPanel 在 361dp 手机上会把「移除 Token」挤出卡片，改成自动换行
        var buttons = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var button in new Control[] { save, test, meter, clear })
        {
            button.Margin = new Thickness(0, 0, 6, 6);
            buttons.Children.Add(button);
        }
        return Stack(10, T("百炼语音识别", 16, "#45654D", true),
            T("北京地域 · 实时识别；按住话筒时上传音频。" + (_speechSettings.TokenIsEncrypted
                ? "Token 由 Windows DPAPI 绑定当前用户加密保存，不包含在 JSON 导出中。"
                : "Token 只存在于本机应用数据中（仅混淆、未加密），不包含在 JSON 导出中。"), 11),
            T("Token（API Key）"), token, T("业务空间（可选）"), workspace, T("麦克风"), microphones, status, buttons);
    }
}
