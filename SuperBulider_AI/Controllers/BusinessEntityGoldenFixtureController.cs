using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.BI.Entity;
using SuperBuilder_AI.Models.Metadata;
using System.Text;
using System.Text.Encodings.Web;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// Phase 3.1 Golden Fixture provisioning。
/// 仅在用户明确点击“创建 Golden Fixture”时写入 SuperBI Metadata DB。
/// 不创建业务数据库表，不修改动态 DataSource 的真实数据。
/// </summary>
[ApiController]
[Route("evaluation/business-entity/fixture")]
public sealed class BusinessEntityGoldenFixtureController : ControllerBase
{
    private const string GoldenBusinessKey = "phase3.1.golden.customer";
    private readonly SuperBIContext _db;

    public BusinessEntityGoldenFixtureController(SuperBIContext db) => _db = db;

    [HttpGet("")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var tenants = await _db.Tenants.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.TenantName }).ToListAsync(cancellationToken);
        var sources = await _db.DataSources.AsNoTracking().OrderBy(x => x.TenantId).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.TenantId, x.Name, x.DbType, x.Enabled }).ToListAsync(cancellationToken);
        var tables = await _db.MetadataTables.AsNoTracking()
            .Where(x => x.TableName != null)
            .OrderBy(x => x.TenantId).ThenBy(x => x.DataSourceId).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.TenantId, x.DataSourceId, x.TableName, ColumnCount = x.Columns.Count })
            .ToListAsync(cancellationToken);

        var html = new StringBuilder("<!doctype html><html><head><meta charset='utf-8'><title>Phase 3.1 Golden Fixture</title><style>body{font-family:Arial,sans-serif;margin:32px;max-width:1200px}select,button{padding:8px;margin:4px}table{border-collapse:collapse;width:100%}th,td{border:1px solid #ddd;padding:8px}.warn{background:#fff4ce;padding:12px}pre{background:#f6f6f6;padding:10px}</style></head><body>");
        html.Append("<h1>Phase 3.1 Golden Fixture Provisioning</h1>");
        html.Append("<div class='warn'><b>仅创建 Metadata DB 中的 Golden Entity 数据。</b><br>不会创建业务数据库表，不会修改 DataSource 连接目标中的业务数据。请只在本地/验证环境执行。</div>");
        if (tenants.Count == 0 || sources.Count == 0 || tables.Count == 0)
        {
            html.Append("<h2>无法创建 Fixture</h2><p>需要至少一个 Tenant、DataSource 和 MetadataTable。</p></body></html>");
            return Content(html.ToString(), "text/html", Encoding.UTF8);
        }
        html.Append("<form method='post' action='/evaluation/business-entity/fixture/provision'>");
        html.Append("<label>Tenant：</label><select name='tenantId'>");
        foreach (var t in tenants) html.Append($"<option value='{t.Id}'>{H(t.TenantName ?? "-")} (#{t.Id})</option>");
        html.Append("</select><label>DataSource：</label><select name='dataSourceId'>");
        foreach (var s in sources) html.Append($"<option value='{s.Id}'>{H(s.Name ?? "-")} (#{s.Id}, tenant={s.TenantId}, {H(s.DbType ?? "-")})</option>");
        html.Append("</select><label>MetadataTable：</label><select name='metadataTableId'>");
        foreach (var t in tables) html.Append($"<option value='{t.Id}'>{H(t.TableName ?? "-")} (#{t.Id}, tenant={t.TenantId}, ds={t.DataSourceId}, columns={t.ColumnCount})</option>");
        html.Append("</select><button type='submit'>创建 / 重用 Golden Fixture</button></form>");
        html.Append("<p>Fixture Key: <code>" + GoldenBusinessKey + "</code></p>");
        html.Append("<p><a href='/evaluation/business-entity'>返回 Runtime Verification</a></p></body></html>");
        return Content(html.ToString(), "text/html", Encoding.UTF8);
    }

    [HttpPost("provision")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> Provision(long tenantId, long dataSourceId, long metadataTableId, CancellationToken cancellationToken = default)
    {
        var tenantExists = await _db.Tenants.AnyAsync(x => x.Id == tenantId, cancellationToken);
        if (!tenantExists) return Content(Error("Tenant 不存在。"), "text/html", Encoding.UTF8);
        var source = await _db.DataSources.FirstOrDefaultAsync(x => x.Id == dataSourceId && x.TenantId == tenantId, cancellationToken);
        if (source is null) return Content(Error("DataSource 不存在，或不属于选择的 Tenant。"), "text/html", Encoding.UTF8);
        var table = await _db.MetadataTables.Include(x => x.Columns)
            .FirstOrDefaultAsync(x => x.Id == metadataTableId && x.TenantId == tenantId && x.DataSourceId == dataSourceId, cancellationToken);
        if (table is null) return Content(Error("MetadataTable 不存在，或不属于选择的 Tenant/DataSource。"), "text/html", Encoding.UTF8);
        var columns = table.Columns.Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.ColumnName)).OrderBy(x => x.Id).Take(3).ToList();
        if (columns.Count < 2) return Content(Error("Golden Fixture 至少需要同一 MetadataTable 下的 2 个有效 MetadataColumn。"), "text/html", Encoding.UTF8);

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var entity = await _db.BusinessEntities
            .Include(x => x.Keys).ThenInclude(x => x.PhysicalBindings)
            .Include(x => x.Attributes).ThenInclude(x => x.PhysicalBindings)
            .Include(x => x.Metrics).ThenInclude(x => x.PhysicalBindings)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.BusinessKey == GoldenBusinessKey, cancellationToken);

        if (entity is null)
        {
            entity = new BusinessEntity
            {
                TenantId = tenantId, BusinessKey = GoldenBusinessKey, Name = "GoldenCustomer",
                DisplayName = "Phase 3.1 Golden Customer",
                Description = "Runtime verification fixture; safe to remove after Phase 3.1 validation.",
                BusinessDomain = "Evaluation", SemanticText = "Golden customer business entity for Phase 3.1 runtime verification", Status = "Active"
            };
            _db.BusinessEntities.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var key = entity.Keys.FirstOrDefault(x => x.Name == "GoldenCustomerId");
        if (key is null)
        {
            key = new BusinessEntityKey { BusinessEntityId = entity.Id, Name = "GoldenCustomerId", DisplayName = "Customer ID", IsPrimary = true, KeyType = "Long" };
            entity.Keys.Add(key);
        }
        var attribute = entity.Attributes.FirstOrDefault(x => x.Name == "GoldenCustomerName");
        if (attribute is null)
        {
            attribute = new BusinessEntityAttribute { BusinessEntityId = entity.Id, Name = "GoldenCustomerName", DisplayName = "Customer Name", SemanticType = "Dimension", IsNullable = true, IsIdentifier = false };
            entity.Attributes.Add(attribute);
        }
        var metric = entity.Metrics.FirstOrDefault(x => x.Name == "GoldenCustomerCount");
        if (metric is null)
        {
            metric = new BusinessEntityMetric { BusinessEntityId = entity.Id, Name = "GoldenCustomerCount", DisplayName = "Customer Count", SemanticType = "Metric", Aggregation = "COUNT", IsCalculated = false };
            entity.Metrics.Add(metric);
        }
        await _db.SaveChangesAsync(cancellationToken);

        EnsureBinding(key.PhysicalBindings, dataSourceId, table.Id, columns[0].Id, "Key");
        EnsureBinding(attribute.PhysicalBindings, dataSourceId, table.Id, columns[1].Id, "Attribute");
        EnsureBinding(metric.PhysicalBindings, dataSourceId, table.Id, columns[0].Id, "Metric");
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return Content(Result(entity, source, table, columns), "text/html", Encoding.UTF8);
    }

    private static void EnsureBinding(ICollection<PhysicalBinding> bindings, long dataSourceId, long tableId, long columnId, string role)
    {
        if (bindings.Any(x => x.IsActive && x.DataSourceId == dataSourceId && x.MetadataTableId == tableId && x.MetadataColumnId == columnId)) return;
        bindings.Add(new PhysicalBinding { DataSourceId = dataSourceId, MetadataTableId = tableId, MetadataColumnId = columnId, PhysicalRole = role, BindingType = "GoldenFixture", Priority = 100, IsActive = true });
    }

    private static string Result(BusinessEntity e, DataSource s, MetadataTable t, IReadOnlyList<SuperBuilder_AI.Models.Metadata.MetadataColumn> c) =>
        $"<!doctype html><html><body style='font-family:Arial;margin:32px'><h1>Golden Fixture Ready</h1><pre>{H($"BusinessEntity #{e.Id}: {e.Name}\nTenantId: {e.TenantId}\nDataSourceId: {s.Id}\nMetadataTableId: {t.Id}\nColumns: {string.Join(", ", c.Select(x => $"#{x.Id} {x.ColumnName}"))}\nBusinessKey: {GoldenBusinessKey}")}</pre><p><a href='/evaluation/business-entity'>进入 Runtime / Golden Verification</a></p></body></html>";
    private static string Error(string message) => $"<!doctype html><html><body style='font-family:Arial;margin:32px'><h1>Golden Fixture Error</h1><pre>{H(message)}</pre><p><a href='/evaluation/business-entity/fixture'>返回</a></p></body></html>";
    private static string H(string? value) => HtmlEncoder.Default.Encode(value ?? string.Empty);
}
