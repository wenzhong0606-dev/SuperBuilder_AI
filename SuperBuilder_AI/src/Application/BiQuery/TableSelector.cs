using System;
using System.Collections.Generic;
using System.Linq;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 候选表选择器。
///
/// 根据 MetadataSemanticSearchResult 选择当前查询的最佳主表。
///
/// 纯结构化重构：方法体从 QueryPlanBuilder 平移而来，评分公式与输出保持不变。
/// 仅将共享的文本归一化/业务关联判断转发给 FieldResolver，避免逻辑漂移。
/// </summary>
public sealed class TableSelector
{
	private readonly FieldResolver _fieldResolver;

	/// <summary>
	/// 创建候选表选择器。
	/// </summary>
	public TableSelector(
		FieldResolver fieldResolver)
	{
		_fieldResolver =
			fieldResolver
			?? throw new ArgumentNullException(
				nameof(fieldResolver));
	}

	/// <summary>
	/// 候选表评分结果。
	///
	/// 注意：
	/// 当前阶段只负责把原有 SelectBestTable 中的评分过程
	/// 结构化，不改变实际生效的评分公式。
	/// </summary>
	private sealed class TableCandidateScore
	{
		public MetadataTable Table { get; init; } = null!;

		public int MatchedColumns { get; init; }

		public double ColumnScore { get; init; }

		public double TableScore { get; init; }

		public double Boost { get; init; }

		public int LocalMatchCount { get; init; }

		public double FinalScore { get; init; }
	}

	/// <summary>
	/// 根据 MetadataSemanticSearchResult 选择当前查询的最佳主表。
	/// </summary>
	internal MetadataTable?
		SelectBestTable(
			List<MetadataSemanticSearchResult> results,
			List<BusinessTerm> businessTerms,
			QueryIntent? intent = null,
			Func<string, MetadataTable?>? tableResolver = null)
	{
		if (results == null || results.Count == 0)
		{
			return null;
		}

		/*
         * 按 MetadataTable 分组。
         */

		var groupedCandidates =
			results
				.Where(x => x.Table != null)
				.GroupBy(x => x.Table!.Id)
				.Select(group => new
				{
					Table = group.First().Table!,
					Results = group.ToList()
				})
				.ToList();

		if (groupedCandidates.Count == 0)
		{
			return null;
		}

		var explicitTable =
			ResolveExplicitTableOverride(
				intent,
				groupedCandidates.Select(x => x.Table),
				tableResolver);

		if (explicitTable is not null)
		{
			return explicitTable;
		}

		/*
         * 只保留有字段命中的候选表。
         */

		var candidatesWithColumnMatches =
			groupedCandidates
				.Where(x =>
					x.Results
						.Where(r => r.Column != null)
						.Select(r => r.Column!.Id)
						.Distinct()
						.Any())
				.ToList();

		var candidates =
			candidatesWithColumnMatches.Count > 0
				? candidatesWithColumnMatches
				: groupedCandidates;

		/*
         * 对每张候选表只计算一次评分。
         */

		var scoredCandidates =
			candidates
				.Select(x =>
					ScoreTableCandidate(
						x.Table,
						x.Results,
						businessTerms))
				.Where(x => x != null)
				.Cast<TableCandidateScore>()
				.OrderByDescending(x => x.FinalScore)
				.ThenByDescending(x => x.MatchedColumns)
				.ThenByDescending(x => x.ColumnScore)
				.ThenByDescending(x => x.TableScore)
				.ToList();

		return scoredCandidates
			.FirstOrDefault()
			?.Table;
	}

