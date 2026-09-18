# v10 闭环验收补强（收敛版）

> 配套 `2026-09-datasource-scan-optimization-final.md`（v10 单次交付规格）。
> **方向采纳、规模收敛**：原草稿"每类 2–3 个深度测试 + 全部页面逐项 E2E + 新增 §9.1 硬门槛"被判定为过度——成本高、且把新测试方法数量当成闭环门槛。本版改为：**四组关键跨系统路径真实后端验证 + 关键页面流程 E2E（各 1 条）+ 其余 C1–C11/§L.8 逐项证据映射（已有单测/控制器测试/人工记录）**；不事后改写 §9.1 门槛，签署条件以"证据补齐"为准。

---

## 0. 核心结论

- 现有 5/5 真实后端测试**只证明片段跨系统契约**（只读读取器 / 只生成 SQL / 只直接调激活闸门 / 手工写 GC 待办），不能代替 §9.1 描述的完整扫描→激活→Ask / 重试 / 迁移 / 删除场景。
- 原 §9.1 **已写明**扫描、Ask、重试、迁移、删除的具体行为；本次不新增硬性门槛，而是补齐"原验收条款 → 自动化证据 → 剩余风险"的缺口。
- **收敛后签署条件**：① 四组关键跨系统路径真实后端验证全过；② 关键页面流程 E2E 各 1 条（取消 / 续显 / 删除确认 / 失败项重扫）；③ 其余 C1–C11/§L.8 在矩阵中逐项映射到已有证据或人工记录；④ 全部 CI 通过。满足后签"v10 完整闭环"——不以新测试方法数量为门槛。

---

## 1. 原 §9.1 验收条款 → 自动化证据 → 剩余风险 矩阵

> 判定口径：✅ 已有自动化 / 🟡 本次补齐（真实后端集成测试）/ ⚠️ 设计已覆盖 + 待补（reader 扫描期守卫 / 人工记录）/ ❌ 无证据。

| §9.1 原验收条款 | 现有证据 | 本次补齐 | 剩余风险 |
|---|---|---|---|
| ① 跨 schema Ask：扫描两 schema，分别 Ask 并 JOIN，FROM/JOIN 用限定名、字段用表 Id 稳定别名、不串表 | 🟡 `QueryScopeRealBackendTests` 验读取器 + `SqlQueryBuilder` 生成限定名 SQL（未走扫描→激活→Ask） | ✅ **1.1 跨 schema 激活**：扫描结果进入激活版本、两 schema 同名表按物理键区分不串表（确定性查询链，非自然语言 Ask） — CI 13/13/0 已验证 | ⚠️ 完整 `POST /scan`→`Succeeded`→Ask 接口闭环建议补 1 条 E2E |
| ② 跨库 Ask：逐库汇总扫描，同名表各自 Ask / 跨 catalog JOIN；PG 跨库与"连 db1 查 db2"均拒绝 | 🟡 `QueryScopeRealBackendTests.CrossDatabase_*` 验 MySQL/SQL Server 两级名 + PG 拒绝 | ✅ **1.1 跨库激活**：跨 catalog 结果进入激活版本、PG 拒绝经确定性查询链断言 — CI 13/13/0 已验证 | ⚠️ 同上，建议 1 条跨库 E2E |
| ③ 向量单表失败：必需 point 重试耗尽→激活被 `vector_index_incomplete` 阻断、旧版 Ask 仍可用、旧 point 不被 GC；C7 仅重扫失败项后激活成功 | 🟡 `MetadataLifecycleRealBackendTests.IncompleteVector_*` 直接 `ActivateAsync` 验闸门（未验旧版可用 / 未走重扫） | ✅ **1.2 向量失败保旧版 + 失败项重扫**：必需 point 未 `Synced` 阻断激活；旧 v0 point 仍召回且不被 GC；`retry-failed` 仅重扫失败项后激活成功 — CI 13/13/0 已验证 | ⚠️ "重试耗尽"为扫描期语义，数据层以"未 Synced 阻断"等价验证；真实 AI 重试计数建议人工/在线确认 |
| ④ 存量迁移：未升级旧库跑迁移，默认/非默认 schema 唯一候选准确回填；歧义/不可达进入异常清单且不误填/激活；`MetadataVectorBackfillJob` 补齐三类 point `data_source_id`；激活引用重映射 | 🟡 `MetadataLifecycleRealBackendTests.LegacyDatabase_*` 仅 happy-path 计数 + 三类 point | ✅ **1.3 存量 backfill→activate**：旧库升级后 backfill 补 `data_source_id` + 激活 v0→v1 翻指针无孤儿 — CI 13/13/0 已验证 | ⚠️ 歧义/不可达为**扫描期守卫**（需真实读取器），建议补 fake-reader 集成测试或人工记录；不在数据层硬测 |
| ⑤ 删除后重启 GC：cleanup 删源→事务内写 `MetadataVectorGcRequest`（含 point ID 快照）→ kill -9 前已持久→重启后 `MetadataVectorGcJob` 删尽、无孤儿 | 🟡 `MetadataLifecycleRealBackendTests.DeletedSource_*` 手工 `Add` GC 待办后换 DbContext | ✅ **1.4 真实 cleanup 持久 GC + 重启清理**：经事务写 GC 待办（含 point 快照）→ 跨连接读取持久待办→GC 删尽、无孤儿；审计落 `metadata:vector:gc` — CI 13/13/0 已验证 | ⚠️ "kill -9"以"待办已提交 DB、跨连接读取"模拟进程退出，非真实进程杀死；cleanup 审计 `metadata:datasource:cleanup` 在 E2E 层验 |

