# SuperBuilder AI Native BI - 项目长期记忆（精简版）

> 详细阶段进度以 `docs/DevelopmentPlan.md`（主阶段脊柱，唯一事实来源）+ `docs/PhaseChecklist.md` 为准；本文件仅留跨会话有用的结论与约束。

## 项目定位与技术栈
- AI Native BI：自然语言 → AI理解 → 语义分析 → 查询计划 → SQL → 数据分析 → 业务答案
- .NET 10 (net10.0) + EF Core 10 + Dapper；Qdrant 向量库(1024维)；Qwen LLM；多数据库(SQL Server/MySQL/PostgreSQL)
- 业务库 WMS MySQL `192.168.16.120:3306`（用户确认现已可达）
- 主交付文档（唯一执行计划）：`docs/Master_Development_Plan.md`（v2.0，取代 DevelopmentPlan/PhaseChecklist/DevChecklist_Final 的分散跟踪职责）；源需求保留于 `docs/Audit_Report_and_DevPlan.md`、`docs/DevChecklist_Final.md`、`docs/DevelopmentPlan.md`、`docs/PhaseChecklist.md`

- **P11.6 前端结构与开发清单（2026-09-02，未提交）**：页面六组化 + 通用组件库 + Layout 层 + 9 个新页面（S0 ✅）+ S1 组件化改造全部完成（S1-1~S1-5 ✅）+ S2 写操作闭环全部可补项完成（S2-1/2/3/4/5/6 ✅：删除·新建·启停·DSL 编辑器·角色权限·租户设置 Upsert·字段级 SbField.Error+服务端 errors 友好化；S2-7 后端无写端点⬜）。文档 `docs/Frontend_Structure_Plan.md` + `docs/Frontend_DevChecklist.md`（S0/S1/S2 ✅，S3~S5 ⬜）。三端 build 0 error。

## 阶段状态（截至 2026-09-04）
- **测试基线**：实测 **504/504 全绿、build 0 error**（Master_Development_Plan.md 标注的 431/431 / 468/468 基线均已过时）。Golden 18/18 契约未改。
- **M0 发布阻塞**：M0-09（Ask 旗舰对话）✅ 已完成；M0-02 四路由契约 ✅ 已完成（提交 f4df66c，全量 483/483）；M0-03 权限守卫 / M0-04 Ask 可靠性 ✅ 已收口（提交 fda72c1）；M0-06 字段越权与关系链 ✅ 已完成（提交 071ce82，全量 489/489）；M0-05 受控 Migration ✅ 已完成（提交 f0ed8e3，全量 495/495）；M0-08 匿名端点与限流 ✅ 已完成（提交见下，全量 504/504）；**M0-01 凭据轮换 仍为 🔴 未做（含密钥轮换流程动作，不能仅靠代码）**。上述本地提交均未 push。
- **Stage 0 基础/运行时**：✅ 全完成；Golden 18/18 PASS
- **Stage 1 架构治理**：🟡 A1/A2/A4 ✅；A3/A5 ⬜（用户指令暂缓「先不做」）
- **Stage 2 产品演进 P3~P10**：✅ 全绿（每阶段退出门槛 = Golden 18/18，硬约束）
- **P11 前端（MAUI Blazor Hybrid + Web 共享 RCL）**：✅ P11.0/P11.1/P11.2 + UI 现代化重构；✅ P11.3 全完成（15 页 + 组件库页 + 主题编辑器 + `ask/refine` 多轮语义调整）；✅ P11.4 MAUI 验证（RCL/MAUI 多目标化，Android 真原生 Signed.apk + iOS 编译链打通，已提交 `6aab6f8`）；✅ P11.5 优化轨道起步（缓存+指标已提交 `2651026`；**鉴权收尾已完成未提交**）

## 可复用零回归手法（核心）
- **门控隔离**：多语言/AI 路径一律「非默认才启用」短路，默认路径逐字节不变 → 触碰 Golden 依赖文件也安全
- **双路径 Agent/AppBuilder**：默认确定性路径(不调 LLM) + 仅显式描述启用 LLM 路径；不触碰 Golden 依赖文件
- **约束**：Golden 契约 `Evaluation/Golden/query-plan-golden-v1.json` 不可删改，多语言验证只用独立离线一致性测试

## P11 前端关键事实
- 三项目：`SuperBuilder_AI.Components`(RCL) + `SuperBuilder_AI.Web`(Blazor Server 经典模型, `_Host.cshtml`+`MapBlazorHub`) + `SuperBuilder_AI.Maui`(Blazor Hybrid, Windows 目标 0 error)；均 build 0 error；纳入 `SuperBulider_AI.slnx`
- .NET 10 MAUI 需显式引 `Microsoft.Maui.Controls`+`Microsoft.AspNetCore.Components.WebView.Maui`(10.0.20)；`UseMauiBlazorWebView()` 已移除仅用 `AddMauiBlazorWebView()`；RCL JS(`chart.js`)须显式 `<script>` 引入
- 已落地：统一错误治理(`src/Api/Errors/` + `UnifiedExceptionMiddleware` + `ApiClient.ParseApiError`，零业务抛点改动、Golden 不受影响)、Bootstrap 5.3.3 本地化、中国古风色系 `app.css`

