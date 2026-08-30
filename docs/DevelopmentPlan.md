# SuperBuilder AI Native Low-code BI Platform — 主开发计划（单一事实来源）

> 本文件是项目**唯一事实来源**，整合了：
> 1. 已有的工程治理计划（`docs/ARCHITECTURE.md` 的 Clean Architecture + DDD 重构）
> 2. 用户给出的 10 阶段产品演进路线（Phase 2.7 → Phase 10）
>
> 两套分类法已在 §0 统一为 **三阶段脊柱（Stage 0 / Stage 1 / Stage 2）**，杜绝 Phase 编号冲突。
> 任何后续讨论、计划、代码提交都以此文件的阶段命名为准。

---

## 0. 阶段分类法统一（重要：解决编号冲突）

历史上存在**两个 Phase 3**：
- 旧工程计划里的 "Phase 3 = Business Entity / Golden Runtime + CSV Fixture" → **已完成**（即 Stage 0.3）。
- 用户路线图里的 "Phase 3 = Business Semantic Model" → **真正的下一步**（即 Stage 2 / P3）。

为避免混淆，统一采用下面的**主阶段脊柱**：

| 主阶段 | 名称 | 对应旧/外部命名 | 状态 |
|---|---|---|---|
| **Stage 0** | 基础与 AI Native BI 运行时（已完成） | 旧 Phase 0 / 1 / 2 / 2.7(D14–D21) / 3.1 | ✅ 完成 |
| **Stage 1** | 架构治理（A-track，工程线，与产品并行） | 旧 Phase 4（4.1–4.5） | 🟡 A1/A2 完成，A3–A5 待做 |
| **Stage 2** | 产品演进（P-track，用户 10 阶段路线去掉已完成的 2.7） | 用户 Phase 3 → Phase 10 | ✅ P3 完成 |

**Stage 2 产品阶段明细（即用户 10 阶段路线，含阶段依赖）：**

| 编号 | 阶段 | 目标 | 依赖 |
|---|---|---|---|
| P3 | Business Semantic Model | 业务语义层 / 业务实体模型化 | Stage 0 |
| P4 | Multi-Tenant Platform Core | 平台级多租户上下文 | P3 |
| P5 | Multi-Language Runtime | 三层（UI/业务语义/AI）本地化 | P4 |
| P6 | Low-code BI Engine | Dashboard DSL / 低代码渲染 | P4 |
| P7 | Multi-Theme / Style Engine | 主题与风格引擎 | P6 |
| P8 | AI App Builder | 自然语言→应用生成 | P6,P7 |
| P9 | AI Agent / Copilot | 工具选择型 Agent | P5,P8 |
| P10 | Enterprise / SaaS | IAM/RBAC/审计/计费/可观测 | P4–P9 |

> **关键顺序纪律（来自用户路线 + 本项目教训）**：
> - 先把 Stage 0 收口（已收口），**不要跳过 P3 直接进 P4**。
> - 每个产品阶段结束都**必须**通过 `Golden 18/18` 回归闸门（见 §5），这是本项目从 Phase 3 回归事故中确立的硬约束。
> - A-track 工程线与 P-track 产品线**并行推进**：A3（拆项目）可在 P3 期间启动（低风险、加固结构）；A4（上帝类拆分）**必须在 P6 之前完成**，否则 `QueryPlan` 会变成几千行万能对象，限制 Low-code 发展（用户原话）。

---

## 1. 当前已完成基线（真实状态 · 截至 2026-08-28）

### 1.1 Stage 0 — 基础与 AI Native BI 运行时 ✅
- **Phase 0 基础架构**：项目骨架、EF Core + SQL Server、Qdrant 向量库、Qwen LLM、多数据库（SQL Server/MySQL/PostgreSQL）支撑。
- **Phase 1 AI BI 查询核心链**：自然语言 → `QueryIntent` → `QueryPlan` → `SqlQuery` → 执行 → `QueryAnswer`。
- **Phase 2 QueryPlan 可靠性（含 2.7）**：校验管线、语义校验、修复管线、置信度/决策、可解释性、DimensionAware。
  - **Phase 2.7 D14–D21 全部完成**（用户原以为 D14 是下一步，实为旧快照）：`docs/plans/archive/D18状态同步-20260827.md` 记 D14–D20 FROZEN、D21 Exit Review PASS、**Phase 2.7 READY TO CLOSE**。
  - **Golden 18/18 达标**：11 positive + 5 negative + 1 ambiguous + 1 unresolved，overall/positive/negative 均 100%，`failedGates=[]`。
