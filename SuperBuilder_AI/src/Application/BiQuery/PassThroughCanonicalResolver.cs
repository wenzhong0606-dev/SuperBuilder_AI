using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// M5-01 默认解析器：以原始引用本身作为统一 ID 的键，不依赖任何外部元数据或业务词表。
///
/// 零影响、向后兼容：在未接入治理驱动的语义模型前，解析结果等同于"名称即标识"，
/// 不改变任何管线行为；同时使 <see cref="CanonicalSemanticId"/> 契约在管线中可用，
/// 供 M5-02（统一字段解析）与 M5-08（真实敏感度分类）后续接入。
/// </summary>
public sealed class PassThroughCanonicalResolver : ICanonicalSemanticResolver
{
    public Task<CanonicalSemanticId?> ResolveAsync(
        SemanticKind kind,
        string rawReference,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawReference))
            return Task.FromResult<CanonicalSemanticId?>(null);
        return Task.FromResult<CanonicalSemanticId?>(new CanonicalSemanticId(kind, rawReference));
    }

    public Task<CanonicalSemanticModel> GetModelAsync(CancellationToken ct = default)
        => Task.FromResult(CanonicalSemanticModel.Empty);
}
