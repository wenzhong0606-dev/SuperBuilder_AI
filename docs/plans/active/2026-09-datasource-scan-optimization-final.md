# 数据源扫描能力优化 — 单次交付规格（v10，单次交付 / 单次验收）

> 取代 v9。v9 已修正 3 处落地缺陷（SQL Server 视图 UNION / C4 并发启动+自有 CTS / 跨库逐库连接定稿）。
> v10 **不重写正文**，仅把 v9 通盘复核确认的 3 处「查询链闭环 + 失败兑现」缺口补实为实施级设计（均经源码核对，见 §10.5/§10.6/§10.7 证据）：① 扫描区分了 catalog/schema，但 **Ask 查询链仍只按 `TableName`**（`QueryTable.cs:7` 仅 `TableName`；`SqlQueryBuilder.cs:43-59` FROM/JOIN 裸表名；所有 `new QueryTable` 构造点只设 `TableName`）→ 同名表跨 schema/跨库会查错表；② **`PartiallySucceeded` 激活门槛未定义**——克隆清空三类向量后（§L.3），某表向量重试耗尽却激活为部分成功，该表向量在新版消失；③ **存量数据无版本标签**——旧 `MetadataTable` catalog/schema 可空、`MetadataVersion` 缺省，旧 Qdrant point 无 `data_source_id`，迁移与删除 GC 无法定位。
> 现状依据（已读）：v5 全文 + v9 全文 +
> `QueryTable.cs:7-13`（仅 `TableName`）、`QueryJoin.cs:28-154`（仅 `Left/RightTableName`）、
> `SqlQueryBuilder.cs:43-59,191-208`（FROM/JOIN/字段引用裸表名）、`ISqlDialect.cs:15-89`（仅 `EscapeIdentifier/ApplyLimit/GetParameterName`，无限定名构造）、
> `QueryPlanBuilder.cs:350-364`（从 `MetadataTable` 建 `QueryTable` 未带 catalog/schema）、
> `MetadataVectorService.cs:120-148`（point payload 无 `data_source_id`/`metadata_version`）、
> `MetadataTable.cs:37,43`（`CatalogName`/`SchemaName` 可空）、
> `MetadataScannerService.cs:588`（`NeedsIndex`）、`SuperBIContext.cs:283,312,370,400,742-744,834-836`。
> 本规格**一次性交付全部能力（C1–C11 + §L + §10.5/§10.6/§10.7）**，§7 是开发顺序非验收分界。
> **最终验收门槛（v10 新增）**：同名表跨 schema Ask、跨库 Ask、向量单表失败、存量迁移、删除后重启 GC 五类场景**必须用真实 MySQL/SQL Server/PostgreSQL/Qdrant 跑集成测试**，Fake 单测不能证明这些跨系统契约（见 §9.1）。

---

## 0. 相对 v5 的修正（本轮 5 处，均经源码核对）

| # | 用户指正 | 源码证据 | v6 处置 |
|---|---|---|---|
| 1 | 克隆后向量须重建/复制；回填只写语义 | `MetadataScannerService.cs:588` `NeedsIndex` 在 `VectorId` 非空时返回 false（克隆旧 VectorId → 跳过索引，新版本行无向量）；`MetadataVectorService.cs:77/119/166` 向量有 **table/column/semantic 三类** point，v5 §L.4 仅写 `MetadataSemantic` | **§L.3** 克隆时**清空** `VectorId/VectorStatus/VectorSyncTime/VectorErrorCode`（三实体），逼 `NeedsIndex` 重索引出新版本 point；**§L.4 回填覆盖三类 point**（table/column/semantic，version=当前 active）。 |
| 2 | 回填闸门要保护「启用过滤」，不只扫描激活 | v5 `VectorBackfillGate` 仅拦激活；若部署后搜索立即启用版本过滤，回填完成前存量 point（无 `metadata_version`）不可召回 | **§L.4 新增部署契约**：`Features.MetadataVersionFilterEnabled` 标志；部署两阶段（先回填再翻标志）+ 过渡期「兼容过滤 `(version==active OR payload 缺 metadata_version)`」兜底。闸门同时拦激活与（标志开启时的）过滤启用。 |
| 3 | 版本号不能仅从现存行取 MAX | 全仓无 `NextMetadataVersion`；`MAX(row version)+1` 遇空表任务/GC 后批次/并发创建会重号 | **§L.1 改为持久化 `DataSource.NextMetadataVersion`**：建任务时在 DB 事务内原子 `SELECT @n=NextMetadataVersion; UPDATE SET NextMetadataVersion=@n+1`，`BatchVersion=@n`。单调计数器，与行存活/并发无关。 |
| 4 | 引用重映射需定义「物理表/列已消失」 | v5 §L.5b 假定每个旧 ID 都有新版映射；全量扫描发现列被删时 RLS/PhysicalBinding 无目标 | **§L.5b 新增孤儿引用处理**：映射后仍有指向旧 ID 的 RLS/PhysicalBinding/LearningRecord → **默认阻断激活**（任务 `Failed` + 返回依赖清单，用户删/禁用配置后重扫）；可选 `MetadataRemapDropMode=DisableAndAudit` 自动停用配置并落 `AuditLog`（不静默丢失安全策略）。 |
| 5 | 删除后向量 GC 请求须持久化 | v5 §L.7 提交后才「触发」GC，进程退出则任务永不建 | **§L.7 新增 `MetadataVectorGcRequest` 持久表**：删除（及取消/failed 清理）事务内写入 `Status=Pending` 待办；`MetadataVectorGcJob` 后台轮询执行，进程退出不丢。 |

> v5 已采纳的：引用重映射（§L.5b）、统一从 active 续种（§L.6）、向量接口扩展+存量回填+闸门（§L.4）、版本防重号（§L.1）、类名（`MetadataSemantic`/`MySqlMetadataReader`）—— 继续有效。

---

## 1. 交付范围与验收原则

- **单次交付、单次验收**：C1–C11 + §L 全部本轮落地。§7 是开发顺序，不是验收分界。
- **复用既有**：`MetadataScanJob`/`ScanTelemetry`/`ScanProgressEvent`（扩展，不平行新建）、`AuditLog`（审计，不新建）、权限沿用 `metadata:*` 命名空间。
- **每项附可验证验收场景**（Given-When-Then），作为验收清单。
- **版本隔离是地基**：§L 必须先于 C6/C7/C8 落地，否则 C6 验收无法成立。

---

## 2. 能力清单（一次性全量）

- **C1** 新增不自动扫描 + 「保存并扫描」选项（还原原第 4 点）
- **C2** 扫描范围选择（库/schema 包含排除、排除系统库/空表/前缀、视图开关）— 独立功能
- **C3** 可中断（软取消，按任务）
- **C4** 重启恢复（启动重入队 + 中断 Running 处理 + 超时归宿）
- **C5** 页面恢复续显（含重启后）
- **C6** 真正元数据隔离（可见性门控，失败/取消不污染 Ask）— 实现见 §L
- **C7** 失败表级记录 + 自动重试 + 部分成功 — 实现见 §L.6
- **C8** 删除两档 + 影响分析 + 一致性与回滚 — 实现见 §L.7
- **C9** 细粒度权限 + 审计（复用 `AuditLog`）
- **C10** 阶段日志 + 配色高亮（无障碍）
- **C11** 页面最终形态（区域 + 按钮，按权限显隐）
- **§L** 元数据版本生命周期（贯穿 C2/C6/C7/C8 的数据 spine）

---

## L. 元数据版本生命周期（贯穿 C2/C6/C7/C8）

> 这是剩余工作决定成败的一处设计。所有「隔离 / 重扫 / 激活 / 删除」都遵守同一套版本协议。

