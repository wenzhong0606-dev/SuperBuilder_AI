using System.Collections.Concurrent;
using System.Security.Cryptography;
using SuperBuilder_AI.Components.Services;

namespace SuperBuilder_AI.Web.Services;

/// <summary>
/// 服务端内存会话存储（Phase 1 默认后端；Phase 3 亦作无 Redis 时的单实例回退）。
///
/// <para>
/// 浏览器仅持有不透明的 <c>sb_sess</c> 会话 id；本存储持有真正的 <see cref="SessionData"/>
/// （含访问令牌），令牌永不下发到浏览器、不进 localStorage。单实例 Blazor Server 或粘性 LB 下可用；
/// 多实例无粘性须升级到 <see cref="RedisWebSessionStore"/>（Phase 3）共享会话数据。
/// </para>
///
/// <para>
/// 过期采用懒清理：读取时若已过期则移除并返回 null。会话最长存活由 <c>Session:TtlMinutes</c> 控制（默认 480 分钟）。
/// 另维护 <c>userId → 会话 id 集合</c> 索引以支持 <see cref="RemoveByUserId"/>（跨实例吊销由 Redis 后端承担）。
/// </para>
/// </summary>
public sealed class WebSessionStore : IWebSessionStore
{
    private readonly ConcurrentDictionary<string, StoredSession> _sessions = new();
    private readonly ConcurrentDictionary<long, HashSet<string>> _userIndex = new();
    private readonly object _indexLock = new();
    private readonly TimeSpan _ttl;

    public WebSessionStore(IConfiguration configuration)
    {
        var minutes = configuration.GetValue("Session:TtlMinutes", 480);
        _ttl = TimeSpan.FromMinutes(minutes);
    }

    /// <summary>创建新会话并返回不透明会话 id（≥128bit 熵，见 hardening §8.2(f)）。</summary>
    public string Create(SessionData data)
    {
        var id = GenerateSessionId();
        _sessions[id] = new StoredSession(data, DateTimeOffset.UtcNow.Add(_ttl));
        IndexAdd(data.UserId, id);
        return id;
    }

    /// <summary>按会话 id 读取；过期或不存在返回 null（并清理索引）。</summary>
    public SessionData? Get(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return null;
        if (_sessions.TryGetValue(sessionId, out var s) && DateTimeOffset.UtcNow < s.ExpiresAtUtc)
            return s.Data;
        Remove(sessionId);
        return null;
    }

    /// <summary>覆盖写入指定会话 id 的数据（租户切换、刷新后更新令牌）。</summary>
    public void Set(string sessionId, SessionData data)
    {
        if (string.IsNullOrEmpty(sessionId)) return;
        _sessions[sessionId] = new StoredSession(data, DateTimeOffset.UtcNow.Add(_ttl));
        IndexAdd(data.UserId, sessionId);
    }

    /// <summary>删除单一会话（登出 / 会话吊销）。</summary>
    public void Remove(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return;
        if (_sessions.TryRemove(sessionId, out var s))
            IndexRemove(s.Data.UserId, sessionId);
    }

    /// <summary>吊销某用户的所有会话，返回被清除的会话数。</summary>
    public int RemoveByUserId(long userId)
    {
        if (!_userIndex.TryRemove(userId, out var ids)) return 0;
        var count = 0;
        lock (_indexLock)
        {
            foreach (var id in ids)
                if (_sessions.TryRemove(id, out _)) count++;
        }

        return count;
    }

    private void IndexAdd(long userId, string id)
    {
        var set = _userIndex.GetOrAdd(userId, _ => new HashSet<string>());
        lock (_indexLock)
        {
            set.Add(id);
        }
    }

    private void IndexRemove(long userId, string id)
    {
        if (_userIndex.TryGetValue(userId, out var set))
        {
            lock (_indexLock)
            {
                set.Remove(id);
            }
        }
    }

    /// <summary>≥128bit 熵的不透明会话 id（RandomNumberGenerator；不使用 Guid.NewGuid()，见 hardening §8.2(f)）。</summary>
    private static string GenerateSessionId()
    {
        var bytes = RandomNumberGenerator.GetBytes(32); // 256 bit
        return Convert.ToHexString(bytes);
    }

    private sealed record StoredSession(SessionData Data, DateTimeOffset ExpiresAtUtc);
}
