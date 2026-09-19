using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Components.Models;

namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 基于命名 HttpClient 的 API 客户端门面（M9-01：拆分 God ApiClient 后的向后兼容层）。
/// 自身不再实现任何业务逻辑，仅将 <see cref="IApiClient"/> 的 29 个方法委托给 7 个聚焦域客户端：
/// Identity / Admin / BI / App / Dashboard / Agent / DataSource。
/// 既有调用点（全部经 <c>@inject IApiClient</c> 注入）零改动即可继续工作。
/// </summary>
/// <remarks>
/// 鉴权：登录后写入 <see cref="AppState.Token"/>，每次请求自动附带 Bearer。
/// 租户恒由令牌承载（SB-P0-02C 已移除 X-Tenant-Id 自动头）。
/// 若需仅测试某个域，可直接注入对应的 <c>I*ApiClient</c> 聚焦接口，无需牵连其他域。
/// </remarks>
public sealed class ApiClient : IApiClient
{
    private readonly IIdentityApiClient _identity;
    private readonly IAdminApiClient _admin;
    private readonly IBiApiClient _bi;
    private readonly IAppApiClient _app;
    private readonly IDashboardApiClient _dashboard;
    private readonly IAgentApiClient _agent;
    private readonly IDataSourceApiClient _dataSource;

    public ApiClient(
        IIdentityApiClient identity,
        IAdminApiClient admin,
        IBiApiClient bi,
        IAppApiClient app,
        IDashboardApiClient dashboard,
        IAgentApiClient agent,
        IDataSourceApiClient dataSource)
    {
        _identity = identity;
        _admin = admin;
        _bi = bi;
        _app = app;
        _dashboard = dashboard;
        _agent = agent;
        _dataSource = dataSource;
    }

    public Task<(AuthResult? Result, string? Error, string? Code)> LoginAsync(string username, long tenantId, string? password = null, CancellationToken ct = default)
        => _identity.LoginAsync(username, tenantId, password, ct);

    public Task<(long Id, string? TenantCode, string? Name, string? Error)> ResolveTenantByCodeAsync(string code, CancellationToken ct = default)
        => _identity.ResolveTenantByCodeAsync(code, ct);

    public Task<string?> AskRawAsync(string question, long? dataSourceId, CancellationToken ct = default)
        => _bi.AskRawAsync(question, dataSourceId, ct);

    public Task<AskOutcome> AskAsync(string question, long? dataSourceId, string? conversationId = null, CancellationToken ct = default)
        => _bi.AskAsync(question, dataSourceId, conversationId, ct);

    public Task<AskOutcome> RefineAsync(string? question, string instruction, IEnumerable<RefineTurn>? history, long? dataSourceId, CancellationToken ct = default)
        => _bi.RefineAsync(question, instruction, history, dataSourceId, ct);

    public Task<(bool Ok, string? Code, string? Error)> PublishAppAsync(long tenantId, string dslJson, string? code, CancellationToken ct = default)
        => _app.PublishAppAsync(tenantId, dslJson, code, ct);

    public Task<T?> GetAsync<T>(string relativeUrl, CancellationToken ct = default) where T : class
        => _dashboard.GetAsync<T>(relativeUrl, ct);

    public Task<(bool Ok, int Status, string? Error, string? Code)> SendAsync(HttpMethod method, string relativeUrl, object? body = null, CancellationToken ct = default)
        => _agent.SendAsync(method, relativeUrl, body, ct);

    public Task<(bool Ok, int Status, string? Error, string? Code)> PostAsync(string relativeUrl, object? body = null, CancellationToken ct = default)
        => _agent.PostAsync(relativeUrl, body, ct);

    public Task<(bool Ok, int Status, string? Error, string? Code)> PutAsync(string relativeUrl, object? body, CancellationToken ct = default)
        => _agent.PutAsync(relativeUrl, body, ct);

    public Task<(bool Ok, int Status, string? Error, string? Code)> PatchAsync(string relativeUrl, object? body = null, CancellationToken ct = default)
        => _agent.PatchAsync(relativeUrl, body, ct);

