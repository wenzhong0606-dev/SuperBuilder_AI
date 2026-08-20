using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;
using SuperBuilder_AI.Services.BI.Evaluation;

namespace SuperBulider_AI.Tests;

public sealed class QueryPlanEvaluatorMetricAnchorTests
{
    [Fact]
    public void GQ001_CorrectSumMetric_Passes()
    {
        var expected = new GoldenQueryExpectation
        {
            Metrics = new()
            {
                new GoldenMetricExpectation
                {
                    SemanticText = "入库数量",
                    Aggregation = QueryAggregation.Sum
                }
            }
        };

        var runtime = new QueryPlan
        {
            Metrics = new()
            {
                new QueryMetric
                {
                    Name = "入库数量",
                    Field = "quantity",
                    Aggregation = QueryAggregation.Sum
                }
            }
        };

        var result = new QueryPlanEvaluator().Evaluate("GQ-001", expected, runtime);

        Assert.True(result.Passed);
        Assert.True(result.Metrics.Passed);
    }

    [Fact]
    public void GQN001_WrongAggregation_FailsMetricEvaluation()
    {
        var expected = new GoldenQueryExpectation
        {
            Metrics = new()
            {
                new GoldenMetricExpectation
                {
                    SemanticText = "入库数量",
                    Aggregation = QueryAggregation.Sum
                }
            }
        };

        var runtime = new QueryPlan
        {
            Metrics = new()
            {
                new QueryMetric
                {
                    Name = "入库数量",
                    Field = "quantity",
                    Aggregation = QueryAggregation.Average
                }
            }
        };

        var result = new QueryPlanEvaluator().Evaluate("GQ-N001", expected, runtime);

        Assert.False(result.Passed);
        Assert.False(result.Metrics.Passed);
    }
}
