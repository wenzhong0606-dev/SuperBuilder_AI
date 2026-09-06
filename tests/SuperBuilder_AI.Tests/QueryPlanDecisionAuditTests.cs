using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Services.BI;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M5-09 AI Decision Audit：管线对决策全过程的可追踪审计采集。
///
/// 验证要点：
/// 1. 默认 NoOp Sink 零行为变更（返回值与历史一致、无副作用）；
/// 2. 配置启用（Collecting Sink 模拟）时，一次运行恰好采集 1 条记录，且八类字段正确；
/// 3. EarlyResponse 短路同样被审计（Outcome=EarlyResponse 且捕获错误信息）；
/// 4. 审计旁路故障不影响主流程（由 Pipeline finally/try-catch 保证，本文件以 NoOp 不抛验证形态）。
/// </summary>
public class QueryPlanDecisionAuditTests
{
	#region 测试用阶段与 Sink

	/// <summary>播撒一个完整的“成功 Allow”流程上下文，用于验证字段提取。</summary>
	private sealed class SeedSuccessStage : IQueryPlanStage
	{
		public string StageName => "SeedSuccess";

		public Task ExecuteAsync(
			QueryPlanPipelineContext ctx,
			CancellationToken ct = default)
		{
			ctx.Plan = new QueryPlan
			{
				Tables = new List<QueryTable>
				{
					new QueryTable { TableName = "inbound_receipt" }
				},
				Fields = new List<QueryField>
				{
					new QueryField { ColumnName = "id" },
					new QueryField { ColumnName = "code" }
				},
				Filters = new List<QueryFilter>
				{
					new QueryFilter { Field = "status" }
				},
				Orders = new List<QueryOrder>(),
				Joins = new List<QueryJoin>(),
				Limit = 10,
				IsAggregate = false
			};
			ctx.Confidence = new QueryPlanConfidence
			{
				Score = 0.95,
				Level = QueryPlanConfidenceLevel.High,
				CanProceed = true,
				IsExecutableDetailQuery = true,
				Evidence = new QueryPlanConfidenceEvidence
				{
					ValidationErrorCount = 0,
					RepairCount = 1
				}
			};
			ctx.SemanticValidation = new QueryPlanValidationPipelineResult
			{
				RepairTrace = new QueryPlanRepairTrace
				{
					TotalAttempts = 1,
					ChangedPlanCount = 1,
					Status = QueryPlanRepairTraceStatus.Repaired,
					History = new List<QueryPlanRepairTraceEntry>
					{
						new QueryPlanRepairTraceEntry()
					}
				}
			};
			ctx.Decision = new QueryPlanDecision
			{
				Decision = QueryPlanDecisionType.Allow,
				Reason = "high confidence",
				Trace = new QueryPlanDecisionTrace
				{
					ConfidenceScore = 0.95
				}
			};
			ctx.Explanation = new QueryPlanExplanation();
			return Task.CompletedTask;
		}
	}

	/// <summary>第一阶段即短路返回 EarlyResponse。</summary>
	private sealed class EarlyResponseStage : IQueryPlanStage
	{
		public string StageName => "Early";

		public Task ExecuteAsync(
			QueryPlanPipelineContext ctx,
			CancellationToken ct = default)
		{
			ctx.EarlyResponse = new BIResponse
			{
				Success = false,
				ErrorMessage = "blocked by column security"
			};
			return Task.CompletedTask;
		}
	}

	/// <summary>收集所有审计记录，供断言。</summary>
	private sealed class CollectingAuditSink : IDecisionAuditSink
	{
		public List<QueryPlanDecisionAuditRecord> Records { get; } = new();

		public Task RecordAsync(
			QueryPlanDecisionAuditRecord record,
			CancellationToken ct = default)
		{
			Records.Add(record);
			return Task.CompletedTask;
		}
	}

	#endregion

	#region 默认 NoOp：零行为变更

	[Fact]
	public async Task RunAsync_DefaultNoOpSink_ReturnsNormalResultAndNoSideEffect()
	{
		var sink = new NoOpDecisionAuditSink();
		var pipeline = new QueryPlanPipeline(
			new IQueryPlanStage[] { new SeedSuccessStage() },
			sink);

		var result = await pipeline.RunAsync(
			"列出最近十张入库单",
			new QueryIntent
			{
				IntentType = "Detail",
				OriginalQuestion = "列出最近十张入库单"
			});

		// 主流程返回值与历史完全一致
		Assert.NotNull(result.Plan);
		Assert.NotNull(result.Decision);
		Assert.Equal(QueryPlanDecisionType.Allow, result.Decision!.Decision);
		Assert.True(result.Decision.ShouldExecute);
		Assert.NotNull(result.Confidence);
		Assert.Equal(0.95, result.Confidence!.Score, precision: 3);
		// 没有 EarlyResponse（成功路径）
		Assert.Null(result.EarlyResponse);
	}

	#endregion

	#region 配置启用：八类字段采集正确

