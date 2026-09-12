using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Agent.Runtime;
using SuperBuilder_AI.Models.Agent;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Services.Agent.Runtime;

/// <summary>
/// 元数据探查工具（M7-04 live）：真实查询租户作用域内的元数据表与字段，
/// 产出 <c>Mode=live</c> 信封并返回真实表结构。只读、确定性、不调 LLM、无外部副作用。
/// <para>这是 M7-03 受控信封所声明的"真实后端将于 M7-04 接入"的落地：不再回执占位，而是返回真实数据。</para>
/// </summary>
public sealed class LiveMetadataTool : ITool
{
	private readonly SuperBIContext _db;

	/// <summary>构造 live 元数据工具（依赖作用域内的 <see cref="SuperBIContext"/>）。</summary>
	public LiveMetadataTool(SuperBIContext db) => _db = db;

	/// <inheritdoc />
	public string Tool => AgentTools.Metadata;

	/// <inheritdoc />
	public ToolRisk Risk => ToolRisk.Safe;

	/// <inheritdoc />
	public bool RequiresApproval => false;

	/// <summary>后端连接状态：已接真实后端（live）。</summary>
	public string BackendStatus => "live";

	/// <summary>展示名。</summary>
	public string DisplayName => "元数据探查";

	/// <inheritdoc />
	public async Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct = default)
	{
		// 显式施加租户作用域（与主流程共享同一作用域实例，幂等），保证只读当前租户元数据。
		_db.ApplyTenantScope(context.TenantId);

		var tables = await _db.MetadataTables.AsNoTracking()
			.Include(t => t.Columns)
			.OrderBy(t => t.TableName)
			.ToListAsync(ct);

		var output = JsonSerializer.Serialize(new
		{
			tool = Tool,
			displayName = DisplayName,
			mode = "live",
			connected = true,
			backend = "SuperBIContext.MetadataTables",
			tableCount = tables.Count,
			tables = tables.Select(t => new
			{
				tableName = t.TableName,
				comment = t.TableComment,
				businessDomain = t.BusinessDomain,
				columns = (t.Columns ?? Enumerable.Empty<MetadataColumn>())
					.OrderBy(c => c.Ordinal)
					.Select(c => new
					{
						columnName = c.ColumnName,
						dataType = c.DataType,
						nativeType = c.NativeType,
						comment = c.ColumnComment,
						isPrimaryKey = c.IsPrimaryKey,
					}).ToList(),
			}).ToList(),
			parameters = context.Parameters,
			correlationId = context.CorrelationId,
		});

		return ToolResult.Ok(output, "live", RequiresApproval);
	}
}
