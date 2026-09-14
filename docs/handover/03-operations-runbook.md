# 03 · 日常运维手册

> 受众：**[运维]** 客户 IT / SRE
> 所属：M14-07 交接包｜状态：**草稿**（SLO 已由 O2 回填目标值，待 PERF-01 实测定稿）

配套：`docs/ops/observability-troubleshooting.md`（排障主线）、`docs/ops/cache-auth-consistency.md`（一致性模型）。

---

## 1. 服务目标（SLO）

> ⚠️ **数值为 O2 回填的目标值（2026-09-14），非最终 SLA。** 最终定稿须 PERF-01/M13-12 实测达标（承诺不超过实测能力，M14-07 验收判据 4）；若实测优于目标则维持目标，劣于目标则调整目标或投入优化，不得静默降级。

| 指标 | 目标值 | 依据 | 状态 |
|---|---|---|---|
| 可用性（月度） | **99.5%**（单实例无 HA，含计划维护窗口） | O2-2.9 | 已回填（待 PERF 确认） |
| Ask 接口 P95 延迟 | **≤ 8 s**（受 LLM 延迟主导） | O2-2.3 | 已回填（待 PERF 确认） |
| 实时查询接口 P95 延迟 | **≤ 2 s**、P99 ≤ 5 s（不含 LLM） | O2-2.4 | 已回填（待 PERF 确认） |
| 请求失败率上限 | **≤ 1%**（不含 429 限流拒绝） | O2-2.5 | 已回填 |
| 并发承载 | **≤ 20 并发用户 / ≤ 50 请求·分钟⁻¹**（单实例） | O2-2.2 | 已回填（待 PERF 确认） |
| 数据丢失窗口 **RPO** | **≤ 24 h**（每日备份；元数据扫描驱动、变化低频） | O2-2.7 / DR-01 | 已回填 |
| 恢复时间 **RTO** | **≤ 30 min**（含发现+准备+还原+业务验证；Qdrant 还原 109s 为主项） | O2-2.8 / DR-01 | 已回填（50k 向量须重算） |
| 每请求成本上限 | **≤ ¥0.05**（Ask 单次，LLM 主导；待 token 实测） | O2-2.6 | 已回填 |

> 上述数值由 O2 回填（2026-09-14）。PERF-01 实测后**必须同步**核对：本表、`04-incident-and-support.md` §3（承诺口径）、`docs/ops/observability-troubleshooting.md` §3（生产级阈值）；承诺不得超过实测能力。

## 2. 巡检

### 每日

| # | 检查 | 命令/入口 | 正常判据 |
|---|---|---|---|
| 1 | 应用健康 | `GET /health` | `status=healthy`、`state=Ready`；`degraded` 时读 `reason` |
| 2 | 关键指标 | `GET /metrics`（需 `platform:diagnostics:view`） | 无 `activeAlerts`；`ErrorRate` 低于目标；`scanBacklog.pending` 未持续升高 |
| 3 | 登录成功率 | `/metrics` → 登录路由 `LoginSuccessRate` | 接近 1（骤降 → 查 `Unauthorized` 细分） |
| 4 | Ask 结果分布 | `/metrics` → `outcomes[]` | `ask.success` 占主体；`ask.dbError`/`ask.llmError`/`ask.timeout` 无异常升高 |
| 5 | 备份任务结果 | 备份编排日志 | 成功；失败 → 按 `05` §5 处置 |
| 6 | 磁盘/资源 | 宿主机监控 | 关系库、Qdrant 存储、日志目录未逼近阈值 |

### 每周

| # | 检查 | 说明 |
|---|---|---|
| 1 | 配额用量趋势 | `GET api/quota`，确认未长期贴顶 |
| 2 | 审计日志积压 | 确认审计写入正常、可查询 |
| 3 | 扫描任务积压 | `scanBacklog.peak` 是否异常 |
| 4 | 令牌/安全戳一致性 | 抽查撤权是否即时生效（改角色后旧令牌应被拒） |
| 5 | 备份可恢复性 | 按 `05` §4 做**抽样校验**（`RESTORE VERIFYONLY`） |
| 6 | 版本与 schema | `verify-schema.ps1` 离线+在线，确认无漂移 |

