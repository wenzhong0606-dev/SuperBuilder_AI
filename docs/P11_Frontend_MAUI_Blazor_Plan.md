# SuperBuilder AI — 前端与优化方向方案（MAUI Blazor + Web）

> 状态：规划文档（仅设计，未落代码）
> 依据：P3–P10 后端全绿、Golden 18/18；用户决策「MAUI Blazor + Web 一套 UI 双端」；本轮先出方案文档再实现。

---

## 1. 背景与现状

| 维度 | 现状 |
|------|------|
| 后端 | P3–P10 全部完成，11 个控制器覆盖完整产品面：`agent` / `apps` / `audit` / `business-model` / `dashboards` / `identity` / `localization` / `quota` / `semantic-labels` / `tenant-management` / `themes`；单测 294/294；Golden 18/18 PASS |
| 前端 | **几乎为零**：仅 `wwwroot/js/site.js`(231B) / `wwwroot/css/site.css`(667B)；默认路由 `Home/Index` 404；无 Swagger/OpenAPI |
| 鉴权 | Identity/RBAC(P10.2) 已有「全局守卫」，但前端未接线（无 Token 流、无 CORS 策略） |
| 性能 | Golden runtime 每次实时调 Qwen LLM（非确定、偶发 403 限流）；无缓存层 |
| 契约 | 11 个控制器响应形状不统一，无统一信封/分页 |

**结论**：最大优化方向就是「把已就绪的后端能力用前端呈现出来」，其次是后端成熟度（缓存 / 契约 / 安全 / 运维）。

---

## 2. 目标

1. 交付 **多端 BI 控制台**：浏览器（Web）+ 原生（Windows / Android / iOS）共用一套 UI 代码。
2. 把现有 `api/*` 能力可视化、可操作化（尤其旗舰 **Ask BI 智能问数**）。
3. 同步推进后端成熟度（性能 / 安全 / 运维三轨）。
4. **硬约束**：现有 API 项目的 `src/` 与 Golden 依赖文件零改动，Golden 18/18 全程保持。

---

## 3. 技术决策

**采用 .NET MAUI Blazor Hybrid + Blazor Web，共享 Razor Class Library（RCL）。**

理由：
- **单 UI 代码库双端**：RCL 里的 `.razor` 同时被 Blazor Web（浏览器）和 MAUI `BlazorWebView`（原生）复用，开发一次、处处运行。
- **工作负载已具备**：本机已安装 `maui-windows` 10.0.20 + `android` / `ios` / `maccatalyst` / `macos`，SDK 10.0.301 → 双端可落地，非画饼。
- **契合「暂不拆 API（A3）」取向**：仅在 UI 层新增 RCL + 两个 Head，不动后端单项目结构。

方案对比：

| 方案 | 契合度 | 双端 | 工具链 | 取舍 |
|------|--------|------|--------|------|
| **MAUI Blazor + Web（选）** | 高 | 浏览器 + 原生 | 仅 .NET | 单 UI 代码库；MAUI 原生运行需设备/模拟器 |
| React + Vite SPA | 中 | 仅 Web | 需 Node | 生态最强，但双产物部署，与 A3 暂缓取向略冲突 |
| Blazor Server + 组件库 | 中 | 仅 Web | 仅 .NET | 纯 C# 交互，但无原生端 |
| Razor Pages + 轻量 JS | 中 | 仅 Web | 仅 .NET | 最轻量，但非双端 |

---

## 4. 总体架构

```
        ┌─────────────────────┐         ┌──────────────────────────┐
        │  Blazor Web App     │         │  .NET MAUI Blazor Hybrid │
        │  （浏览器 Head）     │         │  （原生 Head）            │
        └─────────┬───────────┘         └─────────────┬────────────┘
                  │ 引用                               │ 引用 (BlazorWebView)
                  └───────────────┬───────────────────┘
                                  ▼
                     ┌────────────────────────────┐
                     │  共享 Razor 组件库 (RCL)     │
                     │  全部 .razor 页面/组件       │
                     │  Login/Tenant · Ask BI ·    │
                     │  Dashboards · Apps · Agent ·│
                     │  Admin/*                    │
                     └──────────────┬─────────────┘
                                    │ 运行时 HttpClient 调 api/*
                                    ▼
                     ┌────────────────────────────┐
                     │  SuperBuilder_AI API（现有）│
                     │  api/* 11 控制器 · 不动     │
                     │  Golden 18/18 不变          │
                     └────────────────────────────┘
```

