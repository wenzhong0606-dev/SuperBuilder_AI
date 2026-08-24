using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// C.13.3 本地 Runtime 验证控制器。
/// 用于开发机直接验证 SQL Server / Qdrant 的真实运行链，避免依赖 GitHub Runner 的基础设施差异。
/// </summary>
[ApiController]
[Route("evaluation/local-runtime")]
public sealed class LocalRuntimeDiagnosticsController : ControllerBase
{
    private readonly SuperBIContext _context;
    private readonly IConfiguration _configuration;

    public LocalRuntimeDiagnosticsController(
        SuperBIContext context,
        IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpGet("sqlserver")]
    public async Task<ActionResult<object>> SqlServer(CancellationToken cancellationToken)
        => Ok(await CheckSqlServerAsync(cancellationToken));

    [HttpGet("qdrant")]
    public async Task<ActionResult<object>> Qdrant(CancellationToken cancellationToken)
        => Ok(await CheckQdrantAsync(cancellationToken));

    [HttpGet("infrastructure")]
    public async Task<ActionResult<object>> Infrastructure(CancellationToken cancellationToken)
    {
        var sql = await CheckSqlServerAsync(cancellationToken);
        var qdrant = await CheckQdrantAsync(cancellationToken);
        return Ok(new { passed = sql.Passed && qdrant.Passed, sqlServer = sql, qdrant });
    }

    private async Task<LocalRuntimeCheckResult> CheckSqlServerAsync(CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var connected = await _context.Database.CanConnectAsync(cancellationToken);
            if (!connected)
                return new LocalRuntimeCheckResult(false, "SqlServer", "SuperBIContext 无法连接当前配置的 SQL Server。", stopwatch.ElapsedMilliseconds);

            var result = await _context.Database.SqlQueryRaw<int>("SELECT 1 AS Value").SingleAsync(cancellationToken);
            return new LocalRuntimeCheckResult(
                result == 1,
                "SqlServer",
                result == 1 ? "SQL Server 认证与 SELECT 1 均通过。" : "SQL Server SELECT 1 返回非预期结果。",
                stopwatch.ElapsedMilliseconds,
                new
                {
                    connected = true,
                    select1 = result,
                    database = _context.Database.GetDbConnection().Database,
                    server = _context.Database.GetDbConnection().DataSource
                });
        }
        catch (Exception ex)
        {
            return new LocalRuntimeCheckResult(
                false,
                "SqlServer",
                ex.Message,
                stopwatch.ElapsedMilliseconds,
                new { connected = false, exceptionType = ex.GetType().FullName, innerMessage = ex.InnerException?.Message });
        }
    }

    private async Task<LocalRuntimeCheckResult> CheckQdrantAsync(CancellationToken cancellationToken)
    {
        var host = _configuration["Qdrant:Host"] ?? "127.0.0.1";
        var grpcPort = _configuration.GetValue<int?>("Qdrant:Port") ?? 6334;
        var httpPort = _configuration.GetValue<int?>("Qdrant:HttpPort") ?? 6333;
        var uri = new Uri($"http://{host}:{httpPort}/healthz");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var response = await client.GetAsync(uri, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return new LocalRuntimeCheckResult(
                response.IsSuccessStatusCode,
                "Qdrant",
                response.IsSuccessStatusCode ? "Qdrant HTTP healthz 通过。" : "Qdrant HTTP healthz 返回失败状态。",
                stopwatch.ElapsedMilliseconds,
                new { host, httpPort, grpcPort, httpHealthUrl = uri.ToString(), statusCode = (int)response.StatusCode, responseBody = body });
        }
        catch (Exception ex)
        {
            return new LocalRuntimeCheckResult(
                false,
                "Qdrant",
                ex.Message,
                stopwatch.ElapsedMilliseconds,
                new { host, httpPort, grpcPort, httpHealthUrl = uri.ToString(), exceptionType = ex.GetType().FullName, innerMessage = ex.InnerException?.Message });
        }
    }

    private sealed record LocalRuntimeCheckResult(
        bool Passed,
        string Stage,
        string Message,
        long ElapsedMs,
        object? Details = null);
}
