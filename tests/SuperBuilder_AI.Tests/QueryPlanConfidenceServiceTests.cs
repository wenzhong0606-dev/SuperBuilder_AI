using System.Collections.Generic;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.BI;
using Xunit;

namespace SuperBuilder_AI.Tests;

public sealed class QueryPlanConfidenceServiceTests
{
	[Fact]
	public async Task Explicit_table_correction_is_honored_and_floors_confidence_to_medium()
	{
		// 复现 M0 Ask 跨数据源选表歧义修复：
		// 用户用“查询物理表必须使用 wms_storage_receipt”纠正后，
		// 即便向量/字段证据偏弱，置信度也须保底至 Medium 且采纳该表为主表。
		var service =
			new QueryPlanConfidenceService(
				new EmptyMetadataSemanticSearchService());

		var plan =
			new QueryPlan
			{
				IsAggregate = false,
				Limit = 10,
				Tables =
				{
					new QueryTable
					{
						MetadataTableId = 1001,
						TableName = "wms_storage_receipt"
					}
				},
				Dimensions =
				{
					new QueryDimension
					{
						MetadataColumnId = 2001,
						ColumnName = "receipt_no",
						SemanticText = "入库凭证号"
					}
				},
				Orders =
				{
					new QueryOrder
					{
						MetadataColumnId = 2002,
						Field = "created_at",
						Direction = "DESC"
					}
				}
			};

		var validationResult =
			new QueryPlanValidationPipelineResult
			{
				Plan = plan,
				ValidationResult = new QuerySemanticValidationResult(),
				RepairTrace = new QueryPlanRepairTrace
				{
					Status = QueryPlanRepairTraceStatus.NotRequired
				}
			};

		var confidence =
			await service.EvaluateAsync(
				plan,
				validationResult,
				validationResult.RepairTrace,
				"查询物理表必须使用 wms_storage_receipt");

		Assert.True(confidence.Evidence.TableCorrectionHonored);
		Assert.True(confidence.Level >= QueryPlanConfidenceLevel.Medium);
		Assert.Contains(
			"用户已显式纠正目标物理表且该表已采纳为主表，置信度保底至 Medium。",
			confidence.Reasons);
	}

	[Fact]
	public async Task No_correction_phrase_means_table_correction_not_honored()
	{
		var service =
			new QueryPlanConfidenceService(
				new EmptyMetadataSemanticSearchService());

		var plan =
			new QueryPlan
			{
				IsAggregate = false,
				Limit = 10,
				Tables =
				{
					new QueryTable
					{
						MetadataTableId = 1001,
						TableName = "wms_storage_receipt"
					}
				}
			};

		var validationResult =
			new QueryPlanValidationPipelineResult
			{
				Plan = plan,
				ValidationResult = new QuerySemanticValidationResult(),
				RepairTrace = new QueryPlanRepairTrace
				{
					Status = QueryPlanRepairTraceStatus.NotRequired
				}
			};

		var confidence =
			await service.EvaluateAsync(
				plan,
				validationResult,
				validationResult.RepairTrace,
				"最近十条入库凭证");

		Assert.False(confidence.Evidence.TableCorrectionHonored);
	}

	[Fact]
	public async Task Detail_list_with_display_dimension_is_executable_at_medium_confidence()
	{
		var service =
			new QueryPlanConfidenceService(
				new EmptyMetadataSemanticSearchService());

		var plan =
			new QueryPlan
			{
				IsAggregate = false,
				Limit = 10,
				Tables =
				{
					new QueryTable
					{
						MetadataTableId = 1001,
						TableName = "inbound_voucher"
					}
				},
				Dimensions =
				{
					new QueryDimension
					{
						MetadataColumnId = 2001,
						ColumnName = "voucher_no",
						SemanticText = "入库凭证"
					}
				},
				Orders =
				{
					new QueryOrder
					{
						MetadataColumnId = 2002,
						Field = "created_at",
						Direction = "DESC"
					}
				}
			};

		var validationResult =
			new QueryPlanValidationPipelineResult
			{
				Plan = plan,
				ValidationResult = new QuerySemanticValidationResult(),
				RepairTrace = new QueryPlanRepairTrace
				{
					Status = QueryPlanRepairTraceStatus.NotRequired
				}
			};

		var confidence =
			await service.EvaluateAsync(
				plan,
				validationResult,
				validationResult.RepairTrace,
				"最近十条入库凭证");

		Assert.Equal(QueryPlanConfidenceLevel.Medium, confidence.Level);
		Assert.True(confidence.IsExecutableDetailQuery);
	}

	// =========================================================
	// Phase 4：学习规则回放的置信度证据
	// =========================================================

