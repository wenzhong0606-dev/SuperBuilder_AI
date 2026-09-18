using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Services;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Application.Metadata;
using SuperBuilder_AI.Infrastructure.Security;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Api.Diagnostics;
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Application.Common.Options;

namespace SuperBuilder_AI.Api.Background;

/// <summary>
/// 元数据扫描后台处理器（M4-05 / Step 5：C3 软取消 + C4 重启恢复）。
/// 消费循环与对账循环并发启动；每个运行任务持有独立 CancellationTokenSource，
/// 取消端点可中断；启动对账将遗留 Queued 重入队、遗留 Running/Retrying/Cancelling
/// 标记终态（不自动重跑），并周期检查心跳超时。
/// </summary>
public sealed class MetadataScanHostedService : BackgroundService
{
    private readonly IMetadataScanQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MetadataScanHostedService> _logger;
    private readonly ScanBacklogGauge _backlog;
    private readonly ScanHeartbeatOptions _heartbeatOptions;

    /// <summary>C3：每个运行任务的取消源，键为 jobId；取消端点据此中断对应扫描。</summary>
    private readonly ConcurrentDictionary<long, CancellationTokenSource> _jobCts = new();

    /// <summary>C4：自有取消源（禁止对宿主 stoppingToken 调 Cancel）。</summary>
    private CancellationTokenSource _cts = new();

    private Task? _consumeTask;
    private Task? _reconcileTask;

