# SuperBuilder AI Native BI - 项目长期记忆（精简版）
> 唯一事实来源：`docs/Master_Development_Plan.md`(v2.0)。本文件仅留跨会话有用结论与约束。

## 技术栈
.NET 10 + EF Core 10 + Dapper；Qdrant(1024维)；Qwen LLM；多库(SQL Server/MySQL/PostgreSQL)；业务库 WMS MySQL `192.168.16.120:3306`(已可达)。

## 阶段状态(2026-09-08 刷新)
- 测试基线：全量测试保持全绿、build 0 error（M7 全量 957 绿、M7-06 923/923、M7-04 916/916；M8 后继续增长）；Golden 契约 `Evaluation/Golden/query-plan-golden-v1.json` 18/18 未改。
- M0：M0-01~M0-09 **全部 ✅（2026-09-04 收尾）**。M0-01 = Git 历史重写(629 提交, `git log --all -S` 5 类明文 0 命中) + 外部凭据轮换(WMS/LLM/Auth:SigningKey/元库) + 强制推送至 `wenzhong0606-dev/SuperBuilder_AI`。M0-02~M0-08 经本回合代码核查确认已在 P10/P11 落地：`AuthMiddleware`+`TokenService`+`TenantDataPlanePolicy`+`RowLevelSecurityService`(Program.cs 已接线, deny-by-default)、`SchemaProbe`+固定迁移/种子启动序列(受控 Migration)、`RateLimitMiddleware`+`ForwardedHeaders` 仅信任配置代理 + `/metrics` 受 `platform:diagnostics:view` 守卫(限流与匿名端点治理)、TenantId 取自 JWT 而非请求体(字段越权/跨租户隔离)、四路由契约统一为 `api/agent|apps|ask|data-sources` 等。测试基线按既有记忆：476/476 全绿、build 0 error、Golden 18/18。
- Stage 0 ✅；Stage 1 🟡(A3/A5 暂缓)；Stage 2 P3~P10 ✅；P11 前端 P11.0~P11.5 ✅（UI 现代化、P11.5 鉴权收尾、P11.6 结构等部分改动仍待提交，三端 build 0 error）。
- 里程碑交付（截至 2026-09-08，git HEAD = M8-07）：
  - M0 ✅（M0-01~09 全部）；M1-01~06 ✅；M2-01~07 ✅（含 M2-06 默认关闭自注册、M2-07 独立 Demo 安装器）。
  - M3：G0 多语言核心子集 ✅、M3-01 语言关系模型 ✅（均 2026-09-05）；M3-02~06 仍为发布门禁未关闭。
  - M4 数据源与元数据闭环 ✅（2026-09-06）。
  - **M5-01~14 全部 ✅**（语义模型与 QueryPlan 企业化：规范语义模型/统一字段解析/QueryPlan 阶段化/列级安全/查询成本治理/Decision Gate 状态化/治理策略真实启用/AI 决策审计/Production Feedback 闭环/AI BI E2E 执行闭环/GQ-006 DirectKey 解析/Phase2.7 回归/Ask 首用例闭环）。
  - **M6 ✅**（M6-02 Ask 输入状态契约硬化、M6-03 行为分类+结构化澄清+循环检测、M6-04 7 维语义缓存、M6-05 Ask 审计+分段指标+脱敏）。
  - **M7-01~10 全部 ✅**（Dashboard/App 草稿发布隔离+可追踪回滚、Agent Runtime 受控执行框架、Agent Safe 工具接真实后端、ModelAccounts 加密绑定+模型目录、ThemeEditor 真实持久化、主题复用生命周期、配额治理）。
  - **M8-01~07 全部 ✅**（统一视觉系统 tokens、列表/表格/菜单/响应式一致性、导出标准化、性能与图表真实性、生命周期与前端安全 CSP/async void/Token XSS、E2E+a11y+视觉回归基建、物理设备 API 基地址可配）。
  - **M9-01 拆分 God ApiClient ✅（2026-09-08，994/994 测试全绿、四端 0 error）**：`ApiClient` 由 761 行单体重构为 `IApiClient` 向后兼容门面 + 7 个聚焦域客户端（Identity/Admin/BI/App/Dashboard/Agent/DataSource，各含 `I*ApiClient` 接口与 `ApiClientBase` 基类）；39 个既有调用点零改动。
  - **M9-02 统一 Namespace 与目录边界 ✅（2026-09-06，999/999 测试全绿、四端 0 error）**：双轨约定固化——目录按分层(Domain/Application/Infrastructure/Api)、命名空间按关注点(Models=Domain/Services=Application/Controllers=Api/Interfaces=Ports/Data+Infrastructure=Infra)；修复 `src.` 前缀与迁移双命名空间(81 文件统一 `Infrastructure.Persistence.Migrations`)、`git mv` 移走 Diagnostics 中 16 个 Controller 与 Interfaces/BI 2 游离文件、归一化关注点命名空间内游离文件(`Application.BiQuery`→`Services.BI`/`Application.Metadata`→`Services`/`Configuration`→`Application.Common.Options`/`Services.Database`→`Infrastructure.Database`；Domain 误用 `Services.BI` 4 文件→`Models.BI`、SharedKernel `Models.BI` 1→`Models` 按路径精确编辑以免误伤保留命名空间)；`ArchitectureTests` 新增 4 不变量防回归(无 `src.` 前缀/迁移单命名空间/源文件属 SuperBuilder_AI 树/Domain 层用 Models 命名空间)。M9-03~15 仍为发布门禁未关闭。
  - M10（扩展能力）、M11（长期储备）计划内未启动。
  - 下一步：**M9 架构/测试/运维治理**。

