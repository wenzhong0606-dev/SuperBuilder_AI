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
	/// A4 重构：QueryField 写入转发给 FieldResolver，逻辑保持原样。
	/// </summary>
	private void AddOrUpdateQueryField(
		QueryPlan plan,
		MetadataColumn column,
		string? aggregation)
		=> _fieldResolver.AddOrUpdateQueryField(
			plan,
			column,
			aggregation);

	/// <summary>
	/// A4 重构：JOIN 候选推断转发给 JoinBuilder，逻辑保持原样。
	/// </summary>
	private async Task<List<QueryJoinCandidate>> BuildJoinsAsync(
		MetadataTable mainTable,
		List<MetadataSemanticSearchResult> metadataResults)
		=> await _joinBuilder.BuildJoinsAsync(
			mainTable,
			metadataResults);

	/// <summary>
	/// A4 重构：JOIN 候选转换转发给 JoinBuilder，逻辑保持原样。
	/// </summary>
	private QueryJoin BuildQueryJoin(
		QueryJoinCandidate candidate)
		=> _joinBuilder.BuildQueryJoin(
			candidate);
}
