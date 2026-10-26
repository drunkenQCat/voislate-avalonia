using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Xunit;

namespace VoiSlate.Tests.Desktop;

public sealed partial class DesktopWorkspaceTests
{
    [WindowsOnlyFact]
    public Task Ui_joystick_track_does_not_start_recording() => RunUi(async () =>
    {
        using var f = await Create(); await SpeechSettings(f).SaveAsync("sk-ui-test-only", "", 0);
        var capture = new UiCapture(); var window = await WindowFor(f, new UiTransport(), capture);
        try
        {
            var knob = Control<Button>(window, "voice-joystick");
            var center = knob.TranslatePoint(new Point(30, 30), window)!.Value;
            // Visible track, outside the 48px ball but within the 96px track.
            var track = center + new Vector(36, 0);
            window.MouseDown(track, MouseButton.Left); window.MouseUp(track, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.False(capture.Started);
            Assert.Equal("record", window.View.CurrentPage);
        }
        finally { window.Close(); }
    });

    [WindowsOnlyFact]
    public Task Ui_joystick_return_from_edge_cancels_and_animates_home() => RunUi(async () =>
    {
        using var f = await Create(); await SpeechSettings(f).SaveAsync("sk-ui-test-only", "", 0);
        await f.Workspace.SaveDraftAsync("keep recording", "keep shot");
        var capture = new UiCapture(); var wire = new UiTransport(); var window = await WindowFor(f, wire, capture);
        try
        {
            var knob = Control<Button>(window, "voice-joystick");
            var center = knob.TranslatePoint(new Point(30, 30), window)!.Value;
            window.MouseDown(center, MouseButton.Left); await Until(() => capture.Started);
            window.MouseMove(center + new Vector(35, 0), RawInputModifiers.LeftMouseButton);
            window.MouseMove(center + new Vector(10, 0), RawInputModifiers.LeftMouseButton);
            window.MouseUp(center + new Vector(10, 0), MouseButton.Left);
            Assert.True(knob.RenderTransform!.Value.M31 > 0);
            await Until(() => capture.Disposed);
            await Until(() => Math.Abs(knob.RenderTransform!.Value.M31) < .001);
            Assert.False(wire.Finished); // Cancellation does not submit an ASR finish task.
            Assert.Equal("keep recording", f.Workspace.Draft.RecordingNote);
            Assert.Equal("keep shot", f.Workspace.Draft.ShotNote);
        }
        finally { window.Close(); }
    });

    [WindowsOnlyFact]
    public Task Ui_joystick_waits_at_edge_then_animates_home_after_final() => RunUi(async () =>
    {
        using var f = await Create(); await SpeechSettings(f).SaveAsync("sk-ui-test-only", "", 0);
        var capture = new UiCapture(); var wire = new UiTransport { HoldFinal = true };
        var window = await WindowFor(f, wire, capture);
        try
        {
            var knob = Control<Button>(window, "voice-joystick");
            var center = knob.TranslatePoint(new Point(30, 30), window)!.Value;
            window.MouseDown(center, MouseButton.Left); await Until(() => capture.Started);
            window.MouseMove(center + new Vector(30, 0), RawInputModifiers.LeftMouseButton);
            window.MouseUp(center + new Vector(30, 0), MouseButton.Left);
            await Until(() => wire.Finished);
            Assert.Equal(25, knob.RenderTransform!.Value.M31);
            Assert.Empty(f.Workspace.Draft.ShotNote);
            wire.Complete(); await Until(() => capture.Disposed);
            Assert.True(knob.RenderTransform!.Value.M31 > 0);
            await Until(() => Math.Abs(knob.RenderTransform!.Value.M31) < .001);
            Assert.Equal("最终转写。", f.Workspace.Draft.ShotNote);
            CaptureFrame(window, "joystick-desktop");
        }
        finally { window.Close(); }
    });
}
