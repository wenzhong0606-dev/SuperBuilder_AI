namespace SuperBulider_AI.Models.BI.Evaluation;

/// <summary>
/// Golden Join 的数据库无关语义期望。
/// </summary>
public class GoldenJoinExpectation
{
    public string LeftTableSemanticText { get; set; } = string.Empty;

    public string LeftColumnSemanticText { get; set; } = string.Empty;

    public string RightTableSemanticText { get; set; } = string.Empty;

    public string RightColumnSemanticText { get; set; } = string.Empty;

    public string JoinType { get; set; } = "INNER";
}
