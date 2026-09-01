using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces.BI.Planning;

/// <summary>SQL 生成前的最终查询计划安全闸门。</summary>
public interface IQueryPlanSecurityGate
{
	Task ValidateAsync(QueryPlan plan, long tenantId, long userId, CancellationToken ct = default);
}
