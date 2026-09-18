using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using SuperBuilder_AI.Components.Models;

namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 与 SuperBuilder_AI API 通信的客户端契约（RCL 共享，两个 Head 各自注册实现）。
/// 鉴权：登录后写入 <see cref="AppState.Token"/>，每次请求自动附带 Bearer。
/// 租户恒由令牌承载（SB-P0-02C 已移除 X-Tenant-Id 自动头）。
/// </summary>
public interface IApiClient
{
    Task<(AuthResult? Result, string? Error, string? Code)> LoginAsync(string username, long tenantId, string? password = null, CancellationToken ct = default);
    /// <summary>M6 登录兜底：按已知租户编码（不枚举目录）解析租户 Id，供 <c>Auth:ShowTenantDirectory</c> 关闭时手动登录。</summary>
    Task<(long Id, string? TenantCode, string? Name, string? Error)> ResolveTenantByCodeAsync(string code, CancellationToken ct = default);
    Task<string?> AskRawAsync(string question, long? dataSourceId, CancellationToken ct = default);
    /// <summary>类型化问数：返回 <see cref="BIResponse"/> 并区分传输错误。</summary>
    Task<AskOutcome> AskAsync(string question, long? dataSourceId, string? conversationId = null, CancellationToken ct = default);
    /// <summary>
    /// 多轮语义调整：在已有问题（+ 历史上下文）上追加细化指令，重新走完整 BI 链路。
    /// 仅在用户显式发起「细化」时调用（<c>POST api/ask/refine</c>）；默认问数路径 <c>api/ask</c> 不变。
    /// </summary>
    Task<AskOutcome> RefineAsync(string? question, string instruction, IEnumerable<RefineTurn>? history, long? dataSourceId, CancellationToken ct = default);
    /// <summary>发布为应用：结构化 App DSL 经默认路径（P8）保存到 api/apps。</summary>
    Task<(bool Ok, string? Code, string? Error)> PublishAppAsync(long tenantId, string dslJson, string? code, CancellationToken ct = default);
    Task<T?> GetAsync<T>(string relativeUrl, CancellationToken ct = default) where T : class;

    /// <summary>
    /// 通用写操作（POST/PUT/PATCH/DELETE）：成功返回 Ok=true，否则返回 Status、统一错误体中的 message 与错误码 code。
    /// <c>code</c> 为后端 <see cref="ErrorCodes"/> 的 <c>SB_*</c> 常量，供前端按 <c>L10n.T("Error."+code, message)</c> 取本地化友好提示。
    /// 401 由实现统一触发会话失效回收（与读路径一致）。
    /// </summary>
    Task<(bool Ok, int Status, string? Error, string? Code)> SendAsync(HttpMethod method, string relativeUrl, object? body = null, CancellationToken ct = default);

    /// <summary>POST JSON（body 为 null 时发送空请求）。</summary>
    Task<(bool Ok, int Status, string? Error, string? Code)> PostAsync(string relativeUrl, object? body = null, CancellationToken ct = default);

    /// <summary>PUT JSON（整体更新）。</summary>
    Task<(bool Ok, int Status, string? Error, string? Code)> PutAsync(string relativeUrl, object? body, CancellationToken ct = default);

    /// <summary>PATCH JSON（局部更新，如启用/禁用）。</summary>
    Task<(bool Ok, int Status, string? Error, string? Code)> PatchAsync(string relativeUrl, object? body = null, CancellationToken ct = default);

    /// <summary>DELETE。</summary>
    Task<(bool Ok, int Status, string? Error, string? Code)> DeleteAsync(string relativeUrl, CancellationToken ct = default);

    /// <summary>POST 并读取响应体（如创建后返回的资源详情），供需要解析返回内容的写操作使用。</summary>
    Task<(JsonElement? Data, int Status, string? Error, string? Code)> PostJsonAsync(string relativeUrl, object? body = null, CancellationToken ct = default);

    /// <summary>M2-05 切换生效租户：校验成员资格后由后端重签令牌，返回新令牌与切换后端租户上下文。</summary>
    Task<(TenantSwitchResult? Result, string? Error, string? Code)> SwitchTenantAsync(long tenantId, CancellationToken ct = default);

    /// <summary>M2-06 自助注册：匿名创建新租户与首位管理员，注册即登录（后端重签令牌）。</summary>
    Task<(SelfRegistrationResult? Result, string? Error, string? Code)> RegisterSelfAsync(
        string tenantCode, string tenantName, string adminUsername, string adminEmail,
        string adminPassword, string? adminDisplayName = null, CancellationToken ct = default);

    /// <summary>M2-06 平台管理员查看自助注册配置（需 platform:admin:manage）。</summary>
    Task<(SelfRegistrationConfigView? Result, string? Error)> GetSelfRegistrationConfigAsync(CancellationToken ct = default);

    /// <summary>M2-07 平台管理员预览演示数据安装计划（需 platform:admin:manage）。</summary>
    Task<(DemoInstallPlan? Result, string? Error)> GetDemoDataPlanAsync(CancellationToken ct = default);

