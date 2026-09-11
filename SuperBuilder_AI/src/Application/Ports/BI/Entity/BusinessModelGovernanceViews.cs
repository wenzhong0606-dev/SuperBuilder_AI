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
    int PhysicalBindingCount);

/// <summary>M12-16 指标中心：维度治理视图行（只读投影）。</summary>
public sealed record BusinessDimensionView(
    long Id,
    long BusinessDomainId,
    string? DomainName,
    string Name,
    string? Description);