    public MetadataScanHostedService(
        IMetadataScanQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<MetadataScanHostedService> logger,
        ScanBacklogGauge backlog,
        IOptions<ScanHeartbeatOptions> heartbeatOptions)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _backlog = backlog;
        _heartbeatOptions = heartbeatOptions.Value;
    }

    /// <summary>C3：请求取消某运行任务。返回 true 表示已对其 CTS 发信号（任务已在运行）；false 表示任务未运行（如仍 Queued）。</summary>
    public bool RequestCancel(long jobId)
    {
        if (_jobCts.TryGetValue(jobId, out var cts))
        {
            try { if (!cts.IsCancellationRequested) cts.Cancel(); } catch { }
            return true;
        }
        return false;
    }

    public override void Dispose()
    {
        try { _cts.Dispose(); } catch { }
        foreach (var kv in _jobCts)
        {
            try { kv.Value.Dispose(); } catch { }
        }
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // C4(v9)：使用自有 CTS；宿主停止仅触发 _cts.Cancel()，绝不调用 stoppingToken.Cancel()。
        _cts = new CancellationTokenSource();
        _ = stoppingToken.Register(() => SafeCancel(_cts));

        // C4(v9)：消费循环与对账循环**并发**启动，再 WhenAll。禁止串行（空队列会阻塞消费，对账永不执行）。
        _consumeTask = ConsumeLoopAsync(_cts.Token);
        _reconcileTask = ReconcileAsync(_cts.Token);
        await Task.WhenAll(_consumeTask, _reconcileTask);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        SafeCancel(_cts);
        if (_consumeTask is not null || _reconcileTask is not null)
        {
            try
            {
                await Task.WhenAll(
                    _consumeTask ?? Task.CompletedTask,
                    _reconcileTask ?? Task.CompletedTask).WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("扫描后台服务停止超时（15s），强制退出。");
            }
            catch (OperationCanceledException)
            {
                // 预期内。
            }
        }
        await base.StopAsync(cancellationToken);
    }

    private static void SafeCancel(CancellationTokenSource cts)
    {
        try { if (!cts.IsCancellationRequested) cts.Cancel(); } catch { }
    }

    private async Task ConsumeLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            long jobId;
            try
            {
                jobId = await _queue.DequeueAsync(token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫描队列出队异常，消费循环退出。");
                break;
            }

            try
            {
                await ProcessJobAsync(jobId, token);
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
        var audit = scope.ServiceProvider.GetService<IAuditLogService>();
        var recorder = scope.ServiceProvider.GetRequiredService<MetadataScanFailureRecorder>();
        var retryPolicy = scope.ServiceProvider.GetRequiredService<IOptions<ScanRetryPolicy>>().Value;

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

        // C2：反序列化扫描范围（§L.6 从 active 选种仅应用 scope 内表）；空字符串表示全量。
        ScanScope? scanScope = null;
        if (!string.IsNullOrWhiteSpace(job.ScopeJson))
            scanScope = JsonSerializer.Deserialize<ScanScope>(job.ScopeJson);

        var dataSource = await context.DataSources
            .FirstOrDefaultAsync(d => d.Id == job.DataSourceId, stoppingToken);
        if (dataSource is null)
        {
            await FailJobAsync(context, job, "DataSourceNotFound", "数据源不存在，无法执行扫描。", stoppingToken);
            await AuditScanAsync(audit, job, "failure", "数据源不存在。");
            _backlog.Decrement();
            return;
        }

        IReadOnlyCollection<MetadataScanJobFailure>? retryFailures = null;
        if (job.OriginalJobId is { } originalJobId)
        {
            retryFailures = await context.MetadataScanJobFailures.AsNoTracking()
                .Where(f => f.JobId == originalJobId && f.DataSourceId == job.DataSourceId
                    && !f.Resolved && f.TableName != null)
                .ToListAsync(stoppingToken);
            if (retryFailures.Count == 0)
            {
                await FailJobAsync(context, job, "no_failed_tables", "原任务没有可单独重扫的失败表。", stoppingToken);
                return;
            }
        }

        // C3/C4：链接 stoppingToken + 本任务 CTS，便于取消端点中断（阶段/每表边界抛 OCE）。
        var jobCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _jobCts[jobId] = jobCts;

        // 状态、版本分配和种子版本一起提交，避免崩溃后留下 Running 但没有批次号的任务。
        try
        {
            await using var allocationTx = await context.Database.BeginTransactionAsync(jobCts.Token);
            job.Status = MetadataScanJobStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            job.LastHeartbeatUtc = DateTime.UtcNow;
            job.ProgressPercent = 0;
            job.Stage = "Connecting";
            var allocated = (await context.Database
                .SqlQuery<int>($"UPDATE DataSources SET NextMetadataVersion = NextMetadataVersion + 1 OUTPUT DELETED.NextMetadataVersion WHERE Id = {dataSource.Id}")
                .ToListAsync(jobCts.Token)).First();
            job.BatchVersion = allocated;
            job.SeedVersion = dataSource.ActiveMetadataVersion;
            await context.SaveChangesAsync(jobCts.Token);
            await allocationTx.CommitAsync(jobCts.Token);
        }
        catch
        {
            _jobCts.TryRemove(jobId, out _);
            jobCts.Dispose();
            throw;
        }

        // 心跳循环使用独立 scope 的定点 UPDATE，避免与扫描主 DbContext 并发 SaveChanges。
        var heartbeatTask = HeartbeatLoopAsync(job.Id, jobCts.Token);

        var telemetry = new ScanTelemetry();
        var tableFailures = new List<ScanTableFailure>();
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

        var cancelled = false;
        try
        {
            // §L.3 从 active 克隆出 staging（清空向量状态），新扫描在 staging 上改写。
            await scanner.PrepareStagingAsync(job.TenantId, job.DataSourceId, job.BatchVersion, job.SeedVersion, jobCts.Token);

            await scanner.ScanAsync(
                job.TenantId,
                job.DataSourceId,
                secrets.ResolvePlaintext(dataSource.ConnectionString) ?? string.Empty,
                job.BatchVersion,
                job.SeedVersion,
                progress,
                telemetry,
                cleanupOrphans: true,
                scope: scanScope,
                retryFailures: retryFailures,
                failedTables: tableFailures,
                ct: jobCts.Token);

            foreach (var failure in tableFailures)
            {
                for (var attempt = 0; attempt < failure.Attempts; attempt++)
                    await recorder.RecordTableFailureAsync(job.Id, job.DataSourceId, job.OriginalJobId,
                        failure.CatalogName, failure.SchemaName, failure.TableName, failure.Stage,
                        failure.ErrorType, failure.ErrorMessage, jobCts.Token);
            }

            // C4 防激活：激活前复查状态（心跳超时可能已标记 Failed），迟到 worker 不得激活。
            await context.Entry(job).ReloadAsync(jobCts.Token);
            if (job.Status != MetadataScanJobStatus.Running)
            {
                await CleanupStagingAsync(job.TenantId, job.DataSourceId, job.BatchVersion, jobCts.Token);
                return;
            }

            // §L.5 激活：向量完整性闸门 + 引用重映射 + 仅翻指针。
            await scanner.ActivateAsync(job, dataSource, jobCts.Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            // 重新加载以读取取消端点写入的 Cancelling 状态（决定是否记为 Cancelled）。
            await context.Entry(job).ReloadAsync(CancellationToken.None);
        }
        catch (MetadataActivationBlockedException actEx)
        {
            foreach (var table in actEx.IncompleteVectorTables)
                await recorder.RecordTableFailureAsync(job.Id, job.DataSourceId, job.OriginalJobId,
                    table.CatalogName, table.SchemaName, table.TableName, "VectorIndex",
                    "VectorIndexIncomplete", "必需的表、列或语义向量未同步。", jobCts.Token);
            job.FailedReason = actEx.Reason;
            await FailJobAsync(context, job, actEx.Reason, "激活被阻断：" + actEx.Reason, jobCts.Token);
            await AuditScanAsync(audit, job, "failure", "激活被阻断：" + actEx.Reason);
            await CleanupStagingAsync(job.TenantId, job.DataSourceId, job.BatchVersion, jobCts.Token);
            return;
        }
        catch (Exception ex)
        {
            var errType = ex.GetType().Name;
            var errMsg = Sanitize(ex.Message);
            await recorder.RecordTableFailureAsync(
                job.Id, job.DataSourceId, job.OriginalJobId, null, null, null, job.Stage, errType, errMsg, jobCts.Token);

            // C7：连接/权限错误快失败不重试；其余抖动可重试，直至耗尽 MaxAttempts。
            if (!IsFastFail(ex, retryPolicy))
            {
                var prior = await context.MetadataScanJobFailures
                    .Where(f => f.JobId == job.Id)
                    .OrderByDescending(f => f.RetryCount)
                    .FirstOrDefaultAsync(jobCts.Token);
                var attempts = prior?.RetryCount ?? 1;
                if (attempts < retryPolicy.MaxAttempts)
                {
                    // 重试：清理上次尝试的 staging，重置为 Queued 重新入队（ProcessJobAsync 重分配 BatchVersion 并从 active 重播）。
                    await CleanupStagingAsync(job.TenantId, job.DataSourceId, job.BatchVersion, jobCts.Token);
                    job.Status = MetadataScanJobStatus.Retrying;
                    job.FailedReason = "retrying";
                    job.ErrorCode = errType.Length > 64 ? errType[..64] : errType;
                    job.ErrorMessage = errMsg.Length > 2000 ? errMsg[..2000] : errMsg;
                    await context.SaveChangesAsync(jobCts.Token);

                    job.Status = MetadataScanJobStatus.Queued;
                    job.StartedAt = null;
                    job.FinishedAt = null;
                    job.BatchVersion = 0;
                    job.SeedVersion = 0;
                    job.ProgressPercent = 0;
                    job.Stage = "Queued";
                    job.FailedReason = null;
                    job.ErrorCode = null;
                    job.ErrorMessage = null;
                    await context.SaveChangesAsync(jobCts.Token);
                    await _queue.EnqueueAsync(job.Id, jobCts.Token);
                    _backlog?.Increment();
                    await AuditScanAsync(audit, job, "failure", $"扫描失败，第 {attempts} 次重试：{errType}");
                    return;
                }
            }

            await FailJobAsync(context, job, ex, jobCts.Token);
            await AuditScanAsync(audit, job, "failure", errType + ":" + errMsg);
            await CleanupStagingAsync(job.TenantId, job.DataSourceId, job.BatchVersion, jobCts.Token);
            return;
        }
        finally
        {
            try { if (!jobCts.IsCancellationRequested) jobCts.Cancel(); } catch { }
            _jobCts.TryRemove(jobId, out _);
            try { await heartbeatTask; } catch (OperationCanceledException) { } catch (Exception ex) { _logger.LogWarning(ex, "心跳任务异常（忽略）。"); }
        }

        if (cancelled)
        {
            if (job.Status == MetadataScanJobStatus.Cancelling)
            {
                // C3：用户取消 → Cancelled；staging 不可见、active 不变，回收 staging。
                job.Status = MetadataScanJobStatus.Cancelled;
                job.CancelledAt = DateTime.UtcNow;
                job.FinishedAt = DateTime.UtcNow;
                job.Stage = "Cancelled";
                job.ErrorCode = null;
                job.ErrorMessage = null;
                await context.SaveChangesAsync(stoppingToken);
                await CleanupStagingAsync(job.TenantId, job.DataSourceId, job.BatchVersion, stoppingToken);
                await AuditScanAsync(audit, job, "success", "扫描已取消。");
                _backlog.Decrement();
            }
            else
            {
                // 由宿主停止触发取消：保守标记 Failed(worker_interrupted) 并回收 staging。
                await FailJobAsync(context, job, "worker_interrupted", "扫描被宿主停止。", stoppingToken);
                await CleanupStagingAsync(job.TenantId, job.DataSourceId, job.BatchVersion, stoppingToken);
            }
            return;
        }

        // 成功：回写数据源最后扫描时间，并落盘最终计数。
        job.Status = tableFailures.Count == 0 ? MetadataScanJobStatus.Succeeded : MetadataScanJobStatus.PartiallySucceeded;
        job.FinishedAt = DateTime.UtcNow;
        job.ProgressPercent = 100;
        job.TablesScanned = telemetry.TablesScanned;
        job.ColumnsScanned = telemetry.ColumnsScanned;
        job.OrphansDetected = telemetry.OrphansDetected;
        job.Stage = job.Status.ToString();
        telemetry.UpdateTiming(job.StartedAt, 100);
        job.ProgressDetailsJson = telemetry.ToJson();
        job.ErrorCode = null;
        job.ErrorMessage = null;
        dataSource.LastScanAt = DateTime.UtcNow;
        await context.SaveChangesAsync(stoppingToken);
        await AuditScanAsync(audit, job, "success", tableFailures.Count == 0
            ? "扫描完成并激活。" : $"扫描部分成功并激活，失败表 {tableFailures.Count} 张。");
        _backlog.Decrement();
    }

    /// <summary>
    /// C4 心跳：周期性刷新任务 LastHeartbeatUtc（独立 scope 的定点 UPDATE，避免与扫描主 DbContext 并发）。
    /// 独立于逐表进度；慢表读取/向量调用期间仍按间隔刷新，确保不误判心跳超时。
    /// </summary>
    private async Task HeartbeatLoopAsync(long jobId, CancellationToken ct)
    {
        var interval = Math.Max(1, _heartbeatOptions.IntervalSeconds);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(interval), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                if (ct.IsCancellationRequested) break;
                try
                {
                    using var hbScope = _scopeFactory.CreateScope();
                    var hbCtx = hbScope.ServiceProvider.GetRequiredService<SuperBIContext>();
                    await hbCtx.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE MetadataScanJobs SET LastHeartbeatUtc = {DateTime.UtcNow} WHERE Id = {jobId}");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "心跳更新失败（忽略）。");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常结束。
        }
    }

    /// <summary>
    /// C4 重启恢复（ScanStartupReconciler）：遗留 Queued 分批重入队 + 遗留 Running/Retrying/Cancelling 标记终态
    /// + 周期心跳超时检查。与消费循环并发运行。
    /// </summary>
    private async Task ReconcileAsync(CancellationToken token)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SuperBIContext>();
            var audit = scope.ServiceProvider.GetService<IAuditLogService>();

            // 1) 遗留 Running/Retrying → Failed(worker_interrupted)（单工作实例重启，不自动重跑）。
            var interrupted = await context.MetadataScanJobs
                .Where(j => j.Status == MetadataScanJobStatus.Running || j.Status == MetadataScanJobStatus.Retrying)
                .ToListAsync(token);
            foreach (var j in interrupted)
            {
                j.Status = MetadataScanJobStatus.Failed;
                j.FailedReason = "worker_interrupted";
                j.FinishedAt = DateTime.UtcNow;
                j.Stage = "Failed";
                j.ErrorCode = "worker_interrupted";
                j.ErrorMessage = "工作进程重启，遗留运行任务标记为失败，不自动重跑。";
            }

            // 2) 遗留 Cancelling → Cancelled（沿用原取消请求）。
            var cancelling = await context.MetadataScanJobs
                .Where(j => j.Status == MetadataScanJobStatus.Cancelling)
                .ToListAsync(token);
            foreach (var j in cancelling)
            {
                j.Status = MetadataScanJobStatus.Cancelled;
                j.CancelledAt = DateTime.UtcNow;
                j.FinishedAt = DateTime.UtcNow;
                j.Stage = "Cancelled";
                j.ErrorCode = null;
                j.ErrorMessage = null;
            }

            if (interrupted.Count > 0 || cancelling.Count > 0)
                await context.SaveChangesAsync(token);

            // 3) 遗留 Queued 分批重入队（≤256/批，批间让出；与消费循环并发，避免 Bounded(1024) 阻塞启动）。
            const int batchSize = 256;
            while (!token.IsCancellationRequested)
            {
                var queued = await context.MetadataScanJobs
                    .Where(j => j.Status == MetadataScanJobStatus.Queued)
                    .OrderBy(j => j.Id)
                    .Take(batchSize)
                    .Select(j => j.Id)
                    .ToListAsync(token);
                if (queued.Count == 0) break;

                foreach (var id in queued)
                {
                    try
                    {
                        await _queue.EnqueueAsync(id, token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "遗留 Queued 任务 {JobId} 入队失败（忽略）。", id);
                    }
                }
                for (var i = 0; i < queued.Count; i++) _backlog?.Increment();
                await Task.Yield();
            }

            // 4) 周期心跳超时检查：失心跳超过阈值 → Failed(heartbeat_timeout)（迟到 worker 不得激活）。
            var timeoutMinutes = Math.Max(1, _heartbeatOptions.TimeoutMinutes);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                var threshold = DateTime.UtcNow.AddMinutes(-timeoutMinutes);
                var stale = await context.MetadataScanJobs
                    .Where(j => (j.Status == MetadataScanJobStatus.Running || j.Status == MetadataScanJobStatus.Retrying)
                        && (j.LastHeartbeatUtc == null ? j.StartedAt : j.LastHeartbeatUtc) < threshold)
                    .ToListAsync(token);
                foreach (var j in stale)
                {
                    j.Status = MetadataScanJobStatus.Failed;
                    j.FailedReason = "heartbeat_timeout";
                    j.FinishedAt = DateTime.UtcNow;
                    j.Stage = "Failed";
                    j.ErrorCode = "heartbeat_timeout";
                    j.ErrorMessage = $"超过 {timeoutMinutes} 分钟无心跳，标记为失败。";
                    j.LastHeartbeatUtc = j.LastHeartbeatUtc ?? j.StartedAt;
                }
                if (stale.Count > 0) await context.SaveChangesAsync(token);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常结束。
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "扫描启动对账循环异常。");
        }
    }

    /// <summary>
    /// Step 5 「GC 回收孤儿 staging」：未激活的 staging 元数据行（BatchVersion）未被任何引用（RLS/绑定/学习）指向，
    /// 安全删除；向量写持久 GC 待办（OrphanStagingGc，按版本精确清理），由 MetadataVectorGcJob 重启续跑。
    /// 使用独立 scope，避免与扫描主 DbContext 的跟踪实体相互干扰。
    /// </summary>
    private async Task CleanupStagingAsync(long tenantId, long dataSourceId, int batchVersion, CancellationToken ct)
    {
        if (batchVersion <= 0) return;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<SuperBIContext>();
            ctx.MetadataVectorGcRequests.Add(new MetadataVectorGcRequest
            {
                DataSourceId = dataSourceId,
                TenantId = tenantId,
                OldVersion = batchVersion,
                Reason = "OrphanStagingGc",
                Status = "Pending"
            });
            await ctx.MetadataSemantics
                .Where(s => s.MetadataColumn != null && s.MetadataColumn.MetadataTable != null
                    && s.MetadataColumn.MetadataTable.DataSourceId == dataSourceId
                    && s.MetadataVersion == batchVersion)
                .ExecuteDeleteAsync(ct);
            await ctx.MetadataColumns
                .Where(c => c.MetadataTable != null && c.MetadataTable.DataSourceId == dataSourceId
                    && c.MetadataVersion == batchVersion)
                .ExecuteDeleteAsync(ct);
            await ctx.MetadataTables
                .Where(t => t.DataSourceId == dataSourceId && t.MetadataVersion == batchVersion)
                .ExecuteDeleteAsync(ct);
            await ctx.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "孤儿 staging 回收失败（忽略，GC 待办仍处理向量）。");
        }
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

    /// <summary>C7：判定是否为快失败错误（连接/权限），此类错误不进入自动重试。</summary>
    private static bool IsFastFail(Exception ex, ScanRetryPolicy policy)
    {
        if (!policy.RetryOnConnectionError && IsConnectionError(ex)) return true;
        if (!policy.RetryOnPermissionError && IsPermissionError(ex)) return true;
        return false;
    }

    private static bool IsConnectionError(Exception ex)
    {
        var name = ex.GetType().Name;
        var msg = ex.Message;
        return name.Contains("Socket", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Connection", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Timeout", StringComparison.OrdinalIgnoreCase)
            || name.Contains("IOException", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)
            || name.Contains("MySql", StringComparison.OrdinalIgnoreCase)
            || name.Contains("SqlException", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("connection", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("连接", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("timeout", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPermissionError(Exception ex)
    {
        var name = ex.GetType().Name;
        var msg = ex.Message;
        return name.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Permission", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("permission", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("access denied", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("权限", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("拒绝", StringComparison.OrdinalIgnoreCase);
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

    private static async Task AuditScanAsync(IAuditLogService? audit, MetadataScanJob job, string result, string message)
    {
        if (audit is null) return;
        try
        {
            long? uid = null;
            if (long.TryParse(job.TriggeredBy, out var parsed)) uid = parsed;
            await audit.LogAsync(new AuditLogEntry(
                TenantId: job.TenantId,
                Action: "metadata:scan",
                EntityType: "DataSource",
                UserId: uid,
                Actor: job.TriggeredBy ?? "scheduler",
                EntityId: job.DataSourceId.ToString(),
                Result: result,
                Message: message), default);
        }
        catch
        {
            // 审计写入失败不应影响主流程。
        }
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
