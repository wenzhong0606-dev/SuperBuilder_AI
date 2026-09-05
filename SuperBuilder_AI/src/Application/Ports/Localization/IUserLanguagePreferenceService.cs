using System.Threading;
using System.Threading.Tasks;

namespace SuperBuilder_AI.Interfaces.Localization;

/// <summary>
/// 用户级界面语言偏好服务端口。
/// 偏好按 (TenantId, UserId) 维度持久化，写入时强制校验目标文化属于租户可用语言范围。
/// </summary>
public interface IUserLanguagePreferenceService
{
    /// <summary>读取用户偏好文化；无记录时返回 null（调用方回退到租户默认）。</summary>
    Task<string?> GetAsync(long tenantId, long userId, CancellationToken ct = default);

    /// <summary>
    /// 设置用户偏好文化。若 <paramref name="culture"/> 不在租户可用语言范围内，则回退到租户默认语言并持久化该值。
    /// 返回实际生效的文化代码。
    /// </summary>
    Task<string> SetAsync(long tenantId, long userId, string culture, CancellationToken ct = default);
}