## 零回归手法
门控隔离(多语言/AI 路径「非默认才启用」短路，默认路径逐字节不变) + 双路径 Agent/AppBuilder(默认确定性不调 LLM)。Golden 契约 `Evaluation/Golden/query-plan-golden-v1.json` 不可删改。

## P11 前端关键陷阱
- 三项目：`SuperBuilder_AI.Components`(RCL)+`.Web`(Blazor Server)+`.Maui`(Blazor Hybrid,Win 0 error)，纳入 `SuperBulider_AI.slnx`。
- ⚠️ 预渲染：`ServerPrerendered` 阶段无 JS，`MainLayout.OnInitializedAsync` 内 `IJSRuntime.InvokeAsync` 抛 500；主题须移到 `OnAfterRenderAsync(firstRender)`（首屏 data-theme 由内联脚本设，no-FOUC）。
- ⚠️ RCL 不可引 `Microsoft.AspNetCore.Components.WebView.Maui`（与 MAUI 头冲突→build error）；RCL 只引 `Microsoft.AspNetCore.Components.Web`+`Microsoft.Extensions.Http`(全 TFM)。
- ⚠️ 签名密钥坑(已修)：`TokenService` 缺 `Auth:SigningKey` 回退硬编码 `dev-insecure-signing-key-P11-change-in-prod`→可伪造 token；生产须密钥管理覆盖。
- token 链路：`Login`→`AuthStore.SetFromLoginAsync`→`AppState`(localStorage)；`MainLayout` 自举 `RestoreAsync`+`ValidateAsync(/api/auth/me)`；`ApiClient` 401→`AppState.NotifySessionExpired`→跳 `/login`。
- 登录租户兜底（2026-09-06 修复）：`Auth:ShowTenantDirectory`（M0-08 默认 `false`）关闭时 `LoginOptions` 返回 `[]` → `Login.razor` 改渲染「手动租户编码」输入，`DoLogin` 经匿名端点 `/api/auth/tenant-by-code?code=`（`AuthController.TenantByCode`，排除 platform/停用）解析 `Id` 后再登录；开发/演示环境在 `appsettings.Development.json` 设 `ShowTenantDirectory=true` 可见完整下拉。语言切换器缺失多为运行实例 DB 未播种 `UiLanguages` → 重启后端（Program.cs:430 无条件种子 5 语言）即现，非登录代码 bug。

