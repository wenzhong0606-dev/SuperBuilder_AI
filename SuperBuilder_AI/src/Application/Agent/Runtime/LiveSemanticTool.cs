using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Agent.Runtime;
using SuperBuilder_AI.Models.Agent;
using SuperBuilder_AI.Models.Localization;

namespace SuperBuilder_AI.Services.Agent.Runtime;

/// <summary>
/// 语义解析工具（M7-04 live）：真实查询租户（含全局共享）语义标签，
/// 产出 <c>Mode=live</c> 信封并返回真实概念→标签/同义词映射。只读、确定性、不调 LLM、无外部副作用。
/// <para>这是 M7-03 受控信封所声明的"真实后端将于 M7-04 接入"的落地：不再回执占位，而是返回真实标签。</para>
/// </summary>
public sealed class LiveSemanticTool : ITool
{
	private readonly SuperBIContext _db;

	/// <summary>构造 live 语义工具（依赖作用域内的 <see cref="SuperBIContext"/>）。</summary>
	public LiveSemanticTool(SuperBIContext db) => _db = db;

	/// <inheritdoc />
	public string Tool => AgentTools.Semantic;

	/// <inheritdoc />
	public ToolRisk Risk => ToolRisk.Safe;

	/// <inheritdoc />
	public bool RequiresApproval => false;

	/// <summary>后端连接状态：已接真实后端（live）。</summary>
	public string BackendStatus => "live";

	/// <summary>展示名。</summary>
	public string DisplayName => "语义解析";

	/// <inheritdoc />
	public async Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct = default)
	{
		// 显式施加租户作用域（与主流程共享同一作用域实例，幂等）；
		// SemanticLabels 的全局过滤器含 TenantId==0 的全局共享标签，故返回"租户+全局"语义标签（符合语义召回语义）。
		_db.ApplyTenantScope(context.TenantId);

		var labels = await _db.SemanticLabels.AsNoTracking()
			.OrderBy(l => l.ConceptType).ThenBy(l => l.ConceptId).ThenBy(l => l.SortOrder)
			.ToListAsync(ct);

		var concepts = labels
			.GroupBy(l => new { l.ConceptType, l.ConceptId })
			.Select(g => new
			{
				conceptType = g.Key.ConceptType,
				conceptId = g.Key.ConceptId,
				labels = g.Select(l => new { l.Culture, l.LabelKind, l.Value }).ToList(),
			}).ToList();

		var output = JsonSerializer.Serialize(new
		{
			tool = Tool,
			displayName = DisplayName,
			mode = "live",
			connected = true,
			backend = "SuperBIContext.SemanticLabels",
			labelCount = labels.Count,
			conceptCount = concepts.Count,
			concepts,
			parameters = context.Parameters,
			correlationId = context.CorrelationId,
		});

		return ToolResult.Ok(output, "live", RequiresApproval);
	}
}