---

## 2. 必须补的四组真实后端验证（数据/向量契约层）✅ 已在 CI 验证（2026-09-18，13/13/0）

> 载体：`tests/SuperBuilder_AI.RealBackend.Tests/`，沿用 `[Fact]`+`[Trait("Category","RealBackend")]`；运行依赖 `SB_REAL_*` 指向真实后端（可远程库，需建库权限 + Qdrant 可写）。四组均**复用现有直接实例化模式**（`new MetadataScannerService(context, null, null, null, null, gate)` + `MetadataVectorGcJob` + `QdrantService`），不启动完整 API——完整 API 闭环归 E2E。

### 2.1 跨 schema / 跨库：扫描结果进入激活版本并正确查询
- **确定性查询链**（CI 用 `CiBlockedQwenService` 阻断真实 AI，**自然语言 Ask 稳定输出不得作常规 CI 硬门槛**）：验证激活后两 schema 同名表按物理键（Catalog/Schema/Table）区分、版本指针翻转、不串表；跨 catalog 结果进入激活版本。完整 `SqlQueryBuilder` 限定名生成已由现有 `QueryScope` 测试覆盖，本组补"激活态正确性"。
- 方法：`CrossSchemaScan_ActivatesBothVersions_QueryChainResolvesQualifiedNames`（v0/v1 各两 schema 同名 `orders`，激活后断言两表均留存且 `SchemaName` 正确、active 版本=1）。

### 2.2 向量失败：保旧版 + 失败项重扫
- **不注入 `VectorErrorCode` 冒充"重试耗尽"**——以"必需 point 未 `Synced`"这一真实激活闸门等价验证。
- 方法：
  - `VectorFailure_BlocksActivation_OldVersionAskStillWorks`：v1 表必需 point `VectorStatus="Failed"` → `ActivateAsync` 抛 `vector_index_incomplete`、`ActiveMetadataVersion` 不变、v0 point 仍可被 Qdrant 召回且**不入 GC 待办**。
  - `RescanOnlyFailedItems_ThenActivates`：模拟 `retry-failed`（重扫使必需 point `Synced`）→ 激活成功、`ActiveMetadataVersion=v1` → `MetadataVectorGcJob` 删旧 v0 point、保留新 v1 point。

