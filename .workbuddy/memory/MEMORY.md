# SuperBuilder AI Native BI - 项目长期记忆（精简版）
> 唯一事实来源：`docs/Master_Development_Plan.md`(v2.0)。

## 技术栈
.NET 10 + EF Core 10 + Dapper；Qdrant(1024维)；Qwen LLM；多库(SQL Server/MySQL/PostgreSQL)；业务库 WMS MySQL `192.168.16.120:3306`(已可达)。

## 本地运行环境（关键，2026-09-10 实测）
- Qdrant：`~/Downloads/qdrant-x86_64-pc-windows-msvc/qdrant.exe`，storage 在**同目录 `./storage`**（非 config/ 所指路径），REST 6333 / gRPC 6334，集合 `superbi_metadata`。
- ⚠️ **宿主有代理环境变量** `HTTP_PROXY/HTTPS_PROXY/http_proxy/https_proxy=http://127.0.0.1:53963` → 会导致 .NET gRPC 连 localhost 报 `unable to establish HTTP/2 connection`。`QdrantService` 已改为 `SocketsHttpHandler{UseProxy=false}` 豁免（勿回退为 `new QdrantClient(host,port)`）。
- 元数据库 SQL Server `localhost/SuperBuilder_Platform`（Uid=live Pwd=root）；后端跑 `ASPNETCORE_URLS=http://localhost:5032 dotnet bin/Debug/net10.0/SuperBuilder_AI.dll`。
- 租户实测：**1=platform / 2=demo(禁用) / 3=e2e / 4=e2eapp / 5=bizcheck**；口令统一 `longping00`；平台管理员 `platform-admin`@1。数据源在租户4：`WMS入库`(id=1)。
- ⚠️ **E2E 账号真值**：管理员 `e2eadmin`＝**用户4/租户4**（含 `app:create/edit/publish`）；读者 `e2ereader`＝**用户5/租户4**（viewer，仅 `app:view`）。**租户3 另有同名 `e2ereader`（用户3）但该租户无任何数据源** → Ask 必 0 行。读者必须与管理员**共用租户与数据源**。
- ⚠️ **`ask-publish-box` 仅在 `resp.Data.Rows.Count>0` 时渲染**（`AskTurnCard.razor:45`）——读者无数据源 → 元素不出现 → 60s 超时，非 UI 缺陷。
- ⚠️ **PlatformLogin 需独立平台凭据**（`platform-admin`@1）；用业务管理员登录 `platform` 租户必然失败。

## 已知缺陷与修复（2026-09-10 实证）
- ✅ **登出缺陷已中心化修复**：页面自身 `OnAfterRenderAsync(firstRender)` 早于 `MainLayout` 异步自举 → 请求不带 Bearer → 401；旧 `OnUnauthorized()` 在 401 异步返回且自举已完成时误判会话过期 → 清 localStorage + 跳 `/login`（实测 `/agent`、`/business-model` 硬加载必登出，E2E 9/9 路由现正常）。**修复**：`ApiClientBase.OnUnauthorized(bool requestCarriedToken)` —— 仅当「请求确实携带令牌」且「当前已登录」才清会话；派生客户端 14 处调用点同步改造。验证：三端 build 0 error、单测 1061/1061 绿、E2E 9/9 路由不再登出。
- 🔴 **API 观测盲区（取证陷阱，待决策）**：`Program.cs` 中间件顺序 `AuthMiddleware(599)`→`RateLimitMiddleware(600)`→`UseAuthorization(601)`→**`ObservabilityMiddleware(603)`** → **401/403/429 全部短路，日志看不到**。实证：`curl /api/agent/plans?tenantId=0` → 401，RES 行数 143→143（新增 0）。**本项目 API 日志不能作为「无鉴权失败」的证据**。前移观测中间件会丢失 REQ 日志租户信息，权衡未擅改。
- 其余 E2E 既有缺口（与登出修复无关，待决策）：`/ask` 无 `AuthGuard`（`AutoRedirect` 仅 `Home.razor` 用）、全仓**无 `/forbidden` 路由**（`PermissionGuard` 实际渲染内联「无访问权限」）、`TenantUiLanguages` 每租户仅播 zh-CN → 语言切换器（要求 `AvailableCultures.Count>1`）**在全新安装中永不出现**。

