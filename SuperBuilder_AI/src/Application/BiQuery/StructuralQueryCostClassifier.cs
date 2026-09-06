using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 结构性查询成本分类器（M5-06 默认实现）。
///
/// 仅基于 QueryPlan 的确定性结构信号（JOIN 数、无界性、请求行数上限）评估，
/// 模型成本评级默认 Low（未接入 LLM 成本遥测）。无外部依赖、无默认副作用。
/// </summary>
public sealed class StructuralQueryCostClassifier : IQueryCostClassifier
{
	/// <inheritdoc />
	public Task<QueryCostAssessment> AssessAsync(
		QueryPlan plan,
		QueryPlanConfidence? confidence,
		CancellationToken ct = default)
	{
		var joinCount = plan.Joins?.Count ?? 0;
		var tableCount = 1 + joinCount;
		var requestedLimit = plan.Limit ?? plan.Intent?.Limit;
		var isUnbounded = !requestedLimit.HasValue;

		return Task.FromResult(new QueryCostAssessment
		{
			JoinCount = joinCount,
			TableCount = tableCount,
			IsUnbounded = isUnbounded,
			RequestedLimit = requestedLimit,
			ModelCostTier = ModelCostTier.Low
		});
	}
}