### L.1 三态版本模型与版本分配（修 §0-3）
- `DataSource.ActiveMetadataVersion int`（默认 0）：当前对 Ask 生效的版本号。
- `DataSource.NextMetadataVersion int`（默认 1）：**单调版本计数器**，用于原子分配新批次号。
- 每个 `MetadataTable`/`MetadataColumn`/**`MetadataSemantic`** 带 `MetadataVersion int`（默认 0），等于写入它的扫描批次号（job `BatchVersion`）。
- **版本分配（原子单语句，修 §0-3 + v6 复核第 2 项）**：创建扫描任务时，在**任务创建的同一 DB 事务**内用一条 `UPDATE … OUTPUT` 原子取旧值，避免「先 SELECT 再 UPDATE」在 READ COMMITTED/SNAPSHOT 下出现的并发重号或死锁：
  ```sql
  -- 元库为 SQL Server；单条语句对 DataSources 行加 U+X 锁，OUTPUT 取更新前（已分配）版本
  UPDATE DataSources
     SET NextMetadataVersion = NextMetadataVersion + 1
  OUTPUT DELETED.NextMetadataVersion AS AllocatedVersion   -- 取旧值 = 本次 BatchVersion
   WHERE Id = @dsId;
  -- job.BatchVersion = AllocatedVersion（DELETED.NextMetadataVersion）
  ```
  - 单语句完成「读旧值 + 写新值」，不存在两语句之间的竞态窗口；`BatchVersion` 取自 `DELETED.NextMetadataVersion`，**不依赖任何元数据行的 MAX**。空表任务、失败后被 GC 的批次、并发创建（行锁）都不会导致重号；计数器单调递增、与行是否存活无关。
  - 若 EF Core 不便直接 `OUTPUT`，等价做法：对该行显式 `UPDLOCK, HOLDLOCK`（或 `sp_getapplock`）后再读改写同一事务内完成，**禁止**拆成「先查后改」两条无锁语句。
- **同源去重（DB 层保证，v6 复核第 2 项）**：在分配版本**同一事务**内、写入 job 前，用**过滤唯一索引**强制「一个数据源同一时刻仅一个活动扫描」，并发创建自然撞键、一条失败返回 409，无需应用层轮询：
  ```sql
  CREATE UNIQUE INDEX ux_ds_active_scan
    ON MetadataScanJobs (DataSourceId)
    WHERE Status IN ('Queued','Running','Cancelling','Retrying');
  ```
  - 创建任务时若已存在活动 job → `INSERT` 触发唯一违例 → 事务回滚（含版本分配）→ 返回 `409 + 当前 jobId`（C9 同源去重）。版本计数不受污染。
  - 该约束与 §L.1 版本分配同处一事务 → 分配与去重要么同时成功、要么同时回滚。
- Ask 所有元数据读取路径一律 `WHERE MetadataVersion == ds.ActiveMetadataVersion`（见 §L.5 消费点清单）。

### L.2 唯一索引与版本列
- **迁移**：唯一索引改为
  `IX_MetadataTables_DataSourceId_CatalogName_SchemaName_TableName`
  = `(DataSourceId, CatalogName, SchemaName, TableName, MetadataVersion)`，`IsUnique()`，保留原过滤 `[CatalogName] IS NOT NULL AND [SchemaName] IS NOT NULL`。
  - null-catalog 源（MySQL/SQLite）唯一性由写入协议（L.3）保证：catalog 为空者归一化为数据库名（sentinel）。
- `MetadataTable`/`MetadataColumn`/**`MetadataSemantic`** 各加 `MetadataVersion int`（默认 0，非 null）。存量行回填 `0`。

### L.3 扫描写入协议：版本化 clone-on-write（修 §0-1 向量）
- 任务启动、进入「结构同步」前，先按**本次 scope（C2）**从「种子版本=当前 active」深拷贝出 staging 行：
  - 复制 = 深拷贝 `MetadataTable`+`Columns`+**`MetadataSemantic`**，生成**新实体 Id**，`MetadataVersion = job.BatchVersion`，`DataSourceId`/`TenantId` 不变。
- **克隆时清空向量状态（修 §0-1）**：复制出的三实体一律 `VectorId = null`、`VectorStatus = null`、`VectorSyncTime = null`、`VectorErrorCode = null`。
  - 理由：`NeedsIndex`（`MetadataScannerService.cs:588`）在 `VectorId` 非空时返回 false → 若克隆了旧 VectorId，该行被跳过、不再索引，新版本行在「版本过滤」下无对应向量。清空后 `NeedsIndex` 返回 true，扫描器按既有流水线用**新实体 Id** 重新生成 point（`CreateStableVectorId(type, newId)`，`MetadataVectorService.cs:367`），新 point 自带 `metadata_version=BatchVersion` payload。
  - 语义文本的 re-embed 沿用现有 `SearchText`，向量确定性不变；不复制旧 point（避免版本错挂）。
- 扫描器**改写查找逻辑**：`existsTables.FirstOrDefault(x => x.TableName == t && x.MetadataVersion == staging)`（原 `:168` 按 `TableName` 取旧实体原地更新 → 改为取 staging 克隆；若 staging 无该表则即时 clone 再改）。
- **永不修改 `MetadataVersion == ActiveMetadataVersion` 的行**。扫描进行中写入对 Ask 完全不可见（靠 §L.5 过滤，与 SaveChanges 分阶段无关）。

### L.4 向量隔离（修 §0-1 覆盖三类 / §0-2 部署闸门，重写）
- **接口扩展**（`IQdrantService`）：
  - `QueryAsync(float[] vector, int limit, VectorSearchFilter? filter = null, CancellationToken ct = default)`。
  - `RetrieveVectorAsync(string id, CancellationToken ct = default)` → 返回 `(float[] Vector, IReadOnlyDictionary<string,object> Payload)?`，供回填读取存量向量（避免重新 embedding）。
  - `VectorSearchFilter`：`(long TenantId, IReadOnlyList<(long DataSourceId, int MetadataVersion)> AllowedVersions)`。
- **Payload（写入时）**：`MetadataVectorService.UpsertAsync/Batch` 给每个 point 写 `tenant_id`/`data_source_id`/`metadata_version`/`metadata_id`/`metadata_type`（type ∈ table/column/semantic）。
- **搜索过滤**：语义搜索按涉及授权源解析各自 `ActiveMetadataVersion`，构造 `VectorSearchFilter`：`must: tenant_id==T` + `should: [(ds==D1 AND version==V1), …]`。
- **存量向量回填（修 §0-1 覆盖三类 point，修 §0-2 部署契约）**：
  - 新增 **`MetadataVectorBackfillJob`**（幂等、可重试）：遍历**三类** `MetadataTable`(type=table)/`MetadataColumn`(type=column)/**`MetadataSemantic`**(type=semantic) 中 `VectorId` 非空的行，用 `RetrieveVectorAsync` 取回向量+旧 payload，按 `DataSourceId`（table/column→table→ds；semantic→column→table→ds）反查，以**该 ds 当前 `ActiveMetadataVersion`（初始 0）** 为 `metadata_version` 重新 `UpsertAsync`（同 ID，向量不变）。多租户分批；失败点入重试队列。
  - **部署两阶段（修 §0-2）**：
    1. **阶段 A（过滤关闭）**：`Features.MetadataVersionFilterEnabled=false` 部署；搜索不传 `VectorSearchFilter`（沿用旧行为，全量召回）；后台先跑 `MetadataVectorBackfillJob` 给所有存量 point 打 `metadata_version=0`。
    2. **阶段 B（翻标志）**：`VectorBackfillGate` 确认**全部 ds 的 `DataSource.VectorsBackfilled=true`**（v6 复核第 4 项修正归属）后，运维将 `Features.MetadataVersionFilterEnabled` 置 `true` → 严格 `version==active` 过滤生效；此后扫描激活前也须经同一闸门（任一 ds 未回填则拒绝激活，409）。
  - **过渡期兼容过滤（兜底）**：若标志提前开启而个别 ds 尚未回填，`VectorSearchFilter` 在过渡期解析为 `(version==active OR payload 缺 metadata_version)`，保证存量 untagged point 仍可召回；回填完成后自动退化为严格过滤。
- **激活后旧版 point 清理**：仅在 §10.6 向量完整性闸门通过且版本指针成功翻转后，`MetadataVectorGcJob` 按 `metadata_version < active`（或已删除源的 `data_source_id`）删除；失败/取消批次不得清理旧 active point。GC 失败仅影响存储，不影响正确性（过滤已屏蔽）。

### L.5 激活：仅翻指针 + 引用重映射 + 延迟 GC（C6 核心）
- **激活事务（单 `SaveChanges`，SQL 事务内原子）只做三件事**：
  1. **向量完整性闸门（§10.6，激活前置校验）**：遍历新版本全部表及实际需要生成的 table/column/semantic point；需要索引的实体须有 `VectorId` 且 `VectorStatus == "Synced"`（与现有 `MetadataVectorService` 一致），不需要生成 point 的实体不参与比较。任一必需 point 未同步 → **阻断激活**：不翻指针、任务置 `Failed`+`FailedReason="vector_index_incomplete"`、返回失败表清单；旧 active 不变。闸门通过后才进入下方 2–3。
  2. `ds.ActiveMetadataVersion = job.BatchVersion`；
  3. `MetadataScanJob` 置 `Succeeded`/`PartiallySucceeded` + `ActivatedVersion = BatchVersion`（`PartiallySucceeded` 仅限 SQL 部分成功且必需向量全部 `Synced`，见 §10.6）；
  4. **引用重映射（§L.5b）**。
- **§L.5b 引用重映射（与翻指针同事务）+ 孤儿引用处理（修 §0-4）**：
  - 取「旧 active 版本」表/列 Id 集合，按物理键 `(DataSourceId,CatalogName,SchemaName,TableName[,ColumnName])` 与新版本（BatchVersion）行建立 `旧Id→新Id` 映射。
  - `UPDATE RowLevelSecurityPolicy` / `PhysicalBinding` / `MetadataLearningRecord` 的表/列 FK → 新 Id（RLS 表级 Cascade `:743`、列级 Restrict `:744`；PhysicalBinding 全 Restrict `:835-836`；LearningRecord SetNull `:400`，重映射以保留学习）。
  - **孤儿引用（修 §0-4）**：映射后仍有指向旧 ID 的 RLS/PhysicalBinding/LearningRecord（=该表/列在本轮扫描中已被删除）→ 分两种策略：
    - **默认阻断（fail-safe，已拍板）**：**中止激活**，`MetadataScanJob` 置 `Failed` + `FailedReason="orphaned_references"`，并在任务结果返回结构化依赖清单（`{kind: RLS|BINDING|LEARNING, id, dataSourceId, table, column}`）。用户须在配置页删除失效引用，或将其改绑到仍存在的表/列后重新扫描；仅把 RLS/PhysicalBinding 标记为禁用**不等于**解除旧表/列 FK，不能保证后续旧版行 GC 成功。处理完成后重新从 active 播种并扫描，激活前再次检查依赖；页面须给出依赖项与处理入口。**绝不静默丢失安全策略。**
    - **可选自动停用（opt-in，`MetadataRemapDropMode=DisableAndAudit`）**：将上述孤儿 RLS 置 `Enabled=false`、孤儿 PhysicalBinding 软禁用（或置 `Disabled` 标志），并落 `AuditLog("reference_target_dropped: config auto-disabled", actor=system, entityId)`；配置保留可审计、可恢复。两种策略均不物理删除引用，仅阻断其生效。
  - 上述 UPDATE/阻断判定与指针翻转同处一个 SQL 事务 → 原子、对外一致。
- **§L.5c 旧版行延迟 GC**：重映射（或阻断）后，旧 active 版本行已无 Restrict 引用 → `MetadataVersionGcJob` 删除 `MetadataVersion < active` 且无存活引用的 `MetadataTable`/`MetadataColumn`/`MetadataSemantic` 行。GC 与激活解耦。
- **取消/失败**：不执行激活 → `ActiveMetadataVersion` 不变 → Ask 不变；staging 行/point + 孤儿引用均完好。
- **消费点清单（必须加 `MetadataVersion == active` 过滤，Golden 回归）**：`MetadataSemanticSearchService.SearchAsync`（SQL 侧）、`MetadataContextBuilder`/`QueryUnderstanding`、`TableSelector`、`DisplayResolutionService.Enrich`、`DataSourceCatalogVersionProvider`；grep `MetadataTables`/`MetadataColumns`/`MetadataSemantics` 全量覆盖；`AskCacheVersionProvider` 改为仅对 active 行求和。

### L.6 新任务如何基于上一版本（闭合 C7/C2/部分成功）
**统一播种规则（任务启动时一次性决定）**：每个新任务都从「当前 active 版本」复制完整快照（full/scope/retry/partial 一律如此）。
- **全量重扫**：种子=active → 复制全部 → 应用全部表 → 激活。
- **范围扫描（C2）**：种子=active → 复制全部 → 仅应用 scope 内表 → 激活（scope 外保留）。
- **仅重扫失败项 / PartiallySucceeded 续扫**：种子=active（已含成功表）→ 复制 → **仅应用失败表**（来自 `MetadataScanJobFailure` 中 `OriginalJobId` 关联源任务）；失败项记录 `OriginalJobId` 关联原任务 → 激活后成功表保留、失败表更新。
- **取消后不提供「继续」（已拍板）**：`Cancelled` 任务保留状态、日志与错误记录供页面查看，但 staging 版本永不作为新任务种子；取消后重新扫描统一从 active 播种。取消批次的 staging 行与 point 按 §L.7 的持久 GC 待办回收，不设置为 Resume 预留的 24h GC 豁免。

### L.7 取消与删除的外部一致性（C3/C8，修 §0-5 持久 GC）
- **取消**：软取消（C3）安全点退出；staging 行/point 在 GC 前暂存但不可见，`ActiveMetadataVersion` 不变；终态落库后持久化该批次 GC 待办，由 `MetadataVersionGcJob`/`MetadataVectorGcJob` 幂等清理，失败可重试，不因不支持 Resume 而长期豁免。
- **删除 cleanup（C8）**：
  - **先清 Restrict 引用**：因 `RowLevelSecurityPolicy.DataSourceId`(`:742`) 与 `PhysicalBinding.DataSourceId`(`:834`) 为 **Restrict**，删源前必须事务内先删：RLS、PhysicalBinding、LearningRecord（显式清，免孤儿）。
  - 随后 `DELETE DataSource` → 级联 `MetadataTables`(`Cascade :283`)→`Columns`(`Cascade :312`)→`Semantics`(`Cascade :370`)→`MetadataScanJobs`/授权/`MetadataDictionaryConfigs`。
  - **持久化向量 GC 待办（修 §0-5）**：在**删除同一事务内**写入 `MetadataVectorGcRequest{ DataSourceId=ds, TenantId, Status=Pending, Reason="DataSourceDeleted", RequestedAt }`。`MetadataVectorGcJob` 后台轮询 `Pending` 待办，按 `data_source_id` 删 Qdrant point（可重试、幂等）。进程在提交后、Job 执行前退出 → 待办行持久 → 重启后 Job 继续，不会丢失。
  - 即使 GC 暂未跑完，因 `data_source_id` 已删、Ask 不再引用该源，残留 point 不会被检索到（tenant/ds 过滤）。
  - 影响分析（C8）在事务**前**查引用（Ask 快照/仪表盘/应用/`QueryPlan`），返回清单供二次确认。

### L.8 生命周期验收场景（端到端）
- **L-场景1（隔离）**：扫描进行中 → 对该源 Ask → 结果仅含上次 active 元数据、不含半写入；向量检索返回旧版 point。
- **L-场景2（取消不污染）**：取消 → Ask 不变；`ActiveMetadataVersion` 不变；staging/point 最终被 GC；RLS/PhysicalBinding 完好。
- **L-场景3（激活原子 + 引用不丢）**：激活 → Ask 含新元数据、缓存键变化、向量过滤切新 version；RLS/PhysicalBinding 自动改指向新版本行、不丢失、不抛 Restrict 异常。
- **L-场景4（失败不污染）**：失败 → Ask 仍用旧版本；staging 清理；旧 active 行与引用完好。
- **L-场景5（重扫失败项不丢成功表）**：`PartiallySucceeded` 后仅重扫失败项 → 激活后成功表保留、失败表更新、全集一致。
- **L-场景6（删除一致性）**：cleanup → 先删 Restrict 引用 → 删源级联 → 事务内写 `MetadataVectorGcRequest` → 提交后 Job 删 point；中途进程退出 → 待办持久、重启续跑；残留 point 不被检索 + `AuditLog`。
- **L-场景7（版本不重号）**：`Cancelled/Failed` 遗留 staging + 并发创建 → `NextMetadataVersion` 单调分配，新批次号恒大于旧，无唯一索引冲突。
- **L-场景8（向量回填闸门）**：过滤关闭阶段 A 下存量点全量召回；回填完成 + 翻标志后严格过滤生效；阶段 A 中途若标志误开，兼容过滤 `(version==active OR 缺 metadata_version)` 兜底不丢召回。
- **L-场景9（克隆向量重建）**：克隆 staging 行 `VectorId` 清空 → `NeedsIndex` 重索引 → 新版本三类型 point（table/column/semantic）均生成；激活后搜索可召回新版本向量。
- **L-场景10（孤儿引用处理）**：全量扫描删列 → 激活默认阻断（`Failed`+依赖清单）；或 `DisableAndAudit` 模式自动停用 RLS/Binding 并落审计；安全策略不静默丢失。

---

## 3. 数据模型与接口变更（合并，单次）

**枚举** `MetadataScanJobStatus`：+ `Cancelling`/`Cancelled`/`PartiallySucceeded`/`Retrying`（共 8 态）；`Failed` 可带 `FailedReason=orphaned_references`。

**`MetadataScanJob`**：+ `CancelledAt?`、`ScopeJson?`、`BatchVersion int`、`SeedVersion int`、`OriginalJobId?`、`LastHeartbeatUtc?`、`ActivatedVersion?`、`FailedReason?`。

**`DataSource`**：+ `VectorsBackfilled bool`（默认 false，v6 复核第 4 项 **修正归属**）。该标志判断「本数据源存量向量是否已补齐 `metadata_version` payload」，闸门按数据源判定（非按 job）；**正常重扫不重置**（新 point 自带 version，v8 修订）；仅当检测到缺 payload 旧 point 或 collection 重建后才置 false 并触发 `MetadataVectorBackfillJob` 重新回填，回填完成置 true；`cleanup` 删除源时不重置。`MetadataScanJob` 不再承载此标志。

**新表** `MetadataScanJobFailure`：`JobId`/`OriginalJobId`/`DataSourceId`/`Database`/`Schema`/`TableName`/`Stage`/`ErrorType`/`ErrorMessage(脱敏)`/`RetryCount`/`FirstFailedAt`/`LastFailedAt`/`Resolved`。索引 `IX_JobId`。

**新表** `MetadataVectorGcRequest`：`Id`、`DataSourceId?`、`TenantId`、`OldVersion?`、`Reason`(enum: DataSourceDeleted/StagingGc)、`Status`(Pending/Running/Done/Failed)、`PayloadJson?`、`RequestedAt`、`LastAttemptAt?`、`Error?`。索引 `IX_Status`。

**版本隔离（§L）**：
- `DataSource.ActiveMetadataVersion int`（默认 0）+ **`DataSource.NextMetadataVersion int`（默认 1，单调计数器，§L.1）**；
- `MetadataTable/Column/MetadataSemantic` 各 + `MetadataVersion int`（默认 0）；
- 唯一索引改为 `(DataSourceId,CatalogName,SchemaName,TableName,MetadataVersion)`（过滤非 null cat/sch）；
- null-catalog 源扫描时 catalog 归一化为数据库名。

**向量（§L.4）**：`IQdrantService.QueryAsync` 增 `VectorSearchFilter` + 新增 `RetrieveVectorAsync`；point payload 增 `tenant_id`/`data_source_id`/`metadata_version`/`metadata_type`；新增 `MetadataVectorBackfillJob`/`MetadataVectorGcJob`/`VectorBackfillGate`；新增配置 `Features.MetadataVersionFilterEnabled` 与 `MetadataRemapDropMode`。

**读取器契约（§5）**：`IDataSourceMetadataReader` 全方法加 `CancellationToken`；新增逐表列读取重载；实现类为统一多方言 **`MySqlMetadataReader`**。

**端点**：
- `POST /api/data-sources`：增 `scanAfterCreate`(默认 false)。
- `POST /api/data-sources/{id}/metadata/scan`：body 增 `ScanScope`（C2）；启动按 §L.6 从 active 选种；`BatchVersion` 由 §L.1 事务分配。
- `POST /api/data-sources/{id}/metadata/scan/{jobId}/cancel`（C3）。
- `GET /api/data-sources/{id}/metadata/scan/latest`（C5，返最新任务不限终态）。
- `POST /api/data-sources/{id}/metadata/scan/{jobId}/retry-failed`（C7，从 active 续扫）。
- `DELETE /api/data-sources/{id}?mode=disable|cleanup`（C8，事务内写 `MetadataVectorGcRequest`）。

**权限**：`metadata:cancel_scan`/`metadata:delete`/`metadata:cleanup_metadata`；复用 `metadata:view/edit/scan`。

**审计**：复用 `AuditLog`，`Action` ∈ `metadata.scan`/`metadata.cancel`/`metadata.retry`/`metadata.delete`/`metadata.cleanup`/`metadata.vector_gc`/`metadata.vector_backfill`/`metadata.ref_remap_drop`。

---

## 4. 逐项规格 + 验收场景

### C1 新增不自动扫描 + 保存并扫描
- `Create`（`DataSourcesController.cs:207-255`）仅存源+授权；请求增 `scanAfterCreate`（默认 false）。
- **响应约定**：`scanAfterCreate=false` → `201`；`true` 入队成功 → `201` + `jobId`；`true` 入队失败 → `202` + `{sourceCreated:true, scanEnqueued:false, retryScanUrl}`，源不回滚。
- 列表对 `LastScanAt` 为空源显「立即扫描」。
- **验收**：① 仅创建→`Enabled=true`、无 `ScanJob`；② 保存并扫描成功→`Queued/Running`+跳转；③ 入队失败→`202`+源已建+可重试；④ 列表对从未扫描源显「立即扫描」。

### C2 扫描范围选择
- `ScanScope`：`Databases`/`Schemas`（include，空=全部）、`ExcludeSystemDbs=true`、`ExcludeEmptyTables`、`ExcludePrefixes[]`、`ScanViews(bool)`。默认排除 `information_schema`/`mysql`/`performance_schema`/`sys`/SQL Server 系统库/临时表。
- 按 §L.6 从 active 播种、仅应用 scope 内表、激活（scope 外保留）。
- **数据契约（DTO/读取 SQL/外键/物理键）是 C2 可实施的硬前提**，见 **§10.1**（扩展 `TableMetadataDto`/`ColumnMetadataDto` + `ForeignKeyMetadataDto` 的 catalog/schema/类型；三库读取 SQL；扫描器物理键改为 `(CatalogName,SchemaName,TableName)`；`MetadataTable.ObjectKind` 区分表/视图；同名表跨 schema 靠唯一索引并存）。
- **验收**：① 默认排除系统库；② `ExcludePrefixes=["tmp_"]`；③ `ScanViews=false` 视图不扫；④ SQL Server/PostgreSQL 选单 schema→仅该 schema 激活、其余保留；MySQL 的 `Schemas` 与 `Databases` 指向同一数据库集合，不构造独立 schema 层；⑤ **同名表在 SQL Server/PostgreSQL 的 A.schema1 与 A.schema2 各存一份、互不覆盖；MySQL 在 db1/db2 各存一份**（§10.1 契约 + 唯一索引 `(DataSourceId,CatalogName,SchemaName,TableName,MetadataVersion)`）；⑥ **扫描结果能被 Ask 正确命中**（§10.5 查询链贯通 + §9.1 场景1/2）：同名跨 schema/跨库 Ask 用限定名和稳定别名、不串表、PostgreSQL 非当前连接库查询被拒。

### C3 可中断（软取消）
- `MetadataScanHostedService` 维护 `ConcurrentDictionary<long, CancellationTokenSource> _jobCts`；`ProcessJobAsync` 用 `linked(stoppingToken, jobCts)` 传 `ScanAsync`。取消端点置 `Cancelling`+`_jobCts.Cancel()`；`ScanAsync` 在阶段/每表边界 `ct.ThrowIfCancellationRequested()` → `Cancelled`（staging 保留不可见，active 不变）。
- **验收**：① 运行取消→`Running→Cancelling→Cancelled`（≤数秒）；② active 不变、Ask 不变、引用完好；③ in-flight 表安全点退出。

### C4 重启恢复
- 启动对账（`ScanStartupReconciler`，v6 非阻塞 + v8 宿主托管 + **v9 并发启动/取消修正**）：
  - **消费循环与对账循环必须并发启动（v9 修订，关键修正）**：`ExecuteAsync` 内**同时**启动受管消费循环与受管对账循环，再 `await Task.WhenAll(_consumeTask, _reconcileTask)`。`_consumeTask` 即现有 `while(!token.IsCancellationRequested){ DequeueAsync → ProcessJobAsync }`；`_reconcileTask` 为遗留 `Queued`/`Running` 对账。**禁止**「先 `await` 消费循环、再 `await` 对账」的串行写法——空队列下消费循环阻塞至停机，对账永远不执行，遗留 `Queued` 永不重入队。
  - **取消用自有 `CancellationTokenSource`（v9 修订，关键修正）**：`CancellationToken` 无 `Cancel()` 方法；service 持有 `_cts = new CancellationTokenSource()`，将 `_cts.Token` 传入两个循环，`ExecuteAsync` 同时监听宿主 `stoppingToken`（`stoppingToken` 触发即 `_cts.Cancel()`）。`StopAsync` 中 `_cts.Cancel()` + `await Task.WhenAll(_consumeTask, _reconcileTask)`（带超时）确保可停止。**禁止**对 `stoppingToken` 调用 `.Cancel()`。
  - **对账须为宿主管理的后台任务（v8）**：`_reconcileTask` 持有引用；`ReconcileAsync` 全程 `try/catch` 记录异常（日志/`AuditLog`，单批失败不中断整体）；`_cts` 取消即终止剩余批次。
  - **遗留 `Queued` 异步分批重入队**：对账按批（≤256）读取 `Queued` 并 `EnqueueAsync`；批间 `await Task.Yield()`；与消费循环并发 → 水位被消费压低，不会因 `Bounded(1024, FullMode=Wait)`（`MetadataScanQueue.cs:13-22`）永久阻塞启动；`Queued` > 1024 不预读进内存，逐批取逐批投。
  - **本期单工作实例约束**：扫描 HostedService 只能有一个实际消费者实例；Web 可多实例，但不得在每个实例都运行扫描 worker。`ux_ds_active_scan` 只限制同源活动任务数，不能证明其他实例的 `Running` 已停止，也不能代替 worker 租约/抢占；若部署多扫描 worker，须先另行实现持久 owner/lease 与 fencing，本规格不声称该模式安全。
  - **遗留 `Running` 归宿（已拍板）**：单工作实例重启时，旧进程已退出，启动对账将遗留 `Running/Retrying/Cancelling` 条件更新为 `Failed`（原因 `worker_interrupted`；已请求取消的任务记录原取消请求），不自动重跑；保持 active 不变，staging 由持久 GC 待办回收。`Queued` 仍按上文重入队。
  - **心跳与超时（已拍板）**：`MetadataScan:HeartbeatTimeoutMinutes` 默认 30、可配且须校验为正值；worker 独立于逐表进度定时刷新 `LastHeartbeatUtc`（建议每 30 秒，包含慢表读取/向量调用期间）。定期对账仅将 `Running/Retrying` 且心跳早于阈值的任务条件更新为 `Failed`（原因 `heartbeat_timeout`）并请求取消当前执行；`LastHeartbeatUtc` 为 null 时以 `StartedAt` 为起点。心跳、超时和激活都以数据库状态条件更新协调：失去活动状态的 worker 不得继续写入 staging 或激活，即使外部调用不响应取消也必须在每次写入/激活前复查状态；超时/终态切换幂等并保留日志。
- **验收**：① `Queued` 自动继续；② 单工作实例 kill -9 留 `Running`→启动标记 `Failed(worker_interrupted)`、active 不变、Ask 用上次成功版本、staging 最终回收；③ 单表操作超过 30min 但独立心跳正常→不误判，失心跳超过可配阈值→`Failed(heartbeat_timeout)` 且迟到 worker 不能激活；④ **Channel 满（>1024 遗留 `Queued`）启动不阻塞**，消费与对账**并发**、全部续跑；⑤ **`StopAsync`→`_cts.Cancel()`+`Task.WhenAll(消费,对账)` 两者完成**（单批异常被记录、剩余 `Queued` 重启续入队、不丢失）。

### C5 页面恢复续显（含重启）
- `GET …/scan/latest` 返**最新任务（不限终态）**；前端按状态：`Queued/Running/Cancelling/Retrying`→续轮询；`Succeeded/PartiallySucceeded/Failed/Cancelled`→终态摘要。`DataSourceDetail.OnAfterRenderAsync→Load()` 续轮询。
- **验收**：① 起扫→退出→重进续显；② 重启→任务仍在（Queued 等待或 Failed 终态），不丢、不二次发起。

### C6 真正元数据隔离（实现见 §L）
- 方案 = §L：clone-on-write（L.3）+ 向量过滤（L.4）+ 激活仅翻指针+引用重映射（L.5/L.5b）+ 消费点过滤。
- **验收 = §L.8 场景 1–5、7–10**。

### C7 失败表级记录 + 自动重试 + 部分成功
- `ScanRetryPolicy`：`MaxAttempts=3`；连接失败快速失败不重试；权限失败不自动重试（标记需用户处理）；表/列/向量抖动可重试。耗尽 → 写 `MetadataScanJobFailure`（`OriginalJobId`）+ 继续；关键阶段失败 → `Failed`。有失败项已产元数据 → `PartiallySucceeded`；重试中 → `Retrying`。
- `retry-failed` 按 §L.6 从 active 续扫。
- **验收**：① 抖动表重试 3 次写失败项→`PartiallySucceeded`；② 权限失败不重试；③ 连接失败→`Failed`；④ 仅重扫失败项→成功表保留（§L.8 场景5）；⑤ 范围扫描成功→scope 外表保留。

### C8 删除两档 + 影响分析 + 一致性与回滚（修 §0-5）
- **两档**：① `mode=disable`：置 `Enabled=false`，配置+元数据保留；② `mode=cleanup`：先删 Restrict 引用（§L.7：RLS/PhysicalBinding/LearningRecord）→ 事务删源级联 → **事务内写 `MetadataVectorGcRequest`** → 提交后 Job 删 point。
- 影响分析：cleanup 前查引用返回清单，二次确认。
- **验收**：① 禁用→Ask 不可用但元数据在；② cleanup→Restrict 引用先清、级联干净、写 GC 待办；③ 有依赖→返清单二次确认；④ GC 中途进程退出→待办持久、重启续跑、残留 point 不被检索 + `AuditLog`（§L.8 场景6）。

### C9 细粒度权限 + 审计（复用 AuditLog）
- 端点加 `HasPermissionAsync` 守卫；同源去重（`Queued/Running`→409+当前 jobId）。审计复用 `AuditLog`，含向量 GC/回填/引用停用。
- **验收**：① 无 `cancel_scan`→403；② 每次操作落一条；③ 失败→`AuditLog.result=failure`。

### C10 阶段日志 + 配色高亮
- `ScanProgressEvent.Level` 扩 `Success`/`Debug`；蓝=进行中、绿=成功、黄=警告/跳过/部分成功、红=失败、灰=等待/取消、深色=需用户处理；RCL 语义文本色变量；关键数字高亮；无障碍配图标。
- **验收**：日志按级别着色、关键数字高亮、色盲友好。

### C11 页面最终形态
- 区域：① 基础信息 ② 连接状态 ③ 扫描范围选择 ④ 当前任务卡片 ⑤ 阶段进度时间线 ⑥ 表扫描进度 ⑦ 失败项列表 ⑧ 扫描日志 ⑨ 操作区。
- 按钮（按权限显隐）：测试连接 / 开始扫描 / 取消扫描 / 重扫失败项 / 全量重扫 / 禁用 / 删除并清理。
- **验收**：各区与按钮按规格呈现、权限显隐正确、失败项可展开与重扫。

---

## 5. 读取器契约变更（C3/C7 前提，必须先行）

- `IDataSourceMetadataReader`：所有方法加 `CancellationToken ct`；新增逐表列读取重载 `GetColumnsAsync(connectionString, dbType, IEnumerable<string> tableNames, ct)`。
- 实现为统一多方言 **`MySqlMetadataReader`**（`MySqlMetadataReader.cs:14`）+ 测试 Fake：`DictFakeReader`/`WideDictFakeReader`/`FakeReader`（`MetadataScannerServiceTests.cs:134,174,215`）同步加 `ct`+逐表重载。
- **验收**：① 读取中取消→当前表边界抛 `OperationCanceledException`；② 单表列读取失败→仅该表重试；③ `MySqlMetadataReader`+Fake 编译通过 + 单测覆盖逐表边界。

---

## 6. 接口 / 模型变更汇总（单次）

枚举 +4 态 · `MetadataScanJob`(+8 字段含 `SeedVersion`/`OriginalJobId`/`FailedReason`) · **`DataSource.VectorsBackfilled`**(回填完成标志，按源判定) · 新表 `MetadataScanJobFailure`(含 `OriginalJobId`) · **新表 `MetadataVectorGcRequest`** · `DataSource.ActiveMetadataVersion`+**`NextMetadataVersion`**(单调计数器) + 三处 `MetadataVersion`(表/列/**`MetadataSemantic`**) + **`MetadataTable.ObjectKind`**(Table/View，C2 视图开关) · 唯一索引加 `MetadataVersion` · 向量 payload(三类 point)+`MetadataVectorGcJob`+`MetadataVectorBackfillJob`+`VectorBackfillGate`+`IQdrantService` 扩展(`QueryAsync` filter/`RetrieveVectorAsync`)+`Features.MetadataVersionFilterEnabled`/`MetadataRemapDropMode` · 读取器加 `ct`+逐表重载(统一 `MySqlMetadataReader`) · 端点 ×6 · 权限 ×3 · 审计复用 `AuditLog` · 迁移：状态枚举、新列、`MetadataScanJobFailure`、`MetadataVectorGcRequest`、可见性 3 列、唯一索引改造、null-catalog 归一化、存量语义/表/列向量回填、NextMetadataVersion 初始化、**DTO/读取器/外键 catalog+schema+类型扩展**、`ux_ds_active_scan` 过滤唯一索引。

---

## 7. 开发顺序（模块序，非分期 defer）

1. **读取器契约（§5 + §10.1）** — 所有后续依赖；含 `ct`+逐表重载（§5）**与** catalog/schema/类型/FK 三键扩展（§10.1，C2 前提）。
2. **版本生命周期骨架（§L.1–L.3, L.5, L.6）** — 迁移（`ActiveMetadataVersion`/**`NextMetadataVersion`**/三处 `MetadataVersion`/唯一索引）、版本事务分配、种子复制（统一从 active、**克隆清空向量状态**）、激活仅翻指针+**引用重映射+孤儿引用处理**、消费点过滤 + 回归。**先于 C6/C7/C8。**
2.5 **Ask 查询链贯通（§10.5）** — `QueryTable` 增 `CatalogName`/`SchemaName`，`QueryJoin` 用左右表 Id 解析两端物理键；补 `ISqlDialect.QualifyTable`，所有 `new QueryTable` 构造点带三键，`SqlQueryBuilder` 在 FROM/JOIN 用限定名并按表 Id 分配稳定别名（所有字段用别名），PostgreSQL 逐表核对连接库。**这是 C2 扫描结果能被 Ask 正确命中的前提**，与步骤 2 并行可，但需在 C2 验收前完成。
3. **向量隔离 + 激活向量闸门（§L.4 + §10.6）** — `IQdrantService` 扩展 + payload（三类 point 含 `data_source_id`）+ `MetadataVectorBackfillJob`（三类 + 补 `data_source_id`）+ `VectorBackfillGate` + `MetadataVectorGcJob`（**按表级向量完整**，§10.6）+ **`MetadataVectorGcRequest` 持久表（含 point ID 快照，§10.7）** + 部署两阶段/兼容过滤 + **激活向量完整性闸门**（§10.6）。
4. **任务模型 + 失败表 + 权限 + 审计**（C7/C9 数据面）。
5. **软取消 + 重启恢复（C3/C4）+ GC 回收孤儿 staging**。
6. **可见性门控（C6 = §L.5 闭环 + L.8 场景 1–5/7–10）**。
7. **失败重试 + 部分成功 + 重扫续种（C7 = §L.6）**。
8. **范围选择 + 保存并扫描（C2/C1）**。
9. **删除两档 + 影响分析 + 引用先清 + 持久 GC 待办（C8 = §L.7）+ 存量迁移规范化（§10.7）** — 含删除前 backfill 闸门（409）。
10. **页面（C5/C10/C11）**。
11. **测试补强（对应每项验收场景 + §9.1 真实后端五类集成）**。

