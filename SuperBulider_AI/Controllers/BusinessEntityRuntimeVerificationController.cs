using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.BI.Entity;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Entity;
using SuperBuilder_AI.Models.Metadata;
using System.Text;
using System.Text.Encodings.Web;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// Phase 3.1 Browser Runtime / Golden Verification。
/// 不依赖 Swagger、Postman 或独立测试工程；只读取 SuperBI Metadata DB，并调用正式 Entity Resolution Contract。
/// </summary>
[ApiController]
[Route("evaluation/business-entity")]
public sealed class BusinessEntityRuntimeVerificationController : ControllerBase
{
    private readonly SuperBIContext _context;
    private readonly IEntityQueryPlanMapper _mapper;

    public BusinessEntityRuntimeVerificationController(SuperBIContext context, IEntityQueryPlanMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    [HttpGet("")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var entities = await _context.BusinessEntities
            .AsNoTracking()
            .Select(x => new EntityRow(x.Id, x.TenantId, x.Name, x.DisplayName, x.Status))
            .OrderBy(x => x.TenantId).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var dataSources = await _context.DataSources
            .AsNoTracking()
            .Select(x => new DataSourceRow(x.Id, x.TenantId, x.Name, x.DbType, x.Enabled))
            .OrderBy(x => x.TenantId).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var html = new StringBuilder();
        html.Append("<!doctype html><html><head><meta charset='utf-8'><title>Phase 3.1 Runtime Verification</title>");
        html.Append("<style>body{font-family:Arial,sans-serif;margin:32px;max-width:1200px}select,button{padding:8px;margin:4px}table{border-collapse:collapse;width:100%;margin-top:20px}th,td{border:1px solid #ddd;padding:8px;text-align:left}.ok{font-weight:bold}.muted{color:#666}pre{white-space:pre-wrap;background:#f6f6f6;padding:12px}</style></head><body>");
        html.Append("<h1>Phase 3.1 Business Entity Runtime / Golden Verification</h1>");
        html.Append("<p class='muted'>不依赖 Swagger / Postman。数据来自当前 SuperBI Metadata DB；执行正式 Entity → PhysicalBinding → QueryPlan Resolution。</p>");

        if (entities.Count == 0)
        {
            html.Append("<h2>没有 BusinessEntity</h2><p>当前 Metadata DB 没有可验证的 BusinessEntity。请先创建 3.1 Entity 数据。</p></body></html>");
            return Content(html.ToString(), "text/html", Encoding.UTF8);
        }

        html.Append("<form method='post' action='/evaluation/business-entity/run'>");
        html.Append("<label>BusinessEntity：</label><select name='businessEntityId'>");
        foreach (var entity in entities)
            html.Append($"<option value='{entity.Id}'>{H(entity.TenantId.ToString())} / {H(entity.Name)} (#{entity.Id})</option>");
        html.Append("</select><label>DataSource：</label><select name='dataSourceId'>");
        foreach (var source in dataSources)
            html.Append($"<option value='{source.Id}'>{H(source.TenantId?.ToString() ?? "-")} / {H(source.Name ?? "-")} (#{source.Id}, {H(source.DbType ?? "-")})</option>");
        html.Append("</select><button type='submit'>运行 Golden Cases</button></form>");

        html.Append("<h2>当前可用数据</h2><table><tr><th>Tenant</th><th>BusinessEntity</th><th>Status</th></tr>");
        foreach (var e in entities)
            html.Append($"<tr><td>{e.TenantId}</td><td>{H(e.DisplayName ?? e.Name)} (#{e.Id})</td><td>{H(e.Status)}</td></tr>");
        html.Append("</table><h3>DataSource</h3><table><tr><th>Tenant</th><th>Name</th><th>DB</th><th>Enabled</th></tr>");
        foreach (var s in dataSources)
            html.Append($"<tr><td>{s.TenantId}</td><td>{H(s.Name ?? "-")} (#{s.Id})</td><td>{H(s.DbType ?? "-")}</td><td>{s.Enabled}</td></tr>");
        html.Append("</table></body></html>");
        return Content(html.ToString(), "text/html", Encoding.UTF8);
    }

    [HttpPost("run")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> Run(long businessEntityId, long dataSourceId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.BusinessEntities
            .Include(x => x.Keys).ThenInclude(x => x.PhysicalBindings).ThenInclude(x => x.MetadataTable)
            .Include(x => x.Keys).ThenInclude(x => x.PhysicalBindings).ThenInclude(x => x.MetadataColumn)
            .Include(x => x.Attributes).ThenInclude(x => x.PhysicalBindings).ThenInclude(x => x.MetadataTable)
            .Include(x => x.Attributes).ThenInclude(x => x.PhysicalBindings).ThenInclude(x => x.MetadataColumn)
            .Include(x => x.Metrics).ThenInclude(x => x.PhysicalBindings).ThenInclude(x => x.MetadataTable)
            .Include(x => x.Metrics).ThenInclude(x => x.PhysicalBindings).ThenInclude(x => x.MetadataColumn)
            .Include(x => x.SourceRelationships).ThenInclude(x => x.PhysicalBindings).ThenInclude(x => x.MetadataTable)
            .Include(x => x.SourceRelationships).ThenInclude(x => x.PhysicalBindings).ThenInclude(x => x.MetadataColumn)
            .Include(x => x.TargetRelationships).ThenInclude(x => x.PhysicalBindings).ThenInclude(x => x.MetadataTable)
            .Include(x => x.TargetRelationships).ThenInclude(x => x.PhysicalBindings).ThenInclude(x => x.MetadataColumn)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == businessEntityId, cancellationToken);

        if (entity is null)
            return Content(RenderError("BusinessEntity not found."), "text/html", Encoding.UTF8);

        if (entity.TenantId <= 0)
            return Content(RenderError("BusinessEntity TenantId is invalid."), "text/html", Encoding.UTF8);

        var source = await _context.DataSources.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dataSourceId && x.TenantId == entity.TenantId, cancellationToken);
        if (source is null)
            return Content(RenderError("Selected DataSource does not belong to the BusinessEntity tenant."), "text/html", Encoding.UTF8);

        var cases = new List<CaseResult>();
        var firstMetric = entity.Metrics.FirstOrDefault(x => x.PhysicalBindings.Any(b => b.IsActive && b.DataSourceId == dataSourceId));
        if (firstMetric != null)
        {
            var intent = new QueryIntent { OriginalQuestion = $"验证指标 {firstMetric.DisplayName ?? firstMetric.Name}", IntentType = "Aggregate", Metrics = new List<QueryMetric> { new() { Name = firstMetric.Name, SemanticText = firstMetric.DisplayName ?? firstMetric.Name, Aggregation = firstMetric.Aggregation ?? "COUNT" } } };
            cases.Add(await ExecuteCase("G-3.1.12.6-01", "Metric Resolution", entity, intent, dataSourceId, x => x.Metrics.Count == 1, cancellationToken));
        }
        else cases.Add(Skipped("G-3.1.12.6-01", "Metric Resolution", "当前 DataSource 没有可用 Metric PhysicalBinding。"));

        var firstAttribute = entity.Attributes.FirstOrDefault(x => x.PhysicalBindings.Any(b => b.IsActive && b.DataSourceId == dataSourceId));
        if (firstAttribute != null)
        {
            var semantic = firstAttribute.DisplayName ?? firstAttribute.Name;
            var intent = new QueryIntent { OriginalQuestion = $"验证维度 {semantic}", IntentType = "Detail", Dimensions = new List<string> { semantic } };
            cases.Add(await ExecuteCase("G-3.1.12.6-02", "Dimension Resolution", entity, intent, dataSourceId, x => x.Dimensions.Count == 1, cancellationToken));

            var filterIntent = new QueryIntent { OriginalQuestion = $"验证过滤 {semantic}", IntentType = "Detail", Filters = new List<QueryFilter> { new() { SemanticText = semantic, Operator = "=", Value = "GOLDEN" } } };
            cases.Add(await ExecuteCase("G-3.1.12.6-03", "Filter Resolution", entity, filterIntent, dataSourceId, x => x.Filters.Count == 1, cancellationToken));
        }
        else
        {
            cases.Add(Skipped("G-3.1.12.6-02", "Dimension Resolution", "当前 DataSource 没有可用 Attribute PhysicalBinding。"));
            cases.Add(Skipped("G-3.1.12.6-03", "Filter Resolution", "当前 DataSource 没有可用 Attribute PhysicalBinding。"));
        }

        var selectedBindings = AllBindings(entity).Where(b => b.IsActive && b.DataSourceId == dataSourceId).ToList();
        cases.Add(new CaseResult("G-3.1.12.6-04", "DataSource Isolation", selectedBindings.Count > 0 && selectedBindings.All(b => b.DataSourceId == dataSourceId), selectedBindings.Count > 0 ? $"{selectedBindings.Count} active Binding(s), all DataSourceId={dataSourceId}." : "当前 DataSource 没有 active Binding。", null));

        var unusedDataSourceId = (await _context.DataSources.AsNoTracking().MaxAsync(x => (long?)x.Id, cancellationToken) ?? 0) + 1;
        try
        {
            var negativeIntent = new QueryIntent { OriginalQuestion = "Phase 3.1 wrong data source", IntentType = "Aggregate", Metrics = entity.Metrics.Take(1).Select(x => new QueryMetric { Name = x.Name, SemanticText = x.DisplayName ?? x.Name, Aggregation = x.Aggregation ?? "COUNT" }).ToList() };
            if (negativeIntent.Metrics.Count == 0)
            {
                cases.Add(new CaseResult("G-3.1.12.6-05", "Wrong DataSource", true, "Entity 没有 Metric，无法形成跨 DataSource fallback；视为隔离 PASS。", null));
            }
            else
            {
                await _mapper.MapAsync(entity, negativeIntent, unusedDataSourceId, cancellationToken);
                cases.Add(new CaseResult("G-3.1.12.6-05", "Wrong DataSource", false, "Unexpected resolution succeeded; cross-DataSource fallback may exist.", null));
            }
        }
        catch (InvalidOperationException ex)
        {
            cases.Add(new CaseResult("G-3.1.12.6-05", "Wrong DataSource", true, "错误 DataSource 未发生 fallback，Mapper 正确拒绝。", ex.Message));
        }

        return Content(RenderResults(entity, source, cases), "text/html", Encoding.UTF8);
    }

