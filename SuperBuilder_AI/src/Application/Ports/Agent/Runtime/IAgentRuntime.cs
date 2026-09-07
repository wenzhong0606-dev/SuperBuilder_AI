using SuperBuilder_AI.Models.Agent;

namespace SuperBuilder_AI.Interfaces.Agent.Runtime;

/// <summary>
/// Agent 运行时端口（M7-03）：编排受控工具执行、维护运行状态机、施加权限与审批闸门。
///
/// <para>职责：</para>
/// <list type="bullet">
/// <item><see cref="StartRunAsync"/>：加载 Agent DSL → 按序执行 SelectedTools → 每步做权限校验、
/// （必要时）审批挂起、带重试执行 → 持久化 <see cref="AgentRun"/> 状态与每步信封。</item>
/// <item><see cref="ApproveAsync"/> / <see cref="RejectAsync"/>：处理审批挂起的运行（恢复或终止）。</item>
/// </list>
///
/// <para>本端口不触碰 Golden 契约数据，不影响 BI 查询链路；执行信封为受控（controlled）模式，
/// 真实后端接入由 M7-04 完成。</para>
/// </summary>
public interface IAgentRuntime
{
	/// <summary>
	/// 启动一次 Agent 运行。
	/// </summary>
	/// <param name="tenantId">所属租户（生效租户，已通过数据面策略校验）。</param>
	/// <param name="planCode">Agent 编码。</param>
	/// <param name="actor">触发者标识；为空时由调用方回退 system。</param>
	/// <param name="grantedTools">本次调用方被授予的工具集合；为空则采用运行时默认基线（仅 Safe）。</param>
	/// <param name="cancellationToken">取消令牌。</param>
	/// <returns>已持久化的运行记录（含实时状态）。</returns>
	/// <exception cref="AgentRuntimeException">Agent 不存在或 DSL 无法解析。</exception>
	Task<AgentRun> StartRunAsync(
		long tenantId,
		string planCode,
		string? actor = null,
		IReadOnlySet<string>? grantedTools = null,
		CancellationToken cancellationToken = default);

	/// <summary>审批通过挂起的运行，恢复执行剩余步骤。</summary>
	Task<AgentRun> ApproveAsync(long tenantId, long runId, CancellationToken cancellationToken = default);

	/// <summary>拒绝挂起的运行，终止。</summary>
	Task<AgentRun> RejectAsync(long tenantId, long runId, CancellationToken cancellationToken = default);
}
