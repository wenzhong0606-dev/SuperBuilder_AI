using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.BI;
using SuperBuilder_AI.Services.BI.Evaluation;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M5-12 (GQ-006)：物料等缺独立主表时，DimensionResolutionEvidenceService 须真实解析为 DirectKey
/// （事实表稳定 Association ID = material_id），而非回退 NotResolved。
///
/// 复现根因：事实表 wms_storage_receipt_info 承载 material_id(FK)/material_name(显示)/material_code，
/// 但 material_id 列元数据不含维度词"物料"，旧逻辑在 HasDimensionEvidence 过滤中将其排除 →
/// keyCandidates 为空 → 无主表且无可用关联键 → 落回 NotResolved（即便事实表确有稳定 material_id）。
///
/// 修复后：用命名根对齐从事实表全列中发现稳定 FK/PK Association ID，配合 display 列的存在性证明建立 DirectKey。
/// 双视角验证：
///  - 服务层：直接断言 DimensionResolutionEvidenceService.ResolveAsync 返回 DirectKey(material_id)。
///  - 评估器层：经 SemanticApplicabilityEvaluator 跑 GQ-006，断言 State=Resolved 且维度 DimensionKeyColumn=material_id
///    （修复前评估器兜底会以 material_name 兜底为 DirectKey，修复后以规范关联键 material_id 解析）。
/// 两者在修复前均 RED（证明根因），修复后均 GREEN。
/// </summary>
public class DimensionResolutionEvidenceServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SuperBIContext _ctx;
    private readonly long _factTableId = 1;
    private readonly long _dataSourceId = 1;
    private readonly long _tenantId = 1;

    public DimensionResolutionEvidenceServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Pooling=false");
        _connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(_connection).Options;
        _ctx = new SuperBIContext(options);
        _ctx.Database.EnsureCreated();

        _ctx.Tenants.Add(new Tenant { Id = _tenantId, TenantCode = "e2e", TenantName = "E2E", Enabled = true });
        _ctx.DataSources.Add(new DataSource
        {
            Id = _dataSourceId,
            TenantId = _tenantId,
            Name = "DS",
            NormalizedName = "ds",
            DbType = "POSTGRESQL",
            ConnectionString = "x",
            Enabled = true
        });
        var fact = new MetadataTable { Id = _factTableId, TenantId = _tenantId, DataSourceId = _dataSourceId, TableName = "wms_storage_receipt_info" };
        fact.Columns = new List<MetadataColumn>
        {
            new() { Id = 11, MetadataTableId = fact.Id, ColumnName = "id", IsPrimaryKey = true },
            new() { Id = 12, MetadataTableId = fact.Id, ColumnName = "material_id", ColumnComment = "material foreign key" }, // FK，元数据不含"物料"
            new() { Id = 13, MetadataTableId = fact.Id, ColumnName = "material_name", ColumnComment = "物料名称" },            // display，含"物料"
            new() { Id = 14, MetadataTableId = fact.Id, ColumnName = "material_code", ColumnComment = "物料编码" },            // display，含"物料"
            new() { Id = 15, MetadataTableId = fact.Id, ColumnName = "quantity", ColumnComment = "入库数量" },
        };
        _ctx.MetadataTables.Add(fact);
        _ctx.SaveChanges();
    }

    public void Dispose()
    {
        _ctx.Dispose();
        _connection.Dispose();
    }

    private sealed class FakeSearch : IMetadataSemanticSearchService
    {
        private readonly Dictionary<string, List<MetadataSemanticSearchResult>> _map;
        public FakeSearch(Dictionary<string, List<MetadataSemanticSearchResult>> map) => _map = map;
        public Task<List<MetadataSemanticSearchResult>> SearchAsync(string question, int topK = 10, LocaleContext? locale = null)
            => Task.FromResult(_map.TryGetValue(question, out var list) ? list : new List<MetadataSemanticSearchResult>());
    }

    private static MetadataSemanticSearchResult Semantic(MetadataTable table, MetadataColumn column, double score, string businessMeaning)
        => new()
        {
            VectorType = "semantic",
            VectorId = Guid.NewGuid().ToString(),
            Table = table,
            Column = column,
            Semantic = new MetadataSemantic { BusinessMeaning = businessMeaning },
            Score = score
        };

    private DimensionResolutionEvidenceService BuildService(Dictionary<string, List<MetadataSemanticSearchResult>> map)
        => new(_ctx, new FakeSearch(map));

    private Dictionary<string, List<MetadataSemanticSearchResult>> MaterialSearchMap()
    {
        var fact = _ctx.MetadataTables.Include(t => t.Columns).First(t => t.Id == _factTableId);
        var nameCol = fact.Columns.First(c => c.ColumnName == "material_name");
        var codeCol = fact.Columns.First(c => c.ColumnName == "material_code");
        return new Dictionary<string, List<MetadataSemanticSearchResult>>
        {
            ["物料"] = new()
            {
                Semantic(fact, nameCol, 0.95, "物料名称"),
                Semantic(fact, codeCol, 0.90, "物料编码"),
            }
        };
    }

    [Fact]
    public async Task Material_Without_MasterTable_Resolves_To_DirectKey_On_AssociationId()
    {
        var svc = BuildService(MaterialSearchMap());
        var evidence = await svc.ResolveAsync(_factTableId, _dataSourceId, "物料", 15);

        Assert.NotNull(evidence);
        Assert.Equal("DirectKey", evidence!.ResolutionType);
        Assert.Equal("Executable", evidence.ExecutionCapability);
        Assert.Equal("material_id", evidence.FactKeyColumn);
    }

    [Fact]
    public async Task Material_With_No_Dimension_Evidence_Stays_NotResolved()
    {
        // "供应商" 在事实表中无任何列承载该语义 → 不应猜测为 DirectKey/MasterJoin。
        var svc = BuildService(new Dictionary<string, List<MetadataSemanticSearchResult>>());
        var evidence = await svc.ResolveAsync(_factTableId, _dataSourceId, "供应商", 15);
        Assert.True(evidence is null || evidence.ResolutionType is "NotResolved" or "Ambiguous");
    }

    [Fact]
    public async Task GQ006_Evaluator_Resolves_Material_Dimension_To_DirectKey_On_AssociationId()
    {
        var fact = _ctx.MetadataTables.Include(t => t.Columns).First(t => t.Id == _factTableId);
        var nameCol = fact.Columns.First(c => c.ColumnName == "material_name");
        var codeCol = fact.Columns.First(c => c.ColumnName == "material_code");
        var qtyCol = fact.Columns.First(c => c.ColumnName == "quantity");

        var map = new Dictionary<string, List<MetadataSemanticSearchResult>>
        {
            ["物料"] = new()
            {
                Semantic(fact, nameCol, 0.95, "物料名称"),
                Semantic(fact, codeCol, 0.90, "物料编码"),
            },
            ["入库数量"] = new()
            {
                Semantic(fact, qtyCol, 0.95, "入库数量"),
            }
        };

        var evaluator = new SemanticApplicabilityEvaluator(new FakeSearch(map), new DimensionResolutionEvidenceService(_ctx, new FakeSearch(map)));
        var golden = new GoldenQueryCase
        {
            Id = "GQ-006",
            Question = "查询入库数量最多的前10个物料",
            Expected = new GoldenQueryExpectation
            {
                IntentType = "Ranking",
                Metrics = new List<GoldenMetricExpectation> { new() { SemanticText = "入库数量", Aggregation = QueryAggregation.Sum } },
                Dimensions = new List<GoldenDimensionExpectation> { new() { SemanticText = "物料" } },
                IsAggregate = true,
                IsRanking = true,
                IsAggregateRanking = true,
                Limit = 10
            }
        };

        var result = await evaluator.EvaluateAsync(golden);

        Assert.Equal("Resolved", result.State);
        var dim = result.DimensionResolutions.FirstOrDefault();
        Assert.NotNull(dim);
        Assert.Equal("DirectKey", dim!.ResolutionType);
        Assert.Equal("Executable", dim.ExecutionCapability);
        Assert.Equal("material_id", dim.DimensionKeyColumn);
    }
}
