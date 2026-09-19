using SuperBuilder_AI.Components.Services;

namespace SuperBuilder_AI.Web.Services;

/// <summary>Web 端会话签发（Phase 1）：生成一次性登录交接码。</summary>
public interface IWebSessionIssuer
{
    /// <summary>暂存待交接会话并返回一次性交接码（≥128bit 熵，默认 60s 时效）。</summary>
    Task<string> IssueAsync(SessionData data, string? returnUrl);
}
