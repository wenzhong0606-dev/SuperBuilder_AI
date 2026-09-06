using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Services.BI;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M5-07：Decision Gate 状态化。
///
/// 验证 <see cref="QueryPlanDecisionType"/> 的 5 个显式状态已成为
/// <see cref="QueryPlanDecision"/> 的单一事实来源：ShouldExecute /
/// RequiresConfirmation 均由 Decision 枚举派生，不再各自独立赋值，
/// 杜绝「枚举 + 双布尔」漂移。
/// </summary>
public sealed class QueryPlanDecisionStateTests
{
	[Theory]
	[InlineData(QueryPlanDecisionType.Reject, false, false)]
	[InlineData(QueryPlanDecisionType.RequireApproval, false, true)]
	[InlineData(QueryPlanDecisionType.Allow, true, false)]
	[InlineData(QueryPlanDecisionType.AskClarification, false, true)]
	[InlineData(QueryPlanDecisionType.LimitedExecution, true, false)]
	public void Decision_Derives_ShouldExecute_And_RequiresConfirmation(
		QueryPlanDecisionType state,
		bool expectedShouldExecute,
		bool expectedRequiresConfirmation)
	{
		var decision = new QueryPlanDecision { Decision = state };

		Assert.Equal(expectedShouldExecute, decision.ShouldExecute);
		Assert.Equal(expectedRequiresConfirmation, decision.RequiresConfirmation);
	}

	[Fact]
	public void StateHelpers_MapEachOfFiveStates()
	{
		Assert.True(new QueryPlanDecision { Decision = QueryPlanDecisionType.Reject }.IsReject);
		Assert.True(new QueryPlanDecision { Decision = QueryPlanDecisionType.RequireApproval }.IsRequireApproval);
		Assert.True(new QueryPlanDecision { Decision = QueryPlanDecisionType.Allow }.IsAllow);
		Assert.True(new QueryPlanDecision { Decision = QueryPlanDecisionType.AskClarification }.IsAskClarification);
		Assert.True(new QueryPlanDecision { Decision = QueryPlanDecisionType.LimitedExecution }.IsLimitedExecution);

		// 互斥性：每种状态仅命中自身访问器
		var allow = new QueryPlanDecision { Decision = QueryPlanDecisionType.Allow };
		Assert.False(allow.IsReject);
		Assert.False(allow.IsRequireApproval);
		Assert.False(allow.IsAskClarification);
		Assert.False(allow.IsLimitedExecution);
	}

	[Fact]
	public void Gate_HighConfidence_MapsToAllow()
	{
		var gate = new QueryPlanDecisionGate();
		var decision = gate.Evaluate(new QueryPlanConfidence
		{
			Level = QueryPlanConfidenceLevel.High,
			Score = 0.92,
			CanProceed = true
		});

		Assert.Equal(QueryPlanDecisionType.Allow, decision.Decision);
		Assert.True(decision.ShouldExecute);
		Assert.False(decision.RequiresConfirmation);
	}

	[Fact]
	public void Gate_MediumWithoutDetail_MapsToRequireApproval()
	{
		var gate = new QueryPlanDecisionGate();
		var decision = gate.Evaluate(new QueryPlanConfidence
		{
			Level = QueryPlanConfidenceLevel.Medium,
			Score = 0.70,
			CanProceed = false,
			IsExecutableDetailQuery = false
		});

		Assert.Equal(QueryPlanDecisionType.RequireApproval, decision.Decision);
		Assert.False(decision.ShouldExecute);
		Assert.True(decision.RequiresConfirmation);
	}

	[Fact]
	public void Gate_LowConfidence_MapsToReject()
	{
		var gate = new QueryPlanDecisionGate();
		var decision = gate.Evaluate(new QueryPlanConfidence
		{
			Level = QueryPlanConfidenceLevel.Low,
			Score = 0.30
		});

		Assert.Equal(QueryPlanDecisionType.Reject, decision.Decision);
		Assert.False(decision.ShouldExecute);
		Assert.False(decision.RequiresConfirmation);
	}

	[Fact]
	public void Gate_DoesNotEmitExtensionStates_Currently()
	{
		// 当前 gate（M5-07）仅产出 Reject / RequireApproval / Allow；
		// AskClarification / LimitedExecution 为扩展态，由 M5-05 / M5-06 注入。
		var gate = new QueryPlanDecisionGate();

		var allow = gate.Evaluate(new QueryPlanConfidence
		{
			Level = QueryPlanConfidenceLevel.High,
			Score = 0.92,
			CanProceed = true
		});

		Assert.NotEqual(QueryPlanDecisionType.AskClarification, allow.Decision);
		Assert.NotEqual(QueryPlanDecisionType.LimitedExecution, allow.Decision);
	}
}
