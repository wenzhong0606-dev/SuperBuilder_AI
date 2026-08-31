# SuperBuilder AI Native BI - 项目长期记忆（精简版）

> 详细阶段进度以 `docs/DevelopmentPlan.md`（主阶段脊柱，唯一事实来源）+ `docs/PhaseChecklist.md` 为准；本文件仅留跨会话有用的结论与约束。

## 项目定位与技术栈
- AI Native BI：自然语言 → AI理解 → 语义分析 → 查询计划 → SQL → 数据分析 → 业务答案
- .NET 10 (net10.0) + EF Core 10 + Dapper；Qdrant 向量库(1024维)；Qwen LLM；多数据库(SQL Server/MySQL/PostgreSQL)
- 业务库 WMS MySQL `192.168.16.120:3306`（用户确认现已可达）
- 主交付文档：`docs/DevelopmentPlan.md` + `docs/PhaseChecklist.md` + `docs/ARCHITECTURE.md`

## 阶段状态（截至 2026-08-31）
- **Stage 0 基础/运行时**：✅ 全完成；Golden 18/18 PASS
- **Stage 1 架构治理**：🟡 A1/A2/A4 ✅；A3/A5 ⬜（用户指令暂缓「先不做」）
- **Stage 2 产品演进 P3~P10**：✅ 全绿（每阶段退出门槛 = Golden 18/18，硬约束）
- **P11 前端（MAUI Blazor Hybrid + Web 共享 RCL）**：✅ P11.0/P11.1/P11.2 完成；本轮新增「UI 现代化重构」(见下)；P11.3~P11.5 待启动

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

## 架构治理 Phase 4
- 234 .cs 已迁至 `src/` 四层(Domain 79/Application 109/Infrastructure 29/Api 17)；旧顶层目录清空
- 待合并重复：GoldenBaseline↔GoldenBaselinePersistenceRecord；QueryPlanSemanticResolution↔SemanticApplicabilityResult/*Resolution

## 历史教训（回归防护）
- Phase3.1 事故：`MetadataCsvFixtureService.ImportAsync` 全局 RemoveRange 清空全部租户 → 18 case 全 BLOCK；根治为仅限自身租户子树
- Golden 运行时实时调 Qwen 有非确定性抖动（GQ-008 幽灵维度/403 限流），非环境问题；判定字段 `expectedOutcomeSatisfied`
- 后端启动：`ASPNETCORE_URLS="http://localhost:5032" dotnet bin/Debug/net10.0/SuperBuilder_AI.dll`（默认 5000，必须显式设）；Qdrant 从中性可写 CWD 启动(/c/tmp/qdrant_run)
