# 数据源扫描能力优化 — 最终方案（v2 整合版）

> 整合对象：我的初版 7 点方案（扫描体验 + 失败处理）与用户提交的「后台任务系统」分析（12 节，含权限/安全/并发/一致性/可观测/回滚）。
> 现状核对依据（已读源码）：`MetadataScanJob.cs`、`ScanTelemetry.cs`、`MetadataScanHostedService.cs`、`MetadataController.cs`、`MetadataScannerService.cs`、`DataSourceDetail.razor`、`PermissionCodes.cs`、`IdentityConstants.cs`、`SuperBIContext.cs`、`AskCacheVersionProviders.cs`。
> 本文档**取代** `2026-09-datasource-scan-optimization.md` 成为最终方案。

---

## 0. 两份方案差异分析

### 0.1 一句话结论

用户 12 节里**约 60% 的骨架后端已具备**（`MetadataScanJob` 任务化、`ScanTelemetry` 富进度、`ScanProgressEvent` 带 `Level`、阶段化、前端 1.5s 轮询、`ScanAsync` 全程 `ct` 可取消）。真正**净新增**且初版 7 点**缺失**的是：元数据版本隔离、并发去重/上限、细粒度权限与审计、删除三档 + 影响分析、持久化表级失败记录、扫描范围（库/schema）选择。本方案=「初版 7 点（就地增强）」+「用户 12 节中净新增的治理能力（补地基）」。

### 0.2 差异矩阵

| 用户提案 | 初版 7 点 | 现状（代码证据） | 处置 |
|---|---|---|---|
| 一、任务模型 `DataSourceScanJob` | R1 状态机 | **已存在** `MetadataScanJob`（Queued/Running/Succeeded/Failed，含 `ProgressDetailsJson`、`TriggeredBy`） | ⚠️**修正**：扩展现有实体，不新建平行实体 |
| 一、状态 `Retrying`/`PartiallySucceeded`/`Cancelling`/`Cancelled` | R1(+R6) | 枚举仅 4 态，缺 4 态 | 采纳，并入枚举 |
| 二、11 阶段 + 每阶段计数/错误 | R3 阶段 stepper | `Stage` 字符串 + `ScanProgressDetails`（6 阶段粒度：Connecting/DiscoveringTables/SyncingMetadata/GeneratingSemantics/IndexingVectors/Finalizing） | 后端 6 阶段映射用户 11 微步；UI stepper 可显子步 |
| 三、软取消 | R1 | 后端仅 `stoppingToken`（进程关），无按任务取消 | **采纳**，对齐 R1 |
| 四、页面恢复续显 | R2 | `OnAfterRenderAsync` 不加载进行中任务 | **采纳**，对齐 R2 |
| 五、扫描日志 + 配色级别 | R7 | `ScanProgressEvent.Level`=Info/Warning/Error（**无 Success/Debug**） | 扩展 Level + 配色，对齐 R7 |
| 六、表级失败记录 + 重试策略 | R6 | 仅顶层 `ErrorCode/ErrorMessage` + 向量重试1次；无逐项记录 | **增强**：新增持久化失败项表（用户比初版更结构化） |
| 七、数据库/schema 选择 + 排除 | R4(口径歧义) | `ScanAsync` 无 scope 参数，整库扫 | **净新增**，重定义 R4 |
| 八、删除三档 + 影响分析 | R5(仅硬删) | 无 `DELETE` 端点；`DataSource→MetadataScanJob` 已级联 | **采纳三档**，升级 R5 |
| 九、版本隔离 + 安全回滚 | —（初版未提） | 仅 `IMetadataVersionProvider`（Ask 缓存版本号），无扫描期版本隔离 | **净新增（最重）**，见 §8 |
| 十、并发控制 | —（初版未提） | `MetadataScanHostedService` 单 `ExecuteAsync` 循环 → **全局串行 cap=1**；无同源去重 | **净新增**，但现实是串行，见 §9 |
| 十一、权限与审计 | —（初版仅提权限守卫） | `metadata:view/edit/scan` 存在；无 cancel/delete/cleanup；无审计表 | **净新增**，对齐现有 `metadata:*` 命名空间 |
| 十二、页面最终形态（区域+按钮） | R3/R7 | 详情页已有基础监控 | 采纳区域/按钮清单，整合 R3/R7 |

### 0.3 关键修正（避免偏差）

