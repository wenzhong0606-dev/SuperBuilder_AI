using SuperBuilder_AI.Models.Agent;

namespace SuperBuilder_AI.Interfaces.Agent;

/// <summary>
/// Agent 编排端口（P9.2 AI Agent / Copilot）。
///
/// <para>
/// 职责：把<strong>自然语言意图</strong>或<strong>结构化 DSL</strong> 编排为可持久化的
/// <see cref="AgentPlan"/> 实体。编排层不负责执行（那是后续执行器的职责），也不负责持久化
/// （那是 P9.3 控制器 / 仓储的职责）。
/// </para>
///
/// <para>
/// <strong>零回归门控（与 P5/P6/P8 同款手法）</strong>：
/// <see cref="PlanFromIntentAsync"/> 是<em>默认路径</em>，纯确定性、不触碰任何 LLM，
/// 行为逐字节稳定（同一意图永远产出同一计划）；<see cref="GenerateFromDescriptionAsync"/> 是
/// <em>非默认路径</em>，仅当调用方显式传入自然语言描述时才启用 LLM。两条路径都不触碰 Golden 依赖文件。
/// </para>
/// </summary>
public interface IAgentPlanner
{
	/// <summary>
	/// 默认路径：把用户意图（自然语言或关键词）编排为 <see cref="AgentPlan"/>。
	/// 通过 <see cref="ToolRegistry"/> 做<strong>确定性</strong>工具选择，并据意图是否含异常分析信号
	/// 决定是否附加异常原因分析链路（销售额→同比→环比→区域→客户→产品→渠道）。不调用 LLM，零回归。
	/// </summary>
	/// <param name="tenantId">目标租户（0 表示全局模板）。</param>
	/// <param name="intent">用户原始意图（自然语言或关键词）。</param>
	/// <param name="code">可选业务编码；为空时按意图生成 slug。</param>
	/// <returns>编排结果；校验失败则 <see cref="AgentResult.Success"/> 为 false。</returns>
	Task<AgentResult> PlanFromIntentAsync(long tenantId, string intent, string? code = null);

	/// <summary>
	/// 非默认路径：把自然语言描述编排为 <see cref="AgentPlan"/>。
	/// 通过 <c>IQwenService</c> 生成 DSL JSON，再经 <see cref="IAgentDslSerializer"/> 校验后落地。
	/// </summary>
	/// <param name="tenantId">目标租户（0 表示全局模板）。</param>
	/// <param name="description">用户自然语言描述（如"分析销售额为什么下降"）。</param>
	/// <param name="code">可选业务编码；为空时按描述生成 slug。</param>
	/// <returns>编排结果；<see cref="AgentResult.UsedAi"/> 为 true，失败时给出 LLM 输出无法解析的原因。</returns>
	Task<AgentResult> GenerateFromDescriptionAsync(long tenantId, string description, string? code = null);
}
