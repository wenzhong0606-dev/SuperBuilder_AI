using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SuperBuilder_AI.Application.Metadata;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.Database;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Services.BI;

/// <summary>译码行为开关。</summary>
public sealed class DisplayResolutionOptions
{
	/// <summary>
	/// 是否就地替换原列值（默认 true：用户直接看到文本）。
	/// 置 false 时保留原码值，另增 {col}_name 列。
	/// </summary>
	public bool ReplaceInPlace { get; set; } = true;

	/// <summary>单次字典/FK 点查的最大键数量，防止超大 IN 列表。</summary>
	public int MaxLookupKeys { get; set; } = 2000;
}

/// <summary>
/// 查询结果字段译码服务。
///
/// <para>
/// 插入点：SQL 执行之后、ResultUnderstanding 之前（见 <c>BIConversationService</c> Step 7.5）。
/// 所有取数均为参数化 IN 点查，不改用户 SQL、不跨库 JOIN；任何异常只降级不改语义。
/// </para>
/// </summary>
public class DisplayResolutionService : IDisplayResolutionService
{
	private static readonly Regex SafeIdentifier =
		new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private readonly SuperBIContext _context;
	private readonly IDataSourceConnectionFactory _connections;
	private readonly DisplayResolutionOptions _options;
	private readonly ILogger<DisplayResolutionService>? _logger;