- **Phase 3.1 Golden Runtime + CSV Fixture**：`GoldenDatasetRunner`/`GoldenDatasetRuntimeService`/`MetadataCsvFixtureService` 已实现；并从一次回归事故中恢复（根因 `ImportAsync` 全局 `RemoveRange` 误删 WMS golden 租户，已改为仅清自身租户子树并部署，恢复 18/18）。

### 1.2 Stage 1 架构治理 — 已完成部分 ✅
- **A1（旧 4.1）目标结构与文档**：`docs/ARCHITECTURE.md`、`docs/DevelopmentPlan.md`、`src/{Domain,Application,Infrastructure,Api}/` 骨架；删除 `DebugOutputs/*`(38) 与 `QueryRepair*`(2) 死代码；13 份历史计划归档 `docs/plans/archive/`。
- **A2（旧 4.2）单项目内 `src/` 分层物理迁移**：234 个 `.cs` 按 ARCHITECTURE §4 映射 `git mv` 至四层（Domain 79 / Application 109 / Infrastructure 29 / Api 17），**保留命名空间、零逻辑改动**；`dotnet build` 0 error；服务启动 HTTP 200；**Golden 18/18 未破**（expectedOutcome 18/18，failedGates=[]）。

### 1.3 当前代码结构（迁移后，A2 落地）
```
src/
├─ Domain/        (零依赖) SharedKernel / Organization / Metadata / BiQuery / BusinessEntity / Golden
├─ Application/   (→Domain) Ports/ + 各 BC 应用服务与用例 + Common(DTO/Options)
├─ Infrastructure/(→App+Domain) Persistence / Vector / Llm / Database
└─ Api/           (→App) Controllers/(生产) Diagnostics/(非生产) Program.cs Views wwwroot
```
> 旧顶层 `Data/Models/Services/Interfaces/Controllers/Configuration/Infrastructure` 已清空删除。契约数据 `Evaluation/Golden/query-plan-golden-v1.json` 与扫描夹具 `Document/*.csv` 保留在项目根。

---

## 2. 阶段依赖与并行关系

```
Stage 0 (DONE)
   │
   ├──▶ Stage 1 A-track（工程线，并行）──────────────────────────┐
   │       A1 ✅  A2 ✅  A3(拆项目)⬜  A4(上帝类+DI)⬜  A5(BC解耦)⬜  │
   │                                                        │     │
   └──▶ Stage 2 P-track（产品线）                              │     │
           P3 Business Semantic Model ✅             │     │
            │                                                  │     │
            ▼                                                  │     │
           P4 Multi-Tenant Core 🟡 ──┬──▶ P5 Multi-Language ⬜  │     │
            │                       └──▶ P6 Low-code BI ⬜ ──────┘     │
            │                                 │                     │
            │                                 ▼                     │
            │                              P7 Theme 🔵 ──▶ P8 App Builder ⬜
            │                                 │                     │
            └────────────────────────────────┴──▶ P9 Agent ⬜      │
                                                  │                │
                                                  ▼                ▼
                                              P10 Enterprise/SaaS ⬜
```
**并行约束**：
- A3（拆项目）随 P3 启动，不阻塞产品功能。
- **A4（上帝类拆分）必须在 P6 之前完成**（用户硬要求，防止 `QueryPlan` 膨胀）。
- 每个 P 阶段验收含 `Golden 18/18`；A3/A4/A5 每个子步骤也含 build + Golden。

---

## 3. Stage 2 产品演进路线（至完成）—— 各阶段清单

### P3 — Business Semantic Model（✅ 已完成）
**目标**：把"从 SQL 字段猜"升级为"先理解业务实体再生成查询"。建立 `BusinessDomain → BusinessEntity → Attribute/Metric/Dimension/Relationship → PhysicalBinding` 的语义业务模型，AI 提问时先映射到业务实体而非裸表字段。

**范围与关键文件（文件级清单）**

