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
	/// A4 重构：Metric 字段解析转发给 FieldResolver，逻辑保持原样。
	/// </summary>
	private MetadataColumn?
		ResolveMetricField(
			QueryMetric metric,
			MetadataTable table,
			List<MetadataSemanticSearchResult> results)
		=> _fieldResolver.ResolveMetricField(
			metric,
			table,
			results);

	/// <summary>
	/// A4 重构：Metric 字段解析（异步）转发给 FieldResolver，逻辑保持原样。
	/// </summary>
	private Task<MetadataColumn?>
		ResolveMetricFieldAsync(
			QueryMetric metric,
			MetadataTable table,
			List<MetadataSemanticSearchResult> results)
		=> _fieldResolver.ResolveMetricFieldAsync(
			metric,
			table,
			results);

	/// <summary>
	/// A4 重构：业务字段解析转发给 FieldResolver，逻辑保持原样。
	/// </summary>
	private MetadataColumn?
		ResolveColumn(
			string? businessField,
			MetadataTable table,
			List<MetadataSemanticSearchResult> results)
		=> _fieldResolver.ResolveColumn(
			businessField,
			table,
			results);

	/// <summary>
	/// A4 重构：业务字段解析（异步）转发给 FieldResolver，逻辑保持原样。
	/// </summary>
	private Task<MetadataColumn?>
		ResolveColumnAsync(
			string? businessField,
			MetadataTable table,
			List<MetadataSemanticSearchResult> results)
		=> _fieldResolver.ResolveColumnAsync(
			businessField,
			table,
			results);
}