> 以上全部本轮交付；任一模块阻塞须显式升级，不得静默 defer。

---

## 8. 风险与已确认决策

- **§L 是 C6 落地前提**：未实现 clone-on-write+引用重映射+持久版本计数器前，C6 验收无法成立。
- **激活绝不删旧行**：旧行由 `MetadataVersionGcJob` 延迟删；删前须 `MetadataVersionGcRequest`/重映射已把 Restrict 引用改指向新行（`:744,835-836,400`）。
- **克隆须清空向量状态（§0-1）**：否则 `NeedsIndex`(`:588`) 跳过，新版本行无向量；回填须覆盖 table/column/semantic 三类。
- **向量回填是过滤启用前置（§0-2）**：部署两阶段（先回填再翻 `MetadataVersionFilterEnabled`），过渡期兼容过滤兜底；`VectorBackfillGate` 未放行前禁止激活。
- **版本分配用持久计数器（§0-3）**：`NextMetadataVersion` 事务内原子分配，不依赖行存活/并发。
- **孤儿引用默认阻断激活（§0-4）**：fail-safe，返回依赖清单；`DisableAndAudit` 为可选；绝不静默丢安全策略。
- **删除向量 GC 须持久化（§0-5）**：`MetadataVectorGcRequest` 在删除事务内写入，进程退出不丢。
- **唯一索引迁移**：存量行全 `0`；MySQL null-catalog 靠归一化；上线前校验唯一性。
- **staging 存储成本**：每次复制 scope 内全量元数据行；大 schema 关注行数。
- **四项默认已确认**：① 单工作实例重启后遗留 `Running` → `Failed(worker_interrupted)`，不自动重跑；② 无心跳超时默认 30min、`MetadataScan:HeartbeatTimeoutMinutes` 可配，独立定时心跳与终态/激活竞态保护见 C4；③ 不提供取消后「继续」，staging 不作 24h Resume 豁免并由 GC 回收；④ `MetadataRemapDropMode=Block`，给出依赖清单，用户删除或改绑失效 FK 引用后重扫，单纯禁用不视为解阻。多扫描 worker 不在本期支持范围内。
- **i18n / 脱敏**：新增串走四处一致护栏；失败项错误脱敏禁含连接串/SQL。

