# SuperBuilder AI · 最终开发计划（Master Development Plan）

> **版本**：v1.0　　**生成日期**：2026-09-03　　**性质**：整合主计划（唯一执行参考）
> **整合来源**：
> - `docs/DevChecklist_Final.md`（后端 P0 已闭环、P1×16、P2×10）
> - `docs/Frontend_DevChecklist.md`（前端 P11.7：S0–S6，已更新为发布前整改清单）
> - `docs/Audit_Report_and_DevPlan.md`（本审计发现的契约 404 / 初始化 / 多语言缺口）
> - `docs/Platform_Product_Development_Requirements.md`（平台产品需求基线）
> - `docs/DevelopmentPlan.md` / `PhaseChecklist.md`（Stage 1 架构治理 A3/A5，用户此前决定暂缓）
>
> **说明**：本计划取代上述清单的「分散跟踪」角色，成为统一执行参考。各源文档保留作细节溯源，但状态以本计划 + 门禁结果为准。所有条目均带来源标签，可追溯。

---

## 0. 总览与就绪度

| 维度 | 就绪度 | 一句话结论 |
|---|---|---|
| 后端治理（鉴权/租户隔离/审计/配额） | 🟢 ~90% | P0 全部闭合（411/411 + Golden 18/18），但**限流可绕过、租户作用域 opt-in 脆弱** |
| 前端页面覆盖 | 🟡 ~85% | 无文件级缺页；**4 个页面 404 契约 bug**、2 存根页、6 占位操作、权限守卫 fail-open |
| 前端构建/发布 | 🟡 ~80% | Web 上次审计构建未完成、47 项未提交未跑门禁、iOS 完整打包需 Mac |
| 初始化数据 | 🟡 ~55% | 缺 `Migrate`、全新库可能启动崩溃、无演示数据、多管理员禁建、明文凭据 |
| 多语言 | 🔴 ~35% | 后端管理面完备；**前端接入仅 5/67 文件**、5 语言链路被砍、保存不生效 |
| 架构/测试 | 🟡 ~50% | A3/A5 暂缓；P1/P2 全 ⬜；缺 E2E/集成测试、缺 Swagger |

**当前最高风险（必须最先处置）**：工作区 **47 项未提交变更**（含 12 新文件，部分实现平台需求基线）**未经 Golden 18/18 + 全量 426 测试门禁**。

---

## 1. 横切门禁纪律（适用于全部里程碑，不可降级）

> 取自 `DevChecklist_Final.md` 约束 1–4，全程强制。

1. **Golden 18/18 + 全量测试动态硬门禁**：每完成一项须复跑 Golden 18/18 PASS + 全量单测零失败；当前基线 **426/426**（后端 411 + 前端 bUnit 13），后续以 `master` 分支点测试总数为动态下限；**禁止删/跳/降级既有测试**降低门禁。
2. **安全项测试不得只断言 HTTP 状态码**：须同时断言无敏感数据返回、无越权副作用、拒绝已入账；涉及缓存须覆盖高权限→低权限、策略变更、角色撤销后的缓存隔离。
3. **门控隔离（Gated Isolation）**：优先保持默认路径稳定；安全修复可受控改变默认行为但须补充回归测试；Golden 基线不得为「让测试通过」而修改（确需改须 reviewer 书面确认 + `docs/` 记录 + 单次只对应一条 + 差异可审计）；**先加测试再改实现**。
4. **Golden 爆炸半径规则**：Builder / ValidationPipeline 属爆炸半径；`BIConversationService` 的「管线后、SqlQueryBuilder 前」是天然隔离缝；进入该区域的改动 PR 须标注「Golden 爆炸半径变更」。
5. **平台治理角色边界红线**：`platform-admin` 仅是治理角色，**绝不可读取任何租户业务数据**；管理目标记为 `ManagementTargetTenantId`，不是 `EffectiveTenantId` 切换。
6. **统一退出门槛（前端）**：RCL/Web/MAUI(Win) 干净构建 0 error + 前端自有 C# 警告清零；权限守卫不得 fail-open；Android 主流程 + iOS Release 编译/裁剪/静态资源验证；Golden 不回归、工作区干净、留存证据。

---

## 2. 里程碑路线总图

