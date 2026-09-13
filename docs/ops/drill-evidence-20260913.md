# 备份恢复演练证据 — 2026-09-13（真实数据级演练）

> 关联：M13-10 / DR-01｜配套：`docs/ops/backup-restore-dr.md`、`scripts/dr-backup/*`。
> 结论：**关系库（SQL Server）与 Qdrant 均完成真实"备份 → 隔离还原 → 一致性校验"闭环，全部通过。**
> 与 `drill-evidence-20260909.md`（架构级/脚本级）的关系：本文件是其**续篇**，补上了当年因
> "端口扫描误判环境不可达"而缺失的数据级演练（该误判已于 2026-09-10 勘误，见 09-09 文件 §E3）。

---

## 0. 演练环境

| 项 | 值 |
|---|---|
| 关系库 | SQL Server 2022 (RTM 16.0.1000.6) Developer Edition，`localhost`（命名实例/共享内存） |
| 关系库客户端 | `sqlcmd`（ODBC 17，`-I` + stdin 重定向调用） |
| 向量库 | Qdrant 1.19.0，REST `http://localhost:6333`，集合 `superbi_metadata`（1024 维 Cosine） |
| 源库 | `SuperBuilder_Platform`（数据文件 72 MB / 日志 72 MB；52 表 / 45 迁移） |
| 执行方式 | 演练脚本 = 本次新增的原生 T-SQL 路径（见 §5）；全程只读源库，还原落到独立目标库，**不动源库** |

---

## 1. 演练前基线（源库，实测）

| 指标 | 值 |
|---|---|
| 迁移数（`__EFMigrationsHistory`） | **45** |
| 用户表数（`sys.tables`） | **52** |
| 核心表行数 | Users 6 / Tenants 5 / MetadataTables 35 / MetadataSemantics 513 / MetadataColumns 513 / AskQuerySnapshots 37 / DataSources 1 / DataSourceAccessGrants 2 / AppPlans 15 / AppVersions 15 / AuditLogs 3044 / UiTextResources 8170 / RolePermissions 49 / Permissions 34 |
| Qdrant 点 | **1061**（`points_count`，status=green，8 segments） |

## 2. 关系库演练（备份 → 还原 → 校验）

### 2.1 备份
- 命令：`BACKUP DATABASE [SuperBuilder_Platform] TO DISK=... WITH INIT, COMPRESSION, CHECKSUM` + `RESTORE VERIFYONLY WITH CHECKSUM`（由 `scripts/dr-backup/backup-db-native.sql` 执行）。
- 产物：`SuperBuilder_Platform.bak`，**6,180,864 字节（≈5.9 MB，压缩）**，
  SHA256 `1bcb9838b515d29035a1b5751f2e903b102b9bccb3e82893ddf7b31b16ab645c`。
- **备份耗时：2,281 ms**（含 `sqlcmd` 启动 + BACKUP + VERIFYONLY 全校验）。
- 自校验：`RESTORE VERIFYONLY ... WITH CHECKSUM` 无错误 → 备份集完整性通过。

### 2.2 还原（隔离目标库）
- 命令：`RESTORE DATABASE ... WITH MOVE ... RECOVERY`（由 `scripts/dr-backup/restore-db-native.sql` 执行）。
- 目标库：`SuperBuilder_Platform_restored`（新库，与源库同实例、独立文件，不动源库）。
- **还原引擎耗时：0.124 s**（2346 页，147.775 MB/s）；**含 sqlcmd 启动的墙钟耗时 ≈4.0 s**。

### 2.3 一致性校验（还原库 vs 源库）

| 校验项 | 源库 | 还原库 | 结论 |
|---|---|---|---|
| 迁移数 | 45 | 45 | ✅ |
| 表数 | 52 | 52 | ✅ |
| Users | 6 | 6 | ✅ |
| Tenants | 5 | 5 | ✅ |
| MetadataTables | 35 | 35 | ✅ |
| MetadataSemantics | 513 | 513 | ✅ |
| MetadataColumns | 513 | 513 | ✅ |
| AskQuerySnapshots | 37 | 37 | ✅ |
| DataSources | 1 | 1 | ✅ |
| DataSourceAccessGrants | 2 | 2 | ✅ |
| AppPlans | 15 | 15 | ✅ |
| AuditLogs | 3044 | 3044 | ✅ |
| UiTextResources | 8170 | 8170 | ✅ |
| RolePermissions | 49 | 49 | ✅ |
| `DBCC CHECKDB ... WITH NO_INFOMSGS` | — | 无输出（无错） | ✅ 物理一致 |

