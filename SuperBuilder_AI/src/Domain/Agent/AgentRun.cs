using System;
using System.Collections.Generic;
using System.Text.Json;
using SuperBuilder_AI.Models;

namespace SuperBuilder_AI.Models.Agent;

/// <summary>
/// Agent 运行（M7-03 Agent Runtime）状态常量。
///
/// <para>状态机：</para>
/// <list type="bullet">
/// <item><see cref="Queued"/> → <see cref="Running"/> → <see cref="Succeeded"/>（全部步骤成功）。</item>
/// <item><see cref="Running"/> 中遇需审批工具 → <see cref="ApprovalPending"/>（挂起，等待人工审批）。</item>
/// <item><see cref="ApprovalPending"/> → <see cref="Approved"/>（恢复执行）→ <see cref="Succeeded"/>。</item>
/// <item><see cref="ApprovalPending"/> → <see cref="Rejected"/>（终止）。</item>
/// <item>权限不足或不可重试错误 → <see cref="Failed"/>。</item>
/// </list>
/// </summary>
public static class AgentRunStatuses
{
	/// <summary>已入队，尚未开始。</summary>
	public const string Queued = "queued";

	/// <summary>执行中。</summary>
	public const string Running = "running";

	/// <summary>全部步骤成功。</summary>
	public const string Succeeded = "succeeded";

	/// <summary>失败（权限不足或不可重试错误）。</summary>
	public const string Failed = "failed";

	/// <summary>等待人工审批（卡在某个需审批的工具）。</summary>
	public const string ApprovalPending = "approval_pending";

	/// <summary>已审批通过，恢复执行。</summary>
	public const string Approved = "approved";

	/// <summary>审批被拒绝，运行终止。</summary>
	public const string Rejected = "rejected";

	/// <summary>受支持的状态集合。</summary>
	public static IReadOnlyList<string> Supported { get; } =
		new[] { Queued, Running, Succeeded, Failed, ApprovalPending, Approved, Rejected };
}

/// <summary>
/// Agent 运行记录（M7-03 Agent Runtime）。
///
/// <para>持久化策略：与 Dashboard/App 一致，运行本身仅一张表；每步执行结果以 JSON 数组
/// （<see cref="StepLogJson"/>，受控执行信封）整体存储，不另建步骤关系表。</para>
/// </summary>
public class AgentRun : BaseEntity
{
	/// <summary>所属租户 Id；运行恒归属某一租户，不存在全局模板。</summary>
	public long TenantId { get; set; }

	/// <summary>被执行的 Agent 编码（同 <see cref="AgentPlan.Code"/>）。</summary>
	public string PlanCode { get; set; } = string.Empty;

	/// <summary>运行状态，取值见 <see cref="AgentRunStatuses"/>。</summary>
	public string Status { get; set; } = AgentRunStatuses.Queued;

	/// <summary>当前执行到的步骤顺序（对应 <see cref="AgentToolSelection.Order"/>）。</summary>
	public int CurrentStepOrder { get; set; }

	/// <summary>触发者标识（认证主体；无认证回退 system）。</summary>
	public string? Actor { get; set; }

	/// <summary>本次运行被授予的工具集合（JSON 数组），用于审批恢复时复用，deny-by-default 基线。</summary>
	public string? GrantedToolsJson { get; set; }

	/// <summary>运行级审批是否通过（通过后后续需审批工具不再卡审批闸门）。</summary>
	public bool Approved { get; set; }

	/// <summary>开始时间（UTC）。</summary>
	public DateTime? StartedAt { get; set; }

	/// <summary>结束时间（UTC）；审批挂起时为空。</summary>
	public DateTime? FinishedAt { get; set; }

	/// <summary>结果摘要（成功步骤数 / 失败原因等）。</summary>
	public string? ResultSummary { get; set; }

	/// <summary>每步执行结果（JSON 数组，受控执行信封）。</summary>
	public string? StepLogJson { get; set; }

	/// <summary>反序列化每步执行记录（无则返回空列表）。</summary>
	public IReadOnlyList<AgentRunStepRecord> GetStepLog()
	{
		if (string.IsNullOrWhiteSpace(StepLogJson))
			return Array.Empty<AgentRunStepRecord>();
		try
		{
			return JsonSerializer.Deserialize<List<AgentRunStepRecord>>(StepLogJson)
				?? (IReadOnlyList<AgentRunStepRecord>)Array.Empty<AgentRunStepRecord>();
		}
		catch (JsonException)
		{
			return Array.Empty<AgentRunStepRecord>();
		}
	}
}

/// <summary>单步执行记录（M7-03，受控执行信封）。</summary>
public sealed record AgentRunStepRecord(
	/// <summary>步骤顺序（对应 AgentToolSelection.Order）。</summary>
	int Order,
	/// <summary>工具类型。</summary>
	string Tool,
	/// <summary>工具风险等级（safe/read/write）。</summary>
	string Risk,
	/// <summary>是否执行成功。</summary>
	bool Succeeded,
	/// <summary>实际尝试次数（含重试）。</summary>
	int Attempts,
	/// <summary>执行输出（JSON 字符串；受控信封为确定性说明）。</summary>
	string? Output,
	/// <summary>失败原因（成功时为空）。</summary>
	string? Error,
	/// <summary>执行模式：controlled=受控信封（M7-03）；live=真实后端（M7-04+）。</summary>
	string Mode,
	/// <summary>是否因需审批而挂起（本步未真正执行）。</summary>
	bool PendingApproval = false);
