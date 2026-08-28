using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// Phase 2.7 D14：从当前 Metadata Snapshot 提供 Dimension 物理绑定证据。
/// 不生成 SQL，不写入 QueryPlan.Joins，不进行业务字段硬编码。
/// </summary>
public sealed class DimensionResolutionEvidenceService : IDimensionResolutionEvidenceService
{
    // D14-R1：Master Evidence 必须基于当前已绑定/启用 DataSource 的 Metadata。
    // D14-R2：跨 DataSource Master 无法执行 JOIN 时必须降级为 Fact Association ID 的 DirectKey。
    // D14-R5：同一 Dimension 的“展示字段”和“稳定键字段”属于不同物理角色，不能直接互相制造 Ambiguous。
    private const double MasterJoinThreshold = 0.65d;
    private const double DirectKeyThreshold = 0.60d;
    private const double AmbiguityGap = 0.05d;

    private readonly SuperBIContext _context;
    private readonly IMetadataSemanticSearchService _semanticSearchService;

    public DimensionResolutionEvidenceService(SuperBIContext context, IMetadataSemanticSearchService semanticSearchService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _semanticSearchService = semanticSearchService ?? throw new ArgumentNullException(nameof(semanticSearchService));
    }

    public async Task<DimensionResolutionEvidence?> ResolveAsync(long factTableId, long factDataSourceId, string dimensionSemanticText, long preferredColumnId, CancellationToken cancellationToken = default)
    {
        if (factTableId <= 0 || factDataSourceId <= 0 || string.IsNullOrWhiteSpace(dimensionSemanticText)) return null;

        var fact = await _context.MetadataTables
            .Include(x => x.Columns)
            .Include(x => x.DataSource)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == factTableId && x.DataSourceId == factDataSourceId, cancellationToken);

        // D14-R1：未绑定/已禁用 DataSource 的 Metadata 不属于当前 Snapshot。
        if (fact?.Columns is null || fact.DataSource?.Enabled == false) return null;

        var factColumnIds = fact.Columns.Select(x => x.Id).ToHashSet();
        // D8: Increase topK from 20 to 50 — global semantic search may not return
        // fact table columns in top-20 when many other tables share the same dimension.
        var factSearch = await _semanticSearchService.SearchAsync(dimensionSemanticText, 50);

        var activeDataSourceIds = await _context.DataSources
            .Where(x => x.TenantId == fact.TenantId && x.Enabled != false)
            .Select(x => x.Id)
            .ToHashSetAsync(cancellationToken);

        // D14：以当前 Metadata Snapshot 的 Column.Id 作为事实表物理边界。
        var factCandidates = factSearch
            .Where(x => x.IsSemanticVector && x.Column is not null && factColumnIds.Contains(x.Column.Id))
            .Where(x => HasDimensionEvidence(x, dimensionSemanticText))
            .GroupBy(x => x.Column!.Id)
            .Select(g => g.OrderByDescending(x => x.Score).First())
            .OrderByDescending(x => ScoreFactCandidate(x, dimensionSemanticText))
            .ToList();

        if (factCandidates.Count == 0)
        {
            // D8: Global semantic search may not return fact table columns in top-K
            // when many other tables share the same dimension semantic text.
            // Scan the fact table's loaded columns directly for dimension evidence.
            factCandidates = ScanFactTableColumns(fact, dimensionSemanticText, factSearch);
        }

        if (factCandidates.Count == 0)
        {
            // D-CrossTable：事实表内未找到维度列，尝试通过 FK 推断在父表中解析。
            var crossTable = await ResolveCrossTableDimensionAsync(fact, dimensionSemanticText, factSearch, activeDataSourceIds, cancellationToken);
            if (crossTable is not null) return crossTable;
            return null;
        }

        // D14-R5：事实表 Dimension Candidate 先按物理角色分层：
        // 1. ForeignKey/PrimaryKey：用于 MasterJoin 的事实侧连接键（仅 _id 后缀）；
        // 2. 非 ForeignKey（_code/_name 等）：仅作为 DirectKey 的候选展示/分组字段。
        // 用 LooksLikeForeignKey 替代 LooksLikeIdentifier，避免 material_code 等业务编码
        // 与 material_id 产生虚假歧义。
        var keyCandidates = factCandidates
            .Where(x => x.Column is not null && (x.Column.IsPrimaryKey == true || LooksLikeForeignKey(x.Column.ColumnName)))
            .ToList();
        var displayCandidates = factCandidates
            .Where(x => x.Column is not null && x.Column.IsPrimaryKey != true && !LooksLikeForeignKey(x.Column.ColumnName))
            .ToList();

