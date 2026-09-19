# 认证令牌存储加固改造方案（httpOnly cookie + 服务端会话 + Redis 刷新）

> 状态：计划（未开发） ｜ 关联：M8-05（XSS 边界）、P11.0 安全轨道、P0-04B（令牌吊销）
> 目标：消除 `localStorage` 存放 JWT 带来的凭据窃取风险，实现静默续期与可吊销会话，并降低 XSS 攻击后的凭据离线复用能力，满足商业化安全/合规。

---

## 0. 关键修正（相对于通用 SPA 建议）

通用 SPA 建议是「access token 放 httpOnly cookie 随浏览器请求发往 API」。**本架构不适用**，证据：

- `SuperBuilder_AI.Web/Program.cs:31-34`：Web 通过 `IHttpClientFactory` 创建名为 `SuperBuilderApi` 的 HttpClient，BaseAddress=API。Blazor Server 的组件调用走**服务端进程内的 HttpClient**，浏览器并不直连 API。
- `SuperBuilder_AI.Components/Services/Clients/ApiClientBase.cs:40-47`：`CreateClient()` 用 `AppState.Token` 注入 `Bearer` 头——令牌一直在**服务端 AppState 内存**，localStorage 仅是 F5/重连时「还原」用的副本。

因此正确边界划分：

```
浏览器 ──httpOnly 会话 cookie(same-site)──> Web(Blazor Server)
Web(服务端) ──Bearer(服务端内存持有)──────> API(自定义 AuthMiddleware)
```

- **浏览器↔Web**：用 httpOnly+Secure+SameSite=Lax 的会话 cookie 携带 `sessionId`，令牌永不在浏览器 JS 上下文。
- **Web↔API**：令牌只存在于 Web 服务端内存（单实例）或 Redis（多实例），由 Web 服务端代发 Bearer。

这样 XSS 注入脚本读不到 JWT，也不能通过 `document.cookie` 读取 `sessionId`，可显著降低凭据被窃取后离线复用的风险。**这并不消除 XSS 本身的危害**：同源恶意脚本仍可能借用户现有会话发起请求并读取响应，因此 CSP、输出编码、依赖治理与敏感操作二次校验仍须保留。

---

## 1. 现状证据（已核代码）

| 关注点 | 现状 | 证据位置 |
|---|---|---|
| 令牌存储 | `localStorage` 键 `sb_auth_v1`，存完整 AuthSnapshot（含 Token） | `SuperBuilder_AI.Components/Services/AuthStore.cs:31,57,70` |
| XSS 边界自述 | 注释明确承认 localStorage 令牌可被同源 XSS 窃取，尚无 refresh | `SuperBuilder_AI.Components/Services/AuthStore.cs:16-23` |
| 登录态还原 | `MainLayout.OnAfterRenderAsync` 调 `RestoreAsync()`（首帧之后） | `SuperBuilder_AI.Components/Services/AuthStore.cs:66`、`MainLayout.razor:108-137` |
| API 鉴权 | 自定义 `AuthMiddleware` 读 Bearer，**手动写 `HttpContext.User`**；管线无 `app.UseAuthentication()`，`UseAuthorization()`(SuperBuilder_AI/src/Api/Program.cs:675) 实际靠 `ApiAuthorizationFilter`(SuperBuilder_AI/src/Api/Program.cs:80) 兜底；`[Authorize]` 标准属性**不生效** | `SuperBuilder_AI/src/Api/Program.cs:673,675,80`、`SuperBuilder_AI/src/Api/Middleware/AuthMiddleware.cs:41,151-154` |
| 令牌签发/校验 | `ITokenService.Issue` 手写 HS256（HMACSHA256 + Base64Url）；`Validate` 用 `FixedTimeEquals` 常量时间比对签名 + 校验 `Exp` 过期——**加密正确，非「假 JWT」**；但 payload 无 `aud`/`iss`，故**不校验受众/签发者**；无 `UseAuthentication` 接入 | `SuperBuilder_AI/src/Application/Auth/TokenService.cs:33-94`（签发）、`SuperBuilder_AI/src/Application/Auth/TokenService.cs:96-138`（校验） |
| 每请求 DB 成本 | `AuthMiddleware` 每次受保护请求查库比对 `SecurityStamp` + 主/生效租户 `Enabled`（1–2 次查询），支撑即时吊销 | `SuperBuilder_AI/src/Api/Middleware/AuthMiddleware.cs:80-136` |
| 吊销机制 | `SecurityStamp` 声明（P0-04B），AuthMiddleware 据此校验 | `SuperBuilder_AI/src/Application/Auth/TokenService.cs:18-19,82`、`SuperBuilder_AI/src/Api/Controllers/AuthController.cs:101` |
| 刷新端点 | **无**（当前纯「过期即重登」） | `SuperBuilder_AI/src/Api/Controllers/AuthController.cs` 仅 `login/me/login-options/tenant-by-code` |
| Redis | `IConnectionMultiplexer` 已作为单例注册，但**仅当** `RateLimit:Store:Type=Redis` 才建；限流专用 | `SuperBuilder_AI/src/Api/Program.cs:479-490` |
| CORS | `P11Cors`：`AllowAnyHeader/AllowAnyMethod`，未 `AllowCredentials` | `SuperBuilder_AI/src/Api/Program.cs:407-413` |
| Web 端认证 | 无 cookie 认证/会话中间件；仅 CSP | `SuperBuilder_AI.Web/Program.cs` 全文 |

