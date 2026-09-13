using System.Linq;
using System.Threading.Tasks;
using SuperBuilder_AI.Api.Diagnostics;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Middleware;
using Xunit;

namespace SuperBuilder_AI.Tests.Diagnostics;

/// <summary>
/// OBS-01：<see cref="AlertEvaluationService"/> 的「触发 + 恢复」闭环（使用 <see cref="TestAlertSink"/>）。
/// 直接驱动 <see cref="AlertEvaluationService.CycleOnce"/>，验证规则从触发到恢复各发一次告警。
/// </summary>
public class AlertEvaluationServiceTests
{
	private static AlertEvaluationService Build(RequestMetricsCollector metrics, ScanBacklogGauge gauge, IAlertSink sink, AlertThresholds? t = null)
	{
		// 用空 logger；AlertEvaluationService 对 null logger 做了防御。
		return new AlertEvaluationService(sink, metrics, gauge, t ?? new AlertThresholds(), logger: null!);
	}

	[Fact]
	public void CycleOnce_FiresAlert_ThenRecovers()
	{
		var metrics = new RequestMetricsCollector();
		for (var i = 0; i < 6; i++) metrics.Record("GET /api/ask", 500, 10);
		for (var i = 0; i < 14; i++) metrics.Record("GET /api/ask", 200, 10); // 0.30 错误率，触发

		var sink = new TestAlertSink();
		var svc = Build(metrics, new ScanBacklogGauge(), sink);

		// 第一轮：错误率越界 → 触发
		svc.CycleOnce();
		Assert.Contains(sink.OfRule("route_error_rate_high"), a => a.Rule == "route_error_rate_high" && a.Signal == "GET /api/ask");
		Assert.Equal(1, sink.OfRule("route_error_rate_high").Count);

		// 第二轮：错误率仍越界 → 不应重复发（_firedRules 已含该规则）
		svc.CycleOnce();
		Assert.Equal(1, sink.OfRule("route_error_rate_high").Count);

		// 第三轮：清空指标（新采集窗口），错误率回落 → 应发恢复告警
		metrics.Reset();
		for (var i = 0; i < 20; i++) metrics.Record("GET /api/ask", 200, 10); // 0 错误
		svc.CycleOnce();
		Assert.Contains(sink.OfRule("route_error_rate_high"), a => a.Signal == "recovered");
		Assert.Equal(1, sink.OfRule("route_error_rate_high").Count(a => a.Signal != "recovered"));
		Assert.Equal(1, sink.OfRule("route_error_rate_high").Count(a => a.Signal == "recovered"));
	}

	[Fact]
	public void CycleOnce_NoAlerts_WhenHealthy()
	{
		var metrics = new RequestMetricsCollector();
		for (var i = 0; i < 20; i++) metrics.Record("GET /api/ask", 200, 10);

		var sink = new TestAlertSink();
		var svc = Build(metrics, new ScanBacklogGauge(), sink);

		svc.CycleOnce();
		Assert.Empty(sink.Snapshot());
		Assert.Empty(svc.LastAlerts);
	}

	[Fact]
	public void LastAlerts_ReflectsCurrentFiring()
	{
		var metrics = new RequestMetricsCollector();
		for (var i = 0; i < 6; i++) metrics.Record("GET /api/ask", 500, 10);
		for (var i = 0; i < 14; i++) metrics.Record("GET /api/ask", 200, 10);

		var sink = new TestAlertSink();
		var svc = Build(metrics, new ScanBacklogGauge(), sink);
		svc.CycleOnce();

		Assert.Single(svc.LastAlerts, a => a.Rule == "route_error_rate_high");
	}
}
