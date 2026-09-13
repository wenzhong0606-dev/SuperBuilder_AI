using System;

namespace SuperBuilder_AI.Api.Diagnostics;

/// <summary>
/// 告警阈值（OBS-01）。默认值偏保守，可在 Program.cs 以单例覆盖；
/// 不依赖「性能与恢复目标」OPEN 项（那是 PERF/DR 的验收前提），此处仅提供合理出厂基线。
/// </summary>
public sealed class AlertThresholds
{
	/// <summary>路由服务端错误率（Errors/Count）触发阈值。</summary>
	public double RouteErrorRate { get; set; } = 0.20;

	/// <summary>路由触发所需最小请求样本，避免冷启动抖动误报。</summary>
	public int RouteMinSamples { get; set; } = 20;

	/// <summary>登录失败率（1 − LoginSuccessRate）触发阈值。</summary>
	public double LoginFailureRate { get; set; } = 0.30;

	/// <summary>登录路由触发所需最小样本。</summary>
	public int LoginMinSamples { get; set; } = 10;

	/// <summary>Ask 失败率（Failure/(Success+Failure)）触发阈值。</summary>
	public double AskFailureRate { get; set; } = 0.20;

	/// <summary>Ask 触发所需最小样本。</summary>
	public int AskMinSamples { get; set; } = 20;

	/// <summary>扫描积压（待处理任务数）触发阈值。</summary>
	public int ScanBacklog { get; set; } = 5;

	/// <summary>路由错误率超过该值升级为 Critical。</summary>
	public double CriticalErrorRate { get; set; } = 0.50;
}