---

## 2. 目标架构

```
[浏览器]  ── GET /_Host (带 Cookie: sb_sess=<id>) ──> [Web]
[Web]     中间件从 Cookie 取 id → WebSessionStore.Get → 注入 Scoped SessionInitializer
[Web]     App.razor.OnInitializedAsync 读 SessionInitializer → 填充 AppState
[Web]     API 调用：HttpClient(服务端) 带 AppState.Token 作 Bearer → [API AuthMiddleware]
[Web]     临近过期 → 服务端调 POST /api/auth/refresh(带 refreshToken) → 更新 WebSessionStore.Token
[登出/改密/撤权] → 删 WebSessionStore/Redis key → 下次 API 401 → 跳登录
```

---

## 3. 分阶段改造（每阶段独立可发、可回滚）

### Phase 0 — 门禁前移，消除首屏闪烁（纯前端，低风险，建议先发）
*解决用户原始痛点①：启动先 home 再 login。*

- 新增 `Components/Components/AuthGate.razor`：
  - `!SessionRestored` → 渲染 `BlankLayout` 极简 splash（**不套 MainLayout 壳**，无侧边栏/顶栏）。
  - `OnAfterRenderAsync(firstRender)` → 调用 `AuthStore.RestoreAsync()`；此时交互式电路已经建立、`IJSRuntime` 可用。还原完成后触发 `StateHasChanged()`，门禁再决定渲染登录页或业务路由。
  - 还原后未登录且非匿名路由（`/login`、`/register`、`/self-registration`、`/platform-login`、错误页）→ `NavigateTo("/login", forceLoad:true)`。
  - 已登录 → 正常 `RouteView`。
- `App.razor`/`Routes.razor`：用 `<AuthGate>` 包裹 `RouteView`。Phase 0 阶段（存储仍为 localStorage）**不要**把 `RestoreAsync()` 放进 `OnInitializedAsync`；否则预渲染模式下 `IJSRuntime` 不可用。还原职责必须移到始终会被渲染的 `AuthGate.OnAfterRenderAsync`，不能继续留在被门禁阻挡的 `MainLayout`，否则会形成「门禁等待还原、MainLayout 等待渲染」的初始化死锁。
- `MainLayout.razor:108-137`：移除会话还原与首次跳转逻辑，仅保留 `SessionExpired` 订阅 → `ClearAsync()` + 跳登录。
- 验收：首屏不再闪 Home；未登录直接进登录页（BlankLayout）。

### Phase 1 — 浏览器不再持有令牌（httpOnly 会话 cookie + 服务端内存会话）

> ⚠️ **2026-09-18 评审阻塞**：Blazor Server 组件事件运行在 SignalR 电路内，通常**没有可用的普通 HTTP response** 可写 `Set-Cookie`。因此「在 `AuthStore.SaveAsync()` 里直接调 `SessionCookieService.Set(...)` 写 httpOnly cookie」**不可行**——须改由 Web 宿主的正常 HTTP 端点写 cookie（见下「写 cookie 的入口」）。另：`AuthStore` 是 RCL 共享服务（Web + MAUI 共用），直接改成 Web cookie 版会打断 MAUI 登录持久化——须抽 `IAuthPersistence` 抽象（见 §8.1(k)）。本阶段拆为三个小任务。

**任务 1：RCL 持久化抽象（`IAuthPersistence`）**
- 抽接口 `IAuthPersistence`（`SaveAsync` / `RestoreAsync` / `ClearAsync`），`AuthStore` 依赖它而非直接 `localStorage`。
  - Web 实现 `WebAuthPersistence` = `WebSessionStore`（服务端内存/Redis）+ 一次性交接码（handoff code）管理；它只创建待交接会话，**不直接写 cookie**。
  - MAUI 实现 `MauiAuthPersistence` = 现有 `SecureStorage` / 回退 `localStorage`（维持现状，不受 Web cookie 改造影响）。
- `SuperBuilder_AI.Maui/MauiProgram.cs:27` 的 `AddScoped<AuthStore>()` 不变；仅替换其内部持久化后端为 `MauiAuthPersistence`。Web 侧 `SuperBuilder_AI.Web/Program.cs` 注册 `IAuthPersistence → WebAuthPersistence`。

**任务 2：写 cookie 的入口（推荐 Web 登录代理；过渡方案使用一次性交接码）**
- A. **推荐：Web 登录代理。** 浏览器向 Web 自己的 `/auth/login-proxy` 提交凭据，Web 服务端调用 API 登录，在同一个普通 HTTP response 内创建 `WebSessionStore` 会话并写入 `sb_sess`，只向浏览器返回登录结果，不返回 access/refresh token。登录端点启用 antiforgery、限流和统一错误响应。
- B. **过渡：Web Session API + 一次性交接码。** Blazor 电路登录成功后，`WebAuthPersistence.SaveAsync()` 把 `SessionData` 存入服务端待交接区并生成一次性随机 handoff code（至少 128bit、30–60 秒过期、仅可消费一次）；浏览器仅把该 code POST 给 `/auth/session/start`。端点原子消费 code、将数据迁入正式 `WebSessionStore`、生成全新 sessionId 并写 `sb_sess`。handoff code 不得是 JWT 或最终 sessionId，端点必须启用 antiforgery、限流并拒绝重放。
- `/auth/session/end` 通过普通 HTTP response 清除 cookie，同时按当前 cookie 删除服务端会话；登出请求同样启用 antiforgery。组件事件不直接操作 `HttpResponse.Cookies`。

