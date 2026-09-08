namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 身份域客户端契约（M9-01）：认证、租户切换、自助注册、自助注册配置与当前用户语言偏好。
/// 与 <see cref="IApiClient"/> 中对应方法签名一致，可独立于其他域单独测试/替换。
/// </summary>
public interface IIdentityApiClient
{
    Task<(AuthResult? Result, string? Error)> LoginAsync(string username, long tenantId, string? password = null, CancellationToken ct = default);

    /// <summary>M6 登录兜底：按已知租户编码（不枚举目录）解析租户 Id，供 <c>Auth:ShowTenantDirectory</c> 关闭时手动登录。</summary>
    Task<(long Id, string? TenantCode, string? Name, string? Error)> ResolveTenantByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>M2-05 切换生效租户：校验成员资格后由后端重签令牌，返回新令牌与切换后端租户上下文。</summary>
    Task<(TenantSwitchResult? Result, string? Error)> SwitchTenantAsync(long tenantId, CancellationToken ct = default);

    /// <summary>M2-06 自助注册：匿名创建新租户与首位管理员，注册即登录（后端重签令牌）。</summary>
    Task<(SelfRegistrationResult? Result, string? Error)> RegisterSelfAsync(
        string tenantCode, string tenantName, string adminUsername, string adminEmail,
        string adminPassword, string? adminDisplayName = null, CancellationToken ct = default);

    /// <summary>M2-06 平台管理员查看自助注册配置（需 platform:admin:manage）。</summary>
    Task<(SelfRegistrationConfigView? Result, string? Error)> GetSelfRegistrationConfigAsync(CancellationToken ct = default);

    /// <summary>M3-G0 读取当前用户的服务端语言偏好（已认证；无记录时 Culture 为 null）。</summary>
    Task<(string? Culture, string? Error)> GetUserLanguageAsync(CancellationToken ct = default);

    /// <summary>M3-G0 持久化当前用户语言偏好到服务端（强制校验租户可用语言范围，越界回退默认）。</summary>
    Task<(string? Culture, string? Error)> SetUserLanguageAsync(string culture, CancellationToken ct = default);
}
