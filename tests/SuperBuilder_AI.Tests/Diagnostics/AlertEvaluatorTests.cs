using SuperBuilder_AI.Api.Diagnostics;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Middleware;
using Xunit;

namespace SuperBuilder_AI.Tests.Diagnostics;

/// <summary>
/// OBS-01：<see cref="AlertEvaluator"/> 规则正确性（纯函数、无外部依赖）。
/// </summary>
public class AlertEvaluatorTests
{
	private static RequestMetricsCollector BuildRouteErrors(int errors, int total, string route = "GET /api/ask")
	{
		var c = new RequestMetricsCollector();
		for (var i = 0; i < errors; i++) c.Record(route, 500, 10);
		for (var i = 0; i < total - errors; i++) c.Record(route, 200, 10);
		return c;
	}

	[Fact]
	public void RouteErrorRate_OverThreshold_RaisesAlert()
	{
		var metrics = BuildRouteErrors(errors: 6, total: 20); // 0.30 >= 0.20
		var alerts = AlertEvaluator.Evaluate(metrics, new ScanBacklogGauge(), new AlertThresholds());

		var a = Assert.Single(alerts, x => x.Rule == "route_error_rate_high");
		Assert.Equal(AlertSeverity.Warning, a.Severity);
		Assert.Equal("GET /api/ask", a.Signal);
	}

	[Fact]
	public void RouteErrorRate_BelowThreshold_NoAlert()
	{
		var metrics = BuildRouteErrors(errors: 2, total: 20); // 0.10 < 0.20
		var alerts = AlertEvaluator.Evaluate(metrics, new ScanBacklogGauge(), new AlertThresholds());
		Assert.DoesNotContain(alerts, x => x.Rule == "route_error_rate_high");
	}

	[Fact]
	public void RouteErrorRate_BelowMinSamples_NoAlert()
	{
		var metrics = BuildRouteErrors(errors: 5, total: 10); // 0.50 但样本不足 20
		var alerts = AlertEvaluator.Evaluate(metrics, new ScanBacklogGauge(), new AlertThresholds());
		Assert.DoesNotContain(alerts, x => x.Rule == "route_error_rate_high");
	}

	[Fact]
	public void RouteErrorRate_CriticalWhenVeryHigh()
	{
		var metrics = BuildRouteErrors(errors: 18, total: 20); // 0.90 >= 0.50
		var alerts = AlertEvaluator.Evaluate(metrics, new ScanBacklogGauge(), new AlertThresholds());
		var a = Assert.Single(alerts, x => x.Rule == "route_error_rate_high");
		Assert.Equal(AlertSeverity.Critical, a.Severity);
	}

	[Fact]
	public void LoginFailureRate_OverThreshold_RaisesAlert()
	{
		var c = new RequestMetricsCollector();
		for (var i = 0; i < 7; i++) c.Record("POST /api/auth/login", 200, 10);
		for (var i = 0; i < 4; i++) c.Record("POST /api/auth/login", 401, 10); // 0.36 >= 0.30

		var alerts = AlertEvaluator.Evaluate(c, new ScanBacklogGauge(), new AlertThresholds());
		Assert.Contains(alerts, x => x.Rule == "login_failure_high");
	}

	[Fact]
	public void LoginFailureRate_BelowThreshold_NoAlert()
	{
		var c = new RequestMetricsCollector();
		for (var i = 0; i < 9; i++) c.Record("POST /api/auth/login", 200, 10);
		c.Record("POST /api/auth/login", 401, 10); // 0.10 < 0.30

		var alerts = AlertEvaluator.Evaluate(c, new ScanBacklogGauge(), new AlertThresholds());
		Assert.DoesNotContain(alerts, x => x.Rule == "login_failure_high");
	}

	[Fact]
	public void AskFailureRate_OverThreshold_RaisesAlert()
	{
		var c = new RequestMetricsCollector();
		for (var i = 0; i < 16; i++) c.RecordOutcome(IPipelineMetricsSink.OutcomeSuccess, true);
		for (var i = 0; i < 5; i++) c.RecordOutcome(IPipelineMetricsSink.OutcomeFailure, true); // 0.24 >= 0.20

		var alerts = AlertEvaluator.Evaluate(c, new ScanBacklogGauge(), new AlertThresholds());
		Assert.Contains(alerts, x => x.Rule == "ask_failure_high");
	}

	[Fact]
	public void ScanBacklog_OverThreshold_RaisesAlert()
	{
		var gauge = new ScanBacklogGauge();
		for (var i = 0; i < 6; i++) gauge.Increment(); // pending 6 >= 5

		var alerts = AlertEvaluator.Evaluate(new RequestMetricsCollector(), gauge, new AlertThresholds());
		var a = Assert.Single(alerts, x => x.Rule == "scan_backlog_high");
		Assert.Equal("metadata-scan", a.Signal);
	}

	[Fact]
	public void ScanBacklog_BelowThreshold_NoAlert()
	{
		var gauge = new ScanBacklogGauge();
		gauge.Increment();
		gauge.Increment(); // pending 2 < 5

		var alerts = AlertEvaluator.Evaluate(new RequestMetricsCollector(), gauge, new AlertThresholds());
		Assert.DoesNotContain(alerts, x => x.Rule == "scan_backlog_high");
	}
}