---

## 10. 实施即需补实的模块级设计（v7 增量，均经源码核对）

> 用户在 v6 复核中确认整体可作为开发依据，但以下 4 项需在对应模块编码时**补实数据契约 / 并发 / 启动 / 模型归属**，否则部分验收仍不成立。此处给出可落地的改动点 + 测试场景，不重写正文。

### 10.1 C2 跨库/schema 数据契约（v6 复核第 1 项）
**现状（已读）**：`TableMetadataDto.cs:6` 仅 `TableName`/`TableComment`；`ColumnMetadataDto.cs:6` 仅 `TableName`/`ColumnName`/…；`MetadataTable` 有 `CatalogName(:37)`/`SchemaName(:43)` 但无类型字段；`MySqlMetadataReader.GetMySqlTablesAsync(:69-77)` 用 `DATABASE()`、过滤 `TABLE_TYPE='BASE TABLE'`、不返回 schema/catalog；扫描器按 `TableName` 匹配（`:168`）。「选单 schema / 扫视图 / 同名表并存」**无法仅靠逐表读取重载实现**。

**改动点**：
1. **DTO 扩展**（统一 `SuperBuilder_AI.Models.DTO`）：
   - `TableMetadataDto` + `string? CatalogName`、`string? SchemaName`、`MetadataObjectKind ObjectKind`（enum：`Table`/`View`）。
   - `ColumnMetadataDto` + `string? CatalogName`、`string? SchemaName`（列须用三键归属表，不能仅靠表名）。
   - `ForeignKeyMetadataDto` 两端各补 `CatalogName`/`SchemaName`（PK 端 + FK 端），外键关联按三键而非表名。