	private static MetadataTable? ResolveExplicitTableOverride(
		QueryIntent? intent,
		IEnumerable<MetadataTable> candidates,
		Func<string, MetadataTable?>? tableResolver = null)
	{
		var question =
			intent?.OriginalQuestion;

		if (string.IsNullOrWhiteSpace(question))
		{
			return null;
		}

		var correctionContext =
			question.Contains("物理表必须使用", StringComparison.OrdinalIgnoreCase)
			|| question.Contains("查询表必须使用", StringComparison.OrdinalIgnoreCase)
			|| question.Contains("查询表错误", StringComparison.OrdinalIgnoreCase)
			|| question.Contains("表错", StringComparison.OrdinalIgnoreCase)
			|| question.Contains("错表", StringComparison.OrdinalIgnoreCase)
			|| question.Contains("表不对", StringComparison.OrdinalIgnoreCase)
			|| question.Contains("应该是", StringComparison.OrdinalIgnoreCase)
			|| question.Contains("应为", StringComparison.OrdinalIgnoreCase)
			|| question.Contains("改成", StringComparison.OrdinalIgnoreCase)
			|| question.Contains("改为", StringComparison.OrdinalIgnoreCase);

		if (!correctionContext)
		{
			return null;
		}

		// 1) 优先在收敛后的候选里按表名匹配（历史行为，零额外 IO）。
		var byCandidate =
			candidates
				.Where(t => !string.IsNullOrWhiteSpace(t.TableName))
				.Where(t => question.Contains(t.TableName!, StringComparison.OrdinalIgnoreCase))
				.OrderByDescending(t => t.TableName!.Length)
				.FirstOrDefault();

		if (byCandidate is not null)
		{
			return byCandidate;
		}

		// 2) 候选里没有（典型场景：ScopeAsync 已把目标表所在数据源收敛掉）：
		//    从全部已授权数据源按表名确定性找回，强制作为主表。
		//    仅当 resolver 返回非空时才生效，避免引入检索候选之外的不可信表。
		if (tableResolver is not null)
		{
			var correctedName = ExtractCorrectedTableName(question);
			if (!string.IsNullOrWhiteSpace(correctedName))
			{
				var resolved = tableResolver(correctedName);
				if (resolved is not null)
				{
					return resolved;
				}
			}
		}

		return null;
	}

	/// <summary>
	/// 从纠正句中提取用户显式指定的物理表名。
	/// 合成问题形如「…查询物理表必须使用 wms_storage_receipt…」，
	/// 原始问题本身不含物理表名，因此取问题中首个含下划线的英文表名 token 即目标表。
	/// </summary>
	private static readonly System.Text.RegularExpressions.Regex
		PhysicalTableNameRegex =
			new(
				@"(?<![A-Za-z0-9_])(?<table>[A-Za-z][A-Za-z0-9_]{2,})(?![A-Za-z0-9_])",
				System.Text.RegularExpressions.RegexOptions.Compiled
				| System.Text.RegularExpressions.RegexOptions.CultureInvariant);

	private static string? ExtractCorrectedTableName(string question)
	{
		foreach (System.Text.RegularExpressions.Match m in
			PhysicalTableNameRegex.Matches(question))
		{
			var value = m.Groups["table"].Value;
			if (value.Contains('_', StringComparison.Ordinal)
				&& !string.Equals(
					value,
					"QueryPlan",
					StringComparison.OrdinalIgnoreCase))
			{
				return value;
			}
		}

		return null;
	}

	/// <summary>
	/// 对单个候选表进行评分。
	/// </summary>
	private TableCandidateScore?
		ScoreTableCandidate(
			MetadataTable table,
			List<MetadataSemanticSearchResult> results,
			List<BusinessTerm> businessTerms)
	{
		if (table == null)
		{
			return null;
		}

		var matchedColumns =
			results
				.Where(x => x.Column != null)
				.Select(x => x.Column!.Id)
				.Distinct()
				.Count();

		var columnScore =
			results
				.Where(x => x.Column != null)
				.Select(x => x.Score)
				.DefaultIfEmpty(0)
				.Max();

		var tableScore =
			results
				.Where(x => x.IsTableVector)
				.Select(x => x.Score)
				.DefaultIfEmpty(0)
				.Max();

		var boost =
			CalculateTableBoost(
				table,
				businessTerms);

		var localMatchCount =
			CalculateLocalMatchCount(
				table,
				businessTerms);

		var finalScore =
			matchedColumns * 10.0
			+
			columnScore * 5.0
			+
			tableScore * 2.0
			+
			boost
			+
			localMatchCount * 20.0;

		return new TableCandidateScore
		{
			Table = table,

			MatchedColumns =
				matchedColumns,

			ColumnScore =
				columnScore,

			TableScore =
				tableScore,

			Boost =
				boost,

			LocalMatchCount =
				localMatchCount,

			FinalScore =
				finalScore
		};
	}