## 3. 告警与响应

告警规则与出厂阈值见 `docs/ops/observability-troubleshooting.md` §3（出厂基线：路由错误率 ≥20%/≥50%、登录失败率 ≥30%、Ask 失败率 ≥20%、扫描积压 ≥5）。

| 告警 | 首查 | 处置方向 |
|---|---|---|
| `route_error_rate_high` (Warning) | `/metrics` 定位高错误路由 → 取一条响应头 `X-Correlation-Id` 查日志 | 按 `04` §4 定位链路 |
| `route_error_rate_high` (Critical) | 同上 + 宿主资源 | 启动降级/限流预案，必要时回滚 |
| `login_failure_high` | 登录路由 `Unauthorized`；审计日志 | 凭据/账号锁定/暴力破解 |
| `ask_failure_high` | `outcomes[]` 区分 `timeout`/`dbError`/`llmError` | DB 侧查连接与慢查询；LLM 侧查配额与上游状态 |
| `scan_backlog_high` | `MetadataScanJobs` 中 `Queued`/`Running` | 后台服务存活？业务库锁？ |

> 默认告警**只写日志**（`LoggingAlertSink`）。生产接入工单/告警平台需替换 `IAlertSink` 实现——列为交付配置项，见 `04` §3。
> 规则回落时会额外发一条 `recovered` 信号，用于确认告警闭环。

## 4. 例行操作

### 重启应用

1. 确认无进行中的扫描/长事务；
2. 停止应用 → 启动；
3. 校验 `/health` = `healthy`/`Ready`；
4. 说明：重启会**清空进程内缓存与澄清会话**（按策略失效，不产生越权）；令牌为无状态 + 安全戳校验，重启天然一致。

### 轮换密钥

| 密钥 | 影响 | 注意 |
|---|---|---|
| `Auth:SigningKey` | 轮换后**所有已签发令牌立即失效**，用户需重新登录 | 需停机窗口；非开发环境须满足 ≥32 字节 / ≥12 不同字符 |
| `SecretStore:MasterKey` | 影响数据源连接串解密 | **不可直接轮换**：需按「解密→用新密钥重加密」迁移流程，否则连接串不可解。操作前必须备份且验证新密钥包可解 |
| `Qwen:ApiKey` / `Embedding:ApiKey` | 模型调用 | 可热更新（重启生效）；注意向量维度必须仍为 1024 |

### 新增数据源 / 授权

按 `02-customer-config-checklist.md` §2 ②③；授权/撤权**立即生效**（实时查库、无缓存窗口；组变更会轮换成员安全戳）。

### 多实例部署注意

| 项 | 现状 | 要求 |
|---|---|---|
| 限流存储 | 默认 `Memory` | **必须**改共享存储，否则各实例独立计数 |
| Ask 缓存 / 澄清会话 | 进程内 | 不共享；已做租户/用户隔离，最坏退化为未命中。详见 `docs/ops/cache-auth-consistency.md` |
| 双实例支持状态 | O1 已确认：**首批单实例试点，双实例不支持**（列为 CACHE-01 验证项，后续再考虑） | 2026-09-14 O1 回填 |

## 5. 日志与取证

- **排障主线**：响应头 `X-Correlation-Id` → 日志中串联 `REQ`/`RES`、异常、审计记录。详见 `docs/ops/observability-troubleshooting.md` §1。
- **租户上下文**：`REQ` 日志含 `authTenant / effTenant / reqTenant / switchAuth / mgmtTarget`，可判定是否有人伪造 `tenantId` 参数（真实生效租户取自令牌，绝不取请求值）。
- **审计**：变更类操作有审计记录（含 `CorrelationId`）；退出/删除流程的授权记录见 `06` §5。

## 6. 运维台账（交付时填写）

| 项 | 值 |
|---|---|
| 巡检责任人 / 频次 | 【待填写】 |
| 告警接收渠道 | 【待填写】 |
| 备份窗口 | 【待填写】 |
| 备份保留期 | 【待填写】 |
| 变更窗口（可停机时段） | 【待填写】 |
| 升级窗口与流程审批人 | 【待填写】 |
| 值班/升级联系链 | 【待填写】 |
