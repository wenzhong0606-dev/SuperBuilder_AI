using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.BI.Dashboard;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Interfaces.Database;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Dashboard;
using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Services.BI.Dashboard;

/// <summary>
/// 组件取数解析器默认实现（P6.3）。对照 QueryPlanPipeline 接入：
/// 把 <see cref="WidgetQueryDsl"/> 转换为 QueryIntent → 过 QueryPlanPipeline（含 Decision Gate）
/// → ISqlQueryBuilder 生成 SQL → IQueryExecutionService 执行，得到结构化结果行。
///
/// 与 <c>BIConversationService</c> 的 Step 2~7 完全一致，仅把"用户问句"替换为 DSL 取数定义，
/// 因此不触碰任何既有 Golden 路径、不引入新的语义漂移。
/// </summary>
public sealed class QueryPlanWidgetDataResolver : IWidgetDataResolver
{
	private readonly IQueryUnderstandingService _understanding;
	private readonly IQueryPlanPipeline _pipeline;
	private readonly SuperBIContext _db;
	private readonly ISqlDialectResolver _dialectResolver;
	private readonly ISqlQueryBuilder _sqlBuilder;
	private readonly IQueryExecutionService _exec;

	public QueryPlanWidgetDataResolver(
		IQueryUnderstandingService understanding,
		IQueryPlanPipeline pipeline,
		SuperBIContext db,
		ISqlDialectResolver dialectResolver,
		ISqlQueryBuilder sqlBuilder,
		IQueryExecutionService exec)
	{
		_understanding = understanding ?? throw new ArgumentNullException(nameof(understanding));
		_pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
		_db = db ?? throw new ArgumentNullException(nameof(db));
		_dialectResolver = dialectResolver ?? throw new ArgumentNullException(nameof(dialectResolver));
		_sqlBuilder = sqlBuilder ?? throw new ArgumentNullException(nameof(sqlBuilder));
		_exec = exec ?? throw new ArgumentNullException(nameof(exec));
	}

	/// <inheritdoc />
	public async Task<WidgetDataResult> ResolveAsync(
		WidgetQueryDsl? query,
		PlatformContext context,
		IReadOnlyList<FilterDsl> effectiveFilters,
		CancellationToken cancellationToken = default)
	{
		var result = new WidgetDataResult();

		if (query is null)
		{
			result.Decision = "NoQuery";
			return result;
		}

		// 1. 构造意图：优先自然语言问句；否则由显式指标/维度合成问句交给理解服务解析。
		QueryIntent intent;
		if (!string.IsNullOrWhiteSpace(query.Question))
		{
			intent = await _understanding.UnderstandAsync(query.Question, context);
		}
		else
		{
			intent = await _understanding.UnderstandAsync(BuildQuestion(query), context);
		}

		// 2. 全局/组件级筛选器下推到意图（P6.3 下推语义）。
		foreach (var f in effectiveFilters)
		{
			intent.Filters.Add(new QueryFilter
			{
				SemanticText = f.Field,
				Field = f.Field,
				Operator = f.Operator,
				Value = f.Value ?? string.Empty,
			});
		}

		// 3. 过 QueryPlanPipeline（语义验证 + 自动修复 + Confidence + Decision Gate）。
		var pipelineResult = await _pipeline.RunAsync(intent.OriginalQuestion, intent);
		if (pipelineResult.EarlyResponse is not null)
		{
			result.Decision = "Blocked";
			result.Resolved = false;
			result.Error = pipelineResult.EarlyResponse.ErrorMessage;
			return result;
		}

		var plan = pipelineResult.Plan;

		// 4. 数据源 → 方言 → SQL → 执行。
		try
		{
			var dataSource = await _db.DataSources
				.FirstAsync(x => x.Id == plan.DataSourceId, cancellationToken);
			var dialect = _dialectResolver.Resolve(dataSource.DbType ?? "sqlserver");
			var sql = await _sqlBuilder.BuildAsync(plan, dialect);
			var data = await _exec.ExecuteAsync(sql, plan.DataSourceId);

			result.Resolved = data.Success;
			result.Error = data.ErrorMessage;
			result.Decision = "Proceed";
			if (data.Rows.Count > 0)
			{
				result.Columns = data.Rows[0].Keys.ToList();
			}

			result.Rows = data.Rows;
		}
		catch (Exception ex)
		{
			result.Decision = "Error";
			result.Resolved = false;
			result.Error = ex.Message;
		}

		return result;
	}

	private static string BuildQuestion(WidgetQueryDsl query)
	{
		var metrics = string.Join("、", query.Metrics.Select(m => m.Field));
		var dimensions = query.Dimensions.Count > 0
			? "，按 " + string.Join("、", query.Dimensions)
			: string.Empty;
		return string.IsNullOrWhiteSpace(metrics) ? "查询数据" : $"统计{metrics}{dimensions}";
	}
}