**任务 3：服务端会话存储与还原**
- 新增 `SuperBuilder_AI.Web/Services/WebSessionStore.cs`：单实例内存（`ConcurrentDictionary` 或 `IMemoryCache`），`Set/Get/Remove`；`SessionData{Token,RefreshToken?,TenantId,HomeTenantId,UserId,Username,Permissions,AvailableCultures,DefaultCulture,ExpiresAtUtc}`。若采用过渡方案 B，再提供独立的 `PendingSessionStore`/handoff API，正式 sessionId 与 handoff code 使用不同命名空间并设置不同 TTL。
- 新增 `SuperBuilder_AI.Web/Services/SessionCookieService.cs`：写/清 `sb_sess`（`HttpOnly;Secure;SameSite=Lax;Path=/`）。**仅由任务 2 的 HTTP 端点调用**，组件事件不直接调用。
- 新增 `SuperBuilder_AI.Web/Controllers/SessionController.cs`（或 Minimal API）：`/auth/session/start`、`/auth/session/end`。
- 根组件初始化：`App.razor.OnInitializedAsync` 经 `IHttpContextAccessor.HttpContext.Request.Cookies["sb_sess"]` 取 `sessionId` → `WebSessionStore.Get` → 填充 `AppState`（Blazor Server 初始渲染期 `IHttpContextAccessor` 可用；若空则加 `MapBlazorHub` 前中间件把 cookie 捕获进 Scoped `SessionInitializer` 兜底）。

**修改 `AuthStore`（RCL）**：`SaveAsync/RestoreAsync/ClearAsync` 改为委托 `IAuthPersistence`；`ValidateAsync` 不变。MAUI 与 Web 共用同一 `AuthStore`，行为由注入的 `IAuthPersistence` 决定。
- 推荐代理方案下，`Login.razor`、`PlatformLogin.razor`、`SelfRegistration.razor` 统一调用 Web 登录代理；采用过渡方案时，登录成功后 `SetFromLoginAsync` → `IAuthPersistence.SaveAsync()` 取得 handoff code，再由浏览器 POST `/auth/session/start` 完成交接，响应成功后立即丢弃 code。
- `SuperBuilder_AI.Components/Services/Clients/ApiClientBase.cs`：不变（仍服务端 Bearer）。
- `SuperBuilder_AI.Web/Program.cs`：注册 `AddHttpContextAccessor()`、`WebSessionStore`(单例)、`SessionCookieService`(Scoped)、`WebAuthPersistence`(Scoped)、`IAuthPersistence → WebAuthPersistence`。

验收：DevTools→Application→**localStorage 无 `sb_auth_v1`**；cookies 有 `sb_sess`(HttpOnly 标记)；XSS 注入脚本 `document.cookie`/`localStorage` 均拿不到令牌；F5 不丢登录；MAUI 登录持久化不受影响。
### Phase 2 — Refresh Token 轮换与静默续期

（解决用户痛点②）
*从「被动 401 踢登录」升级为「主动保活」。*

新增/修改（API）：
- `SuperBuilder_AI/src/Application/Auth/TokenService.cs`：新增 `IssuePair(...)` → 返回 `(accessToken, refreshToken)`；refreshToken 使用 `RandomNumberGenerator` 生成高熵不透明随机串，API 仅持久化其密码学哈希，不在日志或数据库中保存明文。
- 新增 API 侧 `RefreshTokenStore` 与持久化模型（Phase 2 即落数据库，不延后到 Redis）：至少保存 `TokenHash`、`UserId`、`SecurityStamp`、`FamilyId`、`ExpiresAtUtc`、`RevokedAtUtc`、`ReplacedByTokenHash`、创建时间与客户端审计信息。WebSessionStore 只保管当前 refresh token 明文，**不是 API 的有效性权威来源**。
- `SuperBuilder_AI/src/Api/Controllers/AuthController.cs`：
  - `Login`（`SuperBuilder_AI/src/Api/Controllers/AuthController.cs:57-115`）：返回 `accessToken`+`refreshToken`；refresh 经 Web 存入 `WebSessionStore.RefreshToken`，**不下发浏览器**。
  - 新增 `POST /api/auth/refresh`（`[AllowAnonymous]` 但需 refreshToken）：由 Web 服务端提交 refreshToken；API 计算哈希并查询 `RefreshTokenStore`，校验过期、撤销状态与 `SecurityStamp` 后，在同一事务中撤销旧 token、签发新 access+refresh 并记录替换关系。
