using Android.App;
using Android.Content.PM;
using Avalonia.Android;

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
}