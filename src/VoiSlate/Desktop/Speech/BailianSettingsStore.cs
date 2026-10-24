using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using VoiSlate.Services;

namespace VoiSlate.Desktop.Speech;

public sealed record BailianOptions(string Token, string WorkspaceId = "", int DeviceNumber = 0)
{
    public const string Model = "paraformer-realtime-v2";
    public Uri Endpoint => new(WorkspaceId.Length == 0 ? "wss://dashscope.aliyuncs.com/api-ws/v1/inference" : $"wss://{WorkspaceId}.cn-beijing.maas.aliyuncs.com/api-ws/v1/inference");
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Token)) throw new InvalidOperationException("请先在工程与偏好中填写百炼 Token（北京地域 API Key）");
        if (Token.Any(char.IsWhiteSpace)) throw new InvalidOperationException("Token 不能包含空格或换行，请粘贴完整 API Key");
        if (WorkspaceId.Length > 0 && !Regex.IsMatch(WorkspaceId, @"^[A-Za-z0-9_-]{1,100}$")) throw new InvalidOperationException("业务空间 ID 格式不正确");
        if (DeviceNumber < 0) throw new InvalidOperationException("请选择有效的麦克风");
    }
}
public interface ITokenProtector { string Protect(string value); string Unprotect(string value); }
public sealed class WindowsTokenProtector : ITokenProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("VoiSlate.Bailian.v1");
    public string Protect(string value)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Token 加密存储需要 Windows");
        return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser));
    }
    public string Unprotect(string value)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Token 解密需要 Windows");
        try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value), Entropy, DataProtectionScope.CurrentUser)); }
        catch (Exception e) when (e is CryptographicException or FormatException) { throw new InvalidOperationException("无法解密已保存的 Token，请重新填写", e); }
    }
}
/// <summary>Secrets are deliberately separate from workspace sessions and all backup/export DTOs.</summary>
public sealed class BailianSettingsStore(ISessionSettingsStore settings, ITokenProtector protector)
{
    public const string TokenKey = "desktop.bailian.protected-token.v1";
    public async Task<BailianOptions> LoadAsync()
    {
        var encrypted = await settings.GetStringAsync(TokenKey, "");
        return new(encrypted.Length == 0 ? "" : protector.Unprotect(encrypted),
            await settings.GetStringAsync("desktop.bailian.workspace", ""), await settings.GetIntAsync("desktop.bailian.microphone", 0));
    }
    public async Task SaveAsync(string? newToken, string workspace, int device)
    {
        var token = string.IsNullOrWhiteSpace(newToken) ? (await LoadAsync()).Token : newToken.Trim();
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) token = token[7..].Trim();
        var options = new BailianOptions(token, workspace.Trim(), device);
        if (token.Length > 0) options.Validate();
        else if (workspace.Length > 0) new BailianOptions("not-configured", workspace.Trim(), device).Validate();
        var encrypted = token.Length == 0 ? "" : protector.Protect(token);
        await settings.SetAsync(TokenKey, encrypted);
        await settings.SetAsync("desktop.bailian.workspace", workspace.Trim());
        await settings.SetAsync("desktop.bailian.microphone", device);
    }
    public Task ClearTokenAsync() => settings.SetAsync(TokenKey, "");
}
