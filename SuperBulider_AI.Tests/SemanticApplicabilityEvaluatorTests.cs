using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Services.BI.Evaluation;

namespace SuperBulider_AI.Tests;

public sealed class SemanticApplicabilityEvaluatorTests
{
    [Fact]
    public async Task GQ001_ResolvedMetric_ReturnsResolved()
    {
        var service = new DeterministicSemanticSearchService(
            DeterministicSemanticFixture.Metric("入库数量", "入库数量", 0.95));
        var evaluator = new SemanticApplicabilityEvaluator(service);
        var golden = new GoldenQueryCase
        {
            Id = "GQ-001",
            Question = "查询入库数量",
            Expected = new GoldenQueryExpected
            {
                Metrics = new()
                {
                    new GoldenQueryMetric
                    {
                        SemanticText = "入库数量",
                        Aggregation = QueryAggregation.Sum
                    }
                }
            }
        };

        var result = await evaluator.EvaluateAsync(golden);

        Assert.Equal("Resolved", result.State);
        Assert.NotNull(result.Resolution);
        Assert.True(result.Evidence?.LexicalMatch);
    }

    [Fact]
    public async Task GQA001_CompetingSemanticCandidates_ReturnsAmbiguous()
    {
        var service = new DeterministicSemanticSearchService(
            DeterministicSemanticFixture.Metric("数量", "入库数量", 0.95, columnId: 1),
            DeterministicSemanticFixture.Metric("数量", "出库数量", 0.94, columnId: 2));
        var evaluator = new SemanticApplicabilityEvaluator(service);
        var golden = new GoldenQueryCase
        {
            Id = "GQ-A001",
            Question = "查询数量",
            Expected = new GoldenQueryExpected
            {
                Metrics = new()
                {
                    new GoldenQueryMetric
                    {
                        SemanticText = "数量",
                        Aggregation = QueryAggregation.Sum
                    }
                }
            }
        };

        var result = await evaluator.EvaluateAsync(golden);

        Assert.Equal("Ambiguous", result.State);
        Assert.Null(result.Resolution);
        Assert.True(result.Evidence?.CompetingCandidates);
    }

    [Fact]
    public async Task GQU001_NoSemanticCandidates_ReturnsNotResolved()
    {
        var service = new DeterministicSemanticSearchService();
        var evaluator = new SemanticApplicabilityEvaluator(service);
        var golden = new GoldenQueryCase
        {
            Id = "GQ-U001",
            Question = "查询火星库存温度",
            Expected = new GoldenQueryExpected
            {
                Metrics = new()
                {
                    new GoldenQueryMetric
                    {
                        SemanticText = "火星库存温度",
                        Aggregation = QueryAggregation.Average
                    }
                }
            }
        };

        var result = await evaluator.EvaluateAsync(golden);

        Assert.Equal("NotResolved", result.State);
        Assert.Null(result.Resolution);
        Assert.False(result.Evidence?.SemanticCandidateExists);
    }
}