| 动作 | 文件（目标位置） | 职责 / 变更 |
|---|---|---|
| 已有(改) | `src/Domain/BusinessEntity/BusinessEntity.cs` 等 6 个 | 升级为聚合根族；补 `Domain`/`MetricDef`/`DimensionDef` 导航 |
| 新增 | `src/Domain/BusinessEntity/BusinessDomain.cs` | 聚合根：一组业务实体的业务域（如"销售域"） |
| 新增 | `src/Domain/BusinessEntity/BusinessEntityMetricDefinition.cs`、`BusinessEntityDimensionDefinition.cs` | 指标/维度定义值对象 |
| 新增 | `src/Application/BusinessEntity/IBusinessEntityRepository.cs` | 端口（A4 依赖倒置铺垫） |
| 新增 | `src/Application/BusinessEntity/BusinessEntityRegistryService.cs` | 应用服务：注册/发现/校验业务实体 |
| 新增 | `src/Application/BusinessEntity/BusinessSemanticMappingService.cs` | 承接现有 `PhysicalBindingResolver`，把 `Metadata` 映射为业务实体语义 |
| 改 | `src/Application/BI/Planning/Intent/QueryIntentNormalizer.cs` | 在意图归一化中注入"业务实体感知"（命中业务实体优先于裸字段） |
| 改 | `src/Application/BI/QueryPlanBuilder*.cs`（仅编排层，不膨胀） | 解析时优先走 BusinessEntity 语义路径 |
| 新增 | `src/Infrastructure/Persistence/Configurations/BusinessDomainConfiguration.cs` + 新 Migration | 持久化新实体 |
| 新增 | `src/Api/Controllers/BusinessModelController.cs`（生产） | 业务模型 CRUD / 自动识别触发 |
| 改 | `src/Api/Diagnostics/*BusinessEntity*Controller.cs` | 已存在诊断控制器，收敛路由前缀 |

**验收（P3 退出门槛）**
- [ ] `dotnet build` 0 error
- [ ] 新业务语义查询（如"今年华东区销售额最高的 10 个客户"）经 `BusinessEntity` 解析，而非裸 SQL 字段
- [ ] **Golden 18/18 仍 PASS**（原有 case 不受影响）；可选新增 1–2 个 business-entity positive case
- [ ] 新增 Migration 可 `dotnet ef database update` 成功

---

### P4 — Multi-Tenant Platform Core

> ✅ **P4 已完成（全绿）**：P4.1 平台上下文抽象 ✅；**P4.2 Tenant 扩展 + Migration ✅（Golden 18/18 PASS）**；**P4.3 全局租户过滤 + 跨租户单测 ✅（xunit 4/4 通过 + Golden 18/18 PASS）**；**P4.4 核心 Runtime 入口 PlatformContext 化 ✅（UnderstandAsync 入口由 `long tenantId` 迁移为 `PlatformContext`，Golden 无租户重载不变；切换可用模型 qwen-plus 后 Golden 18/18 PASS）**。
**目标**：把 `Tenant` 升级为平台第一层运行时上下文 `TenantContext`，任何 Business Data / Metadata / Semantic / Dashboard / AI Memory 都具备 Tenant Scope。

**关键交付**
- [x] `PlatformContext`（聚合：Tenant / User / Workspace / Locale / Theme）—— 增量建设，P4.1 已落地 `TenantContext` + `PlatformContext` 骨架
- [x] `Tenant`（`src/Domain/Organization/Tenant.cs`，已存在）扩展 `TenantSetting`（通用租户 KV 配置，统一承载 Setting/Locale/Theme/Workspace；细分实体按需；`TenantUser/Role/Permission` 留 P10 IAM）— Migration `20260829090341_P4_2_TenantSettings` 已生成并应用
- [x] 核心 Runtime 入口 `UnderstandAsync` 由 `long tenantId` 迁移为接受 `PlatformContext`（P4.4；`QueryPlanPipeline.RunAsync` 当前签名本就不携带 `tenantId`，其租户隔离已由 P4.3 全局过滤在查询层强制，故无需改动）—— **P4.4 完成（Golden 18/18 PASS）**
- [x] 持久化层全量加 `TenantId` 过滤；`SuperBIContext` 查询自动带租户作用域（P4.3：`HasQueryFilter` + 显式 `ApplyTenantScope`，Golden 路径恒 no-op）
- [x] 新增 `TenantManagementController`（生产）+ Migration（P4.1 落地控制器；P4.2 扩展 settings 端点 + 应用 Migration）

**验收**：build 绿 + **Golden 18/18**（验证加租户隔离后原有 positive 查询未被破坏）+ 跨租户数据不可见性单测。