	[Fact]
	public async Task Learning_replay_floors_confidence_to_medium_and_is_auditable()
	{
		// 回放自动化学习规则时，即便向量/字段证据偏弱，置信度也须保底至 Medium，
		// 且「用了学习规则」在证据与原因里可见、可审计 —— 与用户当轮表纠正同待遇。
		//
		// 这里刻意让向量召回只给一条低分命中：仅凭证据本身该计划是 Low，
		// 这样「保底」才是可观察的（基线 Low → 回放学习规则后 Medium）。
		var service =
			new QueryPlanConfidenceService(
				new LowScoreSemanticSearchService());

		var plan = BuildBareDetailPlan();

		var validationResult =
			new QueryPlanValidationPipelineResult
			{
				Plan = plan,
				ValidationResult = new QuerySemanticValidationResult(),
				RepairTrace = new QueryPlanRepairTrace
				{
					Status = QueryPlanRepairTraceStatus.NotRequired
				}
			};

		// 前置校验：无学习规则时该计划确实低于 Medium，否则本用例证明不了「保底」。
		var baseline =
			await service.EvaluateAsync(
				plan,
				validationResult,
				validationResult.RepairTrace,
				"最近十条入库凭证");

		Assert.True(baseline.Level < QueryPlanConfidenceLevel.Medium);
		Assert.False(baseline.Evidence.LearningApplied);
		Assert.Empty(baseline.Evidence.LearningRuleKinds);

		var learning =
			QueryPlanLearningContext.From(
				new List<CorrectionKind>
				{
					CorrectionKind.TableOverride
				});

		var confidence =
			await service.EvaluateAsync(
				plan,
				validationResult,
				validationResult.RepairTrace,
				"最近十条入库凭证",
				learning);

		Assert.Equal(QueryPlanConfidenceLevel.Medium, confidence.Level);
		Assert.True(confidence.Evidence.LearningApplied);
		Assert.Equal(1, confidence.Evidence.LearningRuleCount);
		Assert.Equal(
			CorrectionKind.TableOverride,
			Assert.Single(confidence.Evidence.LearningRuleKinds));
		Assert.True(confidence.Evidence.LearningTableOverrideApplied);
		Assert.Contains(
			confidence.Reasons,
			r => r.Contains("已回放 1 条学习规则"));
		Assert.Contains(
			confidence.Reasons,
			r => r.Contains("其中包含表级覆盖"));
	}

	[Fact]
	public async Task Non_table_learning_rule_also_floors_confidence()
	{
		// 值映射 / 外键 / 列展示类规则不含锁表句式，从问题文本推断不出来，
		// 必须靠显式上下文才能享受与表纠正同等的保底 —— 这正是 Phase 4 存在的理由。
		// 同样用低分向量命中建立 Low 基线，验证非锁表类规则也能抬升到 Medium。
		var service =
			new QueryPlanConfidenceService(
				new LowScoreSemanticSearchService());

		var plan = BuildBareDetailPlan();

		var validationResult =
			new QueryPlanValidationPipelineResult
			{
				Plan = plan,
				ValidationResult = new QuerySemanticValidationResult(),
				RepairTrace = new QueryPlanRepairTrace
				{
					Status = QueryPlanRepairTraceStatus.NotRequired
				}
			};

		var learning =
			QueryPlanLearningContext.From(
				new List<CorrectionKind>
				{
					CorrectionKind.ValueMap,
					CorrectionKind.ColumnDisplay
				});

		var confidence =
			await service.EvaluateAsync(
				plan,
				validationResult,
				validationResult.RepairTrace,
				"最近十条入库凭证",
				learning);

		Assert.Equal(QueryPlanConfidenceLevel.Medium, confidence.Level);
		Assert.True(confidence.Evidence.LearningApplied);
		Assert.Equal(2, confidence.Evidence.LearningRuleCount);
		Assert.False(confidence.Evidence.LearningTableOverrideApplied);
		Assert.DoesNotContain(
			confidence.Reasons,
			r => r.Contains("其中包含表级覆盖"));
	}

	[Fact]
	public async Task Learning_replay_does_not_bypass_hard_safety_block()
	{
		// 安全边界：存在 Validation Error 时仍判 Low。
		// 学习规则只抬升下限，不能把有硬错误的计划抬进 SQL Builder。
		var service =
			new QueryPlanConfidenceService(
				new EmptyMetadataSemanticSearchService());

		var plan = BuildBareDetailPlan();

		var semanticResult = new QuerySemanticValidationResult();
		semanticResult.Errors.Add(
			new SemanticValidationError
			{
				Code = "FieldNotFound",
				Message = "字段不存在",
				Severity = SemanticValidationSeverity.Error
			});

		var validationResult =
			new QueryPlanValidationPipelineResult
			{
				Plan = plan,
				ValidationResult = semanticResult,
				RepairTrace = new QueryPlanRepairTrace
				{
					Status = QueryPlanRepairTraceStatus.NotRequired
				}
			};

		var learning =
			QueryPlanLearningContext.From(
				new List<CorrectionKind>
				{
					CorrectionKind.TableOverride
				});

		var confidence =
			await service.EvaluateAsync(
				plan,
				validationResult,
				validationResult.RepairTrace,
				"最近十条入库凭证",
				learning);

		Assert.Equal(QueryPlanConfidenceLevel.Low, confidence.Level);

		// 证据仍如实记录命中，便于审计「保底为何没生效」。
		Assert.True(confidence.Evidence.LearningApplied);
	}