- `SuperBuilder_AI/src/Api/Middleware/AuthMiddleware.cs`：refresh 路径放行（不要求 Bearer）。
- 撤回 P0-04B：refreshToken 纳入 `SecurityStamp` 吊销校验。

修改（Web）：
- `AppState`：增 `AccessTokenExpiresAtUtc`。
- **[更正] 访问令牌时效须同步缩短**：`TokenService._lifetime`（`SuperBuilder_AI/src/Application/Auth/TokenService.cs:62`）由 60min 降为 ~15min，且 `SuperBuilder_AI/src/Api/Controllers/AuthController.cs.Login` 返回的 `ExpiresInSeconds`（`SuperBuilder_AI/src/Api/Controllers/AuthController.cs:107`，当前 3600）同步改为 900；否则 refresh 只改善体验、不缩小失窃窗口（详见 §8.1(c)）。
- 静默续期触发：Web 端后台 `Timer` 或每次 API 请求前检查，access 剩余 <5min 且无进行中刷新 → 服务端调 `/api/auth/refresh`（带 `WebSessionStore.RefreshToken`）→ 更新 `WebSessionStore.Token` 与 `AppState.Token`。
- **[补充] refresh 轮换**：每次刷新签发新 refresh 并使旧值失效；若已撤销 token 再次出现，按 `FamilyId` 原子吊销整条会话链并记录安全审计事件。并发刷新须通过事务/唯一约束保证最多一次成功。
- 刷新失败（refresh 失效/被吊销）→ `AuthStore.ClearAsync()` + 跳登录。

验收：用户持续操作不被踢；仅 refresh 也失效（如改密）才跳登录。

### Phase 3 — Redis 会话 + 可吊销 + 合规
*多实例横向扩展与即时吊销，满足等保/ISO 27001。*

- `WebSessionStore`：增加 Redis 后端，复用 `IConnectionMultiplexer`（**新增独立开关** `Session:Redis:Configuration`，不要依赖 `RateLimit:Store:Type`，否则限流用 Memory 时会话无法跨实例）。连接串缺失时回退内存（单实例兼容）。
- **[更正] 多实例 Blazor Server 必须配粘性会话（LB affinity）**：Phase 1 内存方案在多实例 + 无粘性 LB 时，F5/重连落到冷实例会因该实例无会话而登出；Phase 3(Redis) 仅共享「认证数据」（重连任一经 Redis 还原 AppState），**不共享活动电路 UI 状态**。故无论哪档，商业化多实例都建议 LB 粘性作兜底（详见 §8.1(b)）。
- **[补充] 登录时轮转 sessionId**：登录成功签发全新 `sessionId` 并使旧 id 失效，防会话固定（§8.2(d)）；`sessionId` 用 `RandomNumberGenerator` 生成 ≥128bit 熵，勿用 `Guid.NewGuid()`（§8.2(f)）。
- **[补充] `Secure` 标志**：cookie 须 `Secure`（仅 HTTPS 生效），Web 全链路须 HTTPS，否则开发/HTTP 环境不下发 cookie（§8.2(f)）。
- 吊销联动：登出 / `IdentityService.SetPasswordAsync`(`src/Application/Identity/IdentityService.cs:226`) / `PlatformAdminService.DisableAsync`(`src/Application/Identity/PlatformAdminService.cs:160`) → 维护 `userId→sessionId` 索引，删对应 Redis key → 下次 API 401 → 跳登录。
- `Auth:SigningKey` 已强制（`SuperBuilder_AI/src/Api/Program.cs:393-398`）。补充 `DataProtection` 密钥持久化：Web 当前仅 Development 持久化（`SuperBuilder_AI.Web/Program.cs` 未见；`SuperBuilder_AI/src/Api/Program.cs:58-61` 仅 API 端 Dev），**Production 需共享密钥**（多实例解密 cookie/circuit）。
- CORS：若未来引入浏览器↔API 凭据调用，需 `P11Cors` 加 `AllowCredentials()` + 显式 origins（当前 `SuperBuilder_AI/src/Api/Program.cs:407-413` 未设）；推荐设计下 API 调用保持服务端发起，cookie 仅限 Web 源，故 CORS 暂不必改。

验收：多实例部署会话共享；管理员禁用账号即时失效；密钥轮换不丢登录。

---

## 4. 风险与回滚

| 风险 | 缓解 |
|---|---|
| `IHttpContextAccessor` 在某些托管下首帧为 null | 加 `MapBlazorHub` 前中间件捕获 cookie 进 Scoped `SessionInitializer` 作兜底（Phase 1 即做） |
| 单实例内存会话在 Web 重启后全量登出 | Phase 1 接受（重启本就需重登）；Phase 3 Redis 解决 |
| refresh 轮询增加 API 压力 | 仅在剩余 <5min 且无为空窗触发；可加抖动避免惊群 |
| Session API 被 CSRF / 重放 | `SameSite=Lax` 只是纵深措施；`/auth/session/start`、`/end`、登录代理必须启用 antiforgery 与限流。handoff code 短时效、一次性原子消费，正式 sessionId 登录时轮转 |
| refresh token 数据库泄漏 | 仅保存 token 哈希；轮换与吊销使用事务，明文只在 API 响应和 Web 服务端会话中短暂存在，日志统一脱敏 |
| Phase 2/3 改 API 契约 | 与前端版本对齐；`/api/auth/refresh` 为新增端点，不破坏现有 `login/me` |

