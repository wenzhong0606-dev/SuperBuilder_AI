# SuperBuilder_AI 备份恢复与灾备演练（M9-14 → M13-10 / DR-01）

> 交付 M9-14｜G1 收口 M13-10（DR-01）｜验收口径：RPO/RTO 目标明确、步骤明确、演练证据明确。
> 本文档为运维级契约：定义**要备份什么、RPO/RTO 是多少、怎么备份、怎么恢复、怎么演练**。
> 配套脚本：`scripts/dr-backup/` —— 6 个 `*.ps1`（PS 5.1 解析校验通过）+ 2 个**原生 T-SQL**
> （`backup-db-native.sql` / `restore-db-native.sql`，2026-09-13 实测通过，见 `drill-evidence-20260913.md`）。

---

## 1. 范围与目标

- 目标：在 **数据库故障 / 误操作 / 环境重建 / 勒索加密** 四类场景下，能在既定 RTO 内恢复 SuperBuilder_AI 平台的**可服务状态**（元数据、向量、配置、种子一致）。
- 非目标：应用代码与二进制（由 Git + CI 制品库保障，不在本方案重复覆盖）；业务源库（WMS 等）由各自 owner 负责。
- 关键依赖：EF 迁移即"架构即真相"（schema-as-truth），任意空库可通过迁移重建全部表结构（见 §5 演练证据）。

## 2. 组件清单（要备份什么）

| 组件 | 技术 | 内容 | 重建方式 |
|---|---|---|---|
| 关系库 | SQL Server / MySQL / PostgreSQL（多引擎，`SuperBIContext`） | 租户、用户、权限、元数据、语义、App/Agent DSL、审计日志、i18n 等全部业务表 | ① EF 迁移建架构 → ② 备份数据还原 |
| 向量库 | Qdrant（集合 `superbi_metadata`，1024 维） | 元数据语义向量嵌入 | ① Qdrant 快照还原 ② 或自元数据重嵌入（兜底） |
| 配置 | `appsettings*.json` + 环境变量（`Auth:SigningKey`、`Qwen:ApiKey`、`Embedding:ApiKey` 等） | 运行时配置与密钥 | 配置仓库 + 密钥管理器（**严禁明文入库**） |
| 种子/语义 | `Document/Semantic.csv` 等 | 语义层种子数据 | 代码仓库（版本化） |
| 迁移程序集 | `SuperBuilder_AI.dll` 内含 EF 迁移 | 架构演进历史 | Git 仓库（commit 锁定） |

> 注：关系库默认引擎为 **SQL Server（`Server=localhost;Database=SuperBuilder_Platform`）**，同时支持 MySQL/PostgreSQL；脚本按连接串自动识别引擎。

## 3. RPO / RTO 目标（建议值，待平台 owner 裁定）

| 组件 | RPO | RTO | 依据 |
|---|---|---|---|
| 关系库 | **15 min** | **30 min** | BI/分析平台，非高并发交易；15min 增量/事务日志足够，30min 含还原+校验 |
| Qdrant 向量 | **4 h** | **1 h** | 向量可由元数据**重嵌入**重建（兜底路径），快照仅加速；RPO 可放宽 |
| 配置/密钥 | **0（实时）** | **15 min** | 存于配置仓库+密钥管理器，恢复=重新下发 |
| 种子/语义 CSV | **24 h** | **15 min** | 版本化于 Git，随发布回滚 |

> 上述为**推荐目标**，最终数值需平台负责人确认后写入本表（当前为交付基线，非 SLA 承诺）。
> **2026-09-14 O2 回填（平台 owner 确认）**：G1 单一平台级目标 **RPO ≤ 24 h、RTO ≤ 30 min** 已锁定，取代上表逐组件建议值作为对外口径；上表逐组件值保留为内部容量参考。最终定稿仍须按 ≥10× 规模（≤1000 表 / ≤50000 向量）实测复算（尤其 Qdrant 还原耗时随向量量增长）。
> **2026-09-13 实测基线**：本环境（元库 72 MB / 1061 向量点）关系库全量备份 **2.3 s**、还原 **0.124 s**，
> Qdrant 592 MB 快照还原 **109 s**——**远优于上表 RTO**；但数据规模小、非生产口径，仅作参考（见 `drill-evidence-20260913.md`）。

## 4. 备份策略（步骤明确）

### 4.1 关系库（按引擎）
- **SQL Server（已验证主路径，2026-09-13 实测，见 `drill-evidence-20260913.md`）**：原生 `BACKUP DATABASE ... WITH INIT, COMPRESSION, CHECKSUM` + `RESTORE VERIFYONLY`（经 `sqlcmd`），**无需额外工具**、备份即校验。
  ```powershell
  # 须用 stdin 重定向（-i 在 MSYS 等 shell 下被改写）；路径类变量请改脚本内 :setvar（sqlcmd -v 不接受含 ":" 的 Windows 路径）
  sqlcmd -S localhost -U <user> -P <pwd> -I -W < scripts\dr-backup\backup-db-native.sql
  ```
