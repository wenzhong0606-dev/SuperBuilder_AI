using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Agent;
using SuperBuilder_AI.Interfaces.Agent.Runtime;
using SuperBuilder_AI.Models.Agent;

namespace SuperBuilder_AI.Services.Agent.Runtime;

/// <summary>
/// Agent 运行时（M7-03）：编排受控工具执行、维护运行状态机、施加权限与审批闸门。
///
/// <para>执行契约：</para>
/// <list type="bullet">
/// <item>按 <see cref="AgentToolSelection.Order"/> 升序执行 Agent DSL 的 <c>SelectedTools</c>。</item>
/// <item>每步先过<strong>权限闸门</strong>（deny-by-default）；未授权 → <see cref="AgentRunStatuses.Failed"/>。</item>
/// <item>需审批且运行级未通过 → 进入 <see cref="AgentRunStatuses.ApprovalPending"/> 并挂起，等待 Approve/Reject。</item>
/// <item>执行带<strong>重试</strong>（仅 <see cref="TransientToolException"/> 重试）。</item>
/// <item>每步结果以受控信封（<see cref="AgentRunStepRecord"/>）持久化到 <see cref="AgentRun.StepLogJson"/>。</item>
/// </list>
///
/// <para>本运行时执行信封恒为 controlled 模式，不触碰真实后端、不声称业务结果；
/// 真实后端与权限来源（RBAC）接入见 M7-04 及后续里程碑。</para>
/// </summary>
public sealed class AgentRuntime : IAgentRuntime
{
	// 默认授权基线：仅 Safe 级工具（deny-by-default 的安全下限）。
	private static readonly IReadOnlySet<string> DefaultGranted =
		new HashSet<string>(StringComparer.OrdinalIgnoreCase) { AgentTools.Metadata, AgentTools.Semantic };

	private readonly SuperBIContext _db;
	private readonly IAgentDslSerializer _dslSerializer;
	private readonly IToolCatalog _catalog;
	private readonly IToolPermissionPolicy _policy;
	private readonly IRetryPolicy _retry;

	/// <summary>构造 Agent 运行时。</summary>
	public AgentRuntime(
		SuperBIContext db,
		IAgentDslSerializer dslSerializer,
		IToolCatalog catalog,
		IToolPermissionPolicy policy,
		IRetryPolicy retry)
	{
		_db = db;
		_dslSerializer = dslSerializer;
		_catalog = catalog;
		_policy = policy;
		_retry = retry;
	}

	/// <inheritdoc />
	public async Task<AgentRun> StartRunAsync(
		long tenantId,
		string planCode,
		string? actor = null,
		IReadOnlySet<string>? grantedTools = null,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(planCode))
			throw new AgentRuntimeException("Agent 编码不能为空。");

		var plan = await _db.AgentPlans.FirstOrDefaultAsync(p => p.Code == planCode, cancellationToken);
		if (plan is null)
			throw new AgentRuntimeException($"Agent 不存在：{planCode}", 404);

		if (!_dslSerializer.TryDeserialize(plan.DslJson, out var dsl, out var errors) || dsl is null)
			throw new AgentRuntimeException("Agent DSL 无法解析：" + string.Join("; ", errors), 422);

		var granted = grantedTools ?? DefaultGranted;
		var run = new AgentRun
		{
			TenantId = tenantId,
			PlanCode = planCode,
			Status = AgentRunStatuses.Queued,
			Actor = actor,
			StartedAt = DateTime.UtcNow,
			GrantedToolsJson = JsonSerializer.Serialize(granted.ToList()),
		};
		_db.AgentRuns.Add(run);
		await _db.SaveChangesAsync(cancellationToken);

