using System.Threading;
using System.Threading.Tasks;

namespace SuperBuilder_AI.Interfaces.BI.Planning;

/// <summary>
/// QueryPlan 编排管线的单个阶段（M5-03）。
///
/// 每个阶段消费并（按需）改写 <see cref="QueryPlanPipelineContext"/>：
/// 读取前置阶段产出的字段、写入自身产出；
/// 在验证失败 / 安全拦截时设置 <see cref="QueryPlanPipelineContext.EarlyResponse"/>
/// 以令管线短路返回（与重构前 Step 4 / Step 5.1 / Step 5.4 的提前返回语义一致）。
/// </summary>
public interface IQueryPlanStage
{
	/// <summary>阶段名称（用于诊断 / 日志）。</summary>
	string StageName { get; }

	/// <summary>
	/// 执行本阶段。实现应改写 <paramref name="ctx"/> 以传递状态；
	/// 需要提前结束时设置 <see cref="QueryPlanPipelineContext.EarlyResponse"/>。
	/// </summary>
	Task ExecuteAsync(
		QueryPlanPipelineContext ctx,
		CancellationToken ct = default);
}
