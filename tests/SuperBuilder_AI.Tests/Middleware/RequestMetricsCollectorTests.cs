using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Middleware;
using Xunit;

namespace SuperBuilder_AI.Tests.Middleware;

/// <summary>
/// M9-05：<see cref="RequestMetricsCollector"/> 分段延迟 + 结果分类计数 的纯单元测试。
/// 复用请求指标范式（有界内存 / 异常静默），此处仅验证新增能力的正确性，不触发任何外部依赖。
/// </summary>
public class RequestMetricsCollectorTests
{
	[Fact]
	public void RecordStage_AggregatesCountAvgP95Max()
	{
		var c = new RequestMetricsCollector();
		c.RecordStage(IPipelineMetricsSink.StageUnderstand, 100);
		c.RecordStage(IPipelineMetricsSink.StageUnderstand, 200);

		var snap = c.StageSnapshot();
		Assert.Single(snap);

		var s = snap[0];
		Assert.Equal(IPipelineMetricsSink.StageUnderstand, s.Stage);
		Assert.Equal(2, s.Count);
		Assert.Equal(150.0, s.AvgMs);
		Assert.Equal(200, s.MaxMs);
		Assert.Equal(200.0, s.P95Ms); // 2 样本 P95 = 较大值
	}

	[Fact]
	public void RecordStage_SeparateKeysRemainIndependent()
	{
		var c = new RequestMetricsCollector();
		c.RecordStage(IPipelineMetricsSink.StagePlan, 50);
		c.RecordStage(IPipelineMetricsSink.StageSql, 70);

		var snap = c.StageSnapshot();
		Assert.Equal(2, snap.Count);
		Assert.Contains(snap, x => x.Stage == IPipelineMetricsSink.StagePlan && x.Count == 1);
		Assert.Contains(snap, x => x.Stage == IPipelineMetricsSink.StageSql && x.Count == 1);
	}

	[Fact]
	public void RecordOutcome_OnlyCountsWhenOccurred()
	{
		var c = new RequestMetricsCollector();
		c.RecordOutcome(IPipelineMetricsSink.OutcomeReject, true);
		c.RecordOutcome(IPipelineMetricsSink.OutcomeReject, false); // 不应计数
		c.RecordOutcome(IPipelineMetricsSink.OutcomeRepair, true);

		var snap = c.OutcomeSnapshot();
		Assert.Equal(2, snap.Count);

		var reject = Assert.Single(snap, x => x.Outcome == IPipelineMetricsSink.OutcomeReject);
		Assert.Equal(1, reject.Count);

		var repair = Assert.Single(snap, x => x.Outcome == IPipelineMetricsSink.OutcomeRepair);
		Assert.Equal(1, repair.Count);
	}

	[Fact]
	public void RecordOutcome_SameKeyAccumulates()
	{
		var c = new RequestMetricsCollector();
		c.RecordOutcome(IPipelineMetricsSink.OutcomeEarlyReturn, true);
		c.RecordOutcome(IPipelineMetricsSink.OutcomeEarlyReturn, true);
		c.RecordOutcome(IPipelineMetricsSink.OutcomeEarlyReturn, true);

		var snap = c.OutcomeSnapshot();
		var early = Assert.Single(snap, x => x.Outcome == IPipelineMetricsSink.OutcomeEarlyReturn);
		Assert.Equal(3, early.Count);
	}

	[Fact]
	public void Reset_ClearsStagesAndOutcomes()
	{
		var c = new RequestMetricsCollector();
		c.RecordStage(IPipelineMetricsSink.StageDb, 10);
		c.RecordOutcome(IPipelineMetricsSink.OutcomeReject, true);

		c.Reset();

		Assert.Empty(c.StageSnapshot());
		Assert.Empty(c.OutcomeSnapshot());
	}

	[Fact]
	public void RecordStage_NullOrEmptyKey_FallsBackToOtherRoute()
	{
		var c = new RequestMetricsCollector();
		c.RecordStage("", 5);
		c.RecordStage(null!, 5);

		// 不抛异常；空键归入 __other__，至少有一条记录。
		Assert.NotEmpty(c.StageSnapshot());
	}
}