2. **三库读取 SQL**（统一 `MySqlMetadataReader` 的各 `GetXxxAsync` 私有方法；v8 修订跨库边界与 SQL Server 不回退）：
   - MySQL：`SELECT TABLE_SCHEMA CatalogName, TABLE_SCHEMA SchemaName, TABLE_NAME TableName, TABLE_COMMENT TableComment, CASE TABLE_TYPE WHEN 'VIEW' THEN 'View' ELSE 'Table' END ObjectKind FROM information_schema.tables WHERE table_schema = DATABASE()`（去掉 `BASE TABLE` 过滤，视图由 `ObjectKind` 标记；catalog=schema=库名，仅用于统一 DTO 物理键）。列查询 `SELECT … TABLE_SCHEMA CatalogName, TABLE_SCHEMA SchemaName, TABLE_NAME, COLUMN_NAME … FROM information_schema.columns`。**此 SQL 仅覆盖连接串指定数据库；MySQL 的 schema 即 database，不存在独立的库内 schema 层**。`ScanScope.Schemas` 在 MySQL 下与 `Databases` 归一为同一数据库筛选条件，二者若同时指定且不一致则拒绝请求；跨库仍按下文逐库连接汇总。
   - **SQL Server：保留现有 `sys` 系读取能力，不得回退到 `INFORMATION_SCHEMA`（v8 修订）；视图必须被扫到（v9 修订）**。`sys.tables` 仅返基表、`OBJECTPROPERTY(t.object_id,'IsView')` 恒为 0，单表查询无法产出视图行——须将 `sys.tables` 与 `sys.views` **UNION** 后再取 catalog/schema/类型：
     - 表查询：`FROM (SELECT object_id,schema_id,name,type,is_ms_shipped FROM sys.tables UNION ALL SELECT object_id,schema_id,name,type,is_ms_shipped FROM sys.views) o`，投影 `DB_NAME() CatalogName, SCHEMA_NAME(o.schema_id) SchemaName, o.name TableName, CAST(ep.value ...) TableComment, CASE WHEN o.type='V' THEN 'View' ELSE 'Table' END ObjectKind`；`ep` 仍 `LEFT JOIN sys.extended_properties`（按 `o.object_id`，表与视图通用）；`WHERE o.is_ms_shipped=0`。**保留原 `MS_Description` 注释读取路径**。
     - 列查询：`FROM (SELECT object_id,schema_id,name,type FROM sys.tables UNION ALL SELECT object_id,schema_id,name,type FROM sys.views) o INNER JOIN sys.columns c ON c.object_id=o.object_id …` —— 由此 **视图列也进入结果**；保留原 `sys.types`/`is_primary_key`（视图无 PK，`IsPrimaryKey` 自然为 0，不回退）。
     - FK：父表恒为基表，沿用原 `sys.foreign_key_columns`+`sys.tables` 双端，补 `CONSTRAINT_SCHEMA`/`TABLE_CATALOG`，**不动 PK/注释读取路径**。
   - PostgreSQL：`SELECT table_catalog CatalogName, table_schema SchemaName, table_name TableName, table_type→ObjectKind FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema')`。
    - **跨数据库选择 `ScanScope.Databases`（v9 定稿：唯一策略＝「逐库连接汇总」，删除「二选一」退化口径）**：三库 SQL 均针对「连接串当前数据库」返回元数据（MySQL 无独立库内 schema，SQL Server/PostgreSQL 可含多个 schema）；**跨多个数据库必须逐库连接汇总**——`MySqlMetadataReader` 新增 `GetDatabasesAsync(connectionString)`（MySQL `SHOW DATABASES` / SQL Server `SELECT name FROM sys.databases` / Postgres `SELECT datname FROM pg_database`，排除系统库），对每个入选 `Databases` 的库，用「替换了 `database`/`Initial Catalog` 后的连接串」分别调用 `GetTablesAsync`/`GetColumnsAsync`/`GetForeignKeysAsync` 并汇总；`CatalogName` 按实际库名填入。C2 功能范围与验收**仅此一种策略**，不再允许「限定为连接串指定库」的退化口径（v8 原「二选一」作废）。