- SQL Server（可选，BACPAC 自含架构+数据）：`sqlpackage /Action:Export`；**前提：已安装 sqlpackage**（本仓库当前环境未安装，故主路径用上面的原生方式）。
  ```powershell
  .\scripts\dr-backup\backup-db.ps1 -ConnectionString $env:SB_DB_CS -OutputDir ./backups
  ```
- MySQL：`mysqldump --single-transaction --routines --events --triggers`
- PostgreSQL：`pg_dump -Fc`
- PowerShell 脚本按连接串自动识别引擎、产出 `<name>.bacpac|.sql|.dump` + `manifest.json`（SHA256 + 大小 + 时间）。

### 4.2 Qdrant 向量
- `POST /collections/{collection}/snapshots` 触发 → `GET` 下载 `.snapshot`：
  ```powershell
  .\scripts\dr-backup\backup-qdrant.ps1 -Host localhost -Port 6333 -Collection superbi_metadata -OutputDir ./backups
  ```

### 4.3 配置/种子/迁移
- 配置：仅归档**结构/占位符**文件（`appsettings.json` 等），**不含明文密钥**；密钥经密钥管理器下发。
- 种子/语义 CSV、迁移程序集：随 Git 仓库版本化（无需额外备份）。

### 4.4 全量编排与校验
```powershell
# 一次性产出"恢复点"：关系库 + Qdrant + 配置归档
.\scripts\dr-backup\backup-all.ps1 -DbConnectionString $env:SB_DB_CS -OutputDir ./backups
# 完整性校验（SHA256 + 大小比对 manifest）
.\scripts\dr-backup\verify-backup.ps1 -BackupDir ./backups/sb_rp_*
```

### 4.5 保留与加密
- 保留策略：每日全量 + 每 15min 事务日志（关系库），保留 30 天；Qdrant 每日快照保留 14 天。
- 备份文件落地后**加密静止存储**（AES-256 或对象存储服务端加密），传输走 TLS。
- 异地副本：至少 1 份跨可用区/异地（满足灾备地理分散）。

## 5. 恢复手册（有序步骤）

> 原则：先架构（迁移）→ 再数据（还原）→ 再校验（一致性）。避免"有数据无表"或"有表无种子"。

1. **准备**：确认目标实例可达、凭据就位、备份点（`sb_rp_*`）完整（`verify-backup.ps1` 通过）。
2. **架构重建（空库场景）**：在新库执行 EF 迁移（等同 `M7-11_apply_migrations.ps1` 的 `dotnet ef database update`）；或应用仓库自带幂等脚本 `scripts/dr-backup/restore-schema-from-migrations.sql`（**随迁移集自动生成、当前 46 个迁移**；2026-09-13 空库重放实测：46 迁移 / 52 表 / 5.9 s）。
3. **数据还原**：
   - 关系库（原生，主路径，2026-09-13 已验证）：`sqlcmd -S localhost -U <user> -P <pwd> -I -W < scripts\dr-backup\restore-db-native.sql`（还原到独立目标库，不动源库）
   - 关系库（BACPAC 可选）：`.\scripts\dr-backup\restore-db.ps1 -ConnectionString $env:SB_DB_CS -BackupFile <point>.bacpac`
   - Qdrant：`.\scripts\dr-backup\restore-qdrant.ps1 -SnapshotFile <point>.snapshot`；或用服务端非破坏式还原到新集合：
     `PUT /collections/<target>/snapshots/recover` body `{"location":"http://<host>:6333/collections/<src>/snapshots/<file>"}`（2026-09-13 实测：592 MB 快照 109 s，points 1061 一致）
4. **配置下发**：从配置仓库 + 密钥管理器恢复 `appsettings*.json` 与密钥（**绝不**从历史备份恢复明文密钥）。
5. **种子/语义**：随发布从 Git 拉取 `Document/Semantic.csv`。
6. **一致性校验**：
   - 关系库：`SELECT COUNT(*) FROM [dbo].[__EFMigrationsHistory]` 应等于**当前迁移总数（截至 2026-09-13 为 45）**；核心表行数与备份前一致；i18n 新键经启动自动播种。建议追加 `DBCC CHECKDB(<db>) WITH NO_INFOMSGS`。
   - Qdrant：`GET /collections/{collection}` 的 **`points_count`** 与备份前一致（Qdrant 1.19：`indexed_vectors_count` 为 0 属正常，应比对 `points_count`）。
7. **切换流量**：校验通过后，将流量切至恢复实例；旧实例隔离观察。

## 6. DR 拓扑

