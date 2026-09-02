# SuperBuilder AI · 前端开发清单（P11.6）

> 结构规范见 `docs/Frontend_Structure_Plan.md`。
> 状态图例：✅ 已完成 ｜ 🟡 进行中 ｜ ⬜ 待办。
> 统一退出门槛：**RCL / Web / MAUI(Windows) 三端 build 0 error**，后端单测保持 308/308，Golden 基线不回归。

---

## S0 · 结构与基建（✅ 本轮完成）

| ID | 任务 | 产出 | 验收 |
| --- | --- | --- | --- |
| S0-1 | 目录分层重组 | `Pages/{Analysis,Design,Platform,Admin,Account,Errors}`、`Shared/{UI,BI,Feedback,Guard}`；`_Imports.razor`（两个 Head）同步登记 | 三端 build 0 error；路由不变 |
| S0-2 | 布局层补齐 | `BlankLayout`、`AppBreadcrumb`、`IconSprite`、`NavMenuItems`；`MainLayout` 增加用户菜单/退出与 `SbToastHost` | 登录页与错误页无侧栏顶栏；面包屑正确显示「分组/父级/当前」 |
| S0-3 | 通用组件库 | 19 个 `Sb*` 组件 + `SbColumn/SbCol` 列定义 | 组件库页可预览（纳入 S1-2） |
| S0-4 | 守卫与反馈 | `AuthGuard`、`PermissionGuard`、`LoadingState`、`ErrorState` | 未登录访问受保护页不闪烁、不误跳 |
| S0-5 | BI 组件 | `KpiGrid`、`SqlViewer`、`ResultTable`、`DataSourcePicker`（`ChartView` 迁至 `Shared/BI`） | Ask 结果可复用组件渲染 |
| S0-6 | 服务增强 | `ToastService`；`AppState.SessionRestored`；`IApiClient` 写操作 + `GetTextAsync` | 详情页删除/刷新可用 |
| S0-7 | 页面补齐 | 工作台 `/`、仪表盘/应用/Agent 计划/数据源详情、个人设置、403/404/500、系统状态 | 新页面可访问且错误态优雅 |
| S0-8 | 根路由修正 | `Login` 仅保留 `/login`，`/` 由 `Home` 接管 | 侧栏「首页」进入工作台而非登录页 |

---

## S1 · 现有页面组件化改造（✅ 本轮完成）

> 目标：把 P11.3 的 15 个页面从「手写 HTML + 内联样式」升级为统一组件，消除重复与占位实现。
> 验收：RCL / Web / MAUI(Windows) 三端 build 0 error；所有列表页统一为 `SbListPage`/通用组件；全局无占位 Toast。

| ID | 任务 | 依赖 | 验收 | 状态 |
| --- | --- | --- | --- | --- |
| S1-1 | 列表页统一 | S0-3 | Dashboards / Apps / Agent / SemanticLabels / BusinessModel / Audit / Identity / Localization / Quota / DataSources / ModelAccounts / Admin×6 表现一致 | ✅ |
| S1-2 | `ComponentGallery` 增补新组件示例（Tabs/Modal/Confirm/Toast/DataTable/Guard） | S0-3 | 新组件全部可在该页交互预览 | ✅ |
| S1-3 | 消除占位 Toast | S0-6 | 全局无 `_msg` 占位；提示统一右下角弹出（`ToastService`） | ✅ |
| S1-4 | `SemanticLabels` / `BusinessModel` 详情页化 | S1-1 | 新增 `SemanticLabelDetail` / `BusinessModelEntityDetail` 路由，列表行可跳转 | ✅ |
| S1-5 | `Ask.razor` 拆分为子组件 | S0-5 | 单文件 446→~150 行；抽取 `AskTurn`/`VizOverride` 到 `Models`，结果渲染移交 `AskTurnCard` | ✅ |

> 实现要点：列表页统一走 `SbListPage`（含 `AuthGuard`、统计卡、搜索、分页、空/错态）；双列表/多视图页（Identity、BusinessModel）改用 `SbPanel + SbDataTable`/`SbTabs` 组合；占位 Toast 全部替换为 `ToastService.Info/Success/Warning/Error`。

---

## S2 · 写操作闭环（✅ 全部可补项完成；S2-7 受后端限制待补）

> 目标：从「只读展示」升级为完整 CRUD，前端具备真正的管理能力。
> 说明：后端写接口比清单假设更完整（已核查 `src/Api/Controllers`）。所有写操作统一走 `Api.PostAsync/PutAsync/PatchAsync/DeleteAsync`（返回 `(Ok, Status, Error)` 不抛异常），删除经 `SbConfirm` 二次确认，模态表单用 `SbModal` + `SbField`，错误以 `Toast` + 表单内提示呈现。本轮新增：身份全量写、应用/仪表盘 DSL 编辑器。

