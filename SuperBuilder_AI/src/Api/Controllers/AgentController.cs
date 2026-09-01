using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Api.Security;
using SuperBuilder_AI.Interfaces.Agent;
using SuperBuilder_AI.Models.Agent;
using SuperBuilder_AI.Services.Agent;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// AI Agent / Copilot 端点（P9.3 AI Agent / Copilot）。
///
/// <para>
/// 提供 Agent 计划（结构化 <see cref="AgentDsl"/> 文档）的租户作用域 CRUD、从自然语言描述生成 Agent、
/// 工具目录、异常原因分析链路，以及编辑器蓝图：
/// <list type="bullet">
/// <item><c>POST /api/agent/plan</c>：从意图编排 Agent（默认路径，确定性、不调 LLM，由 <see cref="IAgentPlanner.PlanFromIntentAsync"/> 完成）。</item>
/// <item><c>POST /api/agent/plan/generate</c>：从自然语言描述生成 Agent（非默认路径，仅显式描述启用 LLM）。</item>
/// <item><c>GET /api/agent/plans</c>：按租户作用域列表（含全局模板 TenantId=0）。</item>
/// <item><c>GET /api/agent/plans/{code}</c>：获取单个 Agent（含完整 DSL）。</item>
/// <item><c>PUT /api/agent/plans/{code}</c>：从意图重新编排（Code 不变；全局模板不可改）。</item>
/// <item><c>DELETE /api/agent/plans/{code}</c>：删除 Agent（全局模板不可删）。</item>
/// <item><c>GET /api/agent/tools</c>：可用工具目录（<see cref="ToolRegistry"/>）。</item>
/// <item><c>GET /api/agent/anomaly-chain</c>：异常原因分析链路（销售额→同比→环比→区域→客户→产品→渠道），确定性、不调 LLM。</item>
/// <item><c>GET /api/agent/plans/blueprint</c>：编辑器蓝图（DSL 骨架 + 枚举清单 + 工具目录），仅 JSON。</item>
/// </list>
/// </para>
///
/// <para>
/// 编排统一委托给 <see cref="IAgentPlanner"/>（P9.2）：<c>CreatePlan</c> / <c>Update</c> 走默认路径
/// <c>PlanFromIntentAsync</c>（确定性、零回归），<c>GeneratePlan</c> 走非默认路径
/// <c>GenerateFromDescriptionAsync</c>（仅显式描述启用 LLM）。传输层不持有 DSL 文档，只把意图/描述交给编排层，
/// 由编排层产出完整 <see cref="AgentPlan"/>（含序列化后的 <see cref="AgentPlan.DslJson"/>）。
/// </para>
///
/// <para>
/// 租户隔离沿用 P4.3 的"显式开启"策略：每个端点内调用 <see cref="SuperBIContext.ApplyTenantScope"/>。
/// 当前平台无 IAM 中间件，租户由 <c>tenantId</c> 查询参数显式传入（默认 0 = 系统/全局视图）。
/// 全局模板（TenantId=0）对所有租户可见但不可被租户修改/删除。Code 在同租户 + 全局范围内唯一。
/// </para>
///
/// <para>本控制器属平台管理面，不触碰 BI 查询链路与 Golden 契约数据，不影响 Golden 18/18 行为契约。</para>
/// </summary>
[ApiController]
[Route("api/agent")]
public sealed class AgentController : ControllerBase
{
	private readonly SuperBIContext _db;
	private readonly IAgentDslSerializer _dslSerializer;
	private readonly IAgentPlanner _planner;

	public AgentController(SuperBIContext db, IAgentDslSerializer dslSerializer, IAgentPlanner planner)
	{
		_db = db;
		_dslSerializer = dslSerializer;
		_planner = planner;
	}