### 本轮 UI 现代化重构（2026-08-31，待提交）
- 重做应用壳层：顶栏(汉堡/品牌/主题切换/用户芯片) + 全高 flex + 侧栏；**≤991px 侧栏变抽屉 + 遮罩**，路由变更自动收起
- `NavMenu` 加 SVG 图标精灵(Feather 风格) + 分组；移除旧侧栏品牌(品牌上移顶栏)
- `Ask.razor`：提问栏 `position:sticky` 置顶、气泡加头像、KPI 网格/图表卡/表格层次与阴影
- `Login.razor`：hero 分栏(品牌侧+表单侧)，移动端堆叠
- 主题：明/暗切换 `ThemeService`→`SuperBuilder.setTheme`，持久化 localStorage；`_Host.cshtml`+MAUI `index.html` 内联脚本防首屏闪烁(no-FOUC)
- **⚠️ 预渲染陷阱**：Blazor Server `ServerPrerendered` 阶段无 JS 运行时，`MainLayout.OnInitializedAsync` 内调用 `IJSRuntime.InvokeAsync` 会抛 500；主题读取/应用必须移到 `OnAfterRenderAsync(firstRender)`（初始 data-theme 由内联脚本设，无闪烁）
- 设计文档 `docs/P11_Frontend_MAUI_Blazor_Plan.md`

### P11.3 其余页面（2026-08-31）
- 共享基建：`Components/Shared/PageHead.razor`（页头：图标+标题+描述+右侧操作区）、`Shared/TablePresenter.cs`（任意 JSON 数组 → 友好表头/单元格/状态徽章）
- `IApiClient`/`ApiClient` 新增松类型 `GetJsonAsync` → `(JsonElement? Data, int Status, string? Error)`，不抛异常，页面优雅降级
- 15 个页面：数据类 Dashboards/Apps/BusinessModel/SemanticLabels/Agent；平台类 DataSources/ModelAccounts/Components(组件库)/Themes(主题编辑器)；管理后台 Admin/{Tenants,Identity,Audit,Quota,Localization,Themes}
- 设计系统工具类：`page-head`/`stat-grid`/`stat-tile`/`panel`/`toolbar`/`badge`/`empty-state`/`field-grid`/`seg`/`row-list`/`swatch-grid`；`NavMenu` 图标精灵共 22 symbol
- `ask/refine` 多轮语义调整：后端 `POST api/ask/refine`（`ComposeRefinedQuestion` 合成「原问题+历史(user轮)+指令」）；前端 `Ask.razor`「语义细化」框，结果作子轮次(`.turn.refine` 高亮)追加。**门控隔离**：默认 `api/ask` 路径不变，不碰 Golden 依赖文件

### P11.4 MAUI 双端验证（2026-08-31，已打通原生构建）
- **Windows 端 0 error**（复验）；`MauiProgram.cs` 用 `AddMauiBlazorWebView()` + Scoped 注册 `IApiClient/AppState/ThemeService/LocalizationService`；`index.html` 同 Web 引用 RCL 资源 + no-FOUC 主题脚本
- **RCL 多目标打通**：RCL `net10.0;net10.0-android;net10.0-ios`；MAUI 头 `net10.0-android;net10.0-ios;net10.0-windows10.0.19041.0`；补齐 `Platforms/Android`+`Platforms/iOS`+`Resources/AppIcon|/Splash`。**Android 真原生 0 error 产出 Signed.apk；iOS 0 error(AOT+原生库+资源) 但 `.app` 打包/签名需配对 Mac**（Windows 上 `_CreateAppBundle` 为空操作，属平台限制）。已提交 `6aab6f8`。
- **运行时提示**：`MauiProgram.cs` `HttpClient.BaseAddress=https://localhost:5032`，Android 模拟器内应改 `http://10.0.2.2:5032`
- **⚠️ RCL 静态资源陷阱（关键，已付学费）**：RCL **不可**引 `Microsoft.AspNetCore.Components.WebView.Maui`。该包随 RCL 发布 `_framework/blazor.modules.json` 等 WebView 宿主静态资源，与引用 RCL 的 MAUI 应用自身资源冲突（`StaticWebAsset SourceType: Project` 重复 → build error）；且 `<StaticWebAsset Remove>` 在评估期无法拦截（包资源在 build 期注入）。RCL 统一只引 `Microsoft.AspNetCore.Components.Web` + `Microsoft.Extensions.Http`（全 TFM），WebView 宿主能力由 MAUI 头项目自身提供。

