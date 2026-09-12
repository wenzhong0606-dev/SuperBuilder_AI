using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.Platform;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.Organization;


namespace SuperBuilder_AI.Services;

/// <summary>
/// Metadata语义检索服务
///
/// 功能:
///
/// 用户问题
///     ↓
/// Embedding
///     ↓
/// Qdrant Query
///     ↓
/// 根据VectorType解析:
///
/// table
/// column
/// semantic
///
///     ↓
///
/// MetadataTable
/// MetadataColumn
/// MetadataSemantic
///
///     ↓
///
/// MetadataSemanticSearchResult
///
/// </summary>
public class MetadataSemanticSearchService
	: IMetadataSemanticSearchService
{

	/// <summary>
	/// P5：多语言标签命中对候选排序的提升上限（乘以匹配强度 0–1）。
	/// 取值保守（0.15）：标签命中是<strong>纠正偏序</strong>的证据，
	/// 不应压倒向量相似度本身——避免短标签误命中时反向污染排序。
	/// </summary>
	private const double LabelRecallBoostFactor = 0.15;


	private readonly IEmbeddingService _embedding;


	private readonly IQdrantService _qdrant;


	private readonly SuperBIContext _context;


	/// <summary>
	/// P5：多语言标签召回服务。仅在传入<strong>非默认</strong>语言区域时参与排序。
	/// </summary>
	private readonly ISemanticLabelRecallService? _labelRecall;



	public MetadataSemanticSearchService(
		IEmbeddingService embedding,
		IQdrantService qdrant,
		SuperBIContext context,
		ISemanticLabelRecallService? labelRecall = null)
	{

		_embedding = embedding;

		_qdrant = qdrant;

		_context = context;

		_labelRecall = labelRecall;

	}




	/// <summary>
	/// Metadata语义搜索
	/// </summary>
	public async Task<List<MetadataSemanticSearchResult>>
		SearchAsync(
			string question,
			int topK = 10,
			LocaleContext? locale = null)
	{



		/*
		 * 1.
		 * 用户问题Embedding
		 */

		var vector =
			await _embedding.GenerateAsync(
				question,
				"query");




		/*
		 * 2.
		 * Qdrant查询
		 */

		var points =
			await _qdrant
			.QueryAsync(
				vector,
				topK);




		var results =
			new List<MetadataSemanticSearchResult>();




		/*
		 * 3.
		 * 解析Vector
		 */

		foreach (var point in points)
		{


			if (!point.Payload
				.TryGetValue(
					"type",
					out var typeValue))
			{
				continue;
			}



			var vectorType =
				typeValue
				.ToString()
				?.ToLower();




			if (string.IsNullOrWhiteSpace(vectorType))
			{
				continue;
			}




			switch (vectorType)
			{

				/*
				 * ============================
				 * Table Vector
				 * ============================
				 */

				case "table":

					await LoadTableVectorAsync(
						point,
						results);

					break;




				/*
				 * ============================
				 * Column Vector
				 * ============================
				 */

				case "column":

					await LoadColumnVectorAsync(
						point,
						results);

					break;




				/*
				 * ============================
				 * Semantic Vector
				 * ============================
				 */

				case "semantic":

					await LoadSemanticVectorAsync(
						point,
						results);

					break;


			}

		}




		/*
		 * 4.
		 * P5：多语言标签排序提升（可选）
		 *
		 * 仅当显式传入非默认语言区域时执行：
		 *   - locale 为 null 或平台默认语言（zh-CN）=> 完全跳过，行为与 P5 之前逐字节一致；
		 *   - 传入其他语言 => 对命中多语言标签的候选提升排序（跨语言 Embedding 常能召回
		 *     正确概念但排序偏后，标签命中的确定性证据用于纠正该偏序）。
		 *
		 * 只提升已召回的候选，绝不注入合成候选——避免凭空产生下游无法解释的结果。
		 */

		if (_labelRecall is not null
			&& locale is not null
			&& !locale.IsDefault
			&& !string.IsNullOrEmpty(locale.Culture))
		{
			await ApplyLabelRecallBoostAsync(
				question,
				locale,
				results);
		}


		return results
			.OrderByDescending(x =>
				x.Score)
			.ToList();

	}




	/// <summary>
	/// 基于语义层关键词/Synonyms 的确定性精确匹配检索，不依赖向量召回深度。
	/// 用于评估器等场景：当向量召回候选缺少词法证据时，作为确定性兜底，
	/// 直接按 Keywords/Synonyms 独立关键词精确匹配 query 返回携带物理绑定的候选。
	/// </summary>
	public async Task<List<MetadataSemanticSearchResult>> SearchByKeywordAsync(string keyword, int limit = 30)
	{
		if (string.IsNullOrWhiteSpace(keyword)) return new List<MetadataSemanticSearchResult>();
		var normalized = Normalize(keyword);
		if (normalized.Length == 0) return new List<MetadataSemanticSearchResult>();

		var semantics = await _context.MetadataSemantics
			.Include(x => x.MetadataColumn)
			.ThenInclude(x => x!.MetadataTable)
			.AsNoTracking()
			.ToListAsync();

		var hits = new List<MetadataSemanticSearchResult>();
		foreach (var s in semantics)
		{
			if (s.MetadataColumn is null || s.MetadataColumn.MetadataTable is null) continue;
			if (KeywordMatches(s.Keywords, normalized) || KeywordMatches(s.Synonyms, normalized))
			{
				hits.Add(new MetadataSemanticSearchResult
				{
					VectorType = "semantic",
					Table = s.MetadataColumn.MetadataTable,
					Column = s.MetadataColumn,
					Semantic = s,
					Score = 0.99
				});
			}
		}
		return hits.OrderByDescending(x => x.Score).Take(limit).ToList();
	}

	private static bool KeywordMatches(string? keywords, string normalized)
	{
		if (string.IsNullOrWhiteSpace(keywords)) return false;
		return keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Any(k => Normalize(k) == normalized);
	}

	private static string Normalize(string? value) =>
		(value ?? string.Empty).Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();



	/// <summary>
	/// P5：按多语言标签命中提升候选排序。
	/// 提升量 = <see cref="LabelRecallBoostFactor"/> × 匹配强度，并封顶于 1.0
	/// （Qdrant 余弦相似度上界为 1，避免产生超出原始分域的异常值）。
	/// </summary>
	private async Task ApplyLabelRecallBoostAsync(
		string question,
		LocaleContext locale,
		List<MetadataSemanticSearchResult> results)
	{
		if (results.Count == 0) return;

		IReadOnlyList<SemanticLabelHit> hits;

		try
		{
			hits = await _labelRecall!
				.MatchAsync(
					question,
					locale);
		}
		catch
		{
			// 标签召回属增强手段，任何异常都不得阻断主检索链路。
			return;
		}

		if (hits.Count == 0) return;

		var strengthBySemanticId = hits
			.GroupBy(h => h.SemanticId)
			.ToDictionary(g => g.Key, g => g.Max(h => h.Strength));

		foreach (var result in results)
		{
			var semanticId = result.Semantic?.Id;
			if (semanticId is null) continue;
			if (!strengthBySemanticId.TryGetValue(semanticId.Value, out var strength)) continue;

			result.Score = Math.Min(
				1.0,
				result.Score + (LabelRecallBoostFactor * strength));
		}
	}




	/// <summary>
	/// 加载表向量
	/// </summary>
	private async Task LoadTableVectorAsync(
		VectorSearchResult point,
		List<MetadataSemanticSearchResult> results)
	{


		if (!TryGetLong(
			point.Payload,
			"metadataId",
			out var tableId))
		{
			return;
		}




		var table =
			await _context.MetadataTables

			.Include(x =>
				x.Columns)

			.FirstOrDefaultAsync(x =>
				x.Id == tableId);




		if (table == null)
		{
			return;
		}




		results.Add(
			new MetadataSemanticSearchResult
			{

				VectorType =
					"table",


				VectorId =
					point.Id,


				Table =
					table,


				Score =
					point.Score

			});

	}




	/// <summary>
	/// 加载字段向量
	/// </summary>
	private async Task LoadColumnVectorAsync(
		VectorSearchResult point,
		List<MetadataSemanticSearchResult> results)
	{


		if (!TryGetLong(
			point.Payload,
			"columnId",
			out var columnId))
		{
			return;
		}




		var column =
			await _context.MetadataColumns

			.Include(x =>
				x.MetadataTable)

			.Include(x =>
				x.Semantic)

			.FirstOrDefaultAsync(x =>
				x.Id == columnId);




		if (column == null)
		{
			return;
		}




		results.Add(
			new MetadataSemanticSearchResult
			{

				VectorType =
					"column",


				VectorId =
					point.Id,


				Table =
					column.MetadataTable,


				Column =
					column,


				Semantic =
					column.Semantic,


				Score =
					point.Score

			});

	}




	/// <summary>
	/// 加载语义向量
	/// </summary>
	private async Task LoadSemanticVectorAsync(
		VectorSearchResult point,
		List<MetadataSemanticSearchResult> results)
	{


		if (!TryGetLong(
			point.Payload,
			"semanticId",
			out var semanticId))
		{
			return;
		}




		var semantic =
			await _context.MetadataSemantics

			.Include(x =>
				x.MetadataColumn)

			.ThenInclude(x =>
				x!.MetadataTable)

			.FirstOrDefaultAsync(x =>
				x.Id == semanticId);




		if (semantic == null
			||
			semantic.MetadataColumn == null)
		{
			return;
		}




		results.Add(
			new MetadataSemanticSearchResult
			{

				VectorType =
					"semantic",


				VectorId =
					point.Id,


				Table =
					semantic.MetadataColumn
					.MetadataTable,


				Column =
					semantic.MetadataColumn,


				Semantic =
					semantic,


				Score =
					point.Score

			});

	}




	/// <summary>
	/// 获取Qdrant Payload Long值
	/// </summary>
	private bool TryGetLong(
		Dictionary<string, object> payload,
		string key,
		out long value)
	{

		value = 0;



		if (!payload.TryGetValue(
			key,
			out var obj))
		{
			return false;
		}




		try
		{

			value =
				Convert.ToInt64(obj);

			return true;

		}
		catch
		{

			return false;

		}

	}


}