	/// <summary>
	/// 计算表级业务文本匹配 Boost。
	/// </summary>
	private double
		CalculateTableBoost(
			MetadataTable table,
			List<BusinessTerm> businessTerms)
	{
		if (table == null ||
			businessTerms == null ||
			businessTerms.Count == 0)
		{
			return 0.0;
		}

		var boost = 0.0;

		try
		{
			var tableNameText =
				_fieldResolver.NormalizeText(
					table.TableName);

			var tableCommentText =
				_fieldResolver.NormalizeText(
					table.TableComment);

			foreach (var term in businessTerms)
			{
				var normalizedTerm =
					_fieldResolver.NormalizeText(term);

				if (string.IsNullOrWhiteSpace(
					normalizedTerm))
				{
					continue;
				}

				if (
					(
						!string.IsNullOrWhiteSpace(
							tableNameText)
						&&
						tableNameText.Contains(
							normalizedTerm,
							StringComparison.OrdinalIgnoreCase)
					)
					||
					(
						!string.IsNullOrWhiteSpace(
							tableCommentText)
						&&
						tableCommentText.Contains(
							normalizedTerm,
							StringComparison.OrdinalIgnoreCase)
					)
				)
				{
					boost += 50.0;
					break;
				}
			}

			var tableSearchText =
				_fieldResolver.NormalizeText(
					table.SearchText);

			foreach (var term in businessTerms)
			{
				var normalizedTerm =
					_fieldResolver.NormalizeText(term);

				if (string.IsNullOrWhiteSpace(
					normalizedTerm))
				{
					continue;
				}

				if (
					(
						!string.IsNullOrWhiteSpace(
							tableNameText)
						&&
						tableNameText.Contains(
							normalizedTerm,
							StringComparison.OrdinalIgnoreCase)
					)
					||
					(
						!string.IsNullOrWhiteSpace(
							tableCommentText)
						&&
						tableCommentText.Contains(
							normalizedTerm,
							StringComparison.OrdinalIgnoreCase)
					)
					||
					(
						!string.IsNullOrWhiteSpace(
							tableSearchText)
						&&
						tableSearchText.Contains(
							normalizedTerm,
							StringComparison.OrdinalIgnoreCase)
					)
				)
				{
					boost += 200.0;
					break;
				}

				if (table.Columns == null)
				{
					continue;
				}

				var columnSemanticMatched =
					false;

				foreach (var column in table.Columns)
				{
					try
					{
						if (column.Semantic == null)
						{
							continue;
						}

						var semanticText =
							_fieldResolver.NormalizeText(
								string.Join(
									" ",
									new[]
									{
										column.Semantic.BusinessMeaning,
										column.Semantic.Keywords,
										column.Semantic.Synonyms,
										column.Semantic.ExampleQuestions,
										column.Semantic.SearchText
									}));

						if (
							!string.IsNullOrWhiteSpace(
								semanticText)
							&&
							semanticText.Contains(
								normalizedTerm,
								StringComparison.OrdinalIgnoreCase)
						)
						{
							boost += 100.0;
							columnSemanticMatched = true;
							break;
						}
					}
					catch
					{
					}
				}

				if (columnSemanticMatched)
				{
					break;
				}
			}
		}
		catch
		{
		}

		return boost;
	}

	/// <summary>
	/// 计算候选表内部列与业务词的直接匹配数量。
	/// </summary>
	private int
		CalculateLocalMatchCount(
			MetadataTable table,
			List<BusinessTerm> businessTerms)
	{
		if (table == null ||
			table.Columns == null ||
			businessTerms == null ||
			businessTerms.Count == 0)
		{
			return 0;
		}

		var localMatchCount = 0;

		try
		{
			foreach (var term in businessTerms)
			{
				var normalizedTerm =
					_fieldResolver.NormalizeText(term);

				if (string.IsNullOrWhiteSpace(
					normalizedTerm))
				{
					continue;
				}

				foreach (var column in table.Columns)
				{
					try
					{
						var columnName =
							_fieldResolver.NormalizeText(
								column.ColumnName);

						var columnComment =
							_fieldResolver.NormalizeText(
								column.ColumnComment);

						var matched =
							(
								!string.IsNullOrWhiteSpace(
									columnName)
								&&
								columnName.Contains(
									normalizedTerm,
									StringComparison.OrdinalIgnoreCase)
							)
							||
							(
								!string.IsNullOrWhiteSpace(
									columnComment)
								&&
								columnComment.Contains(
									normalizedTerm,
									StringComparison.OrdinalIgnoreCase)
							)
							||
							_fieldResolver.IsRelatedBusinessText(
								term,
								column);

						if (matched)
						{
							localMatchCount++;
						}
					}
					catch
					{
					}
				}
			}
		}
		catch
		{
		}

		return localMatchCount;
	}
}