每阶段独立合并、独立回滚；建议顺序 **Phase 0 → Phase 1 → Phase 2 → Phase 3**（Phase 0/1 即可消除最大风险，先行发布）。

---

## 5. 测试与验收清单

- [x] 单测：`WebSessionStore` 增删查/过期；`AuthStore` 由 localStorage 改为 session 后 `Save/Restore/Clear` 行为。（`WebSessionStoreTests` + `AuthStoreTests`，2026-09-19）
- [x] 单测：handoff code 仅可消费一次、过期/重放拒绝、消费后轮转为独立 sessionId；Session API 缺 antiforgery token 时拒绝。（`PendingHandoffStoreTests` 覆盖一次性/过期/未知码；antiforgery 拒绝项随 `/auth/session/start|end` 端点接入 antiforgery 后补）
- [ ] 单测：`POST /api/auth/refresh` 正常轮换、并发刷新仅一次成功、旧 token 复用吊销 family、refresh 失效返回 401、`SecurityStamp` 变更后拒绝。
- [ ] `AuthMiddleware` 吊销：改密后旧 access+refresh 均拒。
- [ ] E2E（Playwright，CI `M13-09`）：未登录首屏直接进登录页（无 Home 闪烁）；F5 保持登录；localStorage 无令牌；注入 `<script>document.cookie/localStorage</script>` 仿真 XSS 拿不到令牌。
- [ ] 真容器（§9.1 同类）：多实例 + Redis 会话共享 + 管理员禁用即时失效。

---

## 6. 与之前建议的差异（明确记录）

1. **之前**：access token 放 httpOnly cookie 发往 API。 **修正**：Blazor Server 下 API 调用是服务端发起，正确做法是浏览器↔Web 用 httpOnly cookie，令牌只存 Web 服务端。
2. **之前**：令牌存储三选一对比。 **修正**：localStorage（弃用）、httpOnly cookie（仅作浏览器↔Web 会话 id 载体）、Redis（Web 端会话/refresh 存储后端）——三者各司其职，非互斥。
3. **过期处理**：从「被动 401 踢登录」升级为「refresh token 静默保活，仅刷新失败才踢」。
4. 新增：returnUrl 深链回跳（登录后回原目标页）、认证可观测（登录失败率/会话过期率/重定向环）——纳入 Phase 1/2 收尾。

---

## 7. API 端鉴权中间件选型：自定义 AuthMiddleware vs ASP.NET JWT Bearer（独立决策层）

> 本节约与「令牌存储层（Phase 1/3）」「续期层（Phase 2）」**正交**：无论令牌存在哪、怎么刷新，API 收到 Bearer 后「怎么验」是可单独决策的一层。
> 结论：**不必为安全性迁移 Bearer；但建议先用框架 `JwtSecurityTokenHandler` 替换手写 HS256 解析（低风险）。**

### 7.1 现状核实（证据）

- `TokenService.Validate`（`SuperBuilder_AI/src/Application/Auth/TokenService.cs:96-138`）：手写 HS256——`HMACSHA256(_key)` 重算签名，`FixedTimeEquals` 常量时间比对（防时序攻击），校验 `Exp`。**加密正确，不是「假 JWT」**。
- 缺口：payload 仅有 `sub/tid/name/perms/sec/htid/iat/exp`，**无 `aud`/`iss`**，故 `Validate` **不校验受众与签发者**。
- `AuthMiddleware`（`SuperBuilder_AI/src/Api/Middleware/AuthMiddleware.cs:31-209`）手动 `context.User = new ClaimsPrincipal(...)`，管线（`SuperBuilder_AI/src/Api/Program.cs:673`）**无 `app.UseAuthentication()`**；下游 `UseAuthorization()`（SuperBuilder_AI/src/Api/Program.cs:675）靠 MVC 过滤器 `ApiAuthorizationFilter`（SuperBuilder_AI/src/Api/Program.cs:80）兜底。因此标准 `[Authorize]`/`[Authorize(Policy=...)]` **不接入**，授权靠私有过滤器。
- 每受保护请求 1–2 次 DB 查询（`SecurityStamp` 比对 + 主/生效租户 `Enabled`），支撑即时吊销（P0-04B）。

### 7.2 对比

| 维度 | 自定义 AuthMiddleware（现状） | ASP.NET JWT Bearer（AddJwtBearer） |
|---|---|---|
| 验签 | 手写 HS256（正确，需自维护） | `JwtSecurityTokenHandler` 官方校验，覆盖签名/过期/受众/签发者 |
| `aud`/`iss` 校验 | ✗ 不校验 | ✓ 声明式免费补上 |
| `[Authorize]`/Policy | ✗ 不接入，靠 `ApiAuthorizationFilter` | ✓ 原生策略授权 + `IAuthorizationService` |
| 401 标准挑战 | ✗ 自定义 JSON，无 `WWW-Authenticate` | ✓ 标准 Bearer 挑战头 |
| 即时吊销/租户停用 | ✓ 每次请求查库 | 需自写 `OnTokenValidated` 事件；纯无状态则无即时吊销 |
| 跨租户/治理隔离 | ✓ 内联中间件 | 需移到 `IAuthorizationHandler`/`Requirement` |
| 每请求 DB 成本 | 1–2 次（保留吊销则免不掉） | 无状态=0 次（若保留吊销仍要查） |
| 可审计/合规熟悉度 | 私有 288 行，审计员需读懂 | 标准方案，合规更「眼熟」 |

