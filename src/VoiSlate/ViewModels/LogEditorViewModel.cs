using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiSlate.Models;
using VoiSlate.Services;

namespace VoiSlate.ViewModels;

/// <summary>
/// 场记条目编辑器 VM（契约 §4 LogEditorViewModel；对齐原版 log_editor.dart）。
/// 编辑副本：保存/删除只经 ITakeFlowService.SaveEditAsync/DeleteItemAsync（维护唯一写入口纪律，B1）。
/// 可用文件号 = 1..500 减已用 + 当前文件号置顶（原版 fileNumPicker）；
/// shtNote 按 Mic 协议“正文&lt;对象1/&gt;…”拆分正文与轨道标签（E 交付 MicObjectsExtractor 前为 VM 内联实现，偏差注明）。
/// </summary>
public partial class LogEditorViewModel : ObservableObject
{
    private readonly ITakeFlowService _takeFlow;
    private readonly int _index;

    public LogEditorViewModel(
        ITakeFlowService takeFlow,
        SlateLogItem item,
        int index,
        IReadOnlyList<int> usedFileNumbers)
    {
        _takeFlow = takeFlow;
        _index = index;

        Scn = item.Scn;
        Sht = item.Sht;
        TkNumber = item.Tk;
        FilenamePrefix = item.FilenamePrefix;
        FilenameLinker = item.FilenameLinker;
        FilenameNum = item.FilenameNum;
        TkNote = item.TkNote;

        var (body, tracks) = SplitShtNote(item.ShtNote);
        ShtNote = body;
        foreach (var t in tracks)
        {
            TrackTags.Add(t);
        }

        ScnNote = item.ScnNote;
        OkTk = item.OkTk;
        OkSht = item.OkSht;

        var used = new HashSet<int>(usedFileNumbers);
        var available = Enumerable.Range(1, 500).Where(n => !used.Contains(n)).ToList();
        available.Insert(0, item.FilenameNum); // 当前文件号始终可选（原版行为）
        AvailableFileNumbers = available;
    }

    // ---- 只读上下文 ----

    public string Scn { get; }

    public string Sht { get; }

    public string FilenamePrefix { get; }

    public string FilenameLinker { get; }

    /// <summary>可用文件号（1..500 减已用，当前号置顶）。</summary>
    public IReadOnlyList<int> AvailableFileNumbers { get; }

    // ---- 可编辑副本 ----

    [ObservableProperty]
    private int _tkNumber;

    [ObservableProperty]
    private int _filenameNum;

    [ObservableProperty]
    private string _tkNote = string.Empty;

    /// <summary>镜头标注正文（Mic 协议“&lt;”之前部分；保存时与轨道标签合并回 shtNote）。</summary>
    [ObservableProperty]
    private string _shtNote = string.Empty;

    [ObservableProperty]
    private string _scnNote = string.Empty;

    [ObservableProperty]
    private TkStatus _okTk;

    [ObservableProperty]
    private ShtStatus _okSht;

    /// <summary>轨道标签（TagChips 增删改）。</summary>
    public ObservableCollection<string> TrackTags { get; } = [];

    public void AddTag(string tag)
    {
        if (!string.IsNullOrEmpty(tag) && !TrackTags.Contains(tag))
        {
            TrackTags.Add(tag);
        }
    }

    public bool RenameTag(int index, string newTag)
    {
        if (index < 0 || index >= TrackTags.Count || string.IsNullOrEmpty(newTag))
        {
            return false;
        }

        TrackTags[index] = newTag;
        return true;
    }

    public bool RemoveTag(int index)
    {
        if (index < 0 || index >= TrackTags.Count)
        {
            return false;
        }

        TrackTags.RemoveAt(index);
        return true;
    }

    public bool Saved { get; private set; }

    public bool Deleted { get; private set; }

    // ---- 保存 / 删除（唯一入口 ITakeFlowService）----

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task Save()
    {
        var item = new SlateLogItem
        {
            Scn = Scn,
            Sht = Sht,
            Tk = TkNumber,
            FilenamePrefix = FilenamePrefix,
            FilenameLinker = FilenameLinker,
            FilenameNum = FilenameNum,
            TkNote = TkNote,
            ShtNote = ShtNote + string.Concat(TrackTags.Select(t => $"<{t}/>")),
            ScnNote = ScnNote,
            OkTk = OkTk,
            OkSht = OkSht,
        };
        await _takeFlow.SaveEditAsync(item, _index, CancellationToken.None);
        Saved = true; // C 据此关闭编辑对话框
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task Delete()
    {
        await _takeFlow.DeleteItemAsync(_index, CancellationToken.None);
        Deleted = true;
    }

    // ---- Mic 协议拆分（对齐原版 MicObjectsExtractor：split '<'、剥 '/>'）----

    private static (string Body, List<string> Tracks) SplitShtNote(string shtNote)
    {
        var parts = shtNote.Split('<');
        var body = parts[0];
        var tracks = new List<string>();
        for (var i = 1; i < parts.Length; i++)
        {
            var track = parts[i].Replace("/>", string.Empty);
            if (!string.IsNullOrEmpty(track))
            {
                tracks.Add(track);
            }
        }

        return (body, tracks);
    }
}