| 里程碑 | 主题 | 主要来源 | 关键交付 | 门禁前置 |
|---|---|---|---|---|
| **M0** | 发布阻塞修复（P0 级） | Audit(C/I) + Frontend(S6-1/2) + 附录B | 4 个 404 修复、权限守卫、Ask 可靠性、启动健壮性、编译警告清零、47 变更过门禁提交 | — |
| **M1** | 后端企业能力 Sprint 3 | DevChecklist P1 | SB-P1-01/02/03/04/07（语义与 QueryPlan 重构） | M0 |
| **M2** | 后端企业能力 Sprint 4 | DevChecklist P1 | SB-P1-05/06/08/09/10/11（治理与质量） | M1 |
| **M3** | 后端企业能力 Sprint 5 | DevChecklist P1 | SB-P1-12/13/14/15/16（平台产品化） | M2 |
| **M4** | 架构治理 | DevChecklist P2 + DevPlan A5/A3 | SB-P2-01~10 + 限界上下文解耦 | 与 M1–M3 可并行（A5 先行） |
| **M5** | 初始化数据 + 平台需求基线收口 | Audit(I) + PlatformReq | Migrate/启动、多管理员、凭据外移、演示数据、bootstrap 裁决、§15 验收 | M0（I-1 入 M0） |
| **M6** | 前端发布验收收口 | Frontend S6 | S6-3/4/5/6/7/8/9（功能真实化 + 三端发布证据） | M0 + 依赖 M1/M3/M5 写契约 |
| **M7** | 多语言产品化 | Audit(L) + PlatformReq §5 | 5 语言链路、保存即生效、约定键种子、62 页文案迁移 | M5（L-2/L-3 可提前） |
| **M8** | 平台扩展 P12/P13 | PhaseChecklist | 多数据库连接器 / 多 AI 模型 BYO | M3（连接器端口就绪） |

---

## 3. 各里程碑详细工作项

### M0 · 发布阻塞修复（P0 级，最先执行，门禁前置）

> 目标：消除「功能已实现但不可用/不可发布」的状态偏差。完成且 47 项未提交变更过门禁并提交后，才允许进入候选发布分支。

| ID | 来源 | 优先级 | 任务 | 验收标准 |
|---|---|---|---|---|
| M0-1 | Audit C-1~4 | P0 | 修复 4 个前后端契约 404：`api/agent`→`api/agent/plans`、`tools-catalog`→`tools`、`api/business-model/entities/{id}`（补 GET 路由）、`api/semantic-labels/{id}`（补 GET 路由） | Agent 列表/工具、业务实体详情、语义标签详情页正常加载；双端路由一致 |
| M0-2 | Frontend S6-1 | P0 | 权限守卫 fail-open 修复：`AppState` 增独立 `PermissionsLoaded`/加载失败态；`AuthStore.ValidateAsync` 明确写入；`NavMenu`/`PermissionGuard` 加载前等待、加载后严格判断 | 空权限用户看不到受限菜单，直连显示 403；加载失败不放行；新增 bUnit：未加载/空权限/有权限/All/Any |
| M0-3 | Frontend S6-2 | P0 | Ask 异常与会话恢复：`DoAsk` 加 try/catch/finally + 超时/取消/防重复提交；会话单独保存数据源 ID 并恢复后校验授权列表 | 断网/超时/401 后 Busy 必恢复；历史轮次与原数据源一致；失效数据源有安全回退 |
| M0-4 | Audit I-1 | P0 | 启动健壮性：`Program.cs` 增加 `Migrate()`（或受控迁移工具）；`PlatformAdminBootstrapper.EnsureAsync()`（Program.cs:274）加 try/catch；内部 `SingleAsync` 异常不再导致进程崩溃 | 全新数据库可启动并完成种子；DB 不可达时优雅失败不崩栈 |
| M0-5 | Frontend 附录B | P1 | 编译警告清零：CS4014（`Dashboards.Create` 未 await `OpenCreate`）、CS8602（`Ask.ApplyOutcome` 空引用）、CS0414（`BusinessModel._loading` 未用） | RCL/Web/MAUI(Win) 0 error 且前端自有 C# 警告 0 |
| M0-6 | 全局 | P0 | **47 项未提交变更过门禁**：停服后干净重建，复跑 Golden 18/18 + 全量 426 测试，通过后提交 | 测试零失败、工作区干净、留存构建/测试证据 |

### M1 · 后端企业能力 Sprint 3（语义与 QueryPlan 重构）

