using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Services.BI;

namespace SuperBulider_AI.Tests;

public sealed class QueryPlanDecisionGateAnchorTests
{
    [Fact]
    public void HighConfidenceSafePlan_Proceeds()
    {
        var confidence = new QueryPlanConfidence
        {
            Score = 0.95,
            Level = QueryPlanConfidenceLevel.High,
            CanProceed = true,
            Evidence = new QueryPlanConfidenceEvidence()
        };

        var result = new QueryPlanDecisionGate().Evaluate(confidence);

        Assert.Equal(QueryPlanDecisionType.Proceed, result.Decision);
        Assert.True(result.ShouldExecute);
        Assert.False(result.RequiresConfirmation);
        Assert.NotNull(result.Trace);
    }

    [Fact]
    public void MediumConfidence_RequiresConfirmation()
    {
        var confidence = new QueryPlanConfidence
        {
            Score = 0.70,
            Level = QueryPlanConfidenceLevel.Medium,
            CanProceed = true,
            Evidence = new QueryPlanConfidenceEvidence()
        };

        var result = new QueryPlanDecisionGate().Evaluate(confidence);

        Assert.Equal(QueryPlanDecisionType.Confirm, result.Decision);
        Assert.False(result.ShouldExecute);
        Assert.True(result.RequiresConfirmation);
    }

    [Fact]
    public void HighConfidenceWithValidationError_IsRejected()
    {
        var confidence = new QueryPlanConfidence
        {
            Score = 0.99,
            Level = QueryPlanConfidenceLevel.High,
            CanProceed = true,
            Evidence = new QueryPlanConfidenceEvidence
            {
                ValidationErrorCount = 1
            }
        };

        var result = new QueryPlanDecisionGate().Evaluate(confidence);

        Assert.Equal(QueryPlanDecisionType.Reject, result.Decision);
        Assert.False(result.ShouldExecute);
        Assert.False(result.RequiresConfirmation);
    }

    [Fact]
    public void HighConfidenceWithRepairLoop_IsRejected()
    {
        var confidence = new QueryPlanConfidence
        {
            Score = 0.99,
            Level = QueryPlanConfidenceLevel.High,
            CanProceed = true,
            Evidence = new QueryPlanConfidenceEvidence
            {
                RepairLoopDetected = true
            }
        };

        var result = new QueryPlanDecisionGate().Evaluate(confidence);

        Assert.Equal(QueryPlanDecisionType.Reject, result.Decision);
        Assert.False(result.ShouldExecute);
    }
}
