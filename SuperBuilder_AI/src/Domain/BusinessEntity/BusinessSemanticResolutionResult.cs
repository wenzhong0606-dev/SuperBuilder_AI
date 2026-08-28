namespace SuperBuilder_AI.Models.BI.Entity;

/// <summary>
/// 单个业务实体的语义解析命中结果。
/// </summary>
public sealed class BusinessSemanticMatch
{
    public long EntityId { get; set; }
    public string BusinessKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? BusinessDomain { get; set; }
    public double Score { get; set; }
}

/// <summary>
/// 按业务域分组的语义解析结果。
/// </summary>
public sealed class BusinessSemanticDomainGroup
{
    public string Domain { get; set; } = string.Empty;
    public IReadOnlyList<BusinessSemanticMatch> Matches { get; set; } = new List<BusinessSemanticMatch>();
}

/// <summary>
/// 自然语言 → 业务实体的语义解析结果。
/// </summary>
public sealed class BusinessSemanticResolutionResult
{
    public string Query { get; set; } = string.Empty;
    public IReadOnlyList<BusinessSemanticDomainGroup> Domains { get; set; } = new List<BusinessSemanticDomainGroup>();
}
