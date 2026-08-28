using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// QueryPlan 自动修复服务。
///
/// Phase 2.3.2
///
/// 核心职责：
///
/// QueryPlan
///      ↓
/// ValidationResult
///      ↓
/// Semantic Candidate Ranking
///      ↓
/// Confidence Gate
///      ↓
/// Repair QueryPlan
///
/// 本服务不负责：
///
/// 1. SQL 生成
/// 2. SQL 执行
/// 3. MetadataSemanticSearch
/// 4. QueryPlan 重新构建
///
/// Repair 直接修改当前 QueryPlan。
///
/// Candidate Ranking 完全基于：
///
/// - OriginalQuestion
/// - QueryPlan Context
/// - Validation Error
/// - MetadataColumn
/// - MetadataSemantic
///
/// 不包含任何行业业务词典。
/// </summary>
public class QueryPlanRepairService :
	IQueryPlanRepairService
{
	/*
     * Candidate Ranking 安全阈值。
     *
     * Score:
     *     0.0 ~ 1.0
     *
     * Top Candidate 必须：
     *
     * 1. 达到最低置信度
     * 2. 明显领先第二候选
     *
     * 否则不自动修复。
     */

	private const double MinimumConfidence = 0.60;

	private const double MinimumScoreGap = 0.10;


	/// <summary>
	/// QueryPlan 自动修复。
	/// </summary>
	public Task<QueryPlanRepairResult> RepairAsync(
		QueryPlanRepairRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);

		var plan = request.QueryPlan;

		var validationResult =
			request.ValidationResult;

		var context =
			request.ValidationContext;

		if (plan == null)
		{
			return Task.FromResult(
				Failed(
					"QueryPlan 为空，无法执行自动修复。"));
		}

		if (validationResult == null)
		{
			return Task.FromResult(
				Failed(
					"QueryPlanValidationResult 为空，无法执行自动修复。"));
		}

		if (context == null)
		{
			return Task.FromResult(
				Failed(
					"QueryPlanValidationContext 为空，无法执行基于 Metadata 的自动修复。"));
		}

		var actions =
			new List<string>();

		foreach (var error in validationResult.ErrorItems)
		{
			var repaired =
				RepairError(
					plan,
					context,
					request.OriginalQuestion,
					error,
					actions);

			if (!repaired)
			{
				continue;
			}
		}

		if (actions.Count == 0)
		{
			return Task.FromResult(
				Failed(
					"当前 QueryPlan 没有找到满足置信度要求的 Metadata Semantic 修复候选。"));
		}

		return Task.FromResult(
			new QueryPlanRepairResult
			{
				Success = true,

				RepairedPlan = plan,

				RepairActions = actions,

				Explanation =
					$"QueryPlan Repair 完成，共执行 {actions.Count} 项修复。"
			});
	}


	/// <summary>
	/// 根据语义验证错误选择修复策略。
	/// </summary>
	private static bool RepairError(
		QueryPlan plan,
		QueryPlanValidationContext context,
		string? originalQuestion,
		SemanticValidationError error,
		List<string> actions)
	{
		var code =
			NormalizeCode(error.Code);

		var type =
			NormalizeCode(error.Type);

		/*
         * 优先使用 Code。
         *
         * 如果 Validator 尚未设置 Code，
         * 则兼容使用 Type。
         */

		if (!string.IsNullOrWhiteSpace(code))
		{
			if (RepairByCode(
					plan,
					context,
					originalQuestion,
					error,
					code,
					actions))
			{
				return true;
			}
		}

		if (!string.IsNullOrWhiteSpace(type))
		{
			return RepairByType(
				plan,
				context,
				originalQuestion,
				error,
				type,
				actions);
		}

		return false;
	}


	/// <summary>
	/// 根据 Validation Code 修复。
	/// </summary>
	private static bool RepairByCode(
		QueryPlan plan,
		QueryPlanValidationContext context,
		string? originalQuestion,
		SemanticValidationError error,
		string code,
		List<string> actions)
	{
		switch (code)
		{
			case "METRICFIELDNOTFOUND":
			case "METRICFIELDINVALID":
			case "INVALIDMETRICFIELD":

				return RepairMetricField(
					plan,
					context,
					originalQuestion,
					error,
					actions);


			case "FILTERFIELDNOTFOUND":
			case "FILTERFIELDINVALID":
			case "INVALIDFILTERFIELD":

				return RepairFilterField(
					plan,
					context,
					originalQuestion,
					error,
					actions);


			case "DIMENSIONFIELDNOTFOUND":
			case "DIMENSIONFIELDINVALID":
			case "INVALIDDIMENSIONFIELD":

				return RepairDimensionField(
					plan,
					context,
					originalQuestion,
					error,
					actions);


			case "INVALIDMETRICAGGREGATION":
			case "METRICAGGREGATIONINVALID":

				return RepairMetricAggregation(
					plan,
					context,
					error,
					actions);


			case "INVALIDFIELDAGGREGATION":
			case "FIELDAGGREGATIONINVALID":

				return RepairFieldAggregation(
					plan,
					context,
					error,
					actions);


			case "INVALIDDATEFILTER":
			case "DATEFILTERINVALID":

				return RepairDateFilter(
					plan,
					context,
					originalQuestion,
					error,
					actions);


			default:

				return false;
		}
	}


	/// <summary>
	/// 根据旧版 Validation Type 修复。
	/// </summary>
	private static bool RepairByType(
		QueryPlan plan,
		QueryPlanValidationContext context,
		string? originalQuestion,
		SemanticValidationError error,
		string type,
		List<string> actions)
	{
		if (type.Contains(
				"METRIC",
				StringComparison.OrdinalIgnoreCase)
			&&
			type.Contains(
				"FIELD",
				StringComparison.OrdinalIgnoreCase))
		{
			return RepairMetricField(
				plan,
				context,
				originalQuestion,
				error,
				actions);
		}

		if (type.Contains(
				"FILTER",
				StringComparison.OrdinalIgnoreCase)
			&&
			type.Contains(
				"FIELD",
				StringComparison.OrdinalIgnoreCase))
		{
			return RepairFilterField(
				plan,
				context,
				originalQuestion,
				error,
				actions);
		}

		if (type.Contains(
				"DIMENSION",
				StringComparison.OrdinalIgnoreCase)
			&&
			type.Contains(
				"FIELD",
				StringComparison.OrdinalIgnoreCase))
		{
			return RepairDimensionField(
				plan,
				context,
				originalQuestion,
				error,
				actions);
		}

		if (type.Contains(
				"AGGREGATION",
				StringComparison.OrdinalIgnoreCase)
			&&
			type.Contains(
				"METRIC",
				StringComparison.OrdinalIgnoreCase))
		{
			return RepairMetricAggregation(
				plan,
				context,
				error,
				actions);
		}

		if (type.Contains(
				"AGGREGATION",
				StringComparison.OrdinalIgnoreCase))
		{
			return RepairFieldAggregation(
				plan,
				context,
				error,
				actions);
		}

		if (type.Contains(
				"DATE",
				StringComparison.OrdinalIgnoreCase)
			&&
			type.Contains(
				"FILTER",
				StringComparison.OrdinalIgnoreCase))
		{
			return RepairDateFilter(
				plan,
				context,
				originalQuestion,
				error,
				actions);
		}

		return false;
	}


	/// <summary>
	/// 修复 Metric 字段。
	/// </summary>
	private static bool RepairMetricField(
		QueryPlan plan,
		QueryPlanValidationContext context,
		string? originalQuestion,
		SemanticValidationError error,
		List<string> actions)
	{
		if (string.IsNullOrWhiteSpace(error.Field))
		{
			return false;
		}

		var metrics =
			plan.Metrics
				.Where(
					x => string.Equals(
						x.Field,
						error.Field,
						StringComparison.OrdinalIgnoreCase))
				.ToList();

		if (metrics.Count == 0)
		{
			return false;
		}

		foreach (var metric in metrics)
		{
			var candidate =
				FindBestCandidate(
					error.Field,
					originalQuestion,
					plan,
					context,
					column =>
						IsMetricCandidate(
							column,
							metric.SemanticType));

			if (candidate == null)
			{
				continue;
			}

			if (string.Equals(
					metric.Field,
					candidate.Column.ColumnName,
					StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var oldField =
				metric.Field;

			metric.Field =
				candidate.Column.ColumnName!;

			actions.Add(
				$"Metric '{metric.Name}' 字段由 '{oldField}' 修复为 '{candidate.Column.ColumnName}'。" +
				$" CandidateScore={candidate.TotalScore:F3}。");

			return true;
		}

		return false;
	}


	/// <summary>
	/// 修复 Filter 字段。
	/// </summary>
	private static bool RepairFilterField(
		QueryPlan plan,
		QueryPlanValidationContext context,
		string? originalQuestion,
		SemanticValidationError error,
		List<string> actions)
	{
		if (string.IsNullOrWhiteSpace(error.Field))
		{
			return false;
		}

		var filters =
			plan.Filters
				.Where(
					x => string.Equals(
						x.Field,
						error.Field,
						StringComparison.OrdinalIgnoreCase))
				.ToList();

		if (filters.Count == 0)
		{
			return false;
		}

		var candidate =
			FindBestCandidate(
				error.Field,
				originalQuestion,
				plan,
				context,
				IsFilterCandidate);

		if (candidate == null)
		{
			return false;
		}

		var repaired =
			false;

		foreach (var filter in filters)
		{
			if (string.Equals(
					filter.Field,
					candidate.Column.ColumnName,
					StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var oldField =
				filter.Field;

			filter.Field =
				candidate.Column.ColumnName!;

			actions.Add(
				$"Filter 字段由 '{oldField}' 修复为 '{candidate.Column.ColumnName}'。" +
				$" CandidateScore={candidate.TotalScore:F3}。");

			repaired = true;
		}

		return repaired;
	}


	/// <summary>
	/// 修复 Dimension 字段。
	/// </summary>
	private static bool RepairDimensionField(
		QueryPlan plan,
		QueryPlanValidationContext context,
		string? originalQuestion,
		SemanticValidationError error,
		List<string> actions)
	{
		if (string.IsNullOrWhiteSpace(error.Field))
		{
			return false;
		}

		var dimensions =
			plan.Dimensions
				.Where(
					x => string.Equals(
						x.ColumnName,
						error.Field,
						StringComparison.OrdinalIgnoreCase))
				.ToList();

		if (dimensions.Count == 0)
		{
			return false;
		}

		foreach (var dimension in dimensions)
		{
			var candidate =
				FindBestCandidate(
					error.Field,
					originalQuestion,
					plan,
					context,
					column =>
						IsDimensionCandidate(
							column));

			if (candidate == null)
			{
				continue;
			}

			if (string.Equals(
					dimension.ColumnName,
					candidate.Column.ColumnName,
					StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var oldField =
				dimension.ColumnName;

			dimension.ColumnName =
				candidate.Column.ColumnName!;

			dimension.MetadataColumnId =
				candidate.Column.Id;

			actions.Add(
				$"Dimension 字段由 '{oldField}' 修复为 '{candidate.Column.ColumnName}'。" +
				$" CandidateScore={candidate.TotalScore:F3}。");

			return true;
		}

		return false;
	}


	/// <summary>
	/// 修复 Metric 聚合。
	///
	/// 这里不根据行业业务词判断。
	///
	/// 优先依据 Metadata DataType。
	/// </summary>
	private static bool RepairMetricAggregation(
		QueryPlan plan,
		QueryPlanValidationContext context,
		SemanticValidationError error,
		List<string> actions)
	{
		if (string.IsNullOrWhiteSpace(error.Field))
		{
			return false;
		}

		var metric =
			plan.Metrics.FirstOrDefault(
				x => string.Equals(
					x.Field,
					error.Field,
					StringComparison.OrdinalIgnoreCase));

		if (metric == null)
		{
			return false;
		}

		var column =
			FindMetadataColumn(
				error,
				context,
				metric.Field);

		if (column == null)
		{
			return false;
		}

		var oldAggregation =
			metric.Aggregation;

		var newAggregation =
			ResolveSafeAggregation(
				column,
				metric.SemanticType);

		if (string.Equals(
				oldAggregation,
				newAggregation,
				StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		metric.Aggregation =
			newAggregation;

		actions.Add(
			$"Metric '{metric.Name}' 聚合方式由 '{oldAggregation}' 修复为 '{newAggregation}'。");

		return true;
	}


	/// <summary>
	/// 修复普通 QueryField 聚合。
	/// </summary>
	private static bool RepairFieldAggregation(
		QueryPlan plan,
		QueryPlanValidationContext context,
		SemanticValidationError error,
		List<string> actions)
	{
		if (string.IsNullOrWhiteSpace(error.Field))
		{
			return false;
		}

		var field =
			plan.Fields.FirstOrDefault(
				x => string.Equals(
					x.ColumnName,
					error.Field,
					StringComparison.OrdinalIgnoreCase));

		if (field == null)
		{
			return false;
		}

		var column =
			FindMetadataColumn(
				error,
				context,
				field.ColumnName);

		if (column == null)
		{
			return false;
		}

		var oldAggregation =
			field.Aggregation;

		var newAggregation =
			ResolveSafeAggregation(
				column,
				null);

		if (string.Equals(
				oldAggregation,
				newAggregation,
				StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		field.Aggregation =
			newAggregation;

		actions.Add(
			$"字段 '{field.ColumnName}' 聚合方式由 '{oldAggregation}' 修复为 '{newAggregation}'。");

		return true;
	}


	/// <summary>
	/// 修复日期 Filter。
	///
	/// 注意：
	///
	/// 不使用“日期”“时间”等业务词。
	///
	/// 只根据 Metadata DataType 判断。
	/// </summary>
	private static bool RepairDateFilter(
		QueryPlan plan,
		QueryPlanValidationContext context,
		string? originalQuestion,
		SemanticValidationError error,
		List<string> actions)
	{
		if (string.IsNullOrWhiteSpace(error.Field))
		{
			return false;
		}

		var filters =
			plan.Filters
				.Where(
					x => string.Equals(
						x.Field,
						error.Field,
						StringComparison.OrdinalIgnoreCase))
				.ToList();

		if (filters.Count == 0)
		{
			return false;
		}

		var candidate =
			FindBestCandidate(
				error.Field,
				originalQuestion,
				plan,
				context,
				IsDateCandidate);

		if (candidate == null)
		{
			return false;
		}

		var repaired =
			false;

		foreach (var filter in filters)
		{
			if (string.Equals(
					filter.Field,
					candidate.Column.ColumnName,
					StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var oldField =
				filter.Field;

			filter.Field =
				candidate.Column.ColumnName!;

			actions.Add(
				$"日期 Filter 字段由 '{oldField}' 修复为 '{candidate.Column.ColumnName}'。" +
				$" CandidateScore={candidate.TotalScore:F3}。");

			repaired = true;
		}

		return repaired;
	}


	/// <summary>
	/// 在当前 ValidationContext 中寻找 MetadataColumn。
	/// </summary>
	private static MetadataColumn? FindMetadataColumn(
		SemanticValidationError error,
		QueryPlanValidationContext context,
		string? field)
	{
		if (error.MetadataColumnId.HasValue
			&&
			context.Columns.TryGetValue(
				error.MetadataColumnId.Value,
				out var byId))
		{
			return byId;
		}

		if (error.MetadataTableId.HasValue
			&&
			!string.IsNullOrWhiteSpace(field)
			&&
			context.TableColumns.TryGetValue(
				error.MetadataTableId.Value,
				out var tableColumns))
		{
			var tableColumn =
				tableColumns.FirstOrDefault(
					x => string.Equals(
						x.ColumnName,
						field,
						StringComparison.OrdinalIgnoreCase));

			if (tableColumn != null)
			{
				return tableColumn;
			}
		}

		if (!string.IsNullOrWhiteSpace(field))
		{
			return context.Columns.Values
				.FirstOrDefault(
					x => string.Equals(
						x.ColumnName,
						field,
						StringComparison.OrdinalIgnoreCase));
		}

		return null;
	}


	// ============================================================
	// Candidate Ranking
	// ============================================================

	/// <summary>
	/// Candidate 评分结果。
	/// </summary>
	private sealed class CandidateScore
	{
		public MetadataColumn Column { get; init; } = null!;

		public double NameScore { get; init; }

		public double CommentScore { get; init; }

		public double BusinessMeaningScore { get; init; }

		public double KeywordScore { get; init; }

		public double SynonymScore { get; init; }

		public double ExampleQuestionScore { get; init; }

		public double SearchTextScore { get; init; }

		public double QuestionScore { get; init; }

		public double ContextScore { get; init; }

		public double TotalScore { get; init; }
	}


	/// <summary>
	/// 查找最佳 Metadata Candidate。
	///
	/// 排序依据：
	///
	/// 1. Field Name
	/// 2. Comment
	/// 3. BusinessMeaning
	/// 4. Keywords
	/// 5. Synonyms
	/// 6. ExampleQuestions
	/// 7. SearchText
	/// 8. OriginalQuestion
	/// 9. QueryPlan Context
	///
	/// 最终执行 Confidence Gate。
	/// </summary>
	private static CandidateScore? FindBestCandidate(
		string field,
		string? originalQuestion,
		QueryPlan plan,
		QueryPlanValidationContext context,
		Func<MetadataColumn, bool> predicate)
	{
		var candidates =
			context.Columns.Values
				.Where(predicate)
				.Select(
					column =>
						ScoreCandidate(
							field,
							originalQuestion,
							plan,
							column))
				.OrderByDescending(
					x => x.TotalScore)
				.ThenBy(
					x => x.Column.Id)
				.ToList();

		if (candidates.Count == 0)
		{
			return null;
		}

		var best =
			candidates[0];

		var second =
			candidates.Count > 1
				? candidates[1]
				: null;

		/*
         * Confidence Gate #1
         *
         * 最佳候选本身必须达到最低置信度。
         */
		if (best.TotalScore < MinimumConfidence)
		{
			return null;
		}

		/*
         * Confidence Gate #2
         *
         * 如果存在第二候选，
         * 最佳候选必须明显领先。
         */
		if (second != null
			&&
			best.TotalScore - second.TotalScore
				< MinimumScoreGap)
		{
			return null;
		}

		return best;
	}


	/// <summary>
	/// 对 Metadata Candidate 进行多维评分。
	/// </summary>
	private static CandidateScore ScoreCandidate(
		string field,
		string? originalQuestion,
		QueryPlan plan,
		MetadataColumn column)
	{
		var candidateSemanticText =
			BuildCandidateSemanticText(
				column);

		var repairContext =
			BuildRepairContext(
				field,
				originalQuestion,
				plan);

		var nameScore =
			CalculateTextSimilarity(
				field,
				column.ColumnName);

		var commentScore =
			CalculateTextSimilarity(
				repairContext,
				column.ColumnComment);

		var businessMeaningScore =
			CalculateTextSimilarity(
				repairContext,
				column.Semantic?.BusinessMeaning);

		var keywordScore =
			CalculateTextSimilarity(
				repairContext,
				column.Semantic?.Keywords);

		var synonymScore =
			CalculateTextSimilarity(
				repairContext,
				column.Semantic?.Synonyms);

		var exampleQuestionScore =
			CalculateTextSimilarity(
				originalQuestion,
				column.Semantic?.ExampleQuestions);

		var searchTextScore =
			CalculateTextSimilarity(
				repairContext,
				column.SearchText);

		var questionScore =
			CalculateTextSimilarity(
				originalQuestion,
				candidateSemanticText);

		var contextScore =
			CalculateTextSimilarity(
				repairContext,
				candidateSemanticText);

		/*
         * 通用算法权重。
         *
         * 不包含任何业务领域知识。
         */
		var total =
			nameScore * 0.10
			+
			commentScore * 0.10
			+
			businessMeaningScore * 0.20
			+
			keywordScore * 0.15
			+
			synonymScore * 0.15
			+
			exampleQuestionScore * 0.10
			+
			searchTextScore * 0.05
			+
			questionScore * 0.10
			+
			contextScore * 0.05;

		return new CandidateScore
		{
			Column = column,

			NameScore = nameScore,

			CommentScore = commentScore,

			BusinessMeaningScore =
				businessMeaningScore,

			KeywordScore =
				keywordScore,

			SynonymScore =
				synonymScore,

			ExampleQuestionScore =
				exampleQuestionScore,

			SearchTextScore =
				searchTextScore,

			QuestionScore =
				questionScore,

			ContextScore =
				contextScore,

			TotalScore =
				total
		};
	}


	/// <summary>
	/// 构造 Repair Semantic Context。
	///
	/// 不添加任何业务词。
	/// </summary>
	private static string BuildRepairContext(
		string? field,
		string? originalQuestion,
		QueryPlan plan)
	{
		var parts =
			new List<string>();

		AddText(parts, field);

		AddText(
			parts,
			originalQuestion);

		if (plan.Intent != null)
		{
			AddText(
				parts,
				plan.Intent.OriginalQuestion);

			AddText(
				parts,
				plan.Intent.IntentType);
		}

		foreach (var metric in plan.Metrics)
		{
			AddText(
				parts,
				metric.Name);

			AddText(
				parts,
				metric.Field);

			AddText(
				parts,
				metric.SemanticType);
		}

		foreach (var dimension in plan.Dimensions)
		{
			AddText(
				parts,
				dimension.ColumnName);

			AddText(
				parts,
				dimension.SemanticType);
		}

		foreach (var queryField in plan.Fields)
		{
			AddText(
				parts,
				queryField.ColumnName);

			AddText(
				parts,
				queryField.DataType);
		}

		foreach (var filter in plan.Filters)
		{
			AddText(
				parts,
				filter.Field);

			AddText(
				parts,
				filter.Operator);

			AddText(
				parts,
				filter.Value);
		}

		return string.Join(
			" ",
			parts);
	}


	/// <summary>
	/// 构造 Metadata Semantic Document。
	/// </summary>
	private static string BuildCandidateSemanticText(
		MetadataColumn column)
	{
		var parts =
			new List<string>();

		AddText(
			parts,
			column.ColumnName);

		AddText(
			parts,
			column.ColumnComment);

		AddText(
			parts,
			column.SearchText);

		if (column.Semantic != null)
		{
			AddText(
				parts,
				column.Semantic.BusinessMeaning);

			AddText(
				parts,
				column.Semantic.Keywords);

			AddText(
				parts,
				column.Semantic.Synonyms);

			AddText(
				parts,
				column.Semantic.ExampleQuestions);
		}

		return string.Join(
			" ",
			parts);
	}


	/// <summary>
	/// 通用文本相似度。
	///
	/// 不使用行业词典。
	/// </summary>
	private static double CalculateTextSimilarity(
		string? source,
		string? target)
	{
		if (string.IsNullOrWhiteSpace(source)
			||
			string.IsNullOrWhiteSpace(target))
		{
			return 0;
		}

		var normalizedSource =
			Normalize(source);

		var normalizedTarget =
			Normalize(target);

		if (normalizedSource.Length == 0
			||
			normalizedTarget.Length == 0)
		{
			return 0;
		}

		if (string.Equals(
				normalizedSource,
				normalizedTarget,
				StringComparison.OrdinalIgnoreCase))
		{
			return 1.0;
		}

		if (normalizedTarget.Contains(
				normalizedSource,
				StringComparison.OrdinalIgnoreCase)
			||
			normalizedSource.Contains(
				normalizedTarget,
				StringComparison.OrdinalIgnoreCase))
		{
			return 0.85;
		}

		var sourceTokens =
			Tokenize(source);

		var targetTokens =
			Tokenize(target);

		if (sourceTokens.Count == 0
			||
			targetTokens.Count == 0)
		{
			return 0;
		}

		var intersection =
			sourceTokens
				.Intersect(
					targetTokens,
					StringComparer.OrdinalIgnoreCase)
				.Count();

		if (intersection == 0)
		{
			return 0;
		}

		var union =
			sourceTokens
				.Union(
					targetTokens,
					StringComparer.OrdinalIgnoreCase)
				.Count();

		if (union == 0)
		{
			return 0;
		}

		return
			(double)intersection /
			union;
	}


	// ============================================================
	// Candidate Compatibility
	// ============================================================

	/// <summary>
	/// Metric Candidate。
	///
	/// 不使用行业业务词。
	///
	/// 只使用：
	///
	/// - DataType
	/// - QueryPlan SemanticType
	/// </summary>
	private static bool IsMetricCandidate(
		MetadataColumn column,
		string? semanticType)
	{
		if (string.IsNullOrWhiteSpace(
				column.ColumnName))
		{
			return false;
		}

		/*
         * 日期类型通常不作为普通 Metric Candidate。
         *
         * 如果 QueryPlan 明确表达为 Date，
         * 则允许。
         */
		if (IsDateColumn(column))
		{
			return string.Equals(
				semanticType,
				"DATE",
				StringComparison.OrdinalIgnoreCase)
				||
				string.Equals(
					semanticType,
					"TIME",
					StringComparison.OrdinalIgnoreCase);
		}

		return true;
	}


	/// <summary>
	/// Filter Candidate。
	///
	/// Filter 可以作用于任意 MetadataColumn。
	/// </summary>
	private static bool IsFilterCandidate(
		MetadataColumn column)
	{
		return !string.IsNullOrWhiteSpace(
			column.ColumnName);
	}


	/// <summary>
	/// Dimension Candidate。
	///
	/// 不根据行业词判断。
	///
	/// 只排除明显不适合作为 Dimension 的日期/数值约束，
	/// 其余交给 Semantic Ranking。
	/// </summary>
	private static bool IsDimensionCandidate(
		MetadataColumn column)
	{
		return !string.IsNullOrWhiteSpace(
			column.ColumnName);
	}


	/// <summary>
	/// Date Candidate。
	///
	/// 唯一判断依据：
	///
	/// Metadata DataType。
	/// </summary>
	private static bool IsDateCandidate(
		MetadataColumn column)
	{
		return IsDateColumn(
			column);
	}


	// ============================================================
	// Aggregation
	// ============================================================

	/// <summary>
	/// 根据 Metadata DataType 和已有 QueryPlan SemanticType
	/// 选择相对安全的聚合方式。
	///
	/// 不使用行业业务词。
	/// </summary>
	private static string ResolveSafeAggregation(
		MetadataColumn column,
		string? semanticType)
	{
		/*
         * 如果 QueryPlan 已经具有系统标准 SemanticType，
         * 优先使用它。
         *
         * 这些是现有 QueryMetric 模型的标准语义类型，
         * 不是行业业务词典。
         */

		if (string.Equals(
				semanticType,
				"Count",
				StringComparison.OrdinalIgnoreCase))
		{
			return "COUNT";
		}

		if (string.Equals(
				semanticType,
				"Ratio",
				StringComparison.OrdinalIgnoreCase))
		{
			return "AVG";
		}

		/*
         * 数值字段：
         *
         * 默认使用 SUM。
         *
         * 这是 DataType 层面的通用行为，
         * 不是业务领域规则。
         */
		if (LooksNumeric(column))
		{
			return "SUM";
		}

		/*
         * 非数值字段：
         *
         * 默认 COUNT。
         */
		return "COUNT";
	}


	// ============================================================
	// Metadata Helpers
	// ============================================================

	private static bool LooksNumeric(
		MetadataColumn column)
	{
		var type =
			column.DataType?
				.Trim()
				.ToLowerInvariant()
			?? string.Empty;

		return type.Contains("int")
			||
			type.Contains("decimal")
			||
			type.Contains("numeric")
			||
			type.Contains("float")
			||
			type.Contains("double")
			||
			type.Contains("money");
	}


	private static bool IsDateColumn(
		MetadataColumn column)
	{
		var type =
			column.DataType?
				.Trim()
				.ToLowerInvariant()
			?? string.Empty;

		return type.Contains("date")
			||
			type.Contains("time");
	}


	// ============================================================
	// Text Helpers
	// ============================================================

	private static string Normalize(
		string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}

		return value
			.Trim()
			.ToLowerInvariant()
			.Replace("_", string.Empty)
			.Replace("-", string.Empty)
			.Replace(" ", string.Empty);
	}


	/// <summary>
	/// 通用 Tokenizer。
	///
	/// 英文/数字：
	///     连续字符作为 Token。
	///
	/// 中文：
	///     使用 1/2/3-gram。
	///
	/// 不包含任何业务词典。
	/// </summary>
	private static HashSet<string> Tokenize(
		string? value)
	{
		var result =
			new HashSet<string>(
				StringComparer.OrdinalIgnoreCase);

		if (string.IsNullOrWhiteSpace(value))
		{
			return result;
		}

		var normalized =
			Normalize(value);

		if (normalized.Length == 0)
		{
			return result;
		}

		var buffer =
			new System.Text.StringBuilder();

		foreach (var ch in normalized)
		{
			if (char.IsLetterOrDigit(ch)
				&&
				!IsChinese(ch))
			{
				buffer.Append(ch);

				continue;
			}

			if (buffer.Length > 0)
			{
				result.Add(
					buffer.ToString());

				buffer.Clear();
			}

			if (IsChinese(ch))
			{
				result.Add(
					ch.ToString());
			}
		}

		if (buffer.Length > 0)
		{
			result.Add(
				buffer.ToString());
		}

		/*
         * 中文 n-gram。
         */
		for (var i = 0;
			 i < normalized.Length;
			 i++)
		{
			if (!IsChinese(
					normalized[i]))
			{
				continue;
			}

			if (i + 1 < normalized.Length
				&&
				IsChinese(
					normalized[i + 1]))
			{
				result.Add(
					normalized.Substring(
						i,
						2));
			}

			if (i + 2 < normalized.Length
				&&
				IsChinese(
					normalized[i + 1])
				&&
				IsChinese(
					normalized[i + 2]))
			{
				result.Add(
					normalized.Substring(
						i,
						3));
			}
		}

		return result;
	}


	private static bool IsChinese(
		char value)
	{
		return value >= '\u4E00'
			&&
			value <= '\u9FFF';
	}


	private static void AddText(
		List<string> parts,
		string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			parts.Add(
				value.Trim());
		}
	}


	// ============================================================
	// Validation Helpers
	// ============================================================

	private static string NormalizeCode(
		string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}

		return new string(
			value
				.Where(
					char.IsLetterOrDigit)
				.ToArray())
			.ToUpperInvariant();
	}


	private static QueryPlanRepairResult Failed(
		string reason)
	{
		return new QueryPlanRepairResult
		{
			Success = false,

			RepairedPlan = null,

			FailureReason = reason,

			Explanation = reason
		};
	}
}