namespace SuperBulider_AI.Models.BI.Evaluation;

/// <summary>
/// Query Evaluation Framework 的 Golden Dataset 根模型。
/// </summary>
public class GoldenQueryDataset
{
    /// <summary>
    /// Dataset 版本。
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Dataset 名称。
    /// </summary>
    public string Dataset { get; set; } = "query-plan-golden";

    /// <summary>
    /// Golden Cases。
    /// </summary>
    public List<GoldenQueryCase> Cases { get; set; } = new();
}