- **RCL**：唯一写 UI 的地方，两个 Head 都引用它。
- **Web Head**：Blazor Web App，交互式 Server/Auto 渲染，现在即可在浏览器验证。
- **MAUI Head**：`BlazorWebView` 承载同一个 RCL → 编译后即为原生应用。
- **API**：完全不动；两个 Head 通过 `HttpClient` 调用。

---

## 5. 目录结构草案

```
SuperBuilder_AI/                    (现有 API 项目，零改动)
  src/...                           (controllers / services / models)
  wwwroot/                         (可放 Chart.js 等前端静态资源)

SuperBuilder_AI.Components/        (新增 · Razor Class Library, net10.0)
  Components/
    Layout/AppLayout.razor         (顶栏 + 侧边导航 + 主题/语言切换)
    Shared/ChartView.razor         (Ask BI 结果图表，封装 Chart.js)
    NavMenu.razor
  Pages/
    Login.razor                    (租户选择 + 用户登录)
    Ask.razor                      (Ask BI 智能问数 · 旗舰)
    Dashboards.razor
    Apps.razor
    Agent.razor
    SemanticLabels.razor
    BusinessModel.razor
    Components.razor               (§12.1 用户自定义组件库)
    Themes.razor                  (§12.2 用户自定义风格编辑器，用户态)
    Admin/
      Tenants.razor  Identity.razor  Audit.razor
      Quota.razor  Localization.razor  Themes.razor
  wwwroot/js/chart.razor.js
  _Imports.razor
  SuperBuilder_AI.Components.csproj

SuperBuilder_AI.Web/               (新增 · Blazor Web App)
  Program.cs                       (AddRazorComponents<App>; MapRazorComponents;
                                   配置 HttpClient base=相对路径; 引用 RCL)
  Components/App.razor  Routes.razor  _Imports.razor
  SuperBuilder_AI.Web.csproj

SuperBuilder_AI.Maui/              (新增 · MAUI Blazor Hybrid)
  Platforms/Windows|Android|iOS|MacCatalyst/
  MainPage.xaml                    (BlazorWebView RootComponent=App)
  MauiProgram.cs                   (AddMauiBlazorWebView; 引用 RCL)
  SuperBuilder_AI.Maui.csproj
```

> API base 地址：Web Head 同进程用相对路径；MAUI Head 用配置（如 `https://localhost:5032` 或打包时注入）。

---

## 6. 与现有 API 的对接约定

| 关注点 | 方案 |
|--------|------|
| 调用方式 | RCL 内注入 `HttpClient`（单例），base = API 地址；页面 `@inject` 后直接 fetch `api/*` |
| 租户上下文 | 登录后保存 `tenantId`，统一在 `HttpClient` 默认请求中带 `?tenantId=`（与现有控制器 `[FromQuery] long tenantId` 约定一致） |
| 鉴权 | Identity 登录返回 Token → 存入 Head 的 auth state → 注入 `Authorization` 头；API 侧全局守卫校验（**需核实 P10.2 守卫是否已对每个请求生效**，见 §11） |
| 主题 | 启动时 `GET api/themes/{key}` → 在 RCL `App.razor` 写入 CSS 变量，运行时切换 |
| 多语言 | `GET api/localization/resolve` / `fallback-chain` → RCL 内运行时切换 |
| 图表 | Ask BI 结果用 Chart.js（RCL `wwwroot/js` 封装）或更重的组件库（MudBlazor / AntDesign.Blazor），**选型在 P11.1 定** |

---

## 7. 页面地图（→ 现有端点映射）

