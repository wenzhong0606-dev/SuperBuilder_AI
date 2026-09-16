using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.Platform;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.Metadata;
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


	/// <summary>
	/// 数据源作用域下的向量过采样倍数。
	///
	/// 数据源过滤只能发生在「向量召回 → 全量落库读回」之后（Qdrant payload 未写入
	/// dataSourceId，且写入侧不保证与元库同步），因此按 topK 原样召回时，
	/// 跨源候选会挤占名额，使作用域内候选不足。
	/// </summary>
	private const int ScopeOversampleFactor = 5;


	/// <summary>数据源作用域下的过采样上限，避免极窄作用域把召回量放大到无谓的规模。</summary>
	private const int ScopeOversampleCeiling = 200;


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
	public Task<List<MetadataSemanticSearchResult>>
		SearchAsync(
			string question,
			int topK = 10,
			LocaleContext? locale = null)
		=> SearchCoreAsync(
			question,
			topK,
			locale);


	/// <summary>
	/// Metadata语义搜索（数据源作用域内）。
	///
	/// 作用于「查询理解」阶段：把喂给 LLM 的 Metadata 上下文收敛到
	/// 当前用户已授权 / 已显式请求的数据源，避免跨源同名列污染
	/// Metric / Dimension 解析（详见接口注释）。
	///
	/// 过滤点是**落库读回之后**按 <c>MetadataTable.DataSourceId</c> 判定，
	/// 即以元数据库为准（向量 payload 仅用于定位主键，不用于判归属），
	/// 因此不受索引新旧影响。
	/// </summary>
	public async Task<List<MetadataSemanticSearchResult>>
		SearchAsync(
			string question,
			int topK,
			LocaleContext? locale,
			IReadOnlyCollection<long>? dataSourceIds)
	{
		// null = 不限定作用域 => 与三参重载逐字节同路径（Golden / 评估器零回归）。
		if (dataSourceIds is null)
		{
			return await SearchCoreAsync(
				question,
				topK,
				locale);
		}

		var scope =
			dataSourceIds
				.Where(id => id > 0)
				.ToHashSet();

		// 空集合：无任何允许的数据源 => 无候选（不抛异常，处置权交调用方）。
		if (scope.Count == 0)
		{
			return new List<MetadataSemanticSearchResult>();
		}

		if (topK <= 0)
		{
			return new List<MetadataSemanticSearchResult>();
		}

		// 过采样：过滤在全量召回之后进行，须多召回一些才能保证作用域内凑满 topK。
		var oversample =
			Math.Clamp(
				topK * ScopeOversampleFactor,
				topK,
				Math.Max(topK, ScopeOversampleCeiling));

		var candidates =
			await SearchCoreAsync(
				question,
				oversample,
				locale);

		return candidates
			.Where(r =>
				r.Table is not null
				&& scope.Contains(r.Table.DataSourceId))
			.Take(topK)
			.ToList();
	}




	/// <summary>
	/// 语义检索主流程（向量召回 + 落库读回 + 排序），不含数据源过滤。
	///
	/// 两个公开重载都汇聚到这里，保证「限定作用域」与「不限定作用域」
	/// 共用同一套召回 / 评分 / 语言提升逻辑。
	/// </summary>
	private async Task<List<MetadataSemanticSearchResult>>
		SearchCoreAsync(
			string question,
			int topK,
			LocaleContext? locale)
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

	/// <summary>
	/// 基于语义层关键词/Synonyms/BusinessMeaning 的<strong>子串</strong>确定性匹配检索，用于实体计数等场景。
	/// 与 <see cref="SearchByKeywordAsync"/> 的精确匹配不同，本方法按“归一化子串包含”匹配，
	/// 以覆盖“入库单ID/入库单号”等将实体词作为前缀的关键词形态，不依赖向量召回深度。
	/// </summary>
	public async Task<List<MetadataSemanticSearchResult>> SearchByKeywordSubstringAsync(string keyword, int limit = 30)
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
			if (SubstringKeywordMatches(s.Keywords, normalized)
				|| SubstringKeywordMatches(s.Synonyms, normalized)
				|| (s.BusinessMeaning is not null && Normalize(s.BusinessMeaning).Contains(normalized, StringComparison.OrdinalIgnoreCase)))
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

	private static bool SubstringKeywordMatches(string? keywords, string normalized)
	{
		if (string.IsNullOrWhiteSpace(keywords)) return false;
		return keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Any(k => Normalize(k).Contains(normalized, StringComparison.OrdinalIgnoreCase));
	}

	private static string Normalize(string? value) =>
		(value ?? string.Empty).Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

	/// <summary>
	/// 按物理表名在（已授权）元数据库目录中确定性解析目标表。
	/// 仅用于多轮表纠正：当 ScopeAsync 已把目标表所在数据源收敛掉时，
	/// 仍能在“全部已授权数据源”范围内按表名找回，强制作为主表。
	/// 表名大小写不敏感（依赖 SQL Server 默认 CI 排序规则）。
	/// </summary>
	public async Task<MetadataTable?> ResolveTableByNameAsync(
		string tableName,
		IReadOnlyCollection<long>? authorizedDataSourceIds = null)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			return null;
		}

		var name = tableName.Trim();

		var query =
			_context
				.MetadataTables
				.Include(t => t.Columns)
				.AsNoTracking();

		if (authorizedDataSourceIds is { Count: > 0 })
		{
			query =
				query.Where(
					t => authorizedDataSourceIds.Contains(
						t.DataSourceId));
		}

		return await query
			.FirstOrDefaultAsync(
				t => t.TableName == name);
	}



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
