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
    // D14-R2：DirectKey 不应因二次加权把已有明确语义的事实表键错误判为 NotResolved。
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
        var factSearch = await _semanticSearchService.SearchAsync(dimensionSemanticText, 20);

        // D14：以当前 Metadata Snapshot 的 Column.Id 作为事实表物理边界。
        var factCandidates = factSearch
            .Where(x => x.IsSemanticVector && x.Column is not null && factColumnIds.Contains(x.Column.Id))
            .Where(x => HasDimensionEvidence(x, dimensionSemanticText))
            .GroupBy(x => x.Column!.Id)
            .Select(g => g.OrderByDescending(x => x.Score).First())
            .OrderByDescending(x => ScoreFactCandidate(x, dimensionSemanticText))
            .ToList();

        if (factCandidates.Count == 0) return null;

        // D14-R5：事实表 Dimension Candidate 先按物理角色分层：
        // 1. Identifier/PrimaryKey：用于 MasterJoin 的事实侧连接键；
        // 2. 非 Identifier：在没有 Master Evidence 时作为 DirectKey 的直接展示/分组字段。
        // 两类候选不能仅因为分差接近就互相判定为 Ambiguous。
        var keyCandidates = factCandidates
            .Where(x => x.Column is not null && (x.Column.IsPrimaryKey == true || LooksLikeIdentifier(x.Column.ColumnName)))
            .ToList();
        var displayCandidates = factCandidates
            .Where(x => x.Column is not null && x.Column.IsPrimaryKey != true && !LooksLikeIdentifier(x.Column.ColumnName))
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

        var activeDataSourceIds = await _context.DataSources
            .Where(x => x.TenantId == fact.TenantId && x.Enabled != false)
            .Select(x => x.Id)
            .ToHashSetAsync(cancellationToken);

		// D14-R5：只有存在稳定事实侧 Key Candidate 时才尝试 Master Evidence。
		// Master 候选同时对每个事实侧 Key 计算，避免先任意选一个 Key 再造成错误竞争。
		var masterSearch = keyCandidates.Count == 0
	        ? new List<MetadataSemanticSearchResult>()
	        : await _semanticSearchService.SearchAsync(dimensionSemanticText, 20);

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

        // D14-R5：Metadata 中已经出现 Master Evidence，但 Evidence 竞争时必须 BLOCK，不能偷偷降级 DirectKey。
        if (bestMaster is not null && masterAmbiguous)
        {
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
                Reason = "当前 Metadata Snapshot 存在多个竞争 Master Evidence，禁止猜测。"
            };
        }

        if (bestMaster is not null && bestMaster.Score >= MasterJoinThreshold)
        {
            var master = bestMaster.Candidate;
            var factKey = bestMaster.FactKey;
            var sameDataSource = master.Table!.DataSourceId == fact.DataSourceId;
            var label = await FindLabelColumnAsync(master.Table.Id, master.Column!.Id, dimensionSemanticText, cancellationToken);
            return new DimensionResolutionEvidence
            {
                ResolutionType = "MasterJoin",
                ExecutionCapability = sameDataSource ? "Executable" : "NotExecutable",
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
                Reason = sameDataSource
                    ? "当前已绑定 Metadata Snapshot 存在稳定的 Master Key Semantic Evidence。"
                    : "已绑定 Metadata Snapshot 发现跨 DataSource Master Evidence；当前 QueryPlan/SQL Runtime 不执行跨 DataSource Join。"
            };
        }

        // 当前 Snapshot 没有 Master Evidence：DirectKey 只在同一物理角色内部判断稳定性。
        if (bestDisplay is not null)
        {
            if (displayAmbiguous)
            {
                return new DimensionResolutionEvidence
                {
                    ResolutionType = "Ambiguous",
                    ExecutionCapability = "NotExecutable",
                    FactTableId = fact.Id,
                    FactDataSourceId = fact.DataSourceId,
                    FactKeyColumnId = bestDisplay.Column!.Id,
                    FactTable = fact.TableName ?? string.Empty,
                    FactKeyColumn = bestDisplay.Column.ColumnName ?? string.Empty,
                    Score = bestDisplayScore,
                    Reason = "当前事实表存在多个竞争的直接 Dimension 展示字段，禁止猜测。"
                };
            }

            if (bestDisplayScore >= DirectKeyThreshold)
            {
                return new DimensionResolutionEvidence
                {
                    ResolutionType = "DirectKey",
                    ExecutionCapability = "Executable",
                    FactTableId = fact.Id,
                    FactDataSourceId = fact.DataSourceId,
                    FactKeyColumnId = bestDisplay.Column!.Id,
                    FactTable = fact.TableName ?? string.Empty,
                    FactKeyColumn = bestDisplay.Column.ColumnName ?? string.Empty,
                    Score = bestDisplayScore,
                    Reason = "当前已绑定 Metadata Snapshot 未形成 Master Evidence；事实表存在稳定的直接 Dimension 展示字段，因此采用 DirectKey。"
                };
            }
        }

        // 没有直接展示字段时才退回稳定 Identifier/PrimaryKey。
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
                    Reason = "当前事实表存在多个分差不足的稳定 Dimension Key 候选，禁止猜测。"
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
                    Reason = "当前已绑定 Metadata Snapshot 未形成 Master Evidence；事实表存在稳定 Dimension Identifier，因此采用 DirectKey。"
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
        if (column.IsPrimaryKey == true) score -= 0.20d;
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
    private static bool LooksLikeIdentifier(string? name)
    {
        var n = NormalizeIdentifier(name);
        return n.EndsWith("id", StringComparison.OrdinalIgnoreCase) || n.EndsWith("code", StringComparison.OrdinalIgnoreCase) || n.EndsWith("key", StringComparison.OrdinalIgnoreCase) || n.EndsWith("no", StringComparison.OrdinalIgnoreCase);
    }
    private static string NormalizeIdentifier(string? value) => string.Concat((value ?? string.Empty).Where(char.IsLetterOrDigit)).ToLowerInvariant();
    private static string Normalize(string? value) => (value ?? string.Empty).Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    private static bool Contains(string text, string value) => value.Length > 0 && text.Contains(value, StringComparison.OrdinalIgnoreCase);
}
