using System.Collections.Generic;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;
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