### P11.5 优化轨道（2026-08-31）
- **鉴权收尾（P11.5.3，已完成待提交）**：核心守卫 `AuthMiddleware`+`TokenService`(HMAC 无状态令牌) **早已在 P11.0 落地并接进管道**（`Program.cs: app.UseMiddleware<AuthMiddleware>()`，对所有 `/api/*` 除 `login` 要求有效 Bearer/X-Api-Token 否则 401）——**计划文档 §9.3/第227行「无鉴权中间件」为过时记录**，勿再据其新建中间件。
- **⚠️ 签名密钥坑（已修）**：`TokenService` 在 `Auth:SigningKey` 缺失时回退到硬编码 dev 默认串 `dev-insecure-signing-key-P11-change-in-prod` → **任何人可伪造任意租户/权限 token**。已在本轮于 `appsettings.json` 配强随机 `Auth:SigningKey`；生产须用密钥管理覆盖此值。
- **前端 token 链路**：`Login.razor`→`AuthStore.SetFromLoginAsync`（写 `AppState`+持久化 localStorage）；`MainLayout` 启动 `RestoreAsync`+`ValidateAsync(/api/auth/me)` 自举；`ApiClient` 401→`AppState.NotifySessionExpired`→`MainLayout` 跳 `/login`。`AuthStore`/`AppState` 须在两个 Head(Web+MAUI) 注册 `AddScoped`；`AppState` 现含 `event Action? SessionExpired` + `ClearSession()`。
- **验证**：三端(API/Web/MAUI-Win) build 0 error；单测 308/308；运行时实测 无 token→401 / 坏 token→401 / 合法 token→200 / `/metrics`→200。Golden 走独立 `evaluation/golden-runtime` 端点隔离。

### P11.6 前端结构（2026-09-02）
- 目录：`Pages/{Analysis,Design,Platform,Admin,Account,Errors}`；`Shared/{UI,BI,Feedback,Guard}`；`Layout/{MainLayout,BlankLayout,NavMenu,NavMenuItems,AppBreadcrumb,IconSprite}`。新增目录必须同步两个 Head 的 `_Imports.razor`，且目录内至少有一个组件（空目录 → CS0234 命名空间不存在）
- 导航单一事实来源 `Layout/NavMenuItems.cs`（Href/Title/Icon/Permission）；`EnforcePermissions` **默认 false**，避免权限码未对齐时把管理菜单全隐藏（对齐后置 true）
- 服务新增：`ToastService`（Scoped，需在 Web/Program.cs 与 Maui/MauiProgram.cs 同时注册）；`AppState.SessionRestored` 由 `AuthStore.RestoreAsync` 的 finally 置位
- `IApiClient` 已补 `SendAsync/PostAsync/PutAsync/PatchAsync/DeleteAsync/GetTextAsync`，写操作可直接在页面使用
- 根路由修正：`/` = Home 工作台，`/login` 专用（原 Login 同时占两个路由，导致侧栏「首页」跳登录）

### ⚠️ Blazor/Razor 踩坑（务必遵守，已付学费）
5. **属性引号**：内联 `Nav.NavigateTo("x")` 的事件处理器属性必须用单引号定界 `@onclick='() => Nav.NavigateTo("x")'`，双引号会提前闭合属性 → 生成代码 CS1026
6. **`<details>` 不支持 `@bind-open`**（RZ9991）；同理 HTML 属性绑定只支持 `bind` / `bind-value` 形式
7. **登录判定必须等 `AppState.SessionRestored`**：子组件 `OnAfterRender` 早于 `MainLayout` 的会话自举，只看 `IsAuthenticated` 会误判
8. **串行 build**：并行 `dotnet build` 同一解决方案会因 bin 文件被对方重建删除而报 MSB3030
1. **事件处理器含 C# 字符串 → 属性用单引号定界**：`@onclick='() => Toast("文本")'` ✅；`@onclick="() => Toast('文本')"` ❌（单引号变 char 字面量 → CS1012）；`$"..."` 内嵌双引号会提前闭合属性 → CS1056/CS1026。
2. **渲染名为 `code` 的变量必须写 `@(code)`**：`@code</span>` 被当作 `@code` 指令 → RZ2005/RZ1017。
3. **void 方法不能直接绑 `@onclick='Toast("x")'`**（CS1503 void→EventCallback）→ 包 lambda `'() => Toast("x")'`。
4. **DI：页面只能注入 `IApiClient`**（注册为 `AddScoped<IApiClient, ApiClient>()`）；注入具体类 `Services.ApiClient` 会 500「无注册服务」。**新增方法必须同步加到 `IApiClient` 接口**，否则改用接口注入后编译不过。

## 架构治理 Phase 4
- 234 .cs 已迁至 `src/` 四层(Domain 79/Application 109/Infrastructure 29/Api 17)；旧顶层目录清空
- 待合并重复：GoldenBaseline↔GoldenBaselinePersistenceRecord；QueryPlanSemanticResolution↔SemanticApplicabilityResult/*Resolution

## 历史教训（回归防护）
- Phase3.1 事故：`MetadataCsvFixtureService.ImportAsync` 全局 RemoveRange 清空全部租户 → 18 case 全 BLOCK；根治为仅限自身租户子树
- Golden 运行时实时调 Qwen 有非确定性抖动（GQ-008 幽灵维度/403 限流），非环境问题；判定字段 `expectedOutcomeSatisfied`
- 后端启动：`ASPNETCORE_URLS="http://localhost:5032" dotnet bin/Debug/net10.0/SuperBuilder_AI.dll`（默认 5000，必须显式设）；Qdrant 从中性可写 CWD 启动(/c/tmp/qdrant_run)
