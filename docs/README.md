# SuperBuilder AI 文档中心

> Status: ACTIVE  
> Last Updated: 2026-09-12

本目录采用“战略总账 → 里程碑 → 当前执行计划 → Backlog → 历史证据”的治理方式，避免旧计划、旧审计和当前代码状态互相冲突。

## 权威顺序

发生冲突时，按以下顺序判断：

1. 当前源码、数据库迁移与自动化测试结果。
2. `docs/Master_Development_Plan.md`。
3. `docs/milestones/M*.md`。
4. `docs/plans/active/` 当前执行计划。
5. `docs/Development_Backlog.md`。
6. 产品、架构、运维规范。
7. `docs/audits/archive/`、`docs/plans/archive/` 与 `.workbuddy/` 历史资料。

历史文档不得覆盖当前源码事实；若历史文档与当前实现冲突，应更新当前 Master/Backlog，而不是按历史结论重复开发。

## 目录

- `Master_Development_Plan.md`：唯一战略总账，回答“项目走到哪、下一阶段是什么”。
- `Development_Backlog.md`：唯一未完成任务总账，回答“还有什么没做”。
- `milestones/`：M0–M14 的里程碑详细定义和历史完成证据。
- `plans/active/`：当前正在实施的计划。
- `plans/archive/`：已完成、已替代或历史计划。
- `audits/archive/`：历史审计快照，不作为当前缺陷清单。
- `product/`：产品需求、试点与产品边界。
- `architecture/`：长期架构规范。
- `specs/`：专项契约与设计说明。
- `ops/`：迁移、备份、恢复、部署、运维证据。
- `api/`：OpenAPI 等 API 产物。

## 状态规范

三类状态**分别定义、不得混用**。判定依据只能是源码、提交、测试或验收证据。

**1. 文档状态** —— 用于 `plans/`、`audits/` 文件级 `Status:` 字段：

- `ACTIVE` / `COMPLETED` / `SUPERSEDED` / `ARCHIVED` / `DEFERRED`

**2. 任务状态** —— 用于 `Development_Backlog.md` 条目：

- `BACKLOG`：已登记但**未开工**
- `ACTIVE`：**已开工**（存在对应提交、分支或已指派责任人）
- `DONE` / `OBSOLETE` / `DEFERRED`

**3. 里程碑状态** —— 用于 `Master_Development_Plan.md` §3.1 与 `milestones/Mn.md`：

- `COMPLETED` / `PARTIAL` / `ACTIVE` / `BACKLOG` / `DEFERRED`

规则：

- 未开工的任务一律 `BACKLOG`；**不得因为「已排期」或「属于当前里程碑」而标 `ACTIVE`**。优先级（P0/P1/P2）是排期属性，不是任务状态。
- 同一任务在 Backlog（任务状态）与 Master（里程碑状态）中的维度不同，允许并存，但不得互相代替。
- 禁止使用"差不多完成""基本完成"等不可验收状态。

## 当前入口

- Master：[`Master_Development_Plan.md`](Master_Development_Plan.md)
- Backlog：[`Development_Backlog.md`](Development_Backlog.md)
- 当前执行计划：[`plans/active/2026-09-production-readiness.md`](plans/active/2026-09-production-readiness.md)
- M13：[`milestones/M13.md`](milestones/M13.md)
- M14：[`milestones/M14.md`](milestones/M14.md)
- 发布证据记录：[`ops/release-evidence.md`](ops/release-evidence.md)（`BASE-01` 载体）