    public Task<(bool Ok, int Status, string? Error, string? Code)> DeleteAsync(string relativeUrl, CancellationToken ct = default)
        => _agent.DeleteAsync(relativeUrl, ct);

    public Task<(JsonElement? Data, int Status, string? Error, string? Code)> PostJsonAsync(string relativeUrl, object? body = null, CancellationToken ct = default)
        => _agent.PostJsonAsync(relativeUrl, body, ct);

    public Task<(TenantSwitchResult? Result, string? Error, string? Code)> SwitchTenantAsync(long tenantId, CancellationToken ct = default)
        => _identity.SwitchTenantAsync(tenantId, ct);

    public Task<(SelfRegistrationResult? Result, string? Error, string? Code)> RegisterSelfAsync(
        string tenantCode, string tenantName, string adminUsername, string adminEmail,
        string adminPassword, string? adminDisplayName = null, CancellationToken ct = default)
        => _identity.RegisterSelfAsync(tenantCode, tenantName, adminUsername, adminEmail, adminPassword, adminDisplayName, ct);

    public Task<(SelfRegistrationConfigView? Result, string? Error)> GetSelfRegistrationConfigAsync(CancellationToken ct = default)
        => _identity.GetSelfRegistrationConfigAsync(ct);

    public Task<(DemoInstallPlan? Result, string? Error)> GetDemoDataPlanAsync(CancellationToken ct = default)
        => _admin.GetDemoDataPlanAsync(ct);

    public Task<(DemoInstallResult? Result, string? Error, string? Code)> InstallDemoDataAsync(CancellationToken ct = default)
        => _admin.InstallDemoDataAsync(ct);

    public Task<(string? Culture, string? Error)> GetUserLanguageAsync(CancellationToken ct = default)
        => _identity.GetUserLanguageAsync(ct);

    public Task<(string? Culture, string? Error, string? Code)> SetUserLanguageAsync(string culture, CancellationToken ct = default)
        => _identity.SetUserLanguageAsync(culture, ct);

    public Task<(IReadOnlyList<AdminLanguageView>? Result, string? Error)> GetAdminLanguagesAsync(CancellationToken ct = default)
        => _admin.GetAdminLanguagesAsync(ct);

    public Task<(bool Ok, string? Error)> CreateLanguageAsync(AdminLanguageCreate model, CancellationToken ct = default)
        => _admin.CreateLanguageAsync(model, ct);

    public Task<(bool Ok, string? Error)> UpdateLanguageAsync(long id, AdminLanguageUpdate model, CancellationToken ct = default)
        => _admin.UpdateLanguageAsync(id, model, ct);

    public Task<(bool Ok, string? Error)> SetLanguageEnabledAsync(long id, bool enabled, CancellationToken ct = default)
        => _admin.SetLanguageEnabledAsync(id, enabled, ct);

    public Task<(bool Ok, string? Error)> ReorderLanguagesAsync(IReadOnlyList<long> orderedIds, CancellationToken ct = default)
        => _admin.ReorderLanguagesAsync(orderedIds, ct);

    public Task<(IReadOnlyList<PublicLanguageView>? Result, string? Error)> GetPublicLanguagesAsync(CancellationToken ct = default)
        => _admin.GetPublicLanguagesAsync(ct);

    public Task<(JsonElement? Data, int Status, string? Error, string? Code)> GetJsonAsync(string relativeUrl, CancellationToken ct = default)
        => _dashboard.GetJsonAsync(relativeUrl, ct);

    public Task<(string? Text, int Status, string? Error)> GetTextAsync(string relativeUrl, CancellationToken ct = default)
        => _dashboard.GetTextAsync(relativeUrl, ct);

    public Task<(bool Ok, int Status, long? JobId, string? Error, string? Code)> StartScanAsync(long dataSourceId, CancellationToken ct = default)
        => _dataSource.StartScanAsync(dataSourceId, ct);

    public Task<(ScanJobView? Job, int Status, string? Error, string? Code)> GetScanJobAsync(long dataSourceId, long jobId, CancellationToken ct = default)
        => _dataSource.GetScanJobAsync(dataSourceId, jobId, ct);