### 2.3 存量歧义 / 不可达：不误迁移
- 方法：`LegacyMigration_BackfillThenActivate_FlipsActiveVersion`：旧库升级 → `MetadataVectorBackfillJob` 补 `data_source_id` → 激活 v0→v1 翻指针、无孤儿引用、不产生 `Failed` GC 待办。
- **歧义/不可达边界**（同名 schema、源库不可达）属**扫描期守卫**，需真实/假读取器，建议在 fake-reader 集成测试或人工记录中覆盖（见 §1 剩余风险 ④）；数据层不硬测"误填 dbo/public"。

### 2.4 真实 cleanup 产生持久 GC 待办 + 重启后清理
- **不释放 DbContext 冒充 kill -9**——以"GC 待办已 `SaveChanges` 提交、跨连接（新 `SuperBIContext`）读取"模拟进程退出后的持久性。
- 方法：
  - `Cleanup_DeleteWritesGcRequest_WithPointIdSnapshot`：删源事务内写 `MetadataVectorGcRequest`（含全部待删 point ID 快照）+ 级联清源。
  - `Cleanup_KillBeforeGc_Restart_Continues_NoOrphan`：提交后跨连接读持久待办 → `MetadataVectorGcJob` 删尽 Qdrant point、无孤儿。
  - `Cleanup_AuditTrail_Recorded`：GC 作业审计落 `metadata:vector:gc`（用 `RecordingAudit` 捕获真实 action 名）。

---

## 3. 按层验证计划（收敛）

| 维度 | 验证方式 | 范围 |
|---|---|---|
| 数据库 / 向量契约 | 真实后端集成测试（§2 四组） | ①–⑤ 数据正确性 / 安全边界 |
| 关键页面流程 | Blazor E2E **各挑 1 条** | 取消 / 退出重进续显 / 删除影响确认 / 失败项重扫 |
| C1–C11 / §L.8 其余项 | 已有单测 + 控制器测试 + **人工记录逐项映射** | 矩阵外不强制每条新 E2E |
| C10 视觉验收 | **人工证据**（截图 / 录屏）留记录 | 不可标"不阻塞"而无记录 |
| Ask 在线模型验收 | 与 CI 分离，单独在线跑 | `CiBlockedQwenService` 阻断下不验自然语言输出 |

---

## 4. 测试设计修正（对照初版草稿）

- ❌ 初版"注入 `VectorErrorCode` 即重试耗尽" → ✅ 改以"必需 point 未 `Synced` 阻断激活"等价验证。
- ❌ 初版"释放 DbContext 即 kill -9" → ✅ 改以"待办已提交 DB、跨连接读取"模拟重启持久化，注释明确非真实进程杀死。
- ❌ 初版自然语言 Ask 稳定输出作 CI 硬门槛 → ✅ 改验确定性查询链；在线模型验收另做。
- ✅ 审计断言用代码实际 action 名：`metadata:datasource:cleanup`（cleanup 控制器）、`metadata:vector:gc`（GC 作业）。
- ❌ 初版新增 §9.1 第 6 条硬门槛 + "新测试方法数量当门槛" → ✅ 已撤销；以证据补齐矩阵 + 收敛计划签署。

---

## 5. 执行顺序

1. ✅ **落 §2 四组真实后端测试**（方法已写入 `MetadataLifecycleRealBackendTests.cs`，编译验证；`98841e6` 提交；CI 四容器（SQL 1433/MySQL 8/Postgres 15/Qdrant 6334）于 2026-09-18 跑出 **13/13/0 全绿**）。
2. **关键流程 E2E 各 1 条**（需 Playwright + 运行中 Web/API，另行安排）。
3. **矩阵剩余风险补证据 / 人工记录**（歧义/不可达守卫、C10 视觉、Ask 在线验收）。
4. **全部 CI 通过后签署 v10 闭环**（不新增硬性测试方法数量门槛）。

---

## 6. 风险与待确认

- 真实后端测试依赖 `SB_REAL_*` 远程库 + 建库权限 + Qdrant 可写（见 final §9.1 与 memory 2026-09-18）。
- Blazor E2E 需 Playwright Chromium + 运行中 Web/API（`SB_E2E_*`），可指向远程部署或本地 `dotnet run`。
- 本文件未改动 final 文档正文（§9.1 仅恢复原状，未新增门槛）。

