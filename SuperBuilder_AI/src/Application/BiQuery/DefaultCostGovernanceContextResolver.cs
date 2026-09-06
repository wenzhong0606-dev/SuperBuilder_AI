using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 默认成本治理上下文解析器（M5-06）。
///
/// 从执行身份读取租户；特权角色豁免留作治理扩展点（默认不豁免）。
/// </summary>
public sealed class DefaultCostGovernanceContextResolver : ICostGovernanceContextResolver
{
	private readonly IDataSourceExecutionIdentityAccessor _identity;

	/// <summary>创建默认上下文解析器。</summary>
	public DefaultCostGovernanceContextResolver(IDataSourceExecutionIdentityAccessor identity)
	{
		_identity = identity ?? throw new System.ArgumentNullException(nameof(identity));
	}

	/// <inheritdoc />
	public Task<CostGovernanceContext> ResolveAsync(
		QueryPlanPipelineContext ctx,
		CancellationToken ct = default)
	{
		var caller = _identity.Current;
		var tenantId = caller?.TenantId ?? 0;
		// 特权角色豁免留作治理扩展点（默认不豁免）。
		var bypass = false;
		return Task.FromResult(new CostGovernanceContext(tenantId, bypass));
	}
}