## ⚠️ Blazor/Razor 踩坑
1. 事件处理器含 C# 字符串→属性单引号：`@onclick='() => Toast("文本")'`；双引号提前闭合→CS1056/CS1026；`@onclick="() => Toast('文本')"` 单引号变 char→CS1012。
2. 渲染 `code` 变量写 `@(code)`，否则 `@code</span>` 被当指令→RZ2005/RZ1017。
3. void 方法不能直接绑 `@onclick='Toast("x")'`(CS1503)→包 lambda。
4. DI 页面可注入 `IApiClient`(向后兼容门面, `AddScoped<IApiClient,ApiClient>()`) 或各域 `I*ApiClient`(M9-01 聚焦客户端: IIdentity/IAdmin/IBi/IApp/IDashboard/IAgent/IDataSource)；注入具体类 `Services.ApiClient` 会 500；新增方法须同步加接口(`IApiClient` 或对应 `I*ApiClient`)。
5. `<details>` 不支持 `@bind-open`(RZ9991)；HTML 属性绑定只支持 `bind`/`bind-value`。
6. 登录判定须等 `AppState.SessionRestored`；子组件 `OnAfterRender` 早于 `MainLayout` 自举。
7. 并行 `dotnet build` 同方案会因 bin 被重建删报 MSB3030；新增目录须同步两 Head 的 `_Imports.razor` 且至少一个组件。

## 架构治理
234 .cs 迁至 `src/` 四层(Domain 79/App 109/Infra 29/Api 17)；待合并：GoldenBaseline↔GoldenBaselinePersistenceRecord；QueryPlanSemanticResolution↔SemanticApplicabilityResult/*Resolution。
**命名空间约定(双轨, M9-02 固化)**：目录=分层，命名空间=关注点(`Models`=Domain/`Services`=Application/`Controllers`=Api/`Interfaces`=Ports/`Data`+`Infrastructure`=Infra)；`Services` 跨 Application+Infrastructure 目录为有意设计(非碰撞)。`SuperBuilder_AI.Services.BI`/`Models.BI` 为保留主流命名空间——新增 Domain 类型须入 `Models.BI` 不得入 `Services.BI`；迁移统一 `Infrastructure.Persistence.Migrations`；`ArchitectureTests` 4 条命名空间不变量(`NoNamespaceContainsIllegalSrcPrefix`/`AllMigrationsShareSingleNamespace`/`EverySourceFileHasSuperBuilderAiNamespaceExceptProgram`/`DomainLayerUsesModelsNamespace`)防回归。

## 历史教训
- Phase3.1：`MetadataCsvFixtureService.ImportAsync` 全局 RemoveRange 清空全部租户→18 case BLOCK；限自身租户子树。
- Golden 实时调 Qwen 有抖动(GQ-008/403)，非环境问题；判定 `expectedOutcomeSatisfied`。
- 后端：`ASPNETCORE_URLS="http://localhost:5032" dotnet bin/Debug/net10.0/SuperBuilder_AI.dll`；Qdrant 从 /c/tmp/qdrant_run 启动。
- **Git 历史重写复盘**：① filter-repo 务必后台(`run_in_background`)——前台 120s SIGTERM 损坏 HEAD(`bad object`)；② 中断用 `git bundle create --all` 备份克隆恢复；③ 替换规则须覆盖所有真实值(WMS/元库/LLM Key + 旧 `appsettings.json` 的 `Auth:SigningKey` 64 字符 HMAC 密钥)；④ 顶层 `mv` 报 busy→改 `mv <repo>/.git <repo>/.git-corrupt` 腾空再移回；⑤ `rm -rf` >50 文件触发 `SAFE_DELETE_BULK_CONFIRM_REQUIRED` 需用户确认；⑥ **文档/记忆提交切勿写明文凭据**——曾误写致 `-S` 重现命中，须脱敏后 `commit --amend`+`reflog expire`+`gc --prune=now` 清悬空含密 blob。