### 2.4 可服务性（登录 / 授权 / Ask 数据就绪）
- 登录：`Users` 6 行中 **6/6 的 `PasswordHash` 与 `SecurityStamp` 均非空** → 账号凭据与令牌吊销量具可恢复。
- 授权：`Permissions` 34、`RolePermissions` 49、`Tenants` 5 → 鉴权数据完整。
- Ask/语义：`MetadataSemantics` 513、`MetadataColumns` 513 → 语义层可恢复（LLM 用密钥不在库内，见 §6）。

### 2.5 空库重放（"迁移即恢复"路径）
- 命令：新建空库 `SuperBuilder_SchemaDrill` → 重放 `scripts/dr-backup/restore-schema-from-migrations.sql`（幂等）。
- 结果：**46 迁移全部应用、52 表重建**（与源库表数一致）；`DataSources.ConnectionString` 为 **`nvarchar(max)`**（证明含 M13_03 迁移）。
- **重放墙钟耗时：5.87 s**，零错误。
- ⚠️ **脚本已修复**：该文件原为 2026-09-09 生成（仅 **42** 迁移，落后于当前 **46**，用于空库恢复会漏 3~4 个迁移）；
  本次用 `dotnet ef migrations script --idempotent --no-build` **重新生成至 46**（268 KB），并以上述重放验证之。

## 3. Qdrant 演练（快照 → 还原 → 校验）

### 3.1 快照
- 命令：`POST /collections/superbi_metadata/snapshots`。
- 产物：`superbi_metadata-7337019735908986-2026-09-13-06-34-09.snapshot`，
  **621,207,040 字节（592 MB）**，服务端 checksum `7d8d1cb233014e28914ed7f7f171be59d52fd8ce31d6274e14029565d68be6da`。
- **快照创建耗时：约 15.9 s**（引擎时间；592 MB 属大对象，故当年沙箱未下载）。

### 3.2 还原（非破坏式，落到新集合）
- 命令（服务端自取，无需 592 MB 客户端下载）：
  `PUT /collections/superbi_metadata_drill/snapshots/recover` body `{"location":"http://localhost:6333/collections/superbi_metadata/snapshots/<snap>"}`。
- **还原耗时：108.6 s（引擎）/ 109.1 s（墙钟）**。
- 校验：还原集合 `superbi_metadata_drill` → **status=green，points_count=1061**，与源集合 1061 完全一致。✅
- 安全性：还原落到独立集合，**源集合 `superbi_metadata` 全程 green / 1061 点未受影响**。✅

## 4. "失败步骤可定位"验证（DR-01 验收项）

演练中故意以**不存在的目标目录**执行还原，SQL Server 返回精确、可定位的错误链（非泛化失败）：
```
消息 5133 … 对文件"C:\dr_drill\SuperBuilder_Platform_restored.mdf"的目录查找失败，出现操作系统错误 2(系统找不到指定的文件。)
消息 3156 … 文件 'SuperBuilder_Platform' 无法还原为 '…\SuperBuilder_Platform_restored.mdf'。请使用 WITH MOVE 选项来标识该文件的有效位置。
消息 3013 … RESTORE DATABASE 正在异常终止。
```
→ 失败点（缺哪个目录/文件）、原因（OS 错误 2）、补救（WITH MOVE / 建目录）均在错误里定位。✅

## 5. 本次新增并已验证的可执行脚本

| 脚本 | 作用 | 验证 |
|---|---|---|
| `scripts/dr-backup/backup-db-native.sql` | SQL Server **原生**备份（`BACKUP` + `RESTORE VERIFYONLY WITH CHECKSUM`），无需 `sqlpackage` | 实跑通过，产出 §2.1 产物 |
| `scripts/dr-backup/restore-db-native.sql` | SQL Server **原生**还原到独立目标库（`WITH MOVE`），不动源库 | 实跑通过，还原库校验全绿（§2.3） |
| `scripts/dr-backup/restore-schema-from-migrations.sql` | 幂等全量建表脚本（空库"迁移即恢复"） | **重新生成至 46 迁移**，空库重放验证 46 迁移 / 52 表（§2.5） |

> 关键坑（已写入脚本头注释）：本仓库 `sqlcmd` 的 `-v` **以 `:` 作分隔符**，无法传 `C:\...` 路径
> （报「参数无效」）；路径类变量须写进脚本内 `:setvar`。且 `-i` 在 MSYS 下被改写，须用
> **stdin 重定向**（`sqlcmd ... < script.sql`）。目标目录须预先存在（T-SQL 不能建 OS 目录）。

## 6. 指标汇总与 RPO/RTO

