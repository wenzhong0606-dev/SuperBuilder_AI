namespace SuperBuilder_AI.Components.Services;

/// <summary>跨页面共享的会话态：登录令牌、租户、用户。两个 Head 各自注册为 Scoped。</summary>
public sealed class AppState
{
    public string? Token { get; set; }
    public long TenantId { get; set; }
    public long UserId { get; set; }
    public string Username { get; set; } = "";
    public System.Collections.Generic.IReadOnlyList<string> Permissions { get; set; }
        = System.Array.Empty<string>();

    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    /// <summary>会话失效（如 token 过期/被服务端拒绝）时由 ApiClient 触发，供壳层回收并跳登录。</summary>
    public event Action? SessionExpired;

    /// <summary>清空本地会话态（不触碰持久化存储；存储清理由 <see cref="AuthStore"/> 负责）。</summary>
    public void ClearSession()
    {
        Token = null;
        TenantId = 0;
        UserId = 0;
        Username = "";
        Permissions = System.Array.Empty<string>();
    }

    /// <summary>通知监听方会话已失效。</summary>
    public void NotifySessionExpired() => SessionExpired?.Invoke();
}
