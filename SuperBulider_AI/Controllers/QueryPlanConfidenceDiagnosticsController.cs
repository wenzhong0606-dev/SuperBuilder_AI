using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;
using SuperBuilder_AI.Services.BI.Evaluation;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// Phase 2.4-E QueryPlan Confidence 诊断入口。
/// 使用 Semantic Resolution 已确认的物理绑定构造确定性的 Runtime QueryPlan。
/// </summary>
[ApiController]
[Route("evaluation/diagnostics")]
public sealed class QueryPlanConfidenceDiagnosticsController : ControllerBase
{
    private readonly GoldenQueryDatasetSerializer _serializer;
    private readonly IWebHostEnvironment _environment;
    private readonly SemanticApplicabilityEvaluator _semanticApplicabilityEvaluator;
    private readonly QueryPlanEvaluationGate _queryPlanEvaluationGate;
    private readonly QueryPlanEvaluator _queryPlanEvaluator;
    private readonly IQueryPlanContextBuilder _queryPlanContextBuilder;
    private readonly IQueryPlanValidationPipeline _queryPlanValidationPipeline;
    private readonly IQueryPlanConfidenceService _queryPlanConfidenceService;

    public QueryPlanConfidenceDiagnosticsController(
        GoldenQueryDatasetSerializer serializer,
        IWebHostEnvironment environment,
        SemanticApplicabilityEvaluator semanticApplicabilityEvaluator,
        QueryPlanEvaluationGate queryPlanEvaluationGate,
        QueryPlanEvaluator queryPlanEvaluator,
        IQueryPlanContextBuilder queryPlanContextBuilder,
        IQueryPlanValidationPipeline queryPlanValidationPipeline,
        IQueryPlanConfidenceService queryPlanConfidenceService)
    {
        _serializer = serializer;
        _environment = environment;
        _semanticApplicabilityEvaluator = semanticApplicabilityEvaluator;
        _queryPlanEvaluationGate = queryPlanEvaluationGate;
        _queryPlanEvaluator = queryPlanEvaluator;
        _queryPlanContextBuilder = queryPlanContextBuilder;
        _queryPlanValidationPipeline = queryPlanValidationPipeline;
        _queryPlanConfidenceService = queryPlanConfidenceService;
    }

    [HttpGet("query-plan-confidence")]
    public async Task<ActionResult<object>> QueryPlanConfidence(
        [FromQuery] string caseId,
        [FromQuery] int topK = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(caseId))
            return BadRequest(new { passed = false, message = "caseId is required." });

        if (topK < 1 || topK > 100)
            return BadRequest(new { passed = false, message = "topK must be between 1 and 100." });

        var goldenCase = LoadGoldenCase(caseId);
        if (goldenCase is null)
            return NotFound(new { passed = false, message = $"Golden Case '{caseId}' was not found." });

        var applicability = await _semanticApplicabilityEvaluator.EvaluateAsync(goldenCase, topK);
        var gate = _queryPlanEvaluationGate.Evaluate(applicability);

        if (gate.Blocking)
        {
            return Ok(new
            {
                passed = false,
                stage = "SemanticApplicabilityGate",
                decision = gate,
                applicability,
                confidence = (object?)null
            });
        }

        cancellationToken.ThrowIfCancellationRequested();

        var intent = BuildIntentFromGoldenCase(goldenCase);
        var resolution = QueryPlanSemanticResolutionFactory.From(applicability);
        var runtimePlan = BuildRuntimePlanFromResolution(intent, resolution);

        var evaluation = _queryPlanEvaluator.Evaluate(goldenCase.Id, goldenCase.Expected, runtimePlan);
        var validationContext = await _queryPlanContextBuilder.BuildAsync(runtimePlan);
        var validationResult = await _queryPlanValidationPipeline
            .ValidateAsync(runtimePlan, validationContext, goldenCase.Question);

        cancellationToken.ThrowIfCancellationRequested();

        var confidence = await _queryPlanConfidenceService.EvaluateAsync(
            runtimePlan,
            validationResult,
            validationResult.RepairTrace,
            goldenCase.Question,
            cancellationToken);