1. **不新建 `DataSourceScanJob`**：后端已有 `MetadataScanJob`，新建平行实体会分裂状态机与队列。统一扩展之。
2. **权限命名对齐 `metadata:*`**：用户写 `datasource:*`，但项目约定为 `metadata:scan` 等。新增 `metadata:cancel_scan` / `metadata:delete` / `metadata:cleanup_metadata`，沿用 `IdentityConstants.Permissions` 注册。
3. **并发现实**：当前后端**全局串行**（单 worker 循环）。用户「不同源并行 cap 2-3」是**增强项**，非现状缺口；第一阶段先做「同源去重 + 拒绝重复」即可，并行 worker 列为可选 Phase 3。
4. **版本隔离分轻/重两档**：轻档=扫描完成才 `BumpMetadataVersion()` 使 Ask 缓存失效 + 取消/失败不触活（改动小）；重档=staging 行 + active 指针切换（改动大）。先轻后重。
5. **失败记录持久化**：初版把 `FailedItems` 放 `ProgressDetailsJson`；用户要求可独立查询/重试，故**新增 `MetadataScanJobFailure` 表**（与 JSON 冗余但可检索），重试 UI 直接读库。

### 0.4 合并后净新增范围（初版 7 点之外的增量）

- 元数据版本隔离（轻档先行）
- 并发去重 + 全局上限（串行→可配置）
- 细粒度权限（`cancel_scan`/`delete`/`cleanup`）+ 审计表
- 删除三档（禁用 / 删配置留元数据 / 删配置+清元数据）+ 影响分析
- 持久化表级失败记录 + 重试元信息（`retryCount`/`firstFailedAt`/`lastFailedAt`）
- 扫描范围选择（库/schema 包含排除、排除系统库/空表/前缀、视图开关）

---

## 1. 任务模型（EXTEND `MetadataScanJob`）

**枚举** `MetadataScanJobStatus` 扩为 8 态：
`Queued` / `Running` / `Cancelling` / `Cancelled` / `Succeeded` / `PartiallySucceeded` / `Retrying` / `Failed`。

**字段扩展**（`MetadataScanJob`）：
- `CancelledAt?`、`ActivatedVersion?`（轻档版本指针）、`ScopeJson?`（本次扫描范围快照）。
- 失败项**不**堆在 Job 上，单列 `MetadataScanJobFailure` 表（FK + 索引 `IX_JobId`）。

**新增实体** `MetadataScanJobFailure`：
`JobId / DataSourceId / Database / Schema / TableName / Stage / ErrorType / ErrorMessage(脱敏) / RetryCount / FirstFailedAt / LastFailedAt / Resolved(bool)`。

**审计** `AuditLog`（或复用既有审计表）记录：scan 发起 / cancel / retry-failed / delete / cleanup，含 `Actor`、`DataSourceId`、`JobId`、`Summary`。

---

## 2. 扫描范围与阶段

**范围参数** `ScanScope`（新增，`ScanAsync` 加参）：
- `Databases`/`Schemas`（include 列表，空=全部）、`ExcludeSystemDbs=true`、`ExcludeEmptyTables`、`ExcludePrefixes[]`、`ScanViews(bool)`。
- 前端「扫描范围选择」区：扫描全部 / 选中库(schema) / 排除系统库 / 排除空表 / 排除前缀 / 视图开关。
- 默认排除：`information_schema`、`mysql`、`performance_schema`、`sys`、SQL Server 系统库、临时表。

**阶段映射**（后端 6 阶段覆盖用户 11 微步，UI stepper 可显子步）：
`Connecting`(读配置+测连) → `DiscoveringDatabases`(发现库/schema) → `DiscoveringTables`(表/列/PK·索引·FK) → `Sampling`(可选采样) → `SyncingMetadata`(写元数据+生成搜索文本) → `GeneratingSemantics` → `IndexingVectors` → `Finalizing`(写摘要+激活版本)。
每阶段在 `ScanTelemetry` 记录 `StartedAt/FinishedAt/Status/Succeeded/Failed/CurrentObject/ErrorSummary`。

---

## 3. 软取消（对齐 R1 + 用户三）

- 端点 `POST …/scan/{jobId}/cancel`（权限 `metadata:cancel_scan`）。
- `MetadataScanHostedService` 维护 `ConcurrentDictionary<long, CancellationTokenSource> _jobCts`；`ProcessJobAsync` 用 `linked(stoppingToken, jobCts)` 传 `ScanAsync`。
- 用户点取消 → `Cancelling` + `_jobCts.Cancel()`；`ScanAsync` 在阶段/每批表/每表边界 `ct.ThrowIfCancellationRequested()` → 安全点到退出 → `Cancelled`（保留已落库元数据，不触活新版本）。
- `catch(OperationCanceledException)` 且 `Status==Cancelling` → `Cancelled`；`catch(Exception)` → `Failed`。