	[Fact]
	public async Task Empty_learning_context_is_a_no_op()
	{
		// 空集合 / null 必须与引入 Phase 4 之前行为一致（Golden 零回归）。
		var service =
			new QueryPlanConfidenceService(
				new EmptyMetadataSemanticSearchService());

		var plan = BuildBareDetailPlan();

		var validationResult =
			new QueryPlanValidationPipelineResult
			{
				Plan = plan,
				ValidationResult = new QuerySemanticValidationResult(),
				RepairTrace = new QueryPlanRepairTrace
				{
					Status = QueryPlanRepairTraceStatus.NotRequired
				}
			};

		var withEmpty =
			await service.EvaluateAsync(
				plan,
				validationResult,
				validationResult.RepairTrace,
				"最近十条入库凭证",
				QueryPlanLearningContext.Empty);

		var without =
			await service.EvaluateAsync(
				plan,
				validationResult,
				validationResult.RepairTrace,
				"最近十条入库凭证");

		Assert.Equal(without.Level, withEmpty.Level);
		Assert.False(withEmpty.Evidence.LearningApplied);
		Assert.Equal(0, withEmpty.Evidence.LearningRuleCount);
		Assert.Empty(withEmpty.Evidence.LearningRuleKinds);
	}

	[Fact]
	public void Learning_context_dedups_kinds_but_counts_raw_hits()
	{
		var context =
			QueryPlanLearningContext.From(
				new List<CorrectionKind>
				{
					CorrectionKind.ValueMap,
					CorrectionKind.ValueMap,
					CorrectionKind.TableOverride
				});

		Assert.True(context.Any);
		Assert.Equal(3, context.RuleCount);
		Assert.Equal(2, context.RuleKinds.Count);
		Assert.True(context.HasTableOverride);

		Assert.False(QueryPlanLearningContext.From(null).Any);
		Assert.False(
			QueryPlanLearningContext.From(
				new List<CorrectionKind>()).Any);
	}

	private static QueryPlan BuildBareDetailPlan() =>
		new()
		{
			IsAggregate = false,
			Limit = 10,
			Tables =
			{
				new QueryTable
				{
					MetadataTableId = 1001,
					TableName = "wms_storage_receipt"
				}
			}
		};

	/// <summary>
	/// 只返回一条「低分」语义向量命中。
	///
	/// 用途：让计划自身的置信度落在 Low（证据弱但无硬错误），
	/// 从而可以观察「学习规则回放」把下限抬到 Medium 的效果。
	/// 分数取值使归一化得分约 0.54（&lt; Medium 阈值 0.60）。
	/// </summary>
	private sealed class LowScoreSemanticSearchService
		: IMetadataSemanticSearchService
	{
		private const double WeakScore = 0.20;

		public Task<List<MetadataSemanticSearchResult>> SearchAsync(
			string question,
			int topK = 10,
			LocaleContext? locale = null) =>
			Task.FromResult(
				new List<MetadataSemanticSearchResult>
				{
					new()
					{
						VectorType = "semantic",
						Score = WeakScore
					}
				});

		public Task<List<MetadataSemanticSearchResult>> SearchByKeywordAsync(
			string keyword,
			int limit = 30) =>
			Task.FromResult(new List<MetadataSemanticSearchResult>());

		public Task<List<MetadataSemanticSearchResult>> SearchByKeywordSubstringAsync(
			string keyword,
			int limit = 30) =>
			Task.FromResult(new List<MetadataSemanticSearchResult>());
	}

	private sealed class EmptyMetadataSemanticSearchService
		: IMetadataSemanticSearchService
	{
		public Task<List<MetadataSemanticSearchResult>> SearchAsync(
			string question,
			int topK = 10,
			LocaleContext? locale = null) =>
			Task.FromResult(new List<MetadataSemanticSearchResult>());

		public Task<List<MetadataSemanticSearchResult>> SearchByKeywordAsync(
			string keyword,
			int limit = 30) =>
			Task.FromResult(new List<MetadataSemanticSearchResult>());

		public Task<List<MetadataSemanticSearchResult>> SearchByKeywordSubstringAsync(
			string keyword,
			int limit = 30) =>
			Task.FromResult(new List<MetadataSemanticSearchResult>());
	}
}