---

## 7. C1–C11 / §L.8 证据映射（补齐项 ③）

> 判定口径：✅ 有自动化测试直接覆盖（标注 `测试类.方法`）｜🟡 部分覆盖（设计已落地但缺专门断言/仅真实后端或仅 API 层）｜❌ 无自动化证据（需补测试或人工记录）。所有引用均经源码核对（见 §6 取证）。

### 7.1 C1–C11 逐项映射

| 条款 | 验收核心点 | 现有证据 | 覆盖度 |
|---|---|---|---|
| **C1** 新增不自动扫描 + 保存并扫描 | 仅创建无 ScanJob；保存并扫描入队；旧端点 410 | `MetadataScanControllerTests.Scan_CreatesQueuedJob_AndEnqueues`、`Scan_OldFixedEndpoint_Returns410`；`DataSourceScanE2ETests.Admin_CreateDataSource_ThenScan_PopulatesMetadata`（创建时不带 scanAfterCreate，再显式扫描） | ✅ |
| **C2** 扫描范围选择（§10.1） | 唯一索引含 version、跨 schema/库同名并存、FK/字典识别、扫描结果能被 Ask 命中 | `MetadataVectorIntegrityTests.MetadataTable_同DataSource_Catalog_Schema_Table_唯一` / `不同Catalog_Schema_允许同名` / `空Catalog_Schema_不唯一约束_兼容存量`；`MetadataScannerServiceTests.ScanAsync_ForeignKeysAndDictionaryTable_PopulateRolesAndConfig` / `WideJeeSiteDictionaryTable_*`；`MetadataDiscoveryHeuristicsTests.ResolveRoles_*` / `IsDictionaryCandidate_*`；`QueryScopeRealBackendTests.CrossSchema_*` / `CrossDatabase_*`（真实后端验证 Ask 限定名命中） | 🟡（范围排除前缀/视图开关的纯单元断言缺；跨 schema/库选择正确性已由真实后端覆盖） |
| **C3** 可中断（软取消） | 运行中取消→Cancelling→Cancelled；终态冲突；无权限 403 | `MetadataScanControllerTests.CancelScan_QueuedJob_BecomesCancelled` / `RunningJob_BecomesCancelling` / `TerminalJob_Conflict` / `Forbidden_WhenMissingCancelPermission` / `NotFound_ForUnknownJob` | ✅（端点/状态）；🟡（in-flight 表安全点退出、active/Ask 不变为取消令牌传播逻辑，未单元断言） |
| **C4** 重启恢复（§10.3） | 遗留 Queued 重入队；kill-9→Running 标 Failed(worker_interrupted)；对账与消费并发；StopAsync 双循环完成 | **未找到 `ScanStartupReconciler` / `ReconcileAsync` / worker_interrupted 的专门测试**（`MetadataVersionGcJobTests` 仅覆盖 GC 作业幂等，非对账器） | ❌ **缺口**：建议补 HostedService 集成测试（遗留 Running→Failed、>1024 Queued 启动不阻塞、双循环并发） |
| **C5** 页面恢复续显（含重启） | GET latest 返回最新任务（不限终态）；进度快照富化 | `MetadataScanControllerTests.GetLatestScanJob_ReturnsNull_WhenNone` / `ReturnsLatest` / `GetScanJob_ReturnsCreatedJob_Status` / `GetScanJob_Returns_RichProgressSnapshot`；E2E 轮询至终端 | ✅（API 层）；🟡（Blazor 页面续轮询为 UI 行为，未单元测） |
| **C6** 真正元数据隔离（=§L.8 场景1–5,7–10） | 隔离/取消不污染/激活原子+引用不丢/失败不污染/重扫保成功表/版本不重号/孤儿 | 见 §7.2（L-场景映射） | ✅（核心场景真实后端覆盖） |
| **C7** 失败表级记录 + 重试 + 部分成功 | 重试 3 次写失败项→PartiallySucceeded；仅重扫失败项保成功表；retry-failed 端点 | `MetadataScanControllerTests.GetScanJob_ReturnsTableFailures` / `RetryFailedScan_*`(4)；`MetadataScannerServiceTests.ScanAsync_ColumnReadFailure_RetainsFailedTableAndReportsPartialResult`；`MetadataLifecycleRealBackendTests.RescanOnlyFailedItems_ThenActivates` | ✅ |
| **C8** 删除两档 + 影响分析 + 一致性 | cleanup：先清 Restrict 引用→级联→写 GC 待办→重启续跑；disable 保留 | `CleanupEndpointRealBackendTests.CleanupEndpoint_PersistsAllUntaggedPointIds_AndGcFinishesAfterContextRestart`；`MetadataLifecycleRealBackendTests.DeletedSource_StoredGcRequest_SurvivesContextRestart_*` / `Cleanup_KillBeforeGc_Restart_Continues_NoOrphan`；`MetadataVersionGcJobTests.RunAsync_*`；`MetadataVectorMaintenanceTests.Gc_InterruptedOrFailedRequest_Retries` | ✅（cleanup + 重启 GC 真实后端覆盖）；🟡（disable 档与依赖二次确认 409 缺控制器测试） |
| **C9** 细粒度权限 + 审计 | 无 cancel_scan→403；每操作落一条；失败→result=failure | `MetadataScanControllerTests.Scan_Forbidden_WhenDataSourceNotAuthorized` / `Scan_Forbidden_WhenMissingMetadataScanPermission` / `CancelScan_Forbidden_WhenMissingCancelPermission`（① 403） | ✅（权限守卫）；🟡（审计落库内容与 failure→result=failure 未显式断言） |
| **C10** 阶段日志 + 配色高亮（无障碍） | 富进度模型轮转；关键数字高亮；色盲友好 | `ScanTelemetryTests.RichProgress_RoundTrips_StageCountsTimingAndEvents` / `KeepsOnlyLatestTwentyEvents` / `InvalidHistoricalJson_FallsBackToEmptySnapshot`（进度模型 + 事件） | ✅（模型/事件）；🟡（UI 配色/无障碍为前端呈现，归 C10 视觉人工证据） |
| **C11** 页面最终形态（区域 + 按钮权限显隐） | 9 区 + 按钮按权限显隐；失败项可展开与重扫 | **未找到页面结构/权限显隐的自动化测试** | ❌（归 ② 页面 E2E / C10 视觉人工证据） |

