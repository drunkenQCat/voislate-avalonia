using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace VoiSlate.Android;

/// <summary>
/// Android application class. Avalonia 12.1.1 requires the Avalonia <see cref="App"/> type to be
/// wired through AvaloniaAndroidApplication&lt;TApp&gt; (AvaloniaMainActivity is non-generic in 12.x).
/// </summary>
[Application]
public class AndroidApp : AvaloniaAndroidApplication<App>
{
    protected AndroidApp(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        => base.CustomizeAppBuilder(builder).WithInterFont();
}