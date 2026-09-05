using SuperBuilder_AI.Models;

namespace SuperBuilder_AI.Models.Localization;

/// <summary>
/// 用户级界面语言偏好（按租户维度）。
/// <para>键为 (TenantId, UserId) 唯一组合；Culture 必须属于该租户的可用语言范围，由 <see cref="SuperBuilder_AI.Services.Seed.UserLanguagePreferenceService"/> 在写入时校验。</para>
/// <para>M3-G0「用户语言恢复」：切换语言后服务端持久化，登录时优先恢复用户偏好，原语言被停用时回退租户默认。</para>
/// </summary>
public sealed class UserLanguagePreference : BaseEntity
{
    /// <summary>所属租户（作用域）。</summary>
    public long TenantId { get; set; }

    /// <summary>用户 Id。</summary>
    public long UserId { get; set; }

    /// <summary>偏好文化代码（BCP 47，如 zh-CN / en-US），需在租户可用语言集合内。</summary>
    public string Culture { get; set; } = string.Empty;
}
