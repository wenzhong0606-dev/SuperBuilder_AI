using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// Phase 2.7 D14：从当前 Metadata Snapshot 提供 Dimension 物理绑定证据。
/// 不生成 SQL，不写入 QueryPlan.Joins，不进行业务字段硬编码。
/// </summary>
public sealed class DimensionResolutionEvidenceService : IDimensionResolutionEvidenceService
{
    private readonly SuperBIContext _context;
    private readonly IMetadataSemanticSearchService _semanticSearchService;

    public DimensionResolutionEvidenceService(
        SuperBIContext context,
        IMetadataSemanticSearchService semanticSearchService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _semanticSearchService = semanticSearchService ?? throw new ArgumentNullException(nameof(semanticSearchService));
    }

    public async Task<DimensionResolutionEvidence?> ResolveAsync(
        long factTableId,
        long factDataSourceId,
        string dimensionSemanticText,
        long preferredColumnId,
        CancellationToken cancellationToken = default)
    {
        if (factTableId <= 0 || factDataSourceId <= 0 || string.IsNullOrWhiteSpace(dimensionSemanticText))
            return null;

        var fact = await _context.MetadataTables
            .Include(x => x.Columns)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == factTableId && x.DataSourceId == factDataSourceId, cancellationToken);

        if (fact?.Columns is null)
            return null;

        var preferred = fact.Columns.FirstOrDefault(x => x.Id == preferredColumnId);
        if (preferred is null)
            return null;

        // DirectKey 只能建立在已经解析出的事实表候选字段上；不从字段名硬编码推导 material_id/supplier_id。
        var masterCandidates = await _semanticSearchService.SearchAsync(dimensionSemanticText, 20);
        var candidates = masterCandidates
            .Where(x => x.IsSemanticVector && x.Table is not null && x.Column is not null)
            .Where(x => x.Column!.IsPrimaryKey == true)
            .Where(x => x.Table!.Id != fact.Id)
            .Where(x => HasDimensionEvidence(x, dimensionSemanticText))
            .GroupBy(x => $"{x.Table!.Id}:{x.Column!.Id}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.Score).First())
            .OrderByDescending(x => x.Score)
            .ToList();

        var master = candidates.FirstOrDefault();
        if (master is not null)
        {
            var sameDataSource = master.Table!.DataSourceId == fact.DataSourceId;
            var label = await FindLabelColumnAsync(master.Table.Id, master.Column!.Id, dimensionSemanticText, cancellationToken);

            return new DimensionResolutionEvidence
            {
                ResolutionType = "MasterJoin",
                ExecutionCapability = sameDataSource ? "Executable" : "NotExecutable",
                FactTableId = fact.Id,
                FactDataSourceId = fact.DataSourceId,
                FactKeyColumnId = preferred.Id,
                FactTable = fact.TableName ?? string.Empty,
                FactKeyColumn = preferred.ColumnName ?? string.Empty,
                MasterTableId = master.Table.Id,
                MasterDataSourceId = master.Table.DataSourceId,
                MasterTable = master.Table.TableName,
                MasterKeyColumnId = master.Column.Id,
                MasterKeyColumn = master.Column.ColumnName,
                MasterLabelColumnId = label?.Id,
                MasterLabelColumn = label?.ColumnName,
                Score = master.Score,
                Reason = sameDataSource
                    ? "当前 Metadata Snapshot 存在语义稳定且目标字段为主键的 Master Evidence。"
                    : "当前 Metadata Snapshot 存在跨 DataSource Master Evidence，但当前 SQL Runtime 未允许跨 DataSource 执行。"
            };
        }

        // 没有可执行 Master Evidence 时，保留当前事实表候选作为 DirectKey。
        // 这里不要求 preferred 字段是主键：外键/业务键可以作为事实表上的稳定维度 Key。
        return new DimensionResolutionEvidence
        {
            ResolutionType = "DirectKey",
            ExecutionCapability = "Executable",
            FactTableId = fact.Id,
            FactDataSourceId = fact.DataSourceId,
            FactKeyColumnId = preferred.Id,
            FactTable = fact.TableName ?? string.Empty,
            FactKeyColumn = preferred.ColumnName ?? string.Empty,
            Score = 1d,
            Reason = "当前 Metadata Snapshot 未发现稳定 Master Evidence，保留已解析的事实表 Dimension Key，采用 DirectKey。"
        };
    }

    private async Task<SuperBuilder_AI.Models.Metadata.MetadataColumn?> FindLabelColumnAsync(
        long tableId,
        long keyColumnId,
        string dimensionSemanticText,
        CancellationToken cancellationToken)
    {
        var table = await _context.MetadataTables
            .Include(x => x.Columns)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tableId, cancellationToken);
        if (table?.Columns is null)
            return null;

        var search = await _semanticSearchService.SearchAsync(dimensionSemanticText, 10);
        var tableIds = search
            .Where(x => x.Table?.Id == tableId && x.Column is not null && x.Column.Id != keyColumnId)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Column!.Id)
            .ToHashSet();

        return table.Columns
            .Where(x => x.Id != keyColumnId)
            .OrderByDescending(x => tableIds.Contains(x.Id))
            .ThenBy(x => x.IsPrimaryKey == true)
            .FirstOrDefault();
    }

    private static bool HasDimensionEvidence(MetadataSemanticSearchResult candidate, string semanticText)
    {
        var normalized = Normalize(semanticText);
        if (normalized.Length == 0 || candidate.Table is null || candidate.Column is null)
            return false;

        var values = new[]
        {
            candidate.Table.TableName,
            candidate.Table.TableComment,
            candidate.Table.SearchText,
            candidate.Column.ColumnName,
            candidate.Column.ColumnComment,
            candidate.Semantic?.BusinessMeaning,
            candidate.Semantic?.Keywords,
            candidate.Semantic?.Synonyms,
            candidate.Semantic?.SearchText,
            candidate.Semantic?.ExampleQuestions
        };

        return values.Any(x => !string.IsNullOrWhiteSpace(x) && Normalize(x).Contains(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string value) => value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
}