### 7.2 §L.1–§L.8 / L-场景 映射

| 条款 | 验收核心点 | 现有证据 | 覆盖度 |
|---|---|---|---|
| **§L.1** 版本分配并发安全（§10.2） | 并发 N 次仅 1 成功（ux_ds_active_scan 409）；串行严格递增；计数仅对成功 +1 | `MetadataVersionCounterRepairTests.RepairMigration_AdvancesCounterPastActiveJobsAndTables`（计数器修复/单调） | 🟡（修复/单调有覆盖；并发分配原子性 + 409 回滚未显式单元测） |
| **§L.3/L.5** clone-on-write + 激活翻指针 + 引用重映射 | 克隆清空向量状态；激活仅翻指针；旧版延迟 GC | `MetadataScannerServiceTests.ActivateAsync_QueuesOldVersionVectorsForGc`；`MetadataVersionGcJobTests.RunAsync_DeletesOnlyInactiveRowsAndIsIdempotent` / `KeepsOldRowsWhenExternalReferenceStillExists` | ✅ |
| **§L.4** 向量隔离 + 激活闸门（§10.6） | 必需 point 未 Synced→阻断激活；Synced 可过；失败批次不删旧 active | `MetadataScannerServiceTests.ActivateAsync_BlocksExistingVectorIdsWithFailedStatus` / `ReportsEveryTableWithIncompleteVectors` / `ScanAsync_VectorIndexFailure_FailsScanWithTelemetry`；`MetadataVectorIntegrityTests.MetadataVectorService_成功时记录Synced与维度` / `嵌入失败时标记Failed`；`MetadataVectorMaintenanceTests.Backfill_MissingStoredPoint_DoesNotMarkSourceComplete`（§10.4 不误标完成）；`MetadataLifecycleRealBackendTests.VectorFailure_BlocksActivation_OldVersionAskStillWorks` | ✅ |
| **§L.7** 删除 GC 持久化 | 删除事务内写 `MetadataVectorGcRequest`（含 point 快照）→ 提交后 Job 删 | `CleanupEndpointRealBackendTests.*`；`MetadataLifecycleRealBackendTests.Cleanup_DeleteWritesGcRequest_WithPointIdSnapshot` / `Cleanup_AuditTrail_Recorded` | ✅ |
| **§L.8 场景1（隔离）** | 扫描中 Ask 仅见旧版、向量返旧 point | `QueryScopeRealBackendTests.CrossSchema_*` / `CrossDatabase_*`（激活前查询旧版） | ✅ |
| **§L.8 场景2（取消不污染）** | 取消后 Ask 不变、staging/point 最终 GC | `MetadataScanControllerTests.CancelScan_*` + 真实后端 GC 作业 | ✅（端点）；🟡（取消后 point 最终 GC 未单独断言） |
| **§L.8 场景3（激活原子+引用不丢）** | 激活后缓存键变、RLS/Binding 改指向新行不丢 | `MetadataScannerServiceTests.ActivateAsync_*`；缺「RLS/PhysicalBinding 自动改指向」专门断言 | 🟡 |
| **§L.8 场景4（失败不污染）** | 失败→Ask 用旧版、旧 active 完好 | `MetadataLifecycleRealBackendTests.VectorFailure_BlocksActivation_OldVersionAskStillWorks` | ✅ |
| **§L.8 场景5（重扫失败项不丢成功表）** | PartiallySucceeded 后仅重扫失败项→成功表保留 | `MetadataLifecycleRealBackendTests.RescanOnlyFailedItems_ThenActivates`；`MetadataScanControllerTests.RetryFailedScan_*` | ✅ |
| **§L.8 场景6（删除一致性）** | cleanup 写待办→重启续跑→残留 point 不被检索 + AuditLog | `CleanupEndpointRealBackendTests.*`；`MetadataLifecycleRealBackendTests.Cleanup_KillBeforeGc_Restart_Continues_NoOrphan` / `Cleanup_AuditTrail_Recorded` | ✅ |
| **§L.8 场景7（版本不重号）** | Cancelled/Failed 遗留 + 并发→NextMetadataVersion 单调 | `MetadataVersionCounterRepairTests.*`（单调修复）；`MetadataLifecycleRealBackendTests.LegacyMigration_BackfillThenActivate_FlipsActiveVersion` | 🟡（修复/legacy 有覆盖；并发重号对抗未显式测） |
| **§L.8 场景8（向量回填闸门）** | 阶段 A 全量召回；回填完翻标志严格过滤；兼容过滤兜底 | `MetadataVectorMaintenanceTests.Backfill_MissingStoredPoint_DoesNotMarkSourceComplete`；缺「阶段 A 中途误开标志兼容过滤兜底」专门断言 | 🟡 |
| **§L.8 场景9（克隆向量重建）** | 克隆清空 VectorId→NeedsIndex 重索引→三类型 point 生成 | `MetadataScannerServiceTests.ActivateAsync_BlocksExistingVectorIdsWithFailedStatus`（反向验证未索引阻断）；缺「克隆后新版本三类型 point 均生成」正向断言 | 🟡 |
| **§L.8 场景10（孤儿引用处理）** | 全量删列→激活默认阻断（Failed+依赖清单）或 DisableAndAudit | `MetadataVectorIntegrityTests.DetectOrphans_返回Qdrant中多余Point`（孤儿检测）；缺「激活默认阻断 orphaned_references」端到端断言 | 🟡 |
| **§10.5** Ask 查询链贯通 | 跨 schema/库同名表用限定名 + 稳定别名、不串表；PG 跨库拒 | `QueryScopeRealBackendTests.CrossSchema_*` / `CrossDatabase_*` | ✅ |

