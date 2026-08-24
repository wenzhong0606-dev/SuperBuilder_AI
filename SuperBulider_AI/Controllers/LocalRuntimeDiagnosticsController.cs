using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// C.13.3 本地 Runtime 验证控制器。
/// 用于开发机直接验证 SQL Server / Qdrant / Metadata Fixture 的真实运行链，避免依赖 GitHub Runner 的基础设施差异。
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

    /// <summary>
    /// C.13.3-01：验证当前应用实际使用的 SQL Server 连接是否可认证、可执行 SELECT 1。
    /// </summary>
    [HttpGet("sqlserver")]
    public async Task<ActionResult<object>> SqlServer(CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var connected = await _context.Database.CanConnectAsync(cancellationToken);
            if (!connected)
            {
                return Ok(new
                {
                    passed = false,
                    stage = "SqlServer",
                    connected = false,
                    message = "SuperBIContext 无法连接当前配置的 SQL Server。",
                    elapsedMs = stopwatch.ElapsedMilliseconds
                });
            }

            var result = await _context.Database.SqlQueryRaw<int>("SELECT 1 AS Value")
                .SingleAsync(cancellationToken);

            return Ok(new
            {
                passed = result == 1,
                stage = "SqlServer",
                connected = true,
                select1 = result,
                database = _context.Database.GetDbConnection().Database,
                server = _context.Database.GetDbConnection().DataSource,
                elapsedMs = stopwatch.ElapsedMilliseconds
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                passed = false,
                stage = "SqlServer",
                connected = false,
                exceptionType = ex.GetType().FullName,
                message = ex.Message,
                innerMessage = ex.InnerException?.Message,
                elapsedMs = stopwatch.ElapsedMilliseconds
            });
        }
    }

    /// <summary>
    /// C.13.3-02：验证应用配置中的 Qdrant HTTP 端点以及 healthz。
    /// 6333 是 HTTP，6334 是正式 Runtime 使用的 gRPC 端口。
    /// </summary>
    [HttpGet("qdrant")]
    public async Task<ActionResult<object>> Qdrant(CancellationToken cancellationToken)
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

            return Ok(new
            {
                passed = response.IsSuccessStatusCode,
                stage = "Qdrant",
                host,
                httpPort,
                grpcPort,
                httpHealthUrl = uri.ToString(),
                statusCode = (int)response.StatusCode,
                responseBody = body,
                elapsedMs = stopwatch.ElapsedMilliseconds
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                passed = false,
                stage = "Qdrant",
                host,
                httpPort,
                grpcPort,
                httpHealthUrl = uri.ToString(),
                exceptionType = ex.GetType().FullName,
                message = ex.Message,
                innerMessage = ex.InnerException?.Message,
                elapsedMs = stopwatch.ElapsedMilliseconds
            });
        }
    }

    /// <summary>
    /// C.13.3-03：一次执行 SQL Server + Qdrant 基础设施诊断。
    /// 该 Action 不导入数据、不修改数据库、不重建向量，仅用于定位环境问题。
    /// </summary>
    [HttpGet("infrastructure")]
    public async Task<ActionResult<object>> Infrastructure(CancellationToken cancellationToken)
    {
        var sql = await SqlServer(cancellationToken);
        var qdrant = await Qdrant(cancellationToken);
        return Ok(new
        {
            passed = sql.Value is not null && qdrant.Value is not null,
            sqlServer = sql.Value,
            qdrant = qdrant.Value
        });
    }
}