| 页面（RCL） | 后端端点 | 说明 |
|------------|----------|------|
| 登录 / 租户选择 | `tenant-management` + `identity` | 选租户 → 登录拿 Token |
| **Ask BI 智能问数（旗舰）** | 核心问数端点（见 §11 待确认） | 自然语言 → 图表/表格 |
| 语义模型 | `business-model` (entities/domains/resolve) | |
| 仪表盘 | `dashboards` (render/editor/blueprint) | |
| 应用工厂 | `apps` (generate/editor/blueprint) | |
| 智能体 / Copilot | `agent` (plan/tools/anomaly-chain) | |
| 语义标签 | `semantic-labels` (resolve/synonyms/recall) | |
| 管理后台-租户 | `tenant-management` (settings) | |
| 管理后台-身份权限 | `identity` (users/roles/permissions) | |
| 管理后台-审计 | `audit` (logs) | |
| 管理后台-配额 | `quota` (check/consume) | |
| 管理后台-多语言 | `localization` | |
| 管理后台-主题 | `themes` | |
| **组件库（用户自定义）** | 复用 P6 组件模型 / 评估 `components`(设计态) | 用户维护可复用可视化组件 DSL |
| **主题编辑器（用户自定义风格）** | `themes`(P7.4 CRUD+指派+复制+蓝图) | 令牌可视化配置 + 实时预览 + 按租户/应用指派 |
| **Ask 会话面板（多轮调整 + 发布）** | Ask 会话态 + `ask/refine`(新增) + `apps`(P8 发布) | 结果不对时多轮对话修正 + 确认后发布为应用页 |
| **数据源管理** | 复用 `IDataSourceConnectionFactory` + `IDataSourceMetadataReader`；评估 `datasources`(连接器注册) | 选连接器类型 + 连接串/凭据 + 测试连接 + 触发扫描 |
| **模型与账号（BYO）** | 评估 `models`(目录) + `user-model-bindings`(BYO key) | 选模型 + 绑定自有 API Key + 设为默认 |

---

## 8. 分阶段交付（建议命名 P11）

- **P11.0 后端前置（因 §11 核实新增）**：
  1. 新增 `api/ask` 公开问数端点（包装 `IBIConversationService.AskAsync`）——**旗舰页硬前置依赖**；
  2. 新建**鉴权中间件**（Token + 租户上下文 + 权限校验，复用 P10.2 `HasPermissionAsync`）+ CORS + 速率限制——当前 `api/*` **零鉴权保护**，最高优先级安全项。
  - 闸门：build 0 error；单测绿；**Golden 18/18 不变**（均为新增文件、不碰 Golden 依赖）。
- **P11.1 脚手架**：建 RCL + Web Head + MAUI Head 空壳，可编译/可运行；接入 Theme/Localization 运行时骨架；定图表库；预留「数据源管理」「模型与账号」空页占位（§13）。
  - 闸门：三个新项目 `build 0 error`；现有 API 项目 Golden 依赖文件零改动；Golden 18/18 不变。
- **P11.2 旗舰页（含多轮对话雏形）**：登录/租户选择 + **Ask BI 智能问数**（打通端到端）；实现「视图层多轮调整」（改成柱状图/加图例等纯前端 DSL 变更，零后端调用、即时重渲染）与「发布为应用」的基础骨架（确认结果 → 序列化为 App DSL → P8 `BuildFromDslAsync`）。
  - 闸门：Web Head 本地跑通；Golden 18/18 不变；若需新增 `ask/refine` 只读端点，须不破坏 Golden。
- **P11.3 其余页面 + 用户自定义能力**：语义模型 / 仪表盘 / 应用工厂 / 智能体 / 语义标签 / 管理后台(租户·身份·审计·配额·多语言·主题)；并落地 §12 三项用户自定义能力——**组件库**、**主题编辑器**、**Ask 语义层多轮调整 + 完整发布为应用流程**。
- **P11.4 MAUI 双端验证**：Windows 原生编译运行；Android/iOS 编译校验（设备/模拟器受限时以「可编译」为闸门）。
- **P11.5 优化轨道落地**（见 §9）：性能缓存、安全鉴权接线、运维可观测。

---

## 9. 优化方向（四轨细化与排序）

用户本轮选定推进：**体验/前端、性能/成本、安全/运维**；**架构/测试（A3/A5/集成测试）暂缓**。

