using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace VoiSlate.Desktop;

public sealed partial class DesktopWorkspaceWindow
{
    private readonly DispatcherTimer _backupTimer = new() { Interval = TimeSpan.FromMinutes(3) };
    private bool _backupRunning, _closingBackup, _exitReady;
    private void InitializeBackups()
    {
        _backupTimer.Tick += async (_, _) =>
        {
            if (!_service.State.AutoBackup || _backupRunning) return;
            _backupRunning = true;
            try { await _service.WriteBackupAsync("auto"); }
            catch (Exception ex) { Toast("自动备份失败：" + ex.Message, true); }
            finally { _backupRunning = false; }
        };
        _backupTimer.Start();
        Closing += async (_, e) =>
        {
            if (_exitReady) return;
            _finishVoice?.Invoke();
            if (!_service.State.AutoBackup) { _backupTimer.Stop(); return; }
            e.Cancel = true; if (_closingBackup) return;
            _closingBackup = true; _backupTimer.Stop();
            try { await _service.WriteBackupAsync("exit"); _exitReady = true; Close(); }
            catch (Exception ex)
            {
                _closingBackup = false; _backupTimer.Start();
                if (await Confirm("退出备份失败", "本机数据库仍已保存，但退出备份失败：" + ex.Message + "。是否仍然退出？")) { _exitReady = true; Close(); }
            }
        };
        Closed += (_, _) => _backupTimer.Stop();
    }
    private Control BuildBackupSettings()
    {
        var automatic = new CheckBox { Content = "每三分钟及退出时自动备份", IsChecked = _service.State.AutoBackup };
        var directory = T(_service.BackupDirectory, 11);
        var status = T("", 12);
        var choose = B("选择备份目录", async () =>
        {
            try
            {
                var folders = await StorageProvider.OpenFolderPickerAsync(new() { Title = "选择备份目录", AllowMultiple = false });
                if (folders.Count == 0) return;
                var path = folders[0].TryGetLocalPath() ?? throw new InvalidOperationException("请选择本地文件夹");
                await _service.SetBackupPreferencesAsync(automatic.IsChecked == true, path); directory.Text = _service.BackupDirectory; status.Text = "备份目录已保存";
            }
            catch (Exception ex) { status.Text = ex.Message; }
        });
        automatic.IsCheckedChanged += async (_, _) =>
        {
            try { await _service.SetBackupPreferencesAsync(automatic.IsChecked == true, _service.State.BackupDirectory); }
            catch (Exception ex) { status.Text = ex.Message; }
        };
        return Stack(8, T("备份与恢复", 16, "#45654D", true), automatic, directory, choose,
            B("立即备份", async () => { try { status.Text = "备份已写入：" + await _service.WriteBackupAsync(); } catch (Exception ex) { status.Text = "备份失败：" + ex.Message; } }),
            B("导入场记 / 恢复 JSON 备份", ImportWorkspace), B("导出 Flutter 兼容场记 JSON", ExportFlutter), status);
    }
    private async void ImportWorkspace()
    {
        _finishVoice?.Invoke();
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new() { Title = "导入场记或备份", AllowMultiple = false, FileTypeFilter = [new("VoiSlate JSON") { Patterns = ["*.json"] }] });
            if (files.Count == 0) return;
            await using var stream = await files[0].OpenReadAsync(); using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync(); var preview = DesktopWorkspaceService.PrepareImport(content, _service.Today);
            Window? dialog = null;
            var date = new TextBox { Text = _service.Today, PlaceholderText = "六位年月日，例如 261024" };
            var mode = new ComboBox { ItemsSource = preview.Legacy ? new[] { "合并场记" } : new[] { "合并（保留现有内容）", "恢复备份（替换计划与场记）" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
            var error = T("", 12, "#AD4C4C"); bool importing = false;
            var execute = B("执行导入", async () =>
            {
                if (importing) return; importing = true;
                try
                {
                    var parsed = DesktopWorkspaceService.PrepareImport(content, date.Text ?? ""); var replace = mode.SelectedIndex == 1;
                    if (replace && !await Confirm("恢复备份", "将替换当前计划与全部场记。会先生成恢复前备份；Token 和本机备份偏好保留。是否继续？", dialog)) return;
                    await _service.WriteBackupAsync("before-import");
                    await _service.ImportAsync(parsed, replace); dialog!.Close(); Toast("已导入；相同日期与文件名的相同记录自动跳过");
                }
                catch (Exception ex) { error.Text = "导入失败：" + ex.Message; }
                finally { importing = false; }
            }, "primary");
            dialog = Dialog("导入预览", Stack(12, T($"{(preview.Legacy ? "Flutter 场记" : "桌面备份")} · {preview.TakeCount} 条 · {preview.Backup.Plan.Count} 场计划"),
                T(preview.UndatedCount > 0 ? $"{preview.UndatedCount} 条无法推断日期，将归档到下方指定日期。" : "日期已从归档或文件名前缀读取。", 12),
                T("无日期记录的归档日期"), date, T("导入方式"), mode, T("导入前自动备份；合并遇到同名而内容不同的记录时会停止。", 11), error,
                Row(8, B("取消", () => dialog!.Close(), "quiet"), execute)));
            await dialog.ShowDialog(this);
        }
        catch (Exception ex) { Toast("无法导入：" + ex.Message, true); }
    }
    private async void ExportFlutter()
    {
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new() { Title = "导出 Flutter 兼容场记", SuggestedFileName = "VoiSlate-Flutter-" + _service.Today + ".json", DefaultExtension = "json", FileTypeChoices = [new("JSON") { Patterns = ["*.json"] }] });
            if (file is null) return;
            await using var stream = await file.OpenWriteAsync(); stream.SetLength(0); await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(_service.ExportFlutterJson()); Toast("Flutter 兼容场记已导出");
        }
        catch (Exception ex) { Toast("导出失败：" + ex.Message, true); }
    }
}
