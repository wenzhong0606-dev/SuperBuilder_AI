using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces.BI.Planning;

/// <summary>
/// 成本治理上下文解析器（M5-06）：从执行身份解析租户与特权豁免信息。
/// </summary>
public interface ICostGovernanceContextResolver
{
	/// <summary>
	/// 解析当前请求的成本治理上下文。
	/// </summary>
	Task<CostGovernanceContext> ResolveAsync(
		QueryPlanPipelineContext ctx,
		CancellationToken ct = default);
}