---

### P5 — Multi-Language Runtime
**目标**：三层本地化（UI 语言 / 业务语义语言 / AI 语言）。语义概念语言无关，`Intent → Semantic Concept → QueryPlan` 不受提问语言影响。

> ✅ **P5 已完成（全绿）**：**P5.1** LocaleContext + PlatformContext 接入（默认语言恒 `zh-CN` 保证零回归）；**P5.2** `SemanticLabel` 多语言标签持久化 + Migration；**P5.3** 五语言 `PlatformStrings` + `SemanticLabelRecallService` 接入检索；**P5.4** `QueryIntentLocaleDirective` 意图语言无关化。**统一手法：多语言能力一律以「非默认语言才启用」门控隔离，默认语言路径行为逐字节不变。** 单测 65/65；Golden 18/18 PASS ×4 轮（P5.1/P5.2/P5.3/P5.4）。
>
> 注：多语言问句验证未改写不可删改的契约文件 `Evaluation/Golden/query-plan-golden-v1.json`，而是新增确定性离线一致性测试（四语言问句解析到同一 `semanticId`）。

**关键交付**
- [x] `Localization` 资源体系（zh-CN/zh-TW/en-US/ja-JP/ko-KR…）—— **P5.3 完成**：`PlatformStrings` 静态资源 + `ILocalizationService.GetString` 按回退链解析（业务语义多语言走数据库 `SemanticLabel`，与 UI 文案职责分离）
- [x] 业务语义多语言标签映射（如 销售额/Sales Amount/売上高/매출액 映射到同一 Semantic Concept）—— **P5.2 完成**：`SemanticLabel`（ConceptType+ConceptId 弱多态）+ 回退链解析 + 全局/租户私有两级作用域
- [x] `LocaleContext` 接入 `PlatformContext`（**P5.1 完成**：`PlatformContext.Locale` 由 `string?` 升级为 `LocaleContext`，新增带 culture 的 `FromTenant` 工厂；回退链策略位于 `ILocalizationService`）
- [x] AI 意图理解语言无关化 —— **P5.4 完成**：`QueryIntentLocaleDirective` 要求模型一律以简体中文输出语义名称，使 `Intent → Semantic Concept → QueryPlan` 与提问语言无关；默认语言时指令为空，提示词逐字节不变
- [ ] 问答/错误/消息多语言

**验收**：build 绿 + **Golden 18/18**（用多语言问句重跑核心 case，语义层不变）+ 语义概念映射测试。

---

### P6 — Low-code BI Engine（前置依赖 **A4 上帝类拆分已完成**）
**目标**：`Dashboard DSL`（JSON 描述 type/query/visualization/layout/style），AI 生成 + 低代码编辑器 + 运行时渲染，底层不存裸 HTML。

**关键交付**
- [x] **P6.1 Dashboard DSL 领域模型 + 持久化** ✅（Domain `DashboardDsl`/`WidgetDsl`/`WidgetQueryDsl` + 持久化实体 `Dashboard` 无 FK；Migration `P6_1_Dashboard`；租户隔离单测 5/5；Golden 18/18 PASS）
- [x] **P6.2 DashboardDSL 序列化与校验** ✅（`IDashboardDslSerializer` + `DashboardDslSerializer` 含「不存裸 HTML」红线拦截；单测 28 项；Golden 18/18 PASS；合计单测 98/98）
- [x] **P6.3 LowcodeRenderer 渲染引擎** ✅（`DashboardLowcodeRenderer` + `QueryPlanWidgetDataResolver` 对照 QueryPlanPipeline 接入取数；纯结构化渲染模型不存 HTML；单测 9 项；Golden 18/18 PASS；合计单测 107/107）
- [x] **P6.4 DashboardController（生产）+ 低代码编辑器前端占位** ✅（`DashboardController` CRUD + 结构化渲染端点 + 编辑器蓝图占位；DI 补注册 `IDashboardDslSerializer`；单测 10 项；Golden 18/18 PASS；合计单测 117/117）
- [ ] `Dashboard / Page / Widget / Chart / Table / KPI / Filter / Text / AI Insight / Query` DSL 类型补全

**验收**：build 绿 + **Golden 18/18** + DSL 渲染冒烟测试。

---

### P7 — Multi-Theme / Style Engine
**目标**：`Theme`（Brand/Color/Typography/Layout/Border/Radius/Shadow/ChartPalette/Component/DashboardTemplate），同一 DSL 不同主题产生不同风格。