### 7.3 关键判断（避免误判）

1. **「改 Bearer 更安全」——不成立**：当前手写 HS256 验签本身正确。唯一安全缺口是缺 `aud`/`iss` 校验，这是 Bearer 顺手能补的，**非整体迁移专属收益**。
2. **刷新/吊销权衡与「自定义 vs Bearer」无关**：要即时吊销就必查库（当前已做），要零 DB 就放弃即时吊销——是产品策略，不是中间件选型。
3. **每请求 1–2 次 DB 查询是真实成本**，但保留吊销就无法省；迁移 Bearer 不会自动提性能。

### 7.4 推荐路径（按性价比）

- **P-低成本（建议先做，可作 Phase 0.5 前置）**：保留 `AuthMiddleware` 编排逻辑，**把 `TokenService.Validate` 手写 HS256 换成 `JwtSecurityTokenHandler.ValidateToken`**（`TokenValidationParameters{ ValidateLifetime=true, ValidIssuer, ValidAudience, IssuerSigningKey=HMAC }`）。改动小、消灭「手写加密代码」审计原罪，并白捡 `aud`/`iss` 校验。需同步给 `Issue` 补写 `aud`/`iss` 声明（`SuperBuilder_AI/src/Application/Auth/TokenService.cs:33-94`）。
- **P-中成本（可选，技术债清理）**：接 `AddJwtBearer` 作 AuthenticationScheme，把 `SecurityStamp`/租户/数据面逻辑搬进 `OnTokenValidated` + 自定义 `AuthorizationHandler`，让 `[Authorize(Policy=...)]` 生效。收益是标准授权体系，代价是重写编排、回归风险高。
- **不建议**：为「改 Bearer」而改 Bearer 却丢掉当前吊销/租户/数据面管控——用无状态便利换安全强度，商业化产品不划算。

> **定位**：令牌存储加固（前一轮 Phase 1/3）对合规的贡献远大于「API 校验从自定义改 Bearer」。先把存储层落地；Bearer 标准化作为后续技术债清理，不绑死进本次改造。

### 7.5 验收补充

- [ ] 单元化 `JwtSecurityTokenHandler` 替换后：合法令牌通过、篡改签名拒、过期拒、`aud`/`iss` 不符拒。
- [ ] 若接 `AddJwtBearer`：现有 `AuthMiddleware` 匿名白名单与数据面隔离逻辑 100% 迁移为 `OnTokenValidated`/Handler 且无行为回退（E2E `M13-09` 全绿）。

---

## 8. 源码复审发现的遗漏与更正（2026-09-18 二次审阅）

> 逐条回到 `SuperBuilder_AI.Components/Services/AuthStore.cs` / `SuperBuilder_AI.Components/Services/Clients/ApiClientBase.cs` / `SuperBuilder_AI/src/Application/Auth/TokenService.cs` / `SuperBuilder_AI/src/Api/Controllers/AuthController.cs` / `SuperBuilder_AI/src/Api/Middleware/AuthMiddleware.cs` / `SuperBuilder_AI/src/Api/Security/ApiAuthorizationFilter.cs` / `SuperBuilder_AI.Web/Program.cs` / `SuperBuilder_AI/src/Api/Program.cs` 核对。结论：文档的事实断言**基本正确**，但有三处**实现风险/重大遗漏**、六处**商业化最佳实践补充**。

### 8.1 必须更正（错误 / 重大遗漏）

**(a) Phase 0「还原前移到 `OnInitializedAsync`」在 localStorage 阶段有 JS 互操作风险，继续留在 MainLayout 又会造成门禁死锁。**
- 现状 `AuthStore.RestoreAsync` 走 `IJSRuntime.InvokeAsync("localStorage.getItem")`，而 `MainLayout` 故意放在 `OnAfterRenderAsync`（注释：「预渲染阶段无 JS 运行时」）。
- Blazor Server 当前 `_Host.cshtml` 用 `render-mode="Server"`（非 `ServerPrerendered`），电路建立后 JS 可用，故 `OnInitializedAsync` 调 JS **当前可跑**；但这是脆弱耦合——一旦切回 `ServerPrerendered`（历史 NRE 即源于此）立即抛 `InvalidOperationException`。
- **更正**：Phase 0 由始终可渲染的 `AuthGate.OnAfterRenderAsync(firstRender)` 调用 `RestoreAsync()`，完成前显示空白 splash；不要放进 `OnInitializedAsync`，也不能继续留在被 AuthGate 阻挡的 `MainLayout`。否则前者在预渲染模式下可能因 JS 不可用而失败，后者会形成初始化死锁。Phase 1 改为服务端 cookie 捕获后，再把还原前移到服务端初始化链路。

