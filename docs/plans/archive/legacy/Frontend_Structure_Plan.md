> **HISTORICAL SNAPSHOT / 历史快照**：本文是归档资料，只描述记录当时的计划、状态或审计判断。文中的“当前、唯一、已完成、未完成、风险、测试基线、HEAD”等均不得解释为现在的项目状态。当前事实请按 `docs/README.md` 的治理顺序核验。\n\n> **治理声明**：本文档是 `Master_Development_Plan.md` 的**输入 / 审计基线**，**不是**独立执行计划。所有里程碑状态、优先级与验收以 `Master_Development_Plan.md`（唯一事实来源）及其 `milestones/` 拆分文档为准；本文与 MDP 冲突时以 MDP 为准。映射见 MDP §17。
>
> **文档角色**：前端结构计划（映射到 M8）

# SuperBuilder AI · 前端页面结构规范（P11.6）

> 适用范围：`SuperBuilder_AI.Components`（RCL，Web 与 MAUI 双端共享）。
> 本文件是前端结构的**唯一事实来源**：目录分层、路由登记、组件边界、服务职责、新增页面的标准流程。
> 推进计划与验收标准见同目录 `Frontend_DevChecklist.md`。
> 相关背景：`docs/P11_Frontend_MAUI_Blazor_Plan.md`（P11.0~P11.5 历史）。

---

## 0. 摘要（本轮已落地）

| 维度 | 变更 |
| --- | --- |
| 目录 | 页面由扁平 `Pages/` 重组为 `Analysis / Design / Platform / Admin / Account / Errors` 六组；共享组件拆为 `Shared/UI`、`Shared/BI`、`Shared/Feedback`、`Shared/Guard` |
| 布局 | 新增 `BlankLayout`（登录/错误页）、`AppBreadcrumb`（面包屑）、`IconSprite`（图标精灵从 NavMenu 抽出）；`NavMenu` 改为 `NavMenuItems` 元数据驱动 |
| 组件 | 新增 19 个通用组件（面板/表格/标签页/模态/确认/Toast/表单字段/分页/搜索/分段/键值/进度/空态/微标/指标卡/加载/提示/徽章）+ 4 个 BI 组件 + 2 个守卫 |
| 页面 | 新增 9 个：工作台 `/`、仪表盘详情、应用详情、Agent 计划详情、数据源详情（授权+RLS）、个人设置、403/404/500、系统状态 |
| 服务 | 新增 `ToastService`；`AppState` 增加 `SessionRestored` 自举标志；`IApiClient` 补齐通用写操作（POST/PUT/PATCH/DELETE）与纯文本读取 |
| 修正 | 根路由 `/` 原被登录页占用导致「首页」菜单跳登录 → 现由工作台 `Home` 接管，登录页仅保留 `/login` |
| 验证 | RCL / Web / MAUI(Windows) 三端 build 0 error |

---

## 1. 目录结构

```
SuperBuilder_AI.Components/
├── wwwroot/
│   ├── css/app.css                 # 设计令牌 + 工具类（唯一全局样式入口）
│   ├── js/chart.umd.min.js         # Chart.js（RCL 静态资源需显式 <script> 引入）
│   └── lib/bootstrap/              # 本地化 Bootstrap 5.3.3
├── Components/
│   ├── Routes.razor                # 路由表入口（默认布局 MainLayout）
│   ├── _Imports.razor              # 全局 using（新增目录须在此登记）
│   ├── Layout/                     # 布局层
│   │   ├── MainLayout.razor        # 应用壳：顶栏 + 侧栏 + 内容 + Toast 宿主
│   │   ├── BlankLayout.razor       # 无壳布局：登录 / 错误页（同样执行主题与会话自举）
│   │   ├── NavMenu.razor           # 侧栏（由 NavMenuItems 驱动）
│   │   ├── NavMenuItems.cs         # 导航单一事实来源（路由/标题/图标/权限码）
│   │   ├── AppBreadcrumb.razor     # 面包屑（按当前 URL 反查 NavMenuItems）
│   │   └── IconSprite.razor        # 图标 symbol 集合（全站 <use href="#sb-ico-*"> 引用）
│   ├── Shared/
│   │   ├── UI/                     # 通用组件（零业务依赖，可在任意页面复用）
│   │   ├── BI/                     # BI 领域组件（依赖 Models/BIResponse 与图表）
│   │   ├── Feedback/               # 加载 / 错误态
│   │   └── Guard/                  # 登录守卫 / 权限守卫
│   ├── Pages/
│   │   ├── Home.razor              # 工作台（/）
│   │   ├── Analysis/               # 分析域
│   │   ├── Design/                 # 自定义（组件库 / 主题编辑器）
│   │   ├── Platform/               # 平台扩展（数据源 / 模型账号）
│   │   ├── Admin/                  # 治理后台
│   │   ├── Account/                # 账户（登录 / 个人设置）
│   │   └── Errors/                 # 错误页
│   ├── Models/                     # 前端视图模型（BIResponse 等）
│   ├── Services/                   # 状态与服务（IApiClient / AppState / AuthStore / ThemeService / LocalizationService / ToastService）
│   └── Shared/                     # 与组件无关的纯逻辑（TablePresenter / JsonRows）
└── SuperBuilder_AI.Components.csproj
```

