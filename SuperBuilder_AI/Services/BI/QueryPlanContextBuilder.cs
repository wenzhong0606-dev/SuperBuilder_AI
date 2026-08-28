using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;


namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// QueryPlan验证上下文构建服务。
///
/// Phase 2.2.2.2
///
/// 职责:
///
/// 根据 QueryPlan 中引用的 Metadata Id
///
/// 加载:
///
/// MetadataTable
/// MetadataColumn
/// MetadataSemantic
///
/// 构建:
///
/// QueryPlanValidationContext
///
/// 不负责:
///
/// - AI理解
/// - Metadata搜索
/// - SQL生成
/// - QueryPlan修改
/// </summary>
public class QueryPlanContextBuilder :
	IQueryPlanContextBuilder
{
	private readonly SuperBIContext _context;


	public QueryPlanContextBuilder(
		SuperBIContext context)
	{
		_context = context;
	}



	/// <summary>
	/// 构建 QueryPlanValidationContext。
	/// </summary>
	public async Task<QueryPlanValidationContext> BuildAsync(
		QueryPlan plan)
	{
		ArgumentNullException.ThrowIfNull(plan);


		var result =
			new QueryPlanValidationContext();



		/*
		 * 1.
		 * 收集 QueryPlan 中涉及的 MetadataTableId
		 */
		var tableIds =
			plan.Tables
				.Select(x => x.MetadataTableId)
				.Distinct()
				.ToList();



		if (tableIds.Count == 0)
		{
			return result;
		}



		/*
		 * 2.
		 * 加载 MetadataTable
		 *
		 * 同时加载:
		 *
		 * Columns
		 *
		 * Semantic
		 */
		var tables =
			await _context.MetadataTables
				.AsNoTracking()
				.Include(x => x.Columns)
				.ThenInclude(x => x.Semantic)
				.Where(x =>
					tableIds.Contains(x.Id))
				.ToListAsync();



		foreach (var table in tables)
		{
			result.Tables.TryAdd(
				table.Id,
				table);



			var columns =
				table.Columns
					.ToList();



			result.TableColumns.TryAdd(
				table.Id,
				columns);



			foreach (var column in columns)
			{
				result.Columns.TryAdd(
					column.Id,
					column);
			}
		}



		return result;
	}
}