3. **扫描器物理键**：`:168` 匹配由 `x.TableName == t` 改为 `(x.TableName,x.CatalogName,x.SchemaName) == (t.TableName,t.CatalogName,t.SchemaName)`（限定 staging 版本）；写入 `MetadataTable.CatalogName/SchemaName/ObjectKind` 取自 DTO。同名表跨 schema 靠唯一索引 `(DataSourceId,CatalogName,SchemaName,TableName,MetadataVersion)` 并存，互不覆盖。
4. **模型新增**：`MetadataTable.ObjectKind`（`MetadataObjectKind` enum，默认 `Table`），迁移加列；`ScanViews=false` 时扫描器跳过 `ObjectKind=View` 行（沿用 `ScanScope`）。
5. **null-catalog 归一化**（MySQL/SQLite）：catalog 为空者按 §L.2 归一化为数据库名，唯一索引仍可生效。

**测试场景**：
- 单测：`MySqlMetadataReader` 读表返回 `CatalogName=SchemaName=db`、`View`→`ObjectKind=View`；SQL Server 返回 `DB_NAME()`/`SCHEMA_NAME()`/表类型**且注释与 PK 检测能力不弱于现状**（不得因改用 INFORMATION_SCHEMA 丢失 `MS_Description` 注释与 `is_primary_key`）；**SQL Server `ScanViews=true` 时 `sys.views` 行进入结果（`ObjectKind=View`）、视图列经 UNION 也进入列结果**，`ScanViews=false` 跳过视图（v9 修订，覆盖 claim 1）；PostgreSQL 同；`ScanViews=false` 时扫描器跳过 `ObjectKind=View`。
- 单测：SQL Server/PostgreSQL 的 A 库 `schema1` 与 `schema2` 各有同名表，或 MySQL 的 db1/db2 各有同名表 → 扫描器用三键匹配各建一行、唯一索引不冲突、激活后两份并存（`GetTablesAsync` 返回 2 行）。
- 集成：范围选单 schema → 仅该 schema 入 staging、其余保留；FK 在跨 schema 同表名时仍能正确关联两端 catalog/schema。
- 集成（跨库，v8 修订）：`ScanScope.Databases=["db1","db2"]` → `GetDatabasesAsync` 枚举后逐库连接汇总 → 两库的表/列/FK 均进入 staging、`CatalogName` 区分两库、互不覆盖；单连接默认仅扫连接串数据库（SQL Server/PostgreSQL 可含多 schema，MySQL 不含独立 schema）。

### 10.2 §L.1 版本分配并发安全（v6 复核第 2 项）
**改动点**：见 §L.1 已修正的「原子单语句 `UPDATE … OUTPUT DELETED.NextMetadataVersion`」+「过滤唯一索引 `ux_ds_active_scan` 在分配同事务内强制同源去重」。补充约束：若 EF 不便直接 `OUTPUT`，须用 `UPDLOCK, HOLDLOCK`（或 `sp_getapplock`）锁行后再读改写，**禁止**拆成无锁「先查后改」。

**测试场景**：
- 单测（并发去重，v8 修订）：同 ds **并发** N（如 10）次创建任务 → **仅 1 次成功**，`ux_ds_active_scan` 唯一违例使其余 N−1 次事务回滚（版本分配一并回滚）并返回 `409 + 当前 jobId`；**不为并发创建分配多个版本号**（与「同源单活动扫描」约束一致）。
- 单测（版本单调不重号，v8 修订）：**串行**——先创建并等到任务终态（`Succeeded/Failed/Cancelled`，释放 `ux_ds_active_scan`），再创建下一任务，循环 10 次 → 每次 `BatchVersion` **严格递增、互异、无间隙**；空表任务 / 已被 `MetadataVersionGcJob` 回收的 `BatchVersion` 不影响后续分配，计数器恒增。
- 单测（计数单调补充）：并发回滚的版本号不占用计数（`NextMetadataVersion` 仅对成功提交的任务 +1），故串行 10 次后计数恰为初值 +10。

### 10.3 C4 启动重入队非阻塞 + 宿主托管（v6 复核第 3 项，v8 宿主托管，v9 并发+CTS）
**改动点**：见 C4（v9 修订）——消费循环与对账循环**并发启动**（`Task.WhenAll`），取消用自有 `_cts`（非 `stoppingToken.Cancel()`）。补充约束：**禁止**「先 `await` 消费循环再 `await` 对账」的串行写法（空队列下对账永不执行）；**禁止**在消费者就绪前同步 `EnqueueAsync` 全部遗留 `Queued`（撞 `Bounded(1024, FullMode=Wait)` 永久阻塞启动，`MetadataScanQueue.cs:13-22`）；**禁止**裸 `Task.Run` 后不持有/不取消/不等待。遗留 `Running/Retrying/Cancelling` 的失败归宿、独立心跳与可配超时、单工作实例约束及迟到 worker 防激活均按 C4 执行。

**测试场景**：
- 集成：种子 >1024 个 `Queued` 任务 → 启动后消费者先跑、对账受管后台分批入队 → 全部续跑；启动在超时阈值内完成（无 `TaskCanceledException`/启动挂起）。
- 集成：kill -9 留 `Running` → 启动标记 `Failed`、`ActiveMetadataVersion` 不变、Ask 用上次成功版本、无孤儿 staging 行（被 `MetadataVersionGcJob` 回收）。
- 集成（受管停止，v9 修订）：`StopAsync` 触发 → `_cts.Cancel()` + `Task.WhenAll(_consumeTask,_reconcileTask)` 完成；单批异常被记录、剩余 `Queued` 在重启或下次对账中继续入队（不丢失）。

