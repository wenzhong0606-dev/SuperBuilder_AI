using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Components.Models;

namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 基于命名 HttpClient 的 API 客户端实现。Head 项目负责注册名为 "SuperBuilderApi" 的 HttpClient（BaseAddress=API 地址）。
/// 租户隔离由服务端从令牌 <c>tid</c> 声明派生（数据面单租户恒等），前端不再发送 X-Tenant-Id 头。
/// </summary>
/// <remarks>
/// SB-P0-02C：已移除 <c>X-Tenant-Id</c> 自动头。因不存在合法跨租户切换，移除后无需替代通道；
/// 后端 <c>AuthMiddleware</c> / <c>TenantDataPlanePolicy</c> 一律以令牌租户为唯一事实源，
/// 请求中的租户值即便存在也只会被记为「请求值(RequestedTenantId)」，不影响执行租户。
/// </remarks>
public sealed class ApiClient : IApiClient
{
    private readonly IHttpClientFactory _factory;
    private readonly AppState _appState;

    public ApiClient(IHttpClientFactory factory, AppState appState)
    {
        _factory = factory;
        _appState = appState;
    }

    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient("SuperBuilderApi");
        client.DefaultRequestHeaders.Authorization = null;
        // SB-P0-02C：不再发送 X-Tenant-Id（租户恒由令牌承载，前端无替代通道）
        if (!string.IsNullOrEmpty(_appState.Token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _appState.Token);
        return client;
    }

    /// <summary>
    /// 会话失效回收：收到 401 时清空本地 token 并通知壳层跳登录。
    /// 仅当当前确为已登录态才触发，避免重复通知。
    /// </summary>
    private void OnUnauthorized()
    {
        if (!_appState.IsAuthenticated) return;
        _appState.ClearSession();
        _appState.NotifySessionExpired();
    }

    public async Task<(AuthResult? Result, string? Error)> LoginAsync(string username, long tenantId, string? password = null, CancellationToken ct = default)
    {
        var client = _factory.CreateClient("SuperBuilderApi");
        try
        {
            var resp = await client.PostAsJsonAsync("api/auth/login", new { username, tenantId, password }, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var (code, msg, _) = ParseApiError(await resp.Content.ReadAsStringAsync(ct));
                return (null, msg ?? $"登录失败（{(int)resp.StatusCode}）。");
            }
            var r = await resp.Content.ReadFromJsonAsync<AuthResult>(ct);
            return (r, null);
        }
        catch (HttpRequestException ex)
        {
            return (null, "无法连接登录服务，请确认 API 服务已启动且地址配置正确。" +
                (string.IsNullOrWhiteSpace(ex.Message) ? "" : $"（{ex.Message}）"));
        }
    }

    public async Task<string?> AskRawAsync(string question, long? dataSourceId, CancellationToken ct = default)
    {
        var client = CreateClient();
        var payload = new { question, dataSourceId = dataSourceId ?? 0L };
        var resp = await client.PostAsJsonAsync("api/ask", payload, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized();
            return "ERROR " + (int)resp.StatusCode + ": " + err;
        }
        return await resp.Content.ReadAsStringAsync(ct);
    }

    /// <summary>类型化问数：反序列化为 <see cref="BIResponse"/>，并对非成功状态解析统一错误码。</summary>
    public async Task<AskOutcome> AskAsync(string question, long? dataSourceId, string? conversationId = null, CancellationToken ct = default)
    {
        var client = CreateClient();
        var resp = await client.PostAsJsonAsync("api/ask", new { question, dataSourceId = dataSourceId ?? 0L, conversationId }, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var (code, msg, trace) = ParseApiError(await resp.Content.ReadAsStringAsync(ct));
            if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized();
            return new AskOutcome
            {
                Code = code,
                TraceId = trace,
                Error = msg ?? $"问数失败（{(int)resp.StatusCode}）。"
            };
        }

        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrEmpty(raw))
            return new AskOutcome { Error = "请求失败：空响应。" };