        var bestKey = keyCandidates.FirstOrDefault();
        var secondKey = keyCandidates.Skip(1).FirstOrDefault();
        var bestKeyScore = bestKey is null ? 0d : ScoreFactCandidate(bestKey, dimensionSemanticText);
        var secondKeyScore = secondKey is null ? 0d : ScoreFactCandidate(secondKey, dimensionSemanticText);
        var keyAmbiguous = secondKey is not null && bestKeyScore - secondKeyScore <= AmbiguityGap;

        var bestDisplay = displayCandidates.FirstOrDefault();
        var secondDisplay = displayCandidates.Skip(1).FirstOrDefault();
        var bestDisplayScore = bestDisplay is null ? 0d : ScoreFactCandidate(bestDisplay, dimensionSemanticText);
        var secondDisplayScore = secondDisplay is null ? 0d : ScoreFactCandidate(secondDisplay, dimensionSemanticText);
        var displayAmbiguous = secondDisplay is not null && bestDisplayScore - secondDisplayScore <= AmbiguityGap;

        // D14-R5：只有存在稳定事实侧 Key Candidate 时才尝试 Master Evidence。
        // Master 候选同时对每个事实侧 Key 计算，避免先任意选一个 Key 再造成错误竞争。
        // 复用 L47 的 factSearch 结果，避免重复向量搜索（M8 修复）。
        var masterSearch = keyCandidates.Count == 0
            ? new List<MetadataSemanticSearchResult>()
            : factSearch;

