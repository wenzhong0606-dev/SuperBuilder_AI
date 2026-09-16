using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 字段解析器。
///
/// 将业务字段（Metric / Filter / Dimension / OrderBy）解析为 MetadataColumn，
/// 并计算本地文本匹配度与语义重叠度。
///
/// 纯结构化重构：方法体从 QueryPlanBuilder 平移而来，逻辑与输出保持不变。
/// </summary>
public sealed class FieldResolver
{
	/// <summary>
	/// 优先处理明确的字段映射：如果 Metric.Field 明确为 id 或者类似主键标识，优先使用主键或 id 字段
	/// </summary>
	internal MetadataColumn?
		ResolveMetricField(
			QueryMetric metric,
			MetadataTable table,
			List<MetadataSemanticSearchResult> results)
	{
		if (table is null)
			return null;

		// 优先处理明确的字段映射：如果 Metric.Field 明确为 id 或者类似主键标识，优先使用主键或 id 字段
		try
		{
			if (!string.IsNullOrWhiteSpace(metric.Field) && table.Columns != null)
			{
				var rawField = metric.Field.Trim();
				// M10 修复：使用词边界感知匹配，避免 "paid"/"void" 等被误判为 ID 字段
				var isIdField = string.Equals(rawField, "id", StringComparison.OrdinalIgnoreCase) ||
					rawField.EndsWith("_id", StringComparison.OrdinalIgnoreCase) ||
					(rawField.Length >= 4 && rawField.EndsWith("Id", StringComparison.Ordinal));
				if (isIdField)
				{
					var pk = table.Columns.FirstOrDefault(c => c.IsPrimaryKey == true);
					if (pk != null) return pk;
					var idCol = table.Columns.FirstOrDefault(c => string.Equals(c.ColumnName, "id", StringComparison.OrdinalIgnoreCase));
					if (idCol != null) return idCol;
				}
			}
		}
		catch { }

		/*
		 * 第一优先级:
		 *
		 * Metric.Field
		 */

		if (!string.IsNullOrWhiteSpace(
			metric.Field))
		{
			var column =
				ResolveColumn(
					metric.Field,
					table,
					results);

			if (column != null)
			{
				return column;
			}
		}

		/*
		 * 第二优先级:
		 *
		 * Metric.Name
		 */

		if (!string.IsNullOrWhiteSpace(
			metric.Name))
		{
			return ResolveColumn(
				metric.Name,
				table,
				results);
		}

		return null;
	}

	internal Task<MetadataColumn?>
		ResolveMetricFieldAsync(
			QueryMetric metric,
			MetadataTable table,
			List<MetadataSemanticSearchResult> results)
	{
		// 当前实现与同步版本一致，保留异步签名以便将来扩展
		return Task.FromResult(ResolveMetricField(metric, table, results));
	}