		return await ExecuteRunAsync(run, dsl, granted, fromStep: null, cancellationToken);
	}

	/// <inheritdoc />
	public async Task<AgentRun> ApproveAsync(long tenantId, long runId, CancellationToken cancellationToken = default)
	{
		var run = await _db.AgentRuns.FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
		if (run is null)
			throw new AgentRuntimeException($"运行不存在：{runId}", 404);
		if (run.TenantId != tenantId)
			throw new AgentRuntimeException("禁止：运行不属于当前租户。", 403);
		if (run.Status != AgentRunStatuses.ApprovalPending)
			throw new AgentRuntimeException($"运行不在待审批状态（当前：{run.Status}）。", 409);

		var granted = ReadGranted(run);
		var plan = await _db.AgentPlans.FirstOrDefaultAsync(p => p.Code == run.PlanCode, cancellationToken)
			?? throw new AgentRuntimeException($"Agent 不存在：{run.PlanCode}", 404);
		if (!_dslSerializer.TryDeserialize(plan.DslJson, out var dsl, out _))
			throw new AgentRuntimeException("Agent DSL 无法解析。", 422);

		run.Approved = true;
		run.Status = AgentRunStatuses.Approved;
		await _db.SaveChangesAsync(cancellationToken);

		// 从挂起步骤（含该步）恢复执行。
		return await ExecuteRunAsync(run, dsl!, granted, fromStep: run.CurrentStepOrder, cancellationToken);
	}

	/// <inheritdoc />
	public async Task<AgentRun> RejectAsync(long tenantId, long runId, CancellationToken cancellationToken = default)
	{
		var run = await _db.AgentRuns.FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
		if (run is null)
			throw new AgentRuntimeException($"运行不存在：{runId}", 404);
		if (run.TenantId != tenantId)
			throw new AgentRuntimeException("禁止：运行不属于当前租户。", 403);
		if (run.Status != AgentRunStatuses.ApprovalPending)
			throw new AgentRuntimeException($"运行不在待审批状态（当前：{run.Status}）。", 409);

		run.Status = AgentRunStatuses.Rejected;
		run.FinishedAt = DateTime.UtcNow;
		run.ResultSummary = "审批被拒绝，运行终止。";
		await _db.SaveChangesAsync(cancellationToken);
		return run;
	}

	/// <summary>执行工具序列（从 <paramref name="fromStep"/> 起，null 表示从头）。</summary>
	private async Task<AgentRun> ExecuteRunAsync(
		AgentRun run,
		AgentDsl dsl,
		IReadOnlySet<string> granted,
		int? fromStep,
		CancellationToken cancellationToken)
	{
		run.Status = AgentRunStatuses.Running;
		var steps = dsl.SelectedTools.OrderBy(t => t.Order).ToList();
		var log = run.GetStepLog().ToList();

		foreach (var step in steps)
		{
			if (fromStep.HasValue && step.Order < fromStep.Value)
				continue; // 已执行过的步骤跳过（审批恢复场景）

			run.CurrentStepOrder = step.Order;

			var tool = _catalog.Get(step.Tool);
			if (tool is null)
			{
				log.Add(new AgentRunStepRecord(step.Order, step.Tool, "unknown", false, 0, null, $"未知工具：{step.Tool}", "controlled"));
				return await FailRunAsync(run, log, $"未知工具：{step.Tool}", cancellationToken);
			}

			// 权限闸门：deny-by-default
			if (!_policy.IsAllowed(step.Tool, granted))
			{
				log.Add(new AgentRunStepRecord(step.Order, step.Tool, tool.Risk.ToString().ToLowerInvariant(), false, 0, null, "权限不足：工具未被授予", "controlled"));
				return await FailRunAsync(run, log, $"权限不足：工具 {step.Tool} 未被授予", cancellationToken);
			}

			// 审批闸门：需审批且运行级尚未通过 → 挂起
			if (tool.RequiresApproval && !run.Approved)
			{
				log.Add(new AgentRunStepRecord(step.Order, step.Tool, tool.Risk.ToString().ToLowerInvariant(), false, 0, null, null, "controlled", PendingApproval: true));
				run.StepLogJson = JsonSerializer.Serialize(log);
				run.Status = AgentRunStatuses.ApprovalPending;
				run.ResultSummary = $"工具 {step.Tool} 需要人工审批，运行已挂起。";
				await _db.SaveChangesAsync(cancellationToken);
				return run;
			}

			// 执行（带重试）
			try
			{
				var ctx = new ToolContext(run.TenantId, run.PlanCode, run.Actor, step.Parameters, Correlation(run));
				var outcome = await _retry.ExecuteAsync(_ => tool.ExecuteAsync(ctx, cancellationToken), cancellationToken);
				var res = outcome.Result;
				log.Add(new AgentRunStepRecord(step.Order, step.Tool, tool.Risk.ToString().ToLowerInvariant(), res.Succeeded, outcome.Attempts, res.Output, res.Error, res.Mode));
				if (!res.Succeeded)
					return await FailRunAsync(run, log, $"工具 {step.Tool} 执行失败：{res.Error}", cancellationToken);
			}
			catch (Exception ex)
			{
				log.Add(new AgentRunStepRecord(step.Order, step.Tool, tool.Risk.ToString().ToLowerInvariant(), false, 0, null, ex.Message, "controlled"));
				return await FailRunAsync(run, log, $"工具 {step.Tool} 执行异常：{ex.Message}", cancellationToken);
			}
		}

		run.Status = AgentRunStatuses.Succeeded;
		run.FinishedAt = DateTime.UtcNow;
		run.ResultSummary = $"完成 {log.Count} 步。";
		run.StepLogJson = JsonSerializer.Serialize(log);
		await _db.SaveChangesAsync(cancellationToken);
		return run;
	}

	private async Task<AgentRun> FailRunAsync(
		AgentRun run,
		List<AgentRunStepRecord> log,
		string summary,
		CancellationToken cancellationToken)
	{
		run.Status = AgentRunStatuses.Failed;
		run.FinishedAt = DateTime.UtcNow;
		run.ResultSummary = summary;
		run.StepLogJson = JsonSerializer.Serialize(log);
		await _db.SaveChangesAsync(cancellationToken);
		return run;
	}

	private static IReadOnlySet<string> ReadGranted(AgentRun run)
	{
		if (string.IsNullOrWhiteSpace(run.GrantedToolsJson))
			return DefaultGranted;
		try
		{
			var list = JsonSerializer.Deserialize<List<string>>(run.GrantedToolsJson);
			return list is null
				? DefaultGranted
				: new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
		}
		catch (JsonException)
		{
			return DefaultGranted;
		}
	}

	private static string Correlation(AgentRun run) => $"run:{run.Id}:{run.PlanCode}";
}
