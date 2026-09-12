namespace SuperBuilder_AI.Interfaces.Agent.Runtime;

/// <summary>
/// 受控工具端口（M7-03 Agent Runtime）。
///
/// <para>契约边界：每个工具只负责<strong>自身</strong>的确定性、受控执行，不持有编排/权限/状态逻辑。
/// 风险等级由各工具声明，运行时据此施加权限与审批闸门。M7-03 的工具实现为<strong>受控信封</strong>
/// （<see cref="ToolResult.Mode"/> = controlled），真实后端接入见 M7-04。</para>
/// </summary>
public interface ITool
{
	/// <summary>工具类型，取值见 <see cref="AgentTools"/> 常量。</summary>
	string Tool { get; }

	/// <summary>风险等级，决定权限与审批要求。</summary>
	ToolRisk Risk { get; }

	/// <summary>是否需人工审批（<see cref="ToolRisk.Write"/> 为 true）。</summary>
	bool RequiresApproval { get; }

	/// <summary>后端连接状态：<c>live</c>=已接真实后端（可真实执行）；<c>pending</c>=诚实受控信封（未接真实后端，运行时拒绝执行，杜绝假成功）。默认 <c>live</c>。</summary>
	string BackendStatus => "live";

	/// <summary>执行工具（受控信封）。瞬态失败抛 <see cref="TransientToolException"/> 以触发重试。</summary>
	Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken cancellationToken = default);
}
