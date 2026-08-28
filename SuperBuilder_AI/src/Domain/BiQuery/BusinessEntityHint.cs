namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// AI 理解阶段识别出的候选业务实体提示。
///
/// 挂载在 QueryIntent.BusinessEntityHints 上，供后续 QueryPlan 构建阶段
/// 参考业务语义模型（P3）做实体级对齐。
/// </summary>
public sealed class BusinessEntityHint
{
    public string Name { get; set; } = string.Empty;
    public string? BusinessKey { get; set; }
    public string? BusinessDomain { get; set; }
    public double Confidence { get; set; }
}
