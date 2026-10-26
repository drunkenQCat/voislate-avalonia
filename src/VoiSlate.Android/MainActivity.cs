using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia.Android;
using VoiSlate.Services;

namespace VoiSlate.Android;

[Activity(
    Label = "VoiSlate",
    MainLauncher = true,
    Exported = true,
    Theme = "@style/Theme.AppCompat.Light.NoActionBar",
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation
        | ConfigChanges.ScreenSize
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize)]
public class MainActivity : AvaloniaMainActivity
{
    private const int RecordAudioRequest = 4201;
    private static TaskCompletionSource<bool>? _recordAudioPending;
    private static readonly AndroidHardwareKeyService HardwareKeysService = new();

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // 必须早于 Avalonia 建内容：DesktopWorkspaceView 在构造时会读 PcmCapture.Factory
        // 来决定「语音标注可用 / 降级」；音量键走静态事件，与注册时机无关。
        AndroidMic.CurrentActivity = this;
        AndroidMic.Register();
        base.OnCreate(savedInstanceState);
    }

    // 音量键必须在 DispatchKeyEvent 拦：Avalonia 的 Android 视图会先消费按键，
    // 只重写 OnKeyDown 收不到（真机实测：日志里 onKeyDown 一次都没触发）。
    public override bool DispatchKeyEvent(KeyEvent? e)
    {
        if (e is not null && e.Action == KeyEventActions.Down && HardwareKeysService.Handle(e.KeyCode)) return true;
        return base.DispatchKeyEvent(e);
    }

    /// <summary>申请 RECORD_AUDIO，等用户点完系统弹窗（最多 60s）后返回是否授权。</summary>
    internal static Task<bool> RequestRecordAudioAsync()
    {
        var activity = AndroidMic.CurrentActivity;
        if (activity is null) return Task.FromResult(false);
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _recordAudioPending = pending;
        activity.RequestPermissions(new[] { global::Android.Manifest.Permission.RecordAudio }, RecordAudioRequest);
        return pending.Task.WaitAsync(TimeSpan.FromSeconds(60));
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode != RecordAudioRequest) return;
        var granted = grantResults.Length > 0 && grantResults[0] == Permission.Granted;
        _recordAudioPending?.TrySetResult(granted);
        _recordAudioPending = null;
    }
}