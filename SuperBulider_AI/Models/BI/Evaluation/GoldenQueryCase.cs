namespace SuperBulider_AI.Models.BI.Evaluation;

/// <summary>
/// QueryPlan Golden Case。
///
/// 描述 Query Evaluation Framework 的 Ground Truth，
/// 不代表运行时 QueryPlan，也不直接引用 QueryPlan。
/// </summary>
public class GoldenQueryCase
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Question { get; set; } = string.Empty;

    public GoldenQueryExpectation Expected { get; set; } = new();

    public string? Difficulty { get; set; }

    public List<string> Tags { get; set; } = new();

    public string Version { get; set; } = "1.0";

    public bool Enabled { get; set; } = true;
}