        try
        {
            var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var response = JsonSerializer.Deserialize<BIResponse>(raw, opt);
            if (response is null)
                return new AskOutcome { Error = "响应解析失败。" };
            return new AskOutcome { Response = response };
        }
        catch (JsonException ex)
        {
            return new AskOutcome { Error = "响应解析失败：" + ex.Message };
        }
    }

    /// <summary>
    /// 多轮语义调整：在已有问题（+ 历史上下文）上追加细化指令，重新走完整 BI 链路。
    /// 仅在用户显式发起「细化」时调用（对应 <c>POST api/ask/refine</c>）；默认问数路径 <c>api/ask</c> 不变。
    /// </summary>
    /// <param name="question">原始问题，可空（仅凭历史 + 指令亦可）。</param>
    /// <param name="instruction">本轮细化指令，必填。如「只看华东地区」。</param>
    /// <param name="history">可选历史轮次，用于补全指代；仅 role=user 参与后端问题合成。</param>
    public async Task<AskOutcome> RefineAsync(
        string? question,
        string instruction,
        IEnumerable<RefineTurn>? history,
        long? dataSourceId,
        CancellationToken ct = default)
    {
        var client = CreateClient();
        var payload = new
        {
            question,
            instruction,
            history = history?.Select(t => new { role = t.Role, content = t.Content }).ToList(),
            dataSourceId = dataSourceId ?? 0L
        };

        var resp = await client.PostAsJsonAsync("api/ask/refine", payload, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var (code, msg, trace) = ParseApiError(await resp.Content.ReadAsStringAsync(ct));
            if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized();
            return new AskOutcome
            {
                Code = code,
                TraceId = trace,
                Error = msg ?? $"语义调整失败（{(int)resp.StatusCode}）。"
            };
        }

        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrEmpty(raw))
            return new AskOutcome { Error = "请求失败：空响应。" };

        try
        {
            var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var response = JsonSerializer.Deserialize<BIResponse>(raw, opt);
            if (response is null)
                return new AskOutcome { Error = "响应解析失败。" };
            return new AskOutcome { Response = response };
        }
        catch (JsonException ex)
        {
            return new AskOutcome { Error = "响应解析失败：" + ex.Message };
        }
    }

    /// <summary>
    /// 解析后端统一错误体：优先 <see cref="ApiError"/>(code/message/traceId)，
    /// 其次兼容旧 <c>{ error }</c> 形状，最后回退到原始文本（截断避免过长）。
    /// </summary>
    private static (string? Code, string? Message, string? TraceId) ParseApiError(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return (null, null, null);
        try
        {
            var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var e = JsonSerializer.Deserialize<ApiError>(body, opt);
            if (e is not null && (!string.IsNullOrEmpty(e.Code) || !string.IsNullOrEmpty(e.Message)))
                return (e.Code, e.Message, e.TraceId);
            // 兼容旧 { error: "..." }
            var raw = JsonSerializer.Deserialize<JsonElement>(body, opt);
            if (raw.ValueKind == JsonValueKind.Object)
            {
                if (raw.TryGetProperty("error", out var ev) && ev.ValueKind == JsonValueKind.String)
                    return (null, ev.GetString(), null);
                // 后端写操作普遍以 { errors: ["..."] } 扁平数组返回，逐条合并为可读文本，
                // 替代此前回退的原始 JSON 截断文本（如 AppBuilder/Agent/Tenant 等校验失败）。
                if (raw.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array)
                {
                    var msgs = new List<string>();
                    foreach (var item in errs.EnumerateArray())
                        if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                            msgs.Add(item.GetString()!);
                    if (msgs.Count > 0)
                        return (null, string.Join("；", msgs), null);
                }
            }
        }
        catch
        {
            // 非 JSON 则回退原始文本
        }
        var trimmed = body.Length > 240 ? body[..240] + "…" : body;
        return (null, trimmed, null);
    }

    /// <summary>发布为应用：将结构化 App DSL 经默认路径（P8 <c>BuildFromDslAsync</c>）保存到 <c>api/apps</c>。</summary>
    public async Task<(bool Ok, string? Code, string? Error)> PublishAppAsync(
        long tenantId, string dslJson, string? code, CancellationToken ct = default)
    {
        var client = CreateClient();
        var body = new { tenantId, dslJson, code };
        var resp = await client.PostAsJsonAsync("api/apps", body, ct);
        if (resp.IsSuccessStatusCode)
        {
            try
            {
                var detail = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
                var c = detail.TryGetProperty("code", out var ce) ? ce.GetString() : code;
                return (true, c ?? code, null);
            }
            catch
            {
                return (true, code, null);
            }
        }
        var err = await resp.Content.ReadAsStringAsync(ct);
        if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized();
        return (false, null, $"{(int)resp.StatusCode}: {err}");
    }

    public async Task<T?> GetAsync<T>(string relativeUrl, CancellationToken ct = default) where T : class
    {
        var client = CreateClient();
        try
        {
            var resp = await client.GetAsync(relativeUrl, ct);
            if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized();
            if (!resp.IsSuccessStatusCode) return null;
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(body)) return null;
            return JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 通用写操作：统一处理 401 回收与错误体解析，避免每个页面重复实现。
    /// DELETE 与 GET 不发送请求体。
    /// </summary>
    public async Task<(bool Ok, int Status, string? Error)> SendAsync(
        HttpMethod method, string relativeUrl, object? body = null, CancellationToken ct = default)
    {
        var client = CreateClient();
        try
        {
            using var req = new HttpRequestMessage(method, relativeUrl);
            if (body is not null && method != HttpMethod.Get && method != HttpMethod.Delete)
                req.Content = JsonContent.Create(body);

            var resp = await client.SendAsync(req, ct);
            if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized();
            if (!resp.IsSuccessStatusCode)
            {
                var raw = await resp.Content.ReadAsStringAsync(ct);
                var (_, msg, _) = ParseApiError(raw);
                return (false, (int)resp.StatusCode, msg ?? $"请求失败（{(int)resp.StatusCode}）。");
            }
            return (true, (int)resp.StatusCode, null);
        }
        catch (Exception ex)
        {
            return (false, 0, "网络错误：" + ex.Message);
        }
    }

    public Task<(bool Ok, int Status, string? Error)> PostAsync(string relativeUrl, object? body = null, CancellationToken ct = default)
        => SendAsync(HttpMethod.Post, relativeUrl, body, ct);

    public Task<(bool Ok, int Status, string? Error)> PutAsync(string relativeUrl, object? body, CancellationToken ct = default)
        => SendAsync(HttpMethod.Put, relativeUrl, body, ct);

    public Task<(bool Ok, int Status, string? Error)> PatchAsync(string relativeUrl, object? body = null, CancellationToken ct = default)
        => SendAsync(HttpMethod.Patch, relativeUrl, body, ct);

    public Task<(bool Ok, int Status, string? Error)> DeleteAsync(string relativeUrl, CancellationToken ct = default)
        => SendAsync(HttpMethod.Delete, relativeUrl, null, ct);

    /// <summary>
    /// M2-05 租户切换：校验当前用户是否为目标租户成员，校验通过则后端重签令牌（tid=目标，htid=主租户）并返回新令牌。
    /// 非成员将收到 403（调用方转为错误提示，不抛异常）。
    /// </summary>
    public async Task<(TenantSwitchResult? Result, string? Error)> SwitchTenantAsync(long tenantId, CancellationToken ct = default)
    {
        var client = CreateClient();
        var resp = await client.PostAsJsonAsync("api/tenant-membership/switch", new { tenantId }, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var (_, msg, _) = ParseApiError(await resp.Content.ReadAsStringAsync(ct));
            if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized();
            return (null, msg ?? $"切换失败（{(int)resp.StatusCode}）。");
        }
        var r = await resp.Content.ReadFromJsonAsync<TenantSwitchResult>(ct);
        return (r, null);
    }

    /// <summary>M2-06 自助注册：匿名创建新租户与首位管理员，注册即登录（后端重签令牌）。</summary>
    public async Task<(SelfRegistrationResult? Result, string? Error)> RegisterSelfAsync(
        string tenantCode, string tenantName, string adminUsername, string adminEmail,
        string adminPassword, string? adminDisplayName = null, CancellationToken ct = default)
    {
        var client = _factory.CreateClient("SuperBuilderApi");
        try
        {
            var resp = await client.PostAsJsonAsync("api/self-registration/register", new
            {
                tenantCode,
                tenantName,
                adminUsername,
                adminEmail,
                adminPassword,
                adminDisplayName,
            }, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var (_, msg, _) = ParseApiError(await resp.Content.ReadAsStringAsync(ct));
                return (null, msg ?? $"注册失败（{(int)resp.StatusCode}）。");
            }
            var r = await resp.Content.ReadFromJsonAsync<SelfRegistrationResult>(ct);
            return (r, null);
        }
        catch (HttpRequestException ex)
        {
            return (null, "无法连接注册服务，请确认 API 服务已启动且地址配置正确。" +
                (string.IsNullOrWhiteSpace(ex.Message) ? "" : $"（{ex.Message}）"));
        }
    }

    /// <summary>M2-06 平台管理员查看自助注册配置（需 platform:admin:manage）。</summary>
    public async Task<(SelfRegistrationConfigView? Result, string? Error)> GetSelfRegistrationConfigAsync(CancellationToken ct = default)
    {
        var (data, _, err) = await GetJsonAsync("api/self-registration/config", ct);
        if (data is not { ValueKind: System.Text.Json.JsonValueKind.Object })
            return (null, err ?? "无法读取自助注册配置。");
        var v = data.Value;
        var enabled = v.TryGetProperty("enabled", out var e) && e.GetBoolean();
        var approval = v.TryGetProperty("approvalRequired", out var a) && a.GetBoolean();
        var captcha = v.TryGetProperty("requireCaptcha", out var c) && c.GetBoolean();
        var defaultCulture = v.TryGetProperty("defaultCulture", out var dc) ? dc.GetString() ?? "zh-CN" : "zh-CN";
        var domains = v.TryGetProperty("allowedEmailDomains", out var d) && d.ValueKind == System.Text.Json.JsonValueKind.Array
            ? d.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList()
            : new System.Collections.Generic.List<string>();
        var cultures = v.TryGetProperty("defaultAvailableCultures", out var cc) && cc.ValueKind == System.Text.Json.JsonValueKind.Array
            ? cc.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList()
            : new System.Collections.Generic.List<string> { "zh-CN" };
        return (new SelfRegistrationConfigView
        {
            Enabled = enabled,
            AllowedEmailDomains = domains,
            DefaultCulture = defaultCulture,
            DefaultAvailableCultures = cultures,
            ApprovalRequired = approval,
            RequireCaptcha = captcha,
        }, null);
    }

    /// <summary>M2-07 演示数据：平台管理员预览将创建的演示内容（需 platform:admin:manage）。</summary>
    public async Task<(DemoInstallPlan? Result, string? Error)> GetDemoDataPlanAsync(CancellationToken ct = default)
    {
        var (data, _, err) = await GetJsonAsync("api/demo-data/preview", ct);
        if (data is not { ValueKind: System.Text.Json.JsonValueKind.Object })
            return (null, err ?? "无法读取演示数据计划。");
        var v = data.Value;
        var items = v.TryGetProperty("items", out var its) && its.ValueKind == System.Text.Json.JsonValueKind.Array
            ? its.EnumerateArray().Select(x => new DemoPlanItem(
                x.TryGetProperty("entityType", out var et) ? et.GetString() ?? "" : "",
                x.TryGetProperty("count", out var c) && c.TryGetInt32(out var n) ? n : 0,
                x.TryGetProperty("description", out var d) ? d.GetString() : null)).ToList()
            : new System.Collections.Generic.List<DemoPlanItem>();
        return (new DemoInstallPlan
        {
            AlreadyInstalled = v.TryGetProperty("alreadyInstalled", out var a) && a.GetBoolean(),
            DemoTenantCode = v.TryGetProperty("demoTenantCode", out var tc) ? tc.GetString() ?? "demo" : "demo",
            DemoTenantName = v.TryGetProperty("demoTenantName", out var tn) ? tn.GetString() ?? "" : "",
            AdminUsername = v.TryGetProperty("adminUsername", out var au) ? au.GetString() ?? "" : "",
            AdminEmail = v.TryGetProperty("adminEmail", out var ae) ? ae.GetString() ?? "" : "",
            Items = items,
        }, null);
    }

    /// <summary>M2-07 演示数据：平台管理员触发安装（事务原子、重复执行保护）。</summary>
    public async Task<(DemoInstallResult? Result, string? Error)> InstallDemoDataAsync(CancellationToken ct = default)
    {
        var client = _factory.CreateClient("SuperBuilderApi");
        try
        {
            var resp = await client.PostAsJsonAsync("api/demo-data/install", new { }, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var (_, msg, _) = ParseApiError(await resp.Content.ReadAsStringAsync(ct));
                if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized) OnUnauthorized();
                return (null, msg ?? $"安装失败（{(int)resp.StatusCode}）。");
            }
            var r = await resp.Content.ReadFromJsonAsync<DemoInstallResult>(ct);
            return (r, null);
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            return (null, "无法连接服务，请确认 API 已启动且地址配置正确。" +
                (string.IsNullOrWhiteSpace(ex.Message) ? "" : $"（{ex.Message}）"));
        }
    }

    /// <summary>M3-G0 读取当前用户的服务端语言偏好。</summary>
    public async Task<(string? Culture, string? Error)> GetUserLanguageAsync(CancellationToken ct = default)
    {
        var (data, status, error) = await GetJsonAsync("api/user/preferences/language", ct);
        if (error != null) return (null, error);
        if (data is not { ValueKind: JsonValueKind.Object }) return (null, null);
        var culture = data.Value.TryGetProperty("culture", out var c) ? c.GetString() : null;
        return (culture, null);
    }

    /// <summary>M3-G0 持久化当前用户语言偏好到服务端。</summary>
    public async Task<(string? Culture, string? Error)> SetUserLanguageAsync(string culture, CancellationToken ct = default)
    {
        var client = CreateClient();
        try
        {
            var resp = await client.PutAsJsonAsync("api/user/preferences/language", new { culture }, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var (_, msg, _) = ParseApiError(await resp.Content.ReadAsStringAsync(ct));
                if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized) OnUnauthorized();
                return (null, msg ?? $"保存语言偏好失败（{(int)resp.StatusCode}）。");
            }
            var (data, _, err) = await GetJsonAsync("api/user/preferences/language", ct);
            var effective = (err == null && data is { ValueKind: JsonValueKind.Object } && data.Value.TryGetProperty("culture", out var c))
                ? c.GetString()
                : culture;
            return (effective, null);
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            return (null, "无法连接服务，请确认 API 已启动且地址配置正确。" +
                (string.IsNullOrWhiteSpace(ex.Message) ? "" : $"（{ex.Message}）"));
        }
    }

    /// <summary>M3-02 平台管理员查看全部语言目录（含已停用与翻译进度）。</summary>
    public async Task<(IReadOnlyList<AdminLanguageView>? Result, string? Error)> GetAdminLanguagesAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await GetAsync<List<AdminLanguageView>>("api/localization/admin/languages", ct);
            if (list is null) return (null, null);
            return (list, null);
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            return (null, "无法连接服务，请确认 API 已启动且地址配置正确。" +
                (string.IsNullOrWhiteSpace(ex.Message) ? "" : $"（{ex.Message}）"));
        }
    }

    /// <summary>M3-02 平台管理员新建语言（BCP 47 归一化 + 必填名 + 复制键集合待翻译）。</summary>
    public async Task<(bool Ok, string? Error)> CreateLanguageAsync(AdminLanguageCreate model, CancellationToken ct = default)
    {
        var (ok, status, error) = await PostAsync("api/localization/languages", model, ct);
        if (!ok && status == 401) OnUnauthorized();
        return (ok, error);
    }

    /// <summary>M3-02 平台管理员更新语言显示名/本地名/排序。</summary>
    public async Task<(bool Ok, string? Error)> UpdateLanguageAsync(long id, AdminLanguageUpdate model, CancellationToken ct = default)
    {
        var (ok, status, error) = await PutAsync($"api/localization/languages/{id}", model, ct);
        if (!ok && status == 401) OnUnauthorized();
        return (ok, error);
    }

    /// <summary>M3-02 平台管理员启用/停用语言（停用委托租户关系迁移）。</summary>
    public async Task<(bool Ok, string? Error)> SetLanguageEnabledAsync(long id, bool enabled, CancellationToken ct = default)
    {
        var (ok, status, error) = await PostAsync($"api/localization/languages/{id}/enabled", new { enabled }, ct);
        if (!ok && status == 401) OnUnauthorized();
        return (ok, error);
    }

    /// <summary>M3-02 平台管理员按 Id 顺序重排语言目录。</summary>
    public async Task<(bool Ok, string? Error)> ReorderLanguagesAsync(IReadOnlyList<long> orderedIds, CancellationToken ct = default)
    {
        var (ok, status, error) = await PostAsync("api/localization/languages/reorder", new { orderedIds }, ct);
        if (!ok && status == 401) OnUnauthorized();
        return (ok, error);
    }

    /// <summary>读取平台公开语言目录（含本地名称），供语言切换器展示 NativeName。</summary>
    public async Task<(IReadOnlyList<PublicLanguageView>? Result, string? Error)> GetPublicLanguagesAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await GetAsync<List<PublicLanguageView>>("api/localization/public/languages", ct);
            if (list is null) return (null, null);
            return (list, null);
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            return (null, "无法连接服务，请确认 API 已启动且地址配置正确。" +
                (string.IsNullOrWhiteSpace(ex.Message) ? "" : $"（{ex.Message}）"));
        }
    }

    /// <summary>
    /// 读取任意 JSON 端点为 <see cref="JsonElement"/>，失败时返回错误信息且不抛异常。
    /// 用于在不确定后端 DTO 精确结构时安全渲染列表/详情。
    /// </summary>
    public async Task<(JsonElement? Data, int Status, string? Error)> GetJsonAsync(string relativeUrl, CancellationToken ct = default)
    {
        var client = CreateClient();
        try
        {
            var resp = await client.GetAsync(relativeUrl, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
            var (code, msg, _) = ParseApiError(body);
            if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized();
            return (null, (int)resp.StatusCode, msg ?? $"请求失败（{(int)resp.StatusCode}）。");
            }
            if (string.IsNullOrWhiteSpace(body))
                return (null, 200, null);
            using var doc = JsonDocument.Parse(body);
            var el = doc.RootElement.Clone();
            return (el, 200, null);
        }
        catch (Exception ex)
        {
            return (null, 0, "网络或解析错误：" + ex.Message);
        }
    }

    /// <summary>纯文本读取：不解析 JSON，供 /health、/metrics 等非 JSON 端点使用。</summary>
    public async Task<(string? Text, int Status, string? Error)> GetTextAsync(string relativeUrl, CancellationToken ct = default)
    {
        var client = CreateClient();
        try
        {
            var resp = await client.GetAsync(relativeUrl, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized();
                return (null, (int)resp.StatusCode, $"请求失败（{(int)resp.StatusCode}）。");
            }
            return (body, (int)resp.StatusCode, null);
        }
        catch (Exception ex)
        {
            return (null, 0, "网络错误：" + ex.Message);
        }
    }
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

/// <summary>登录 / 当前用户响应（与 api/auth 的 AuthResult 字段对齐）。</summary>
public sealed class AuthResult
{
    public string? Token { get; set; }
    public int ExpiresInSeconds { get; set; }
    public long TenantId { get; set; }
    /// <summary>M2-05：归属主租户（切换后 TenantId=生效租户、HomeTenantId=主租户）。</summary>
    public long HomeTenantId { get; set; }
    public long UserId { get; set; }
    public string Username { get; set; } = "";
    public System.Collections.Generic.List<string>? Permissions { get; set; }
    public System.Collections.Generic.List<string>? AvailableCultures { get; set; }
    public string? DefaultCulture { get; set; }
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