## 迁移集完整性（2026-09-10 已验证完整）
- `AppPlans` 4 列（`PublishedDslJson/PublishedVersion/PublishedAt/PublishedBy`）曾缺失：`M7_02_AppVersion` 重写后只建 `AppVersions`，漏了这 4 个 `AddColumn`（快照有、迁移无）→ `from-ask` 500/`SqlException 207`。已由补丁迁移 `20260910053000_M7_02_Fix_AppPlanPublishColumns` 修复（`AppPlans` 15→19 列，from-ask 500→201）。
- **验证方法（权威）**：`dotnet ef migrations has-pending-model-changes` **检不出**「快照有、迁移无」（只比快照 vs 模型）。必须**空库重放**：`migrations script --idempotent` → 空库 `sqlcmd -I -b <full.sql>`（**必须 `-I`**，否则 `CREATE INDEX` 报 1934）→ 与 `SuperBIContextModelSnapshot.cs` 解析出的表/列做双向差集。**当前 A/B/C 差集全 0**；结构计数 46 表/1797 列/326 索引/45 FK/49 默认约束/317 注释/43 迁移 全等。
- ⚠️ 对已手工打补丁的开发库做 diff 会**假绿**——务必比「迁移集产物」。
- `sqlcmd` 路径 `/c/Program Files/Microsoft SQL Server/Client SDK/ODBC/170/Tools/Binn/sqlcmd`；**用 stdin 重定向**（`-i` 在此 shell 下路径被 MSYS 破坏），`-Q` 内联可用。
- ⚠️ **元数据扫描是「追加」非「重建」**：换数据源/重建库后旧向量残留会污染语义检索（回查元数据落空 → 伪装成 `SB_AUTHZ_001 403`）。重扫后须按 `tableId` 范围删旧点。
- ⚠️ **当前只绑定 WMS 仓储库，无销售类数据源**。测试问句须贴合仓储域（出入库/库存/物流/调拨）。销售类问题（「销售额」「品类」）得到低置信度或 `SB_BI_002` 是**预期正确行为，不是缺陷**；`MetadataSemantics` 已覆盖全部列（513 条，置信度 0.95），`SemanticLabels/BusinessDomains/BusinessEntities` 为 0 属正常（术语治理层未启用）。
- 端点易混：租户管理 `POST /api/tenant-management`（非 `/api/platform/tenants`）；元数据扫描 `POST /api/data-sources/{id}/metadata/scan`（**在 MetadataController**）。
- Python venv：`~/.workbuddy/binaries/python/envs/default`（已装 pymysql；无 mysql CLI，查业务库用 `/c/tmp/checkwms.py`）。

## 测试基线
全量单测 `SuperBuilder_AI.Tests` = **1061/1061 绿、build 0 error**（2026-09-10 复跑；含 QdrantService 代理豁免 + 登出中心化修复，零回归；较旧基线 1059 +2 = 新增 `FocusedApiClientTests` 两用例）。⚠️ `RateLimitMiddlewareTests.Expired_Windows_Are_Evicted_To_Prevent_Unbounded_Growth` 为**时间敏感 flaky**（全量跑偶发失败、单独复跑 7/7 绿）。
**E2E**（`tests/SuperBuilder_AI.E2E.Tests`，需 `SB_E2E_BASE_URL` + `SB_E2E_API_URL`）：`AppRuntimeE2ETests` 两条 ✅、`PlatformLoginE2ETests` ✅（2026-09-10）；仍有 3 条既有缺口失败（`/ask` 无 `AutoRedirect`、无 `/forbidden` 路由、`TenantUiLanguages` 每租户仅播 zh-CN → 语言切换器不渲染）。Golden 契约 `Evaluation/Golden/query-plan-golden-v1.json` 18/18 未改。

