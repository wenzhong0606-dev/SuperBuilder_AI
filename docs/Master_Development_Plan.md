# SuperBuilder AI Master Development Plan

> Version: v3.0  
> Status: ACTIVE  
> Baseline Date: 2026-09-12  
> Baseline Commit Before Documentation Reconciliation: `f185abf50ea7917970a13dfbc4ab589008d06ac9`  
> Role: 项目唯一战略总账；不再承载所有细粒度施工步骤。

## 1. 文档治理

本文件回答三个问题：

1. 项目总体目标是什么；
2. 各 Milestone 当前处于什么状态；
3. 当前应该执行哪一条开发主线。

细粒度未完成事项统一进入 [Development_Backlog.md](Development_Backlog.md)；当前施工步骤统一进入 [plans/active/](plans/active/)；历史计划和审计仅保留为证据，不得作为当前执行入口。

权威顺序见 [docs/README.md](README.md)。

## 2. 产品目标

SuperBuilder AI 的目标是形成一个可部署、可治理、可扩展、可企业试点并最终可商业交付的多租户 AI 数据分析平台，核心能力包括：

- 平台与租户治理；
- 身份、RBAC、数据源授权、RLS/列级安全；
- 多语言；
- 数据源、元数据、业务实体、语义模型；
- QueryPlan、Ask、Dashboard、App Builder；
- Agent 编排与真实工具执行；
- Web/MAUI 多端；
- CI/CD、迁移、灾备、可观测性和性能治理；
- 套餐、配额、用量、成本和商业交付能力。

## 3. 当前状态

当前代码主体已完成 M0–M8；M9 大部分完成但仍有工程治理尾项；M12 主体已完成。M10 尚未启动，M11 延后；M13 是当前主要执行里程碑，M14 为后续商业化里程碑。

当前已确认的发布阻塞和高风险事项统一记录在 Backlog，不再在本文件重复维护容易过期的缺陷明细。

### 3.1 里程碑总览

| Milestone | 主题 | 状态 | 说明 |
|---|---|---|---|
| M0 | 发布阻塞与安全止血 | COMPLETED | 历史交付保留于 milestone |
| M1 | 字段与数据完整性 | COMPLETED | |
| M2 | 平台初始化与身份治理 | COMPLETED | |
| M3 | 多语言产品化 | COMPLETED | |
| M4 | 数据源与元数据闭环 | COMPLETED | |
| M5 | 语义模型与 QueryPlan 企业化 | COMPLETED | 当前仍有 GQ-007 新回归需 M13 修复 |
| M6 | Ask 企业化 | COMPLETED | |
| M7 | Dashboard/App/Agent | PARTIAL | M7-12 Pending Tool 后端缺口转入 M13-07/08 |
| M8 | 前端体验与多端发布 | COMPLETED | |
| M9 | 架构、测试与运维 | PARTIAL | SharedKernel/重复 DTO 等尾项仍在 Backlog |
| M10 | 企业扩展能力 | BACKLOG | 多库连接器、多模型 BYO、外部 IdP |
| M11 | 长期能力 | DEFERRED | 分布式、血缘、质量、语义图等，待真实需求 |
| M12 | 前端功能闭环与 RBAC 收口 | COMPLETED | 主体及增量已交付 |
| M13 | 产品化收口与企业试点门禁 | ACTIVE | 当前主线 |
| M14 | 商业化交付与经营闭环 | BACKLOG | G1 后启动主体 |

详细定义见 [milestones/](milestones/)。

## 4. 当前开发主线

当前只允许一个 Active Plan：

[2026-09 Production Readiness Plan](plans/active/2026-09-production-readiness.md)

执行顺序（任务 ID 与状态见 [Backlog](Development_Backlog.md)；详表与门禁映射见 Active Plan）：

1. `DOC-00` 文档对账（状态规则、ID 映射、失效章节引用）；
2. `BASE-01` 发布基线与证据（落 `docs/ops/release-evidence.md`）；
3. `AUTH-01` Identity 管理 API 权限；
4. `CI-01` 修正 CI 契约断言（**修断言不等于门禁转绿**，期间 CI 保持失败属预期）；
5. `BI-01` GQ-007 Semantic Applicability 回归（CI 转绿）；
6. `CI-02` 全量 Unit Test Gate；
7. `SEC-01` DataSource 凭据加密；
8. `DB-01` Schema pending migration readiness；
9. `AGENT-01` Agent Pending Tool 诚实禁用；
10. `DB-02` EF Global Query Filter correctness；
11. `QUOTA-01` Quota 原子并发（边界：全业务路径归 M14-03）；
12. `E2E-01` 核心 Playwright E2E；
13. `OBS-01` / `DR-01` / `PERF-01` 观测、恢复、容量；
14. `ONBOARD-01` / `CACHE-01` / `M14-01` / `M14-07` / `M14-08a` 试点前置；
15. G0 → G1 验收；
16. `AGENT-02~04` / `SEC-02` / `APP-01` 按签约范围纳入（条件门禁）；
17. M10、`M14-08b` 与 M14 商业化主体（G2）。

P0 未清零前，不启动 M10、M11 或 M14 大规模功能开发。

## 5. 当前 Release Gates

### G0 — Internal Release Candidate

必须满足：

- Build 通过；
- 全量 Unit Test 实际执行且零失败；
- Golden 18/18；
- Runtime Smoke 通过；
- Identity 权限漏洞关闭；
- DataSource 凭据不再明文落库；
- Schema pending migration 能阻止 readiness；
- Agent Pending Tool 不可被规划/执行；
- 无未处理 P0。

### G1 — Enterprise Pilot

G0 基础上增加：

- 核心 Playwright 真实业务链；
- 完整备份恢复演练；
- 关键服务指标与告警；
- 性能与容量基线；
- 首次接入向导；
- 双实例/撤权/重启一致性验证；
- 试点产品包和支持交接。

### G2 — Commercial Delivery

G1 基础上增加：

- 套餐与权益；
- 原子配额；
- Usage/AI Cost Ledger；
- 项目交付与收费台账；
- 商业验收与支持流程；
- 自助 SaaS 场景才额外要求支付、订阅、退款和对账。

## 6. 工程门禁

- Golden 用例不得通过删除、跳过或降低阈值规避。
- 新 API 必须覆盖未认证、缺权限、错误租户、非法输入和成功路径。
- 安全失败必须验证“无副作用”，不能只断言状态码。
- 数据库变更必须验证空库、已有库升级、重复执行和失败恢复。
- 敏感凭据不得明文持久化、返回客户端或写日志。
- 新用户可见文本必须进入 i18n 体系。
- 业务能力未接真实 Backend 时必须明确禁用，不得伪成功。
- CI 报告必须包含真实执行数，0 test 不得视为成功。
- 文档状态必须由源码/测试/CI 证据反推，不允许只靠人工描述宣布完成。

## 7. 未完成任务与历史资料

- 当前未完成任务：见 [Development_Backlog.md](Development_Backlog.md)。
- 当前执行计划：见 [plans/active/](plans/active/)。
- 发布证据记录：见 [ops/release-evidence.md](ops/release-evidence.md)（`BASE-01` 载体，同一候选 SHA 一份清单）。
- 历史计划：见 [plans/archive/](plans/archive/)。
- 历史审计：见 [audits/archive/](audits/archive/)。

旧计划仍可用于理解设计背景，但不再拥有排期和状态裁决权。