	public DisplayResolutionService(
		SuperBIContext context,
		IDataSourceConnectionFactory connections,
		DisplayResolutionOptions? options = null,
		ILogger<DisplayResolutionService>? logger = null)
	{
		_context = context;
		_connections = connections;
		_options = options ?? new DisplayResolutionOptions();
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<DisplayResolutionReport> EnrichAsync(
		QueryPlan plan,
		QueryResult result,
		long tenantId,
		long? userId,
		IReadOnlyCollection<long>? authorizedDataSourceIds,
		CorrectionResolution? learnedCorrections = null,
		CancellationToken cancellationToken = default)
	{
		var report = new DisplayResolutionReport();

		if (plan is null || result is null || !result.Success || result.Rows.Count == 0)
			return report;

		var tableName = plan.Tables.FirstOrDefault()?.TableName;
		if (string.IsNullOrWhiteSpace(tableName))
			return report;

		var columns = await LoadColumnsAsync(plan.DataSourceId, tableName!, cancellationToken);
		if (columns.Count == 0 && (learnedCorrections is null || !learnedCorrections.Any))
			return report;

		var outputColumns = result.Rows
			.SelectMany(r => r.Keys)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		foreach (var column in outputColumns)
		{
			try
			{
				columns.TryGetValue(column, out var meta);
				await ResolveColumnAsync(
					plan, result, column, meta, tenantId, authorizedDataSourceIds,
					learnedCorrections, report, cancellationToken);
			}
			catch (Exception ex)
			{
				// 译码是增值能力：任何失败只降级，绝不破坏主流程结果。
				_logger?.LogWarning(ex, "字段译码失败，已降级保留原值。Column={Column}", column);
				report.Skipped.Add($"{column}:{ex.GetType().Name}");
			}
		}

		return report;
	}

	private async Task ResolveColumnAsync(
		QueryPlan plan,
		QueryResult result,
		string column,
		MetadataColumn? meta,
		long tenantId,
		IReadOnlyCollection<long>? authorizedDataSourceIds,
		CorrectionResolution? learned,
		DisplayResolutionReport report,
		CancellationToken cancellationToken)
	{
		// 1) 跨源字典表（最高优先；数据源须已授权）
		if (meta is { IsDictBacked: true, DictConfigId: not null })
		{
			var config = await _context.MetadataDictionaryConfigs
				.AsNoTracking()
				.FirstOrDefaultAsync(
					x => x.Id == meta.DictConfigId && x.TenantId == tenantId && x.IsEnabled,
					cancellationToken);

			if (config is null)
			{
				report.Skipped.Add($"{column}:dict-config-missing");
			}
			else if (authorizedDataSourceIds is not null
				&& !authorizedDataSourceIds.Contains(config.DataSourceId))
			{
				// 越权防线：未绑定字典所在数据源（如未绑 PMIS 的租户）→ 静默降级到后续策略。
				report.Skipped.Add($"{column}:dict-source-unauthorized");
			}
			else
			{
				var categories = SplitCategories(meta.DictCategoryValue);
				if (categories.Count == 0 && !string.IsNullOrWhiteSpace(config.TypeColumn))
				{
					// 有分类列但列上未声明分类 → 无法安全点查，如实标记而非误报 no-match。
					report.Skipped.Add($"{column}:dict-category-unspecified");
				}
				else
				{
					var map = await LoadDictionaryMapAsync(config, meta.DictCategoryValue, cancellationToken);
					if (map.Count > 0)
					{
						var applied = ApplyMap(result.Rows, column, map);
						if (applied > 0)
						{
							report.Hits.Add(new DisplayResolutionHit
							{
								ColumnName = column,
								Strategy = "dictionary",
								Source = $"{config.TableName}.{config.NameColumn}",
								ResolvedCellCount = applied,
							});
							return;
						}
					}
					report.Skipped.Add($"{column}:dict-no-match");
				}
			}
		}

		// 2) 同源外键元数据（扫描捕获）
		if (meta is not null
			&& !string.IsNullOrWhiteSpace(meta.ReferencedTable)
			&& !string.IsNullOrWhiteSpace(meta.ReferencedDisplayColumn))
		{
			var map = await LoadForeignKeyMapAsync(
				plan.DataSourceId,
				meta.ReferencedTable!,
				meta.ReferencedColumn,
				meta.ReferencedDisplayColumn!,
				DistinctKeys(result.Rows, column),
				cancellationToken);

			var applied = map.Count == 0 ? 0 : ApplyMap(result.Rows, column, map);
			if (applied > 0)
			{
				report.Hits.Add(new DisplayResolutionHit
				{
					ColumnName = column,
					Strategy = "foreign-key",
					Source = $"{meta.ReferencedTable}.{meta.ReferencedDisplayColumn}",
					ResolvedCellCount = applied,
				});
				return;
			}
		}

		// 3) 学习规则（显式纠正的 ValueMap / FkJoin / ColumnDisplay）
		if (learned is not null && learned.Any)
		{
			var valueRule = learned.ValueMapFor(column);
			if (valueRule?.Payload.ValueMap is { Count: > 0 } learnedMap)
			{
				var applied = ApplyMap(result.Rows, column, NormalizeMap(learnedMap));
				if (applied > 0)
				{
					report.Hits.Add(new DisplayResolutionHit
					{
						ColumnName = column,
						Strategy = "learned-value-map",
						Source = $"rule:{valueRule.RuleId}",
						ResolvedCellCount = applied,
					});
					return;
				}
			}

			var fkRule = learned.ForeignKeyFor(column);
			if (fkRule is not null
				&& !string.IsNullOrWhiteSpace(fkRule.Payload.TargetTable)
				&& !string.IsNullOrWhiteSpace(fkRule.Payload.TargetDisplayColumn))
			{
				var map = await LoadForeignKeyMapAsync(
					fkRule.DataSourceId ?? plan.DataSourceId,
					fkRule.Payload.TargetTable!,
					fkRule.Payload.TargetColumn,
					fkRule.Payload.TargetDisplayColumn!,
					DistinctKeys(result.Rows, column),
					cancellationToken);

				var applied = map.Count == 0 ? 0 : ApplyMap(result.Rows, column, map);
				if (applied > 0)
				{
					report.Hits.Add(new DisplayResolutionHit
					{
						ColumnName = column,
						Strategy = "learned-foreign-key",
						Source = $"{fkRule.Payload.TargetTable}.{fkRule.Payload.TargetDisplayColumn}",
						ResolvedCellCount = applied,
					});
					return;
				}
			}
		}

		// 3b) 学习到的「列 → 字典分类」声明：「type 应显示 receipt_type 名称」→ 按分类查字典
		if (learned is not null && learned.Any)
		{
			var displayRule = learned.DisplayFor(column);
			if (displayRule is not null
				&& !string.IsNullOrWhiteSpace(displayRule.Payload.CategoryHint))
			{
				var map = await LoadCategoryMapAsync(
					tenantId,
					displayRule.Payload.CategoryHint!,
					plan.DataSourceId,
					authorizedDataSourceIds,
					cancellationToken);

				var applied = map.Count == 0 ? 0 : ApplyMap(result.Rows, column, map);
				if (applied > 0)
				{
					report.Hits.Add(new DisplayResolutionHit
					{
						ColumnName = column,
						Strategy = "learned-dictionary",
						Source = $"category:{displayRule.Payload.CategoryHint}",
						ResolvedCellCount = applied,
					});
					return;
				}
			}
		}

		// 4) 列内嵌值映射（列注释图例 / 元数据配置）
		if (meta is not null && !string.IsNullOrWhiteSpace(meta.ValueMapJson))
		{
			var map = ParseValueMapJson(meta.ValueMapJson);
			if (map.Count > 0)
			{
				var applied = ApplyMap(result.Rows, column, map);
				if (applied > 0)
				{
					report.Hits.Add(new DisplayResolutionHit
					{
						ColumnName = column,
						Strategy = "value-map",
						Source = "MetadataColumn.ValueMapJson",
						ResolvedCellCount = applied,
					});
					return;
				}
			}
		}
	}

	/// <summary>加载主表下所有列的元数据（按列名不区分大小写索引）。</summary>
	private async Task<Dictionary<string, MetadataColumn>> LoadColumnsAsync(
		long dataSourceId,
		string tableName,
		CancellationToken cancellationToken)
	{
		var columns = await _context.MetadataColumns.WhereActiveVersion(_context)
			.AsNoTracking()
			.Where(c => c.MetadataTable != null
				&& c.MetadataTable.DataSourceId == dataSourceId
				&& c.MetadataTable.TableName == tableName)
			.ToListAsync(cancellationToken);

		var map = new Dictionary<string, MetadataColumn>(StringComparer.OrdinalIgnoreCase);
		foreach (var column in columns)
			map.TryAdd(column.ColumnName, column);

		return map;
	}

	/// <summary>
	/// 字典点查：SELECT code,name FROM dict [WHERE type IN (...) AND active=...]（支持跨数据源）。
	///
	/// <para>
	/// 分类支持多值：WMS 的 <c>type</c> 列语义是「单据类型」，同列码值实测跨
	/// warehousing_type / outbound_type / variation_type / Input_output_type / wms_check_type
	/// 五个分类，故单分类过滤会漏译大部分码值。多分类以 , ; | 分隔声明。
	/// </para>
	/// </summary>
	private async Task<Dictionary<string, string>> LoadDictionaryMapAsync(
		MetadataDictionaryConfig config,
		string? category,
		CancellationToken cancellationToken)
	{
		if (!IsSafe(config.TableName) || !IsSafe(config.CodeColumn) || !IsSafe(config.NameColumn))
			return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		var categorized = !string.IsNullOrWhiteSpace(config.TypeColumn) && IsSafe(config.TypeColumn!);
		var categories = SplitCategories(category);

		// 配了分类列却未给分类值：绝不退化为全表拉取。
		// 字典表的 code 通常只在分类内唯一，全表拉取会把跨分类的同码值错误译码。
		if (categorized && categories.Count == 0)
			return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		var sql =
			$"SELECT {config.CodeColumn} AS Code, {config.NameColumn} AS Name FROM {config.TableName}";

		var parameters = new DynamicParameters();
		var predicates = new List<string>();

		if (categorized && categories.Count > 0)
		{
			predicates.Add($"{config.TypeColumn} IN @categories");
			parameters.Add("categories", categories);
		}

		// 软删/停用过滤：不过滤会把已删除码值译出来（PMIS = status '0' 正常）。
		if (!string.IsNullOrWhiteSpace(config.ActiveFilterColumn)
			&& IsSafe(config.ActiveFilterColumn!)
			&& config.ActiveFilterValue is not null)
		{
			predicates.Add($"{config.ActiveFilterColumn} = @active");
			parameters.Add("active", config.ActiveFilterValue);
		}

		if (predicates.Count > 0)
			sql += " WHERE " + string.Join(" AND ", predicates);

		return await QueryMapAsync(config.DataSourceId, sql, parameters, cancellationToken);
	}

	/// <summary>多值分类分隔符（中英文逗号/分号 + 竖线）。</summary>
	private static readonly char[] CategorySeparators = { ',', ';', '|', '，', '；' };

	/// <summary>
	/// 切分多分类声明，如 <c>"warehousing_type,outbound_type|variation_type"</c>。
	/// 去重、去空白，最多 64 个（防超长 IN 列表）。
	/// </summary>
	public static IReadOnlyList<string> SplitCategories(string? category)
	{
		var result = new List<string>();
		if (string.IsNullOrWhiteSpace(category)) return result;

		foreach (var part in category.Split(
			CategorySeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			if (!result.Contains(part, StringComparer.OrdinalIgnoreCase))
				result.Add(part);
		}

		return result.Take(64).ToList();
	}

	/// <summary>
	/// 按用户声明的字典分类（学习规则的 CategoryHint）在已授权数据源中查字典。
	/// 同源配置优先，跨源（如 PMIS）须命中 authorizedDataSourceIds，否则跳过。
	/// </summary>
	private async Task<Dictionary<string, string>> LoadCategoryMapAsync(
		long tenantId,
		string category,
		long queryDataSourceId,
		IReadOnlyCollection<long>? authorizedDataSourceIds,
		CancellationToken cancellationToken)
	{
		var configs = await _context.MetadataDictionaryConfigs
			.AsNoTracking()
			.Where(x => x.TenantId == tenantId && x.IsEnabled && x.TypeColumn != null)
			.ToListAsync(cancellationToken);

		foreach (var config in configs
			.OrderBy(c => c.DataSourceId == queryDataSourceId ? 0 : 1))
		{
			if (authorizedDataSourceIds is not null
				&& !authorizedDataSourceIds.Contains(config.DataSourceId))
				continue;

			var map = await LoadDictionaryMapAsync(config, category, cancellationToken);
			if (map.Count > 0) return map;
		}

		return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
	}

	/// <summary>同源外键点查：SELECT key,display FROM target WHERE key IN @ids。</summary>
	private async Task<Dictionary<string, string>> LoadForeignKeyMapAsync(
		long dataSourceId,
		string targetTable,
		string? targetColumn,
		string displayColumn,
		IReadOnlyCollection<string> keys,
		CancellationToken cancellationToken)
	{
		if (keys.Count == 0) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		var keyColumn = string.IsNullOrWhiteSpace(targetColumn) ? "id" : targetColumn!;
		if (!IsSafe(targetTable) || !IsSafe(keyColumn) || !IsSafe(displayColumn))
			return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		var sql =
			$"SELECT {keyColumn} AS Code, {displayColumn} AS Name FROM {targetTable} WHERE {keyColumn} IN @ids";

		var parameters = new DynamicParameters();
		parameters.Add("ids", keys);

		return await QueryMapAsync(dataSourceId, sql, parameters, cancellationToken);
	}

	private async Task<Dictionary<string, string>> QueryMapAsync(
		long dataSourceId,
		string sql,
		DynamicParameters parameters,
		CancellationToken cancellationToken)
	{
		var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		await using var connection = await _connections.CreateAsync(dataSourceId);
		if (connection.State != System.Data.ConnectionState.Open)
			await connection.OpenAsync(cancellationToken);

		var rows = await connection.QueryAsync(sql, parameters);
		foreach (var row in rows)
		{
			if (row is not IDictionary<string, object> record) continue;
			var code = NormalizeCode(record.TryGetValue("Code", out var c) ? c : null);
			var name = (record.TryGetValue("Name", out var n) ? n : null)?.ToString()?.Trim();
			if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name)) continue;
			map[code] = name!;
		}

		return map;
	}

