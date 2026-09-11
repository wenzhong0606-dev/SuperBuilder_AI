namespace SuperBuilder_AI.Interfaces.BI.Entity;

/// <summary>
/// M12-16 指标中心：指标治理视图行（只读投影）。
///
/// 刻意返回扁平投影而非 EF 实体：BusinessEntityMetric 的导航属性会与
/// BusinessEntity.Metrics 形成循环引用，直接序列化会失败；投影同时把
/// 需要 join 的实体名/业务域与物理绑定计数一次算好，供前端治理视图直接渲染。
/// </summary>
public sealed record BusinessMetricView(
    long Id,
    long BusinessEntityId,
    string? EntityName,
    string? EntityDisplayName,
    string? BusinessDomain,
    string Name,
    string? DisplayName,
    string? Description,
    string? SemanticType,
    string? Aggregation,
    bool IsCalculated,
    int PhysicalBindingCount,
    /// <summary>M12 增量：计算口径表达式（仅 <see cref="IsCalculated"/> 为 true 时有意义）。</summary>
    string? Expression,
    /// <summary>M12 增量：结果数据类型（decimal/int/string/date）。</summary>
    string? DataType);

/// <summary>M12-16 指标中心：维度治理视图行（只读投影）。</summary>
public sealed record BusinessDimensionView(
    long Id,
    long BusinessDomainId,
    string? DomainName,
    string Name,
    string? Description,
    /// <summary>M12 增量：维度表达式（可分组字段或时间粒度，如 date_trunc('month', created_at)）。</summary>
    string? Expression,
    /// <summary>M12 增量：维度数据类型 / 时间粒度（string/date/month）。</summary>
    string? DataType);
