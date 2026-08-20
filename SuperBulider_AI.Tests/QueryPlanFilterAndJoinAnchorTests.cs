using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;
using SuperBuilder_AI.Services.BI.Evaluation;

namespace SuperBulider_AI.Tests;

public sealed class QueryPlanFilterAndJoinAnchorTests
{
    [Fact]
    public void GQFilter_CorrectSemanticOperatorAndValue_Passes()
    {
        var expected = new[]
        {
            new GoldenFilterExpectation
            {
                SemanticText = "仓库名称",
                Operator = "=",
                Value = "一号仓库"
            }
        };

        var actual = new[]
        {
            new QueryFilter
            {
                Field = "仓库名称",
                Operator = "=",
                Value = "一号仓库"
            }
        };

        var result = new QueryPlanFilterScoringService().Evaluate(expected, actual);

        Assert.True(result.Passed);
        Assert.Equal(1d, result.Score);
    }

    [Fact]
    public void GQN002_WrongFilterOperator_Fails()
    {
        var expected = new[]
        {
            new GoldenFilterExpectation
            {
                SemanticText = "仓库名称",
                Operator = "=",
                Value = "一号仓库"
            }
        };

        var actual = new[]
        {
            new QueryFilter
            {
                Field = "仓库名称",
                Operator = ">",
                Value = "一号仓库"
            }
        };

        var result = new QueryPlanFilterScoringService().Evaluate(expected, actual);

        Assert.False(result.Passed);
        Assert.Equal(0.70d, result.Score, 6);
    }

    [Fact]
    public void EmptyExpectedFilters_RejectsUnexpectedRuntimeFilter()
    {
        var actual = new[]
        {
            new QueryFilter
            {
                Field = "仓库名称",
                Operator = "=",
                Value = "一号仓库"
            }
        };

        var result = new QueryPlanFilterScoringService().Evaluate(
            Array.Empty<GoldenFilterExpectation>(), actual);

        Assert.False(result.Passed);
        Assert.Equal(0d, result.Score);
    }

    [Fact]
    public void JoinExpectation_IsDatabaseIndependent()
    {
        var expected = new GoldenJoinExpectation
        {
            LeftTableSemanticText = "入库单",
            LeftColumnSemanticText = "物料编码",
            RightTableSemanticText = "物料主数据",
            RightColumnSemanticText = "物料编码",
            JoinType = "INNER"
        };

        Assert.Equal("入库单", expected.LeftTableSemanticText);
        Assert.Equal("物料编码", expected.LeftColumnSemanticText);
        Assert.Equal("物料主数据", expected.RightTableSemanticText);
        Assert.Equal("物料编码", expected.RightColumnSemanticText);
        Assert.Equal("INNER", expected.JoinType);
    }
}
