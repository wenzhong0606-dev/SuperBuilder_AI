namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// BI 查询执行计划。
///
/// V2.0。
/// </summary>
public class QueryPlan
{
    /// <summary>
    /// 安全边界确定的有效租户。该值由认证编排层写入，不接受语义模型推断。
    /// </summary>
    public long? EffectiveTenantId { get; set; }
    public QueryIntent? Intent { get; set; }
    public long DataSourceId { get; set; }
    public List<QueryTable> Tables { get; set; } = new();
    public List<QueryField> Fields { get; set; } = new();
    public List<QueryMetric> Metrics { get; set; } = new();
    public List<QueryDimension> Dimensions { get; set; } = new();
    public List<QueryFilter> Filters { get; set; } = new();
    public List<MandatoryRowFilter> MandatoryRowFilters { get; set; } = new();
    public string? DataPolicyFingerprint { get; set; }
    public List<QueryOrder> Orders { get; set; } = new();
    public List<QueryJoin> Joins { get; set; } = new();
    public bool IsAggregate { get; set; }
    public bool Distinct { get; set; }
    public int? Limit { get; set; }

    /// <summary>
    /// P3 业务实体语义上下文：本次查询命中的候选业务实体（来自 QueryIntent.BusinessEntityHints）。
    /// 仅供可读性与下游消费，不覆盖 Metadata 解析结果。
    /// </summary>
    public List<BusinessEntityHint> BusinessEntityContext { get; set; } = new();

    private bool _isRanking;
    public bool IsRanking
    {
        get => _isRanking || Intent?.IsRanking == true;
        set => _isRanking = value;
    }

    private bool _isDetailRanking;
    /// <summary>
    /// Ranking Contract：明细 TopN。Ranking 且执行计划不是聚合时成立。
    /// </summary>
    public bool IsDetailRanking
    {
        get => _isDetailRanking || (IsRanking && !IsAggregate);
        set => _isDetailRanking = value;
    }

    private bool _isAggregateRanking;
    /// <summary>
    /// Ranking Contract：聚合 TopN。Ranking 且执行计划为聚合时成立。
    /// </summary>
    public bool IsAggregateRanking
    {
        get => _isAggregateRanking || (IsRanking && IsAggregate);
        set => _isAggregateRanking = value;
    }
}