### 9.1 体验 / 前端（最大缺口）
见 §3–§8。本质是「把后端能力用 UI 呈现」。

### 9.2 性能 / 成本
- **LLM 结果缓存**：语义相似问句命中（embedding 近似 + TTL/失效策略），显著降低 403 抖动与延迟、省成本。
- **向量检索 / QueryPlan 缓存**：按 `(tenant, ds, normalizedQuestion)` 缓存 Qdrant recall 与查询计划。
- **BI 查询执行优化**：大数据量分页/流式、连接池、索引（GoldenBaseline 等表）。
- 预期：Golden runtime 实时调 Qwen 的非确定抖动明显下降。

### 9.3 安全 / 运维
- **鉴权（已核实 → 需**新建**，非"补接线"）**：**核实结论：全局守卫不存在**（全库 `grep "Guard"` 零命中；`Program.cs` 无鉴权中间件）→ 当前所有 `api/*` 端点**无任何鉴权保护**。须新建：
  - **鉴权中间件**：解析并校验 Token / 租户上下文；
  - **权限校验**：复用 P10.2 `IIdentityService.HasPermissionAsync`（RBAC deny-by-default + 租户隔离）；
  - **CORS 策略** + **速率限制**。
  - 定位：P11 的**最高优先级安全项**，已作为 P11.0 前置（见 §8）。
- **可观测成熟化**：`/health`、metrics（P95 延迟 / 请求计数 / 错误率）、结构化日志 + 关联 ID 贯穿（P10.5 中间件已打底，扩展）。
- **配置与多环境**：`appsettings` 分环境、密钥管理。

### 9.4 架构 / 测试（暂缓，列入后续）
- A3 拆独立项目 / A5 限界上下文解耦（用户暂缓）。
- 统一 `ApiResult` 信封 + ProblemDetails + 分页。
- OpenAPI / Swagger（前端联调契约前提；Web Head 可暴露 Swagger 端点）。
- 集成测试（WebApplicationFactory 端到端 + 租户隔离）。

---

## 10. 退出闸门（每子阶段通用）

- 新项目 `build 0 error`；现有 API 项目 `src/` 零改动。
- **Golden 18/18 不受影响**（前端不触碰后端 `src/`）。
- Web Head 可本地运行；MAUI Head 可编译（原生运行受设备限制时以编译为闸门）。
- 文档 / 记忆同步；git 提交。

---

## 11. 风险与待确认项

1. **核心问数端点缺失（已核实 ✅ 确认缺失）**：`BIConversationService.AskAsync` 存在且已 DI 注册（`Program.cs` 162–163），但**仅被 `QueryPlanWidgetDataResolver` 消费**（仪表盘取数）；11 个生产控制器 + 15 个诊断控制器中**无任何 NL 问数端点**。→ **P11.2 前必须新增 `api/ask`**（包装 `IBIConversationService.AskAsync`，新文件、不改动 Golden 依赖、不影响 18/18）。这是旗舰页的**硬前置依赖**。
   - 附带：`Api/Diagnostics/HomeController.cs` 存在且 `Index()` 返回 `View()`，但项目**无 `Views/` 视图文件** → 默认路由实际无法渲染；P11 前端接入后应移除或改指向前端入口。
