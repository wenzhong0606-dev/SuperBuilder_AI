using System.Collections.Concurrent;
using SuperBuilder_AI.Components.Services;

namespace SuperBuilder_AI.Web.Services;

/// <summary>
/// 一次性登录交接码存储（Phase 1，M8-05 加固）。
///
/// <para>
/// Blazor 电路内登录成功后，Web 服务端在此暂存「待交接会话」并生成一次性、短时效的交接码；
/// 浏览器随后通过普通 HTTP GET（<c>/auth/session/start?code=...</c>）原子消费该码，将其迁入正式的
/// <see cref="WebSessionStore"/> 并写入 httpOnly cookie。交接码不是令牌、也不是最终会话 id，且仅可消费一次、短时效，
/// 即使被泄露也无法重放或读取令牌（见 hardening §3 Phase 1 任务 2）。
/// </para>
/// </summary>
public sealed class PendingHandoffStore
{
    private readonly ConcurrentDictionary<string, PendingEntry> _entries = new();

    public void Store(string code, SessionData data, string? returnUrl, TimeSpan ttl)
    {
        _entries[code] = new PendingEntry(data, returnUrl, DateTimeOffset.UtcNow.Add(ttl));
    }

    /// <summary>原子消费交接码：有效且未过期则返回数据并移除（一次性）；否则返回 false。</summary>
    public bool TryConsume(string code, out SessionData? data, out string? returnUrl)
    {
        data = null;
        returnUrl = null;
        if (string.IsNullOrEmpty(code)) return false;
        if (_entries.TryRemove(code, out var entry))
        {
            if (DateTimeOffset.UtcNow < entry.ExpiresAtUtc)
            {
                data = entry.Data;
                returnUrl = entry.ReturnUrl;
                return true;
            }

            // 已过期：移除后视为无效（防止重放）。
            return false;
        }

        return false;
    }

    private sealed record PendingEntry(SessionData Data, string? ReturnUrl, DateTimeOffset ExpiresAtUtc);
}
