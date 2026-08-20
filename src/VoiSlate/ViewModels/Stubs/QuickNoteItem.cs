namespace VoiSlate.ViewModels;

/// <summary>场记速览条目（契约 N3 quick_view_log_dialog 语义：fileName → tkNote）。</summary>
public sealed record QuickNoteItem(string FileName, string TkNote);