> **HISTORICAL SNAPSHOT / 历史快照**：本文是归档资料，只描述记录当时的计划、状态或审计判断。文中的“当前、唯一、已完成、未完成、风险、测试基线、HEAD”等均不得解释为现在的项目状态。当前事实请按 `docs/README.md` 的治理顺序核验。\n\n> **治理声明**：本文档是 `Master_Development_Plan.md` 的**输入 / 审计基线**，**不是**独立执行计划。所有里程碑状态、优先级与验收以 `Master_Development_Plan.md`（唯一事实来源）及其 `milestones/` 拆分文档为准；本文与 MDP 冲突时以 MDP 为准。映射见 MDP §17。
>
> **文档角色**：前端整改清单（S6* 映射到 M8/M11，见 MDP §17.2）

# SuperBuilder AI · 前端开发与发布整改清单（P11.7）

> 结构规范见 `docs/Frontend_Structure_Plan.md`。
> 状态图例：✅ 已完成 ｜ 🟡 已实现但验收未完全闭环 ｜ ⬜ 待办 ｜ ⛔ 受外部条件阻塞。
> 本文件于 **2026-09-02** 完成一次源码、构建与测试复审。清单状态以“可验证的实际行为”为准，不再仅以代码文件存在或 Debug 编译成功判定完成。
>
> 当前结论：核心功能覆盖约 **90%**，发布验收成熟度约 **82%–85%**。项目已进入“发布前整改与真实环境回归”阶段。
>
> 统一退出门槛：
> 1. RCL / Web / MAUI(Windows) **干净构建 0 error**，前端自有 C# 警告清零；
> 2. 后端及前端组件测试全量通过，当前基线为 **426/426**；
> 3. 权限守卫不得 fail-open，敏感页面直连验证通过；
> 4. Android 完成模拟器或真机主流程回归；iOS 至少完成 Release 编译、裁剪与静态资源验证；
> 5. Golden 基线不回归，工作区保持干净，并留存构建/测试/回归证据。

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

## S1 · 现有页面组件化改造（🟡 主体完成，仍有占位操作）

> 目标：把 P11.3 的 15 个页面从「手写 HTML + 内联样式」升级为统一组件，消除重复与占位实现。
> 验收：RCL / Web / MAUI(Windows) 三端 build 0 error；所有列表页统一为 `SbListPage`/通用组件；全局无占位 Toast。

| ID | 任务 | 依赖 | 验收 | 状态 |
| --- | --- | --- | --- | --- |
| S1-1 | 列表页统一 | S0-3 | Dashboards / Apps / Agent / SemanticLabels / BusinessModel / Audit / Identity / Localization / Quota / DataSources / ModelAccounts / Admin×6 表现一致 | ✅ |
| S1-2 | `ComponentGallery` 增补新组件示例（Tabs/Modal/Confirm/Toast/DataTable/Guard） | S0-3 | 新组件全部可在该页交互预览 | ✅ |
| S1-3 | 消除占位 Toast | S0-6 | 全局无“未实现但提示成功”的占位行为；提示统一右下角弹出（`ToastService`） | 🟡 Toast 已统一；Agent 新建、BusinessModel 新建、ThemeEditor 保存仍为占位行为，纳入 S6-4 |
| S1-4 | `SemanticLabels` / `BusinessModel` 详情页化 | S1-1 | 新增 `SemanticLabelDetail` / `BusinessModelEntityDetail` 路由，列表行可跳转 | ✅ |
| S1-5 | `Ask.razor` 拆分为子组件 | S0-5 | 单文件 446→~150 行；抽取 `AskTurn`/`VizOverride` 到 `Models`，结果渲染移交 `AskTurnCard` | ✅ |

> 实现要点：列表页统一走 `SbListPage`（含 `AuthGuard`、统计卡、搜索、分页、空/错态）；双列表/多视图页（Identity、BusinessModel）改用 `SbPanel + SbDataTable`/`SbTabs` 组合；占位 Toast 全部替换为 `ToastService.Info/Success/Warning/Error`。

---

## S2 · 写操作闭环（🟡 清单内主要操作完成，跨页面 CRUD 尚未完全闭环）

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

