namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// 查询维度。
/// SemanticText 是业务语义，ColumnName 是物理字段，二者职责分离。
/// Phase 2.7：同时承载已冻结的 Dimension Resolution 执行能力。
/// </summary>
public class QueryDimension
{
    public long MetadataColumnId { get; set; }
    public string SemanticText { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public string? Alias { get; set; }
    public string? SemanticType { get; set; }

    /// <summary>
    /// MasterJoin / DirectKey / Ambiguous / NotResolved。
    /// QueryPlan 只消费上游已经确认的 Resolution，不重新推理。
    /// </summary>
    public string ResolutionType { get; set; } = "NotResolved";

    /// <summary>
    /// 当前 Dimension 是否具备可执行绑定。
    /// </summary>
    public string ResolutionState { get; set; } = "NotResolved";

    /// <summary>
    /// Executable / NotExecutable。
    /// </summary>
    public string ExecutionCapability { get; set; } = "NotExecutable";

    /// <summary>
    /// DirectKey 模式下事实表中的稳定 Key。
    /// </summary>
    public long? DimensionKeyColumnId { get; set; }
    public string? DimensionKeyColumnName { get; set; }

    /// <summary>
    /// DirectKey 模式下可直接展示 / GROUP BY 的 Label，可为空。
    /// </summary>
    public long? DimensionLabelColumnId { get; set; }
    public string? DimensionLabelColumnName { get; set; }
}
