using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces.BI.Planning;

/// <summary>
/// M5-01：规范化语义解析器。将管线各阶段使用的原始引用（业务名 / 别名 / 物理列名）
/// 解析为统一的 <see cref="CanonicalSemanticId"/> 与定义，使 Understanding / Builder / Validator
/// 引用同一套稳定标识（"统一 ID 和定义"）。
///
/// 默认实现 <see cref="SuperBuilder_AI.Services.BI.PassThroughCanonicalResolver"/> 以名称为键、零外部依赖、零行为变更；
/// 生产环境应接入基于规范化语义模型（元数据 + 业务词表）的真实解析，并供 M5-08 治理策略使用。
/// </summary>
public interface ICanonicalSemanticResolver
{
    /// <summary>为给定种类与原始引用解析出统一 ID；无法解析时返回 null。</summary>
    Task<CanonicalSemanticId?> ResolveAsync(
        SemanticKind kind,
        string rawReference,
        CancellationToken ct = default);

    /// <summary>返回当前可用的规范化语义模型；默认实现返回空模型。</summary>
    Task<CanonicalSemanticModel> GetModelAsync(CancellationToken ct = default);
}