> 审计补充：S2 原始范围内的 Dashboard / App / SemanticLabel / Tenant / Identity 写操作基本闭环；但页面级“完整管理能力”仍受 Agent 编排器、BusinessModel 实体编辑器、主题持久化以及 S2-7 后端端点缺失影响。这些事项统一纳入 S6-4 与后端依赖清单。

> 实现要点：写操作统一走 `await Api.PostAsync/PutAsync/PatchAsync/DeleteAsync(url, body)`（返回 `(Ok, Status, Error)`，不抛异常）；删除经 `SbConfirm` 二次确认；模态表单用 `SbModal` + `SbField`；错误以 `Toast` + 表单内 `alert` 呈现。

## S3 · Ask 旗舰体验增强（🟡 核心能力完成，边界行为待整改）

| ID | 任务 | 依赖 | 验收 | 状态 |
| --- | --- | --- | --- | --- |
| S3-1 | 数据源从「手输 ID」改为 `DataSourcePicker` 下拉（后端补 `GET api/data-sources`） | S0-5 | 提问前可选择数据源 | ✅ 后端 `DataSourcesController` 返回授权数据源（id+name+dbType）；Ask 启动拉取填充 `DataSourcePicker`，默认选中首项 |
| S3-2 | 会话持久化与历史（localStorage 保存最近 N 轮，支持回溯） | S1-5 | 刷新后恢复上下文及原数据源，失效数据源有明确降级 | 🟡 最近 50 轮可恢复；当前先默认首个数据源再恢复历史，非 ID=1 场景可能无法恢复原数据源，纳入 S6-2 |
| S3-3 | 结果导出（CSV / Excel 前端生成） | S0-5 | CSV 列名/数据一致；Excel 为标准 `.xlsx` 或 UI 明确标注兼容格式；失败有反馈 | 🟡 CSV 已完成；当前 Excel 实际为 HTML table `.xls` 兼容文件，下载异常被静默吞掉，纳入 S6-3 |
| S3-4 | 图表能力扩展（堆叠/面积/多轴、下钻） | S0-5 | 图表类型覆盖主要分析场景；下钻语义与交互验证通过 | 🟡 堆叠/面积/多轴已实现；当前“下钻”实质为分类 Chip 过滤表格，尚非图表点击下钻，纳入 S6-7 |
| S3-5 | 语义细化结果对比（并列展示原结果与细化结果） | 现有 `api/ask/refine` | 多轮对话可读性提升 | ✅ 细化子轮「对比原结果」开关，两栏并列展示父轮（原问题）与子轮（细化）的答案文本 + 数据表 + SQL |

---

## S4 · 权限与治理（🟡 权限码已对齐，守卫策略存在高优先级缺陷）

| ID | 任务 | 依赖 | 验收 | 状态 |
| --- | --- | --- | --- | --- |
| S4-1 | 前端权限码与后端 `api/identity/permissions` 对齐（`NavMenuItems.Permission`） | 后端确认 | 权限码清单文档化 | ✅ 新增 `Components/Constants/PermissionCodes.cs`（镜像后端 `IdentityPermissions`）；菜单/页面统一引用，杜绝手敲漂移 |
| S4-2 | 打开 `NavMenuItems.EnforcePermissions = true` | S4-1 | 权限加载前显示等待态；加载完成后无权限菜单隐藏，直连 URL 被拒绝 | 🟡 开关与页面守卫已接入；`owned.Count==0` 当前直接放行，无法区分“未加载”和“确实无权限”，存在 fail-open，必须由 S6-1 修复 |
| S4-3 | 审计日志筛选与导出（按用户/时间/资源） | S1-1 | 支持分页与条件检索 | ✅ `Audit.razor` 增 action/entityType/actor/时间 四维筛选 + CSV 导出（复用 `FileDownloadService`，带 BOM） |
| S4-4 | 敏感操作二次确认（`SbConfirm`）覆盖删除/禁用/授权变更 | S2 | 危险操作均有确认 | ✅ 租户启用/停用、角色权限保存、角色分配保存均接入 `SbConfirm`；删除类已在 S2 覆盖 |

### 权限码映射矩阵（S4-1 交付物 · 前端 → 后端 `IdentityPermissions`）

