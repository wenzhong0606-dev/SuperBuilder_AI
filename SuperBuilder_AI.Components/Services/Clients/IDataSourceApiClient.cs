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
}
