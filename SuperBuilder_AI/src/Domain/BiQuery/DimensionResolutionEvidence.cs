namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// Dimension 物理绑定证据。
/// 仅描述 Resolution，不生成 SQL，不创建 QueryJoin。
/// </summary>
public sealed class DimensionResolutionEvidence
{
    public string ResolutionType { get; init; } = "NotResolved";
    public string ExecutionCapability { get; init; } = "NotExecutable";
    public long FactTableId { get; init; }
    public long FactDataSourceId { get; init; }
    public long FactKeyColumnId { get; init; }
    public string FactTable { get; init; } = string.Empty;
    public string FactKeyColumn { get; init; } = string.Empty;
    public long? MasterTableId { get; init; }
    public long? MasterDataSourceId { get; init; }
    public string? MasterTable { get; init; }
    public long? MasterKeyColumnId { get; init; }
    public string? MasterKeyColumn { get; init; }
    public long? MasterLabelColumnId { get; init; }
    public string? MasterLabelColumn { get; init; }
    public double Score { get; init; }
    public string Reason { get; init; } = string.Empty;
}