| ID | 来源 | 任务 | 核心改造点 | 验收 |
|---|---|---|---|---|
| SB-P1-01 | DevChecklist | Canonical Semantic Model | Entity/Metric/Dimension/Filter/PhysicalBinding 唯一 ID 与定义 | LLM/Metadata/Validator/Dashboard 引用同一语义对象 |
| SB-P1-02 | DevChecklist | 统一字段解析规则 | 消除 QueryUnderstanding/Builder/Validator 三处分叉 | 同一业务术语各阶段解析一致 |
| SB-P1-03 | DevChecklist | QueryPlan Pipeline Stage 化 | 抽象 `IQueryPlanStage`，拆 Metadata/Semantic/Security/Repair/Confidence/Decision | 新增 Stage 无需大改主流程 |
| SB-P1-04 | DevChecklist | 收缩 QueryPlanBuilder 职责 | Builder 只构造，不做语义/权限/安全判断 | Builder 可单测，边界清楚 |
| SB-P1-07 | DevChecklist | Decision Gate 扩展 | bool → `ALLOW/REJECT/ASK_CLARIFICATION/REQUIRE_APPROVAL/LIMITED_EXECUTION` | 低置信度查询不直接执行 |
| M1-Arch | Audit(归档 GQ-006) | 归档能力缺口 | `plans/archive` 记「物料无独立主表导致 `NotResolved`」，须随 P1-01/02 一并解决，不得修改 Ranking Contract 绕过 | 物料类查询可解析 |

### M2 · 后端企业能力 Sprint 4（治理与质量）

| ID | 来源 | 任务 | 验收 |
|---|---|---|---|
| SB-P1-05 | DevChecklist | Column-Level Security | 无权限字段不出现在 QueryPlan/SQL/结果中 |
| SB-P1-06 | DevChecklist | Query Cost Governance | 高风险/高成本查询可拒绝或降级 |
| SB-P1-08 | DevChecklist | AI Decision Audit | 记录 Question/Intent/QueryPlan/Repair/Confidence/Decision/SQL/模型版本，可完整追溯 |
| SB-P1-09 | DevChecklist(接 P0-44) | Ask Cache 完整上下文版本化 | 补 `SemanticVersion`/`MetadataVersion`，统一稳定摘要算法与淘汰；任一维度变化不复用旧缓存，高权限结果不返低权限 |
| SB-P1-10 | DevChecklist | Production Feedback 闭环 | 反馈→Candidate→Review→Baseline→Regression |
| SB-P1-11 | DevChecklist | AI BI E2E 测试 | NL→API→QueryPlan→SQL→Test DB→Result，覆盖单表/多表/权限/租户/错误修复 |

### M3 · 后端企业能力 Sprint 5（平台产品化）

| ID | 来源 | 任务 | 验收 |
|---|---|---|---|
| SB-P1-12 | DevChecklist | Migration/Seed/升级回退体系 | 新环境自动初始化；旧环境升级可验证（含本次 2 个未提交 Migration 纳入说明） |
| SB-P1-13 | DevChecklist | Dashboard 生命周期 | Draft/Version/Publish/Rollback；草稿与发布隔离 |
| SB-P1-14 | DevChecklist | App 生命周期 | Draft/Version/Publish/Rollback/Permission；不直接覆盖线上 |
| SB-P1-15 | DevChecklist | Agent Runtime 基础 | Tool Registry/权限/执行状态/Retry/Approval；Agent 能安全执行受控工具（解 S6-4 Agent 占位） |
| SB-P1-16 | DevChecklist(前置 P0-02) | 多租户成员关系 | `UserTenant` 表 + 切换授权 + `EffectiveTenantId` 真切换 + 前端切换 UI；合法成员可切换，非成员 403，全程审计 |

### M4 · 架构治理（P2 + Stage 1 A5/A3）

> A5（限界上下文解耦）为 A3（项目拆分）前置；A3/A5 用户此前决定暂缓，需确认是否在本周期重启。M4 可与 M1–M3 并行。

