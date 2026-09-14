# 可观测性与排障指南（OBS-01 / M13-11）

本指南说明 SuperBuilder AI 的运行期可观测能力：如何通过**关联 ID**、**指标端点**、**告警**快速定位「登录失败 / Ask 异常 / 扫描积压 / 限流 / 跨租户」等问题。

## 1. 关联 ID（Correlation ID）—— 排障主线

每个 HTTP 请求都会被分配或透传一个关联 ID，并作为响应头 `X-Correlation-Id` 返回给调用方。

- **取关联 ID**：客户端读取响应头 `X-Correlation-Id`；服务端日志中每条请求/异常/审计都带该值。
- **透传**：若上游已带 `X-Correlation-Id` 请求头，中间件直接复用，保证跨服务串联。
- **检索链路**：在日志中按 `X-Correlation-Id` 过滤，可串联：
  - `ObservabilityMiddleware` 的 `REQ ...` / `RES ...`（方法、路径、租户上下文、状态码、耗时）；
  - `UnifiedExceptionMiddleware` 的结构化错误（错误码 + 关联 ID）；
  - `AuditMiddleware` 的审计记录（含 `CorrelationId`）。
- **租户上下文**：`REQ` 日志打印 `authTenant / effTenant / reqTenant / switchAuth / mgmtTarget`，用于识别「伪造 `tenantId` 查询参数 / header」是否被正确拒绝（真实生效租户取自令牌，绝不取请求值）。

> 排查「某次 Ask 返回 403 / 空数据」时：先拿响应里的 `X-Correlation-Id`，到日志按该值拉出 `REQ`（看 `effTenant` 是否等于预期租户）与异常日志（看错误码），即可判定是权限拒绝还是租户隔离问题。

## 2. 指标端点 `GET /metrics`

需 `platform:diagnostics:view` 权限。返回：

| 字段 | 含义 |
|---|---|
| `routes[]` | 按路由聚合：请求数、服务端错误数(≥500)、客户端错误数(400–499)、**401 / 403 / 429 细分**、平均/P95/最大耗时、`ErrorRate`、登录路由的 `LoginSuccessRate` |
| `pipelineStages[]` | Ask 管线分段耗时（understand / plan / sql / db / result）的计数、平均、P95、最大 |
| `outcomes[]` | Ask 结果分类计数：`reject` / `earlyReturn` / `repair` / `ask.success` / `ask.failure` / `ask.timeout` / `ask.dbError` / `ask.llmError` |
| `askCache` | Ask 响应缓存命中/未命中/命中率 |
| `scanBacklog` | 待处理扫描任务数（`pending`，即 Queued+Running）与观测峰值 `peak` |
| `activeAlerts` | 当前处于触发态的告警（见 §3） |

### 关键派生口径

- **登录成功率** = `1 − Unauthorized / Count`（仅对路径含 `login` 的路由计算；其它路由 `LoginSuccessRate=1`）。成功数 = 总请求数 − 401 数。
- **Ask 成功率** = `ask.success / (ask.success + ask.failure)`。
- **Ask 失败细分**：`ask.timeout`（捕获 TimeoutException/取消，或总耗时 > 30s 阈值）、`ask.dbError`（EF / ADO.NET / SQL 驱动异常）、`ask.llmError`（理解/结果解析阶段的其它异常，近似归因为 AI 后端）。
- **扫描积压**：`scanBacklog.pending` 持续升高或长期 > 阈值，说明后台扫描处理不过来或卡住（检查 `MetadataScanJobs` 表中 `Queued`/`Running` 任务）。

## 3. 告警（持续失败检测）

`AlertEvaluationService` 每分钟评估一次指标，对照阈值（出厂基线见 `AlertThresholds`）发出告警，并经 `IAlertSink` 分发（默认 `LoggingAlertSink` 仅写日志）。当某规则从触发回落至阈值以下时，**额外发一条「恢复」告警**（信号 `recovered`），保证触发+恢复闭环可观测。

| 规则 | 触发条件 | 级别 |
|---|---|---|
| `route_error_rate_high` | 路由错误率 ≥ 20%（样本 ≥ 20）；≥ 50% 升级 Critical | Warning/Critical |
| `login_failure_high` | 登录失败率 ≥ 30%（样本 ≥ 10） | Warning |
| `ask_failure_high` | Ask 失败率 ≥ 20%（样本 ≥ 20） | Warning |
| `scan_backlog_high` | 待处理扫描任务数 ≥ 5 | Warning |

阈值在 `Program.cs` 以单例 `AlertThresholds` 覆盖（不依赖「性能与恢复目标」OPEN 项——那是 PERF/DR 的验收前提）。

> 注：阈值与「P95 / 错误率 / 失败率」的**生产级目标值**已由 O2 回填（2026-09-14）：可用性 99.5%、Ask P95 ≤8s、实时查询 P95 ≤2s、失败率 ≤1%、并发 ≤20 人、成本 ≤¥0.05（见 `03-operations-runbook.md` §1）。最终定稿须 PERF-01 实测达标（承诺不超过实测能力）；此处为可观测能力的出厂基线。

## 4. 健康探测 `GET /health`

返回 `status`（healthy/degraded）、启动状态 `state`、可诊断原因 `reason`、已完成步骤 `steps`。`degraded` 表示 Schema 缺失 / 种子不完整但仍可运行；用于快速判断「是不是数据库没起来」。