    public Task<(ScanJobView? Job, int Status, string? Error, string? Code)> GetLatestScanJobAsync(long dataSourceId, CancellationToken ct = default)
        => _dataSource.GetLatestScanJobAsync(dataSourceId, ct);

    public Task<(bool Ok, int Status, string? Error, string? Code)> CancelScanAsync(long dataSourceId, long jobId, CancellationToken ct = default)
        => _dataSource.CancelScanAsync(dataSourceId, jobId, ct);

    public Task<(bool Ok, int Status, long? JobId, string? Error, string? Code)> RetryFailedScanAsync(long dataSourceId, long jobId, CancellationToken ct = default)
        => _dataSource.RetryFailedScanAsync(dataSourceId, jobId, ct);

    public Task<(bool Ok, int Status, string? Error, string? Code)> TestConnectionAsync(long dataSourceId, CancellationToken ct = default)
        => _dataSource.TestConnectionAsync(dataSourceId, ct);

    public Task<(bool Ok, int Status, string? Error, string? Code)> SetDataSourceEnabledAsync(long dataSourceId, bool enabled, CancellationToken ct = default)
        => _dataSource.SetDataSourceEnabledAsync(dataSourceId, enabled, ct);
}

/// <summary>
/// 多轮语义调整中的一轮对话（对应后端 <c>AskRefineTurn</c>）。
/// 仅 <c>Role=user</c> 的轮次参与后端问题合成；助手轮次仅用于前端展示。
/// </summary>
public sealed class RefineTurn
{
    public string Role { get; set; } = "user";
    public string? Content { get; set; }

    public static RefineTurn User(string content) => new() { Role = "user", Content = content };
    public static RefineTurn Assistant(string content) => new() { Role = "assistant", Content = content };
}