| ID | 来源 | 任务 | 验收 |
|---|---|---|---|
| SB-P2-01 | DevChecklist | 拆分 God `ApiClient` 为 BI/Dashboard/App/Agent/Identity/Admin 客户端 | 职责清晰，无巨型类 |
| SB-P2-02 | DevChecklist | 统一 Application/Infrastructure/Api 的 Namespace 与目录边界 | 目录边界一致 |
| SB-P2-03 | DevChecklist | `BusinessTerm` 从字符串升级为强类型语义引用 | 编译期约束业务术语 |
| SB-P2-04 | DevChecklist | 生产 API 与 Internal Diagnostics API 分区 | 诊断接口不污染生产面 |
| SB-P2-05 | DevChecklist | Metrics 扩展 | 增 QueryPlan/LLM/DB latency、Repair/Reject rate |
| SB-P2-06 | DevChecklist | 统一前后端错误码体系 | 前端不再依赖错误字符串 |
| SB-P2-07 | DevChecklist | 多环境配置校验统一化 | Dev/Test/Prod 配置 fail-fast |
| SB-P2-08 | DevChecklist | Web/Blazor UI 关键链路自动化测试 | 登录/Ask/CRUD/权限拒绝 E2E |
| SB-P2-09 | DevChecklist | Dashboard DSL Schema Version 与兼容升级 | 旧 DSL 可加载 |
| SB-P2-10 | DevChecklist | App/Agent DSL Schema Version 与兼容升级 | 旧 DSL 可加载 |
| A5-1~5 | DevPlan Stage1 | 限界上下文解耦（消 `Metadata↔Organization` 环、抽 SharedKernel、合并重复模型/枚举） | 无循环依赖 |
| A3-1~3 | DevPlan Stage1 | 抽 4 个 `.csproj`（Domain/Application/Infrastructure/Api）+ slnx | 每拆一项 build 绿 + Golden 18/18 |

### M5 · 初始化数据 + 平台需求基线收口

| ID | 来源 | 任务 | 验收 |
|---|---|---|---|
| I-2 | Audit I-2 | 支持多平台管理员 | 新增「平台级新增管理员」端点/页面；解除 `platform-admin` 经 API 授予的限制（同步改 31 权限断言与 seed） |
| I-3 | Audit I-3 | 初始化补 Email 字段 | `PlatformBootstrapRequest` 增 Email；前端校验；`CreateAsync` 落库 |
| I-5 | Audit I-5 | 凭据外移 | DB/MySQL/LLM 密钥移出 `appsettings.json` 入库（环境变量/密钥管理/Secret）；清理历史明文 |
| I-6 | Audit I-6 | 默认主题种子 | `Themes` 表 seed 默认主题记录 |
| I-7 | Audit I-7 | 语言目录种子时序 | `TenantManagementController.Create` 对空 `UiLanguages` 的校验兜底 |
| I-10 | Audit I-10 | 演示数据策略（待裁决） | 可一键装入 demo 租户/数据源/语义模型/仪表盘，避免全新平台空页 |
| PR-§2 | PlatformReq | 租户自注册（可选） | 平台管理员可开启租户自注册 |
| PR-§3.3 | PlatformReq | 分范围治理 | 管理员—租户显式授权关系（非前端隐藏菜单），服务端校验 + 审计 |
| PR-§6 | PlatformReq | 重定向不丢 Authorization 头 | 核 `HttpClientHandler.AllowAutoRedirect` |
| PR-§15 | PlatformReq | 13 条验收执行并留证 | 后端编译/Web·Components 编译/自动化测试/迁移应用/初始化闭环/创建租户+首位管理员/语言来自 DB/默认语言与用户缓存/文本继承覆盖恢复/数据源创建授权列表与 Ask 一致/Column·Semantic·Vector 跳转真实详情/全部查询通过租户隔离与权限测试/常见桌面与窄屏无双列菜单溢出 |
| DEC-1 | Audit(治理冲突) | **bootstrap 治理冲突裁决** | 明确 Loopback 匿名初始化是否被接受为「受控部署」、生产是否禁用 `api/platform-bootstrap`，书面记录 + 回归测试 |

### M6 · 前端发布验收收口（P11.7 S6）

| ID | 来源 | 优先级 | 任务 | 依赖 | 验收 |
|---|---|---|---|---|---|
| S6-3 | Frontend | P1 | 导出能力标准化 | — | Excel 真 `.xlsx` 或 UI 明标兼容；下载失败 Toast；中文/数字/日期/空值验证 |
| S6-4 | Frontend | P1 | 清除占位操作 | M1(SB-P1-15)/M3/M5 | Agent 新建/运行、BusinessModel 新建、ThemeEditor 保存接真实端点；无端点则禁用并标注依赖 |
| S6-5 | Frontend | P1 | 干净构建 + 移动端发布验证 | M0-5 | RCL/Web/MAUI Win 0 error + 前端 warning 0；Android 主流程；iOS Release 编译/裁剪/静态资源 |
| S6-6 | Frontend | P1 | E2E/无障碍/视觉回归 | — | Playwright（登录/Ask/CRUD/权限拒绝/窄屏菜单）+ axe + 991/560 截图基线 |
| S6-7 | Frontend | P2 | 性能与图表交互真实性 | — | 真虚拟化或服务端分页；Chart.js `update()`；下钻名实一致 |
| S6-8 | Frontend | P2 | 生命周期与安全加固 | — | `MainLayout` 解除事件订阅、避免 `async void`；CSP、短期令牌/刷新策略文档 |
| S6-9 | Frontend | P2 | 后端依赖补齐 | M1/M3/M5 | Localization 写端点、Quota 策略维护端点、缺失权限码、Agent/BusinessModel/Theme 写契约文档化；前端不再保留无法闭环入口 |