| ID | 任务 | 依赖 | 验收 | 状态 |
| --- | --- | --- | --- | --- |
| S2-1 | 仪表盘删除（`DELETE api/dashboards/{id}`）+ 新建（拉取 `editor/blueprint` 骨架 → `POST api/dashboards`） | S0-6 | 列表可删除；新建走 `editor/blueprint` DSL | ✅ 删除+新建已闭环 |
| S2-2 | 应用删除（`DELETE api/apps/{code}`）+ 生成（`POST api/apps/generate`）+ 保存（`PUT api/apps/{code}`） | S2-1 | 卡片墙可删除；描述生成 / DSL 编辑可保存 | ✅ 删除+生成+保存已闭环 |
| S2-3 | 语义标签新建（`POST api/semantic-labels`，`UpsertSemanticLabelRequest`） | S0-6 | 表单提交后召回命中 | ✅ 新建已闭环（模态表单） |
| S2-4 | 租户管理（创建 `POST`、启用/停用 `PATCH enable/disable`、**设置 Upsert** `POST {id}/settings`） | S0-6 | 租户生命周期可闭环 | ✅ 列表+新建+启停+设置 Upsert 全闭环（`Tenants.razor`） |
| S2-5 | 身份权限写操作（用户创建、角色创建、角色分配/回收、角色权限更新） | S0-6 | 权限变更后重新登录生效 | ✅ 创建用户/角色/分配/编辑权限已闭环 |
| S2-6 | 统一表单校验与错误呈现（客户端 `SbField.Required`+`Error` 字段级；服务端 `errors` 数组友好化） | S1-1 | 字段级错误来自客户端校验+服务端错误可读化 | ✅ 客户端字段级校验（`SbField.Error`，Tenants/SemanticLabels/Identity）+ 服务端 `{ errors:[...] }` 解析友好化（`ApiClient.ParseApiError`） |
| S2-7 | 配额与多语言写操作（配额检查/消费、语言包维护） | S0-6 | 配额与语言项可维护 | ⬜ 后端限制：Localization 仅 GET（无写端点）、Quota 仅 `check/consume` 运行时（无配额项维护端点），前端无法闭环；待后端补齐写接口后接入 |

> 实现要点：写操作统一走 `await Api.PostAsync/PutAsync/PatchAsync/DeleteAsync(url, body)`（返回 `(Ok, Status, Error)`，不抛异常）；删除经 `SbConfirm` 二次确认；模态表单用 `SbModal` + `SbField`；错误以 `Toast` + 表单内 `alert` 呈现。

## S3 · Ask 旗舰体验增强（✅ 本轮完成，三端 build 0 error）

| ID | 任务 | 依赖 | 验收 | 状态 |
| --- | --- | --- | --- | --- |
| S3-1 | 数据源从「手输 ID」改为 `DataSourcePicker` 下拉（后端补 `GET api/data-sources`） | S0-5 | 提问前可选择数据源 | ✅ 后端 `DataSourcesController` 返回授权数据源（id+name+dbType）；Ask 启动拉取填充 `DataSourcePicker`，默认选中首项 |
| S3-2 | 会话持久化与历史（localStorage 保存最近 N 轮，支持回溯） | S1-5 | 刷新后可恢复上下文 | ✅ 新增 `AskSessionStore`（IJSRuntime + localStorage，`IncludeFields` 序列化 `AskTurn` 含 `BIResponse`/图表覆盖）；会话自举后恢复、提问/细化/清空落盘、上限 50 轮 |
| S3-3 | 结果导出（CSV / Excel 前端生成） | S0-5 | 导出文件列名与数据一致 | ✅ 新增 `FileDownloadService`（Blob 下载）+ 每轮表格「导出 CSV / 导出 Excel」；CSV 带 BOM 防中文乱码，Excel 为 HTML table xls |
| S3-4 | 图表能力扩展（堆叠/面积/多轴、下钻） | S0-5 | 图表类型覆盖主要分析场景 | ✅ `ChartView` 增 `Stacked/Area/MultiAxis`；`AskTurnCard` 工具栏增堆叠/面积/多轴切换；下钻（点击分类切片筛选表格） |
| S3-5 | 语义细化结果对比（并列展示原结果与细化结果） | 现有 `api/ask/refine` | 多轮对话可读性提升 | ✅ 细化子轮「对比原结果」开关，两栏并列展示父轮（原问题）与子轮（细化）的答案文本 + 数据表 + SQL |

---

## S4 · 权限与治理（⬜）

