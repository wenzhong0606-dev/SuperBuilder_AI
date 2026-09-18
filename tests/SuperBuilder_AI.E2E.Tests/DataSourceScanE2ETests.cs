using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace SuperBuilder_AI.E2E;

/// <summary>
/// E2E-01 缺失链之一：<c>DataSource → Scan → Ask</c> 的"数据源摄取"段。
/// 管理员经 API 创建数据源 → 触发元数据扫描 → 轮询任务至 Succeeded → 断言元数据表已摄取。
/// 真实扫描需一个**可达业务库**，故由 <c>SB_E2E_SCAN_CONNECTION</c> 控制：
/// 未配置则诚实跳过（CI 无可达业务库，不会误红）；配置后在实时环境执行真实摄取。
/// 仅当 SB_E2E_BASE_URL 与管理员凭据齐备时执行，否则由 SkippableFact 诚实跳过。
/// </summary>
[Collection("playwright")]
public sealed class DataSourceScanE2ETests
{
    private readonly PlaywrightFixture _fx;
    public DataSourceScanE2ETests(PlaywrightFixture fx) => _fx = fx;

    private static bool IsCi =>
        string.Equals(
            Environment.GetEnvironmentVariable("CI"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    private static string? ScanConnection
    {
        get
        {
            var explicitConnection = Environment.GetEnvironmentVariable("SB_E2E_SCAN_CONNECTION");
            if (!string.IsNullOrWhiteSpace(explicitConnection))
                return explicitConnection;

            if (!IsCi)
                return null;

            var sqlPassword = Environment.GetEnvironmentVariable("TEST_SQL_PASSWORD");
            if (string.IsNullOrWhiteSpace(sqlPassword))
                return null;

            return $"Server=127.0.0.1,1433;Database=SuperBuilder_E2E_Business;User Id=sa;Password={sqlPassword};TrustServerCertificate=True;";
        }
    }

    private static string ScanDbType =>
        Environment.GetEnvironmentVariable("SB_E2E_SCAN_DB_TYPE")
        ?? (IsCi ? "SQLSERVER" : "SQLSERVER");

    /// <summary>数据源创建 + 元数据扫描 → 任务 Succeeded 且 tablesScanned &gt; 0。</summary>
    [SkippableFact]
    public async Task Admin_CreateDataSource_ThenScan_PopulatesMetadata()
    {
        var conn = ScanConnection;
        if (string.IsNullOrWhiteSpace(conn))
        {
            if (IsCi)
                Assert.Fail("CI 必须配置 SB_E2E_SCAN_CONNECTION，DataSource→Scan 核心链路不得跳过。");

            Skip.If(true, "未配置 SB_E2E_SCAN_CONNECTION（可达业务库），本地跳过 DataSource→Scan 链。");
        }
        E2EConfig.Require(_fx.BaseUrl, E2EConfig.User, E2EConfig.Password);
        var page = await _fx.NewPageAsync();
        await LoginHelper.ApiLoginByCodeAsync(page, E2EConfig.User!, E2EConfig.Password!, "e2eapp");

        // 1) 创建数据源（连接串由服务端 AEAD 加密，仅需明文传入）
        var name = "e2e-scan-" + Guid.NewGuid().ToString("N")[..8];
        var create = await E2EApiHelper.CallApiAsync(page, "POST", "api/data-sources", new
        {
            name,
            dbType = ScanDbType,
            connectionString = conn,
        });
        if (!create.Ok || create.Status is < 200 or >= 300)
        {
            if (IsCi)
                Assert.Fail($"CI 数据源创建失败（{create.Status}）：{create.Body}");

            Skip.If(true, $"数据源创建失败（{create.Status}）：{create.Body}");
        }
        if (!JsonDocument.Parse(create.Body).RootElement.TryGetProperty("id", out var idEl)
            || idEl.ValueKind != JsonValueKind.Number)
        {
            if (IsCi)
                Assert.Fail($"CI 数据源创建响应缺少 id：{create.Body}");

            Skip.If(true, $"数据源创建响应缺少 id：{create.Body}");
            return;
        }
        var dsId = idEl.GetInt64();

        // 2) 触发扫描，拿到 jobId（接口返回 202 + jobId）
        var scan = await E2EApiHelper.CallApiAsync(page, "POST", $"api/data-sources/{dsId}/metadata/scan");
        if (scan.Status != 202)
        {
            if (IsCi)
                Assert.Fail($"CI 扫描触发失败（{scan.Status}）：{scan.Body}");

            Skip.If(true, $"扫描触发失败（{scan.Status}）：{scan.Body}");
        }
        var jobId = JsonDocument.Parse(scan.Body).RootElement.GetProperty("jobId").GetInt64();

        // 3) 轮询任务状态直到终态（最多约 90s）
        string? status = null;
        string? failureDetail = null;
        int tablesScanned = 0;
        for (int i = 0; i < 90; i++)
        {
            await Task.Delay(1000);
            var job = await E2EApiHelper.CallApiAsync(page, "GET", $"api/data-sources/{dsId}/metadata/scan/{jobId}");
            if (!job.Ok) continue;
            var j = JsonDocument.Parse(job.Body).RootElement;
            status = j.GetProperty("status").GetString();
            tablesScanned = j.TryGetProperty("tablesScanned", out var ts) && ts.ValueKind == JsonValueKind.Number ? ts.GetInt32() : 0;
            if (status == "Failed")
            {
                var reason = j.TryGetProperty("failedReason", out var r) ? r.ToString() : "";
                var code = j.TryGetProperty("errorCode", out var c) ? c.ToString() : "";
                var message = j.TryGetProperty("errorMessage", out var m) ? m.ToString() : "";
                failureDetail = $"reason={reason}; code={code}; message={message}";
            }
            if (status == "Succeeded" || status == "Failed") break;
        }

        Assert.True(status == "Succeeded", $"扫描任务状态={status ?? "无响应"}; {failureDetail}");
        Assert.True(
            tablesScanned >= 3,
            $"扫描成功但 tablesScanned={tablesScanned}，CI 业务 Fixture 预期至少 3 张表（job：{status}）。");
    }
}