	/// <summary>在当前请求作用域内开启租户隔离，返回解析出的租户 Id。</summary>
	/// <summary>在当前请求作用域内开启租户隔离：有效租户恒为认证租户，跨租户显式请求直接拒绝。</summary>
	private long ScopeTo(long requestedTenantId)
	{
		var resolution = TenantDataPlanePolicy.ResolvePlatformScope(User, requestedTenantId);
		// P0-02B：把解析出的租户上下文写盘，供审计/可观测中间件读取；治理角色管理他租户时另记管理目标
		TenantDataPlanePolicy.StorePlatformScope(HttpContext, resolution, "Agent");
		if (!resolution.Authorized)
			throw new SuperBuilder_AI.Api.Errors.SuperBuilderException(
				SuperBuilder_AI.Api.Errors.ErrorCodes.TenantIsolated,
				"禁止：租户作用域请求只能访问认证租户的数据，跨租户访问被拒绝。",
				403);
		_db.ApplyTenantScope(resolution.EffectiveTenantId);
		return resolution.EffectiveTenantId;
	}

	/// <summary>从意图创建 Agent：默认路径编排（确定性、不调 LLM）。</summary>
	[HttpPost("plan")]
	public async Task<IActionResult> CreatePlan(
		[FromBody] CreateAgentPlanRequest request,
		CancellationToken cancellationToken = default)
	{
		if (request is null) return BadRequest("请求体不能为空。");
		if (request.TenantId <= 0) return BadRequest("不能通过 API 创建全局/内置 Agent（TenantId 必须 > 0）。");
		if (string.IsNullOrWhiteSpace(request.Intent)) return BadRequest("Agent 意图不能为空。");

		// 默认路径：意图 → AgentPlan，确定性、不调 LLM。
		var result = await _planner.PlanFromIntentAsync(request.TenantId, request.Intent, request.Code);
		if (!result.Success || result.Plan is null)
			return BadRequest(new { errors = result.Errors });

		var plan = result.Plan;
		var tenantId = ScopeTo(request.TenantId);
		plan.TenantId = tenantId;

		// 同租户或全局模板已存在该 Code 则冲突。
		var conflict = await _db.AgentPlans
			.IgnoreQueryFilters()
			.AnyAsync(p => p.Code == plan.Code && (p.TenantId == tenantId || p.TenantId == 0), cancellationToken);
		if (conflict) return Conflict(new { errors = new[] { $"Agent 编码已存在：{plan.Code}。" } });

		_db.AgentPlans.Add(plan);
		await _db.SaveChangesAsync(cancellationToken);

		return CreatedAtAction(nameof(GetByCode), new { code = plan.Code, tenantId }, ToDetail(plan));
	}

	/// <summary>
	/// 从自然语言描述生成 Agent（非默认路径）：描述 → Qwen 生成 DSL → 校验 → AgentPlan。
	/// 仅当调用方显式传入描述时才启用 LLM；LLM 失败返回 502，参数错误返回 400。
	/// </summary>
	[HttpPost("plan/generate")]
	public async Task<IActionResult> GeneratePlan(
		[FromBody] GenerateAgentPlanRequest request,
		CancellationToken cancellationToken = default)
	{
		if (request is null) return BadRequest("请求体不能为空。");
		if (request.TenantId <= 0) return BadRequest("生成 Agent 需要 tenantId > 0。");
		if (string.IsNullOrWhiteSpace(request.Description)) return BadRequest("Agent 描述不能为空。");

		// 非默认路径：调用 LLM 生成 DSL JSON，再经 planner 先校验后信任。
		var result = await _planner.GenerateFromDescriptionAsync(request.TenantId, request.Description, request.Code);
		if (!result.Success || result.Plan is null)
		{
			// 失败发生在 LLM 增强路径（UsedAi=true）视为服务端依赖问题；否则参数/结构问题。
			return result.UsedAi
				? StatusCode(StatusCodes.Status502BadGateway, new { errors = result.Errors, usedAi = true })
				: BadRequest(new { errors = result.Errors });
		}

		var plan = result.Plan;
		var tenantId = ScopeTo(request.TenantId);
		plan.TenantId = tenantId;

		var conflict = await _db.AgentPlans
			.IgnoreQueryFilters()
			.AnyAsync(p => p.Code == plan.Code && (p.TenantId == tenantId || p.TenantId == 0), cancellationToken);
		if (conflict) return Conflict(new { errors = new[] { $"Agent 编码已存在：{plan.Code}。" } });

		_db.AgentPlans.Add(plan);
		await _db.SaveChangesAsync(cancellationToken);

		return CreatedAtAction(nameof(GetByCode), new { code = plan.Code, tenantId }, ToDetail(plan));
	}