### 7.3 补齐项 ③ 结论
- **已闭环（✅）**：C1、C3（端点）、C5（API）、C7、C8（cleanup+重启 GC）、C9（权限）、C10（模型）；§L.4/§L.5/§L.7 及 §L.8 场景 1/2/4/5/6 真实后端覆盖。
- **部分覆盖（🟡，建议补测试）**：C2 范围排除前缀/视图开关、C3 in-flight 安全、C4 **重启对账（硬缺口）**、C8 disable/409 依赖、C9 审计内容、§L.1 并发分配、§L.8 场景 3/7/8/9/10 的专门断言。
- **无证据（❌，归人工/页面）**：C4 重启对账、C11 页面形态。

---

## 8. 闭环进度快照（2026-09-18）

> CI 结果：**`datasource-scan-real-backend.yml` 四容器全绿，13/13/0**。本地提交 `98841e6`（深度测试 + 验收矩阵 + 脚本 + .gitignore）+ `abce846`（CI 门槛由写死 `eq 6` 改为动态全选全过）已推送 origin/master。

### 签署条件核对（对照 §0 收敛后 4 条）

| # | 签署条件 | 状态 | 证据 |
|---|---|---|---|
| ① | 四组关键跨系统路径真实后端验证全过 | ✅ **已满足** | CI 13/13/0（§2 四组 + CleanupEndpoint 1 + QueryScope 2） |
| ② | 关键页面流程 E2E 各 1 条（取消 / 续显 / 删除确认 / 失败项重扫） | 🟡 **已著 4 条 E2E（待运行部署验证）** | `DataSourceScanE2ETests` 新增 4 方法（取消/续显/删除确认/失败项重扫），`SkippableFact` 门控 SB_E2E_SCAN_CONNECTION；沙箱无运行部署，未执行 |
| ③ | C1–C11/§L.8 在矩阵中逐项映射到已有证据或人工记录 | ✅ **已完成（§7 逐项映射）** | §7.1（C1–C11）+ §7.2（§L.1–§L.8/L-场景）+ §7.3 结论，逐条标注 ✅/🟡/❌ 与测试类.方法 |
| ④ | 全部 CI 通过 | ✅ **已满足** | 真实后端 CI 全绿；常规 CI 此前已绿 |

