using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Infrastructure.Database;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.Database;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Services.BI;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// AI BI功能测试Controller。
/// 用于本地 Runtime 验证，直接调用正式 Service 链。
/// </summary>
[ApiController]
[Route("test")]
public class AITestController : ControllerBase
{
    private readonly IQueryUnderstandingService _queryUnderstanding;
    private readonly IMetadataSemanticSearchService _metadataSearch;
    private readonly IQueryPlanBuilder _queryPlanBuilder;
    private readonly ISqlQueryBuilder _sqlBuilder;
    private readonly IQueryExecutionService _execution;
    private readonly IBIConversationService _conversation;
    private readonly SqlDialectResolver _dialectResolver;
    private readonly SuperBIContext _context;

    public AITestController(
        IQueryUnderstandingService queryUnderstanding,
        IMetadataSemanticSearchService metadataSearch,
        IQueryPlanBuilder queryPlanBuilder,
        ISqlQueryBuilder sqlBuilder,
        IQueryExecutionService execution,
        IBIConversationService conversation,
        SqlDialectResolver dialectResolver,
        SuperBIContext context)
    {
        _queryUnderstanding = queryUnderstanding;
        _metadataSearch = metadataSearch;
        _queryPlanBuilder = queryPlanBuilder;
        _sqlBuilder = sqlBuilder;
        _execution = execution;
        _conversation = conversation;
        _dialectResolver = dialectResolver;
        _context = context;
    }

    [HttpGet("understand")]
    public async Task<IActionResult> Understand(string question)
    {
        var result = await _queryUnderstanding.UnderstandAsync(question);
        return Ok(result);
    }

    [HttpGet("metadatasearch")]
    public async Task<IActionResult> MetadataSearch(string question)
    {
        var result = await _metadataSearch.SearchAsync(question, 10);
        return Content(JsonSerializer.Serialize(result));
    }

    [HttpGet("test")]
    public async Task<IActionResult> Test(string question)
    {
        var result = await _conversation.AskAsync(question, 1);
        return Ok(result);
    }

    /// <summary>
    /// 本地 SQL Runtime 测试入口。
    /// SQL 方言严格根据 QueryPlan.DataSourceId 对应的 DataSource.DbType 解析，
    /// 不允许测试入口硬编码数据库类型。
    /// </summary>
    [HttpGet("sql")]
    public async Task<IActionResult> Sql(string question)
    {
        var intent = await _queryUnderstanding.UnderstandAsync(question);

        object? diagnostics = null;
        if (_queryPlanBuilder is QueryPlanBuilder concreteBuilder)
        {
            diagnostics = await concreteBuilder.BuildWithDiagnosticsAsync(intent);
        }

        QueryPlan? plan = null;
        if (diagnostics is not null)
        {
            var dyn = diagnostics as QueryPlanBuilder.QueryPlanDiagnostics;
            if (dyn?.Plan != null)
            {
                plan = dyn.Plan;
            }
            else if (!string.IsNullOrWhiteSpace(dyn?.PlanError))
            {
                return Ok(new { intent, diagnostics, error = dyn.PlanError });
            }
        }

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

        if (plan.Tables == null || plan.Tables.Count == 0)
        {
            return Ok(new
            {
                intent,
                diagnostics,
                error = "QueryPlan 未包含任何数据表，无法确定 DataSourceId。"
            });
        }

        var dataSourceId = plan.DataSourceId != 0
            ? plan.DataSourceId
            : plan.Tables.First().DataSourceId;

        var dataSource = await _context.DataSources
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == dataSourceId);

        if (dataSource == null)
        {
            return Ok(new
            {
                intent,
                diagnostics,
                dataSourceId,
                error = $"不存在数据源:{dataSourceId}"
            });
        }

        if (string.IsNullOrWhiteSpace(dataSource.DbType))
        {
            return Ok(new
            {
                intent,
                diagnostics,
                dataSourceId,
                error = $"数据源未配置 DbType:{dataSourceId}"
            });
        }

        var dialect = _dialectResolver.Resolve(dataSource.DbType);
        var sql = await _sqlBuilder.BuildAsync(plan, dialect);
        var result = await _execution.ExecuteAsync(sql, dataSourceId);

        var responseObj = new
        {
            intent,
            diagnostics,
            dataSource = new
            {
                id = dataSource.Id,
                name = dataSource.Name,
                dbType = dataSource.DbType
            },
            sql = sql.Sql,
            parameters = sql.Parameters,
            execution = result
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.Preserve
        };

        return Content(
            JsonSerializer.Serialize(responseObj, options),
            "application/json");
    }
}