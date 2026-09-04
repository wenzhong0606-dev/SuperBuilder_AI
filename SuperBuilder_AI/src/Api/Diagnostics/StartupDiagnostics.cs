namespace SuperBuilder_AI.Api.Diagnostics;

/// <summary>
/// 启动期可诊断状态。用于区分「数据库不可达 / Schema 未创建 / 种子不完整 / 就绪」，
/// 使 Web 可在受限诊断模式下启动，而不因缺库或空库直接崩溃。
/// </summary>
public enum BootstrapState
{
    /// <summary>尚未探测（理论不应出现在运行期）。</summary>
    Unknown,

    /// <summary>数据库不可达（连接失败）。</summary>
    DatabaseUnreachable,

    /// <summary>数据库可达，但迁移历史为空或不存在（Schema 未创建/未应用）。</summary>
    SchemaNotCreated,

    /// <summary>Schema 已就绪，但平台目录/本地化/配额/引导等种子不完整。</summary>
    SeedIncomplete,

    /// <summary>Schema 与全部种子就绪，平台可正常服务。</summary>
    Ready,
}

/// <summary>
/// 启动诊断单例。由 <c>Program.cs</c> 在启动序列中填充，供 <c>/health</c> 与
/// <c>/api/platform-bootstrap/status</c> 等端点读取，向运维暴露受限诊断信息。
/// 该对象绝不向普通用户输出异常堆栈。
/// </summary>
public sealed class StartupDiagnostics
{
    private readonly List<string> _completedSteps = new();

    /// <summary>当前启动状态。</summary>
    public BootstrapState State { get; set; } = BootstrapState.Unknown;

    /// <summary>可诊断原因（用于日志与诊断端点，不含堆栈）。</summary>
    public string? Reason { get; set; }

    /// <summary>原始异常（仅记录，不向普通用户输出）。</summary>
    public Exception? Exception { get; set; }

    /// <summary>已成功完成的启动步骤（顺序：Schema → Identity → Localization → Quota → Bootstrap）。</summary>
    public IReadOnlyList<string> CompletedSteps => _completedSteps;

    /// <summary>是否足以对外提供服务（Schema 与全部关键种子就绪，或仅种子不完整但主机可运行）。</summary>
    public bool IsOperational => State is BootstrapState.Ready or BootstrapState.SeedIncomplete;

    public void MarkStep(string step) => _completedSteps.Add(step);
}
