using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces.BI;

/// <summary>
/// Dimension 物理绑定证据提供器。
/// 只负责 Metadata Snapshot 上的证据判定，不生成 SQL、不修改 QueryPlan.Joins。
/// </summary>
public interface IDimensionResolutionEvidenceService
{
    Task<DimensionResolutionEvidence?> ResolveAsync(
        long factTableId,
        long factDataSourceId,
        string dimensionSemanticText,
        long preferredColumnId,
        CancellationToken cancellationToken = default);
}