| 组件 | 数据量 | 备份耗时 | 还原耗时 | 损失窗口（RPO）实测 |
|---|---|---|---|---|
| 关系库 | 72 MB（压缩后 5.9 MB） | 2.3 s | 0.124 s（引擎）/ ≈4 s（墙钟） | 上次备份后的增量（取决于调度频率，本演练为全量） |
| Qdrant | 1061 点 / 592 MB 快照 | 15.9 s | 108.6 s | 上次快照后的向量变更（可由元数据重嵌入兜底重建） |

- **RTO 实测远优于 `backup-restore-dr.md §3` 的推荐值**（关系库 30 min、Qdrant 1 h）——本环境数据规模小（元库 <100 MB）。
- ⚠️ **RPO/RTO 目标值仍待平台 owner 按 OPEN「性能与恢复目标」定稿**；本文件只提供**实测基线**，不作 SLA 承诺。

## 7. 未覆盖 / 残留

- **应用层端到端登录/发问**：本演练在**数据层**验证"登录/授权/Ask 所需数据齐备"，未启动 API 对还原库实际跑一次登录+Ask（还原库为源库逐字节复制，schema 由迁移保证一致，风险极低）。如需端到端证据，可在具备独立实例时把连接串指向还原库跑一遍冒烟。
- **密钥/配置**：不在库内（`Auth:SigningKey`/`Qwen:ApiKey` 等经密钥管理器下发），备份**不含**明文密钥（符合 §9 红线）。
- **异地副本/主备复制**：本演练覆盖"定期备份 + 手册恢复"轻量模式；主备/异地拓扑未在本环境演练。

### 7.1 演练中发现的相邻问题（不在 DR-01 范围内，需另行处置）

> **处置状态（2026-09-13 已归并）**：下列 1、2 两项均已修复并复核通过 —— 详见 `docs/ops/migration-seed-schemaversion.md §7.5`。
> 开发库已应用 **46** / 代码程序集 **46** / 清单 **46**，三方差集 0；`(Pending)`=0，`SchemaProbe` 不再判 `MigrationsPending`。以下为发现时的原始记录，保留备查。

1. **开发库落后代码 1 个迁移**：代码 / 模型快照为 **46** 迁移（末位 `20260912103000_M13_03_DataSourceConnectionStringEncryption`，
   SEC-01/M13-03，把 `DataSources.ConnectionString` 由 `nvarchar(2048)` 拓宽为 `nvarchar(max)`），
   而开发库 `SuperBuilder_Platform` 的 `__EFMigrationsHistory` = **45**（该迁移未应用，列仍为 `nvarchar(2048)`）。
   - 系统**有检测**：`SchemaProbe.ProbeAsync` 用 `GetPendingMigrationsAsync` 判定 `BootstrapState.MigrationsPending` → readiness=false（DB-01）；
     CI 在测试前执行 `dotnet ef database update`，故 CI 不受影响。
   - **建议**：对开发/部署库执行一次 `dotnet ef database update`（或用 §2.5 的幂等脚本升级），使库与代码一致。
   - **✅ 已执行**：`dotnet ef database update --no-build` 应用该迁移；列已为 `nvarchar(max)`。
2. **`scripts/schema/schema-version.json` 清单落后**：manifest `migrationCount=45`、`schemaVersion=20260911231209_M12_18_MetricDimensionExpression`
   （生成于 2026-09-11），早于 2026-09-12 的 M13_03 迁移 → 与代码的 46 不一致。
   - **建议**：按其 `generator`（`dotnet ef migrations list`）重新生成并回到 46。
   - **✅ 已执行**：清单重生成，`migrationCount=46`、`schemaVersion=20260912103000_M13_03_DataSourceConnectionStringEncryption`。

## 8. 清理（演练后环境复原）

演练产生的临时对象均已删除，环境恢复为演练前状态：
- SQL 库 `SuperBuilder_Drill`、`SuperBuilder_Platform_restored`（已 drop）
- Qdrant 集合 `superbi_metadata_drill`、快照 `superbi_metadata-…-2026-09-13-…snapshot`（已删）
- 临时 `.bak` / `.mdf` / `.ldf` 文件（已删）；源库 `SuperBuilder_Platform` 与源集合 `superbi_metadata` 保持在线且未被改动。

## 9. 证据物清单

| 物 | 说明 |
|---|---|
| `scripts/dr-backup/backup-db-native.sql` / `restore-db-native.sql` | 本次新增、已验证的原生备份/还原脚本 |
| `docs/ops/backup-restore-dr.md` | DR 计划（已修正脚本路径与迁移计数） |
| 本文件 | 2026-09-13 真实数据级演练证据 |
| `docs/ops/drill-evidence-20260909.md` | 架构级/脚本级历史证据（本文件的续篇起点） |
