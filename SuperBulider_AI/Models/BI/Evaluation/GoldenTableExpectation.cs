namespace SuperBuilder_AI.Models.BI.Evaluation;

/// <summary>
/// Golden Table 的数据库无关语义期望。
/// </summary>
public class GoldenTableExpectation
{
    /// <summary>
    /// 业务语义描述，不绑定 DataSourceId 或物理表名。
    /// </summary>
    public string SemanticText { get; set; } = string.Empty;
}
