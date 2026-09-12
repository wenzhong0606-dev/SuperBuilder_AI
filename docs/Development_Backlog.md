# SuperBuilder AI Development Backlog

> Status: ACTIVE  
> Last Updated: 2026-09-12  
> Authority: 未完成任务唯一总账；完成状态必须以源码、测试、CI 或验收证据为准。

状态（任务状态，定义见 [docs/README.md](README.md#状态规范)）：`BACKLOG`（未开工）/ `ACTIVE`（已开工）/ `DONE` / `OBSOLETE` / `DEFERRED`。
优先级（P0~P3）是排期属性，不是任务状态；本文件只维护任务状态，里程碑状态见 MDP §3.1。

## P0 — 当前发布阻塞

| ID | 来源 | 工作项 | 状态 | Milestone | 完成条件 |
|---|---|---|---|---|---|
| DOC-00 | 文档治理 | docs 结构、状态规则、Backlog↔里程碑任务映射对账 | BACKLOG | —（BASE-01 前置） | 形成唯一主线与归档规则；任务 ID 无孤立项、无失效章节引用 |
| BASE-01 | M13-01 | 固定候选版本与发布证据基线（代码/环境/测试/迁移/运行证据） | BACKLOG | M13-01 | 同一候选 SHA 对应完整证据清单并落 `docs/ops/release-evidence.md`；旧的 431/1015 等门槛不再作为当前基线 |
| AUTH-01 | 审计/M13-02 | Identity 管理 API 强制 `identity:manage` | BACKLOG | M13-02 | viewer/member 直接 HTTP 403 且无副作用 |
| BI-01 | CI/M13-06 | GQ-007 多指标 SemanticApplicabilityGate | BACKLOG | M13-06 | runtime smoke + Golden 全绿 |
| CI-01 | CI | 修正 GQ-007 YAML 将第二指标错写为"入库金额"（契约应为"入库单数量"） | BACKLOG | M13-05 | CI 断言与 Golden 契约一致；**本项关闭不等于整体门禁转绿**，BI-01 未闭前 CI 保持失败（属预期） |
| CI-02 | M13-05 | CI 执行全部 Unit Tests | BACKLOG | M13-05 | 非零测试数、0 failure、TRX 可追溯 |
| SEC-01 | M13-03 | DataSource ConnectionString AEAD 加密 | BACKLOG | M13-03 | 新旧数据均不明文持久化 |
| DB-01 | M13-04 | SchemaProbe 检查 pending migrations | BACKLOG | M13-04 | 缺迁移时 readiness=false |
| AGENT-01 | M7-12/M13-07 | Pending Agent Tool 在 planner/runtime 同时禁用 | BACKLOG | M13-07 | 构造 DSL 也无法执行 pending tool |

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