2. **P10.2 全局守卫不存在（已核实 ✅ 确认缺失 · 安全硬缺口）**：全库 `grep "Guard"` **零命中**；`Program.cs` 仅注册 `ObservabilityMiddleware` + `AuditMiddleware`，**无鉴权中间件**。→ 当前所有 `api/*` 端点**无任何鉴权保护**。P11 安全轨道须从「补接线」升级为「**新建鉴权中间件 + Token 校验**」（见 §9.3）。
3. **MAUI 原生运行受限**：本沙箱可编译 Windows/Android，但启动原生窗口/模拟器不一定可行 → MAUI Head 以「可编译」为闸门，真机/模拟器运行单列验证。
4. **严守不碰 A3**：仅在 UI 层新增，不重构后端单项目。
5. **图表库选型**（Chart.js 轻量 vs 组件库丰富）在 P11.1 确定。
6. **用户自定义组件库后端面缺失（已核实 ✅ 确认需新建）**：`ComponentType` 仅存在于 `Domain/AppBuilder/AppDsl.cs` 的 `AppComponentTypes`（P8 应用组件白名单 `chart/table/kpi/text/filter/form`），**无 `Component` 实体、无自定义组件持久化**。→ P11.3 须新增组件库领域模型 + 持久化 + `api/components` 设计态 CRUD（新文件、不碰 Golden），并走 P 级 Golden 回归纪律。
7. **Ask 语义层多轮调整需新增后端端点**：`api/ask/refine`（上一轮 plan + 自然语言指令 → 重算）为新增文件；须沿用「默认路径确定性回退、非默认路径才调 LLM」门控，确保 Golden 18/18 不变。`ask/refine` 属只读问数，不进入 Golden 契约验证集。
8. **发布为应用复用 P8 契约（已核实 ✅ 部分支持）**：
   - **主题引用已支持** ✅：`AppDsl.ThemeKey`（`AppDsl.cs:82`）+ `AppPlan.ThemeKey`（`AppPlan.cs:74`）已存在 → 发布应用套用自定义主题**无需改动 P8**。
   - **自定义组件引用不支持** ⚠️：组件类型受 `AppComponentTypes.Supported` **硬编码白名单**约束（校验点 `AppDslSerializer.cs:160`）。发布含自定义组件的应用，需扩展白名单或引入 `CustomComponentRef` 机制（新增、不破既有校验）。

---

## 12. 用户自定义能力（组件库 / 风格 / Ask 多轮对话与发布）

用户本轮补充的三项能力，均建立在已完成的后端能力之上，定位为「设计态 authoring + 运行时渲染」：

| 能力 | 后端依托 | 前端职责 | 是否需新增后端 |
|------|----------|----------|----------------|
| 用户自定义组件库 | P6 低代码 BI 引擎（ComponentType / 渲染映射） | 组件 DSL 编辑 + 可视化预览 + 复用 | 评估 `components` 设计态 CRUD（新文件，不碰 Golden） |
| 用户自定义风格/样式 | P7 主题引擎（P7.4 `themes` CRUD+指派+复制+蓝图） | 令牌编辑器 + 实时预览 + 指派 | **否**，直接复用 P7.4 端点 |
| Ask 多轮对话调整 | P5 多语言门控 / P8 DSL 校验 | 会话态 + 气泡流 + 撤销快照 | 语义层调整需 `ask/refine`（新文件，不碰 Golden） |
| 发布为应用页面 | P8 应用工厂（`IAppBuilderAgent.BuildFromDslAsync`） | 序列化会话 → App DSL → 保存 `apps` | **否**，复用 P8 |

### 12.1 用户自定义组件库（Custom Component Library）

- **概念**：用户在「组件库」页维护一套**可复用可视化组件定义**——图表 / 表格 / 指标卡 / 筛选器 / 容器，每个组件 = 组件 DSL（类型 + 数据绑定 + 样式 + 默认配置）。可把若干基础组件拖拽组合成「模板组件」，沉淀为团队资产。
- **与 P6 衔接**：P6 低代码 BI 引擎已定义 `ComponentType` 与渲染映射；前端组件库是 P6 在**设计态的用户可编辑扩展面**。仪表盘 / Ask 结果 / 发布应用均可引用库里的自定义组件。
- **后端策略（零回归）**：
  - 优先**复用 P6 已有组件持久化**；若 P6 未暴露组件定义写接口，P11.3 评估新增 `api/components`（设计态 CRUD，仅 authoring，不进入 Golden 查询链路）。
  - 任何新增端点均为**新文件**，不改动 Golden 依赖文件，遵循「非默认路径才调 LLM」门控。
- **页面**：`Components.razor`（组件库管理：新建/复制/编辑/删除 + 组合模板；编辑器内即时预览，复用 Ask 的 `ChartView` 渲染）。
- **运行时**：组件定义经现有 Low-code 渲染引擎出图，用户最终选定后落入仪表盘 / 应用。

### 12.2 用户自定义风格 / 样式（Custom Theme / Style）