	/// <summary>列表：按租户作用域返回（含全局模板 TenantId=0）。tenantId=0 为系统视图。</summary>
	[HttpGet("plans")]
	public async Task<IActionResult> List(
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		ScopeTo(tenantId);

		var items = await _db.AgentPlans.AsNoTracking()
			.OrderBy(p => p.TenantId) // 全局(0) 在前，租户自有在后
			.ThenBy(p => p.Code)
			.ToListAsync(cancellationToken);
		return Ok(items.Select(ToSummary).ToList());
	}

	/// <summary>获取单个 Agent（含完整 DSL）。</summary>
	[HttpGet("plans/{code}")]
	public async Task<IActionResult> GetByCode(
		string code,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(code)) return BadRequest("Agent 编码不能为空。");
		ScopeTo(tenantId);

		var entity = await _db.AgentPlans.AsNoTracking()
			.FirstOrDefaultAsync(p => p.Code == code, cancellationToken);
		if (entity is null) return NotFound();
		return Ok(ToDetail(entity));
	}

	/// <summary>从意图重新编排 Agent：重新校验后覆盖文档列（Code 不变；全局模板不可改）。</summary>
	[HttpPut("plans/{code}")]
	public async Task<IActionResult> Update(
		string code,
		[FromBody] UpdateAgentPlanRequest request,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (request is null) return BadRequest("请求体不能为空。");
		if (string.IsNullOrWhiteSpace(request.Intent)) return BadRequest("Agent 意图不能为空。");

		ScopeTo(tenantId);
		var entity = await _db.AgentPlans.FirstOrDefaultAsync(p => p.Code == code, cancellationToken);
		if (entity is null) return NotFound();
		if (entity.TenantId == 0)
			return BadRequest(new { errors = new[] { "全局/内置 Agent 不可修改。" } });

		// 默认路径：用新意图重新编排，确定性、不调 LLM；Code 锁定为原值，仅允许整文档（DSL）覆盖。
		var result = await _planner.PlanFromIntentAsync(entity.TenantId, request.Intent, request.Code);
		if (!result.Success || result.Plan is null)
			return BadRequest(new { errors = result.Errors });

		entity.Name = result.Plan.Name;
		entity.Description = result.Plan.Description;
		entity.DslVersion = result.Plan.DslVersion;
		entity.DslJson = result.Plan.DslJson;
		// Status 保持不变（草稿/发布状态不因改意图而重置）。

		await _db.SaveChangesAsync(cancellationToken);
		return Ok(ToDetail(entity));
	}

	/// <summary>删除 Agent（全局模板不可删）。</summary>
	[HttpDelete("plans/{code}")]
	public async Task<IActionResult> Delete(
		string code,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(code)) return BadRequest("Agent 编码不能为空。");
		ScopeTo(tenantId);
		var entity = await _db.AgentPlans.FirstOrDefaultAsync(p => p.Code == code, cancellationToken);
		if (entity is null) return NotFound();
		if (entity.TenantId == 0)
			return BadRequest(new { errors = new[] { "全局/内置 Agent 不可删除。" } });

		_db.AgentPlans.Remove(entity);
		await _db.SaveChangesAsync(cancellationToken);
		return NoContent();
	}

	/// <summary>
	/// 工具目录：返回 <see cref="ToolRegistry"/> 中全部可用工具描述（名称、描述、分类、是否依赖业务实体）。
	/// 纯结构化数据、不调 LLM、无租户维度。
	/// </summary>
	[HttpGet("tools")]
	public IActionResult ToolsCatalog()
		=> Ok(ToolRegistry.GetAll());

	/// <summary>
	/// 异常原因分析链路（确定性，不调 LLM）：沿 销售额→同比→环比→区域→客户→产品→渠道 展开 6 步归因链。
	/// <paramref name="metric"/> 为异常指标名（如 销售额），<paramref name="entity"/> 为关联业务实体（可选）。
	/// </summary>
	[HttpGet("anomaly-chain")]
	public IActionResult AnomalyChain([FromQuery] string? metric = null, [FromQuery] string? entity = null)
	{
		var steps = ToolRegistry.BuildAnomalyChain(metric, entity);
		return Ok(new AgentAnomalyChainResponse(
			Metric: string.IsNullOrWhiteSpace(metric) ? "指标" : metric.Trim(),
			Entity: string.IsNullOrWhiteSpace(entity) ? null : entity.Trim(),
			Steps: steps.ToList()));
	}

	/// <summary>
	/// 编辑器蓝图：返回 AgentDsl 骨架与可用枚举/工具清单（工具类型/分析维度/分析方向），供编辑器初始化。
	/// 仅结构化 JSON，绝不承载 HTML。
	/// </summary>
	[HttpGet("plans/blueprint")]
	public IActionResult EditorBlueprint()
	{
		var skeleton = new AgentDsl
		{
			Version = AgentDslVersions.Current,
			Code = "my-agent",
			Name = "新分析 Agent",
			Description = "由意图自动生成的 Agent 执行计划示例。",
			UserRequest = "分析本月销售额为什么下降",
			SelectedTools = new List<AgentToolSelection>
			{
				new() { Tool = AgentTools.Query, Order = 1, Reason = "计算销售额指标" },
				new() { Tool = AgentTools.Dashboard, Order = 2, Reason = "可视化下钻结果" },
			},
			// 蓝图自带一条示例异常归因链，编辑器可直接看到结构。
			AnomalyChain = ToolRegistry.BuildAnomalyChain("销售额").ToList(),
		};

		return Ok(new AgentEditorBlueprint(
			DslVersion: AgentDslVersions.Current,
			Tools: AgentTools.Supported,
			Dimensions: AnalysisDimensions.Supported,
			Directions: AnalysisDirections.Supported,
			ToolCatalog: ToolRegistry.GetAll(),
			Skeleton: _dslSerializer.Serialize(skeleton)));
	}

	private static AgentSummary ToSummary(AgentPlan p) =>
		new(p.Id, p.TenantId, p.Code, p.Name, p.Description, p.Status, p.DslVersion);

	private static AgentDetail ToDetail(AgentPlan p) =>
		new(p.Id, p.TenantId, p.Code, p.Name, p.Description, p.Status, p.DslVersion, p.DslJson);

	#region Request / Response DTOs
	/// <summary>从意图创建 Agent 请求体（默认路径，确定性、不调 LLM）。</summary>
	public sealed record CreateAgentPlanRequest(
		long TenantId,
		string Intent,
		string? Code = null);

	/// <summary>从描述生成 Agent 请求体（非默认路径，启用 LLM）。</summary>
	public sealed record GenerateAgentPlanRequest(
		long TenantId,
		string Description,
		string? Code = null);

	/// <summary>从意图更新 Agent 请求体（Code 不可变，仅重新编排 DSL 文档）。</summary>
	public sealed record UpdateAgentPlanRequest(
		string Intent,
		string? Code = null);

	/// <summary>Agent 摘要 DTO。</summary>
	public sealed record AgentSummary(
		long Id,
		long TenantId,
		string Code,
		string Name,
		string? Description,
		string Status,
		string DslVersion);

	/// <summary>Agent 详情 DTO（含完整 DSL）。</summary>
	public sealed record AgentDetail(
		long Id,
		long TenantId,
		string Code,
		string Name,
		string? Description,
		string Status,
		string DslVersion,
		string DslJson);

	/// <summary>异常原因分析链路响应 DTO（确定性、结构化、无 HTML）。</summary>
	public sealed record AgentAnomalyChainResponse(
		string Metric,
		string? Entity,
		IReadOnlyList<AnalysisStep> Steps);

	/// <summary>编辑器蓝图 DTO（结构化，无 HTML）。</summary>
	public sealed record AgentEditorBlueprint(
		string DslVersion,
		IReadOnlyList<string> Tools,
		IReadOnlyList<string> Dimensions,
		IReadOnlyList<string> Directions,
		IReadOnlyList<AgentToolDescriptor> ToolCatalog,
		string Skeleton);
	#endregion
}