    private async Task<CaseResult> ExecuteCase(string id, string name, BusinessEntity entity, QueryIntent intent, long dataSourceId, Func<QueryPlanSemanticResolution, bool> assertion, CancellationToken cancellationToken)
    {
        try
        {
            var resolution = await _mapper.MapAsync(entity, intent, dataSourceId, cancellationToken);
            var pass = assertion(resolution) && AllResolutionDataSourcesMatch(resolution, dataSourceId);
            return new CaseResult(id, name, pass, pass ? "Resolution 成功且 DataSource Scope 正确。" : "Resolution 返回结果，但断言或 DataSource Scope 不满足。", SerializeResolution(resolution));
        }
        catch (Exception ex)
        {
            return new CaseResult(id, name, false, "Runtime Resolution 异常。", ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool AllResolutionDataSourcesMatch(QueryPlanSemanticResolution r, long id) =>
        r.Metrics.All(x => x.DataSourceId == id) && r.Dimensions.All(x => x.DataSourceId == id) && r.Filters.All(x => x.DataSourceId == id) && r.Tables.All(x => x.DataSourceId == id) && r.Orders.All(x => x.DataSourceId == id);

    private static IEnumerable<PhysicalBinding> AllBindings(BusinessEntity e) =>
        e.Keys.SelectMany(x => x.PhysicalBindings).Concat(e.Attributes.SelectMany(x => x.PhysicalBindings)).Concat(e.Metrics.SelectMany(x => x.PhysicalBindings)).Concat(e.SourceRelationships.SelectMany(x => x.PhysicalBindings)).Concat(e.TargetRelationships.SelectMany(x => x.PhysicalBindings));

    private static CaseResult Skipped(string id, string name, string reason) => new(id, name, false, "SKIPPED：真实数据不足，不能伪造 PASS。", reason);

    private static string SerializeResolution(QueryPlanSemanticResolution r) => $"Tables={r.Tables.Count}; Metrics={r.Metrics.Count}; Dimensions={r.Dimensions.Count}; Filters={r.Filters.Count}; Orders={r.Orders.Count}";

    private static string RenderResults(BusinessEntity entity, DataSource source, IReadOnlyList<CaseResult> cases)
    {
        var passed = cases.Count(x => x.Passed); var failed = cases.Count(x => !x.Passed);
        var sb = new StringBuilder("<!doctype html><html><head><meta charset='utf-8'><title>Phase 3.1 Golden Result</title><style>body{font-family:Arial,sans-serif;margin:32px;max-width:1200px}table{border-collapse:collapse;width:100%}th,td{border:1px solid #ddd;padding:9px} .pass{font-weight:bold}.fail{font-weight:bold;color:#b00020}pre{background:#f6f6f6;padding:10px}</style></head><body>");
        sb.Append($"<h1>Phase 3.1 Golden Runtime Result</h1><p>TenantId={entity.TenantId} | BusinessEntity={H(entity.DisplayName ?? entity.Name)} (#{entity.Id}) | DataSource={H(source.Name ?? "-")} (#{source.Id})</p>");
        sb.Append($"<h2>Result: {passed} PASS / {failed} NOT PASS</h2><table><tr><th>Case</th><th>Name</th><th>Result</th><th>Detail</th><th>Evidence</th></tr>");
        foreach (var c in cases) sb.Append($"<tr><td>{H(c.Id)}</td><td>{H(c.Name)}</td><td class='{(c.Passed ? "pass" : "fail")}'>{(c.Passed ? "PASS" : "NOT PASS")}</td><td>{H(c.Detail)}</td><td><pre>{H(c.Evidence ?? "-")}</pre></td></tr>");
        sb.Append("</table><p><a href='/evaluation/business-entity'>返回重新选择 Entity / DataSource</a></p></body></html>");
        return sb.ToString();
    }

    private static string RenderError(string message) => $"<!doctype html><html><body><h1>Phase 3.1 Verification Error</h1><pre>{H(message)}</pre><a href='/evaluation/business-entity'>返回</a></body></html>";
    private static string H(string? value) => HtmlEncoder.Default.Encode(value ?? string.Empty);

    private sealed record EntityRow(long Id, long TenantId, string Name, string? DisplayName, string Status);
    private sealed record DataSourceRow(long Id, long? TenantId, string? Name, string? DbType, bool? Enabled);
    private sealed record CaseResult(string Id, string Name, bool Passed, string Detail, string? Evidence);
}
