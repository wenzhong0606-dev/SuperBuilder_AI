using System.Security.Cryptography;
using System.Text.Json;
using StackExchange.Redis;
using SuperBuilder_AI.Components.Services;

namespace SuperBuilder_AI.Web.Services;

/// <summary>
/// Redis 后端会话存储（Phase 3：多实例共享 + 跨实例吊销）。
///
/// <para>
/// 仅当配置 <c>Session:Redis:Configuration</c> 时启用；缺省回退内存（<see cref="WebSessionStore"/>）。
/// 复用 <see cref="IConnectionMultiplexer"/>，但使用**独立开关**，不依赖 <c>RateLimit:Store:Type</c>
/// （否则限流用 Memory 时会话无法跨实例，见 hardening §3 Phase 3）。
/// </para>
///
/// <para>
/// 键空间：
/// <list type="bullet">
///   <item><description><c>sb:sess:data:{id}</c> — 会话数据 JSON，TTL = <c>Session:TtlMinutes</c>。</description></item>
///   <item><description><c>sb:sess:user:{userId}</c> — Redis Set，该用户的所有会话 id，用于 <see cref="RemoveByUserId"/> 跨实例吊销。</description></item>
/// </list>
/// 会话数据仅在服务端，令牌永不下发浏览器；Redis 仅作共享存储后端，非令牌有效性权威（API 侧 RefreshTokenStore 才是）。
/// </para>
///
/// <para>
/// 同步接口方法（<c>StringSet</c>/<c>KeyDelete</c> 等）在 StackExchange.Redis 中既可作为接口方法也可经扩展方法，
/// 为避免 Moq 单元测试拦截错层，本类直接调用 <c>*Async</c> 接口方法并以 <c>GetAwaiter().GetResult()</c> 阻塞。
/// 该阻塞方式与 StackExchange.Redis 自有同步扩展内部一致（不捕获 ASP.NET 同步上下文，无死锁风险）。
/// </para>
/// </summary>
public sealed class RedisWebSessionStore : IWebSessionStore
{
    private readonly IDatabase _db;
    private readonly TimeSpan _ttl;
    private const string PrefixData = "sb:sess:data:";
    private const string PrefixUser = "sb:sess:user:";

    public RedisWebSessionStore(IConnectionMultiplexer multiplexer, IConfiguration configuration)
    {
        _db = multiplexer.GetDatabase();
        var minutes = configuration.GetValue("Session:TtlMinutes", 480);
        _ttl = TimeSpan.FromMinutes(minutes);
    }

    /// <summary>创建新会话并登记 userId 索引，返回不透明会话 id（≥128bit 熵）。</summary>
    public string Create(SessionData data)
    {
        var id = GenerateSessionId();
        _db.StringSetAsync(PrefixData + id, JsonSerializer.Serialize(data), _ttl).GetAwaiter().GetResult();
        _db.SetAddAsync(PrefixUser + data.UserId, id).GetAwaiter().GetResult();
        _db.KeyExpireAsync(PrefixUser + data.UserId, _ttl).GetAwaiter().GetResult();
        return id;
    }

    /// <summary>按会话 id 读取；缺失或 JSON 损坏返回 null。</summary>
    public SessionData? Get(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return null;
        var raw = _db.StringGetAsync(PrefixData + sessionId).GetAwaiter().GetResult();
        if (raw.IsNullOrEmpty) return null;
        try
        {
            return JsonSerializer.Deserialize<SessionData>((string)raw!);
        }
        catch (JsonException)
        {
            _db.KeyDeleteAsync(PrefixData + sessionId).GetAwaiter().GetResult();
            return null;
        }
    }

    /// <summary>覆盖写入指定会话 id 的数据，并补登 userId 索引。</summary>
    public void Set(string sessionId, SessionData data)
    {
        if (string.IsNullOrEmpty(sessionId)) return;
        _db.StringSetAsync(PrefixData + sessionId, JsonSerializer.Serialize(data), _ttl).GetAwaiter().GetResult();
        _db.SetAddAsync(PrefixUser + data.UserId, sessionId).GetAwaiter().GetResult();
        _db.KeyExpireAsync(PrefixUser + data.UserId, _ttl).GetAwaiter().GetResult();
    }

    /// <summary>删除单一会话及其 userId 索引条目。</summary>
    public void Remove(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return;
        var data = Get(sessionId); // 取 userId 以便从索引移除
        _db.KeyDeleteAsync(PrefixData + sessionId).GetAwaiter().GetResult();
        if (data is not null) _db.SetRemoveAsync(PrefixUser + data.UserId, sessionId).GetAwaiter().GetResult();
    }

    /// <summary>吊销某用户的所有会话（跨实例），返回被清除的会话数。</summary>
    public int RemoveByUserId(long userId)
    {
        var userKey = PrefixUser + userId;
        var members = _db.SetMembersAsync(userKey).GetAwaiter().GetResult();
        var count = 0;
        foreach (var member in members)
        {
            if (_db.KeyDeleteAsync(PrefixData + member.ToString()).GetAwaiter().GetResult()) count++;
        }

        _db.KeyDeleteAsync(userKey).GetAwaiter().GetResult();
        return count;
    }

    /// <summary>≥128bit 熵的不透明会话 id（RandomNumberGenerator；不使用 Guid.NewGuid()，见 hardening §8.2(f)）。</summary>
    private static string GenerateSessionId()
    {
        var bytes = RandomNumberGenerator.GetBytes(32); // 256 bit
        return Convert.ToHexString(bytes);
    }
}