### M7 · 多语言产品化（i18n）

| ID | 来源 | 任务 | 验收 |
|---|---|---|---|
| L-2/L-3 | Audit L-2/3 | 5 语言链路（去 AuthController 硬编码） | `AuthController.cs:139` 去除 `is "zh-CN" or "en-US"`；登录前后语言一致 |
| L-1 | Audit L-1 | 译文保存即生效 | `Localization.razor` 保存后触发 `L10n` 重载 |
| L-5 | Audit L-5 | 约定键种子 | 补 `Nav.*`/`Nav.Group.*`/`Page.Title.*`/`Page.Desc.*`/`Common.*`/`Accessibility.*`/`Theme.*`；`NavMenuItems` 改登记稳定短码键 |
| L-4 | Audit L-4 | 62 页文案迁移到 `L10n` | 分批抽资源键，每批跑 UI 冒烟 |
| L-7 | Audit L-7 | 复用 `PlatformStrings` 已有译文 | 补齐 zh-TW/ja-JP/ko-KR 语言包（非塞英文） |
| L-6 | Audit L-6 | `theme.light/dark` 入数据库基线 | 非中英文界面不再显示中文 |
| L-9 | Audit L-9 | `UiTextResource` 加 `HasQueryFilter` | 租户隔离纵深防御 |
| L-10 | Audit L-10 | 匿名端点种子写入收敛 | `public/texts` 不泄露指定租户专属文案 |
| L-11 | Audit L-11 | `localization:*` 权限码 + 菜单门禁 | 同步改 31 权限断言与 seed |
| L-14 | Audit L-14 | 合并 `PlatformStrings` 与 `UiTextResources` 键空间 | 运行期错误文案可经管理 UI 维护 |
| PR-§5 | PlatformReq | 用户语言偏好服务端持久化 | 换设备/清缓存不丢失 |

### M8 · 平台扩展 P12/P13

| ID | 来源 | 任务 | 验收 |
|---|---|---|---|
| P12 | PhaseChecklist | 多类型数据库连接器 | `IDataSourceConnector`（连接测试/列举表字段/执行归一化）+ `DataSourceConnectionFactory` 注册分支 + `IDataSourceMetadataReader`；遵守 D14「禁止硬编码业务表/字段」；独立确定性离线测试 |
| P13 | PhaseChecklist | 多 AI 模型 BYO | `ILLMProvider` 端口（`QwenService` 降为其一实现）+ `ModelCatalog` + `UserModelBinding`/`TenantModelBinding`（**AEAD 加密存储 API Key**）+ 日志/审计强制脱敏 |

---

## 4. 统一状态追踪表（全部工作项）

> 状态：⬜ 待办 ｜ 🟡 进行中 ｜ ✅ 完成 ｜ ⛔ 受外部阻塞 ｜ 🔴 新发现阻塞

### 后端（DevChecklist_Final）
| ID | 模块 | 状态 | 归属里程碑 |
|---|---|---|---|
| SB-P0-01~11 | 安全与运行时 | ✅ 已闭环（411/411 + Golden 18/18） | 已完成 |
| SB-P1-01~04,07 | 语义/QueryPlan | ⬜ | M1 |
| SB-P1-05,06,08~11 | 治理/质量 | ⬜ | M2 |
| SB-P1-12~16 | 平台产品化 | ⬜ | M3 |
| SB-P2-01~10 | 架构治理 | ⬜ | M4 |
| A5-1~5 / A3-1~3 | 限界上下文/项目拆分 | ⬜（用户暂缓） | M4（待裁决） |

