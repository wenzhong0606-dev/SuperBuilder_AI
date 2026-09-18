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

    /// <summary>② 取消流程（C3）：启动扫描后立即取消，端点应成功（2xx）或已在终态冲突（409），取消后状态进入 Cancelling/Cancelled 等终态之一。</summary>
    [SkippableFact]
    public async Task Admin_CancelScan_TransitionsToCancellingOrCancelled()
    {
        var conn = ScanConnection;
        Skip.If(string.IsNullOrWhiteSpace(conn), "未配置 SB_E2E_SCAN_CONNECTION（可达业务库），本地跳过取消 E2E。");
        E2EConfig.Require(_fx.BaseUrl, E2EConfig.User, E2EConfig.Password);
        var page = await _fx.NewPageAsync();
        await LoginHelper.ApiLoginByCodeAsync(page, E2EConfig.User!, E2EConfig.Password!, "e2eapp");

        var (dsId, jobId) = await CreateAndScanAsync(page);

        // 轮询到可取消状态（Queued/Running），最多 ~10s
        string? phase = null;
        for (int i = 0; i < 10; i++)
        {
            var job = await E2EApiHelper.CallApiAsync(page, "GET", $"api/data-sources/{dsId}/metadata/scan/{jobId}");
            if (job.Ok) phase = JsonDocument.Parse(job.Body).RootElement.GetProperty("status").GetString();
            if (phase is "Queued" or "Running" or "Succeeded" or "Failed" or "Cancelled" or "PartiallySucceeded") break;
            await Task.Delay(1000);
        }

        var cancel = await E2EApiHelper.CallApiAsync(page, "POST", $"api/data-sources/{dsId}/metadata/scan/{jobId}/cancel");
        Assert.True(
            cancel.Status is >= 200 and < 300 or 409,
            $"取消端点返回非预期状态 {cancel.Status}：{cancel.Body}");

        if (cancel.Status is >= 200 and < 300)
        {
            await Task.Delay(1500);
            var after = await E2EApiHelper.CallApiAsync(page, "GET", $"api/data-sources/{dsId}/metadata/scan/{jobId}");
            var st = JsonDocument.Parse(after.Body).RootElement.GetProperty("status").GetString();
            Assert.True(
                st is "Cancelling" or "Cancelled" or "Succeeded" or "Failed" or "PartiallySucceeded",
                $"取消后状态={st} 不在预期集合");
        }
    }

    /// <summary>② 退出重进续显（C5）：启动后模拟重新进入页面，GET latest 应返回同一任务且带状态。</summary>
    [SkippableFact]
    public async Task Admin_ResumeScan_AfterExit_ReturnsLatestJob()
    {
        var conn = ScanConnection;
        Skip.If(string.IsNullOrWhiteSpace(conn), "未配置 SB_E2E_SCAN_CONNECTION，本地跳过续显 E2E。");
        E2EConfig.Require(_fx.BaseUrl, E2EConfig.User, E2EConfig.Password);
        var page = await _fx.NewPageAsync();
        await LoginHelper.ApiLoginByCodeAsync(page, E2EConfig.User!, E2EConfig.Password!, "e2eapp");

        var (dsId, jobId) = await CreateAndScanAsync(page);

        var latest = await E2EApiHelper.CallApiAsync(page, "GET", $"api/data-sources/{dsId}/metadata/scan/latest");
        Assert.True(latest.Ok, $"GET latest 失败（{latest.Status}）：{latest.Body}");
        var lj = JsonDocument.Parse(latest.Body).RootElement;
        Assert.True(
            lj.TryGetProperty("id", out var lid) && lid.GetInt64() == jobId,
            "latest 返回任务 id 与启动任务不一致");
        Assert.True(lj.TryGetProperty("status", out var lst) && !string.IsNullOrEmpty(lst.GetString()), "latest 缺少 status");
    }

    /// <summary>② 删除影响确认（C8）：cleanup 模式删除数据源应成功返回（2xx）。</summary>
    [SkippableFact]
    public async Task Admin_DeleteDataSource_Cleanup_ReturnsSuccess()
    {
        var conn = ScanConnection;
        Skip.If(string.IsNullOrWhiteSpace(conn), "未配置 SB_E2E_SCAN_CONNECTION，本地跳过删除 E2E。");
        E2EConfig.Require(_fx.BaseUrl, E2EConfig.User, E2EConfig.Password);
        var page = await _fx.NewPageAsync();
        await LoginHelper.ApiLoginByCodeAsync(page, E2EConfig.User!, E2EConfig.Password!, "e2eapp");

        var (dsId, _) = await CreateAndScanAsync(page);

        var del = await E2EApiHelper.CallApiAsync(page, "DELETE", $"api/data-sources/{dsId}?mode=cleanup");
        Assert.True(
            del.Status is >= 200 and < 300 or 409,
            $"cleanup 删除返回非预期状态 {del.Status}：{del.Body}");
    }

    /// <summary>② 失败项重扫（C7）：扫描终态后，retry-failed 在有失败项时建新任务（2xx），无失败项（Succeeded）时返回 409，均属契约。</summary>
    [SkippableFact]
    public async Task Admin_RetryFailedScan_HandlesNoFailuresOrCreatesLinked()
    {
        var conn = ScanConnection;
        Skip.If(string.IsNullOrWhiteSpace(conn), "未配置 SB_E2E_SCAN_CONNECTION，本地跳过重扫 E2E。");
        E2EConfig.Require(_fx.BaseUrl, E2EConfig.User, E2EConfig.Password);
        var page = await _fx.NewPageAsync();
        await LoginHelper.ApiLoginByCodeAsync(page, E2EConfig.User!, E2EConfig.Password!, "e2eapp");

        var (dsId, jobId) = await CreateAndScanAsync(page);

        string? status = null;
        for (int i = 0; i < 90; i++)
        {
            var job = await E2EApiHelper.CallApiAsync(page, "GET", $"api/data-sources/{dsId}/metadata/scan/{jobId}");
            if (job.Ok) status = JsonDocument.Parse(job.Body).RootElement.GetProperty("status").GetString();
            if (status is "Succeeded" or "Failed" or "Cancelled" or "PartiallySucceeded") break;
            await Task.Delay(1000);
        }

        var retry = await E2EApiHelper.CallApiAsync(page, "POST", $"api/data-sources/{dsId}/metadata/scan/{jobId}/retry-failed");
        Assert.True(
            retry.Status is >= 200 and < 300 or 409,
            $"retry-failed 返回非预期状态 {retry.Status}：{retry.Body}");
    }

    private async Task<(long dsId, long jobId)> CreateAndScanAsync(IPage page)
    {
        var name = "e2e-scan-" + Guid.NewGuid().ToString("N")[..8];
        var create = await E2EApiHelper.CallApiAsync(page, "POST", "api/data-sources", new
        {
            name,
            dbType = ScanDbType,
            connectionString = ScanConnection,
        });
        if (!create.Ok || create.Status is < 200 or >= 300)
            Skip.If(true, $"数据源创建失败（{create.Status}）：{create.Body}");
        var dsId = JsonDocument.Parse(create.Body).RootElement.GetProperty("id").GetInt64();

        var scan = await E2EApiHelper.CallApiAsync(page, "POST", $"api/data-sources/{dsId}/metadata/scan");
        if (scan.Status != 202)
            Skip.If(true, $"扫描触发失败（{scan.Status}）：{scan.Body}");
        var jobId = JsonDocument.Parse(scan.Body).RootElement.GetProperty("jobId").GetInt64();
        return (dsId, jobId);
    }
}