	/// <summary>
	/// 将业务字段解析为MetadataColumn。
	///
	/// 匹配顺序:
	///
	/// 1. 数据库字段名完全匹配
	/// 2. BusinessKey匹配
	/// 3. 业务名称匹配
	/// 4. SearchText / Semantic匹配
	/// 5. Qdrant Score
	///
	/// 只允许匹配当前主表。
	/// </summary>
	internal MetadataColumn?
		ResolveColumn(
			string? businessField,
			MetadataTable table,
			List<MetadataSemanticSearchResult> results)
	{
		if (string.IsNullOrWhiteSpace(
			businessField))
		{
			return null;
		}

		// 优先：如果业务字段与表中某列的 ColumnName 或 BusinessKey 精确匹配，直接返回该列（避免语义召回错误）
		try
		{
			var normalizedField = NormalizeText(businessField);
			// 先在语义检索的 results 中查找精确匹配的列（当 table.Columns 可能未被完整加载时）
			var resultExact = results
				.Where(x => x.Table != null && x.Table.Id == table.Id && x.Column != null)
				.Select(x => x.Column!)
				.FirstOrDefault(c =>
					(!string.IsNullOrWhiteSpace(c.ColumnName) && string.Equals(c.ColumnName, businessField, StringComparison.OrdinalIgnoreCase))
					|| (!string.IsNullOrWhiteSpace(NormalizeText(c.ColumnName)) && string.Equals(NormalizeText(c.ColumnName), normalizedField, StringComparison.OrdinalIgnoreCase))
					|| (!string.IsNullOrWhiteSpace(c.BusinessKey) && string.Equals(NormalizeText(c.BusinessKey), normalizedField, StringComparison.OrdinalIgnoreCase)));

			if (resultExact != null)
			{
				return resultExact;
			}

			if (table.Columns != null)
			{
				var exactMatch = table.Columns.FirstOrDefault(c =>
					(!string.IsNullOrWhiteSpace(c.ColumnName) && string.Equals(c.ColumnName, businessField, StringComparison.OrdinalIgnoreCase))
					|| (!string.IsNullOrWhiteSpace(NormalizeText(c.ColumnName)) && string.Equals(NormalizeText(c.ColumnName), normalizedField, StringComparison.OrdinalIgnoreCase))
					|| (!string.IsNullOrWhiteSpace(c.BusinessKey) && string.Equals(NormalizeText(c.BusinessKey), normalizedField, StringComparison.OrdinalIgnoreCase)));

				if (exactMatch != null)
				{
					return exactMatch;
				}
			}
		}
		catch
		{
			// 忽略匹配异常，继续后续逻辑
		}

		var candidates =
			results

			.Where(
				x =>
					x.Column != null
					&&
					x.Table != null
					&&
					x.Table.Id == table.Id)

			.Select(
				x =>
					new ColumnCandidate
					{
						Result = x,

						Column =
							x.Column!,

						Score =
							x.Score
					})

			.ToList();

		if (table.Columns != null)
		{
			foreach (var column in table.Columns)
			{
				if (candidates.Any(
					x =>
						x.Column.Id ==
						column.Id))
				{
					continue;
				}

				candidates.Add(
					new ColumnCandidate
					{
						Result = null,

						Column =
							column,

						Score = 0
					});
			}
		}

		if (candidates.Count == 0)
		{
			return null;
		}

		/*
		 * 计算字段匹配评分。
		 */

		var scored =
			candidates

			.Select(
				x =>
				{
					var lexicalScore =
						CalculateLexicalScore(
							businessField,
							x.Column);

					var semanticScore =
						x.Score;

					/*
					 * 本地字段名称匹配优先级高于
					 * 单纯Qdrant相似度。
					 */

					var finalScore =
						lexicalScore * 100
						+
						semanticScore * 10;

					return new
					{
						x.Column,

						LexicalScore =
							lexicalScore,

						SemanticScore =
							semanticScore,

						FinalScore =
							finalScore
					};
				})

			.OrderByDescending(
				x =>
					x.FinalScore)

			.ToList();

		var best =
			scored.First();

		/*
		 * 如果已经存在明确的字段名称匹配，
		 * 直接使用。
		 */

		if (best.LexicalScore >= 0.8)
		{
			return best.Column;
		}

		/*
		 * 否则使用语义搜索结果。
		 */

		if (best.SemanticScore >= 0.30)
		{
			return best.Column;
		}

		/*
		 * 最后处理一个非常重要的情况:
		 *
		 * Metric:
		 *
		 * 入库凭证条数
		 *
		 * Column:
		 *
		 * 入库凭证
		 */

		var relaxed =
			scored

			.Where(
				x =>
					IsRelatedBusinessText(
						businessField,
						x.Column))

			.OrderByDescending(
				x =>
					x.FinalScore)

			.FirstOrDefault();

		return relaxed?.Column;
	}