| 菜单项 | 前端 `Permission` | 后端码 | 持有角色 | 门禁依据 |
| --- | --- | --- | --- | --- |
| admin/tenants 租户 | `platform:tenant:view` | `platform:tenant:view` | platform-admin | `TenantManagementController.RequirePlatformPermission`（确认强制） |
| admin/identity 身份权限 | `identity:manage` | `identity:manage` | tenant-admin | 后端 identity 端点为认证+租户隔离 |
| admin/audit 审计 | `audit:view` | `audit:view` | tenant-admin | `AuditController.HasAuditPermission`（确认强制） |
| admin/themes 主题 | `theme:view` | `theme:view` | tenant-admin | 目录定义 |
| admin/quota 配额 | （无，仅登录） | 后端 `api/quota` **无细粒度门禁** | 任意已登录 | 后端仅做租户隔离，前端不过度隐藏 |
| admin/system 系统状态 | （无，仅登录） | 后端 `/health`、`/metrics` 未挂 `platform:diagnostics:view` | 任意已登录 | 避免比后端更严格地误隐藏 |
| admin/localization 多语言 | （无，仅登录） | 后端目录**无 localization 权限码** | 任意已登录 | 同 S2-7 后端缺口，待补码后接入 |

> 对齐原则：**后端确有强制鉴权码的用真实码**；后端无门禁/无码的页面保持仅登录。前端只负责体验层门禁，后端始终是最终安全边界。权限加载状态必须独立建模，禁止用“空权限集合”表示“尚未加载”。
> 约束：`tests` 中 `Assert.Equal(31, permCount)` 精确断言全局权限数，故**不改动后端 `IdentityCatalog`/seed**（localization 等缺口留待后端补齐码，非前端能解）。

---

## S5 · 质量与可观测（🟡 基础能力完成，发布级验证未闭环）

| ID | 任务 | 验收 | 状态 |
| --- | --- | --- | --- |
| S5-1 | 错误边界（`ErrorBoundary` 包裹内容区，异常降级到友好卡片） | 单组件异常不白屏 | ✅ `MainLayout` 内容区 `<ErrorBoundary>`；`ErrorContent` 渲染隔离卡片（异常信息可展开 + 重试/返回），`Recover()` 重置子树 |
| S5-2 | 无障碍与键盘可达（焦点可见、`aria-*` 齐全） | 主要流程完成键盘与屏幕阅读器验证 | 🟡 基础焦点环、skip-link、导航 aria 已实现；尚无自动化 axe/人工屏幕阅读器验收证据，纳入 S6-6 |
| S5-3 | 响应式回归（≤991px 抽屉、≤560px 堆叠） | 关键页面断点无溢出并有视觉回归证据 | 🟡 CSS 断点与表格滚动已实现；尚无关键页面截图基线和端到端断点回归，纳入 S6-6 |
| S5-4 | MAUI 双端回归（Android `10.0.2.2` 基址、iOS 编译链） | Windows/Android 主流程可用；iOS Release 编译、裁剪与静态资源验证通过 | 🟡 MAUI Windows 0 error，Android 基址已处理，RCL Android/iOS TFM 可编译；iOS 存在静态资源路径与裁剪警告，完整 `.app` 仍需配对 Mac 验证，纳入 S6-5 |
| S5-5 | 组件级单测（bUnit 覆盖 `SbDataTable`、`AuthGuard`、`ToastService`） | 关键组件有测试 | ✅ 测试项目加 bUnit 2.9.0 + 引用 RCL；新增 `ToastServiceTests`(6) / `SbDataTableTests`(4) / `AuthGuardTests`(3) 共 13 例，全量 426 通过 |
| S5-6 | 性能检查（列表虚拟滚动、图表实例复用） | 大数据量不卡顿，有基准或性能回归证据 | 🟡 `MaxHeight` 仅限制容器高度，并非虚拟滚动；图表当前 destroy→new，属于正确清理而非实例复用；真实虚拟化、Chart.js `update()` 与性能基准纳入 S6-7 |

---

## S6 · 发布前整改与真实环境验收（⬜ 新计划）

> 目标：修正本次审计发现的状态偏差，使“功能已实现”真正升级为“可发布、可验证、可维护”。执行顺序按 P0 → P1 → P2，不建议跳过 P0/P1 直接发布。

