using System;

namespace SuperBuilder_AI.Interfaces.BI;

/// <summary>
/// BI 管线可观测性指标接收端（端口抽象，M9-05）。
///
/// <para>
/// 将「分段延迟采集」与「结果分类计数」从具体实现（<see cref="SuperBuilder_AI.Middleware.RequestMetricsCollector"/>）
/// 抽离为端口层契约，使 Application 层（<c>SuperBuilder_AI.Services</c>）无需反向依赖 Middleware 层，
/// 满足架构门禁「应用层不得依赖 API / Middleware」的不变量。
/// </para>
///
/// <para>
/// 具体实现在 <c>SuperBuilder_AI.Middleware</c>，于 Program.cs 以同一单例同时注册为
/// <see cref="IPipelineMetricsSink"/> 与 <see cref="SuperBuilder_AI.Middleware.RequestMetricsCollector"/>，
/// 保证采集链路（中间件 / 端点 / BI 编排服务）写入同一份内存指标。
/// </para>
/// </summary>
public interface IPipelineMetricsSink
{
	/// <summary>BI 管线分段延迟指标键——固定 5 段，无基数风险。</summary>
	public const string StageUnderstand = "pipeline.understand";
	public const string StagePlan = "pipeline.plan";
	public const string StageSql = "pipeline.sql";
	public const string StageDb = "pipeline.db";
	public const string StageResult = "pipeline.result";

	/// <summary>管线结果分类计数键——固定键，无基数风险。</summary>
	public const string OutcomeReject = "outcome.reject";
	public const string OutcomeEarlyReturn = "outcome.earlyReturn";
	public const string OutcomeRepair = "outcome.repair";

	/// <summary>记录一次管线分段耗时（毫秒）。</summary>
	/// <param name="stage">分段键（见 <see cref="StageUnderstand"/> 等常量）。</param>
	/// <param name="elapsedMs">耗时（毫秒）。</param>
	void RecordStage(string stage, long elapsedMs);

	/// <summary>记录一次管线结果分类。<c>occurred=false</c> 时实现应忽略。</summary>
	/// <param name="outcome">分类键（见 <see cref="OutcomeReject"/> 等常量）。</param>
	/// <param name="occurred">本次是否发生该分类。</param>
	void RecordOutcome(string outcome, bool occurred);
}
