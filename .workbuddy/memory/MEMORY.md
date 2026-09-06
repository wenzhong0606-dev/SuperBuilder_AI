# SuperBuilder AI Native BI - 项目长期记忆（精简版）
> 唯一事实来源：`docs/Master_Development_Plan.md`(v2.0)。本文件仅留跨会话有用结论与约束。

## 技术栈
.NET 10 + EF Core 10 + Dapper；Qdrant(1024维)；Qwen LLM；多库(SQL Server/MySQL/PostgreSQL)；业务库 WMS MySQL `192.168.16.120:3306`(已可达)。

## 阶段状态(2026-09-04)
- 测试基线 504/504 全绿、build 0 error；Golden 18/18 未改。
- M0：M0-01~M0-09 **全部 ✅（2026-09-04 收尾）**。M0-01 = Git 历史重写(629 提交, `git log --all -S` 5 类明文 0 命中) + 外部凭据轮换(WMS/LLM/Auth:SigningKey/元库) + 强制推送至 `wenzhong0606-dev/SuperBuilder_AI`。M0-02~M0-08 经本回合代码核查确认已在 P10/P11 落地：`AuthMiddleware`+`TokenService`+`TenantDataPlanePolicy`+`RowLevelSecurityService`(Program.cs 已接线, deny-by-default)、`SchemaProbe`+固定迁移/种子启动序列(受控 Migration)、`RateLimitMiddleware`+`ForwardedHeaders` 仅信任配置代理 + `/metrics` 受 `platform:diagnostics:view` 守卫(限流与匿名端点治理)、TenantId 取自 JWT 而非请求体(字段越权/跨租户隔离)、四路由契约统一为 `api/agent|apps|ask|data-sources` 等。测试基线按既有记忆：476/476 全绿、build 0 error、Golden 18/18。
- Stage 0 ✅；Stage 1 🟡(A3/A5 暂缓)；Stage 2 P3~P10 ✅；P11 前端 P11.0~P11.5 ✅（UI 现代化、P11.5 鉴权收尾、P11.6 结构等部分改动仍待提交，三端 build 0 error）。
- 里程碑：M0 ✅；M1-01~M1-06 ✅；M2-01~M2-07 ✅（含 M2-06 默认关闭自注册骨架、M2-07 独立 Demo 安装器）；**M3-G0 多语言核心子集 ✅（2026-09-05，661/661 测试、四端 0 error）**；**M3-01 语言关系模型 ✅（2026-09-05，674/674 测试、四端 0 error）**；M3-02~06 仍为发布门禁；**M4 数据源与元数据闭环 ✅（2026-09-06：M4-01 页面增强 + M4-05 扫描与同步（队列+后台处理器+迁移+POST202触发/GET轮询+前端异步轮询面板+6例测试）完成；M4-02/03/04 经 M4-01 审计声明已就绪）**。下一步 M5 语义模型与 QueryPlan 企业化。

## 零回归手法
门控隔离(多语言/AI 路径「非默认才启用」短路，默认路径逐字节不变) + 双路径 Agent/AppBuilder(默认确定性不调 LLM)。Golden 契约 `Evaluation/Golden/query-plan-golden-v1.json` 不可删改。

## P11 前端关键陷阱
- 三项目：`SuperBuilder_AI.Components`(RCL)+`.Web`(Blazor Server)+`.Maui`(Blazor Hybrid,Win 0 error)，纳入 `SuperBulider_AI.slnx`。
- ⚠️ 预渲染：`ServerPrerendered` 阶段无 JS，`MainLayout.OnInitializedAsync` 内 `IJSRuntime.InvokeAsync` 抛 500；主题须移到 `OnAfterRenderAsync(firstRender)`（首屏 data-theme 由内联脚本设，no-FOUC）。
- ⚠️ RCL 不可引 `Microsoft.AspNetCore.Components.WebView.Maui`（与 MAUI 头冲突→build error）；RCL 只引 `Microsoft.AspNetCore.Components.Web`+`Microsoft.Extensions.Http`(全 TFM)。
- ⚠️ 签名密钥坑(已修)：`TokenService` 缺 `Auth:SigningKey` 回退硬编码 `dev-insecure-signing-key-P11-change-in-prod`→可伪造 token；生产须密钥管理覆盖。
- token 链路：`Login`→`AuthStore.SetFromLoginAsync`→`AppState`(localStorage)；`MainLayout` 自举 `RestoreAsync`+`ValidateAsync(/api/auth/me)`；`ApiClient` 401→`AppState.NotifySessionExpired`→跳 `/login`。

## ⚠️ Blazor/Razor 踩坑
1. 事件处理器含 C# 字符串→属性单引号：`@onclick='() => Toast("文本")'`；双引号提前闭合→CS1056/CS1026；`@onclick="() => Toast('文本')"` 单引号变 char→CS1012。
2. 渲染 `code` 变量写 `@(code)`，否则 `@code</span>` 被当指令→RZ2005/RZ1017。
3. void 方法不能直接绑 `@onclick='Toast("x")'`(CS1503)→包 lambda。
4. DI 页面只能注入 `IApiClient`(`AddScoped<IApiClient,ApiClient>()`)；注入具体类 `Services.ApiClient` 会 500；新增方法须同步加接口。
5. `<details>` 不支持 `@bind-open`(RZ9991)；HTML 属性绑定只支持 `bind`/`bind-value`。
6. 登录判定须等 `AppState.SessionRestored`；子组件 `OnAfterRender` 早于 `MainLayout` 自举。
7. 并行 `dotnet build` 同方案会因 bin 被重建删报 MSB3030；新增目录须同步两 Head 的 `_Imports.razor` 且至少一个组件。

## 架构治理
234 .cs 迁至 `src/` 四层(Domain 79/App 109/Infra 29/Api 17)；待合并：GoldenBaseline↔GoldenBaselinePersistenceRecord；QueryPlanSemanticResolution↔SemanticApplicabilityResult/*Resolution。

## 历史教训
- Phase3.1：`MetadataCsvFixtureService.ImportAsync` 全局 RemoveRange 清空全部租户→18 case BLOCK；限自身租户子树。
- Golden 实时调 Qwen 有抖动(GQ-008/403)，非环境问题；判定 `expectedOutcomeSatisfied`。
- 后端：`ASPNETCORE_URLS="http://localhost:5032" dotnet bin/Debug/net10.0/SuperBuilder_AI.dll`；Qdrant 从 /c/tmp/qdrant_run 启动。
- **Git 历史重写复盘**：① filter-repo 务必后台(`run_in_background`)——前台 120s SIGTERM 损坏 HEAD(`bad object`)；② 中断用 `git bundle create --all` 备份克隆恢复；③ 替换规则须覆盖所有真实值(WMS/元库/LLM Key + 旧 `appsettings.json` 的 `Auth:SigningKey` 64 字符 HMAC 密钥)；④ 顶层 `mv` 报 busy→改 `mv <repo>/.git <repo>/.git-corrupt` 腾空再移回；⑤ `rm -rf` >50 文件触发 `SAFE_DELETE_BULK_CONFIRM_REQUIRED` 需用户确认；⑥ **文档/记忆提交切勿写明文凭据**——曾误写致 `-S` 重现命中，须脱敏后 `commit --amend`+`reflog expire`+`gc --prune=now` 清悬空含密 blob。