## 里程碑状态(2026-09-08)
- M0 ✅（M0-01~09，Git 历史重写+凭据轮换+强制推送，2026-09-04 收尾）；Stage 0 ✅；Stage 1 🟡(A3/A5 暂缓)；Stage 2 P3~P10 ✅；P11 前端 P11.0~P11.5 ✅（三端 build 0 error）。
- M1-01~06 ✅；M2-01~07 ✅（M2-06 默认关自注册、M2-07 Demo 安装器）；M3：G0+M3-01 ✅（2026-09-05），M3-02~06 门禁未关；M4 ✅（2026-09-06）。
- M5-01~14 ✅；M6 ✅；M7-01~10 ✅；M8-01~07 ✅；M9-01 拆 ApiClient ✅(994)、M9-02 命名空间治理 ✅(999)、M9-03 BusinessTerm 强类型 ✅(1002)；M9-04~15 门禁未关。
- M10/M11 未启动。下一步：M9 架构/测试/运维治理收尾 + **M7-11 应用闭环**(当前主线)。

## M7-11 应用闭环（当前主线, 2026-09-08 冻结·第六轮勘误）
- 契约：`docs/M7-11_App_Runtime_Contract.md`（已冻结·第六轮勘误）。错误码 SB_APP_*；支持矩阵拒 JOIN/between/多字段排序/Distinct/IN（首批 422）；快照 RLS 注入前截取、仅成功落库；幂等/并发(创建冲突/copy/发布重试同事务+DraftRevision 乐观令牌)；三项列实现验收必过(§14)：copy 须来源读权限+app:create、一致性断言覆盖聚合/筛选操作符+值/排序方向/Limit/别名/模式、发布版本更新并发保护。
- 顺序：P0-A 环境身份实测 → P0-B 真实取数 → C1 binding / C2 render 端点 / C3 前端渲染器 / C4 发布接线 → P0-D1 人工 / P0-D2 自动化验收。
- **实现进度(2026-09-08)**：C1/C2 后端已完成。
  - C1：`AskQuerySnapshot`+`AskQuerySnapshotStore`(EF)；`BIConversationService` RLS 前截取(仅成功落库, turnId→BIResponse.TurnId)；`AppQueryBindingExporter`(归属 403/404、§9 矩阵 422、确定性映射；修复 eq 筛选值被吞→一致性断言 422 回归)。
  - C2：`AppQueryExecutor`(绕过 NLU 直构 QueryIntent、`requestedDataSourceId` 硬约束、复用授权/RLS/SecurityGate 且缺依赖即拒、Pipeline 后一致性断言 422)+`AppRenderModels`+`AppBuilderController` 全端点 `app:*` 收口+from-ask/preview/render/copy+服务端草稿裁剪。
  - 接线：Program.cs 注册 `IAskQuerySnapshotStore/IAppQueryBindingExporter/IAppQueryExecutor`；迁移 `20260908051650_M7_11_AskQuerySnapshot`(命名空间 `SuperBuilder_AI.Infrastructure.Persistence.Migrations`)。
  - 验证：`M7_11_AppRuntimeTests.cs` 11 例绿；全量 1013/1013 绿。IN 多值暂按 422(绑定 `Values` 已预留)。
  - **C3 前端渲染器 ✅（2026-09-08）**：`AppRenderer` 共享组件(text/table→ResultTable/chart→ChartView/kpi→SbStatTile，Columns 元数据重映射表头、整体与逐组件失败展示)；`AppRun.razor`(`/apps/{code}/run`,app:view)+`AppPreview.razor`(`/apps/{code}/preview`,app:edit)；`IAppApiClient` 新增 `RenderAppAsync`/`PreviewAppAsync`(GET render/preview→`AppRenderDto`)；`AppDetail` 增加运行/预览按钮(`PermissionGuard`)、`Apps` 卡片增加运行入口；`Keys.Content` 补 C3 文案+i18n 字典。三端 build 0 error。
  - **C4 发布接线 ✅（2026-09-08）**：`Ask` 发布改「创建草稿(POST api/apps)→publish(POST api/apps/{code}/publish)」两段，修"假成功"；`IAppApiClient` 新增 `PublishExistingAsync`；`AskTurn` 增 `PublishStage`(None/DraftSaved/Published/Failed)+`PublishCode`(重试复用)；`AskTurnCard` 按阶段展示(成功显运行入口/失败显重试按钮)、发布按钮加 `app:publish` `PermissionGuard`；`Keys.Content` 补 C4 文案+i18n。后端 `ResourceKeys.cs`(常量+Catalog en-US)+`LocalizationSeedService.ZhCnDefaults`(zh-CN) 同步 25 个 C3 键+6 个 C4 键补镜像(ResourceKeyRegistryTests 9/9 绿)；全量 1015/1015 绿，三端 build 0 error。
  - **P0-D2 验收 ✅（2026-09-10）**：E2E 主线 `AppRuntimeE2ETests`×2 + `PlatformLoginE2ETests` 全绿（需 `SB_E2E_BASE_URL`+`SB_E2E_API_URL`+Chromium）；三端 build 0 error、单测 1061/1061 绿。P0-D1（人工验收）待平台管理员 demo 凭据（未入仓）。
  - **平台专用登录入口 ✅（2026-09-08）**：新增 `Pages/Admin/PlatformLogin.razor`(`/admin/login`，独立路由+`BlankLayout`，绕过业务租户选择器)。逻辑：`OnAfterRender` 调 `GET api/platform-bootstrap/status` 取 `platformTenantId` → `Api.LoginAsync(user, platformTenantId, pwd)` → `AuthStore.SetFromLoginAsync`（复用业务登录写入路径，token 入 `sb_auth_v1`）→ 跳 `/admin/tenants`。`Login.razor` 加「平台运营登录」链接指向 `/admin/login` 便于发现。`Keys.PlatformLogin` 8 键已在 RCL `Keys.cs`+后端 `ResourceKeys.Catalog`(en-US)+`LocalizationSeedService.ZhCnDefaults`(5 语言) 三处同步(ResourceKeyRegistryTests 9/9 绿)；Web/Maui 构建 0 error、全量 1015/1015 绿。E2E 新增 `PlatformLoginE2ETests.PlatformAdmin_Login_ByDedicatedEntry_ReachesTenantManagement`（UI 走 `/admin/login`→断言到 `/admin/tenants`），E2E 工程 0 error。
  - **登录约束（E2E 关键）**：`platform` 租户被登录 UI 刻意排除（`AuthController.LoginOptions`/`TenantByCode` 两 GET 排除 `TenantCode=="platform"`），但 `POST /api/auth/login` 仅按数字 `TenantId` 校验、不排除 platform → `platform-admin`(**TenantId=1**) 可经 **`/admin/login` 页面**或 **API 登录** 进入（`/admin/tenants` 等由 `PlatformTenantView`/`PlatformAdminManage` 守卫；`NavMenuItems` 在持平台权限时显示）。E2E `LoginHelper.ApiLoginAsync(page,user,pwd,tenantId)` 已实现（fetch 登录→注入 `sb_auth_v1` 快照→进 /ask）；`AppRuntimeE2ETests` 管理员/读者均走此路径，`SB_E2E_TENANT=1`(平台管理员,platform=1)/`SB_E2E_READER_TENANT=4`(读者 e2ereader=用户5/租户4，须与管理员 e2eadmin 用户4/租户4 共用租户4数据源；**非 3**，租户3 无数据源→Ask 必 0 行)。demo 限流 429 来自 `RateLimitMiddleware`（默认 LoginLimit=10/60s，localhost 同 IP 共用桶）→ 调大 `RateLimit:LoginLimit/GlobalLimit`+重启实例即解。