- **概念**：用户在「主题编辑器」**可视化配置设计令牌**——主色 / 辅助色 / 中性色 / 字体 / 圆角 / 间距 / 阴影，实时预览，保存为自定义主题；支持按**租户或按应用**指派（P7 已有 assign）。
- **与 P7 衔接（直接复用，零后端新增）**：P7.4 已交付 `api/themes` 的 CRUD + 指派 + 复制 + 蓝图；前端只做**编辑器 UI**，不新增端点。
- **页面**：`Themes.razor`（主题编辑器：令牌面板 + 实时预览 + 保存 / 指派 / 复制为蓝图）。
- **运行时**：登录后按用户/租户 `GET api/themes/{key}` → 在 RCL `App.razor` 注入 CSS 变量（已在 §6 约定）。发布应用时可指定套用当前自定义主题（见 12.4）。

### 12.3 Ask 多轮对话调整（Conversational Refinement）

- **会话模型**：Ask BI 进入「会话态」，保留上一轮结果（query plan + 数据 + 可视化配置）。对话以**消息气泡流**呈现，每轮保留可撤销快照；结果不对时可逐轮回退。
- **两类调整**：
  1. **视图层调整**（"改成柱状图" / "加图例" / "配色换一下"）：**纯前端 DSL 变更，零后端调用**，即时重渲染。在 P11.2 即落地。
  2. **语义 / 数据层调整**（"只看华东区" / "加上同比" / "换成交指标"）：需后端以「上一轮 plan + 自然语言指令」为上下文**重算** → **新增 `api/ask/refine`**（只读问数，不触碰 Golden 依赖；沿用 P5/P8/P9 的「非默认路径才调 LLM」门控与确定性回退）。
- **零回归纪律**：`ask/refine` 为新增文件；语义层调整在默认路径下走确定性回退（不调 LLM），仅在显式带自然语言指令时启用 LLM——与 P5/P8/P9 既有的零回归手法一致，Golden 18/18 不受影响。
- **会话态存储**：Web 存内存 / IndexedDB；MAUI 存本地存储；不强制后端持久化（跨端续聊可后续增强）。

### 12.4 确认结果 → 发布为应用页面（Publish as App）

- **流程**：用户在多轮对话中确认最终结果 → 点「发布」→ 将当前会话的**最终查询 DSL + 可视化配置 + 选用的自定义组件(12.1) + 自定义主题(12.2)** 序列化为 **App DSL** → 经 P8 `IAppBuilderAgent.BuildFromDslAsync`（确定性、不调 LLM、先校验后信任）生成 `AppPlan` 并保存至 `api/apps`。
- **产出**：「应用」页即时生成可读 / 可分享的**应用页面**（含该查询 + 图表 + 必要交互筛选器），后续可在应用工厂中再次编辑。
- **与 P8 衔接**：完全复用 P8 的 DSL 校验（`IAppDslSerializer` 先校验后信任）与持久化；不触碰 Golden。
- **价值**：把一次探索式问数沉淀为可复用、可分享的标准 BI 应用——这是「AI Native BI」从「问完即走」到「问完即用」的闭环。

### 12.5 三项能力在阶段中的落点

- **P11.2**：视图层多轮调整（纯前端）+ 发布基础骨架（序列化 → P8 保存）。
- **P11.3**：组件库页 + 主题编辑器（复用 P7.4）+ 语义层 `ask/refine` + 完整发布流程（含自定义组件/主题绑定）。

---

## 13. 平台扩展能力（多数据库 / 多 AI 模型 BYO）

用户后续将扩展两类可插拔能力。二者均属「平台级横向扩展」，建议放在 P11 前端与 §9 优化轨道**之后**作为独立阶段（暂记 P12/P13），因为它们涉及后端抽象层增强；但**前端需提前预留入口**（数据源管理页、模型与账号绑定页），避免后期返工。

### 13.1 多类型数据库接入（Multi-DB Connector）