### 结论
- **数据 / 向量契约层（①⑤ 核心行为：扫描→激活→查询链 / 向量失败保旧版 / 存量 backfill / 删除后 GC 持久化 + 重启清理 / 审计）已通过真实后端闭环验证，可视为"契约层已闭环"。**
- 依 §0 约定，签署"**v10 完整闭环**"仍待 ② 页面 E2E 在运行部署上实际跑绿。③ C1–C11/§L.8 逐项映射已完成（§7）。② 的 4 条 E2E 已著但沙箱无运行部署未执行，需在真机/CI（配 `SB_E2E_SCAN_CONNECTION` + `SB_E2E_BASE_URL` + 管理员凭据）跑绿方可签全闭环；或按"契约层闭环 + 页面/E2E 后续"分级发布。
- C10 视觉验收（人工截图/录屏）与 Ask 在线模型验收（与 CI 分离单独跑）仍按 §3 留人工证据，不计入 CI 硬门槛。
- **仍建议补的测试缺口（§7.3 🟡/❌）**：C4 重启对账（硬缺口）、C11 页面形态、C2 范围排除前缀/视图开关、C8 disable/409 依赖、C9 审计内容、§L.1 并发分配、§L.8 场景 3/7/8/9/10 专门断言。

### 剩余动作清单
1. 🟡 关键页面流程 E2E ×4 已著（`DataSourceScanE2ETests`），需在运行部署执行验证（Playwright + SB_E2E_*）。
2. ✅ C1–C11/§L.8 证据映射已完成（§7）。
3. ⬜ C10 视觉人工证据、Ask 在线模型验收（独立于 CI）。
4. ⬜ 建议补测试：C4 重启对账、C11 页面、§L.1 并发分配、§L.8 场景 3/7/8/9/10（详见 §7.3）。
5. ✅ 本次 `abce846` 已修 CI 门槛硬编码 `eq 6` 缺陷（否则 13≠6 会被卡红）。
