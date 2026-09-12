# 2026-09 Production Readiness Implementation Plan

> Status: ACTIVE  
> Created: 2026-09-12  
> Updated: 2026-09-12  
> Master: ../../Master_Development_Plan.md  
> Backlog: ../../Development_Backlog.md  
> Milestone: M13  
> Evidence: ../../ops/release-evidence.md

## Goal

将当前 master 从"核心功能已较完整、但仍存在安全/CI/语义/部署发布阻塞"推进到 G0 内部候选版本和 G1 企业试点版本。

## 任务 ID 约定

本计划**统一引用 Backlog ID**，里程碑编号只作映射，不另立编号体系。

| 本计划段落 | Backlog ID | 里程碑 | 门禁 |
|---|---|---|---|
| A0 | BASE-01 | M13-01 | G0 |
| A1 | AUTH-01 | M13-02 | G0 |
| A2 | BI-01 | M13-06 | G0 |
| A3 | CI-01 / CI-02 | M13-05 | G0 |
| A4 | SEC-01 | M13-03 | G0 |
| A5 | DB-01 | M13-04 | G0 |
| A6 | AGENT-01 | M13-07 | G0 |
| B1 | DB-02 | M13-17 | G1 |
| B2 | QUOTA-01 | M13-18 | G1 |
| B3 | E2E-01 | M13-09 | G1 |
| B4 | AGENT-02~04 | M13-08 | G1 后 / 条件 |
| B5 | OBS-01 / DR-01 / PERF-01 | M13-11 / M13-10 / M13-12 | G1 |
| B6 | ONBOARD-01 / CACHE-01 | M13-14 / M13-16 | G1 |
| C1 | SEC-02 / APP-01 | M13-13 / M13-15 | 条件门禁 |

## 门禁任务表

### G0 — 内部候选版（必做）

| Backlog ID | 里程碑 | 必做 | 阻塞性质 |
|---|---|---|---|
| BASE-01 | M13-01 | ✅ | 其余任务前提 |
| AUTH-01 | M13-02 | ✅ | 鉴权越权 |
| BI-01 | M13-06 | ✅ | 核心评估失败 |
| CI-01 | M13-05 | ✅ | 测试契约错误 |
| CI-02 | M13-05 | ✅ | 无全量测试门禁 |
| SEC-01 | M13-03 | ✅ | 密钥明文写入 |
| DB-01 | M13-04 | ✅ | 缺表启动 |
| AGENT-01 | M13-07 | ✅ | 伪成功 |

### G1 — 企业试点（必做 = G0 + 下表）

| Backlog ID | 里程碑 | 必做 | 备注 |
|---|---|---|---|
| DB-02 | M13-17 | ✅ | EF 全局筛选器/必填导航一致性 |
| QUOTA-01 | M13-18 | ✅ | 仅原子性；全路径归 M14-03 |
| E2E-01 | M13-09 | ✅ | 真实浏览器业务链 |
| DR-01 | M13-10 | ✅ | 完整恢复演练 |
| OBS-01 | M13-11 | ✅ | 指标与告警 |
| PERF-01 | M13-12 | ✅ | 容量与成本基线 |
| ONBOARD-01 | M13-14 | ✅ | 首次接入引导 |
| CACHE-01 | M13-16 | ✅ | 撤权/重启/双实例 |
| M14-01 | M14 | ✅ | 试点产品包，依赖 BASE-01 |
| M14-07 | M14 | ✅ | G1 验收前须完成**可用**交接包（非草稿） |
| M14-08a | M14 | ✅ | 仅测量方案；效果评估为 M14-08b，显式 G1 后 |

### 条件门禁（客户明确需要时升级为必做）

| Backlog ID | 里程碑 | 触发条件 |
|---|---|---|
| SEC-02 | M13-13 | 客户需要列权限自助管理 UI |
| APP-01 | M13-15 | 客户需要自定义组件进入 App DSL |
| AGENT-02~04 | M13-08 | 客户签约要求 Agent 真实执行（Query→Dashboard→Report） |
| M10-01~03 | M10 | SSO / 多模型 / 其他数据库写入合同 |

