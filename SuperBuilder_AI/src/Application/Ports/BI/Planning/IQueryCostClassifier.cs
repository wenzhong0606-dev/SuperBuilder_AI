using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces.BI.Planning;

/// <summary>
/// 查询成本分类器（M5-06）。
///
/// 从 QueryPlan 抽取结构性成本信号（JOIN 数、无界性、请求行数上限、
/// 模型成本评级），不含阈值判断；阈值判定交由 <see cref="ICostGovernancePolicy"/>。
/// </summary>
public interface IQueryCostClassifier
{
	/// <summary>
	/// 评估查询成本原始信号。
	/// </summary>
	Task<QueryCostAssessment> AssessAsync(
		QueryPlan plan,
		QueryPlanConfidence? confidence,
		CancellationToken ct = default);
}
