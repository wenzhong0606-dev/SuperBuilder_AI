using SuperBuilder_AI.Components.Services;

namespace SuperBuilder_AI.Web.Services;

/// <summary>
/// 服务端会话存储抽象（Phase 3：Redis 会话 + 可吊销 + 合规）。
///
/// <para>
/// 浏览器仅持有不透明的 <c>sb_sess</c> 会话 id；本存储持有真正的 <see cref="SessionData"/>
/// （含访问令牌），令牌永不下发到浏览器、不进 localStorage。内存与 Redis 两种后端统一契约：
/// 默认单实例 / 无 Redis 用内存（<see cref="WebSessionStore"/>），多实例横向扩展用 Redis（<see cref="RedisWebSessionStore"/>）。
/// </para>
///
/// <para>
/// 吊销：<see cref="RemoveByUserId"/> 维护 <c>userId → 会话 id 集合</c> 索引，使管理员禁用账号 / 改密后
/// 可一次性清除某用户的全部 Web 会话（跨实例，因为 Redis 为共享存储）。跨组件的令牌即时失效仍由
/// API 侧 <c>AuthMiddleware</c> 的 <c>SecurityStamp</c> 每请求校验兜底（令牌死亡 → API 401 → Web 跳登录）。
/// </para>
/// </summary>
public interface IWebSessionStore
{
    /// <summary>创建新会话并返回不透明会话 id（≥128bit 熵）。</summary>
    string Create(SessionData data);

    /// <summary>按会话 id 读取；过期或不存在返回 null（并清理索引）。</summary>
    SessionData? Get(string sessionId);

    /// <summary>覆盖写入指定会话 id 的数据（租户切换、刷新后更新令牌）。</summary>
    void Set(string sessionId, SessionData data);

    /// <summary>删除单一会话（登出 / 会话吊销）。</summary>
    void Remove(string sessionId);

    /// <summary>吊销某用户的所有会话（管理员禁用账号 / 改密后强踢），返回被清除的会话数。</summary>
    int RemoveByUserId(long userId);
}
