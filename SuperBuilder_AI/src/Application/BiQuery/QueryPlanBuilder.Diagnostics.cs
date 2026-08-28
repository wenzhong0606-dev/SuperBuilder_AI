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
	public class QueryPlanDiagnostics
	{
		public QueryIntent Intent { get; set; } = null!;
		public List<string> BusinessTerms { get; set; } = new();
		public Dictionary<string, List<MetadataSemanticSearchResult>> PerTermResults { get; set; } = new();
		public List<CandidateDiagnostic> Candidates { get; set; } = new();
		public QueryPlan? Plan { get; set; }
		public string? PlanError { get; set; }
	}

	public class CandidateDiagnostic
	{
		public long TableId { get; set; }
		public string? TableName { get; set; }
		public int MatchedColumns { get; set; }
		public double ColumnScore { get; set; }
		public double TableScore { get; set; }
		public double Boost { get; set; }
		public int LocalMatch { get; set; }
		public double FinalScore { get; set; }
	}

	/// <summary>
	/// 构建带诊断信息的 QueryPlan，用于调试链路：intent -> businessTerms -> per-term召回 -> 候选评分 -> 最终 Plan
	/// </summary>
	public async Task<QueryPlanDiagnostics> BuildWithDiagnosticsAsync(QueryIntent intent)
	{
		if (intent == null) throw new ArgumentNullException(nameof(intent));

		var diagnostics = new QueryPlanDiagnostics { Intent = intent };

		// 1. business terms
		var businessTerms = _businessTermExtractor.CollectBusinessTerms(intent);
		// fallback when no terms extracted
		if (businessTerms.Count == 0)
		{
			businessTerms = _businessTermExtractor.CollectBusinessTermsFallback(intent);
		}
		diagnostics.BusinessTerms = businessTerms;

		// 2. per-term raw results
		foreach (var term in businessTerms)
		{
			var items = await _metadataSearch.SearchAsync(term, 10);
			diagnostics.PerTermResults[term] = items.ToList();
		}

		// 3. aggregated metadata results (same as SearchMetadataAsync)
		var metadataResults = await _businessTermExtractor.SearchMetadataAsync(businessTerms);

		// 4. build candidate diagnostics
		var groups = metadataResults.Where(x => x.Table != null).GroupBy(x => x.Table!.Id);
		var candidates = new List<CandidateDiagnostic>();
		foreach (var g in groups)
		{
			var table = g.First().Table!;
			var columnResults = g.Where(x => x.Column != null).ToList();
			var tableResults = g.Where(x => x.IsTableVector).ToList();
			var matchedColumns = columnResults.Select(x => x.Column!.Id).Distinct().Count();
			var columnScore = columnResults.Select(x => x.Score).DefaultIfEmpty(0).Max();
			var tableScore = tableResults.Select(x => x.Score).DefaultIfEmpty(0).Max();

			double boost = 0.0;
			try
			{
				var tableNameText = _fieldResolver.NormalizeText(table.TableName);
				var tableCommentText = _fieldResolver.NormalizeText(table.TableComment);
				foreach (var term in businessTerms)
				{
					var t = _fieldResolver.NormalizeText(term);
					if (string.IsNullOrWhiteSpace(t)) continue;
					if ((!string.IsNullOrWhiteSpace(tableNameText) && tableNameText.Contains(t, StringComparison.OrdinalIgnoreCase)) ||
						(!string.IsNullOrWhiteSpace(tableCommentText) && tableCommentText.Contains(t, StringComparison.OrdinalIgnoreCase)))
					{
						boost += 50.0;
						break;
					}
				}
			}
			catch { }

			int localMatchCount = 0;
			try
			{
				if (table.Columns != null)
				{
					foreach (var term in businessTerms)
					{
						var t = _fieldResolver.NormalizeText(term);
						if (string.IsNullOrWhiteSpace(t)) continue;
						foreach (var col in table.Columns)
						{
							try
							{
								var colName = _fieldResolver.NormalizeText(col.ColumnName);
								var colComment = _fieldResolver.NormalizeText(col.ColumnComment);
								if ((!string.IsNullOrWhiteSpace(colName) && colName.Contains(t, StringComparison.OrdinalIgnoreCase)) ||
									(!string.IsNullOrWhiteSpace(colComment) && colComment.Contains(t, StringComparison.OrdinalIgnoreCase)) ||
									_fieldResolver.IsRelatedBusinessText(term, col))
								{
									localMatchCount++;
								}
							}
							catch { }
						}
					}
				}
			}
			catch { }

			var finalScore = matchedColumns * 10.0 + columnScore * 5.0 + tableScore * 2.0 + boost + localMatchCount * 20.0;

			candidates.Add(new CandidateDiagnostic
			{
				TableId = table.Id,
				TableName = table.TableName,
				MatchedColumns = matchedColumns,
				ColumnScore = columnScore,
				TableScore = tableScore,
				Boost = boost,
				LocalMatch = localMatchCount,
				FinalScore = finalScore
			});
		}

		diagnostics.Candidates = candidates.OrderByDescending(c => c.FinalScore).ThenByDescending(c => c.MatchedColumns).ToList();

		// 5. final plan (reuse existing BuildAsync to ensure consistency)
		try
		{
			diagnostics.Plan = await BuildAsync(intent);
		}
		catch (Exception ex)
		{
			diagnostics.PlanError = ex.Message;
		}

		return diagnostics;
	}






	/// <summary>
	/// 创建查询计划。
	///
	/// 执行流程:
	///
	/// QueryIntent
	///      ↓
	/// 提取业务字段
	///      ↓
	/// MetadataSemanticSearch
	///      ↓
	/// 候选表评分
	///      ↓
	/// 确定主表
	///      ↓
	/// 字段映射
	///      ↓
	/// QueryPlan
	/// </summary>
}
