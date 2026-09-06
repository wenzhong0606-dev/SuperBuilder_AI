using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Services.BI;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// 决策门回归：M0-09「第一个 Ask 明细对话跑通」。
/// 重点锁定：合法明细列表（目标实体已解析、含 Limit/Order、无指标/维度）
/// 在 Medium 置信度下必须直接进入 SQL Builder，不再无限期卡在 Confirmation。
/// </summary>
public sealed class QueryPlanDecisionGateTests
{
	private static QueryPlanConfidence MediumDetail(
		bool executable,
		int validationErrors = 0) => new()
	{
		Score = 0.75,
		Level = QueryPlanConfidenceLevel.Medium,
		CanProceed = false,
		IsExecutableDetailQuery = executable,
		Evidence = new QueryPlanConfidenceEvidence
		{
			ValidationErrorCount = validationErrors
		}
	};

	[Fact]
	public void Detail_list_at_medium_proceeds_when_executable()
	{
		var gate = new QueryPlanDecisionGate();
		var decision = gate.Evaluate(MediumDetail(executable: true));

		Assert.Equal(QueryPlanDecisionType.Allow, decision.Decision);
		Assert.True(decision.ShouldExecute);
		Assert.False(decision.RequiresConfirmation);
	}

	[Fact]
	public void Medium_without_detail_flag_still_requires_confirmation()
	{
		var gate = new QueryPlanDecisionGate();
		var decision = gate.Evaluate(MediumDetail(executable: false));

		Assert.Equal(QueryPlanDecisionType.RequireApproval, decision.Decision);
		Assert.False(decision.ShouldExecute);
		Assert.True(decision.RequiresConfirmation);
	}

	[Fact]
	public void Detail_list_with_validation_error_is_rejected_not_proceeded()
	{
		var gate = new QueryPlanDecisionGate();
		var decision = gate.Evaluate(MediumDetail(executable: true, validationErrors: 1));

		Assert.Equal(QueryPlanDecisionType.Reject, decision.Decision);
		Assert.False(decision.ShouldExecute);
	}
}