**G1 关键路径**：`BASE-01 → G0 → M13-09 → M13-14 → M14-07 → G1 验收`。M13-14 依赖 M13-09，M14-07 依赖 M13-14，故 M14-07 位于 G1 最长串行链末端，**不可作为可延后的软兜底项**。

## Global Constraints

- 继续在 `master` 上执行，不创建新的开发分支。
- 每个缺陷先建立失败测试或可重复证据，再修复。
- 每个任务独立提交，禁止把权限、BI、Migration、UI 混在同一提交。
- P0 未清零前不扩 M10/M14 大功能。
- Golden 不能通过删用例、跳过或降低阈值变绿。
- 安全拒绝必须验证没有数据副作用。
- 数据库改动必须验证空库与已有库升级。
- 所有敏感凭据禁止明文持久化或日志泄漏。
- 基线允许记录失败；**基线的作用是如实记录起点，不要求修复前先全绿**。

## Phase A — G0

### A0 BASE-01 — 固定候选版本与发布证据基线
- 记录提交 SHA、工作树差异、应用与依赖版本、迁移集合、实际配置来源（不记密钥）。
- 核对测试执行/失败/跳过数、Golden 与真实接口冒烟、程序集迁移与数据库实况。
- 产出 `docs/ops/release-evidence.md` 记录；旧门槛（431/476/1015 等）不得作为当前基线。
- 进行中的未提交改动不计入发布版本。
- 登录恢复必须有实际 API/UI 证据才可关闭事故。

### A1 AUTH-01 — Identity management authorization
- 写 viewer/member 直接调用管理 API 的失败测试。
- 要求 `identity:manage`；补齐**逐接口**操作矩阵（所需权限、租户边界、自查例外）。
- 验证跨租户拒绝，以及 403 前后 Users/UserRoles/RolePermissions/SecurityStamp 无变化。
- 核对其他管理控制器同类入口。
- 跑 Identity tests → full unit tests → CI。

### A2 BI-01 — GQ-007
- 固定 runtime fixture；输出 Top-N semantic candidate evidence。
- 分离 recall/ranking/gate 根因；增加反向回归；最小修复。
- Golden 18/18 + controller runtime smoke。

### A3 CI-01 / CI-02
- CI-01：修正 GQ-007 workflow 第二指标断言为**入库单数量**（现为"入库金额"）。
  **关闭 CI-01 不等于整体门禁转绿**：CI-01 是被 BI-01 遮挡的独立契约缺陷，BI-01 未闭前 CI 保持失败（属预期，不视为回归）。
- CI-02：增加完整 `SuperBuilder_AI.Tests` 作业；检查执行数 > 0 且 failure = 0；上传 TRX。
- 明确**必须执行的测试工程、关键用例、允许跳过的白名单范围与失败传播路径**（"执行数大于零"不足以证明全量已运行）。
- 顺序：Build → Unit → Runtime/E2E。

### A4 SEC-01 — DataSource secrets
- 复用 `AesGcmSecretStore`；统一 create/update/pretest/factory/scan/query 的 secret resolution。
- 存量明文幂等升级；错 key/tag 拒绝且不泄漏；API/日志不返回 plaintext。
- 补充**凭据兼容契约**：新旧格式识别、密钥版本、所有读写入口、转换中断后的重跑方式，以及**旧程序能否读取转换后的数据**（兼容窗口判定标准）。

### A5 DB-01 — Migration readiness
- `SchemaProbe` 比较 applied/pending migrations；pending > 0 时 readiness = false。
- 生产不隐式自动迁移。
- 补充**未就绪时的行为契约**：可访问端点白名单（存活/健康检查）、必须拒绝的业务请求、返回的错误码与响应体、升级完成后如何恢复。
- 覆盖不可达、空库、少一条迁移、完全同步、迁移失败。

### A6 AGENT-01 — Pending tool gate
- Tool Registry、Planner、Runtime 三层统一能力状态；Pending/Unavailable 不可被选择或执行。
- 直接构造 DSL 仍拒绝；UI 不显示伪成功。