	/// <summary>把映射应用到结果行（就地替换或新增 {col}_name）。</summary>
	private int ApplyMap(
		IReadOnlyList<Dictionary<string, object?>> rows,
		string column,
		IReadOnlyDictionary<string, string> map)
	{
		var applied = 0;
		foreach (var row in rows)
		{
			var key = row.Keys.FirstOrDefault(k =>
				string.Equals(k, column, StringComparison.OrdinalIgnoreCase));
			if (key is null) continue;

			var code = NormalizeCode(row[key]);
			if (string.IsNullOrEmpty(code) || !map.TryGetValue(code, out var label))
				continue;

			if (string.IsNullOrWhiteSpace(label)) continue;

			if (_options.ReplaceInPlace)
				row[key] = label;
			else
				row[$"{key}_name"] = label;

			applied++;
		}

		return applied;
	}

	private static IReadOnlyCollection<string> DistinctKeys(
		IReadOnlyList<Dictionary<string, object?>> rows,
		string column)
	{
		var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var row in rows)
		{
			var key = row.Keys.FirstOrDefault(k =>
				string.Equals(k, column, StringComparison.OrdinalIgnoreCase));
			if (key is null) continue;
			var code = NormalizeCode(row[key]);
			if (!string.IsNullOrEmpty(code)) keys.Add(code);
		}

