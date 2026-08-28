using System;
using System.Text.RegularExpressions;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Services.BI;

public partial class QueryPlanBuilder : IQueryPlanBuilder
{
	/// <summary>
	/// A4 重构：业务术语抽取转发给 BusinessTermExtractor，逻辑保持原样。
	/// </summary>
	private List<string>
		CollectBusinessTerms(
			QueryIntent intent)
		=> _businessTermExtractor.CollectBusinessTerms(
			intent);

	/// <summary>
	/// A4 重构：业务术语兜底抽取转发给 BusinessTermExtractor，逻辑保持原样。
	/// </summary>
	private List<string> CollectBusinessTermsFallback(QueryIntent intent)
		=> _businessTermExtractor.CollectBusinessTermsFallback(
			intent);

	/// <summary>
	/// A4 重构：Metadata 语义搜索转发给 BusinessTermExtractor，逻辑保持原样。
	/// </summary>
	private async Task<List<MetadataSemanticSearchResult>>
		SearchMetadataAsync(
			List<string> terms)
		=> await _businessTermExtractor.SearchMetadataAsync(
			terms);
}