- 易错定位：`MetadataController` 有 `POST /api/data-sources/{id}/metadata/scan`（不在 DataSourcesController）；E2E 目录 `tests/SuperBuilder_AI.E2E.Tests`（未配 SB_E2E_BASE_URL 跳过，缺 Chromium 无兜底）。

## 零回归手法
门控隔离(多语言/AI 路径非默认启用短路，默认路径不变)+双路径 Agent/AppBuilder(默认确定性不调 LLM)。Golden 契约不可删改。

## P11 前端关键陷阱
- 三项目 Components(RCL)+Web(Blazor Server)+Maui(Hybrid,Win 0 error)，纳入 SuperBulider_AI.slnx。
- ⚠️ `ServerPrerendered` 阶段无 JS → `IJSRuntime.InvokeAsync` 在 `MainLayout.OnInitializedAsync` 抛 500；主题移到 `OnAfterRenderAsync(firstRender)`（首屏 data-theme 内联脚本设 no-FOUC）。
- ⚠️ RCL 不可引 `Microsoft.AspNetCore.Components.WebView.Maui`（与 MAUI 头冲突）；RCL 只引 `Web`+`Extensions.Http`(全 TFM)。
- ⚠️ `TokenService` 缺 `Auth:SigningKey` 回退硬编码 `dev-insecure-signing-key-P11-change-in-prod`→可伪造；生产须密钥管理覆盖。
- token 链路：`Login`→`AuthStore.SetFromLoginAsync`→`AppState`(localStorage)；`MainLayout` 自举 `RestoreAsync`+`ValidateAsync(/api/auth/me)`；`ApiClient` 401→`AppState.NotifySessionExpired`→`/login`。
- 登录租户兜底：M0-08 `Auth:ShowTenantDirectory` 默认 false → `Login.razor` 渲染手动租户编码输入，经 `/api/auth/tenant-by-code?code=` 解析；开发环境设 `ShowTenantDirectory=true`。语言切换器缺失多因 DB 未播种 `UiLanguages`(Program.cs:430 无条件种子 5 语言)，重启即现。