		return keys.Take(2000).ToList();
	}

	/// <summary>解析 MetadataColumn.ValueMapJson：支持对象字典或 [{code,label}] 数组。</summary>
	public static IReadOnlyDictionary<string, string> ParseValueMapJson(string? json)
	{
		var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		if (string.IsNullOrWhiteSpace(json)) return map;

		try
		{
			using var document = JsonDocument.Parse(json);
			var root = document.RootElement;

			if (root.ValueKind == JsonValueKind.Object)
			{
				foreach (var property in root.EnumerateObject())
				{
					var label = property.Value.ValueKind == JsonValueKind.String
						? property.Value.GetString()
						: property.Value.ToString();
					if (!string.IsNullOrWhiteSpace(label))
						map[property.Name.Trim()] = label!.Trim();
				}
			}
			else if (root.ValueKind == JsonValueKind.Array)
			{
				foreach (var element in root.EnumerateArray())
				{
					var code = GetString(element, "code") ?? GetString(element, "value") ?? GetString(element, "key");
					var label = GetString(element, "label") ?? GetString(element, "name") ?? GetString(element, "text");
					if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(label))
						map[code!.Trim()] = label!.Trim();
				}
			}
		}
		catch (JsonException)
		{
			// 非法 JSON 视为无映射。
		}

		return map;
	}

	private static Dictionary<string, string> NormalizeMap(IReadOnlyDictionary<string, string> source)
	{
		var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var (key, value) in source)
		{
			var code = NormalizeCode(key);
			if (!string.IsNullOrEmpty(code) && !string.IsNullOrWhiteSpace(value))
				map[code] = value.Trim();
		}

		return map;
	}

	private static string? GetString(JsonElement element, string propertyName)
		=> element.ValueKind == JsonValueKind.Object
			&& element.TryGetProperty(propertyName, out var value)
			&& value.ValueKind == JsonValueKind.String
				? value.GetString()
				: null;

	/// <summary>码值归一化：数值型去掉多余小数位，字符串去空白（保证 "1" 与 1、" 1 " 一致）。</summary>
	public static string NormalizeCode(object? raw)
	{
		switch (raw)
		{
			case null:
				return string.Empty;
			// MySQL tinyint(1)（WMS 的 flag / 0-1 码值列）会被驱动物化为 bool。
			// 必须回到 "0"/"1"，否则 "True"/"False" 与字典、注释图例、学习映射里的
			// "1"/"0" 永远匹配不上 —— 实测 wms_storage_receipt.source_type 译码失效即此因。
			case bool flag:
				return flag ? "1" : "0";
			case decimal dec:
				return (dec == decimal.Truncate(dec)
					? dec.ToString("0", CultureInfo.InvariantCulture)
					: dec.ToString(CultureInfo.InvariantCulture)).Trim();
			case double dbl:
				return (dbl == Math.Truncate(dbl)
					? dbl.ToString("0", CultureInfo.InvariantCulture)
					: dbl.ToString(CultureInfo.InvariantCulture)).Trim();
			case float flt:
				return (flt == MathF.Truncate(flt)
					? flt.ToString("0", CultureInfo.InvariantCulture)
					: flt.ToString(CultureInfo.InvariantCulture)).Trim();
			default:
				return raw.ToString()?.Trim() ?? string.Empty;
		}
	}

	private static bool IsSafe(string identifier)
		=> !string.IsNullOrWhiteSpace(identifier) && SafeIdentifier.IsMatch(identifier);
}
