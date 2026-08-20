using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;
using SuperBuilder_AI.Services.BI.Evaluation;

namespace SuperBulider_AI.Tests;

public sealed class QueryPlanQueryShapeRankingAnchorTests
{
    [Fact]
    public void GQ006_AggregateRankingTop10Descending_Passes()
    {
        var expected = new GoldenQueryExpectation
        {
            IsAggregate = true,
            Limit = 10,
            IsRanking = true,
            IsAggregateRanking = true,
            Orders = new()
            {
                new GoldenOrderExpectation
                {
                    IsMetric = true,
                    MetricSemanticText = "入库数量",
                    Aggregation = QueryAggregation.Sum,
                    Direction = "DESC"
                }
            }
        };

        var actual = new QueryPlan
        {
            IsAggregate = true,
            Limit = 10,
            IsRanking = true,
            IsAggregateRanking = true,
            Orders = new()
            {
                new QueryOrder
                {
                    IsMetric = true,
                    MetricName = "入库数量",
                    Aggregation = QueryAggregation.Sum,
                    Direction = "DESC"
                }
            }
        };

        var result = new QueryPlanQueryShapeScoringService().Evaluate(expected, actual);

        Assert.True(result.Passed);
        Assert.Equal(1d, result.Score);
    }

    [Fact]
    public void GQN004_WrongRankingDirection_Fails()
    {
        var expected = new GoldenQueryExpectation
        {
            IsRanking = true,
            IsAggregateRanking = true,
            Limit = 10,
            Orders = new()
            {
                new GoldenOrderExpectation
                {
                    IsMetric = true,
                    MetricSemanticText = "入库数量",
                    Aggregation = QueryAggregation.Sum,
                    Direction = "DESC"
                }
            }
        };

        var actual = new QueryPlan
        {
            IsRanking = true,
            IsAggregateRanking = true,
            Limit = 10,
            Orders = new()
            {
                new QueryOrder
                {
                    IsMetric = true,
                    MetricName = "入库数量",
                    Aggregation = QueryAggregation.Sum,
                    Direction = "ASC"
                }
            }
        };

        var result = new QueryPlanQueryShapeScoringService().Evaluate(expected, actual);

        Assert.False(result.Passed);
        Assert.True(result.Score < 1d);
    }

    [Fact]
    public void NoOrdersExpected_UnexpectedRuntimeOrderFails()
    {
        var expected = new GoldenQueryExpectation
        {
            Orders = new()
        };

        var actual = new QueryPlan
        {
            Orders = new()
            {
                new QueryOrder { Field = "quantity", Direction = "DESC" }
            }
        };

        var result = new QueryPlanQueryShapeScoringService().Evaluate(expected, actual);

        Assert.False(result.Passed);
        Assert.Equal(0d, result.Score);
    }
}