**分层规则**

| 层 | 允许依赖 | 禁止 |
| --- | --- | --- |
| `Pages/*` | Shared、Services、Models、Layout | 直接引用其他 Page 的内部类型（跨页复用请先下沉到 Shared） |
| `Shared/UI` | 仅 Razor 基础库 | 注入 `IApiClient`、引用 Models、写业务判定 |
| `Shared/BI` | Models、Shared/UI | 发起网络请求（数据由页面传入） |
| `Shared/Guard` | Services（AppState） | 业务规则硬编码 |
| `Services` | Models | 引用任何组件 |

---

## 2. 路由表

| 路由 | 文件 | 布局 | 说明 |
| --- | --- | --- | --- |
| `/` | `Pages/Home.razor` | Main | 工作台（AuthGuard 保护，未登录跳 `/login`） |
| `/login` | `Pages/Account/Login.razor` | Blank | 登录 / 租户选择 |
| `/ask` | `Pages/Analysis/Ask.razor` | Main | 旗舰：智能问数（提问 / 视图调整 / 语义细化 / 发布应用） |
| `/dashboards` | `Pages/Analysis/Dashboards.razor` | Main | 仪表盘列表 |
| `/dashboards/{Id:long}` | `Pages/Analysis/DashboardDetail.razor` | Main | 详情：概览 / 组件 / 数据与 SQL |
| `/apps` | `Pages/Analysis/Apps.razor` | Main | 应用列表 |
| `/apps/{Code}` | `Pages/Analysis/AppDetail.razor` | Main | 详情：概览 / 页面与组件 / DSL（含删除） |
| `/agent` | `Pages/Analysis/Agent.razor` | Main | 智能体 / Copilot |
| `/agent/plans/{Code}` | `Pages/Analysis/AgentPlanDetail.razor` | Main | 计划详情：概览 / 步骤 / 原始数据（含删除） |
| `/semantic-labels` | `Pages/Analysis/SemanticLabels.razor` | Main | 语义标签 |
| `/business-model` | `Pages/Analysis/BusinessModel.razor` | Main | 语义模型 |
| `/components` | `Pages/Design/ComponentGallery.razor` | Main | 组件库（设计系统展示） |
| `/themes` | `Pages/Design/ThemeEditor.razor` | Main | 主题编辑器 |
| `/data-sources` | `Pages/Platform/DataSources.razor` | Main | 数据源管理 |
| `/data-sources/{Id:long}` | `Pages/Platform/DataSourceDetail.razor` | Main | 授权清单 + 行级安全策略 |
| `/model-accounts` | `Pages/Platform/ModelAccounts.razor` | Main | 模型与账号 |
| `/admin/tenants` | `Pages/Admin/Tenants.razor` | Main | 租户 |
| `/admin/identity` | `Pages/Admin/Identity.razor` | Main | 身份权限 |
| `/admin/audit` | `Pages/Admin/Audit.razor` | Main | 审计 |
| `/admin/quota` | `Pages/Admin/Quota.razor` | Main | 配额 |
| `/admin/localization` | `Pages/Admin/Localization.razor` | Main | 多语言 |
| `/admin/themes` | `Pages/Admin/Themes.razor` | Main | 主题（治理视角） |
| `/admin/system` | `Pages/Admin/SystemStatus.razor` | Main | 系统状态（/health + /metrics） |
| `/settings` | `Pages/Account/Profile.razor` | Main | 个人设置（账号 / 主题偏好 / 退出） |
| `/403` `/404` `/500` | `Pages/Errors/*.razor` | Blank | 错误页 |