## Phase B — G1

### B1 DB-02（M13-17）
消除 EF 10622 required-navigation/global-filter 风险并建立 tenant isolation tests。

### B2 QUOTA-01（M13-18）
改为数据库原子额度消费，覆盖最后一份额度并发争抢、失败回滚和幂等。
**边界**：本项只做原子性与并发正确性；全业务路径强制、周期重置、存量配额与周期调用量语义归 M14-03。
执行前先定义**配额计数规则**：存量配额 vs 周期调用量的区分，以及占用、释放、失败、重试、重置行为。

### B3 E2E-01（M13-09）
新增真实 Playwright：Login/refresh；DataSource pretest/save/grant/scan；Ask clarification；Dashboard/App drag-save-reload-publish；RLS 查询结果变化。

### B4 AGENT-02~04（M13-08）— G1 后 / 条件
按 Query → Dashboard → Report 顺序逐项接真实 Backend。
**默认排在 G1 之后**；客户明确签约需求时转为条件门禁。Forecast/Alert/Workflow 在接通前继续禁用。

### B5 OBS-01 / DR-01 / PERF-01（M13-11 / M13-10 / M13-12）
形成监控、恢复和容量的企业试点证据。
PERF-01 与 DR-01 执行前需先锁定**性能与恢复目标**（数据规模、并发、P95、错误率、允许数据损失、RTO）。

### B6 ONBOARD-01 / CACHE-01（M13-14 / M13-16）
完成首次接入引导与撤权/重启/双实例一致性验证。

### B7 试点前置（M14-01 / M14-07 / M14-08a）
在 G1 验收前完成：试点产品包与验收数据集（M14-01）、**可用**支持与客户交接包（M14-07）、试点测量方案（M14-08a）。
M14-07 的服务目标须取自 M13-10/12 的实测结果，不得先于实测定稿。

## Phase C — Release

### G0
上表 G0 必做项全部 DONE，CI 全绿，无 P0。

### G1
G0 + 上表 G1 必做项全部满足。

### 条件门禁
按客户签约范围逐项升级为必做。

### After G1
才进入 M10 企业扩展、M14-08b 效果评估与 M14 商业化主体。

## 第一批执行顺序

1. **DOC-00** 文档对账（状态规则、ID 映射、失效引用）；
2. **BASE-01** 基线与复现（实跑测试 + 迁移与数据库实况）；
3. **AUTH-01** 权限修复；
4. **CI-01 → CI-02** CI 契约及主单测门禁（接受 CI 在此期间保持红色）；
5. **BI-01** GQ-007 语义修复（CI 转绿）；
6. **SEC-01** 凭据保护；
7. **DB-01** 迁移就绪；
8. **AGENT-01** Agent 禁用；
9. **G0 复验**。

## 待决事项（OPEN）

| 决策 | 需要明确的内容 | 最晚确认时间 | 状态 |
|---|---|---|---|
| 首批交付范围 | 沿用"企业 Web 试点"（已定）；是否含 Agent、移动端、自定义组件、列权限 UI | G1 任务排期前 | 部分已定（试点范围已定，Agent 默认 G1 后） |
| 支持环境 | 部署 OS、单/双实例、支持的数据源类型、已验证到连接/扫描/查询哪一层 | 对应集成验证前 | OPEN |
| 试点业务口径 | 业务场景、指标计算方式、参考结果确认人 | Golden/业务验收标准调整前 | OPEN |
| 性能和恢复目标 | 数据规模、并发、响应时间、错误率、允许数据损失与恢复时间 | PERF-01 / DR-01 前 | OPEN |
| 密钥与升级安排 | 密钥注入与保管位置、存量转换是否允许维护窗口、失败如何恢复 | SEC-01 方案定稿前 | OPEN |

## Commit Convention

建议：

- `docs(governance): ...`
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

任务状态只在 [Development_Backlog.md](../../Development_Backlog.md) 更新。本文只描述执行顺序与门禁映射，不复制测试数字和易过期的 SHA。
