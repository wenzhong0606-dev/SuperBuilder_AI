namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 数据源/元数据域客户端契约（M9-01，增量内聚）：后台元数据扫描的触发与轮询。
/// 与 <see cref="IApiClient"/> 中对应方法签名一致，可独立于其他域单独测试/替换。
/// </summary>
public interface IDataSourceApiClient
{
    /// <summary>M4-05 触发后台扫描：POST 创建任务并入队，从 202 响应体解析 jobId。</summary>
    Task<(bool Ok, int Status, long? JobId, string? Error, string? Code)> StartScanAsync(long dataSourceId, CancellationToken ct = default);

    /// <summary>M4-05 轮询扫描任务状态；成功解析为 <see cref="ScanJobView"/>。</summary>
    Task<(ScanJobView? Job, int Status, string? Error, string? Code)> GetScanJobAsync(long dataSourceId, long jobId, CancellationToken ct = default);

    /// <summary>C5 获取最新扫描任务（不限终态），供页面进入后恢复续显。</summary>
    Task<(ScanJobView? Job, int Status, string? Error, string? Code)> GetLatestScanJobAsync(long dataSourceId, CancellationToken ct = default);

    /// <summary>C3 软取消进行中的扫描任务。</summary>
    Task<(bool Ok, int Status, string? Error, string? Code)> CancelScanAsync(long dataSourceId, long jobId, CancellationToken ct = default);

    /// <summary>C7 仅重扫失败项（从 active 续种）。</summary>
    Task<(bool Ok, int Status, long? JobId, string? Error, string? Code)> RetryFailedScanAsync(long dataSourceId, long jobId, CancellationToken ct = default);

    /// <summary>测试数据源连通性（使用已存储连接串）。</summary>
    Task<(bool Ok, int Status, string? Error, string? Code)> TestConnectionAsync(long dataSourceId, CancellationToken ct = default);

    /// <summary>C8 启用/禁用数据源（disable 保留元数据；cleanup 另走删除端点）。</summary>
    Task<(bool Ok, int Status, string? Error, string? Code)> SetDataSourceEnabledAsync(long dataSourceId, bool enabled, CancellationToken ct = default);
}