**约定**：新增页面必须同步在 `Layout/NavMenuItems.cs` 登记（否则侧栏与面包屑均不识别）。

---

## 3. 布局层

| 组件 | 职责 | 关键实现约束 |
| --- | --- | --- |
| `MainLayout` | 应用壳：顶栏（汉堡 / 品牌 / 主题 / 用户菜单）+ 侧栏 + 内容 + `SbToastHost` | 主题读取与会话自举**必须**在 `OnAfterRenderAsync(firstRender)` 执行；预渲染阶段无 JS 运行时，在 `OnInitialized` 调用 `IJSRuntime` 会抛 500 |
| `BlankLayout` | 独立页面外壳 | 与 MainLayout 一样执行 `Theme.ApplyAsync` + `Session.RestoreAsync/ValidateAsync`，否则直接访问 `/login` 时无法感知已登录 |
| `NavMenu` | 侧栏渲染 | 只读 `NavMenuItems.Groups`；`href=""` 项使用 `NavLinkMatch.All`，其余用 `Prefix` |
| `NavMenuItems` | 导航元数据 | 新增页面唯一登记点；`EnforcePermissions` 默认 `false`（权限码未与后端对齐时不过度隐藏） |
| `AppBreadcrumb` | 分组 / 父级 / 当前 | 详情页按 URL 首段反查父级（`dashboards/12` → `dashboards`） |
| `IconSprite` | 图标 symbol | 在 `MainLayout` 与 `BlankLayout` 各渲染一次，保证图标全局可用 |

---

## 4. 组件库

### 4.1 Shared/UI（通用，零业务依赖）

| 组件 | 用途 | 主要参数 |
| --- | --- | --- |
| `PageHead` | 页面页头（图标+标题+描述+操作区） | `Icon` `Title` `Desc` `ChildContent` |
| `SbPanel` | 面板容器 | `Icon` `Title` `Actions` `Body` `Footer` `Flush` |
| `SbToolbar` | 工具栏（左/右分区） | `Left` `Right` |
| `SbSearch` | 搜索输入（带清除） | `Value` `ValueChanged` `Placeholder` |
| `SbBadge` | 状态徽章 | `Tone` ∈ primary/success/warning/danger/info/muted |
| `SbStatTile` | 指标卡 | `Label` `Value` `Sub` `Delta` `DeltaUp` `Accent` |
| `SbEmptyState` | 空态 | `Icon` `Title` `Text` `Actions` |
| `SbSpinner` / `SbLoading` | 加载指示 | `Size` `Text` / `LoadingState` 的 `Block` |
| `SbAlert` | 提示条 | `Tone` `Title` `Dismissible` `OnDismiss` |
| `SbField` | 表单字段容器 | `Label` `Required` `Full` `Hint` `Error` |
| `SbProgress` | 进度条（阈值变色） | `Percent` `WarningThreshold` `DangerThreshold` |
| `SbSegmented` | 分段单选（泛型） | `Items` `Value` `ValueChanged` `TextSelector` |
| `SbKeyValue` | 键值列表 | `Items` |
| `SbPagination` | 分页 | `Page` `PageSize` `TotalCount` `PageChanged` |
| `SbDataTable` | 通用表格（排序/分页/空态/行操作） | `Items` `Columns`（`SbCol.Of<T>` 构造）`RowActions` |
| `SbTabs` / `SbTab` | 标签页 | `Title` `Badge` |
| `SbModal` | 模态框 | `Visible` `Title` `Size` `Body` `Footer` |
| `SbConfirm` | 确认对话框（基于 SbModal） | `Visible` `Message` `Tone` `OnConfirm` |
| `SbToastHost` | 全局轻提示宿主 | 由 `ToastService` 驱动 |

### 4.2 Shared/BI

| 组件 | 用途 |
| --- | --- |
| `ChartView` | Chart.js 渲染（bar/line/pie，含语义色板） |
| `KpiGrid` | 消费 `QueryAnswer.Summary` 渲染 KPI 卡 |
| `SqlViewer` | SQL 折叠查看 + 一键复制 |
| `ResultTable` | 任意「字段→值」字典列表 → 友好表格（复用 `TablePresenter`） |
| `DataSourcePicker` | 数据源下拉选择 |

### 4.3 Shared/Feedback 与 Guard

