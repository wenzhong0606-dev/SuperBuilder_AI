using SuperBulider_AI.Interfaces.BI;
using SuperBulider_AI.Models.BI;
using SuperBulider_AI.Models.Metadata;

namespace SuperBulider_AI.Services.BI;

/// <summary>
/// QueryPlan 自动修复服务。
///
/// Phase 2.3.1
///
/// 负责：
///
/// QueryPlan
///      ↓
/// Semantic Validation
///      ↓
/// Metadata Semantic Context
///      ↓
/// Repair QueryPlan
///      ↓
/// Re-Validation
///
/// 本服务不负责：
///
/// 1. SQL 生成
/// 2. SQL 执行
/// 3. MetadataSemanticSearch
/// 4. QueryPlan 重新构建
///
/// Repair 的目标是直接修改当前 QueryPlan。
/// </summary>
public class QueryPlanRepairService :
	IQueryPlanRepairService
{
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
					"当前 QueryPlan 没有找到可以基于 Metadata Context 自动修复的问题。"));
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
         * Code 是 Validation → Repair Loop
         * 的稳定机器可读标识。
         *
         * 如果旧 Validator 尚未设置 Code，
         * 则退回使用 Type。
         */

		if (!string.IsNullOrWhiteSpace(code))
		{
			if (RepairByCode(
					plan,
					context,
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
					error,
					actions);


			case "FILTERFIELDNOTFOUND":
			case "FILTERFIELDINVALID":
			case "INVALIDFILTERFIELD":

				return RepairFilterField(
					plan,
					context,
					error,
					actions);


			case "DIMENSIONFIELDNOTFOUND":
			case "DIMENSIONFIELDINVALID":
			case "INVALIDDIMENSIONFIELD":

				return RepairDimensionField(
					plan,
					context,
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
					error,
					actions);


			default:

				return false;
		}
	}


	/// <summary>
	/// 根据旧版 Validation Type 修复。
	///
	/// 用于兼容现有 Validator。
	/// </summary>
	private static bool RepairByType(
		QueryPlan plan,
		QueryPlanValidationContext context,
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
		SemanticValidationError error,
		List<string> actions)
	{
		if (string.IsNullOrWhiteSpace(
				error.Field))
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
					candidate.ColumnName,
					StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var oldField =
				metric.Field;

			metric.Field =
				candidate.ColumnName!;

			actions.Add(
				$"Metric '{metric.Name}' 字段由 '{oldField}' 修复为 '{candidate.ColumnName}'。");

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
		SemanticValidationError error,
		List<string> actions)
	{
		if (string.IsNullOrWhiteSpace(
				error.Field))
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
					candidate.ColumnName,
					StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var oldField =
				filter.Field;

			filter.Field =
				candidate.ColumnName!;

			actions.Add(
				$"Filter 字段由 '{oldField}' 修复为 '{candidate.ColumnName}'。");

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
		SemanticValidationError error,
		List<string> actions)
	{
		if (string.IsNullOrWhiteSpace(
				error.Field))
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
					context,
					column =>
						IsDimensionCandidate(
							column,
							dimension.SemanticType));

			if (candidate == null)
			{
				continue;
			}

			if (string.Equals(
					dimension.ColumnName,
					candidate.ColumnName,
					StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var oldField =
				dimension.ColumnName;

			dimension.ColumnName =
				candidate.ColumnName!;

			dimension.MetadataColumnId =
				candidate.Id;

			if (string.IsNullOrWhiteSpace(
					dimension.SemanticType))
			{
				dimension.SemanticType =
					ResolveSemanticType(
						candidate);
			}

			actions.Add(
				$"Dimension 字段由 '{oldField}' 修复为 '{candidate.ColumnName}'。");

			return true;
		}

		return false;
	}


	/// <summary>
	/// 修复 Metric 聚合方式。
	/// </summary>
	private static bool RepairMetricAggregation(
		QueryPlan plan,
		QueryPlanValidationContext context,
		SemanticValidationError error,
		List<string> actions)
	{
		if (string.IsNullOrWhiteSpace(
				error.Field))
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
	/// 修复普通 QueryField 聚合方式。
	/// </summary>
	private static bool RepairFieldAggregation(
		QueryPlan plan,
		QueryPlanValidationContext context,
		SemanticValidationError error,
		List<string> actions)
	{
		if (string.IsNullOrWhiteSpace(
				error.Field))
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
	/// </summary>
	private static bool RepairDateFilter(
		QueryPlan plan,
		QueryPlanValidationContext context,
		SemanticValidationError error,
		List<string> actions)
	{
		if (string.IsNullOrWhiteSpace(
				error.Field))
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
					candidate.ColumnName,
					StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var oldField =
				filter.Field;

			filter.Field =
				candidate.ColumnName!;

			actions.Add(
				$"日期 Filter 字段由 '{oldField}' 修复为 '{candidate.ColumnName}'。");

			repaired = true;
		}

		return repaired;
	}


	/// <summary>
	/// 在当前 ValidationContext 中寻找 MetadataColumn。
	///
	/// 优先：
	///
	/// MetadataColumnId
	/// ↓
	/// TableColumns
	/// ↓
	/// ColumnName
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


	/// <summary>
	/// 基于 Metadata Semantic 找到最佳候选字段。
	/// </summary>
	private static MetadataColumn? FindBestCandidate(
		string field,
		QueryPlanValidationContext context,
		Func<MetadataColumn, bool> predicate)
	{
		var candidates =
			context.Columns.Values
				.Where(predicate)
				.ToList();

		if (candidates.Count == 0)
		{
			return null;
		}

		var normalizedField =
			Normalize(field);

		return candidates
			.Select(
				column =>
					new
					{
						Column = column,

						Score =
							CalculateSemanticScore(
								normalizedField,
								column)
					})
			.Where(
				x => x.Score > 0)
			.OrderByDescending(
				x => x.Score)
			.ThenBy(
				x => x.Column.Id)
			.Select(
				x => x.Column)
			.FirstOrDefault();
	}


	/// <summary>
	/// Metadata Semantic 综合评分。
	/// </summary>
	private static int CalculateSemanticScore(
		string field,
		MetadataColumn column)
	{
		var score = 0;

		score +=
			ScoreText(
				field,
				column.ColumnName,
				100);

		score +=
			ScoreText(
				field,
				column.ColumnComment,
				60);

		score +=
			ScoreSemantic(
				field,
				column.Semantic,
				80);

		score +=
			ScoreText(
				field,
				column.SearchText,
				40);

		return score;
	}


	/// <summary>
	/// MetadataSemantic 评分。
	/// </summary>
	private static int ScoreSemantic(
		string field,
		MetadataSemantic? semantic,
		int weight)
	{
		if (semantic == null)
		{
			return 0;
		}

		var score = 0;

		score +=
			ScoreText(
				field,
				semantic.BusinessMeaning,
				weight);

		score +=
			ScoreText(
				field,
				semantic.Keywords,
				weight / 2);

		score +=
			ScoreText(
				field,
				semantic.Synonyms,
				weight);

		score +=
			ScoreText(
				field,
				semantic.ExampleQuestions,
				weight / 2);

		return score;
	}


	/// <summary>
	/// 文本匹配评分。
	/// </summary>
	private static int ScoreText(
		string source,
		string? target,
		int weight)
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

		if (normalizedTarget.Contains(
				normalizedSource,
				StringComparison.OrdinalIgnoreCase))
		{
			return weight;
		}

		if (normalizedSource.Contains(
				normalizedTarget,
				StringComparison.OrdinalIgnoreCase))
		{
			return weight / 2;
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

		var matched =
			sourceTokens.Count(
				targetTokens.Contains);

		if (matched == 0)
		{
			return 0;
		}

		return
			weight *
			matched /
			sourceTokens.Count;
	}


	/// <summary>
	/// Metric 候选字段判断。
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

		if (IsDateColumn(column)
			&&
			!string.Equals(
				semanticType,
				"DATE",
				StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (string.Equals(
				semanticType,
				"Quantity",
				StringComparison.OrdinalIgnoreCase)
			||
			string.Equals(
				semanticType,
				"Amount",
				StringComparison.OrdinalIgnoreCase))
		{
			return LooksNumeric(column);
		}

		return true;
	}


	/// <summary>
	/// Filter 候选字段。
	/// </summary>
	private static bool IsFilterCandidate(
		MetadataColumn column)
	{
		return !string.IsNullOrWhiteSpace(
			column.ColumnName);
	}


	/// <summary>
	/// Dimension 候选字段。
	/// </summary>
	private static bool IsDimensionCandidate(
		MetadataColumn column,
		string? semanticType)
	{
		if (string.IsNullOrWhiteSpace(
				column.ColumnName))
		{
			return false;
		}

		if (IsNumeric(column)
			&&
			!string.Equals(
				semanticType,
				"Quantity",
				StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		return true;
	}


	/// <summary>
	/// Date 候选字段。
	/// </summary>
	private static bool IsDateCandidate(
		MetadataColumn column)
	{
		if (IsDateColumn(column))
		{
			return true;
		}

		var semanticText =
			$"{column.Semantic?.BusinessMeaning} " +
			$"{column.Semantic?.Keywords} " +
			$"{column.Semantic?.Synonyms}";

		return ContainsBusinessWord(
			semanticText,
			"日期",
			"时间",
			"年月",
			"date",
			"time");
	}


	/// <summary>
	/// 根据 Metadata 类型选择安全聚合方式。
	/// </summary>
	private static string ResolveSafeAggregation(
		MetadataColumn column,
		string? semanticType)
	{
		if (string.Equals(
				semanticType,
				"Count",
				StringComparison.OrdinalIgnoreCase))
		{
			return "COUNT";
		}

		if (string.Equals(
				semanticType,
				"Amount",
				StringComparison.OrdinalIgnoreCase)
			||
			string.Equals(
				semanticType,
				"Quantity",
				StringComparison.OrdinalIgnoreCase))
		{
			return "SUM";
		}

		if (LooksNumeric(column))
		{
			return "SUM";
		}

		return "COUNT";
	}


	/// <summary>
	/// 推导 Dimension SemanticType。
	/// </summary>
	private static string ResolveSemanticType(
		MetadataColumn column)
	{
		var text =
			$"{column.Semantic?.BusinessMeaning} " +
			$"{column.Semantic?.Keywords} " +
			$"{column.Semantic?.Synonyms}";

		if (ContainsBusinessWord(
				text,
				"日期",
				"时间",
				"年月",
				"date",
				"time"))
		{
			return "Time";
		}

		if (ContainsBusinessWord(
				text,
				"分类",
				"类别",
				"category"))
		{
			return "Category";
		}

		if (ContainsBusinessWord(
				text,
				"客户",
				"供应商",
				"物料",
				"entity"))
		{
			return "Entity";
		}

		return "Dimension";
	}


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


	private static bool IsNumeric(
		MetadataColumn column)
	{
		return LooksNumeric(column);
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


	private static bool ContainsBusinessWord(
		string text,
		params string[] words)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}

		return words.Any(
			word =>
				text.Contains(
					word,
					StringComparison.OrdinalIgnoreCase));
	}


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

		foreach (var ch in Normalize(value))
		{
			if (char.IsLetterOrDigit(ch))
			{
				result.Add(
					ch.ToString());
			}
		}

		return result;
	}


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