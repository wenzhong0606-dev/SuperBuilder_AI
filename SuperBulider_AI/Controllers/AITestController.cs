using Google.Protobuf.Compiler;
using Microsoft.AspNetCore.Mvc;
using SuperBulider_AI.Infrastructure.Database;
using SuperBulider_AI.Interfaces;
using SuperBulider_AI.Models.AI;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace SuperBulider_AI.Controllers;


/// <summary>
/// AI BI功能测试Controller
///
/// 用于Phase 1.5~1.6功能验证。
///
/// </summary>
[ApiController]
[Route("test")]
public class AITestController
	: ControllerBase
{


	private readonly IQueryUnderstandingService
		_queryUnderstanding;


	private readonly IMetadataSemanticSearchService
		_metadataSearch;


	private readonly IQueryPlanBuilder
		_queryPlanBuilder;


	private readonly ISqlQueryBuilder
		_sqlBuilder;


	private readonly IQueryExecutionService
		_execution;


	private readonly IBIConversationService
		_conversation;

	private readonly SqlDialectResolver
	_dialectResolver;


	public AITestController(
		IQueryUnderstandingService queryUnderstanding,
		IMetadataSemanticSearchService metadataSearch,
		IQueryPlanBuilder queryPlanBuilder,
		ISqlQueryBuilder sqlBuilder,
		IQueryExecutionService execution,
		IBIConversationService conversation,
		SqlDialectResolver dialectResolver)
	{

		_queryUnderstanding =
			queryUnderstanding;


		_metadataSearch =
			metadataSearch;


		_queryPlanBuilder =
			queryPlanBuilder;


		_sqlBuilder =
			sqlBuilder;


		_execution =
			execution;


		_conversation =
			conversation;


		_dialectResolver =
			dialectResolver;
	}

	/// <summary>
	/// 测试AI问题理解
	/// </summary>
	[HttpGet("understand")]
	public async Task<IActionResult> Understand(string question)
	{

		var result =
			await _queryUnderstanding
			.UnderstandAsync(question);


		return Ok(result);

	}


	/// <summary>
	/// 测试Metadata向量搜索
	/// </summary>
	[HttpGet("metadatasearch")]
	public async Task<IActionResult> MetadataSearch(string question)
	{

		var result =
			await _metadataSearch
			.SearchAsync(question, 10);


		return Content(JsonSerializer.Serialize(result));

	}


	/// <summary>
	/// AI BI完整链路
	/// </summary>
	[HttpGet("test")]
	public async Task<IActionResult> test(string question)
	{


		var result =
			await _conversation
			.AskAsync(question, 1);



		return Ok(result);

	}

	/// <summary>
	/// SQL生成测试（包含诊断信息）
	/// </summary>
	[HttpGet("sql")]
	public async Task<IActionResult> Sql(string question)
	{
		var intent = await _queryUnderstanding.UnderstandAsync(question);

		// 生成诊断信息（仅当 QueryPlanBuilder 为具体实现时）
		object? diagnostics = null;
		if (_queryPlanBuilder is SuperBulider_AI.Services.QueryPlanBuilder concreteBuilder)
		{
			diagnostics = await concreteBuilder.BuildWithDiagnosticsAsync(intent);
		}

		QueryPlan? plan = null;
		if (diagnostics is not null)
		{
			// 如果诊断已经包含可用的 Plan，则复用，避免重复抛出异常
			var dyn = diagnostics as SuperBulider_AI.Services.QueryPlanBuilder.QueryPlanDiagnostics;
			if (dyn?.Plan != null)
			{
				plan = dyn.Plan;
				// 返回诊断信息以及未执行的 SQL/结果占位
			}
			else if (!string.IsNullOrWhiteSpace(dyn?.PlanError))
			{
				return Ok(new { intent, diagnostics, error = dyn.PlanError });
			}
		}

		// 如果 diagnostics 未提供 Plan，则尝试构建 Plan（BuildAsync 可能抛出）
		if (plan == null)
		{
			try
			{
				plan = await _queryPlanBuilder.BuildAsync(intent);
			}
			catch (Exception ex)
			{
				return Ok(new { intent, diagnostics, error = ex.Message });
			}
		}

		var datasource = plan.Tables.First().DataSourceId;
		var dialect = _dialectResolver.Resolve("MYSQL");
		var sql = await _sqlBuilder.BuildAsync(plan, dialect);
		var result = await _execution.ExecuteAsync(sql, plan.DataSourceId);

		var responseObj = new { intent, diagnostics, sql = sql.Sql, parameters = sql.Parameters, execution = result };
		var options = new JsonSerializerOptions
		{
			WriteIndented = true,
			ReferenceHandler = ReferenceHandler.Preserve
		};
		var json = JsonSerializer.Serialize(responseObj, options);
		return Content(json, "application/json");

	}
}