	internal Task<MetadataColumn?>
		ResolveColumnAsync(
			string? businessField,
			MetadataTable table,
			List<MetadataSemanticSearchResult> results)
	{
		// 目前逻辑为同步，包装为 Task 以便在 BuildAsync 中统一 await
		return Task.FromResult(ResolveColumn(businessField, table, results));
	}

	/// <summary>
	/// 计算业务字段与MetadataColumn的本地文本匹配度。
	/// </summary>
	private double
		CalculateLexicalScore(
			string businessField,
			MetadataColumn column)
	{
		var query =
			NormalizeText(
				businessField);

		if (string.IsNullOrWhiteSpace(
			query))
		{
			return 0;
		}

		var scores =
			new List<double>();

		/*
		 * ColumnName
		 */

		AddTextScore(
			scores,
			query,
			column.ColumnName,
			1.0);

		/*
		 * BusinessKey
		 */

		AddTextScore(
			scores,
			query,
			column.BusinessKey,
			0.95);

		/*
		 * SearchText
		 */

		AddTextScore(
			scores,
			query,
			column.SearchText,
			0.90);

		/*
		 * Semantic
		 */

		if (column.Semantic != null)
		{
			AddTextScore(
				scores,
				query,
				column.Semantic.BusinessMeaning,
				0.95);

			AddTextScore(
				scores,
				query,
				column.Semantic.Keywords,
				0.90);

			AddTextScore(
				scores,
				query,
				column.Semantic.Synonyms,
				0.90);

			AddTextScore(
				scores,
				query,
				column.Semantic.ExampleQuestions,
				0.85);

			AddTextScore(
				scores,
				query,
				column.Semantic.SearchText,
				0.85);
		}

		return scores
			.DefaultIfEmpty(0)
			.Max();
	}

	/// <summary>
	/// 计算文本匹配分数。
	/// </summary>
	private void AddTextScore(
		List<double> scores,
		string query,
		string? target,
		double weight)
	{
		if (string.IsNullOrWhiteSpace(
			target))
		{
			return;
		}

		var normalizedTarget =
			NormalizeText(
				target);

		if (string.IsNullOrWhiteSpace(
			normalizedTarget))
		{
			return;
		}

		/*
		 * 完全相等。
		 */

		if (query.Equals(
			normalizedTarget,
			StringComparison.OrdinalIgnoreCase))
		{
			scores.Add(
				1.0 * weight);
			return;
		}

		/*
		 * 查询内容包含Metadata文本。
		 */

		if (query.Contains(
			normalizedTarget,
			StringComparison.OrdinalIgnoreCase))
		{
			scores.Add(
				0.90 * weight);
			return;
		}

		/*
		 * Metadata文本包含查询内容。
		 */

		if (normalizedTarget.Contains(
			query,
			StringComparison.OrdinalIgnoreCase))
		{
			scores.Add(
				0.85 * weight);
			return;
		}

		/*
		 * 计算中文/英文连续片段重叠。
		 */

		var overlap =
			CalculateTextOverlap(
				query,
				normalizedTarget);

		if (overlap > 0)
		{
			scores.Add(
				overlap * weight);
		}
	}

