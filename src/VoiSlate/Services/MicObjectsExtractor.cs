using VoiSlate.Models;

namespace VoiSlate.Services;

/// <summary>
/// 麦克风对象提取（契约 §3 MicObjectsExtractor；对齐原版 helper/mic_objects_extractor.dart）。
/// 协议：shtNote = "正文&lt;对象1/&gt;&lt;对象2/&gt;" —— 按 '&lt;' 分割，首段为正文，
/// 其余段去掉 "/&gt;" 即对象列表（原版 replaceAll('/>', '')，正文为分割首段）。
/// </summary>
public static class MicObjectsExtractor
{
    /// <summary>提取正文与对象轨列表（B8）。</summary>
    public static (string Body, IReadOnlyList<string> Tracks) Extract(string shtNote)
    {
        var parts = shtNote.Split('<');
        var body = parts[0];
        var tracks = parts.Skip(1).Select(p => p.Replace("/>", string.Empty)).ToList();
        return (body, tracks);
    }
}