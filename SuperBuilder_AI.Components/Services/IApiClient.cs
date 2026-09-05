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
    Task<(AuthResult? Result, string? Error)> LoginAsync(string username, long tenantId, string? password = null, CancellationToken ct = default);
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
    /// 通用写操作（POST/PUT/PATCH/DELETE）：成功返回 Ok=true，否则返回 Status 与统一错误体中的 message。
    /// 401 由实现统一触发会话失效回收（与读路径一致）。
    /// </summary>
    Task<(bool Ok, int Status, string? Error)> SendAsync(HttpMethod method, string relativeUrl, object? body = null, CancellationToken ct = default);

    /// <summary>POST JSON（body 为 null 时发送空请求）。</summary>
    Task<(bool Ok, int Status, string? Error)> PostAsync(string relativeUrl, object? body = null, CancellationToken ct = default);

    /// <summary>PUT JSON（整体更新）。</summary>
    Task<(bool Ok, int Status, string? Error)> PutAsync(string relativeUrl, object? body, CancellationToken ct = default);

    /// <summary>PATCH JSON（局部更新，如启用/禁用）。</summary>
    Task<(bool Ok, int Status, string? Error)> PatchAsync(string relativeUrl, object? body = null, CancellationToken ct = default);

    /// <summary>DELETE。</summary>
    Task<(bool Ok, int Status, string? Error)> DeleteAsync(string relativeUrl, CancellationToken ct = default);

    /// <summary>M2-05 切换生效租户：校验成员资格后由后端重签令牌，返回新令牌与切换后端租户上下文。</summary>
    Task<(TenantSwitchResult? Result, string? Error)> SwitchTenantAsync(long tenantId, CancellationToken ct = default);

    /// <summary>M2-06 自助注册：匿名创建新租户与首位管理员，注册即登录（后端重签令牌）。</summary>
    Task<(SelfRegistrationResult? Result, string? Error)> RegisterSelfAsync(
        string tenantCode, string tenantName, string adminUsername, string adminEmail,
        string adminPassword, string? adminDisplayName = null, CancellationToken ct = default);

    /// <summary>M2-06 平台管理员查看自助注册配置（需 platform:admin:manage）。</summary>
    Task<(SelfRegistrationConfigView? Result, string? Error)> GetSelfRegistrationConfigAsync(CancellationToken ct = default);

    /// <summary>M2-07 平台管理员预览演示数据安装计划（需 platform:admin:manage）。</summary>
    Task<(DemoInstallPlan? Result, string? Error)> GetDemoDataPlanAsync(CancellationToken ct = default);

    /// <summary>M2-07 平台管理员触发演示数据安装（事务原子、重复执行保护，需 platform:admin:manage）。</summary>
    Task<(DemoInstallResult? Result, string? Error)> InstallDemoDataAsync(CancellationToken ct = default);

    /// <summary>M3-G0 读取当前用户的服务端语言偏好（已认证；无记录时 Culture 为 null）。</summary>
    Task<(string? Culture, string? Error)> GetUserLanguageAsync(CancellationToken ct = default);

    /// <summary>M3-G0 持久化当前用户语言偏好到服务端（强制校验租户可用语言范围，越界回退默认）。</summary>
    Task<(string? Culture, string? Error)> SetUserLanguageAsync(string culture, CancellationToken ct = default);
    /// <summary>
    /// 松类型读取：GET 任意端点并以 <see cref="JsonElement"/> 返回（数组或对象皆可）。
    /// 不抛异常——HTTP 非 2xx 与网络/解析错误一律通过 err 返回，便于页面优雅降级。
    /// </summary>
    Task<(JsonElement? Data, int Status, string? Error)> GetJsonAsync(string relativeUrl, CancellationToken ct = default);

    /// <summary>
    /// 纯文本读取（如 <c>/metrics</c> 的 Prometheus 文本、<c>/health</c> 的探针响应）。
    /// 与 <see cref="GetJsonAsync"/> 的区别是不做 JSON 解析，非 2xx 返回错误信息而非抛出。
    /// </summary>
    Task<(string? Text, int Status, string? Error)> GetTextAsync(string relativeUrl, CancellationToken ct = default);
}
