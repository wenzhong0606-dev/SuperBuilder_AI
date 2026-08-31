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
}
