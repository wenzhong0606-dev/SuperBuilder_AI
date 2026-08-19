namespace SuperBulider_AI.Models.BI.Evaluation;

/// <summary>
/// Golden Join 的语义期望。
/// </summary>
public class GoldenJoinExpectation
{
    public long LeftTableId { get; set; }

    public long LeftColumnId { get; set; }

    public long RightTableId { get; set; }

    public long RightColumnId { get; set; }

    public string JoinType { get; set; } = "INNER";
}
