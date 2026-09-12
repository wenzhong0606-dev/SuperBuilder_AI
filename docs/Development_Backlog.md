# SuperBuilder AI Development Backlog

> Status: ACTIVE  
> Last Updated: 2026-09-12  
> Authority: 未完成任务唯一总账；完成状态必须以源码、测试、CI 或验收证据为准。

状态（任务状态，定义见 [docs/README.md](README.md#状态规范)）：`BACKLOG`（未开工）/ `ACTIVE`（已开工）/ `DONE` / `OBSOLETE` / `DEFERRED`。
优先级（P0~P3）是排期属性，不是任务状态；本文件只维护任务状态，里程碑状态见 MDP §3.1。

## P0 — 当前发布阻塞

| ID | 来源 | 工作项 | 状态 | Milestone | 完成条件 |
|---|---|---|---|---|---|
| DOC-00 | 文档治理 | docs 结构、状态规则、Backlog↔里程碑任务映射对账 | ACTIVE | —（BASE-01 前置） | 治理提交 d7302a7/c1d6714/57681e4 已实现 docs 结构与状态规则、Backlog↔里程碑映射；待最终复核与失效章节引用清零 |
| BASE-01 | M13-01 | 固定候选版本与发布证据基线（代码/环境/测试/迁移/运行证据） | ACTIVE | M13-01 | 已实现（c3a7e2b/d7302a7 发布证据基线，真实实测）；旧的 431/1015 等门槛不再作为当前基线；待 CI 验证 |
| AUTH-01 | 审计/M13-02 | Identity 管理 API 强制 `identity:manage` | ACTIVE | M13-02 | 已实现（c1e3006 强制 identity:manage 门禁于 Identity 管理写接口）；viewer/member 直接 HTTP 403 且无副作用；待 CI 验证 |
| BI-01 | CI/M13-06 | GQ-007 多指标 SemanticApplicabilityGate | ACTIVE | M13-06 | **经 2026-08-29 golden run 实证 `applicabilityState=Resolved`、`queryPlanEvaluationPassed=true`、`decision=PASS`（两指标 入库数量/Sum、入库单数量/Count 均通过）→ 无 runtime BLOCK，gate 实际通过。** 近期 CI 在 GQ-007 step 的红系 CI-01 断言错误（已修 `41e9624`），**BI-01 无代码缺陷**（73ac76d）。关闭条件：CI 复跑 GQ-007 step（C.13.3）绿。 |
| CI-01 | CI | 修正 GQ-007 YAML 将第二指标错写为"入库金额"（契约应为"入库单数量"） | ACTIVE | M13-05 | CI 断言已与 Golden 契约对齐（第二指标改 `入库单数量`，41e9624）；**本项关闭不等于整体门禁转绿**，`:173` 先断言 `.passed=="true"`，BI-01 未闭前 `:173` 即失败、`:176` 永不执行，CI 保持失败（属预期）。待 CI 复跑 |
| CI-02 | M13-05 | CI 执行全部 Unit Tests | BACKLOG | M13-05 | 非零测试数、0 failure、TRX 可追溯 |
| SEC-01 | M13-03 | DataSource ConnectionString AEAD 加密 | ACTIVE | M13-03 | **已实现，待 CI 验证**（5476c99）：复用 `ISecretStore`(AES-256-GCM) 信封加密。写路径 `DataSourcesController`(Create/Update)+ 种子 `DemoDataInstaller`/`MetadataCsvFixtureService` 加密落库；读路径 `DataSourceConnectionFactory`/`MetadataScanHostedService`/`LocalRuntimeDiagnosticsController` 经 `ResolvePlaintext` 解密（兼容遗留明文）。列宽 nvarchar(2048)→nvarchar(max)（迁移 `20260912103000_M13_03`）；新增启动期一次性存量再加密托管服务（幂等）。CI 已注入测试主密钥（`SecretStore__MasterKey`）。旧数据与新数据均不明文持久化。 |
| DB-01 | M13-04 | SchemaProbe 检查 pending migrations | ACTIVE | M13-04 | **已实现，待 CI 验证**（30153f7）：`BootstrapState` 新增 `MigrationsPending`；`SchemaProbe.ProbeAsync` 在「已应用迁移非空」后再查 `GetPendingMigrationsAsync`，存在未应用迁移即返回 `MigrationsPending`（readiness=false，/health=degraded）。CI 启动 API 前已 `dotnet ef database update`（工作流 :100）应用全部迁移，pending=0，不误伤。 |
| AGENT-01 | M7-12/M13-07 | Pending Agent Tool 禁用（runtime 后端闸门） | ACTIVE | M13-07 | **runtime 闸门已实现**（待 CI 验证）：`ITool.BackendStatus`（默认 live，ControlledToolBase 重写为 pending）；`AgentRuntime.ExecuteRunAsync` 在权限/审批闸门后、执行前新增后端状态闸门，pending 工具即使显式授权也拒绝执行，满足"构造 DSL 也无法执行 pending tool"。planner 侧（`ToolRegistry.ResolveFromIntent`）**未**过滤 pending 工具——若过滤将移除全部 analytics 意图编排能力（Query/Dashboard/Forecast/Report/Alert/Workflow 均 pending）并破坏 AgentController/Planner 测试套件；当前由 runtime 闸门兜底，pending 工具仅诚实占位（connected:false）且不可执行。是否严格在 planner 抑制 pending 工具为待定决策（需同步改造 ~15 个用例）。 |

> **G0 编译/构建复核（2026-09-12，run `f85d2af` / `34690533915`）**：8 项 G0 代码已全部实现并合入 `master`；`编译检查` 与 `Web/Blazor E2E` 两个 job **转绿**。`V2.6 Evaluation` job 仍红，但本次失败在 `启动 Qdrant CI 容器`（Docker 基础设施，与代码无关）；前次 run（cc8358c）曾暴露 `GQ-007` 在 `SemanticApplicabilityGate` 阶段 BLOCK（reason：Top Candidate 缺少 Golden SemanticText 直接语义证据）——该评估 gate 不属 G0 代码范围（SEC-01/DB-01/AGENT-01 均未触碰），CI-01 仅修正了断言文案、未消除该 BLOCK，BI-01 关闭需另行排查评估 gate 的语义证据链（非 G0 阻塞项）。
> 复核期间修复了 3 个会致编译/E2E 失败的缺陷（均已合入 master）：SEC-01 迁移 Designer 基类 `ModelSnapshot`→`Migration`（CS0262/CS0246，`c612c6b`）、DB-01 `SchemaProbe` 改用 `Any()/Count()`（CS0117/CS1503，`cc8358c`）、SEC-01 托管服务 `StartsWith` 去掉 `StringComparison` 以可翻译为 SQL（运行时 `InvalidOperationException`，`f85d2af`）。

## P1 — 企业试点稳定性

| ID | 来源 | 工作项 | 状态 | Milestone |
|---|---|---|---|---|
| DB-02 | EF Warning | Required relation + global query filter 一致性 | BACKLOG | M13 |
| QUOTA-01 | M14-03 | Quota 原子消费、并发、幂等。**边界**：本项只做数据库原子性（最后一份额度并发、失败回滚、幂等）；全业务路径强制与周期重置归 M14-03 | BACKLOG | M13（原子性）→ M14-03（全路径） |
| E2E-01 | M13-09 | DataSource→Scan→Ask→Dashboard/App→RLS 浏览器链 | BACKLOG | M13 |
| AGENT-02 | M13-08 | Query Tool 真实后端 | BACKLOG | M13（G1 后；客户明确需要时转条件门禁） |
| AGENT-03 | M13-08 | Dashboard Tool 真实后端 | BACKLOG | M13（G1 后；客户明确需要时转条件门禁） |
| AGENT-04 | M13-08 | Report Tool 真实后端 | BACKLOG | M13（G1 后；客户明确需要时转条件门禁） |
| OBS-01 | M13-11 | Login/Ask/错误/P95/CorrelationId 指标告警 | BACKLOG | M13 |
| DR-01 | M13-10 | SQL + Qdrant + config/secret reference 完整恢复 | BACKLOG | M13 |
| PERF-01 | M13-12 | 容量、P95、错误率、成本、过载恢复 | BACKLOG | M13 |
| SEC-02 | M13-13 | 列级权限管理 UI 与有效权限预览 | BACKLOG | M13（条件门禁：客户需要时） |
| ONBOARD-01 | M13-14 | 空白租户到首个分析成果引导 | BACKLOG | M13 |
| APP-01 | M13-15 | Custom Component Version → App DSL/runtime | BACKLOG | M13（条件门禁：客户需要时） |
| CACHE-01 | M13-16 | 撤权、重启、双实例缓存一致性 | BACKLOG | M13 |

## P2 — 工程治理与扩展

| ID | 来源 | 工作项 | 状态 | Milestone |
|---|---|---|---|---|
| ENV-01 | 审计 | Qdrant compose/CI 版本统一 | BACKLOG | M9 |
| ENV-02 | 审计 | dotnet-ef 与 runtime 版本统一 | BACKLOG | M9 |
| ARCH-01 | M9-11 | SharedKernel 收口 | BACKLOG | M9 |
| ARCH-02 | M9-11 | 重复 DTO/枚举合并 | BACKLOG | M9 |
| DATA-01 | 工程治理 | 统一 IDataSourceConnector | BACKLOG | M10 |
| TENANT-01 | 工程治理 | ITenantContextAccessor 自动化 tenant scope | BACKLOG | M9/M10 |
| M10-01 | M10 | 多数据库连接器 | BACKLOG | M10 |
| M10-02 | M10 | 多模型 BYO | BACKLOG | M10 |
| M10-03 | M10 | 外部 IdP/OIDC | BACKLOG | M10 |

## P3 — 延后能力

来源：`milestones/M11.md` 的 12 项长期清单。编号与 M11.md 一一对应，**不得用占位页面假装已支持**；启用任一项时新建正式里程碑。

| ID | 工作项 | 状态 | Milestone |
|---|---|---|---|
| M11-01 | Distributed Cache | DEFERRED | M11 |
| M11-02 | Message Bus | DEFERRED | M11 |
| M11-03 | Event Sourcing | DEFERRED | M11 |
| M11-04 | Data Lineage | DEFERRED | M11 |
| M11-05 | Data Quality | DEFERRED | M11 |
| M11-06 | Metric Marketplace | DEFERRED | M11 |
| M11-07 | Semantic Graph | DEFERRED | M11 |
| M11-08 | Model Routing | DEFERRED | M11 |
| M11-09 | Prompt Versioning | DEFERRED | M11 |
| M11-10 | AI Cost Attribution | DEFERRED | M11 |
| M11-11 | AI Observability | DEFERRED | M11 |
| M11-12 | Multi-Agent Collaboration | DEFERRED | M11 |

## 商业化 Backlog

| ID | 工作项 | 状态 | Milestone |
|---|---|---|---|
| M14-01 | 试点产品包与验收数据集 | BACKLOG | M14 |
| M14-02 | 套餐权益与租户授权 | BACKLOG | M14 |
| M14-03 | 配额全业务路径强制 | BACKLOG | M14 |
| M14-04 | Usage/AI Cost Ledger | BACKLOG | M14 |
| M14-05 | 项目收费与交付台账 | BACKLOG | M14 |
| M14-06 | 自助订阅支付 | DEFERRED | M14 |
| M14-07 | 支持与客户交接包 | BACKLOG | M14（G1 验收前须就绪） |
| M14-08a | 试点成效测量方案（G1 前） | BACKLOG | M14（G1 前） |
| M14-08b | 试点效果评估与续用结论（显式依赖 G1 + M14-08a） | BACKLOG | M14（G1 后） |

## 关闭规则

一项任务只有在以下证据都满足时才可改为 `DONE`：

1. 实现已进入目标 commit；
2. 对应自动化测试通过；
3. 涉及 CI 的任务在目标 SHA 上通过；
4. 涉及 Migration 的任务有空库/升级证据；
5. 涉及 UI/E2E 的任务有实际浏览器证据；
6. 已知限制已回填；
7. Master/Milestone/Active Plan 状态一致。

## 状态更新规则

- 只有当存在**对应提交、分支或已指派责任人**时才把任务从 `BACKLOG` 改为 `ACTIVE`；仅"已排期"不构成开工。
- 优先级（P0~P3）变更不改变任务状态。
- 任务关闭时同步刷新 MDP §3.1 里程碑状态与 Active Plan 施工顺序。