### 10.4 VectorsBackfilled 模型归属（v6 复核第 4 项，v8 修订重置语义）
**改动点**：见 §3/§6/§L.4 已修正——`VectorsBackfilled` 从 `MetadataScanJob` 移到 `DataSource`（按源判定「存量 point 是否已补齐 `metadata_version` payload」）。
- **正常重扫不重置（v8 修订）**：新扫描生成的 point 本来就带 `metadata_version` payload，激活闸门不应因此被阻。`VectorsBackfilled` 仅在以下情形需要重新判定为 `false` 并重跑 `MetadataVectorBackfillJob`：
  1. **检测到缺 payload 的旧 point**（如存量点 `metadata_version` 缺失）；
  2. **重建/重建 Qdrant collection 后**（所有 point 被清空或 payload 丢失）。
- **删除数据源无须重置**：`cleanup` 即将删除该 ds 的全部行，重置标志无意义；按 §L.7 直接事务内写 `MetadataVectorGcRequest` 清理即可。
- `VectorBackfillGate` 对 `DataSource.VectorsBackfilled=false` 的源拒绝激活（409）。

**测试场景**：
- 单测/集成：`DataSource.VectorsBackfilled` 初始 `false`；`MetadataVectorBackfillJob` 跑完置 `true`；闸门对未回填源拒绝激活（409）。
- 集成（**正常重扫不重置，v8 修订**）：`Features.MetadataVersionFilterEnabled=true` 下，对已回填（`VectorsBackfilled=true`）源**正常重扫** → 本次扫描新 point 自带 version → 激活闸门不拦截、扫描正常激活；**不**因重扫将标志置 false。
- 集成（重置触发条件，v8 修订）：仅当检测到缺 payload 旧 point 或 collection 重建后，才将 `VectorsBackfilled` 置 false 并重跑回填；`cleanup` 删除源时不重置该标志（行将被删）。
- 部署两阶段（§L.4）：阶段 A 过滤关闭 → 全量召回；翻 `MetadataVersionFilterEnabled` 前未回填源禁止激活；过渡期兼容过滤 `(version==active OR 缺 metadata_version)` 兜底不丢召回。

---

### 10.5 扫描范围必须贯穿 Ask 查询链（v10 通盘复核第 1 项）

**现状（已读，均确认）**：扫描器已区分 `CatalogName/SchemaName/TableName`（§10.1），但 **Ask 侧完全不携带这三键**——`QueryTable.cs:7-13` 仅 `TableName`；`QueryJoin.cs:28-154` 仅 `Left/RightTableName`；`SqlQueryBuilder.cs:43-59` 的 FROM/JOIN 与 `:191-208,351,493` 字段引用都只 `EscapeIdentifier(TableName)` 裸表名；所有 `new QueryTable` 构造点（`QueryPlanBuilder.cs:350-364,499`、`QueryPlanBuilder.SemanticResolution.cs:439,456`、`AppQueryExecutor.cs:145`、`QueryPlanSecurityGate.cs:96` 及单测）只设 `TableName`；`ISqlDialect.cs:15-89` 只有 `EscapeIdentifier/ApplyLimit/GetParameterName`，**无限定名构造器**。两库或两 schema 同名表时，元数据虽并存，Ask 仍可能查错表。

**改动点**：
1. **DTO 扩展**：`QueryTable` 增 `string? CatalogName`、`string? SchemaName`（可空，兼容历史单库源）。`QueryJoin` 已有 `LeftTableId`/`RightTableId`，用这两个 Id 分别解析对应 `QueryTable` 的物理键；不在 JOIN 上增加含糊的单组 catalog/schema 字段。
2. **方言限定名构造器（新增 `ISqlDialect.QualifyTable`）**：
   - SQL Server：`catalog` 非空 → `[catalog].[schema].[table]`；否则 `[schema].[table]`。
   - MySQL：`catalog`（=数据库）非空 → `` `catalog`.`table` ``；DTO 的 `schema` 必须为空或与 `catalog` 相同，不生成三级 ``catalog.schema.table``。不一致视为无效物理键并拒绝生成 SQL。
   - PostgreSQL：仅在 `catalog` 为空或等于当前**执行连接库**时用 `"schema"."table"`；不允许静默忽略指向其他库的 `catalog`（见第 5 条）。
3. **`SqlQueryBuilder` 改写**：
   - 按 `MetadataTableId` 为计划内每张表分配稳定、唯一的 SQL 别名（如 `t0`、`t1`）；重复表 Id 不重复分配，同名跨 schema/跨库的不同表 Id 必须有不同别名。别名只由服务端生成，不接受用户或模型提供的任意 SQL 片段。
   - FROM（`:44`）使用 `dialect.QualifyTable(t.CatalogName, t.SchemaName, t.TableName) AS t0`；JOIN（`:51-57`）按 `LeftTableId`/`RightTableId` 解析 `plan.Tables`，右表使用物理限定名 `AS tN`，ON 两侧统一用对应别名和转义列名。
   - SELECT/WHERE/GROUP BY/ORDER BY/HAVING 等字段引用（包括现有 `:191-208,351,493`）统一由表 Id 解析成 `tN.escaped_column`；只有字段名或裸 `TableName`、且在同名表中无法唯一对应表 Id 的计划必须拒绝并返回明确的歧义错误，不猜测默认 schema。
   - `NormalizeJoins`（`:86-116`）以表 Id 为主键校验 JOIN 两端确属本计划；同名跨 schema 的不同 Id 不误判为同一张表。仅对历史无 Id 的计划在三键唯一时兼容解析，三键仍不唯一则拒绝。
4. **所有 `new QueryTable` 构造点带 catalog/schema**（从已解析 `MetadataTable` 取）：
   - `QueryPlanBuilder.cs:351,499`、`QueryPlanBuilder.SemanticResolution.cs:439,456` → 带 `table.CatalogName/SchemaName`；
   - `AppQueryExecutor.cs:145`（lockedTable）→ 带元数据 catalog/schema；
   - `QueryPlanSecurityGate.cs:96`（投影）→ 透传 catalog/schema，不丢弃；
   - 单测构造处（`SqlQueryBuilderTests` 等）补 catalog/schema 覆盖同名表。
5. **PostgreSQL 执行库校验（单表也必须执行）**：执行层从实际连接串取得当前数据库名，在生成/执行 SQL 前逐张检查 `plan.Tables` 的非空 `CatalogName` 是否与其一致；任一不一致即拒绝该连接上的查询。多库 JOIN 一律拒绝；只查另一库的单表也不得忽略 catalog 后在当前库执行。若产品要支持另一库的单库查询，须先显式切换到该库连接并重新做授权/物理键校验，不在 `SqlQueryBuilder` 中暗换连接。MySQL/SQL Server 的跨 catalog JOIN 仍由限定名支撑（同一实例且具备权限）；PostgreSQL 同一 catalog 内多 schema JOIN 正常。

**测试场景**：
- 单测：`SqlQueryBuilder` 对带 catalog/schema 的两同名表生成 dialect 限定名（SQL Server `[db].[dbo].[t]` / MySQL `` `db`.`t` `` / PG `"sch"."t"`）。
- 单测：同 plan 内 SQL Server/PostgreSQL 的 `A.schema1.t` 与 `A.schema2.t` JOIN，及 MySQL 的 `db1.t` 与 `db2.t` JOIN → FROM/JOIN 物理限定名不同、两侧使用不同别名；SELECT/WHERE/GROUP BY/ORDER BY 的字段均绑定正确别名；歧义裸表名拒绝。
- 集成（**真实 MySQL/SQL Server**）：MySQL 两库、SQL Server 两 schema 的同名表各自 Ask → 结果仅来自所选物理表；`ScanScope.Databases=["db1","db2"]` 跨 catalog JOIN 正确。
- 集成（**真实 PostgreSQL**）：跨 catalog 两表 JOIN、在当前连接库查询另一 catalog 的**单表**均被拒；同 catalog 多 schema 同名表及 JOIN 正常。

### 10.6 PartiallySucceeded 激活门槛：向量完整性（v10 通盘复核第 2 项）

**现状（已读）**：§L.3 克隆时清空三实体向量状态 → 新版本行须重新索引；§L.6 重试路径沿用。但原 §L.5 激活仅按 SQL 终态（`Succeeded`/`PartiallySucceeded`）翻指针，**未规定向量完整性**——若某表必需的 table/column/semantic point 重试耗尽仍非 `Synced`，激活为 `PartiallySucceeded` 后该表新版本向量在「版本过滤」下消失，旧版本 point 又被 GC 删除 → 该表向量在新版彻底不可召回。

**改动点（激活向量完整性闸门）**：
- **双维度失败定义**：
  - **SQL 级失败**（表/列/语义/FK 读取或结构化失败）→ 允许 `PartiallySucceeded`（成功表入新版本、失败表保留旧版内容）。
  - **向量级失败**（某成功 SQL 的表，其实际应生成的 table/column/semantic point 在重试耗尽后仍无 `VectorId` 或 `VectorStatus != "Synced"`）→ **禁止**以 `PartiallySucceeded` 激活。
- **成功状态契约**：沿用现有 `MetadataVectorService` 的 `Pending/Synced/Failed/Stale`，成功值是 `"Synced"`，不是 `"OK"`；实现时集中封装状态常量/判定。对按现有索引规则确实不需要 point 的实体不要求 `Synced`，但不得把索引失败或缺失 point 当成“不需要”。
- **激活前置校验（与翻指针同事务前）**：激活前遍历新版本全部表及实际必需的三类 point，任一缺 `VectorId` 或状态不为 `"Synced"` → **阻断激活**：`ActiveMetadataVersion` 不变，任务置 `Failed` + `FailedReason="vector_index_incomplete"`，并把失败表/实体写入 `MetadataScanJobFailure` 供 C7 仅重扫失败项读取；旧版本 SQL+向量继续可查询。待重试的 `Pending` 同样不能越过闸门，须等重试完成或明确失败。
- **GC 按已激活版本清理**：仅当新版本完成向量闸门且指针已成功翻转，才清理旧版本 point；失败/取消批次不得触发旧 active point 的 GC。GC 依据已激活版本和持久待办幂等清理；不要依赖“旧版 point 留存”来补偿已激活版本的向量缺失，因为严格版本过滤仍不会召回旧版 point。
- **结论**：`PartiallySucceeded` 仅允许 **SQL 部分成功且必需向量全部 `Synced`**；向量不完整一律 `Failed`（保旧 active），绝不切换到不完整索引。