/// <summary>M4-05 扫描任务轮询视图（与后端 MetadataScanJob 状态端点对齐）。</summary>
public sealed class ScanJobView
{
    public long JobId { get; set; }
    public long DataSourceId { get; set; }
    public string Status { get; set; } = "Queued";
    public int ProgressPercent { get; set; }
    public string Stage { get; set; } = "Queued";
    public ScanProgressDetailsView ProgressDetails { get; set; } = new();
    public int TablesScanned { get; set; }
    public int ColumnsScanned { get; set; }
    public int OrphansDetected { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ScanFailureView> Failures { get; set; } = new();
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}

public sealed class ScanFailureView
{
    public string? Database { get; set; }
    public string? Schema { get; set; }
    public string? TableName { get; set; }
    public string? Stage { get; set; }
    public string? ErrorType { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
}

public sealed class ScanProgressDetailsView
{
    public string? StageMessage { get; set; }
    public int TablesDiscovered { get; set; }
    public int TablesProcessed { get; set; }
    public int ColumnsDiscovered { get; set; }
    public int ColumnsProcessed { get; set; }
    public int SemanticsTotal { get; set; }
    public int SemanticsProcessed { get; set; }
    public int VectorsTotal { get; set; }
    public int VectorsProcessed { get; set; }
    public string? CurrentObjectType { get; set; }
    public string? CurrentObjectName { get; set; }
    public int AddedTables { get; set; }
    public int UpdatedTables { get; set; }
    public int AddedColumns { get; set; }
    public int UpdatedColumns { get; set; }
    public int SemanticsGenerated { get; set; }
    public int VectorsIndexed { get; set; }
    public int OrphansRemoved { get; set; }
    public int WarningsCount { get; set; }
    public long ElapsedMs { get; set; }
    public long? EstimatedRemainingMs { get; set; }
    public List<ScanProgressEventView> Events { get; set; } = new();
}

public sealed class ScanProgressEventView
{
    public DateTime TimestampUtc { get; set; }
    public string Level { get; set; } = "Info";
    public string EventCode { get; set; } = "";
    public string Message { get; set; } = "";
    public int? Current { get; set; }
    public int? Total { get; set; }
}

/// <summary>登录 / 当前用户响应（与 api/auth 的 AuthResult 字段对齐）。</summary>
public sealed class AuthResult
{
    public string? Token { get; set; }
    public int ExpiresInSeconds { get; set; }
    /// <summary>刷新令牌明文（验收 #4）：登录/刷新响应中返回一次；本类反序列化自 API，须承载该字段以免续期链路断流。</summary>
    public string? RefreshToken { get; set; }
    public long TenantId { get; set; }
    /// <summary>M2-05：归属主租户（切换后 TenantId=生效租户、HomeTenantId=主租户）。</summary>
    public long HomeTenantId { get; set; }
    public long UserId { get; set; }
    public string Username { get; set; } = "";
    public System.Collections.Generic.List<string>? Permissions { get; set; }
    public System.Collections.Generic.List<string>? AvailableCultures { get; set; }
    public string? DefaultCulture { get; set; }
    /// <summary>访问令牌绝对过期时间（UTC，验收 #4）：登录响应未携带时由 <see cref="AuthStore.SetFromLoginAsync"/> 按 ExpiresInSeconds 派生。</summary>
    public System.DateTimeOffset? AccessTokenExpiresAtUtc { get; set; }
    /// <summary>刷新令牌绝对过期时间（UTC，验收 #4）：通常由配置派生，API 响应一般不包含。</summary>
    public System.DateTimeOffset? RefreshTokenExpiresAtUtc { get; set; }
}

/// <summary>M2-06 自助注册响应（字段与后端 SelfRegistrationResult 对齐，camelCase 解析）。</summary>
public sealed class SelfRegistrationResult
{
    public bool Success { get; set; }
    public string? Status { get; set; }
    public string? Token { get; set; }
    public int ExpiresInSeconds { get; set; }
    public long TenantId { get; set; }
    public long UserId { get; set; }
    public string? Username { get; set; }
    public System.Collections.Generic.List<string>? Permissions { get; set; }
    public System.Collections.Generic.List<string>? AvailableCultures { get; set; }
    public string? DefaultCulture { get; set; }
    public string? Error { get; set; }
}

/// <summary>M2-06 自助注册配置视图（平台管理员只读）。</summary>
public sealed class SelfRegistrationConfigView
{
    public bool Enabled { get; set; }
    public System.Collections.Generic.List<string>? AllowedEmailDomains { get; set; }
    public string? DefaultCulture { get; set; }
    public System.Collections.Generic.List<string>? DefaultAvailableCultures { get; set; }
    public bool ApprovalRequired { get; set; }
    public bool RequireCaptcha { get; set; }
}

/// <summary>M2-07 演示数据安装预览项。</summary>
public sealed class DemoPlanItem
{
    public DemoPlanItem() { }
    public DemoPlanItem(string? entityType, int count, string? description)
    {
        EntityType = entityType;
        Count = count;
        Description = description;
    }
    public string? EntityType { get; set; }
    public int Count { get; set; }
    public string? Description { get; set; }
}

/// <summary>M2-07 演示数据安装预览计划。</summary>
public sealed class DemoInstallPlan
{
    public bool AlreadyInstalled { get; set; }
    public string? DemoTenantCode { get; set; }
    public string? DemoTenantName { get; set; }
    public string? AdminUsername { get; set; }
    public string? AdminEmail { get; set; }
    public System.Collections.Generic.List<DemoPlanItem>? Items { get; set; }
}

/// <summary>M2-07 演示数据安装结果。</summary>
public sealed class DemoInstallResult
{
    public bool Success { get; set; }
    public string? Status { get; set; }
    public long TenantId { get; set; }
    public long UserId { get; set; }
    public string? Username { get; set; }
    public string? AdminPassword { get; set; }
    public string? Error { get; set; }
}

/// <summary>M2-05 切换租户成功响应（含重签令牌与切换后端租户上下文）。</summary>
public sealed class TenantSwitchResult
{
    public string? Token { get; set; }
    public int ExpiresInSeconds { get; set; }
    public long TenantId { get; set; }
    public long HomeTenantId { get; set; }
    public long UserId { get; set; }
    public string? Username { get; set; }
    public System.Collections.Generic.List<string>? Permissions { get; set; }
    public System.Collections.Generic.List<string>? AvailableCultures { get; set; }
    public string? DefaultCulture { get; set; }
}