	[Fact]
	public async Task RunAsync_AuditEnabled_CollectsAllEightDimensions()
	{
		var sink = new CollectingAuditSink();
		var pipeline = new QueryPlanPipeline(
			new IQueryPlanStage[] { new SeedSuccessStage() },
			sink);

		var result = await pipeline.RunAsync(
			"列出最近十张入库单",
			new QueryIntent
			{
				IntentType = "Detail",
				OriginalQuestion = "列出最近十张入库单"
			});

		// 恰好采集 1 条记录
		Assert.Single(sink.Records);
		var rec = sink.Records[0];

		// 1. 问题
		Assert.Equal("列出最近十张入库单", rec.Question);
		// 2. 意图
		Assert.Equal("Detail", rec.IntentType);
		Assert.Contains("type=Detail", rec.IntentSummary);
		// 3. 计划（SQL 可重建源）
		Assert.Equal(new List<string> { "inbound_receipt" }, rec.PlanTableNames);
		Assert.Equal(new List<string> { "id", "code" }, rec.PlanFields);
		Assert.Equal(1, rec.PlanFilterCount);
		Assert.Equal(0, rec.PlanOrderCount);
		Assert.Equal(0, rec.PlanJoinCount);
		Assert.Equal(10, rec.PlanLimit);
		Assert.False(rec.PlanIsAggregate);
		// 4. SQL 预留（管线不生成 SQL，默认 null）
		Assert.Null(rec.Sql);
		// 5. 修复
		Assert.True(rec.HasRepair);
		Assert.Equal(QueryPlanRepairTraceStatus.Repaired, rec.RepairStatus);
		Assert.Equal(1, rec.RepairAttempts);
		Assert.Equal(1, rec.RepairChangedPlanCount);
		Assert.Equal(1, rec.RepairHistoryCount);
		// 6. 置信度
		Assert.Equal(0.95, rec.ConfidenceScore);
		Assert.Equal(QueryPlanConfidenceLevel.High, rec.ConfidenceLevel);
		Assert.True(rec.CanProceed);
		Assert.True(rec.IsExecutableDetailQuery);
		Assert.Equal(0, rec.ValidationErrorCount);
		Assert.Equal(1, rec.RepairCount);
		// 7. 决策
		Assert.Equal(QueryPlanDecisionType.Allow, rec.DecisionType);
		Assert.Equal("high confidence", rec.DecisionReason);
		Assert.True(rec.ShouldExecute);
		Assert.False(rec.RequiresConfirmation);
		Assert.Contains("conf=0.950", rec.DecisionTraceSummary);
		// 8. 模型预留（管线不持有模型名，默认 null）
		Assert.Null(rec.Model);
		// 结果
		Assert.Equal(AuditOutcome.Executed, rec.Outcome);
		Assert.Null(rec.ErrorMessage);

		// 标识字段已填充
		Assert.NotEqual(Guid.Empty, rec.AuditId);
		Assert.NotNull(rec.CorrelationId);
		Assert.NotEqual(default, rec.EvaluatedAt);

		// 主流程返回值不受影响
		Assert.NotNull(result.Plan);
		Assert.Equal(QueryPlanDecisionType.Allow, result.Decision!.Decision);
	}

	#endregion

	#region EarlyResponse 短路：同样被审计

	[Fact]
	public async Task RunAsync_EarlyResponseShortCircuit_AuditedAsEarlyResponse()
	{
		var sink = new CollectingAuditSink();
		var pipeline = new QueryPlanPipeline(
			new IQueryPlanStage[] { new EarlyResponseStage() },
			sink);

		var result = await pipeline.RunAsync(
			"任意问题",
			new QueryIntent());

		// 主流程返回 EarlyResponse
		Assert.NotNull(result.EarlyResponse);
		Assert.Equal("blocked by column security", result.EarlyResponse!.ErrorMessage);

		// 审计仍采集，且 Outcome 与错误信息正确
		Assert.Single(sink.Records);
		var rec = sink.Records[0];
		Assert.Equal(AuditOutcome.EarlyResponse, rec.Outcome);
		Assert.Equal("blocked by column security", rec.ErrorMessage);
		// 上下文不完整时字段优雅降级为 null/默认
		Assert.Null(rec.DecisionType);
		// 列表类字段设计为非空空列表（便于序列化），故缺失时为 Empty 而非 null
		Assert.Empty(rec.PlanTableNames);
		Assert.Empty(rec.PlanFields);
	}

	[Fact]
	public async Task RunAsync_EarlyResponseShortCircuit_NoOpSinkDoesNotThrow()
	{
		var pipeline = new QueryPlanPipeline(
			new IQueryPlanStage[] { new EarlyResponseStage() },
			new NoOpDecisionAuditSink());

		var result = await pipeline.RunAsync(
			"任意问题",
			new QueryIntent());

		Assert.NotNull(result.EarlyResponse);
		Assert.Equal("blocked by column security", result.EarlyResponse!.ErrorMessage);
	}

	#endregion
}