        return Ok(new
        {
            passed = evaluation.Passed && confidence.CanProceed,
            stage = "QueryPlanConfidence",
            caseId = goldenCase.Id,
            question = goldenCase.Question,
            applicability,
            resolution,
            gate,
            runtimePlan = new
            {
                intentType = runtimePlan.Intent?.IntentType,
                dataSourceId = runtimePlan.DataSourceId,
                metrics = runtimePlan.Metrics,
                dimensions = runtimePlan.Dimensions.Count,
                filters = runtimePlan.Filters.Count,
                orders = runtimePlan.Orders.Count,
                tables = runtimePlan.Tables.Count,
                joins = runtimePlan.Joins.Count,
                fields = runtimePlan.Fields.Count,
                isAggregate = runtimePlan.IsAggregate,
                distinct = runtimePlan.Distinct,
                limit = runtimePlan.Limit,
                isRanking = runtimePlan.IsRanking,
                isDetailRanking = runtimePlan.IsDetailRanking,
                isAggregateRanking = runtimePlan.IsAggregateRanking
            },
            evaluation,
            validation = new
            {
                passed = validationResult.ValidationResult?.IsValid ?? false,
                errors = validationResult.ValidationResult?.ErrorItems.Count() ?? 0,
                warnings = validationResult.ValidationResult?.WarningItems.Count() ?? 0,
                repairStatus = validationResult.RepairTrace?.Status.ToString()
            },
            confidence = new
            {
                confidence.Score,
                level = confidence.Level.ToString(),
                confidence.CanProceed,
                confidence.Evidence,
                confidence.Reasons,
                confidence.BlockingReasons
            }
        });
    }

    private static QueryPlan BuildRuntimePlanFromResolution(QueryIntent intent, QueryPlanSemanticResolution resolution)
    {
        var plan = new QueryPlan
        {
            Intent = intent,
            DataSourceId = resolution.Metric?.DataSourceId
                ?? resolution.Filters.FirstOrDefault()?.DataSourceId
                ?? resolution.Dimensions.FirstOrDefault()?.DataSourceId
                ?? resolution.Orders.FirstOrDefault()?.DataSourceId
                ?? 0,
            IsAggregate = intent.IsAggregate,
            Distinct = false,
            Limit = intent.Limit,
            IsRanking = intent.IsRanking,
            IsDetailRanking = false,
            IsAggregateRanking = false
        };

        if (resolution.Metric is not null)
        {
            var metric = intent.Metrics.FirstOrDefault(x =>
                string.Equals(x.Name, resolution.Metric.SemanticText, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Field, resolution.Metric.Column, StringComparison.OrdinalIgnoreCase));

            metric ??= new QueryMetric
            {
                Name = resolution.Metric.SemanticText,
                Field = resolution.Metric.Column,
                Aggregation = "SUM"
            };

            metric.Field = resolution.Metric.Column;
            plan.Metrics.Add(metric);
            plan.Fields.Add(new QueryField
            {
                MetadataColumnId = resolution.Metric.ColumnId,
                ColumnName = resolution.Metric.Column,
                Aggregation = metric.Aggregation
            });

            EnsureTable(plan, resolution.Metric.TableId, resolution.Metric.DataSourceId, resolution.Metric.Table);
        }

        foreach (var binding in resolution.Filters)
        {
            EnsureTable(plan, binding.TableId, binding.DataSourceId, binding.Table);
            var filter = intent.Filters.FirstOrDefault(x => string.Equals(x.Field, binding.Column, StringComparison.OrdinalIgnoreCase));
            if (filter is not null)
                filter.Field = binding.Column;

            plan.Fields.Add(new QueryField
            {
                MetadataColumnId = binding.ColumnId,
                ColumnName = binding.Column,
                Aggregation = "NONE"
            });
        }

        foreach (var binding in resolution.Dimensions)
        {
            EnsureTable(plan, binding.TableId, binding.DataSourceId, binding.Table);
            var index = intent.Dimensions.FindIndex(x => string.Equals(x, binding.Column, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                intent.Dimensions[index] = binding.Column;

            plan.Dimensions.Add(new QueryDimension
            {
                MetadataColumnId = binding.ColumnId,
                ColumnName = binding.Column
            });

            plan.Fields.Add(new QueryField
            {
                MetadataColumnId = binding.ColumnId,
                ColumnName = binding.Column,
                Aggregation = "NONE"
            });
        }

        foreach (var binding in resolution.Orders)
        {
            EnsureTable(plan, binding.TableId, binding.DataSourceId, binding.Table);
            if (string.Equals(intent.OrderBy, binding.Column, StringComparison.OrdinalIgnoreCase))
                intent.OrderBy = binding.Column;

            plan.Orders.Add(new QueryOrder
            {
                MetadataColumnId = binding.ColumnId,
                Field = binding.Column,
                Direction = intent.OrderDirection ?? "ASC"
            });

            plan.Fields.Add(new QueryField
            {
                MetadataColumnId = binding.ColumnId,
                ColumnName = binding.Column,
                Aggregation = "NONE"
            });
        }

        return plan;
    }

    private static void EnsureTable(QueryPlan plan, long tableId, long dataSourceId, string tableName)
    {
        if (plan.Tables.Any(x => x.MetadataTableId == tableId))
            return;

        plan.Tables.Add(new QueryTable
        {
            MetadataTableId = tableId,
            DataSourceId = dataSourceId,
            TableName = tableName
        });
    }

    private static QueryIntent BuildIntentFromGoldenCase(GoldenQueryCase goldenCase)
    {
        var expectedJson = JsonSerializer.SerializeToElement(goldenCase.Expected);
        var intentType = expectedJson.TryGetProperty("intentType", out var intentTypeElement)
            ? intentTypeElement.GetString() ?? string.Empty
            : string.Empty;

        var intent = new QueryIntent
        {
            OriginalQuestion = goldenCase.Question,
            IntentType = intentType
        };

        if (expectedJson.TryGetProperty("metrics", out var metricsElement) && metricsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var metricElement in metricsElement.EnumerateArray())
            {
                intent.Metrics.Add(new QueryMetric
                {
                    Name = metricElement.TryGetProperty("semanticText", out var semanticText)
                        ? semanticText.GetString() ?? string.Empty
                        : metricElement.TryGetProperty("name", out var name)
                            ? name.GetString() ?? string.Empty
                            : string.Empty,
                    Field = metricElement.TryGetProperty("field", out var field)
                        ? field.GetString() ?? string.Empty
                        : string.Empty,
                    Aggregation = metricElement.TryGetProperty("aggregation", out var aggregation)
                        ? aggregation.GetString() ?? "NONE"
                        : "NONE"
                });
            }
        }

        return intent;
    }

    private GoldenQueryCase? LoadGoldenCase(string caseId)
    {
        var path = Path.Combine(_environment.ContentRootPath, "Evaluation", "Golden", "query-plan-golden-v1.json");
        if (!System.IO.File.Exists(path))
            return null;

        var dataset = _serializer.Deserialize(System.IO.File.ReadAllText(path));
        return dataset.Cases.SingleOrDefault(x => string.Equals(x.Id, caseId, StringComparison.OrdinalIgnoreCase));
    }
}
