# 2026-09 Production Readiness Implementation Plan

> Status: ACTIVE  
> Created: 2026-09-12  
> Master: ../../Master_Development_Plan.md  
> Backlog: ../../Development_Backlog.md  
> Milestone: M13

## Goal

将当前 master 从“核心功能已较完整、但仍存在安全/CI/语义/部署发布阻塞”推进到 G0 内部候选版本和 G1 企业试点版本。

## Global Constraints

- 继续在 `master` 上执行，不创建新的开发分支。
- 每个缺陷先建立失败测试或可重复证据，再修复。
- 每个任务独立提交，禁止把权限、BI、Migration、UI 混在同一提交。
- P0 未清零前不扩 M10/M14 大功能。
- Golden 不能通过删用例、跳过或降低阈值变绿。
- 安全拒绝必须验证没有数据副作用。
- 数据库改动必须验证空库与已有库升级。
- 所有敏感凭据禁止明文持久化或日志泄漏。

## Phase A — G0 P0

### A1 AUTH-01 — Identity management authorization
- 写 viewer/member 直接调用管理 API 的失败测试。
- 要求 `identity:manage`。
- 验证跨租户拒绝。
- 验证 403 前后 Users/UserRoles/RolePermissions/SecurityStamp 无变化。
- 跑 Identity tests → full unit tests → CI。

### A2 BI-01 — GQ-007
- 固定 runtime fixture。
- 输出 Top-N semantic candidate evidence。
- 分离 recall/ranking/gate 根因。
- 增加反向回归。
- 最小修复。
- Golden 18/18 + controller runtime smoke。

### A3 CI-01/CI-02
- 修复 GQ-007 workflow 错误业务断言。
- 增加完整 `SuperBuilder_AI.Tests` job。
- 检查执行数 > 0、failure = 0。
- 上传 TRX。
- Build → Unit → Runtime/E2E。

### A4 SEC-01 — DataSource secrets
- 复用 `AesGcmSecretStore`。
- 统一 create/update/pretest/factory/scan/query secret resolution。
- 存量明文幂等升级。
- 错 key/tag 拒绝且不泄漏。
- API/日志不返回 plaintext。

### A5 DB-01 — Migration readiness
- `SchemaProbe` 比较 applied/pending migrations。
- pending > 0 时 readiness=false。
- 生产不隐式自动迁移。
- 覆盖不可达、空库、少一条迁移、完全同步、迁移失败。

### A6 AGENT-01 — Pending tool gate
- Tool Registry、Planner、Runtime 三层统一能力状态。
- Pending/Unavailable 不可被选择或执行。
- 直接构造 DSL 仍拒绝。
- UI 不显示伪成功。

## Phase B — G1 Stability

### B1 DB-02
消除 EF 10622 required-navigation/global-filter 风险并建立 tenant isolation tests。

### B2 QUOTA-01
改为数据库原子额度消费，覆盖最后一份额度并发争抢、失败回滚和幂等。

### B3 E2E-01
新增真实 Playwright：
- Login/refresh；
- DataSource pretest/save/grant/scan；
- Ask clarification；
- Dashboard/App drag-save-reload-publish；
- RLS 查询结果变化。

### B4 AGENT-02~04
按 Query → Dashboard → Report 顺序逐项接真实 Backend。Forecast/Alert/Workflow 未完成前继续禁用。

### B5 OBS-01 / DR-01 / PERF-01
形成监控、恢复和容量的企业试点证据。

### B6 SEC-02 / ONBOARD-01 / APP-01 / CACHE-01
完成 M13 剩余产品化闭环和条件门禁。

## Phase C — Release

### G0
所有 Phase A 项目 DONE，CI 全绿，无 P0。

### G1
G0 + Phase B 企业试点门禁全部满足。

### After G1
才进入 M10 企业扩展和 M14 商业化主体。

## Commit Convention

建议：

- `fix(auth): enforce identity manage permission`
- `fix(bi): restore GQ-007 semantic resolution`
- `fix(ci): align GQ-007 runtime contract`
- `ci(test): add full unit-test release gate`
- `feat(security): encrypt datasource credentials`
- `fix(startup): block readiness on pending migrations`
- `fix(agent): reject pending backend tools`
- `fix(data): align tenant query filters`
- `fix(quota): make quota consumption atomic`
- `test(e2e): cover core production flows`

## Progress Tracking

任务状态只在 [Development_Backlog.md](../../Development_Backlog.md) 更新。本文只描述执行顺序，不复制测试数字和易过期的 SHA。