---

## 4. 页面恢复续显（对齐 R2）

- 端点 `GET …/scan/latest?dataSourceId=` 返最新非终态任务（复用 `GetScanJob` 体）。
- `DataSourceDetail.OnAfterRenderAsync→Load()` 增 `LoadLatestScanAsync()`：有非终态 → 设 `_scanJob` + 续轮询（**不重复发起**）。
- 列表 `DataSources.razor` 行内对 Running/Queued 显「扫描中」徽章 + 迷你进度，点击直达续看。

---

## 5. 失败重试与表级记录（增强 R6）

- `ScanRetryPolicy`：`MaxAttempts=3`；连接失败**快速失败不重试**；权限失败**不自动重试**（提示用户处理）；表/列/向量抖动可重试。
- 单表耗尽重试 → 写 `MetadataScanJobFailure`（脱敏）+ 继续后续表；仅关键阶段（连接/发现表）失败才 `throw→Failed`。
- 终态：有失败项但元数据已产出 → `PartiallySucceeded`；自动重试中 → `Retrying`（UI 横幅）。
- 重试 UI：详情页「仅重扫失败项」/「重扫当前阶段」/「全量重扫」/「跳过失败项并完成」，均新建任务（同源去重见 §9）。

---

## 6. 日志与配色（对齐 R7 + 用户五）

- `ScanProgressEvent.Level` 扩 `Success`/`Debug`；映射配色：蓝=进行中、绿=成功、黄=警告/跳过/部分成功、红=失败、灰=等待/取消、深色强调=需用户处理。
- RCL 语义文本色变量 `--sb-text-ok/warn/err/info/key`；关键数字 `<strong class=hl->`；无障碍配图标/文字标签（不只靠色）。

---

## 7. 删除三档 + 影响分析（升级 R5 + 用户八）

端点 `DELETE /api/data-sources/{id}`（权限 `metadata:delete`），按 `mode` 分档：
1. **禁用** `?mode=disable`：置 `Enabled=false`，配置+元数据保留，Ask 不可用。
2. **删除配置** `?mode=config`：删连接配置，**元数据标记不可用/只读保留**（历史）。
3. **清理元数据** `?mode=cleanup`（权限 `metadata:cleanup_metadata`，**二次确认**）：删数据源+MetadataTables+Columns+Semantics+向量+ScanJobs+授权+策略+缓存。
- 守卫：Running/Queued 任务 → 409；`mode=cleanup` 前查依赖（Ask 快照 / 仪表盘 / 应用）→ 返回影响清单供确认。
- 级联已配置：`DataSource→MetadataScanJob` 级联；须确认 `DataSource→MetadataTables` 是否级联，否则显式有序删。

---

## 8. 版本与一致性（净新增，最重）

**轻档（Phase 1 落地）**：
- 扫描全程写**当前元数据表**（保持现状），但：取消/失败**不**调 `BumpMetadataVersion()`；仅 `Succeeded`/`PartiallySucceeded` 完成后 `BumpMetadataVersion()` 使 Ask 缓存失效。
- 半写入风险由「同源单扫 + 取消在安全点退出 + 失败项跳过不回滚已成功表」控制 → 可接受「部分可见」，重扫即修复。

**重档（Phase 3 可选）**：
- 引入 `MetadataVersion`（active 指针）：扫描写 `Version=staging` 行，完成并校验后切 `ActiveVersion`；旧版本继续服务 Ask，失败/取消不污染 active。
- 成本：MetadataTables/Columns/Semantics/向量须带 `Version` 键或 staging 表，改动面大 → 列为 Phase 3 决策项，**先轻后重**。

---

## 9. 并发控制（净新增，现实=串行）

- **Phase 1（必做）同源去重**：发起扫描前查该 `DataSourceId` 是否已有 Queued/Running → 有则返回 409 + 当前 jobId，前端提示「查看当前任务 / 取消后重扫」。
- **全局上限**：当前 `HostedService` 单循环=cap 1。若需「不同源并行 cap 2-3」，Phase 3 将单循环改为 N 个 worker 循环（`Parallel`/`Task.WhenAll` 受 `SemaphoreSlim(_globalCap)` 限流）。默认保持串行亦可（简单稳健）。

---

## 10. 权限与审计（净新增）