**关键交付**：`Theme` 聚合、`ThemeContext` 接入 PlatformContext、Tenant→Theme→Workspace→Dashboard 级联。

**子阶段**：
- [x] **P7.1 Theme 领域聚合 + 持久化 + 内置默认主题** ✅（build 0 error；单测 120/120；Golden 18/18 PASS）
- [x] **P7.2 ThemeContext 接入 PlatformContext + 级联解析服务** ✅（build 0 error；单测 126/126；Golden 18/18 PASS）
- [x] **P7.3 渲染引擎接入主题 + 主题切换渲染测试** ✅（build 0 error；单测 131/131；Golden 18/18 PASS；ThemeRenderMapper 输出 ThemeRenderModel.ColorMap + WidgetStyleRenderModel，同 DSL 两主题 StyleSpec 不同）
- [ ] **P7.4 Theme 管理端点（CRUD + 指派）+ P7 总验收**

**验收**：build 绿 + **Golden 18/18** + 主题切换渲染测试。

---

### P8 — AI App Builder
**目标**：自然语言 → App Intent → App Plan → Page Plan → QueryPlan → Component Plan → Application。用户说"建销售经营分析应用"，AI 自动生成含导航/看板/数据模型/查询/筛选/明细/列表/KPI/助手/权限的应用。

**关键交付**：`AppPlan / PagePlan / ComponentPlan` 模型、`AppBuilderAgent`（编排）、`AppBuilderController`。

**验收**：build 绿 + **Golden 18/18** + 端到端应用生成冒烟。

---

### P9 — AI Agent / Copilot
**目标**：从 `Question → SQL` 升级为 `User → AI Agent → Intent → Plan → Tool Selection`（Metadata/Semantic/Query/Dashboard/Report/Forecast/Alert/Workflow）。

**关键交付**：`AgentPlanner`、`ToolRegistry`、`AgentController`、异常检测/原因分析链路。

**验收**：build 绿 + **Golden 18/18** + Agent 工具选择测试。

---

### P10 — Enterprise / SaaS
**目标**：IAM/SSO/RBAC/ABAC/Audit/Billing/Quota/API/Webhook/Workflow/Notification/Data Governance/Security/Observability。

**关键交付**：`Identity`（User/Role/Permission）、`AuditLog`、`Billing/Quota`、`Observability` 中间件、CI 架构依赖校验（NetArchTest）。

**验收**：build 绿 + **Golden 18/18** + 安全/审计基线测试。

---

## 4. Stage 1 架构治理（A-track，工程线，并行）

| 编号 | 内容 | 关键动作 | 状态 |
|---|---|---|---|
| A1 | 目标结构设计与文档/骨架/冗余清理 | `docs/ARCHITECTURE.md` 等、删 40 冗余 | ✅ 完成 |
| A2 | 单项目内 `src/` 分层物理迁移（命名空间保留） | 234 .cs git mv 四层 | ✅ 完成 |
| A3 | 抽取独立项目（Domain/Application/Infrastructure/Api） | 新建 4 个 `.csproj` + slnx 引用；迁移包引用与 `Migrations`；**每拆一个项目 build + Golden** | 🚫 **阻塞：以 A5 为前置**（首次尝试 `1c7ff18` 因既有 `Domain↔Application`、`Application↔Infrastructure` 循环依赖物理不可行，已于 `e87d8b9` 回退为单项目分层） |
| A4 | 上帝类拆分 + 依赖倒置（补 `I*` 端口） | 拆分 `QueryPlanBuilder`(4308 行/8 partial)→ `BusinessTermExtractor`+`TableSelector`+`FieldResolver`+`JoinBuilder`+瘦编排器；`BIConversationService`→`QueryPlanPipeline`；`QueryPlanValidator`/`SemanticApplicabilityEvaluator` 抽纯领域服务；补 `IQueryPlanBuilder` 等端口与 DI | ✅ 完成（`9981d73`；build 0 error；**Golden 18/18 PASS**，期间修复 GQ-010 Filter 数量漂移与 GQ-008 幽灵维度两处回归） |
| A5 | 限界上下文解耦 | 消除 `Metadata↔Organization` 循环；抽 `SharedKernel`；合并 `GoldenBaseline`↔`GoldenBaselinePersistenceRecord`、`QueryPlanSemanticResolution`↔`SemanticApplicabilityResult/*Resolution`；统一三套搜索结果 DTO；统一 `DimensionResolutionType` 枚举 | ⬜ **A3 的前置条件**（须先解除分层循环依赖，再谈多项目物理拆分） |

