namespace SuperBulider_AI.Models.BI.Evaluation;

/// <summary>
/// Golden Join 的语义期望。
/// Golden Dataset 不直接依赖 Metadata 自增 Id。
/// </summary>
public class GoldenJoinExpectation
{
    public long LeftDataSourceId { get; set; }

    public string LeftTableName { get; set; } = string.Empty;

    public string LeftColumnBusinessKey { get; set; } = string.Empty;

    public long RightDataSourceId { get; set; }

    public string RightTableName { get; set; } = string.Empty;

    public string RightColumnBusinessKey { get; set; } = string.Empty;

    public string JoinType { get; set; } = "INNER";
}