- **现状**：已具备关系型方言抽象 `ISqlDialectResolver` + `IDataSourceConnectionFactory` + `IDataSourceMetadataReader`，内置 `SqlServer` / `MySql` / `PostgreSql` 三种方言；BI 查询链路通过动态 Resolution（MasterJoin / DirectKey / Ambiguous / NotResolved）解耦具体库，**未硬编码业务表字段**（D14 约束）。
- **扩展方式（插件式，不破 Golden）**：
  - 引入统一契约 `IDataSourceConnector`：连接测试 / 列举表与字段 / 执行查询并归一化为 `QueryResult`（schema + rows）；关系型方言作为该契约的子集实现。
  - 每类新库 = 一个新连接器实现（`ClickHouse` / `MongoDB` / `Snowflake` / `Excel-CSV` 等）+ 在 `DataSourceConnectionFactory` 注册分支；遵循 D14「禁止硬编码业务表/字段」「逐行全量源码审计后最小修改」纪律。
  - 元数据扫描 `MetadataScannerService` 已依赖 `IDataSourceMetadataReader`，新增连接器同步实现对应 reader 即可进入语义层。
- **前端入口**：「数据源管理」页支持选择连接器类型、填写连接串/凭据、测试连接、触发扫描；租户作用域沿用 P4.3 显式开启策略。
- **零回归**：新增连接器为新增文件/分支，不改动既有 `ISqlDialect` 实现与 Golden 依赖文件；Golden 仅在关系型路径验证，新连接器以独立确定性离线测试覆盖。

### 13.2 多 AI 模型 + 用户自绑定账号（BYO Model / Account）

- **现状**：AI 能力经单一端口 `IQwenService`（Qwen qwen3.7-plus），被 Agent / AppBuilder / QueryUnderstanding / ResultUnderstanding / MetadataSemantic 等多个服务依赖；模型与密钥在配置中固定，**无「按用户/租户选择模型 + 绑定自有账号」机制**。
- **扩展方式**：
  - **模型供应商抽象**：引入 `ILLMProvider` 端口，现有 `QwenService` 作为其一实现；逐步接入 `OpenAI` / `Claude` / `Gemini` / `Ollama(本地)` 等。调用方改为依赖 `ILLMProvider` 而非具体的 `IQwenService`，**默认供应商仍为 Qwen、默认路径行为不变**，确定性回退逻辑沿用 P5/P8/P9 门控。
  - **模型目录**：`ModelCatalog`（平台定义可用模型清单：id / 供应商 / 默认参数 / 上下文窗口 / 是否支持 BYO key）。
  - **用户/租户账号绑定**：新增 `UserModelBinding`（或 `TenantModelBinding`）实体，存储**加密后的**第三方 API Key / OAuth 令牌，作用域绑定到用户或租户；调用时按（租户/用户 → 所选模型 → 绑定凭据）解析，缺省回落平台默认凭据。
  - **安全红线**：密钥 **加密存储**（AEAD / 平台密钥库）、**绝不写入日志/审计明文**（P10.3 Audit + P10.5 Observability 须对凭据字段脱敏）、仅经 TLS 传输；遵循 P10.2 RBAC 的 deny-by-default 与租户隔离。
- **前端入口**：`设置 / 模型与账号` 页——列出可用模型、选择当前模型、绑定自有 API Key（掩码展示）、设为默认；Ask BI / 应用工厂 / 智能体在运行时按绑定选择供应商。
- **零回归**：默认路径仍是 Qwen + 平台凭据，Golden 验证集走默认路径，故 18/18 不受影响；新供应商为新增实现，不触碰 Golden 依赖文件。

### 13.3 落点建议

- 这两项建议作为独立阶段推进：**P12 多数据库连接器**、**P13 多 AI 模型 BYO**（均在 P11 与 §9 优化轨道之后）。
- **P11.1 脚手架即预留「数据源管理」「模型与账号」两个空页面占位**，避免后期返工。
- 风险项（补充至 §11）：① 新连接器/新供应商须各走 P 级 Golden 回归纪律；② `UserModelBinding` 密钥加密与脱敏是合规硬性要求，须与 P10 安全基线联动审查。

---

## 14. 下一步

本方案文档确认后，从 **P11.1 脚手架** 开始落地（RCL + Web Head + MAUI Head 空壳 + Theme/Localization 骨架）。P11.2 起即纳入 §12 的用户自定义能力；§13 的平台扩展能力在 P11 与优化轨道之后按 P12/P13 推进。