**A4 拆分后的 QueryPlan 五平面（用户建议，落地目标）**
```
QueryPlan
├─ Intent
├─ SemanticContext
├─ ResolutionPlan
├─ ExecutionPlan
└─ PresentationPlan
```
AI Intent → Semantic Plan → Query Plan → Execution Plan → Visualization Plan。

---

## 5. 统一验收闸门：Full Golden Regression（每个阶段硬约束）

**为什么每个阶段都要带 Golden 18/18**（本项目已用事故证明）：
1. Golden 是唯一能捕捉"编译绿、启动绿，但 AI 语义行为已漂变"的确定性契约。
2. 直接反例：Phase 3 的 `ImportAsync` 全局 `RemoveRange` 改动，构建/启动全绿，却把 18 case 全 BLOCK——只有 Golden 抓得到。
3. 管线非确定性 + 多组件耦合（Qwen 波动/403 限流/GQ-008 幽灵维度 + Qdrant + WMS 快照 + Builder 兜底），Golden 是冻结行为契约。
4. P4/P5 等横切阶段会注入 `Resolution → QueryPlan → SQL → Runtime` 链路，极易静默改变某 positive case 结果。
5. 成本极低：端点 `GET /evaluation/golden-runtime/run` 跑全量 18 case。

**执行规则**
- 每个 **P 阶段** 以 Golden 18/18 为退出门槛。
- 任何触及 `src/Application/BI`、`src/Domain/{Metadata,BiQuery}`、`src/Infrastructure/{Vector,Database}`、Resolution 契约的**提交**都重跑 Golden。
- 纯界面/配置改动可不强制，但阶段结束时必跑。
- 契约文件 `Evaluation/Golden/query-plan-golden-v1.json` **不可删改**（回归基准）。

---

## 6. 风险与回滚
- **构建中断**：分层/命名空间/拆项目易致编译错 → 小步提交、每步编译。
- **Golden 回归**：LLM 实时非确定性（GQ-008 幽灵维度/403 限流抖动）→ 每步跑 Golden，异常即回退该步。
- **A3 拆项目 churn**：236 文件一次性迁移风险高 → A2 已先在单项目内文件夹落地验证，A3 再拆项目。
- **A4 上帝类膨胀**：`QueryPlanBuilder` 已 4308 行 → **强制 A4 在 P6 前完成**。
- **回滚**：所有迁移均为 git 提交，可 `git revert` 到上一绿态；Golden 契约不可删。

---

## 7. 至完成的能力矩阵（目标）
| 能力 | 当前 | 目标 |
|---|---|---|
| Metadata / Semantic Search / Vector / QueryPlan / Query Validation / Dynamic Relation | 🟢 | 🟢 |
| Business Entity | 🟡 | 🟢 (P3) |
| Multi Database | 🟡 | 🟢 (A3+P4) |
| Multi Tenant | 🟡 | 🟢 (P4) |
| Multi Language | 🔴 | 🟢 (P5) |
| Multi Theme | 🔴 | 🟢 (P7) |
| Low-code Dashboard / App | 🟡/🔴 | 🟢 (P6/P8) |
| AI Dashboard / App Generation | 🟡/🔴 | 🟢 (P6/P8) |
| AI Agent | 🟡 | 🟢 (P9) |
| Workflow | 🔴 | 🟢 (P9/P10) |
| RBAC/ABAC | 🔴/🟡 | 🟢 (P10) |
| Audit | 🟡 | 🟢 (P10) |
| SaaS | 🔴 | 🟢 (P10) |

---

## 8. 下一步动作（立即）
1. **启动 P3**（Business Semantic Model）：按 §3 P3 文件级清单落地，每步 build + Golden。
2. **并行启动 A3**（拆独立项目）：低风险加固，不阻塞 P3 功能。
3. 每完成一个子步骤，跑 `GET /evaluation/golden-runtime/run` 确认 18/18。
4. A4 上帝类拆分在 P6 之前必须收口。

> 历史计划文档已归档于 `docs/plans/archive/`，本文件为唯一权威来源。任何阶段编号以 §0 主阶段脊柱为准。
