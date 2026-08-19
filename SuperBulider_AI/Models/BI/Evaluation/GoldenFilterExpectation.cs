namespace SuperBulider_AI.Models.BI.Evaluation;

/// <summary>
/// Golden Filter 的数据库无关语义期望。
/// </summary>
public class GoldenFilterExpectation
{
    /// <summary>
    /// 业务语义描述，不绑定具体 MetadataColumn.BusinessKey。
    /// </summary>
    public string SemanticText { get; set; } = string.Empty;

    /// <summary>
    /// 期望操作符，例如 =、&gt;、&lt;、LIKE。
    /// </summary>
    public string Operator { get; set; } = string.Empty;

    /// <summary>
    /// 期望过滤值。
    /// </summary>
    public string? Value { get; set; }
}
