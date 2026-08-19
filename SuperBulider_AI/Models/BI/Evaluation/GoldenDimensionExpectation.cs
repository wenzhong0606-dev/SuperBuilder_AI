namespace SuperBulider_AI.Models.BI.Evaluation;

/// <summary>
/// Golden Dimension 的数据库无关语义期望。
/// </summary>
public class GoldenDimensionExpectation
{
    /// <summary>
    /// 业务语义描述，不绑定 DataSource、MetadataColumnId 或 BusinessKey。
    /// </summary>
    public string SemanticText { get; set; } = string.Empty;
}
