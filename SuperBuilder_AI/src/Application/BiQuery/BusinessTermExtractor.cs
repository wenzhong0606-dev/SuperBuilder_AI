using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 业务术语抽取器。
///
/// 从 QueryIntent / 原始问题中提取业务字段（业务术语 / 实体），
/// 并对每个业务字段独立执行 Metadata 语义搜索。
///
/// 纯结构化重构：方法体从 QueryPlanBuilder 平移而来，逻辑与输出保持不变。
/// </summary>
public sealed class BusinessTermExtractor
{
	private readonly IMetadataSemanticSearchService
		_metadataSearch;

	/// <summary>
	/// 创建业务术语抽取器。
	/// </summary>
	public BusinessTermExtractor(
		IMetadataSemanticSearchService metadataSearch)
	{
		_metadataSearch =
			metadataSearch
			?? throw new ArgumentNullException(
				nameof(metadataSearch));
	}

	/// <summary>
	/// 收集QueryIntent中的业务字段。
	///
	/// Metric:
	///
	/// Name
	/// Field
	///
	/// Filter:
	///
	/// Field
	///
	/// Dimension:
	///
	/// Dimension
	///
	/// OrderBy:
	///
	/// OrderBy
	/// </summary>
	internal List<string>
		CollectBusinessTerms(
			QueryIntent intent)
	{
		var terms =
			new List<string>();

		/*
		 * Metrics
		 */

		foreach (var metric in intent.Metrics)
		{
			// 原始名称与字段
			AddTerm(terms, metric.Name);
			AddTerm(terms, metric.Field);

			// 生成名称的变体（去掉常见度量词/年份等），例如 "入库凭证条数" -> "入库凭证"
			foreach (var variant in GenerateTermVariants(metric.Name))
			{
				AddTerm(terms, variant);
			}

		}

		/*
		 * Filters
		 */

		foreach (var filter in intent.Filters)
		{
			AddTerm(
				terms,
				filter.Field);
		}

		/*
		 * Dimensions
		 */

		foreach (var dimension in intent.Dimensions)
		{
			AddTerm(
				terms,
				dimension);
		}

		/*
		 * OrderBy
		 */

		AddTerm(
			terms,
			intent.OrderBy);

		return terms
			.Distinct(
				StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	/// <summary>
	/// 如果 CollectBusinessTerms 未能提取到任何业务词，使用 OriginalQuestion 做兜底处理，
	/// 生成若干变体并拆分关键词以提高召回概率。
	/// </summary>
	internal List<string> CollectBusinessTermsFallback(QueryIntent intent)
	{
		var terms = new List<string>();

		if (intent == null || string.IsNullOrWhiteSpace(intent.OriginalQuestion))
		{
			return terms;
		}

		// 先尝试基于原始问题生成变体
		foreach (var v in GenerateTermVariants(intent.OriginalQuestion))
		{
			AddTerm(terms, v);
		}

		// 进一步按空白或标点拆分，去掉表示时间、数量的词
		var parts = Regex.Split(intent.OriginalQuestion, "[\\s\\p{P}\\p{S}]+")
			.Where(p => !string.IsNullOrWhiteSpace(p))
			.Select(p => p.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		var stopWords = new[] { "最近", "近", "条", "个", "天", "月", "年", "前" };

		foreach (var p in parts)
		{
			if (stopWords.Any(sw => p.Contains(sw)))
				continue;

			// 排除纯数字
			if (Regex.IsMatch(p, "^\\d+$"))
				continue;

			AddTerm(terms, p);
		}

		return terms.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
	}

	/// <summary>
	/// 为业务文本生成若干变体，用于增强语义检索召回率。
	/// 例如去掉度量后缀（条数/数量/个数/总数/金额），去掉年份（2025年）等。
	/// </summary>
	private IEnumerable<string> GenerateTermVariants(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			yield break;

		var original = value.Trim();
		yield return original;

		// 去掉常见的度量后缀
		var cleaned = Regex.Replace(original, "(条数|数量|个数|总数|金额)$", "", RegexOptions.IgnoreCase).Trim();
		if (!string.Equals(cleaned, original, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(cleaned))
			yield return cleaned;

		// 去掉年份例如 2025年
		var noYear = Regex.Replace(cleaned, "\\d{4}年", "", RegexOptions.IgnoreCase).Trim();
		if (!string.Equals(noYear, cleaned, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(noYear))
			yield return noYear;

		// 进一步按空白或标点分割并返回每个子片段
		var parts = Regex.Split(noYear, "[\\u0020\\p{P}\\p{S}]+", RegexOptions.None)
			.Where(p => !string.IsNullOrWhiteSpace(p))
			.Distinct(StringComparer.OrdinalIgnoreCase);

		foreach (var p in parts)
		{
			if (!string.Equals(p, original, StringComparison.OrdinalIgnoreCase) && !string.Equals(p, cleaned, StringComparison.OrdinalIgnoreCase) && !string.Equals(p, noYear, StringComparison.OrdinalIgnoreCase))
				yield return p;
		}
	}

	/// <summary>
	/// 添加业务字段。
	/// </summary>
	private void AddTerm(
		List<string> terms,
		string? value)
	{
		if (string.IsNullOrWhiteSpace(
			value))
		{
			return;
		}

		terms.Add(
			value.Trim());
	}

	/// <summary>
	/// 对所有业务字段进行Metadata语义搜索。
	///
	/// 每个业务字段独立搜索。
	/// </summary>
	internal async Task<List<MetadataSemanticSearchResult>>
		SearchMetadataAsync(
			List<string> terms)
	{
		var results =
			new List<MetadataSemanticSearchResult>();

		foreach (var term in terms)
		{
			var items =
				await _metadataSearch
				.SearchAsync(
					term,
					10);

			results.AddRange(
				items);
		}

		/*
		 * 同一个Vector可能因为多个业务字段被重复召回。
		 *
		 * 去重:
		 *
		 * VectorId + Column/Table
		 */

		return results
			.GroupBy(
				x =>
					$"{x.VectorType}:{x.VectorId}",
				StringComparer.OrdinalIgnoreCase)
			.Select(
				x =>
					x.OrderByDescending(
						r => r.Score)
					.First())
			.ToList();
	}
}