### 前端（Frontend_DevChecklist P11.7）
| ID | 主题 | 状态 | 归属里程碑 |
|---|---|---|---|
| S0-1~8 | 结构与基建 | ✅ | 已完成 |
| S1-1~5 | 组件化（含占位残留） | 🟡 | 已完成+S6-4 |
| S2-1~6 | 写操作闭环 | ✅（S2-7 ⬜ 后端缺端点） | M5/S6-9 |
| S3-1~5 | Ask 体验（边界待整改） | 🟡 | S3-2/3/4 → M0-3/S6-3/S6-7 |
| S4-1~4 | 权限治理（守卫缺陷） | 🟡 | S4-2 → M0-2 |
| S5-1~6 | 质量可观测（发布验证未闭环） | 🟡 | S5-2/3/4/6 → M6 |
| S6-1 | 权限守卫 fail-open | ⬜ P0 | M0-2 |
| S6-2 | Ask 异常与会话恢复 | ⬜ P0 | M0-3 |
| S6-3 | 导出标准化 | ⬜ P1 | M6 |
| S6-4 | 清除占位操作 | ⬜ P1 | M6（依赖 M1/M3/M5） |
| S6-5 | 干净构建+移动端 | ⬜ P1 | M0-5/M6 |
| S6-6 | E2E/无障碍/视觉 | ⬜ P1 | M6 |
| S6-7 | 性能/图表真实性 | ⬜ P2 | M6 |
| S6-8 | 生命周期/安全加固 | ⬜ P2 | M6 |
| S6-9 | 后端依赖补齐 | ⛔ | M6（依赖后端） |

### 审计发现（Audit_Report_and_DevPlan）
| ID | 主题 | 状态 | 归属里程碑 |
|---|---|---|---|
| C-1~4 | 前后端契约 404 | 🔴 新发现阻塞 | M0-1 |
| I-1 | 启动崩溃 | 🔴 新发现阻塞 | M0-4 |
| I-2 | 多平台管理员禁建 | ⬜ | M5 |
| I-3 | 初始化缺 Email | ⬜ | M5 |
| I-5 | 明文凭据入库 | ⬜ | M5 |
| I-6 | 默认主题缺种子 | ⬜ | M5 |
| I-7 | 语言种子时序 | ⬜ | M5 |
| I-10 | 无演示数据 | ⬜（待裁决） | M5 |
| L-1~14 | 多语言 14 项 | ⬜ | M7 |

### 平台需求基线（Platform_Product_Development_Requirements）
| 项 | 状态 | 归属里程碑 |
|---|---|---|
| §1 完整闭环（占位违反） | 🟡 | M6(S6-4) |
| §2 断链禁项（空页） | 🟡 | M5(I-10) |
| §3.1 初始化缺 Email | 🟡 | M5(I-3) |
| §3.2 多平台管理员 | 🟡 | M5(I-2) |
| §3.3 分范围治理 | ⬜ | M5 |
| §5 多语言体系 | 🔴 | M7 |
| §6 重定向不丢 Authorization 头 | 🟡 | M5(PR-§6) |
| §15 测试验收 13 条 | ⬜ 无记录 | M5(PR-§15) |

---

## 5. 关键决策点（需用户/产品裁决）

1. **bootstrap 治理冲突**（DEC-1）：Loopback 匿名初始化是否被接受为「受控部署」？生产是否禁用 `api/platform-bootstrap`？（`P0-11` vs 需求 §3.1 直接冲突）
2. **多平台管理员**：是否支持？支持则须补端点/页面并解除 `platform-admin` 授予限制（I-2）。
3. **演示数据策略**：是否提供一键 demo 数据（I-10），影响首次体验与 §15 验收。
4. **i18n 工作量优先级**：62 页文案迁移巨大，是否接受分批 + 先补齐核心导航/菜单/按钮键（L-5 优先）。
5. **A3/A5 架构治理**：是否在本周期重启（M4），或继续暂缓（阻塞 SB-P2 多条）。
6. **P12/P13 排期**：是否纳入本周期（M8），还是后续独立规划。

---

## 6. 提交与门禁节奏建议

- **每个里程碑结束即跑门禁**（Golden 18/18 + 全量 426 测试）并提交，保持工作区干净。
- **M0 为硬前置**：未过 M0 门禁不得进入候选发布分支。
- **安全/多租户/缓存相关改动**须按 DevChecklist 约束 1–4 补充针对性回归测试（无泄露、无副作用、拒绝已入账、缓存隔离）。
- **先加测试再改实现**（门控隔离）。
- 每个里程碑保留构建/测试/回归证据（参考 Frontend_DevChecklist 附录 C 证据清单）。

---

*本计划为整合主计划，不含任何代码改动（当前为 Agent 模式「先计划」）。所有编号（M0-*/SB-P1-*/SB-P2-*/S6-*/C-*/I-*/L-*/PR-§*）均可映射回对应源码与源文档，便于批次立项与验收。源文档保留作细节溯源。*
