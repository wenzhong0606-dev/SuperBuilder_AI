namespace SuperBuilder_AI.Interfaces.Agent.Runtime;

/// <summary>
/// 工具风险等级（M7-03 Agent Runtime）。决定权限要求与审批要求。
/// <list type="bullet">
/// <item><see cref="Safe"/>：只读自省，无外部副作用（如元数据/语义探查），默认授权、无需审批。</item>
/// <item><see cref="Read"/>：需权限授权但不需人工审批（如查询/看板/预测）。</item>
/// <item><see cref="Write"/>：需权限授权且需人工审批（如报表/告警/流程编排）。</item>
/// </list>
/// </summary>
public enum ToolRisk
{
	/// <summary>安全：只读自省，无外部副作用。</summary>
	Safe = 0,

	/// <summary>读：需权限授权，不须审批。</item>
	Read = 1,

	/// <summary>写：需权限授权且需人工审批。</summary>
	Write = 2,
}

/// <summary>工具执行上下文（M7-03）。</summary>
public sealed record ToolContext(
	/// <summary>所属租户 Id。</summary>
	long TenantId,
	/// <summary>执行该工具的 Agent 编码。</summary>
	string PlanCode,
	/// <summary>触发者标识。</summary>
	string? Actor,
	/// <summary>工具参数（键值对）。</summary>
	IReadOnlyDictionary<string, string> Parameters,
	/// <summary>关联 Id（用于审计/链路追踪）。</summary>
	string CorrelationId);

/// <summary>
/// 工具执行结果（M7-03 受控执行信封）。
/// <para>输出一律为 JSON 字符串；受控阶段 <see cref="Mode"/> 为 <c>controlled</c>，
/// 明确标识后端尚未接入（M7-04 翻为 <c>live</c>），绝不伪造成功。</para>
/// </summary>
public sealed record ToolResult(
	/// <summary>是否执行成功（信封执行成功，不代表真实业务结果）。</summary>
	bool Succeeded,
	/// <summary>执行输出（JSON 字符串）。</summary>
	string? Output,
	/// <summary>失败原因（成功时为空）。</summary>
	string? Error,
	/// <summary>本工具是否需要人工审批。</summary>
	bool RequiresApproval,
	/// <summary>执行模式：controlled=受控信封；live=真实后端。</summary>
	string Mode)
{
	/// <summary>成功工厂。</summary>
	public static ToolResult Ok(string output, string mode, bool requiresApproval = false)
		=> new(true, output, null, requiresApproval, mode);

	/// <summary>失败工厂。</summary>
	public static ToolResult Fail(string error, string mode, bool requiresApproval = false)
		=> new(false, null, error, requiresApproval, mode);
}

/// <summary>瞬态失败：重试策略仅对本异常重试（M7-03）。</summary>
public sealed class TransientToolException : Exception
{
	/// <summary>构造瞬态失败。</summary>
	public TransientToolException(string message) : base(message) { }
}
