using System.Text.Json;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Interfaces.AppBuilder;
using SuperBuilder_AI.Models.AppBuilder;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.AppBuilder;

/// <summary>
/// M7-11：从 Ask 查询快照导出应用取数绑定（C1）。
///
/// <para>职责：</para>
/// <list type="bullet">
/// <item>校验快照归属（仅创建者本人，跨用户/跨租户即 403）。</item>
/// <item>反查 <see cref="QueryPlan"/> 并映射为 <see cref="AppDataSourceBinding"/>（DataSourceId 硬约束 + 实体语义名）。</item>
/// <item>按 §9 支持矩阵拒绝不可无损转换的计划（JOIN / Distinct / 多字段排序 / in / between / 未知操作符 / 实体不可解析），统一 422。</item>
/// </list>
///
/// <para>不固化发布者 RLS 条件、身份参数或敏感结果摘要；运行时重新应用当前访问者策略。</para>
/// </summary>
public sealed class AppQueryBindingExporter : IAppQueryBindingExporter
{
	private readonly IAskQuerySnapshotStore _store;

	public AppQueryBindingExporter(IAskQuerySnapshotStore store)
	{
		_store = store;
	}

	public async Task<AppDataSourceBinding> ExportAsync(
		string turnId, long tenantId, long userId, CancellationToken cancellationToken = default)
	{
		var snapshot = await _store.GetAsync(turnId, cancellationToken);
		if (snapshot is null)
			throw SuperBuilderException.FromCode(ErrorCodes.AppSnapshotNotFound, 404);
		if (snapshot.TenantId != tenantId || snapshot.UserId != userId)
			throw SuperBuilderException.FromCode(ErrorCodes.AppSnapshotForbidden, 403);

		QueryPlan plan;
		try
		{
			plan = JsonSerializer.Deserialize<QueryPlan>(snapshot.QueryPlanJson)
			       ?? throw new InvalidOperationException("空查询计划");
		}
		catch (Exception ex)
		{
			throw SuperBuilderException.FromCode(ErrorCodes.AppSnapshotNotFound, 404, ex);
		}

		ValidateSupportMatrix(plan);

		var mainTable = plan.Tables.FirstOrDefault()
		                ?? throw SuperBuilderException.FromCode(ErrorCodes.AppBindingNotSupported, 422);

		var binding = new AppDataSourceBinding
		{
			// 实体标识优先取业务语义（语义文本 → 表注释 → 物理表名）。
			//
			// M7-11 修复：原先仅回退到 TableName（物理表名，如 wms_storage_receipt），
			// 而运行时 AppQueryExecutor 会把该值当作检索问句交给向量检索选表。
			// 纯物理表名在中文业务语义向量空间中区分度极低，
			// 会召回无关表（实测：wms_storage_receipt → 命中 盘点单/盘点单明细/异常反馈），
			// 使 Semantic/Table 证据归零、Confidence 跌至 Low 而被 Decision Gate 拒绝。
			//
			// 表注释（如「入库凭证」）才是语义检索的稳定锚点，故在物理名之前优先使用。
			Entity = !string.IsNullOrWhiteSpace(mainTable.SemanticText)
				? mainTable.SemanticText
				: !string.IsNullOrWhiteSpace(mainTable.TableComment)
					? mainTable.TableComment
					: mainTable.TableName,
			// 固化主表 Id：运行时据此硬锁定表，不再依赖概率性语义选表。
			TableId = mainTable.MetadataTableId > 0 ? mainTable.MetadataTableId : null,
			DataSourceId = snapshot.DataSourceId,
			Limit = plan.Limit,
		};

		// 指标（仅聚合查询）：映射受支持的聚合方式，DISTINCTCOUNT 拒绝。
		if (plan.IsAggregate)
		{
			foreach (var m in plan.Metrics)
			{
				var agg = MapAggregation(m.Aggregation);
				binding.Metrics.Add(new AppMetricBinding
				{
					Field = m.Field,
					Aggregation = agg,
				});
			}
		}

		// 维度（分组/分类轴）。
		foreach (var d in plan.Dimensions)
		{
			if (!string.IsNullOrWhiteSpace(d.SemanticText))
				binding.Dimensions.Add(d.SemanticText);
			else if (!string.IsNullOrWhiteSpace(d.ColumnName))
				binding.Dimensions.Add(d.ColumnName);
		}

		// 筛选：单值操作符透传原值；in 类型化数组禁用逗号拆分（§9）。
		foreach (var f in plan.Filters)
		{
			var (op, value) = MapFilterOperator(f.Operator, f.Value);
			var fb = new AppFilterBinding
			{
				Field = f.Field,
				Operator = op,
				Value = value,
			};
			if (op == AppFilterOperators.In && f.Value is not null)
			{
				// 尝试按 JSON 数组解析为类型化多值；失败则整体作为单个元素，禁止逗号拆分。
				try
				{
					var arr = JsonSerializer.Deserialize<List<string>>(f.Value);
					if (arr is not null) fb.Values = arr;
				}
				catch
				{
					fb.Values = new List<string> { f.Value };
				}
			}
			binding.Filters.Add(fb);
		}

		// 排序：首批仅单字段；多字段一律 422。
		if (plan.Orders.Count > 0)
		{
			var o = plan.Orders[0];
			binding.Sort.Add(new AppSortBinding
			{
				Field = o.Field,
				Direction = string.Equals(o.Direction, "DESC", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC",
			});
		}

		return binding;
	}

	/// <summary>§9 支持矩阵：拒绝不可无损转换的计划，统一 422。</summary>
	private static void ValidateSupportMatrix(QueryPlan plan)
	{
		if (plan.Joins.Count > 0)
			throw SuperBuilderException.FromCode(ErrorCodes.AppBindingNotSupported, 422);
		if (plan.Distinct)
			throw SuperBuilderException.FromCode(ErrorCodes.AppBindingNotSupported, 422);
		if (plan.Orders.Count > 1)
			throw SuperBuilderException.FromCode(ErrorCodes.AppBindingNotSupported, 422);
		foreach (var f in plan.Filters)
		{
			if (!IsSupportedOperator(f.Operator))
				throw SuperBuilderException.FromCode(ErrorCodes.AppBindingNotSupported, 422);
		}
		if (plan.IsAggregate)
		{
			foreach (var m in plan.Metrics)
			{
				if (string.Equals(m.Aggregation, "DISTINCTCOUNT", StringComparison.OrdinalIgnoreCase))
					throw SuperBuilderException.FromCode(ErrorCodes.AppBindingNotSupported, 422);
			}
		}
	}

	// 注：IN 多值执行（Values 类型化数组）为已记录的后续项；此处按 §9 支持矩阵拒绝（统一 422）。
	// 绑定模型已为 IN 预留 List<string> Values（禁止逗号拆分），不阻碍后续接入。
	private static bool IsSupportedOperator(string op) =>
		op switch
		{
			"=" or "!=" or ">" or "<" or "LIKE" => true,
			_ => false,
		};

	private static string MapAggregation(string agg) =>
		agg.Trim().ToUpperInvariant() switch
		{
			"SUM" => AppAggregateTypes.Sum,
			"COUNT" => AppAggregateTypes.Count,
			"AVG" or "AVERAGE" => AppAggregateTypes.Avg,
			"MAX" => AppAggregateTypes.Max,
			"MIN" => AppAggregateTypes.Min,
			_ => AppAggregateTypes.Sum,
		};

	private static (string Op, string? Value) MapFilterOperator(string op, string? value)
	{
		return op.Trim().ToUpperInvariant() switch
		{
			"=" => (AppFilterOperators.Eq, value),
			"!=" => (AppFilterOperators.Neq, value),
			">" => (AppFilterOperators.Gt, value),
			"<" => (AppFilterOperators.Lt, value),
			"IN" => (AppFilterOperators.In, null),
			"LIKE" => (AppFilterOperators.Like, value),
			_ => throw SuperBuilderException.FromCode(ErrorCodes.AppBindingNotSupported, 422),
		};
	}
}