**(b) 多实例 Blazor Server 必须配粘性会话（LB affinity）——文档 Phase 1/3 的「多实例会话共享」表述不完整。**
- Blazor Server 的**活动电路（UI 状态）常驻单实例内存**，与 WebSessionStore 无关。Phase 1 内存会话在多实例 + 无粘性 LB 时，F5/重连落到冷实例 → 该实例 WebSessionStore 无此会话 → 被登出。
- Phase 3 把 WebSessionStore 迁 Redis 只解决**「认证数据跨实例可恢复」**（重连任一经 Redis 还原 AppState），**并不共享活动电路 UI 状态**（未保存的表单/滚动会丢，可接受）。
- **更正**：明确「Phase 1 内存方案仅适用于单实例或粘性 LB；多实例无粘性必须直接上 Phase 3(Redis)」。无论哪档，商业化多实例 Blazor Server 都建议 LB 粘性（affinity）作为兜底。

**(c) Phase 2 未缩短访问令牌时效，静默刷新收益被稀释。**
- 当前 `TokenService._lifetime = 60min`（`SuperBuilder_AI/src/Application/Auth/TokenService.cs:62`），`SuperBuilder_AI/src/Api/Controllers/AuthController.cs.Login` 回 `ExpiresInSeconds=3600`（`SuperBuilder_AI/src/Api/Controllers/AuthController.cs:107`）。Phase 2 只加 refresh，但访问令牌仍 60min——被盗窗口仍是 60min，refresh 主要改善「体验」而非「失窃窗口」。
- **[补充 2026-09-18 评审] 访问令牌时效不止文档列出的两处**：除 `TokenService.cs:62` 与 `AuthController.cs:107` 外，还存在 `SuperBuilder_AI/src/Application/Identity/SelfRegistrationService.cs:153`（自助注册）、`SuperBuilder_AI/src/Api/Controllers/TenantMembershipController.cs:182`（租户切换）两处 `3600`。四处须统一引用 `ITokenService.Lifetime`（由配置 `Auth:AccessTokenLifetimeMinutes` 驱动），单一来源治理见 `2026-09-hardcoded-config-audit.md`。
- **更正**：Phase 2 须把访问令牌降到 **15min 量级**（同步改 `TokenService` 时效与 `Login` 返回的 `ExpiresInSeconds`），refresh 负责无感续期；降级时仍有 15min 旧令牌窗口（由 `SecurityStamp` 吊销兜底）。

**(j) Phase 1 不能在 Blazor 组件事件里直接写 httpOnly cookie（最大实现阻塞）。**
- 现状计划：`AuthStore.SaveAsync()` 调 `SessionCookieService.Set(...)` 写 `Set-Cookie`；但登录成功发生在 Blazor Server 的 SignalR 电路内，通常**已无可用普通 HTTP response** 可写 cookie。
- **更正**：写 cookie 必须由 Web 宿主的**正常 HTTP 端点**完成。优先使用 Web 登录代理，在同一 response 创建会话并写 `sb_sess`；若暂时保留 Blazor 电路登录，则通过短时、一次性 handoff code 把待交接会话原子迁移给 `/auth/session/start`。组件事件不直接写 cookie，浏览器也不得接触 JWT、refresh token 或最终 sessionId。Phase 1 详见 §3。

**(k) `AuthStore` 是 RCL 共享服务，MAUI 也在注册使用（持久化抽象缺失）。**
- 证据：`SuperBuilder_AI.Components/Services/AuthStore.cs:7` 注释「RCL 共享，两个 Head 各自注册为 Scoped」；`SuperBuilder_AI.Maui/MauiProgram.cs:27` `AddScoped<AuthStore>()`。
- 风险：直接把 `AuthStore` 改成 Web cookie/session 版会打断 MAUI Hybrid 的登录持久化（MAUI 无 Web cookie 概念）。
- **更正**：抽 `IAuthPersistence`（Save/Restore/Clear）；Web 实现负责 WebSessionStore 与待交接会话管理，但 cookie 只由普通 HTTP 端点写入；MAUI 实现 = `MauiAuthPersistence`（SecureStorage / 现有 localStorage 过渡）。`AuthStore` 依赖接口，两 Head 注入各自实现。见 Phase 1 重写。

### 8.2 商业化最佳实践补充（建议，非阻塞）

**(d) 登录时轮转 sessionId（防会话固定）。** Phase 1 登录成功应**签发全新 `sessionId` 并使旧 id 失效**，而非复用匿名期 cookie。httpOnly 使外部固定难度高，但仍是合规基线要求。

**(e) refresh token 轮换 + 复用检测。** Phase 2 使用长时效、高熵不透明随机串；API 从 Phase 2 起就在数据库中仅保存其哈希和 token family 状态。每次刷新在同一事务内签发新 refresh 并作废旧值；检测到旧 refresh 被复用即判定泄露、按 `FamilyId` 吊销整条会话链。Redis 可在 Phase 3 用于缓存或会话共享，但不能替代 API 侧持久化权威数据。