| ID | 任务 | 依赖 | 验收 |
| --- | --- | --- | --- |
| S4-1 | 前端权限码与后端 `api/identity/permissions` 对齐（`NavMenuItems.Permission`） | 后端确认 | 权限码清单文档化 |
| S4-2 | 打开 `NavMenuItems.EnforcePermissions = true` | S4-1 | 无权限菜单项隐藏、直连 URL 被 `PermissionGuard` 拦截 |
| S4-3 | 审计日志筛选与导出（按用户/时间/资源） | S1-1 | 支持分页与条件检索 |
| S4-4 | 敏感操作二次确认（`SbConfirm`）覆盖删除/禁用/授权变更 | S2 | 危险操作均有确认 |

---

## S5 · 质量与可观测（⬜）

| ID | 任务 | 验收 |
| --- | --- | --- |
| S5-1 | 错误边界（`ErrorBoundary` 包裹内容区，异常降级到 `/500` 样式） | 单组件异常不白屏 |
| S5-2 | 无障碍与键盘可达（焦点可见、`aria-*` 齐全） | 主要流程可键盘完成 |
| S5-3 | 响应式回归（≤991px 抽屉、≤560px 堆叠） | 断点无溢出 |
| S5-4 | MAUI 双端回归（Android `10.0.2.2` 基址、iOS 编译链） | 三端可用 |
| S5-5 | 组件级单测（bUnit 覆盖 `SbDataTable`、`AuthGuard`、`ToastService`） | 关键组件有测试 |
| S5-6 | 性能检查（列表虚拟滚动、图表实例复用） | 大数据量不卡顿 |

---

## 附录 A · 已完成项速查（本轮新增/变更文件）

**布局**：`Layout/BlankLayout.razor`（新）、`Layout/AppBreadcrumb.razor`（新）、`Layout/IconSprite.razor`（新）、`Layout/NavMenuItems.cs`（新）、`Layout/NavMenu.razor`（重写）、`Layout/MainLayout.razor`（增强）

**组件**：`Shared/UI/`：`PageHead`（迁入）、`SbPanel`、`SbToolbar`、`SbSearch`、`SbBadge`、`SbStatTile`、`SbEmptyState`、`SbSpinner`、`SbAlert`、`SbField`、`SbProgress`、`SbSegmented`、`SbKeyValue`、`SbPagination`、`SbDataTable`、`SbColumn.cs`、`SbTabs`、`SbTab`、`SbModal`、`SbConfirm`、`SbToastHost`
`Shared/BI/`：`ChartView`（迁入）、`KpiGrid`、`SqlViewer`、`ResultTable`、`DataSourcePicker`
`Shared/Feedback/`：`LoadingState`、`ErrorState` ｜ `Shared/Guard/`：`AuthGuard`、`PermissionGuard`

**页面（S0）**：`Pages/Home.razor`（新）、`Analysis/DashboardDetail.razor`（新）、`Analysis/AppDetail.razor`（新）、`Analysis/AgentPlanDetail.razor`（新）、`Platform/DataSourceDetail.razor`（新）、`Account/Profile.razor`（新）、`Admin/SystemStatus.razor`（新）、`Errors/{Forbidden,NotFound,ServerError}.razor`（新）、`Routes.razor`（404 空态）

**页面（S1）**：`Analysis/SemanticLabels.razor`、`Analysis/BusinessModel.razor`、`Analysis/Audit.razor`、`Analysis/Identity.razor`、`Analysis/Localization.razor`、`Analysis/Quota.razor`、`Platform/DataSources.razor`、`Platform/ModelAccounts.razor`、`Admin/Tenants.razor`、`Admin/Themes.razor`、`Design/ComponentGallery.razor`、`Analysis/Ask.razor` 全部改造为统一组件；新增 `Analysis/SemanticLabelDetail.razor`、`Analysis/BusinessModelEntityDetail.razor`、`Shared/Analysis/AskTurnCard.razor`

**模型**：`Models/AskTurn.cs`（新，`AskTurn` + `VizOverride`，从 Ask.razor 提取）

**服务**：`Services/ToastService.cs`（新）、`Services/AppState.cs`（`SessionRestored`）、`Services/AuthStore.cs`（自举置位）、`Services/IApiClient.cs` + `ApiClient.cs`（写操作/文本读取）、`Shared/JsonRows.cs`（新）

**样式**：`wwwroot/css/app.css` 追加组件库样式（全部基于主题变量，明暗自适应）

## 附录 B · 遗留项

| 项 | 说明 | 状态 |
| --- | --- | --- |
| `Ask.razor` 中 `ApplyOutcome` 的 `outcome.Response` 空引用（CS8602） | 既有可空警告，拆分后仍在 `Ask.razor`；不影响逻辑 | 遗留（低风险） |
| `ThemeEditor._msg` 未使用警告 | S1-3 已改为 `ToastService`，本项已解决 | ✅ 已解决 |
| 权限码未对齐 | `EnforcePermissions` 保持 `false`，见 S4 | 待 S4 |
| 删除类操作 | 目前仅详情页提供；列表页批量操作待 S2 | 待 S2 |
