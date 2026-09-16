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
