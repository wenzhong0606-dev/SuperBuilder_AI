using SuperBulider_AI.Interfaces.BI;
using SuperBulider_AI.Models.BI;
using SuperBulider_AI.Models.Metadata;

namespace SuperBulider_AI.Services.BI;

/// <summary>
/// QueryPlan 自动修复服务。
///
/// Phase 2.3.1
///
/// 职责:
///
/// QueryPlan
///      ↓
/// Semantic Validation Error
///      ↓
/// Metadata Semantic Context
///      ↓
/// Repair QueryPlan
///      ↓
/// QueryPlanRepairResult
///
/// 注意:
///
/// 1. 不负责 SQL 生成
/// 2. 不负责 SQL 执行
/// 3. 不负责 Metadata 搜索
/// 4. 只使用 ValidationContext 中已经解析好的 Metadata
///
/// </summary>
public class QueryPlanRepairService :
	IQueryPlanRepairService
{
	/// <summary>
	/// 执行 QueryPlan 自动修复。
	///
	/// Repair 输入:
	///
	/// QueryPlan
	/// Validation Errors
	/// Metadata Context
	///
	/// Repair 输出:
	///
	/// 修复后的 QueryPlan
	/// </summary>
	public Task<QueryPlanRepairResult> RepairAsync(
		QueryPlanRepairRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);

		ArgumentNullException.ThrowIfNull(request.Plan);

		ArgumentNullException.ThrowIfNull(request.Context);

		var plan = request.Plan;

		var actions = new List<string>();

		var repairCount = 0;

		foreach (var error in request.Errors)
		{
			if (error.Severity != SemanticValidationSeverity.Error)
			{
				continue;
			}

			var repaired = RepairError(
				plan,
				request.Context,
				error,
				actions);

			if (repaired)
			{
				repairCount++;
			}
		}

		if (repairCount == 0)
		{
			return Task.FromResult(
				new QueryPlanRepairResult
				{
					Success = false,
					Plan = plan,
					Reason =
						"当前 QueryPlan 没有找到可以基于 Metadata Context 自动修复的错误。",
					RepairCount = 0
				});
		}

		return Task.FromResult(
			new QueryPlanRepairResult
			{
				Success = true,
				Plan = plan,
				Reason =
					$"QueryPlan 已完成 {repairCount} 项自动修复。",
				RepairCount = repairCount
			});
	}


	/// <summary>
	/// 根据语义验证错误执行具体修复。
	/// </summary>
	private static bool RepairError(
		QueryPlan plan,
		QueryPlanValidationContext context,
		SemanticValidationError error,
		List<string> actions)
	{
		switch (error.Code)
		{
			case "MetricFieldNotFound":

				return RepairMetricField(
					plan,
					context,
					error,
					actions);


			case "FilterFieldNotFound":

				return RepairFilterField(
					plan,
					context,
					error,
					actions);


			case "DimensionFieldNotFound":

				return RepairDimensionField(
					plan,
					context,
					error,
					actions);


			case "InvalidMetricAggregation":

				return RepairMetricAggregation(
					plan,
					context,
					error,
					actions);


			case "InvalidFieldAggregation":

				return RepairFieldAggregation(
					plan,
					context,
					error,
					actions);


			case "InvalidDateFilter":

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
	/// 修复 Metric 字段。
	///
	/// 当前 Validator 已经定位到：
	///
	/// Metric.Field
	///
	/// Repair 使用 MetadataColumn 的:
	///
	/// ColumnName
	/// ColumnComment
	/// Semantic
	/// </summary>
	private static bool RepairMetricField(
		QueryPlan plan,
		QueryPlanValidationContext context,
		SemanticValidationError error,
		List<string> actions)
	{
		var metric = plan.Metrics
			.FirstOrDefault(
				x => string.Equals(
					x.Field,
					error.Field,
					StringComparison.OrdinalIgnoreCase));

		if (metric == null)
		{
			return false;
		}

		var candidate =
			FindSemanticColumn(
				error.Field,
				context,
				column =>
					IsMetricCandidate(
						column,
						metric.SemanticType));

		if (candidate == null)
		{
			return false;
		}

		if (string.Equals(
				metric.Field,
				candidate.ColumnName,
				StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		var oldField = metric.Field;

		metric.Field =
			candidate.ColumnName!;

		actions.Add(
			$"Metric字段由 '{oldField}' 修复为 '{candidate.ColumnName}'。");

		return true;
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
		var filters = plan.Filters
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
			FindSemanticColumn(
				error.Field,
				context,
				column =>
					IsFilterCandidate(
						column));

		if (candidate == null)
		{
			return false;
		}

		var repaired = false;

		foreach (var filter in filters)
		{
			if (string.Equals(
					filter.Field,
					candidate.ColumnName,
					StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var oldField = filter.Field;

			filter.Field =
				candidate.ColumnName!;

			actions.Add(
				$"Filter字段由 '{oldField}' 修复为 '{candidate.ColumnName}'。");

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
		var dimensions = plan.Dimensions
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

		var candidate =
			FindSemanticColumn(
				error.Field,
				context,
				column =>
					IsDimensionCandidate(
						column,
						dimensions[0].SemanticType));

		if (candidate == null)
		{
			return false;
		}

		var repaired = false;

		foreach (var dimension in dimensions)
		{
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
					ResolveSemanticType(candidate);
			}

			actions.Add(
				$"Dimension字段由 '{oldField}' 修复为 '{candidate.ColumnName}'。");

			repaired = true;
		}

		return repaired;
	}


	/// <summary>
	/// 修复 Metric 聚合方式。
	///
	/// 根据 MetadataColumn.DataType 和 SemanticType
	/// 选择安全的聚合方式。
	/// </summary>
	private static bool RepairMetricAggregation(
		QueryPlan plan,
		QueryPlanValidationContext context,
		SemanticValidationError error,
		List<string> actions)
	{
		var metric = plan.Metrics
			.FirstOrDefault(
				x => string.Equals(
					x.Field,
					error.Field,
					StringComparison.OrdinalIgnoreCase));

		if (metric == null)
		{
			return false;
		}

		var column =
			FindColumn(
				error,
				context);

		if (column == null)
		{
			return false;
		}

		var oldAggregation =
			metric.Aggregation;

		var repairedAggregation =
			ResolveSafeAggregation(
				column,
				metric.SemanticType);

		if (string.Equals(
				oldAggregation,
				repairedAggregation,
				StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		metric.Aggregation =
			repairedAggregation;

		actions.Add(
			$"Metric '{metric.Name}' 聚合方式由 '{oldAggregation}' 修复为 '{repairedAggregation}'。");

		return true;
	}


	/// <summary>
	/// 修复 QueryField 聚合方式。
	/// </summary>
	private static bool RepairFieldAggregation(
		QueryPlan plan,
		QueryPlanValidationContext context,
		SemanticValidationError error,
		List<string> actions)
	{
		var field = plan.Fields
			.FirstOrDefault(
				x => string.Equals(
					x.ColumnName,
					error.Field,
					StringComparison.OrdinalIgnoreCase));

		if (field == null)
		{
			return false;
		}

		var column =
			FindColumn(
				error,
				context);

		if (column == null)
		{
			return false;
		}

		var oldAggregation =
			field.Aggregation;

		var repairedAggregation =
			ResolveSafeAggregation(
				column,
				null);

		if (string.Equals(
				oldAggregation,
				repairedAggregation,
				StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		field.Aggregation =
			repairedAggregation;

		actions.Add(
			$"字段 '{field.ColumnName}' 聚合方式由 '{oldAggregation}' 修复为 '{repairedAggregation}'。");

		return true;
	}


	/// <summary>
	/// 修复日期 Filter。
	///
	/// 如果当前字段不是日期字段，
	/// 从 Metadata Context 中寻找日期语义字段。
	/// </summary>
	private static bool RepairDateFilter(
		QueryPlan plan,
		QueryPlanValidationContext context,
		SemanticValidationError error,
		List<string> actions)
	{
		var filters = plan.Filters
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
			FindSemanticColumn(
				error.Field,
				context,
				IsDateCandidate);

		if (candidate == null)
		{
			return false;
		}

		var repaired = false;

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
				$"日期Filter字段由 '{oldField}' 修复为 '{candidate.ColumnName}'。");

			repaired = true;
		}

		return repaired;
	}


	/// <summary>
	/// 根据 ValidationError 定位 MetadataColumn。
	/// </summary>
	private static MetadataColumn? FindColumn(
		SemanticValidationError error,
		QueryPlanValidationContext context)
	{
		if (error.MetadataColumnId.HasValue
			&&
			context.Columns.TryGetValue(
				error.MetadataColumnId.Value,
				out var column))
		{
			return column;
		}

		if (string.IsNullOrWhiteSpace(
				error.Field))
		{
			return null;
		}

		return context.Columns
			.Values
			.FirstOrDefault(
				x =>
					string.Equals(
						x.ColumnName,
						error.Field,
						StringComparison.OrdinalIgnoreCase));
	}


	/// <summary>
	/// 基于 Metadata 语义寻找候选字段。
	///
	/// 优先级:
	///
	/// 1. ColumnName
	/// 2. BusinessMeaning
	/// 3. Keywords
	/// 4. Synonyms
	/// 5. ExampleQuestions
	/// 6. ColumnComment
	/// 7. SearchText
	///
	/// 注意:
	///
	/// 这里不调用 MetadataSemanticSearchService。
	/// ValidationContext 已经携带本次查询涉及的 Metadata。
	/// </summary>
	private static MetadataColumn? FindSemanticColumn(
		string field,
		QueryPlanValidationContext context,
		Func<MetadataColumn, bool> predicate)
	{
		var candidates = context.Columns.Values
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
	/// Metadata语义匹配评分。
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
	/// Semantic对象评分。
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

		score +=
			ScoreText(
				field,
				semantic.BusinessDomain,
				weight / 4);

		return score;
	}


	/// <summary>
	/// 文本语义评分。
	///
	/// 这里使用轻量级 Token 匹配，
	/// 避免在 Repair Service 中重新引入
	/// Embedding / Qdrant / Metadata Search。
	/// </summary>
	private static int ScoreText(
		string source,
		string? target,
		int weight)
	{
		if (string.IsNullOrWhiteSpace(
				source)
			||
			string.IsNullOrWhiteSpace(
				target))
		{
			return 0;
		}

		var normalizedTarget =
			Normalize(target);

		if (normalizedTarget.Contains(
				source,
				StringComparison.OrdinalIgnoreCase))
		{
			return weight;
		}

		var sourceTokens =
			Tokenize(source);

		if (sourceTokens.Count == 0)
		{
			return 0;
		}

		var targetTokens =
			Tokenize(target);

		var matches =
			sourceTokens.Count(
				targetTokens.Contains);

		if (matches == 0)
		{
			return 0;
		}

		return
			weight *
			matches /
			sourceTokens.Count;
	}


	/// <summary>
	/// 判断 Metric 候选字段。
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

		if (!string.IsNullOrWhiteSpace(
				semanticType))
		{
			var semantic =
				column.Semantic?.BusinessMeaning
				?? string.Empty;

			var keywords =
				column.Semantic?.Keywords
				?? string.Empty;

			var synonyms =
				column.Semantic?.Synonyms
				?? string.Empty;

			var text =
				$"{semantic} {keywords} {synonyms}";

			if (semanticType.Equals(
					"AMOUNT",
					StringComparison.OrdinalIgnoreCase)
				&&
				!LooksNumeric(column)
				&&
				!ContainsBusinessWord(
					text,
					"金额",
					"销售额",
					"收入",
					"金额"))
			{
				return false;
			}

			if (semanticType.Equals(
					"QUANTITY",
					StringComparison.OrdinalIgnoreCase)
				&&
				!LooksNumeric(column))
			{
				return false;
			}
		}

		return true;
	}


	/// <summary>
	/// 判断 Filter 候选字段。
	/// </summary>
	private static bool IsFilterCandidate(
		MetadataColumn column)
	{
		return !string.IsNullOrWhiteSpace(
			column.ColumnName);
	}


	/// <summary>
	/// 判断 Dimension 候选字段。
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
				"QUANTITY",
				StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		return true;
	}


	/// <summary>
	/// 判断日期候选字段。
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
	/// 解析安全聚合方式。
	/// </summary>
	private static string ResolveSafeAggregation(
		MetadataColumn column,
		string? semanticType)
	{
		if (string.Equals(
				semanticType,
				"COUNT",
				StringComparison.OrdinalIgnoreCase))
		{
			return "COUNT";
		}

		if (LooksNumeric(column))
		{
			return "SUM";
		}

		return "COUNT";
	}


	/// <summary>
	/// 根据 Metadata Semantic 推导 Dimension 类型。
	/// </summary>
	private static string?
		ResolveSemanticType(
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
				"date",
				"time"))
		{
			return "TIME";
		}

		if (ContainsBusinessWord(
				text,
				"分类",
				"类别",
				"category"))
		{
			return "CATEGORY";
		}

		if (ContainsBusinessWord(
				text,
				"实体",
				"客户",
				"供应商",
				"物料",
				"entity"))
		{
			return "ENTITY";
		}

		return "DIMENSION";
	}


	/// <summary>
	/// 判断是否为数值字段。
	/// </summary>
	private static bool IsNumeric(
		MetadataColumn column)
	{
		return LooksNumeric(column);
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


	/// <summary>
	/// 判断日期字段。
	/// </summary>
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


	/// <summary>
	/// 判断业务文本是否包含指定词。
	/// </summary>
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


	/// <summary>
	/// 文本标准化。
	/// </summary>
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
			.Replace(
				"_",
				string.Empty)
			.Replace(
				"-",
				string.Empty)
			.Replace(
				" ",
				string.Empty);
	}


	/// <summary>
	/// 简单 Token 化。
	///
	/// 中文按字符切分，
	/// 英文按非字母数字字符切分。
	/// </summary>
	private static HashSet<string> Tokenize(
		string? value)
	{
		var result =
			new HashSet<string>(
				StringComparer.OrdinalIgnoreCase);

		if (string.IsNullOrWhiteSpace(
				value))
		{
			return result;
		}

		var normalized =
			Normalize(value);

		foreach (var ch in normalized)
		{
			if (char.IsLetterOrDigit(ch))
			{
				result.Add(
					ch.ToString());
			}
		}

		return result;
	}
}