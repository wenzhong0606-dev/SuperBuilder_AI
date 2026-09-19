using System.Collections.Concurrent;
using System.Security.Cryptography;
using SuperBuilder_AI.Components.Services;

namespace SuperBuilder_AI.Web.Services;

/// <summary>
/// 服务端内存会话存储（Phase 1，M8-05 加固）。
///
/// <para>
/// 浏览器仅持有不透明的 <c>sb_sess</c> 会话 id；本存储持有真正的 <see cref="SessionData"/>
/// （含访问令牌），令牌永不下发到浏览器、不进 localStorage。单实例 Blazor Server 或粘性 LB 下可用；
/// 多实例无粘性必须升级到 Phase 3（Redis）共享会话数据。
/// </para>
///
/// <para>
/// 过期采用懒清理：读取时若已过期则移除并返回 null（避免后台定时任务的复杂度）。
/// 会话最长存活由 <c>Session:TtlMinutes</c> 控制（默认 480 分钟 = 8 小时）。
/// </para>
/// </summary>
public sealed class WebSessionStore
{
    private readonly ConcurrentDictionary<string, StoredSession> _sessions = new();
    private readonly TimeSpan _ttl;

    public WebSessionStore(IConfiguration configuration)
    {
        var minutes = configuration.GetValue("Session:TtlMinutes", 480);
        _ttl = TimeSpan.FromMinutes(minutes);
    }

    /// <summary>创建新会话并返回不透明会话 id（至少 128bit 熵）。</summary>
    public string Create(SessionData data)
    {
        var id = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        _sessions[id] = new StoredSession(data, DateTimeOffset.UtcNow.Add(_ttl));
        return id;
    }

    /// <summary>按会话 id 读取；过期或不存在返回 null（并清理）。</summary>
    public SessionData? Get(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return null;
        if (_sessions.TryGetValue(sessionId, out var s) && DateTimeOffset.UtcNow < s.ExpiresAtUtc)
            return s.Data;
        _sessions.TryRemove(sessionId, out _);
        return null;
    }

    /// <summary>覆盖写入指定会话 id 的数据（租户切换、刷新后更新令牌）。</summary>
    public void Set(string sessionId, SessionData data)
    {
        if (string.IsNullOrEmpty(sessionId)) return;
        _sessions[sessionId] = new StoredSession(data, DateTimeOffset.UtcNow.Add(_ttl));
    }

    /// <summary>删除会话（登出 / 会话吊销）。</summary>
    public void Remove(string sessionId)
    {
        if (!string.IsNullOrEmpty(sessionId))
            _sessions.TryRemove(sessionId, out _);
    }

    private sealed record StoredSession(SessionData Data, DateTimeOffset ExpiresAtUtc);
}
