using Avalonia.Controls;
using Avalonia.Layout;

namespace VoiSlate.Desktop;

public sealed partial class DesktopWorkspaceWindow
{
    private Control BuildNamingSettings()
    {
        var naming = _service.State.Naming;
        var mode = new ComboBox { ItemsSource = new[] { "日期", "Sound Devices", "自定义" }, SelectedIndex = naming.Mode == "custom" ? 2 : naming.Mode == "sound devices" ? 1 : 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        var prefix = new TextBox { Text = naming.CustomPrefix, PlaceholderText = "自定义文件名前缀" };
        var linker = new TextBox { Text = naming.Linker, PlaceholderText = "文件名前缀与序号的分隔符" };
        var result = T(_service.CurrentFile, 12);
        return Stack(8, T("文件命名", 16, "#45654D", true), mode, T("自定义前缀"), prefix, T("分隔符"), linker, result,
            B("保存文件命名", async () =>
            {
                try
                {
                    await _service.SetNamingAsync(new() { Mode = mode.SelectedIndex == 2 ? "custom" : mode.SelectedIndex == 1 ? "sound devices" : "default", CustomPrefix = prefix.Text?.Trim() ?? "", Linker = linker.Text ?? "" });
                    result.Text = "已保存，当前文件：" + _service.CurrentFile;
                }
                catch (Exception ex) { result.Text = ex.Message; }
            }));
    }
    private async void ShowTracks()
    {
        Window? dialog = null;
        var tracks = new TextBox { Text = string.Join("，", _service.Draft.Tracks ?? _service.Shot?.Note.Objects ?? []), AcceptsReturn = true, MinHeight = 80, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var error = T("", 12);
        dialog = Dialog("人物 / 录音轨道", Stack(10, T("用逗号分隔；保存场记时记录当前轨道快照。"), tracks, error,
            B("保存", async () => { try { await _service.SetTracksAsync(tracks.Text ?? ""); dialog!.Close(); } catch (Exception ex) { error.Text = ex.Message; } }, "primary")));
        await dialog.ShowDialog(this);
    }
}
