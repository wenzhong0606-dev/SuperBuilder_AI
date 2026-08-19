namespace SuperBulider_AI.Models.BI.Evaluation;

/// <summary>
/// Golden Table 的语义期望。
/// 当前 Table Identity 使用 DataSourceId + TableName，
/// 与现有 MetadataTable 模型保持一致。
/// </summary>
public class GoldenTableExpectation
{
    public long DataSourceId { get; set; }

    public string TableName { get; set; } = string.Empty;
}