    /// <summary>M2-07 平台管理员触发演示数据安装（事务原子、重复执行保护，需 platform:admin:manage）。</summary>
    Task<(DemoInstallResult? Result, string? Error, string? Code)> InstallDemoDataAsync(CancellationToken ct = default);

    /// <summary>M3-G0 读取当前用户的服务端语言偏好（已认证；无记录时 Culture 为 null）。</summary>
    Task<(string? Culture, string? Error)> GetUserLanguageAsync(CancellationToken ct = default);

    /// <summary>M3-G0 持久化当前用户语言偏好到服务端（强制校验租户可用语言范围，越界回退默认）。</summary>
    Task<(string? Culture, string? Error, string? Code)> SetUserLanguageAsync(string culture, CancellationToken ct = default);

    /// <summary>M3-02 平台管理员查看全部语言目录（含已停用与翻译进度）。</summary>
    Task<(IReadOnlyList<AdminLanguageView>? Result, string? Error)> GetAdminLanguagesAsync(CancellationToken ct = default);

    /// <summary>M3-02 平台管理员新建语言（BCP 47 归一化 + 必填名 + 复制键集合待翻译）。</summary>
    Task<(bool Ok, string? Error)> CreateLanguageAsync(AdminLanguageCreate model, CancellationToken ct = default);

    /// <summary>M3-02 平台管理员更新语言显示名/本地名。</summary>
    Task<(bool Ok, string? Error)> UpdateLanguageAsync(long id, AdminLanguageUpdate model, CancellationToken ct = default);

    /// <summary>M3-02 平台管理员启用/停用语言（停用委托租户关系迁移）。</summary>
    Task<(bool Ok, string? Error)> SetLanguageEnabledAsync(long id, bool enabled, CancellationToken ct = default);

    /// <summary>M3-02 平台管理员按 Id 顺序重排语言目录。</summary>
    Task<(bool Ok, string? Error)> ReorderLanguagesAsync(IReadOnlyList<long> orderedIds, CancellationToken ct = default);

    /// <summary>读取平台公开语言目录（含本地名称），供语言切换器展示 NativeName。</summary>
    Task<(IReadOnlyList<PublicLanguageView>? Result, string? Error)> GetPublicLanguagesAsync(CancellationToken ct = default);
    /// <summary>
    /// 松类型读取：GET 任意端点并以 <see cref="JsonElement"/> 返回（数组或对象皆可）。
    /// 不抛异常——HTTP 非 2xx 与网络/解析错误一律通过 err 返回；非 2xx 时同时透传后端错误码 <c>code</c>（<c>SB_*</c>），
    /// 供页面按 <c>L10n.T("Error."+code, err)</c> 取本地化友好提示。
    /// </summary>
    Task<(JsonElement? Data, int Status, string? Error, string? Code)> GetJsonAsync(string relativeUrl, CancellationToken ct = default);

    /// <summary>
    /// 纯文本读取（如 <c>/metrics</c> 的 Prometheus 文本、<c>/health</c> 的探针响应）。
    /// 与 <see cref="GetJsonAsync"/> 的区别是不做 JSON 解析，非 2xx 返回错误信息而非抛出。
    /// </summary>
    Task<(string? Text, int Status, string? Error)> GetTextAsync(string relativeUrl, CancellationToken ct = default);

    /// <summary>M4-05 触发后台元数据扫描：创建扫描任务并入队，返回 202 与任务 Id。</summary>
    Task<(bool Ok, int Status, long? JobId, string? Error, string? Code)> StartScanAsync(long dataSourceId, CancellationToken ct = default);

    /// <summary>M4-05 轮询扫描任务状态（进度/计数/脱敏错误）。</summary>
    Task<(ScanJobView? Job, int Status, string? Error, string? Code)> GetScanJobAsync(long dataSourceId, long jobId, CancellationToken ct = default);

    /// <summary>C5 获取最新扫描任务（不限终态），供进入页面后恢复续显。</summary>
    Task<(ScanJobView? Job, int Status, string? Error, string? Code)> GetLatestScanJobAsync(long dataSourceId, CancellationToken ct = default);

    /// <summary>C3 软取消进行中的扫描任务。</summary>
    Task<(bool Ok, int Status, string? Error, string? Code)> CancelScanAsync(long dataSourceId, long jobId, CancellationToken ct = default);

    /// <summary>C7 仅重扫失败项（从 active 续种）。</summary>
    Task<(bool Ok, int Status, long? JobId, string? Error, string? Code)> RetryFailedScanAsync(long dataSourceId, long jobId, CancellationToken ct = default);

    /// <summary>测试数据源连通性。</summary>
    Task<(bool Ok, int Status, string? Error, string? Code)> TestConnectionAsync(long dataSourceId, CancellationToken ct = default);

    /// <summary>C8 启用/禁用数据源。</summary>
    Task<(bool Ok, int Status, string? Error, string? Code)> SetDataSourceEnabledAsync(long dataSourceId, bool enabled, CancellationToken ct = default);
}