- **主-备（推荐）**：生产主实例 + 同区域异步副本（关系库 AlwaysOn/流复制；Qdrant 副本）。RPO 由复制延迟决定。
- **异地灾备**：跨可用区/异地第二副本，应对区域级故障。
- **无副本轻量模式**（当前沙箱/小部署）：定期备份 + §5 手册恢复，RTO 按 §3 目标。

## 7. 演练流程与证据模板

### 7.1 演练频率
- 全量演练：**每季度 1 次**；架构/迁移/脚本变更后 **必须** 追加一次。
- 轻量校验（备份可还原性）：**每月 1 次**（仅 `verify-backup.ps1` + 架构重建冒烟）。

### 7.2 演练步骤
1. 取最近一个恢复点（或故意污染测试库）。
2. 在**隔离测试实例**执行 §5 恢复手册。
3. 记录：起点时间、各步耗时、校验结果、RPO/RTO 实际值。
4. 填写下方证据模板，归档至 `docs/ops/drill-evidence-YYYYMMDD.md`。

### 7.3 证据模板
```
演练日期：____  执行人：____  环境：____（隔离测试实例）
恢复点：sb_rp_____  大小：____  SHA256：____
架构重建耗时：____  数据还原耗时：____
RPO 实测：____  RTO 实测：____  （对比 §3 目标）
一致性校验：关系库迁移数=当前总数(2026-09-13=45)? __  核心表行数一致? __  Qdrant points_count 一致? __
结论：通过 / 不通过（附偏差与整改）
```

## 8. 演练证据

> **⚠️ 勘误历史（2026-09-10，M9-15 期间发现）**：本节原结论「沙箱无可达关系库 / Qdrant」**均不成立**。
> 当时仅扫描标准端口（1433/5432/3306/6333/6334）未见监听即判定不可达，但本机 SQL Server 以
> **命名实例/共享内存**方式提供、Qdrant 亦在监听——`ss`/`netstat` 在本沙箱看不到宿主监听，
> **端口扫描法整体不可用**。勘误后：**关系库与 Qdrant 均可达**，M9-14 曾下的「环境门禁」结论作废。
> 当初未执行数据级演练属**排查方法缺陷**，非环境硬性阻塞。

### 8.1 历史证据（2026-09-09，架构级 / 脚本级）
1. **架构级演练（schema-as-truth）**：`dotnet ef migrations script --idempotent` 生成 `scripts/dr-backup/restore-schema-from-migrations.sql`（**当时 42 迁移 / 367 KB**），证明任意空库可由该脚本重建关系架构（"迁移即恢复"路径的真实证据）。**该脚本已于 2026-09-13 重新生成至 46 迁移 / 268 KB 并通过空库重放验证，见 §8.2。**
2. **脚本可用性（解析级）**：6 个 `scripts/dr-backup/*.ps1` 经 PowerShell 5.1 `Language.Parser` 解析 **PARSE_OK**（初版因 UTF-8 无 BOM + 中文误报失败，已统一 UTF-8 BOM 修复）。
3. 详见 `docs/ops/drill-evidence-20260909.md`。

### 8.2 真实数据级演练（2026-09-13，已完成 ✅）
**关系库与 Qdrant 均完成"备份 → 隔离还原 → 一致性校验"闭环，全部通过**；并新增并实测了原生
T-SQL 备份/还原脚本（`backup-db-native.sql` / `restore-db-native.sql`，无需 `sqlpackage`）。
- 关系库：45 迁移 / 52 表 / 全部核心表行数一致（Users 6、MetadataSemantics 513、AuditLogs 3044 …）/ `DBCC CHECKDB` 无错；**备份 2.3 s、还原 0.124 s（引擎）**。
- Qdrant：**592 MB 快照 → 非破坏式还原到新集合，109 s 后 `points_count=1061` 与源完全一致**；源集合全程 green 未受影响。
- 登录/授权/Ask 数据就绪：6/6 用户 `PasswordHash`+`SecurityStamp` 非空、Permissions 34、RolePermissions 49、MetadataSemantics 513。
- RPO/RTO 实测基线、失败可定位性验证、脚本坑（`-v` 冒号 / `-i` 重定向）、清理记录：见 `docs/ops/drill-evidence-20260913.md`。

## 9. 风险与红线

- **密钥**：备份/归档**绝不**包含明文 `Auth:SigningKey` / API Key；`appsettings.json` 本就为占位符（`__SET_VIA_ENV_OR_appsettings.Local.json__`），恢复时由密钥管理器下发。
- **不要从旧备份恢复密钥**：即使历史备份意外含密钥，恢复流程也必须以密钥管理器为准覆盖。
- **演练隔离**：全量演练必须在隔离测试实例执行，禁止对生产直接演练。
- **诚实声明**：本交付的"演练证据"为沙箱内可执行的架构级 + 脚本级证据；端到端数据级演练因环境阻塞未执行，已在 §8 明确标注，不夸大。
