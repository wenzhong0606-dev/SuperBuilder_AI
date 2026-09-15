using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SuperBuilder_AI.Services;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Infrastructure.Security;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Api.Diagnostics;

namespace SuperBuilder_AI.Api.Background;

/// <summary>
/// 允许外部（API 端点）请求中断某个正在运行的扫描任务。
/// </summary>
public interface IMetadataScanCancellation
{
    /// <summary>请求取消指定 jobId 的扫描。返回 true 表示该 job 当前可被取消（运行中）。</summary>
    bool RequestCancel(long jobId);
}

/// <summary>
/// 元数据扫描后台处理器（M4-05）。从队列取出扫描任务，在独立作用域内执行扫描，
/// 并将状态 / 进度 / 计数 / 脱敏错误回写到 <see cref="MetadataScanJob"/>。
/// </summary>
public sealed class MetadataScanHostedService : BackgroundService, IMetadataScanCancellation
{
    private readonly IMetadataScanQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MetadataScanHostedService> _logger;
    private readonly ScanBacklogGauge _backlog;

    /// <summary>运行中任务的取消源：jobId → 该次扫描专属的 CancellationTokenSource。</summary>
    private readonly ConcurrentDictionary<long, CancellationTokenSource> _jobCts = new();

    public MetadataScanHostedService(
        IMetadataScanQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<MetadataScanHostedService> logger,
        ScanBacklogGauge backlog)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _backlog = backlog;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            long jobId;
            try
            {
                jobId = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await ProcessJobAsync(jobId, stoppingToken);
            }
            catch (Exception ex)
            {
                // 单个任务异常不应拖垮整个后台循环。
                _logger.LogError(ex, "扫描任务 {JobId} 处理发生未预期异常。", jobId);
            }
        }
    }

    private async Task ProcessJobAsync(long jobId, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SuperBIContext>();
        var scanner = scope.ServiceProvider.GetRequiredService<MetadataScannerService>();
        var secrets = scope.ServiceProvider.GetRequiredService<ISecretStore>();

        var job = await context.MetadataScanJobs.FirstOrDefaultAsync(j => j.Id == jobId, stoppingToken);
        if (job is null)
        {
            _logger.LogWarning("扫描任务 {JobId} 不存在，跳过。", jobId);
            _backlog.Decrement();
            return;
        }
        if (job.Status != MetadataScanJobStatus.Queued)
        {
            _logger.LogWarning("扫描任务 {JobId} 状态为 {Status}，跳过。", jobId, job.Status);
            _backlog.Decrement();
            return;
        }

        var dataSource = await context.DataSources
            .FirstOrDefaultAsync(d => d.Id == job.DataSourceId, stoppingToken);
        if (dataSource is null)
        {
            await FailJobAsync(context, job, "DataSourceNotFound", "数据源不存在，无法执行扫描。", stoppingToken);
            return;
        }

        job.Status = MetadataScanJobStatus.Running;
        job.StartedAt = DateTime.UtcNow;
        job.ProgressPercent = 0;
        job.Stage = "Connecting";
        await context.SaveChangesAsync(stoppingToken);

        // 每任务专属取消源，与宿主停止信号链接：用户取消或宿主关闭都会中断本次扫描。
        var jobCts = new CancellationTokenSource();
        _jobCts[jobId] = jobCts;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, jobCts.Token);

        var telemetry = new ScanTelemetry();
        var progress = new JobProgress(job, telemetry, async ct =>
        {
            try
            {
                await context.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "扫描任务 {JobId} 进度持久化失败（忽略）。", jobId);
            }
        });

        try
        {
            await scanner.ScanAsync(
                job.TenantId,
                job.DataSourceId,
                secrets.ResolvePlaintext(dataSource.ConnectionString) ?? string.Empty,
                progress,
                telemetry,
                cleanupOrphans: true,
                ct: linked.Token);
        }
        catch (OperationCanceledException) when (jobCts.IsCancellationRequested)
        {
            // 用户主动中断：标记 Cancelled 而非 Failed。
            telemetry.AddEvent("Warning", "ScanCancelled", "扫描已被用户中断。");
            await CancelJobAsync(context, job, stoppingToken);
            return;
        }
        catch (Exception ex)
        {
            await FailJobAsync(context, job, ex, stoppingToken);
            return;
        }
        finally
        {
            _jobCts.TryRemove(jobId, out _);
            jobCts.Dispose();
        }

        // 成功：回写数据源最后扫描时间，并落盘最终计数。
        job.Status = MetadataScanJobStatus.Succeeded;
        job.FinishedAt = DateTime.UtcNow;
        job.ProgressPercent = 100;
        job.TablesScanned = telemetry.TablesScanned;
        job.ColumnsScanned = telemetry.ColumnsScanned;
        job.OrphansDetected = telemetry.OrphansDetected;
        job.Stage = "Succeeded";
        telemetry.UpdateTiming(job.StartedAt, 100);
        job.ProgressDetailsJson = telemetry.ToJson();
        job.ErrorCode = null;
        job.ErrorMessage = null;
        dataSource.LastScanAt = DateTime.UtcNow;
        await context.SaveChangesAsync(stoppingToken);
        _backlog.Decrement();
    }

    private async Task FailJobAsync(SuperBIContext context, MetadataScanJob job, Exception ex, CancellationToken ct)
    {
        var code = ex.GetType().Name;
        var message = Sanitize(ex.Message);
        await FailJobAsync(context, job, code, message, ct);
    }

    private async Task FailJobAsync(SuperBIContext context, MetadataScanJob job, string code, string message, CancellationToken ct)
    {
        job.Status = MetadataScanJobStatus.Failed;
        job.FinishedAt = DateTime.UtcNow;
        job.Stage = "Failed";
        job.ErrorCode = code.Length > 64 ? code[..64] : code;
        job.ErrorMessage = message.Length > 2000 ? message[..2000] : message;
        await context.SaveChangesAsync(ct);
        _backlog.Decrement();
    }

    private async Task CancelJobAsync(SuperBIContext context, MetadataScanJob job, CancellationToken ct)
    {
        job.Status = MetadataScanJobStatus.Cancelled;
        job.FinishedAt = DateTime.UtcNow;
        job.Stage = "Cancelled";
        job.ErrorCode = "UserCancelled";
        job.ErrorMessage = "扫描已被用户中断。";
        await context.SaveChangesAsync(ct);
        _backlog.Decrement();
    }

    /// <inheritdoc />
    public bool RequestCancel(long jobId)
    {
        if (_jobCts.TryGetValue(jobId, out var cts))
        {
            cts.Cancel();
            return true;
        }
        return false;
    }

    /// <summary>
    /// 脱敏错误信息：移除连接串风格的 key=value 片段（含 password/user id/server 等敏感键），
    /// 避免凭据或连接细节经轮询接口泄露给前端。
    /// </summary>
    private static string Sanitize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "扫描失败。";
        var cleaned = Regex.Replace(
            raw,
            @"(?i)(password|pwd|user\s*id|uid|data\s*source|server|port|initial\s*catalog|database)\s*=\s*[^;]*;?",
            "");
        cleaned = cleaned.Replace(";", " ").Replace("\r", " ").Replace("\n", " ").Trim();
        if (cleaned.Length > 500) cleaned = cleaned[..500];
        return string.IsNullOrWhiteSpace(cleaned) ? "扫描失败。" : cleaned;
    }

    /// <summary>
    /// 进度回调：同步更新任务实体的进度与计数，并即时持久化（后台线程无同步上下文，可安全同步等待）。
    /// </summary>
    private sealed class JobProgress : IProgress<int>
    {
        private readonly MetadataScanJob _job;
        private readonly ScanTelemetry _telemetry;
        private readonly Func<CancellationToken, Task> _flush;
        private int _lastPersistedPercent = -1;

        public JobProgress(MetadataScanJob job, ScanTelemetry telemetry, Func<CancellationToken, Task> flush)
        {
            _job = job;
            _telemetry = telemetry;
            _flush = flush;
        }

        public void Report(int value)
        {
            _job.ProgressPercent = value;
            _job.Stage = _telemetry.Stage;
            _job.TablesScanned = _telemetry.TablesScanned;
            _job.ColumnsScanned = _telemetry.ColumnsScanned;
            _job.OrphansDetected = _telemetry.OrphansDetected;
            _telemetry.UpdateTiming(_job.StartedAt, value);
            _job.ProgressDetailsJson = _telemetry.ToJson();
            if (value == _lastPersistedPercent && value is not (0 or 100))
                return;
            _lastPersistedPercent = value;
            _flush(CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
