namespace SuperBuilder_AI.Application.Common.Options;

/// <summary>
/// 扫描心跳与超时配置（C4 重启恢复）。单工作实例下，worker 周期性刷新任务心跳；
/// 对账循环对失心跳超过阈值的 Running/Retrying 任务标记为 Failed(heartbeat_timeout)。
/// </summary>
public class ScanHeartbeatOptions
{
    /// <summary>心跳超时阈值（分钟）。默认 30，须为正值；超过该时长无心跳（且无取消）即判为停滞。</summary>
    public int TimeoutMinutes { get; set; } = 30;

    /// <summary>worker 刷新心跳的间隔（秒）。默认 30，须为正值且应小于 TimeoutMinutes。</summary>
    public int IntervalSeconds { get; set; } = 30;
}