**(f) cookie 安全标志细节。** `Secure` 仅在 HTTPS 生效——开发/HTTP 环境不会下发，须保证 Web 全链路 HTTPS（当前 `ApiBaseUrl` 默认 https，Web 自身也需 https）。`sessionId` 须用 `RandomNumberGenerator` 生成 **≥128bit** 熵（勿用 `Guid.NewGuid()`，仅 122bit 且部分有序）。

**(g) §7.4 替换手写 HS256 的实现注意。** ① `Issue` 必须补写 `aud`/`iss` 声明（`SuperBuilder_AI/src/Application/Auth/TokenService.cs:76-87` 当前 payload 无此二字段），否则 `JwtSecurityTokenHandler` 的 `ValidAudience`/`ValidIssuer` 校验必失败；② `AuthMiddleware` 当前从自定义 `TokenPayload` 取 `tid/perms/sec/htid`，改框架校验后须改为从 `ClaimsPrincipal.FindFirst(...)` 读取，且 `perms` 为多值声明（一个 `perm` 多个 Claim），需 `FindAll("perm")`。

**(h) Phase 3 DataProtection 措辞澄清。** `sb_sess` 是**不透明随机 id**，本身不依赖 DataProtection 解密；真正需跨实例共享的是 Blazor 电路 / antiforgery 保护密钥（与 (b) 粘性会话同议题），勿把二者混为一谈。Phase 3 仍可顺带补 DataProtection 密钥共享，但属于「粘性会话的替代/补充」而非「session cookie 所必需」。

**(i) 日志不泄露令牌（纵深防御）。** 已确认 `ObservabilityMiddleware` 仅处理 `Correlation` 头、不记录 `Authorization`（`ObservabilityMiddleware.cs:77-90`），API 侧无令牌泄露。仍建议在 Web/API 两侧**显式不开启 HttpClient 请求头日志**（`IHttpClientFactory` 默认不记头，但运维勿在 Production 开 `LogLevel.Trace` 级 Http 日志），并加日志脱敏护栏。

### 8.3 已核实无误（信心项）

- `localStorage` 键 `sb_auth_v1`、`AuthSnapshot` 含 `Token`（`SuperBuilder_AI.Components/Services/AuthStore.cs:31,47`）✓
- 手写 HS256 验签正确（`FixedTimeEquals` + `Exp`），但 payload 无 `aud`/`iss`（`SuperBuilder_AI/src/Application/Auth/TokenService.cs:96-138`）✓
- 确无 refresh 端点（`SuperBuilder_AI/src/Api/Controllers/AuthController.cs` 仅 `login/me/login-options/tenant-by-code`）✓
- CORS `P11Cors` = `AllowAnyHeader/Method`、无 `AllowCredentials`（`SuperBuilder_AI/src/Api/Program.cs:407-413`）✓
- Web 端无 cookie 认证 / 无 `AddHttpContextAccessor` / 无 DataProtection（`SuperBuilder_AI.Web/Program.cs` 全文）✓
- `ApiAuthorizationFilter` 为「已认证即放行 / 匿名即 401」二元门禁，不解析 `[Authorize]` 策略（`SuperBuilder_AI/src/Api/Security/ApiAuthorizationFilter.cs:62-73`）✓
- `IConnectionMultiplexer` 仅 `RateLimit:Store:Type=Redis` 才注册（`SuperBuilder_AI/src/Api/Program.cs:480-490`），故 Phase 3 必须**新增独立 `Session:Redis:Configuration` 开关**，不可复用限流开关 ✓
- 访问令牌时效为**四处独立硬编码**（`SuperBuilder_AI/src/Application/Auth/TokenService.cs:62` 字段初始化器 + `SuperBuilder_AI/src/Api/Controllers/AuthController.cs:107` / `SuperBuilder_AI/src/Application/Identity/SelfRegistrationService.cs:153` / `SuperBuilder_AI/src/Api/Controllers/TenantMembershipController.cs:182` 的 `3600` 字面量），非配置驱动、无共享常量；统一治理见 `2026-09-hardcoded-config-audit.md` ✓

### 8.4 落地顺序建议（2026-09-18 评审）

1. **先做硬编码审计计划的「认证时效单一来源」**：新增 `Auth:AccessTokenLifetimeMinutes` 配置 + `ITokenService.Lifetime`，替换四处 `3600`/`60min`（`AuthController` / `TokenService` / `SelfRegistrationService` / `TenantMembershipController`）。此项与登录流程解耦，可独立发。
2. **再做本计划 Phase 0 首屏门禁**：消除启动先 home 后 login 的闪烁（纯前端，低风险）。
3. **最后进入 Phase 1 cookie/session 改造**：(j) 优先用 Web 登录代理在普通 HTTP response 写 cookie；若采用过渡 Session API，则必须使用短时、一次性 handoff code；(k) `AuthStore` 是 RCL 共享 → 抽 `IAuthPersistence` 隔离 Web/MAUI。Phase 1 按「RCL 持久化抽象 → cookie 写入入口 → 服务端会话与还原」三个任务推进。
4. **Phase 2 refresh token 落地时同步建立 API 侧数据库权威存储**：保存 token 哈希与 family 轮换链，覆盖并发刷新、复用检测和 `SecurityStamp` 吊销；不能只把 refresh token 放在 WebSessionStore 后交给 API 无状态校验。
