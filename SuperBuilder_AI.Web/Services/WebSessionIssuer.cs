using System.Security.Cryptography;
using SuperBuilder_AI.Components.Services;

namespace SuperBuilder_AI.Web.Services;

/// <summary>
/// <see cref="IWebSessionIssuer"/> 实现：用 <see cref="RandomNumberGenerator"/> 生成高熵一次性交接码，
/// 经 <see cref="PendingHandoffStore"/> 暂存待交接会话。
/// </summary>
public sealed class WebSessionIssuer : IWebSessionIssuer
{
    private readonly PendingHandoffStore _pending;
    private static readonly TimeSpan HandoffTtl = TimeSpan.FromSeconds(60);

    public WebSessionIssuer(PendingHandoffStore pending) => _pending = pending;

    public Task<string> IssueAsync(SessionData data, string? returnUrl)
    {
        var bytes = RandomNumberGenerator.GetBytes(32); // 256 bit 熵
        var code = Convert.ToHexString(bytes);
        _pending.Store(code, data, returnUrl, HandoffTtl);
        return Task.FromResult(code);
    }
}