| ID | 优先级 | 任务 | 主要修改点 | 验收标准 | 状态 |
| --- | --- | --- | --- | --- | --- |
| S6-1 | P0 | 修复权限守卫 fail-open | `AppState` 增加独立 `PermissionsLoaded`/加载失败状态；`AuthStore.ValidateAsync` 明确写入；`NavMenu`/`PermissionGuard` 在加载前等待、加载后严格判断 | 空权限用户看不到受限菜单，直连显示 403；权限加载失败不放行；新增 bUnit 覆盖未加载/空权限/有权限/All/Any | ⬜ |
| S6-2 | P0 | Ask 异常与会话恢复可靠性 | `DoAsk` 使用 try/catch/finally；加入超时/取消/防重复提交；会话单独保存数据源 ID，恢复后校验授权列表 | 断网、超时、401 后 Busy 必定恢复；历史轮次与原数据源一致；失效数据源有明确提示和安全回退 | ⬜ |
| S6-3 | P1 | 导出能力标准化 | CSV 保留；Excel 改为真正 `.xlsx`，或将 UI/文档明确标记为“Excel 兼容 `.xls`”；下载服务返回结果并由 Toast 告知失败 | Excel 不出现格式与扩展名不一致警告；中文、数字、日期、空值验证通过；Web/Windows/Android 行为有记录 | ⬜ |
| S6-4 | P1 | 清除剩余占位操作 | Agent 新建、BusinessModel 新建/编辑、ThemeEditor 保存接真实端点；若后端缺失则禁用按钮并明确标注依赖，不得提示“已保存” | 全局搜索无“将在 S2 接入”等陈旧提示；所有可点击主操作均产生真实状态变化或明确不可用原因 | ⬜ |
| S6-5 | P1 | 干净构建与移动端发布验证 | 停止锁文件进程后重新构建；修复 CS4014/CS8602/CS0414、MAUI `MainPage` 弃用；处理 iOS 静态资源/裁剪警告；验证 Android/iOS | RCL/Web/MAUI Win 0 error；前端自有 C# warning 0；Android 主流程通过；iOS Release 编译及静态资源/JSON 裁剪验证通过 | ⬜ |
| S6-6 | P1 | E2E、无障碍与视觉回归 | 为登录、Ask、CRUD、权限拒绝、窄屏菜单建立 Playwright 流程；增加 axe 与 991/560 截图基线 | 关键流程端到端通过；无严重无障碍问题；两个断点无溢出且视觉差异受控 | ⬜ |
| S6-7 | P2 | 性能与图表交互真实性 | `SbDataTable` 使用真正虚拟化或服务端分页；Chart.js 优先 `update()`；大数据压测；图表点击下钻或重命名为“分类筛选” | 10k 行场景不一次渲染全部 DOM；图表更新无泄漏且减少重建；下钻名称与实际行为一致 | ⬜ |
| S6-8 | P2 | 生命周期与安全加固 | `MainLayout` 解除事件订阅并避免 `async void`；审视 Token localStorage 策略；增加 CSP、短期令牌/刷新策略说明 | 页面/布局重复创建无事件泄漏；会话失效稳定跳转；安全设计文档明确 XSS 与令牌存储边界 | ⬜ |
| S6-9 | P2 | 后端依赖补齐 | Localization 写端点、Quota 策略维护端点、缺失权限码及 Agent/BusinessModel/Theme 写契约与后端联合确认 | 接口契约、权限码、错误码文档化；前端不再保留无法闭环的可操作入口 | ⛔ 待后端契约 |

### S6 分批交付建议

1. **批次 A（安全与稳定）**：S6-1、S6-2、CS4014/CS8602；完成后才允许进入候选发布分支。
2. **批次 B（功能真实性）**：S6-3、S6-4、S6-9；清除“按钮可点但不落盘”和格式名不副实问题。
3. **批次 C（发布验收）**：S6-5、S6-6；产出三端构建、移动端运行、E2E、无障碍和视觉证据。
4. **批次 D（性能与维护）**：S6-7、S6-8；作为正式发布前的质量收口，或在明确容量约束后进入紧邻版本。