**测试场景**：
- 集成（**真实 Qdrant**）：某表向量重试耗尽 → 激活被阻、`ActiveMetadataVersion` 不变、Ask 仍用旧版（含该表向量，可召回）；该表在旧版正常。
- 集成：表 SQL 成功但向量延迟就绪 → 闸门等向量 `OK` 才激活；激活后新旧向量平滑切换、无消失窗口。
- 单测：索引服务写入的 `"Synced"` 可通过激活闸门，`Pending/Failed/Stale` 或缺 `VectorId` 不可通过；无须生成 point 的实体不误阻断；失败批次的 GC 不删旧 active point。

### 10.7 存量迁移与删除清理须覆盖无版本标签数据（v10 通盘复核第 3 项）

**现状（已读）**：`MetadataTable.CatalogName/SchemaName`（`:37,43`）可空、`MetadataVersion` 为新增列（存量默认 0）；`MetadataVectorService.cs:120-148` 的 point payload 无 `data_source_id`/`metadata_version`。§L.5b 靠物理键 `(CatalogName,SchemaName,TableName)` 做旧 ID→新 ID 重映射——存量 catalog/schema 为空则**无法对齐**；§L.7 删除 GC 按 `data_source_id` 定位 point——旧 point 无该字段则**删不到**，成孤儿。

**改动点**：
1. **迁移规范化存量物理键**：
   - `MetadataVersion` 新增列默认 0，存量行全 0 = 初始 active（与 `DataSource.ActiveMetadataVersion=0` 对齐）。
   - `CatalogName` 为空时先按数据源连接串确定连接数据库（MySQL `DATABASE()`/SQL Server 初始目录/PostgreSQL 连接数据库）；连接串无法确定或原扫描可能跨库时，按真实源库候选对象校验，不能直接把所有行归到默认库。
   - `SchemaName` 为空时逐表按 `(catalog, tableName, objectKind)` 查询真实源库：候选 schema 恰有一个才回填；SQL Server 的 `dbo`、PostgreSQL 的 `public` 仅在真实对象确实位于该 schema 时写入；MySQL 的 schema 与 catalog 同为数据库名。若候选为零个或多个、源库不可达、权限不足，记录结构化迁移异常清单并保持该源未就绪；不得静默默认到 `dbo/public` 或猜选同名表。
   - 未就绪源禁止新版本激活及依赖物理键的引用重映射，待人工确认物理键或源库恢复后幂等续迁移；不修改旧 active 的可见性。生产升级前先对存量源做候选数和连接可用性预检，备份并验证迁移回滚路径。
   - 唯一索引迁移校验：已解析行按 `(DataSourceId,CatalogName,SchemaName,TableName,0)` 校验唯一性；重复或未解析行进入异常清单，修复并重新校验后才视为该源迁移完成。
2. **存量向量补 `data_source_id`**：扩展 `MetadataVectorBackfillJob`（§L.4）除补 `metadata_version` 外，按 `metadata_id → 表 → ds` 反查写入 `data_source_id`（三类 point）；`VectorsBackfilled` 闸门（§10.4）据此保证删源前 point 可被 `data_source_id` 定位。
3. **删除 GC 对无标签 point 鲁棒（修 §L.7 第 141 行）**：`DELETE /cleanup` 事务内写 `MetadataVectorGcRequest` 时，**`PayloadJson` 额外持久化「待删 point ID 列表」**——删除前枚举该 ds 全部 `MetadataTable/Column/Semantic` 的 `VectorId`（`CreateStableVectorId(type,id)` 可重算），快照写入 GC 请求。GC 优先按 `data_source_id` 过滤删；对快照列表中的 ID 直接删除（覆盖从未回填、无 `data_source_id` 的旧 point）。
4. **删除前闸门**：`cleanup` 删除要求 `DataSource.VectorsBackfilled=true`（point 已带 `data_source_id`）**或** 删除事务内先同步跑一轮 backfill 打标；若两者皆否 → 返回 `409 + reason="vector_backfill_required_before_cleanup"`，避免删除后旧 point 成孤儿。

**测试场景**：
- 集成（**真实库 + Qdrant**）：旧数据分别含默认 schema 表、非默认 schema 表和两 schema 同名表；迁移只自动回填唯一可确定的物理键，歧义项列入异常清单且阻断该源激活；修复后版本=0、引用重映射正确；存量 point 经 backfill 带 `data_source_id`+`metadata_version`。
- 集成：**从未回填的旧 ds `cleanup` 删除** → 返 409（或同步 backfill 后删）；删除后 `MetadataVectorGcRequest.PayloadJson` 含该 ds 全部 point ID；重启后 GC 按 ID 删尽、无孤儿 point。
- 集成：删除进行中进程退出 → `MetadataVectorGcRequest` 持久 → 重启续删（覆盖 §L.7 场景 6）。

---

## 9. 测试补强（对应验收场景）

> §10 已为 v6 复核的 4 项（C2 数据契约 / §L.1 并发 / C4 非阻塞重入队 / VectorsBackfilled 归属）给出专属测试场景，本节为全集汇总。

- 单测：状态机；`ScanRetryPolicy`；同源去重；范围排除；`Sanitize`；读取器 `ct`+逐表；§L 种子复制四路径；唯一索引含版本不冲突；**`NextMetadataVersion` 事务原子分配（并发/空表/GC 后不重号）**；**克隆清空向量状态→`NeedsIndex` 重索引**；`VectorSearchFilter` 多源组合；`RetrieveVectorAsync` 回填；孤儿引用阻断/自动停用；`MetadataVectorGcRequest` 持久+幂等。
- 集成：`MetadataController` cancel/latest/retry-failed/DELETE 两档（Restrict 先清+写 GC 待办）/scanAfterCreate 三响应；`ScanStartupReconciler`；`MetadataVectorGcJob`/`MetadataVectorBackfillJob`（三类 point）；`VectorBackfillGate` 拒未回填源；部署两阶段标志翻切换行为。
- E2E：`DataSourceDetail` 取消→重启续显→失败项重扫→删除影响分析→Ask 隔离（§L.8 场景 1–10）。
- Golden：消费点过滤不得改变既有 Ask 结果（仅限未扫描/半扫描态）；激活后 RLS/PhysicalBinding 引用必须指向新版本行（§L.8 场景3）；克隆后三类向量可召回（场景9）；孤儿引用不静默丢失（场景10）。

### 9.1 真实后端五类集成验收（v10 最终验收门槛，Fake 单测不可证）

> 用户结论（v10 通盘复核）：上述三项（§10.5/§10.6/§10.7）与既有生命周期的跨系统契约，**必须用真实 MySQL / SQL Server / PostgreSQL / Qdrant 跑集成测试**；Fake 内存实现无法证明这些契约。以下五类场景为**强制验收门槛**，缺任一类即视为交付未闭环。

1. **同名表跨 schema Ask（§10.5）**：真实 SQL Server/PostgreSQL 在同一数据库的 `schema1`/`schema2` 各建同名表 `orders`，扫描两 schema，分别 Ask 并 JOIN → 结果与 SELECT/WHERE/GROUP BY/ORDER BY 字段只对应目标物理表；FROM/JOIN 用限定名，字段用按表 Id 分配的稳定别名、不串表。MySQL 的同名表放在两个数据库，纳入场景 2。
2. **跨库 Ask（§10.1 + §10.5）**：真实 MySQL/SQL Server 用 `ScanScope.Databases=["db1","db2"]` 逐库连接汇总扫描，同名表各自 Ask 和跨 catalog JOIN 返回正确结果；MySQL SQL 仅用 `database.table` 两级名。真实 PostgreSQL 跨 catalog JOIN、连接 db1 时单查 db2 表均被明确拒绝；同库多 schema 正常。
3. **向量单表失败（§10.6）**：真实 Qdrant 下，构造某表必需 point 索引重试耗尽（注入 `VectorErrorCode`）→ 激活被 `vector_index_incomplete` 阻断、`ActiveMetadataVersion` 不变、Ask 仍用旧版（含该表向量可召回）；旧 active point 不被 GC 删除；C7 仅重扫失败项后，必需 point 全部 `Synced` 才激活成功。
4. **存量迁移（§10.7）**：对**未升级的旧库**（catalog/schema 可空、point 无 `data_source_id`）跑迁移：默认与非默认 schema 的唯一候选准确回填；两 schema 同名表或源库不可达进入异常清单且不得误填/激活；修复后版本=0。跑 `MetadataVectorBackfillJob` → 存量三类 point 补齐 `data_source_id`+`metadata_version`；随后扫描激活，§L.5b 物理键重映射成功、引用不丢。
5. **删除后重启 GC（§10.7 + §L.7）**：对含旧 point（无 `data_source_id`）的 ds 执行 `cleanup` 删除（经 backfill 闸门或同步 backfill）→ 事务内 `MetadataVectorGcRequest.PayloadJson` 含全部待删 point ID；**kill -9 模拟进程在 GC 执行前退出** → 重启后 `MetadataVectorGcJob` 读持久待办、按 ID 删尽 Qdrant point、无孤儿。

- 环境：CI 提供四后端容器（MySQL 8 / SQL Server 2022 / PostgreSQL 15 / Qdrant latest）；五类场景以 `[Fact]`+`[Trait("Category","RealBackend")]` 标记，默认在 CI 真后端跑，本地可用 `RealBackend` 开关跳过。
- 不与 Fake 单测重复：Fake 仅验逻辑分支；跨系统契约（限定名/向量过滤/迁移/GC 持久）只认真实后端。
