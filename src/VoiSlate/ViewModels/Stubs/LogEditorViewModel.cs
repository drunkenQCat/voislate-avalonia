using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiSlate.Models;
using VoiSlate.Services;

namespace VoiSlate.ViewModels;

/// <summary>
/// Agent C stub：场记编辑对话框 VM（契约 §4 LogEditorViewModel —— Agent B 产出，本文件为编译用占位，合并后删除）。
/// 契约：编辑副本 + 可用文件号（1..500 减已用）+ Save/Delete；
/// 保存/删除只经 ITakeFlowService.SaveEditAsync/DeleteItemAsync（唯一写入口纪律）。
/// </summary>
public partial class LogEditorViewModel : ObservableObject
{
    private readonly SlateLogItem _original;
    private readonly int _index;
    private readonly ITakeFlowService _flow;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _tkNote = string.Empty;

    [ObservableProperty]
    private string _shtNote = string.Empty;

    [ObservableProperty]
    private string _scnNote = string.Empty;

    [ObservableProperty]
    private TkStatus _okTk;

    [ObservableProperty]
    private ShtStatus _okSht;

    /// <summary>声音评价可选值（组合框 ItemsSource）。</summary>
    public IReadOnlyList<TkStatus> TkStatuses { get; } = Enum.GetValues<TkStatus>();

    /// <summary>画面评价可选值（组合框 ItemsSource）。</summary>
    public IReadOnlyList<ShtStatus> ShtStatuses { get; } = Enum.GetValues<ShtStatus>();

    /// <summary>可用文件号 1..500 减已用（stub：占位全量；B 交付真实计算）。</summary>
    public IReadOnlyList<int> AvailableFileNumbers { get; } = Enumerable.Range(1, 500).ToList();

    public RelayCommand SaveCommand { get; }

    public RelayCommand DeleteCommand { get; }

    /// <summary>保存/删除完成后由窗口关闭。</summary>
    public event Action? CloseRequested;

    public LogEditorViewModel(SlateLogItem item, int index, ITakeFlowService flow)
    {
        _original = item;
        _index = index;
        _flow = flow;

        FileName = item.FileName;
        TkNote = item.TkNote;
        ShtNote = item.ShtNote;
        ScnNote = item.ScnNote;
        OkTk = item.OkTk;
        OkSht = item.OkSht;

        SaveCommand = new RelayCommand(Save);
        DeleteCommand = new RelayCommand(Delete);
    }

    private void Save()
    {
        var updated = new SlateLogItem
        {
            Id = _original.Id,
            Scn = _original.Scn,
            Sht = _original.Sht,
            Tk = _original.Tk,
            FilenamePrefix = _original.FilenamePrefix,
            FilenameLinker = _original.FilenameLinker,
            FilenameNum = _original.FilenameNum,
            TkNote = TkNote,
            ShtNote = ShtNote,
            ScnNote = ScnNote,
            OkTk = OkTk,
            OkSht = OkSht,
        };

        _ = _flow.SaveEditAsync(updated, _index, CancellationToken.None);
        CloseRequested?.Invoke();
    }

    private void Delete()
    {
        _ = _flow.DeleteItemAsync(_index, CancellationToken.None);
        CloseRequested?.Invoke();
    }
}