- 新增权限（注册进 `IdentityConstants.Permissions`，i18n 四处一致）：
  `metadata:cancel_scan`（取消）、`metadata:delete`（删除/禁用）、`metadata:cleanup_metadata`（清理元数据）。
  复用 `metadata:view`/`metadata:edit`/`metadata:scan`。
- 所有扫描/取消/重试/删除/清理端点加 `HasPermissionAsync` 守卫。
- `AuditLog` 落：谁发起/取消/重试/删除/清理 + 摘要（表数、失败数）。

---

## 11. 页面最终形态（区域 + 按钮）

区域：① 数据源基础信息 ② 连接状态 ③ 扫描范围选择 ④ 当前任务卡片 ⑤ 阶段进度时间线 ⑥ 表扫描进度 ⑦ 失败项列表 ⑧ 扫描日志 ⑨ 操作区。
按钮（按权限显隐）：测试连接 / 开始扫描 / 取消扫描 / 重扫失败项 / 全量重扫 / 禁用 / 删除 / 清理元数据。

---

## 12. 接口 / 数据模型变更清单（合并）

- 枚举 `MetadataScanJobStatus`：+4 态。
- 实体 `MetadataScanJob`：+`CancelledAt?`/`ActivatedVersion?`/`ScopeJson?`。
- 新实体 `MetadataScanJobFailure`（表 + 索引）。
- `ScanProgressDetails`：+`FailedItems`(冗余镜像) / `ThroughputPerMin` / `DiscoveredTables/Columns` / `RetryAttempts`。
- `ScanProgressEvent.Level`：+`Success`/`Debug`。
- `ScanAsync` 签名：+`ScanScope scope`。
- 新端点：`POST …/scan/{jobId}/cancel`、`GET …/scan/latest`、`DELETE /api/data-sources/{id}?mode=`、`POST …/scan/{jobId}/retry-failed`。
- `HostedService`：`_jobCts` 注册表 + 同源去重（或放 controller 守卫）。
- 权限：`metadata:cancel_scan`/`metadata:delete`/`metadata:cleanup_metadata`。
- 审计：`AuditLog` 扫描相关动作。

---

## 13. 实施分期（对齐用户推荐顺序，落地「地基优先」）

**Phase 1（地基，必做）**
1. 任务持久化扩展：枚举 +4 态 + `MetadataScanJobFailure` 表 + 迁移。
2. 软取消（§3）+ 状态恢复续显（§4）+ `latest` 端点。
3. 阶段日志升级（§2 阶段映射 + §6 Level 扩展 + 配色）。
4. 主页面提示优化（§11 区域 + stepper + 高亮）。

**Phase 2（韧性）**
5. 表级失败记录（§5）+ `ScanRetryPolicy` + 自动重试 + 失败项重扫 UI + `PartiallySucceeded`/`Retrying`。
6. 错误高亮（§6 收尾）+ 失败项列表区。

**Phase 3（治理，决策后做）**
7. 扫描范围选择（§2 `ScanScope` + 排除系统库/空表/前缀 + 视图开关）。
8. 元数据版本激活（§8 轻档先行，重档评估）。
9. 删除三档 + 影响分析（§7）。
10. 并发并行 worker（§9）+ 权限细化 + 审计（§10）。

---

## 14. 风险与待确认

- **版本隔离成本**：重档改动面大，建议 Phase 1 先用轻档（完成才 Bump 版本），重档待 Phase 3 决策。
- **取消半写入**：轻档下取消可能留部分元数据，靠「重扫修复」；若需原子需阶段事务（评估大扫描成本）。
- **删除级联**：须确认 `DataSource→MetadataTables` 级联策略，决定显式删还是靠级联；保留 RLS/学习保护。
- **并发并行**：默认串行稳健；并行 worker 仅 Phase 3 按需。
- **i18n**：所有新增中文串走四处一致护栏（Keys.cs/ResourceKeys.cs/LocalizationSeedService.cs/razor），新增即补种子。
- **脱敏**：失败项错误同 `FailJobAsync.Sanitize`，禁含连接串/SQL。

## 15. 测试补强

- 单测：`MetadataScanJob` 状态机（含 Cancelling→Cancelled / PartiallySucceeded）、`ScanRetryPolicy`、同源去重、范围排除、`Sanitize` 脱敏。
- 集成：`MetadataController` 取消/续接/latest/delete 三档/retry-failed。
- E2E：`DataSourceDetail` 取消→续进→失败项重扫→删除影响分析。
- Golden：若触及以上契约，按护栏补。