        var masterCandidates = masterSearch
            .Where(x => x.IsSemanticVector && x.Table is not null && x.Column is not null)
            .Where(x => x.Table!.Id != fact.Id && x.Table.TenantId == fact.TenantId)
            .Where(x => activeDataSourceIds.Contains(x.Table!.DataSourceId))
            .Where(x => x.Column!.IsPrimaryKey == true)
            .Where(x => HasDimensionEvidence(x, dimensionSemanticText))
            .GroupBy(x => $"{x.Table!.Id}:{x.Column!.Id}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.Score).First())
            .Select(x =>
            {
                var factKey = keyCandidates
                    .Select(k => new { Candidate = k, Score = ScoreMasterCandidate(x, k.Column!, dimensionSemanticText) })
                    .OrderByDescending(k => k.Score)
                    .First();
                return new { Candidate = x, FactKey = factKey.Candidate, Score = factKey.Score };
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        var bestMaster = masterCandidates.FirstOrDefault();
        var secondMaster = masterCandidates.Skip(1).FirstOrDefault();
        var masterAmbiguous = secondMaster is not null && bestMaster!.Score - secondMaster.Score <= AmbiguityGap;

        // D8: When master is ambiguous but the fact table has a stable, high-scoring
        // Association ID (FK), fall back to DirectKey instead of blocking.
        // The original D14-R5 rule blocked to prevent "secret downgrade to DirectKey",
        // but this is too strict when the fact key is clearly the dimension's FK.
        if (bestMaster is not null && masterAmbiguous)
        {
            if (bestKey is not null && !keyAmbiguous && bestKeyScore >= DirectKeyThreshold)
            {
                return new DimensionResolutionEvidence
                {
                    ResolutionType = "DirectKey",
                    ExecutionCapability = "Executable",
                    FactTableId = fact.Id,
                    FactDataSourceId = fact.DataSourceId,
                    FactKeyColumnId = bestKey.Column!.Id,
                    FactTable = fact.TableName ?? string.Empty,
                    FactKeyColumn = bestKey.Column.ColumnName ?? string.Empty,
                    Score = bestKeyScore,
                    Reason = "Master Evidence 存在歧义，但事实表存在稳定的 Association ID，降级为 DirectKey 汇总。"
                };
            }

            return new DimensionResolutionEvidence
            {
                ResolutionType = "Ambiguous",
                ExecutionCapability = "NotExecutable",
                FactTableId = fact.Id,
                FactDataSourceId = fact.DataSourceId,
                FactKeyColumnId = bestMaster.FactKey.Column!.Id,
                FactTable = fact.TableName ?? string.Empty,
                FactKeyColumn = bestMaster.FactKey.Column.ColumnName ?? string.Empty,
                Score = bestMaster.Score,
                Reason = "当前 Metadata Snapshot 存在多个竞争 Master Evidence，且事实表无稳定 Association ID 可降级。"
            };
        }

        if (bestMaster is not null && bestMaster.Score >= MasterJoinThreshold)
        {
            var master = bestMaster.Candidate;
            var factKey = bestMaster.FactKey;
            var sameDataSource = master.Table!.DataSourceId == fact.DataSourceId;
            if (sameDataSource)
            {
                var label = await FindLabelColumnAsync(master.Table.Id, master.Column!.Id, dimensionSemanticText, cancellationToken);
                return new DimensionResolutionEvidence
                {
                    ResolutionType = "MasterJoin",
                    ExecutionCapability = "Executable",
                    FactTableId = fact.Id,
                    FactDataSourceId = fact.DataSourceId,
                    FactKeyColumnId = factKey.Column!.Id,
                    FactTable = fact.TableName ?? string.Empty,
                    FactKeyColumn = factKey.Column.ColumnName ?? string.Empty,
                    MasterTableId = master.Table.Id,
                    MasterDataSourceId = master.Table.DataSourceId,
                    MasterTable = master.Table.TableName,
                    MasterKeyColumnId = master.Column.Id,
                    MasterKeyColumn = master.Column.ColumnName,
                    MasterLabelColumnId = label?.Id,
                    MasterLabelColumn = label?.ColumnName,
                    Score = bestMaster.Score,
                    Reason = "当前已绑定 Metadata Snapshot 存在稳定的同 DataSource Master Key Semantic Evidence，采用 MasterJoin。"
                };
            }

            // D14 Multi-Database Contract：Master 存在但跨 DataSource 时，当前 Runtime 不执行跨源 JOIN。
            // 必须保留事实表 Association ID，降级为 DirectKey；绝不能把该场景标记为 NotResolved。
            if (bestKey is not null && !keyAmbiguous && bestKeyScore >= DirectKeyThreshold)
            {
                return new DimensionResolutionEvidence
                {
                    ResolutionType = "DirectKey",
                    ExecutionCapability = "Executable",
                    FactTableId = fact.Id,
                    FactDataSourceId = fact.DataSourceId,
                    FactKeyColumnId = bestKey.Column!.Id,
                    FactTable = fact.TableName ?? string.Empty,
                    FactKeyColumn = bestKey.Column.ColumnName ?? string.Empty,
                    Score = bestKeyScore,
                    Reason = "当前 Metadata Snapshot 存在跨 DataSource Master，但当前 Runtime 不执行跨源 JOIN；采用事实表 Association ID 的 DirectKey 汇总。"
                };
            }

            return new DimensionResolutionEvidence
            {
                ResolutionType = "NotResolved",
                ExecutionCapability = "NotExecutable",
                FactTableId = fact.Id,
                FactDataSourceId = fact.DataSourceId,
                FactKeyColumnId = bestMaster.FactKey.Column!.Id,
                FactTable = fact.TableName ?? string.Empty,
                FactKeyColumn = factKey.Column!.ColumnName ?? string.Empty,
                Score = bestMaster.Score,
                Reason = "发现跨 DataSource Master，但事实表没有稳定 Association ID，无法安全降级为 DirectKey。"
            };
        }

        // D14 Multi-Database / Optional Master Contract：没有 Master 时，只允许使用事实表稳定 Association ID。
        // Display Field 不得冒充 DirectKey；它只能作为 MasterJoin 的标签/展示证据。
        if (bestKey is not null)
        {
            if (keyAmbiguous)
            {
                return new DimensionResolutionEvidence
                {
                    ResolutionType = "Ambiguous",
                    ExecutionCapability = "NotExecutable",
                    FactTableId = fact.Id,
                    FactDataSourceId = fact.DataSourceId,
                    FactKeyColumnId = bestKey.Column!.Id,
                    FactTable = fact.TableName ?? string.Empty,
                    FactKeyColumn = bestKey.Column.ColumnName ?? string.Empty,
                    Score = bestKeyScore,
                    Reason = "当前事实表存在多个分差不足的稳定 Dimension Association ID 候选，禁止猜测。"
                };
            }

            if (bestKeyScore >= DirectKeyThreshold)
            {
                return new DimensionResolutionEvidence
                {
                    ResolutionType = "DirectKey",
                    ExecutionCapability = "Executable",
                    FactTableId = fact.Id,
                    FactDataSourceId = fact.DataSourceId,
                    FactKeyColumnId = bestKey.Column!.Id,
                    FactTable = fact.TableName ?? string.Empty,
                    FactKeyColumn = bestKey.Column.ColumnName ?? string.Empty,
                    Score = bestKeyScore,
                    Reason = "当前 Metadata Snapshot 未形成 Master Evidence；使用事实表稳定 Association ID 作为 DirectKey。"
                };
            }
        }

        var fallback = bestDisplay ?? bestKey;
        if (fallback is null) return null;
        var fallbackScore = bestDisplay is not null ? bestDisplayScore : bestKeyScore;
        return new DimensionResolutionEvidence
        {
            ResolutionType = "NotResolved",
            ExecutionCapability = "NotExecutable",
            FactTableId = fact.Id,
            FactDataSourceId = fact.DataSourceId,
            FactKeyColumnId = fallback.Column!.Id,
            FactTable = fact.TableName ?? string.Empty,
            FactKeyColumn = fallback.Column.ColumnName ?? string.Empty,
            Score = fallbackScore,
            Reason = "当前 Metadata Snapshot 没有足够稳定的 Dimension 物理证据。"
        };
    }

    private async Task<MetadataColumn?> FindLabelColumnAsync(long tableId, long keyColumnId, string dimensionSemanticText, CancellationToken cancellationToken)
    {
        var table = await _context.MetadataTables.Include(x => x.Columns).AsNoTracking().FirstOrDefaultAsync(x => x.Id == tableId, cancellationToken);
        if (table?.Columns is null) return null;
        var search = await _semanticSearchService.SearchAsync(dimensionSemanticText, 10);
        var searchedIds = search.Where(x => x.Table?.Id == tableId && x.Column is not null && x.Column.Id != keyColumnId).OrderByDescending(x => x.Score).Select(x => x.Column!.Id).ToHashSet();
        return table.Columns.Where(x => x.Id != keyColumnId).OrderByDescending(x => searchedIds.Contains(x.Id)).ThenBy(x => LooksLikeIdentifier(x.ColumnName)).FirstOrDefault();
    }

    private static double ScoreFactCandidate(MetadataSemanticSearchResult candidate, string semanticText)
    {
        var score = Math.Clamp(candidate.Score, 0d, 1d) * 0.65d;
        var column = candidate.Column; if (column is null) return 0d;
        var normalizedSemantic = Normalize(semanticText);
        var text = Normalize(string.Join(" ", column.ColumnName, column.ColumnComment, column.SearchText, candidate.Semantic?.BusinessMeaning, candidate.Semantic?.Keywords, candidate.Semantic?.Synonyms, candidate.Semantic?.ExampleQuestions));
        if (Contains(text, normalizedSemantic)) score += 0.20d;
        if (LooksLikeIdentifier(column.ColumnName)) score += 0.10d;
        // Key candidates ARE primary keys or identifiers by definition (see L64-66 filter).
        // Penalizing them for being what we explicitly selected them for is counterproductive.
        // Display candidates (L67-68) already exclude primary keys, so this bonus only affects key candidates.
        if (column.IsPrimaryKey == true) score += 0.10d;
        return Math.Clamp(score, 0d, 1d);
    }

    private static double ScoreMasterCandidate(MetadataSemanticSearchResult candidate, MetadataColumn factKey, string dimensionSemanticText)
    {
        if (candidate.Table is null || candidate.Column is null) return 0d;
        var score = Math.Clamp(candidate.Score, 0d, 1d) * 0.55d;
        var semantic = Normalize(dimensionSemanticText);
        var tableText = Normalize(string.Join(" ", candidate.Table.TableName, candidate.Table.TableComment, candidate.Table.SearchText, candidate.Table.BusinessDomain));
        var keyText = Normalize(string.Join(" ", candidate.Column.ColumnName, candidate.Column.ColumnComment, candidate.Column.SearchText, candidate.Semantic?.BusinessMeaning, candidate.Semantic?.Keywords, candidate.Semantic?.Synonyms));
        if (Contains(tableText, semantic)) score += 0.20d;
        if (Contains(keyText, semantic)) score += 0.10d;
        if (candidate.Column.IsPrimaryKey == true) score += 0.10d;
        if (SameIdentifier(candidate.Column.ColumnName, factKey.ColumnName)) score += 0.10d;
        return Math.Clamp(score, 0d, 1d);
    }

    private static bool HasDimensionEvidence(MetadataSemanticSearchResult candidate, string semanticText)
    {
        if (candidate.Table is null || candidate.Column is null) return false;
        var normalized = Normalize(semanticText); if (normalized.Length == 0) return false;
        var values = new[] { candidate.Table.TableName, candidate.Table.TableComment, candidate.Table.SearchText, candidate.Table.BusinessDomain, candidate.Column.ColumnName, candidate.Column.ColumnComment, candidate.Column.SearchText, candidate.Semantic?.BusinessMeaning, candidate.Semantic?.Keywords, candidate.Semantic?.Synonyms, candidate.Semantic?.ExampleQuestions, candidate.Semantic?.SearchText };
        return values.Any(x => !string.IsNullOrWhiteSpace(x) && Contains(Normalize(x), normalized));
    }

    private static bool SameIdentifier(string? left, string? right) => NormalizeIdentifier(left) == NormalizeIdentifier(right) && NormalizeIdentifier(left).Length > 0;

    /// <summary>
    /// 仅匹配外键列（_id 后缀），用于 keyCandidates 过滤。
    /// 不匹配 _code/_key/_no，因为业务编码不是维度关联键。
    /// </summary>
    private static bool LooksLikeForeignKey(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var lower = name.ToLowerInvariant().Trim();
        if (lower is "id") return true; // PK
        if (lower.EndsWith("_id")) return true;
        if (name.Length >= 4 && name.EndsWith("Id", StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>
    /// D8: 当全局语义搜索 top-K 未返回事实表列时，直接扫描事实表已加载的列元数据。
    /// 通过文本匹配（列名、列注释、搜索文本）查找维度证据，为未出现在搜索结果中的列
    /// 创建合成 MetadataSemanticSearchResult。
    /// </summary>
    private List<MetadataSemanticSearchResult> ScanFactTableColumns(
        MetadataTable fact,
        string dimensionSemanticText,
        List<MetadataSemanticSearchResult> factSearch)
    {
        var normalized = Normalize(dimensionSemanticText);
        if (normalized.Length == 0) return new List<MetadataSemanticSearchResult>();

        // Build a lookup from search results by column ID (for columns that ARE in search results
        // but were filtered out by HasDimensionEvidence — we still want their vector scores).
        var searchByColumnId = factSearch
            .Where(x => x.Column is not null)
            .GroupBy(x => x.Column!.Id)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Score).First());

        var results = new List<MetadataSemanticSearchResult>();

        foreach (var column in fact.Columns ?? new List<MetadataColumn>())
        {
            // Check column metadata for dimension text
            var columnText = Normalize(string.Join(" ", column.ColumnName, column.ColumnComment, column.SearchText));
            if (!Contains(columnText, normalized)) continue;

            // If we have a search result for this column, use it (preserves vector score)
            if (searchByColumnId.TryGetValue(column.Id, out var searchResult))
            {
                results.Add(searchResult);
            }
            else
            {
                // Create a synthetic result with heuristic score for text-match-only
                results.Add(new MetadataSemanticSearchResult
                {
                    VectorType = "semantic",
                    VectorId = string.Empty,
                    Table = fact,
                    Column = column,
                    Semantic = null,
                    Score = 0.5d
                });
            }
        }

        return results
            .GroupBy(x => x.Column!.Id)
            .Select(g => g.OrderByDescending(x => x.Score).First())
            .OrderByDescending(x => ScoreFactCandidate(x, dimensionSemanticText))
            .ToList();
    }

    /// <summary>
    /// 跨表维度解析：事实表内未找到维度列时，通过 FK 推断在父表中解析。
    /// 例：wms_storage_receipt_info.storage_receipt_id → wms_storage_receipt.es_supplier_code
    /// </summary>
    private async Task<DimensionResolutionEvidence?> ResolveCrossTableDimensionAsync(
        MetadataTable fact,
        string dimensionSemanticText,
        List<MetadataSemanticSearchResult> factSearch,
        HashSet<long> activeDataSourceIds,
        CancellationToken cancellationToken)
    {
        // 1. 在 factSearch 中找有维度证据的表（非事实表）
        var dimensionTables = factSearch
            .Where(x => x.IsSemanticVector && x.Table is not null && x.Column is not null)
            .Where(x => x.Table!.Id != fact.Id && x.Table.TenantId == fact.TenantId)
            .Where(x => activeDataSourceIds.Contains(x.Table!.DataSourceId))
            .Where(x => HasDimensionEvidence(x, dimensionSemanticText))
            .GroupBy(x => x.Table!.Id)
            .Select(g => new { Table = g.First().Table!, Columns = g.OrderByDescending(x => x.Score).ToList() })
            .OrderByDescending(dt => dt.Columns.First().Score)
            .ToList();

        if (dimensionTables.Count == 0) return null;

        var factColumns = fact.Columns ?? new List<MetadataColumn>();

        foreach (var dt in dimensionTables)
        {
            // 2. 获取父表 PK
            var masterTable = await _context.MetadataTables
                .Include(x => x.Columns)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == dt.Table.Id, cancellationToken);
            if (masterTable?.Columns is null) continue;

            var pkColumn = masterTable.Columns.FirstOrDefault(c => c.IsPrimaryKey == true);
            if (pkColumn is null) continue;

            // 3. 在事实表中找指向该父表的 FK 列
            var fkColumn = factColumns.FirstOrDefault(c => IsForeignKeyTo(c.ColumnName, masterTable.TableName));
            if (fkColumn is null) continue;

            // 4. 找标签列（父表中有维度证据的列）
            var labelColumn = dt.Columns.FirstOrDefault();

            return new DimensionResolutionEvidence
            {
                ResolutionType = "MasterJoin",
                ExecutionCapability = "Executable",
                FactTableId = fact.Id,
                FactDataSourceId = fact.DataSourceId,
                FactKeyColumnId = fkColumn.Id,
                FactTable = fact.TableName ?? string.Empty,
                FactKeyColumn = fkColumn.ColumnName ?? string.Empty,
                MasterTableId = masterTable.Id,
                MasterDataSourceId = masterTable.DataSourceId,
                MasterTable = masterTable.TableName,
                MasterKeyColumnId = pkColumn.Id,
                MasterKeyColumn = pkColumn.ColumnName,
                MasterLabelColumnId = labelColumn?.Column?.Id,
                MasterLabelColumn = labelColumn?.Column?.ColumnName,
                Score = Math.Clamp(labelColumn?.Score ?? 0d, 0d, 1d),
                Reason = "维度语义在事实表内未找到，通过 FK 推断在父表中完成 MasterJoin 解析。"
            };
        }

        return null;
    }

    /// <summary>
    /// FK 推断：检查列名是否是指向目标表的外键。
    /// storage_receipt_id → wms_storage_receipt (去掉 _id 和 wms_ 前缀后匹配)
    /// </summary>
    private static bool IsForeignKeyTo(string? fkColumnName, string? masterTableName)
    {
        if (string.IsNullOrWhiteSpace(fkColumnName) || string.IsNullOrWhiteSpace(masterTableName)) return false;
        var fk = fkColumnName.ToLowerInvariant().Trim();
        var master = masterTableName.ToLowerInvariant().Trim();

        // 去掉 wms_ 等公共前缀
        if (master.StartsWith("wms_")) master = master[4..];
        if (master.StartsWith("t_")) master = master[2..];

        // 去掉 _id 后缀
        if (fk.EndsWith("_id")) fk = fk[..^3];
        else if (fk.Length > 2 && fk.EndsWith("id")) fk = fk[..^2];
        else return false; // 不是 _id 后缀，不是 FK

        return fk == master || fk.Contains(master) || master.Contains(fk);
    }

    private static bool LooksLikeIdentifier(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var lower = name.ToLowerInvariant().Trim();
        // Exact short matches
        if (lower is "id" or "code" or "key" or "no") return true;
        // snake_case: _id, _code, _key, _no — word boundary via underscore
        if (lower.EndsWith("_id") || lower.EndsWith("_code") || lower.EndsWith("_key") || lower.EndsWith("_no")) return true;
        // camelCase: XxxId, XxxCode, XxxKey, XxxNo (require at least 2 chars before suffix)
        if (name.Length >= 4)
        {
            if (name.EndsWith("Id", StringComparison.Ordinal) ||
                name.EndsWith("Code", StringComparison.Ordinal) ||
                name.EndsWith("Key", StringComparison.Ordinal) ||
                name.EndsWith("No", StringComparison.Ordinal)) return true;
        }
        return false;
    }
    private static string NormalizeIdentifier(string? value) => string.Concat((value ?? string.Empty).Where(char.IsLetterOrDigit)).ToLowerInvariant();
    private static string Normalize(string? value) => (value ?? string.Empty).Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    private static bool Contains(string text, string value) => value.Length > 0 && text.Contains(value, StringComparison.OrdinalIgnoreCase);
}
