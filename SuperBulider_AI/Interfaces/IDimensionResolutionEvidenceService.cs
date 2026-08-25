using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces;

/// <summary>
/// Dimension 物理绑定证据服务。
/// 只负责从当前 Metadata Snapshot 计算 Resolution Evidence；不生成 SQL、不写 QueryPlan.Joins。
/// </summary>
public interface IDimensionResolutionEvidenceService
{
    Task<DimensionResolutionEvidence?> ResolveAsync(
        string dimensionSemanticText,
        long factTableId,
        long factDataSourceId,
        int topK = 20);
}
