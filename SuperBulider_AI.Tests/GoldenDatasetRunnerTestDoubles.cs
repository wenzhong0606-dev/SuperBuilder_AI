using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;
using SuperBuilder_AI.Services.BI.Evaluation;

namespace SuperBulider_AI.Tests;

internal sealed class DeterministicQueryUnderstandingService : IQueryUnderstandingService
{
    public Task<QueryIntent> UnderstandAsync(string question)
        => Task.FromResult(new QueryIntent { OriginalQuestion = question });
}

internal sealed class DeterministicQueryPlanBuilder : IQueryPlanBuilder
{
    public Task<QueryPlan> BuildAsync(QueryIntent intent, QueryPlanSemanticResolution? resolution = null)
    {
        var plan = new QueryPlan
        {
            Intent = intent,
            IsAggregate = true
        };

        var semantic = resolution?.Metrics?.FirstOrDefault()?.SemanticText;
        if (!string.IsNullOrWhiteSpace(semantic))
        {
            plan.Metrics.Add(new QueryMetric
            {
                SemanticText = semantic,
                Aggregation = QueryAggregation.Sum
            });
        }

        return Task.FromResult(plan);
    }
}

internal sealed class DeterministicQueryPlanContextBuilder : IQueryPlanContextBuilder
{
    public Task<QueryPlanValidationContext> BuildAsync(QueryPlan plan)
        => Task.FromResult(new QueryPlanValidationContext());
}

internal sealed class DeterministicValidationPipeline : IQueryPlanValidationPipeline
{
    public Task<QueryPlanValidationPipelineResult> ValidateAsync(
        QueryPlan plan,
        QueryPlanValidationContext context,
        string originalQuestion)
        => Task.FromResult(new QueryPlanValidationPipelineResult
        {
            Plan = plan,
            IsValid = true,
            RepairTrace = null
        });
}