## Blazor/Razor 踩坑
1. 事件含 C# 字符串→属性单引号 `@onclick='() => Toast("文本")'`；双引号提前闭合 CS1056/CS1026；单引号变 char CS1012。
2. 渲染 `code` 变量写 `@(code)`，否则被当指令 RZ2005/RZ1017。
3. void 方法不可直绑 `@onclick='Toast("x")'`(CS1503)→包 lambda。
4. DI 注入 `IApiClient` 或各域 `I*ApiClient`(M9-01)；注入具体类 `Services.ApiClient` 会 500；新增方法须同步接口。
5. `<details>` 不支持 `@bind-open`(RZ9991)；只 `bind`/`bind-value`。
6. 登录须等 `AppState.SessionRestored`；子组件 `OnAfterRender` 早于 `MainLayout` 自举。
7. 并行 `dotnet build` 同方案 bin 重建删报 MSB3030；新增目录须同步两 Head `_Imports.razor` 且至少一个组件。

## 架构治理(M9-02 固化)
234 .cs 迁 `src/` 四层(Domain 79/App 109/Infra 29/Api 17)。命名空间双轨：目录=分层，命名空间=关注点(`Models`=Domain/`Services`=Application/`Controllers`=Api/`Interfaces`=Ports/`Data`+`Infrastructure`=Infra)。`Services.BI`/`Models.BI` 保留主流——新增 Domain 类型入 `Models.BI`；迁移统一 `Infrastructure.Persistence.Migrations`；`ArchitectureTests` 4 不变量防回归(无`src.`前缀/迁移单命名空间/源文件属 SuperBuilder_AI 树/Domain 层用 Models)。业务术语须 `BusinessTerm`(`Models.BI`) 强类型，不得裸 string(M9-03, 3 不变量)。待合并：GoldenBaseline↔GoldenBaselinePersistenceRecord；QueryPlanSemanticResolution↔SemanticApplicabilityResult/*Resolution。

## 历史教训
- Phase3.1：`MetadataCsvFixtureService.ImportAsync` 全局 RemoveRange 清空全部租户→18 case BLOCK；限自身租户子树。
- Golden 实时调 Qwen 有抖动(GQ-008/403)非环境问题；判定 `expectedOutcomeSatisfied`。
- 后端起：`ASPNETCORE_URLS="http://localhost:5032" dotnet bin/Debug/net10.0/SuperBuilder_AI.dll`；Qdrant 从 /c/tmp/qdrant_run。
- Git 历史重写复盘：① filter-repo 必后台(前台 120s SIGTERM 损 HEAD)；② 中断 `git bundle create --all` 备份；③ 替换规则覆盖所有真实值(WMS/元库/LLM Key+旧 `appsettings.json` 的 `Auth:SigningKey` 64 字符 HMAC)；④ 顶层 `mv` busy→改 `.git` 改名腾空；⑤ `rm -rf`>50 文件触发确认；⑥ 记忆/文档切勿写明文凭据。