| 组件 | 用途 |
| --- | --- |
| `LoadingState` | 统一加载态（块级/行内） |
| `ErrorState` | 统一错误态（message + code + traceId + 重试） |
| `AuthGuard` | 登录守卫；等待 `AppState.SessionRestored` 后再判定，避免预渲染误判 |
| `PermissionGuard` | 权限守卫（`Require` + `Mode=Any/All`），受 `NavMenuItems.EnforcePermissions` 开关约束 |

---

## 5. 服务与状态

| 类型 | 职责 | 生命周期 |
| --- | --- | --- |
| `AppState` | 令牌/租户/用户/权限 + 会话自举标志 `SessionRestored` + `SessionExpired` 事件 | Scoped |
| `AuthStore` | localStorage 持久化、启动还原、`/api/auth/me` 校验 | Scoped |
| `ThemeService` | 主题应用（明/暗） | Scoped |
| `LocalizationService` | 多语言文案 | Scoped |
| `ToastService` | 轻提示队列（最多 5 条，含自动消失） | Scoped |
| `IApiClient` / `ApiClient` | HTTP 通信；统一错误体解析；401 → 会话失效回收 | Scoped（HttpClient 由 Head 注册名为 `SuperBuilderApi`） |

`IApiClient` 能力矩阵：

| 方法 | 用途 |
| --- | --- |
| `LoginAsync` / `AskAsync` / `RefineAsync` / `PublishAppAsync` | 业务特化 |
| `GetAsync<T>` / `GetJsonAsync` / `GetTextAsync` | 读取（强类型 / 松类型 JSON / 纯文本） |
| `SendAsync` / `PostAsync` / `PutAsync` / `PatchAsync` / `DeleteAsync` | 通用写操作 |

**注意**：新增方法必须同时加入 `IApiClient` 接口，页面只能注入 `IApiClient`（注册为 `AddScoped<IApiClient, ApiClient>()`），注入具体类会 500。

---

## 6. 新增页面标准流程（5 步）

1. 在 `Components/Pages/<域>/<名称>.razor` 建文件，首行 `@page "/路由"`，按需 `@layout BlankLayout`。
2. 在 `Layout/NavMenuItems.cs` 登记（需在侧栏显示时），填写 `Href / Title / Icon / Permission`。
3. 内容用 `PageHead` 起头；列表用 `SbPanel + SbDataTable`，详情用 `SbTabs + SbKeyValue`，异步态用 `LoadingState` / `ErrorState`。
4. 需要登录时用 `<AuthGuard>` 包裹；涉及治理操作时叠加 `<PermissionGuard>`。
5. 两个 Head 无需改动（路由由 `AppAssembly` 扫描），但如新增服务需在 `SuperBuilder_AI.Web/Program.cs` 与 `SuperBuilder_AI.Maui/MauiProgram.cs` 同时注册。

---

## 7. 已知约束与踩坑（必读）

1. **事件处理器内含 C# 字符串 → 属性用单引号定界**：`@onclick='() => Nav.NavigateTo("ask")'` ✅；`@onclick="() => Nav.NavigateTo("ask")"` ❌（双引号提前闭合属性 → CS1026）。
2. **渲染名为 `code` 的变量必须写 `@(code)`**，否则被解析为 `@code` 指令（RZ2005）。
3. **void 方法不能直接绑 `@onclick`**（CS1503），须包 lambda。
4. **只能注入 `IApiClient`**，注入实现类会「无注册服务」500；新方法同步加接口。
5. **预渲染阶段无 JS 运行时**：`IJSRuntime` 调用一律放 `OnAfterRenderAsync(firstRender)`；初始主题由 `_Host.cshtml` / MAUI `index.html` 内联脚本写入 `data-theme` 防闪烁。
6. **会话自举时序**：子组件的 `OnAfterRender` 早于 `MainLayout`，因此登录判定必须依赖 `AppState.SessionRestored`，不能只看 `IsAuthenticated`。
7. **RCL 不可引用 `Microsoft.AspNetCore.Components.WebView.Maui`**（会与 MAUI 宿主静态资源冲突导致 build error）；WebView 能力由 Head 项目提供。
8. **HTML 属性不支持 `@bind-open`**（`<details>`）等非标准绑定（RZ9991），改用普通属性或自有状态。
9. **后端基线不受前端影响**：前端改动不触碰 `Evaluation/Golden` 契约，Golden 仍走独立 `evaluation/golden-runtime` 端点。
