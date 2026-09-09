# SuperBuilder_AI 备份恢复与灾备演练（M9-14）

> 交付 M9-14｜验收口径：RPO/RTO 目标明确、步骤明确、演练证据明确。
> 本文档为运维级契约：定义**要备份什么、RPO/RTO 是多少、怎么备份、怎么恢复、怎么演练**。
> 配套脚本：`scripts/dr-backup/*.ps1`（6 个，已在 Windows PowerShell 5.1 解析校验通过）。

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

## 4. 备份策略（步骤明确）

### 4.1 关系库（按引擎）
- SQL Server：`sqlpackage /Action:Export`（BACPAC，自含架构+数据）
  ```powershell
  dotnet tool install --global dotnet-sqlpackage
  .\scripts\backup\backup-db.ps1 -ConnectionString $env:SB_DB_CS -OutputDir ./backups
  ```
- MySQL：`mysqldump --single-transaction --routines --events --triggers`
- PostgreSQL：`pg_dump -Fc`
- 脚本自动识别引擎、产出 `<name>.bacpac|.sql|.dump` + `manifest.json`（SHA256 + 大小 + 时间）。

### 4.2 Qdrant 向量
- `POST /collections/{collection}/snapshots` 触发 → `GET` 下载 `.snapshot`：
  ```powershell
  .\scripts\backup\backup-qdrant.ps1 -Host localhost -Port 6333 -Collection superbi_metadata -OutputDir ./backups
  ```

### 4.3 配置/种子/迁移
- 配置：仅归档**结构/占位符**文件（`appsettings.json` 等），**不含明文密钥**；密钥经密钥管理器下发。
- 种子/语义 CSV、迁移程序集：随 Git 仓库版本化（无需额外备份）。

### 4.4 全量编排与校验
```powershell
# 一次性产出"恢复点"：关系库 + Qdrant + 配置归档
.\scripts\backup\backup-all.ps1 -DbConnectionString $env:SB_DB_CS -OutputDir ./backups
# 完整性校验（SHA256 + 大小比对 manifest）
.\scripts\backup\verify-backup.ps1 -BackupDir ./backups/sb_rp_*
```

### 4.5 保留与加密
- 保留策略：每日全量 + 每 15min 事务日志（关系库），保留 30 天；Qdrant 每日快照保留 14 天。
- 备份文件落地后**加密静止存储**（AES-256 或对象存储服务端加密），传输走 TLS。
- 异地副本：至少 1 份跨可用区/异地（满足灾备地理分散）。

## 5. 恢复手册（有序步骤）

> 原则：先架构（迁移）→ 再数据（还原）→ 再校验（一致性）。避免"有数据无表"或"有表无种子"。

1. **准备**：确认目标实例可达、凭据就位、备份点（`sb_rp_*`）完整（`verify-backup.ps1` 通过）。
2. **架构重建（空库场景）**：在新库执行 EF 迁移（等同 `M7-11_apply_migrations.ps1` 的 `dotnet ef database update`）；或应用仓库自带幂等脚本 `scripts/dr-backup/restore-schema-from-migrations.sql`（42 个迁移全量建表，已生成，可在全新库重建整个关系架构）。
3. **数据还原**：
   - 关系库：`.\scripts\backup\restore-db.ps1 -ConnectionString $env:SB_DB_CS -BackupFile <point>.bacpac`
   - Qdrant：`.\scripts\backup\restore-qdrant.ps1 -SnapshotFile <point>.snapshot`
4. **配置下发**：从配置仓库 + 密钥管理器恢复 `appsettings*.json` 与密钥（**绝不**从历史备份恢复明文密钥）。
5. **种子/语义**：随发布从 Git 拉取 `Document/Semantic.csv`。
6. **一致性校验**：
   - 关系库：`SELECT COUNT(*) FROM __EFMigrationsHistory` 应等于 42；核心表行数与备份前一致；i18n 新键经启动自动播种。
   - Qdrant：`GET /collections/{collection}` 的 `vectors_count` 与备份前一致。
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
一致性校验：关系库迁移数=42? __  核心表行数一致? __  Qdrant vectors 一致? __
结论：通过 / 不通过（附偏差与整改）
```

## 8. 沙箱内已完成的演练证据（本交付）

本环境为开发沙箱，**无可达的关系库 / Qdrant 实例**（探测 1433/5432/3306/6333/6334 均无监听；仅 `sqlcmd` 客户端存在，mysql/pg 客户端缺失）。因此**端到端数据级演练不可在沙箱执行**（与 M7-11 P0-D1/D2 同类环境阻塞）。

在沙箱内**已真实执行并产出**的可验证证据：
1. **架构级演练（schema-as-truth）**：`dotnet ef migrations script --idempotent` 成功生成 `scripts/dr-backup/restore-schema-from-migrations.sql`（**367KB / 42 个迁移全量建表 + 迁移历史**），证明任意空库可通过该脚本重建完整关系架构。→ 这是"迁移即恢复"路径的真实证据。
2. **脚本可用性（解析级）**：6 个 `scripts/dr-backup/*.ps1` 经 Windows PowerShell 5.1 `System.Management.Automation.Language.Parser` 解析，**全部 PARSE_OK**（修复了初版在 PS5.1 下因 UTF-8 无 BOM + 中文导致的误报解析失败，已统一改为 UTF-8 BOM 编码）。
3. **幂等迁移 SQL 实测**：`dotnet ef` 工具链 10.0.10 可用，迁移总数 42，脚本生成零错误。

**待环境就绪后的真实演练**（环境门禁，非本交付缺陷）：
- 在具备可达 SQL Server / Qdrant 的环境，按 §5 + §7.3 执行全量演练并填证据模板。
- 建议与 M7-11 P0-D1/D2 验收共用一套环境准备。

## 9. 风险与红线

- **密钥**：备份/归档**绝不**包含明文 `Auth:SigningKey` / API Key；`appsettings.json` 本就为占位符（`__SET_VIA_ENV_OR_appsettings.Local.json__`），恢复时由密钥管理器下发。
- **不要从旧备份恢复密钥**：即使历史备份意外含密钥，恢复流程也必须以密钥管理器为准覆盖。
- **演练隔离**：全量演练必须在隔离测试实例执行，禁止对生产直接演练。
- **诚实声明**：本交付的"演练证据"为沙箱内可执行的架构级 + 脚本级证据；端到端数据级演练因环境阻塞未执行，已在 §8 明确标注，不夸大。
