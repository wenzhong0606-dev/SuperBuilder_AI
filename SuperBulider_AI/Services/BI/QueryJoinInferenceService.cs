using SuperBulider_AI.Interfaces;
using SuperBulider_AI.Interfaces.BI;
using SuperBulider_AI.Models.AI;
using SuperBulider_AI.Models.BI;

namespace SuperBulider_AI.Services.BI;

/// <summary>
/// 动态JOIN推断服务。
///
/// Phase 1.6.2
///
/// 不依赖数据库Foreign Key。
///
/// 推断依据:
///
/// 1. 字段名称
/// 2. 表名称
/// 3. 数据类型
/// 4. MetadataSemantic业务语义
///
/// 当前阶段采用确定性规则。
///
/// 后续接入真实Embedding模型后，
/// 可以继续增加语义向量评分。
/// </summary>
public class QueryJoinInferenceService
	: IQueryJoinInferenceService
{
	/// <summary>
	/// Embedding服务。
	///
	/// 当前主要为后续真实语义模型预留。
	/// </summary>
	private readonly IEmbeddingService _embedding;

	public QueryJoinInferenceService(
		IEmbeddingService embedding)
	{
		_embedding = embedding;
	}

	/// <summary>
	/// 推断JOIN关系。
	/// </summary>
	public async Task<List<QueryJoinCandidate>> InferAsync(
		List<MetadataSemanticSearchResult> metadataResults)
	{
		var candidates =
			new List<QueryJoinCandidate>();

		/*
		 * 只处理同时存在:
		 *
		 * Table
		 * +
		 * Column
		 *
		 * 的Metadata结果。
		 */

		var columns =
			metadataResults
			.Where(x =>
				x.Table != null &&
				x.Column != null)
			.Select(x =>
				new
				{
					Result = x,
					Table = x.Table!,
					Column = x.Column!,
					Semantic = x.Semantic
				})
			.GroupBy(x => x.Column.Id)
			.Select(x => x.First())
			.ToList();

		/*
		 * 两两比较字段。
		 */

		for (var i = 0; i < columns.Count; i++)
		{
			for (var j = i + 1; j < columns.Count; j++)
			{
				var left =
					columns[i];

				var right =
					columns[j];

				/*
				 * 同一张表不需要JOIN。
				 */

				if (left.Table.Id == right.Table.Id)
				{
					continue;
				}

				var score =
					CalculateScore(
						left.Table,
						left.Column,
						left.Semantic,
						right.Table,
						right.Column,
						right.Semantic,
						out var reason);

				/*
				 * 第一阶段使用较高阈值。
				 *
				 * 宁可少JOIN，
				 * 也不能错误JOIN。
				 */

				if (score < 0.75)
				{
					continue;
				}

				candidates.Add(
					new QueryJoinCandidate
					{
						LeftTableId =
							left.Table.Id,

						LeftColumnId =
							left.Column.Id,

						RightTableId =
							right.Table.Id,

						RightColumnId =
							right.Column.Id,

						LeftTableName =
							left.Table.TableName,

						LeftColumnName =
							left.Column.ColumnName,

						RightTableName =
							right.Table.TableName,

						RightColumnName =
							right.Column.ColumnName,

						Confidence =
							score,

						Reason =
							reason
					});
			}
		}

		await Task.CompletedTask;

		return candidates
			.OrderByDescending(x =>
				x.Confidence)
			.ToList();
	}

	/// <summary>
	/// 计算两个Metadata字段之间的JOIN可能性。
	/// </summary>
	private double CalculateScore(
		Models.Metadata.MetadataTable leftTable,
		Models.Metadata.MetadataColumn leftColumn,
		Models.Metadata.MetadataSemantic? leftSemantic,
		Models.Metadata.MetadataTable rightTable,
		Models.Metadata.MetadataColumn rightColumn,
		Models.Metadata.MetadataSemantic? rightSemantic,
		out string reason)
	{
		var score = 0.0;

		var reasons =
			new List<string>();

		/*
		 * 1.
		 * 数据类型匹配。
		 */

		if (!string.IsNullOrWhiteSpace(leftColumn.DataType) &&
			!string.IsNullOrWhiteSpace(rightColumn.DataType) &&
			string.Equals(
				NormalizeType(leftColumn.DataType),
				NormalizeType(rightColumn.DataType),
				StringComparison.OrdinalIgnoreCase))
		{
			score += 0.20;

			reasons.Add(
				"字段数据类型一致");
		}

		/*
		 * 2.
		 * 字段名称直接匹配。
		 */

		var leftName =
			NormalizeName(
				leftColumn.ColumnName);

		var rightName =
			NormalizeName(
				rightColumn.ColumnName);

		if (!string.IsNullOrWhiteSpace(leftName) &&
			leftName == rightName)
		{
			score += 0.45;

			reasons.Add(
				"字段名称一致");
		}

		/*
		 * 3.
		 * 外键命名模式:
		 *
		 * CustomerId
		 *
		 * 对应:
		 *
		 * Customer.Id
		 */

		if (IsReferencePattern(
			leftTable.TableName,
			leftColumn.ColumnName,
			rightTable.TableName,
			rightColumn.ColumnName))
		{
			score += 0.30;

			reasons.Add(
				"字段符合表主键引用命名模式");
		}

		/*
		 * 4.
		 * Semantic业务含义匹配。
		 */

		var semanticScore =
			CalculateSemanticKeywordScore(
				leftSemantic,
				rightSemantic);

		if (semanticScore > 0)
		{
			score +=
				semanticScore;

			reasons.Add(
				"字段业务语义存在关联");
		}

		if (score > 1)
		{
			score = 1;
		}

		reason =
			reasons.Count == 0
				? "字段存在潜在业务关联"
				: string.Join(
					"；",
					reasons);

		return score;
	}

	/// <summary>
	/// 判断字段是否符合常见引用关系。
	///
	/// 示例:
	///
	/// SalesOrder.CustomerId
	/// Customer.Id
	/// </summary>
	private bool IsReferencePattern(
		string? leftTableName,
		string? leftColumnName,
		string? rightTableName,
		string? rightColumnName)
	{
		if (string.IsNullOrWhiteSpace(leftTableName) ||
			string.IsNullOrWhiteSpace(leftColumnName) ||
			string.IsNullOrWhiteSpace(rightTableName) ||
			string.IsNullOrWhiteSpace(rightColumnName))
		{
			return false;
		}

		var left =
			NormalizeName(
				leftColumnName);

		var right =
			NormalizeName(
				rightColumnName);

		var leftTable =
			NormalizeName(
				leftTableName);

		var rightTable =
			NormalizeName(
				rightTableName);

		if (right == "id" &&
			left == leftTable + "id")
		{
			return true;
		}

		if (left == "id" &&
			right == rightTable + "id")
		{
			return true;
		}

		return false;
	}

	/// <summary>
	/// 计算两个Semantic之间的关键词关联度。
	/// </summary>
	private double CalculateSemanticKeywordScore(
		Models.Metadata.MetadataSemantic? left,
		Models.Metadata.MetadataSemantic? right)
	{
		if (left == null ||
			right == null)
		{
			return 0;
		}

		var leftWords =
			GetWords(left);

		var rightWords =
			GetWords(right);

		if (leftWords.Count == 0 ||
			rightWords.Count == 0)
		{
			return 0;
		}

		var intersection =
			leftWords
			.Intersect(
				rightWords,
				StringComparer.OrdinalIgnoreCase)
			.Count();

		if (intersection == 0)
		{
			return 0;
		}

		var ratio =
			(double)intersection /
			Math.Max(
				leftWords.Count,
				rightWords.Count);

		/*
		 * Semantic最多贡献0.25。
		 */

		return Math.Min(
			0.25,
			ratio * 0.25);
	}

	/// <summary>
	/// 获取Semantic关键词集合。
	/// </summary>
	private HashSet<string> GetWords(
		Models.Metadata.MetadataSemantic semantic)
	{
		var result =
			new HashSet<string>(
				StringComparer.OrdinalIgnoreCase);

		AddWords(
			result,
			semantic.Keywords);

		AddWords(
			result,
			semantic.Synonyms);

		AddWords(
			result,
			semantic.BusinessMeaning);

		return result;
	}

	/// <summary>
	/// 添加语义文本。
	/// </summary>
	private void AddWords(
		HashSet<string> words,
		string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}

		foreach (var item in
			text.Split(
				new[]
				{
					',',
					'，',
					';',
					'；',
					' ',
					'|'
				},
				StringSplitOptions.RemoveEmptyEntries))
		{
			var value =
				NormalizeName(item);

			if (!string.IsNullOrWhiteSpace(value))
			{
				words.Add(value);
			}
		}
	}

	/// <summary>
	/// 标准化字段或表名称。
	/// </summary>
	private string NormalizeName(
		string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}

		return new string(
			value
				.Where(char.IsLetterOrDigit)
				.ToArray())
			.ToLowerInvariant();
	}

	/// <summary>
	/// 标准化数据类型。
	/// </summary>
	private string NormalizeType(
		string value)
	{
		var type =
			value
				.Trim()
				.ToLowerInvariant();

		if (type.Contains("bigint"))
		{
			return "integer";
		}

		if (type.Contains("int"))
		{
			return "integer";
		}

		if (type.Contains("decimal") ||
			type.Contains("numeric"))
		{
			return "decimal";
		}

		if (type.Contains("double") ||
			type.Contains("float"))
		{
			return "float";
		}

		if (type.Contains("char") ||
			type.Contains("text") ||
			type.Contains("varchar"))
		{
			return "string";
		}

		if (type.Contains("date") ||
			type.Contains("time"))
		{
			return "datetime";
		}

		return type;
	}
}