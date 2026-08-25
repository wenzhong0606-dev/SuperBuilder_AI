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
    private const double MasterJoinThreshold = 0.80d;
    private const double DirectKeyThreshold = 0.65d;
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
        var fact = await _context.MetadataTables.Include(x => x.Columns).AsNoTracking().FirstOrDefaultAsync(x => x.Id == factTableId && x.DataSourceId == factDataSourceId, cancellationToken);
        if (fact?.Columns is null) return null;

        var factSearch = await _semanticSearchService.SearchAsync(dimensionSemanticText, 20);
        var factCandidates = factSearch.Where(x => x.IsSemanticVector && x.Table?.Id == fact.Id && x.Column is not null).Where(x => HasDimensionEvidence(x, dimensionSemanticText)).GroupBy(x => x.Column!.Id).Select(g => g.OrderByDescending(x => x.Score).First()).OrderByDescending(x => ScoreFactCandidate(x, dimensionSemanticText)).ToList();
        if (factCandidates.Count == 0) return null;

        var bestFact = preferredColumnId > 0 ? factCandidates.FirstOrDefault(x => x.Column!.Id == preferredColumnId) ?? factCandidates[0] : factCandidates[0];
        var secondFact = factCandidates.Where(x => x.Column!.Id != bestFact.Column!.Id).FirstOrDefault();
        var bestFactScore = ScoreFactCandidate(bestFact, dimensionSemanticText);
        var secondFactScore = secondFact is null ? 0d : ScoreFactCandidate(secondFact, dimensionSemanticText);
        if (secondFact is not null && bestFactScore - secondFactScore <= AmbiguityGap)
            return new DimensionResolutionEvidence { ResolutionType = "Ambiguous", ExecutionCapability = "NotExecutable", FactTableId = fact.Id, FactDataSourceId = fact.DataSourceId, FactKeyColumnId = bestFact.Column!.Id, FactTable = fact.TableName ?? string.Empty, FactKeyColumn = bestFact.Column.ColumnName ?? string.Empty, Score = bestFactScore, Reason = "当前事实表存在多个分差不足的 Dimension Key 候选，禁止猜测。" };
        if (bestFactScore < DirectKeyThreshold)
            return new DimensionResolutionEvidence { ResolutionType = "NotResolved", ExecutionCapability = "NotExecutable", FactTableId = fact.Id, FactDataSourceId = fact.DataSourceId, FactKeyColumnId = bestFact.Column!.Id, FactTable = fact.TableName ?? string.Empty, FactKeyColumn = bestFact.Column.ColumnName ?? string.Empty, Score = bestFactScore, Reason = "当前事实表没有足够稳定的 Dimension Key Evidence。" };

        var masterSearch = await _semanticSearchService.SearchAsync(dimensionSemanticText, 20);
        var masterCandidates = masterSearch.Where(x => x.IsSemanticVector && x.Table is not null && x.Column is not null).Where(x => x.Table!.Id != fact.Id && x.Table.TenantId == fact.TenantId).Where(x => x.Column!.IsPrimaryKey == true).Where(x => HasDimensionEvidence(x, dimensionSemanticText)).GroupBy(x => $"{x.Table!.Id}:{x.Column!.Id}", StringComparer.OrdinalIgnoreCase).Select(g => g.OrderByDescending(x => x.Score).First()).Select(x => new { Candidate = x, Score = ScoreMasterCandidate(x, bestFact.Column!, dimensionSemanticText) }).OrderByDescending(x => x.Score).ToList();
        var bestMaster = masterCandidates.FirstOrDefault();
        var secondMaster = masterCandidates.Skip(1).FirstOrDefault();

        if (bestMaster is not null && bestMaster.Score >= MasterJoinThreshold && (secondMaster is null || bestMaster.Score - secondMaster.Score > AmbiguityGap))
        {
            var master = bestMaster.Candidate;
            var sameDataSource = master.Table!.DataSourceId == fact.DataSourceId;
            var label = await FindLabelColumnAsync(master.Table.Id, master.Column!.Id, dimensionSemanticText, cancellationToken);
            return new DimensionResolutionEvidence { ResolutionType = "MasterJoin", ExecutionCapability = sameDataSource ? "Executable" : "NotExecutable", FactTableId = fact.Id, FactDataSourceId = fact.DataSourceId, FactKeyColumnId = bestFact.Column!.Id, FactTable = fact.TableName ?? string.Empty, FactKeyColumn = bestFact.Column.ColumnName ?? string.Empty, MasterTableId = master.Table.Id, MasterDataSourceId = master.Table.DataSourceId, MasterTable = master.Table.TableName, MasterKeyColumnId = master.Column.Id, MasterKeyColumn = master.Column.ColumnName, MasterLabelColumnId = label?.Id, MasterLabelColumn = label?.ColumnName, Score = bestMaster.Score, Reason = sameDataSource ? "当前 Metadata Snapshot 存在稳定的 Master Key Semantic Evidence。" : "发现跨 DataSource Master Evidence，但当前 SQL Runtime 不执行跨 DataSource Join。" };
        }

        return new DimensionResolutionEvidence { ResolutionType = "DirectKey", ExecutionCapability = "Executable", FactTableId = fact.Id, FactDataSourceId = fact.DataSourceId, FactKeyColumnId = bestFact.Column!.Id, FactTable = fact.TableName ?? string.Empty, FactKeyColumn = bestFact.Column.ColumnName ?? string.Empty, Score = bestFactScore, Reason = "当前 Metadata Snapshot 未形成稳定 Master Evidence；事实表存在稳定 Dimension Key，因此采用 DirectKey。" };
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