	/// <summary>
	/// 判断业务字段是否与Metadata字段存在明显业务关联。
	/// </summary>
	internal bool
		IsRelatedBusinessText(
			string businessField,
			MetadataColumn column)
	{
		var query =
			NormalizeText(
				businessField);

		if (string.IsNullOrWhiteSpace(
			query))
		{
			return false;
		}

		var texts =
			new List<string?>()
			{
				column.ColumnName,
				column.BusinessKey,
				column.SearchText
			};

		if (column.Semantic != null)
		{
			texts.Add(
				column.Semantic.BusinessMeaning);

			texts.Add(
				column.Semantic.Keywords);

			texts.Add(
				column.Semantic.Synonyms);

			texts.Add(
				column.Semantic.ExampleQuestions);

			texts.Add(
				column.Semantic.SearchText);
		}

		foreach (var text in texts)
		{
			if (string.IsNullOrWhiteSpace(
				text))
			{
				continue;
			}

			var target =
				NormalizeText(
					text);

			if (query.Contains(
				target,
				StringComparison.OrdinalIgnoreCase)
				||
				target.Contains(
					query,
					StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// 文本重叠计算。
	///
	/// 使用最长公共连续片段近似计算。
	/// </summary>
	private double
		CalculateTextOverlap(
			string source,
			string target)
	{
		if (string.IsNullOrWhiteSpace(source)
			||
			string.IsNullOrWhiteSpace(target))
		{
			return 0;
		}

		var maxLength =
			Math.Min(
				source.Length,
				target.Length);

		var longest =
			0;

		for (
			int length = maxLength;
			length >= 2;
			length--)
		{
			for (
				int i = 0;
				i + length <= source.Length;
				i++)
			{
				var part =
					source.Substring(
						i,
						length);

				if (target.Contains(
					part,
					StringComparison.OrdinalIgnoreCase))
				{
					longest =
						length;

					goto Found;
				}
			}
		}

	Found:

		if (longest == 0)
		{
			return 0;
		}

		return (double)longest
			/
			Math.Max(
				source.Length,
				target.Length);
	}

	/// <summary>
	/// 标准化业务文本。
	///
	/// 去除:
	///
	/// 空格
	/// 下划线
	/// 横线
	/// 常见标点
	/// </summary>
	internal string
		NormalizeText(
			string? text)
	{
		if (string.IsNullOrWhiteSpace(
			text))
		{
			return string.Empty;
		}

		var chars =
			text
			.Trim()
			.ToLowerInvariant()
			.Where(
				c =>
					char.IsLetterOrDigit(c)
					||
					c >= '\u4E00'
					&&
					c <= '\u9FFF')
			.ToArray();

		return new string(
			chars);
	}

	/// <summary>
	/// 添加或者更新QueryField。
	///
	/// 同一个MetadataColumn只生成一个QueryField。
	/// </summary>
	internal void AddOrUpdateQueryField(
		QueryPlan plan,
		MetadataColumn column,
		string? aggregation)
	{
		var existing =
			plan.Fields.FirstOrDefault(
				x =>
					x.MetadataColumnId ==
					column.Id);

		if (existing == null)
		{
			plan.Fields.Add(
				new QueryField
				{
					MetadataColumnId =
						column.Id,

					MetadataTableId =
						column.MetadataTableId,

					TableName =
						column.MetadataTable?.TableName,

					ColumnName =
						column.ColumnName,

					DataType =
						column.DataType,

					Aggregation =
						string.IsNullOrWhiteSpace(
							aggregation)
						? "NONE"
						: aggregation.ToUpperInvariant()
				});

			return;
		}

		/*
		 * 如果原来没有聚合，
		 * 后面发现Metric需要聚合，
		 * 则更新聚合方式。
		 */

		if (string.IsNullOrWhiteSpace(
			existing.Aggregation)
			||
			existing.Aggregation.Equals(
				"NONE",
				StringComparison.OrdinalIgnoreCase))
		{
			if (!string.IsNullOrWhiteSpace(
				aggregation))
			{
				existing.Aggregation =
					aggregation.ToUpperInvariant();
			}
		}
	}

	/// <summary>
	/// Metadata字段候选对象。
	/// </summary>
	private sealed class ColumnCandidate
	{
		/// <summary>
		/// Metadata语义检索结果。
		/// </summary>
		public MetadataSemanticSearchResult?
			Result
		{
			get;
			set;
		}

		/// <summary>
		/// Metadata字段。
		/// </summary>
		public MetadataColumn
			Column
		{
			get;
			set;
		}
			= null!;

		/// <summary>
		/// Qdrant相似度。
		/// </summary>
		public double Score
		{
			get;
			set;
		}
	}
}