### S6 证据清单

- `dotnet build`：RCL 三 TFM、Web、MAUI Windows 的完整日志；
- `dotnet test`：全量测试结果及新增前端组件/E2E 用例数量；
- 权限矩阵：每个受限页面的“有权限/无权限/空权限/加载失败”结果；
- Android/iOS：设备、系统版本、构建模式及登录→Ask→图表→导出主流程记录；
- 响应式/无障碍：991px、560px 截图基线及 axe/人工键盘检查结果；
- 性能：表格行数、DOM 节点数、首次渲染时间、图表连续切换内存曲线。

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
| `Ask.razor` 中 `ApplyOutcome` 的 `outcome.Response` 空引用（CS8602） | 编译器仍报告可空解引用；需改为局部非空变量或显式分支，避免未来模型契约变化触发异常 | S6-5 |
| `Dashboards.Create()` 未等待 `OpenCreate()`（CS4014） | 异步异常可能成为未观察异常，蓝图加载时序不可控 | S6-5 |
| `BusinessModel._loading` 未使用（CS0414） | 加载态未接入 UI 或应删除无效状态 | S6-5 |
| `PermissionGuard`/`NavMenu` 空权限集合直接放行 | 无法区分“权限未加载”与“用户确实无权限”，与 S4-2 验收冲突 | S6-1（P0） |
| Ask 网络异常后 Busy 可能不恢复 | `DoAsk` 缺少 try/finally，网络异常可导致交互锁死 | S6-2（P0） |
| Ask 历史数据源恢复条件不可靠 | 默认数据源先赋值后，历史 `DataSourceIdHint` 可能被忽略 | S6-2（P0） |
| Excel 导出为 HTML table `.xls` | 属 Excel 兼容格式，不是标准工作簿；下载失败被静默吞掉 | S6-3 |
| Agent/BusinessModel/ThemeEditor 仍有占位操作 | 主按钮可点击但不产生真实持久化结果 | S6-4 |
| iOS 静态资源路径与裁剪警告 | Debug TFM 0 error 不等于 Release `.app` 可运行 | S6-5 |
| MAUI `Application.MainPage` 已弃用 | 应迁移为 `CreateWindow()` | S6-5 |
| 表格“虚拟滚动”名不副实 | 当前只有限高滚动容器，全部行仍进入 DOM | S6-7 |
| 图表“实例复用”名不副实 | 当前为 destroy→new，生命周期清理正确但没有复用实例 | S6-7 |
| `MainLayout` 事件未解除订阅、会话事件使用 `async void` | 长生命周期下存在事件泄漏和未观察异常风险 | S6-8 |
| `ThemeEditor._msg` 未使用警告 | S1-3 已改为 `ToastService`，本项已解决 | ✅ 已解决 |
| 权限码未对齐 | `EnforcePermissions` 已由 S4 置 `true`，菜单/页面权限码与后端 `IdentityPermissions` 对齐（见 S4 映射矩阵） | ✅ 已解决（S4） |
| 删除类操作 | Dashboard / App / Agent 等列表与详情已覆盖单项删除；批量删除不在当前验收范围，若需要应另立需求 | ✅ 当前范围完成 |

## 附录 C · 2026-09-02 审计验证记录

| 检查项 | 结果 | 说明 |
| --- | --- | --- |
| Git 工作区 | ✅ 干净 | 审计开始时无未提交修改 |
| RCL 多目标构建 | 🟡 0 error / 105 warnings | `net10.0`、Android、iOS 均生成成功；含 CS4014/CS8602/CS0414、iOS 静态资源与裁剪警告 |
| Web 构建 | ⛔ 本次未完成 | 输出 DLL 被正在运行的 Visual Studio / Web 进程锁定；属于环境占用，但当前提交仍需停服后重验 |
| MAUI Windows | 🟡 0 error / 2 warnings | `Application.MainPage` 弃用警告重复出现 |
| 自动化测试 | ✅ 426/426 | 使用现有构建产物执行通过；因后端进程锁定，本次未完成测试依赖的干净重建 |
| Android/iOS 运行 | ⛔ 未在本次审计执行 | Android/iOS 真实运行证据需在 S6-5 补齐 |
