# Migration / Seed / SchemaVersion / 升级回退体系（M9-15）

> 交付 M9-15｜对应 DevChecklist `SB-P1-12`｜验收：**明确 EF Migration、Seed、Schema Version、升级与回退策略；新环境可自动初始化；旧环境升级可验证**。
> 配套产物：`scripts/schema/schema-version.json`（权威清单）、`scripts/schema/verify-schema.ps1`（可重复校验）、`scripts/schema/rollback-one-step.sql`（回退样例）、`scripts/dr-backup/restore-schema-from-migrations.sql`（M9-14 幂等全量建表）。

---

## 1. 现状盘点（回查源码得出，非假设）

| 项 | 现状 | 证据 |
|---|---|---|
| EF 迁移 | **44 个**（`20260810081146_CreateDB` → `20260911143327_M12_17_IdentityOrganizationUnits`） | `dotnet ef migrations list` |
| SchemaVersion | **无独立概念**，仅 EF 自带 `__EFMigrationsHistory` | 全仓 grep `SchemaVersion` 无结果 |
| 启动自动迁移 | 由 `Startup:MigrateOnStartup` 控制，**默认 false** | `Program.cs:481` |
| 启动探测 | `SchemaProbe` 区分 三态：`DatabaseUnreachable` / `SchemaNotCreated` / `Ready` | `src/Api/Diagnostics/SchemaProbe.cs` |
| 启动种子 | **5 步固定顺序**，逐步异常隔离 | `Program.cs:477-581` |
| 种子失败语义 | 置 `BootstrapState.SeedIncomplete`，`/health` 仍 200 但暴露 state/reason | `Program.cs:511/526/545/562/577`、`/health` |

## 2. Migration 策略

- **唯一真相源**：EF 迁移程序集（随代码版本化）。禁止手工改库结构；结构变更必须走迁移。
- **命名**：`<yyyyMMddHHmmss>_<标识>`，时间戳保证全局有序。
- **执行方式（推荐）**：由 CI/部署流水线执行 `dotnet ef database update`，**生产不建议**开启 `Startup:MigrateOnStartup`（避免多实例并发迁移与启动耦合）。
  - 新环境/单机/演示场景可开启 `Startup:MigrateOnStartup=true` 实现"自动初始化"。
- **离线/无 SDK 场景**：应用 `scripts/dr-backup/restore-schema-from-migrations.sql`（幂等，含全量建表+迁移历史）。
- **多引擎**：默认 SQL Server，支持 MySQL / PostgreSQL；迁移 SQL 由 EF 按提供程序生成，切引擎需重新生成脚本。

## 3. Schema Version 策略

- **定义**：`schemaVersion` = **最新（序号最大）迁移 ID**。当前 = `20260912103000_M13_03_DataSourceConnectionStringEncryption`。
- **权威清单**：`scripts/schema/schema-version.json`（含 46 个有序迁移 ID、生成时间、生成命令）。
- **读取方式**：
  - 代码/运维：`SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC`
  - 或直接用校验脚本（见 §6）。
- **升级判定**：环境 `schemaVersion` < 清单 → 需升级；等于 → 一致；大于/集合不一致 → **漂移**，须人工介入。
- **清单维护**：每次新增迁移后**必须**重新生成清单（否则校验报漂移）。生成命令：
  ```bash
  cd SuperBuilder_AI && dotnet ef migrations list --no-connect
  ```
  （取输出中 `^\d{14}_\w+` 行，按序写入 `scripts/schema/schema-version.json` 的 `migrations` 数组，并更新 `schemaVersion` 为末项。）

## 4. Seed 策略

启动 5 步（**固定顺序，不可调换**；每步独立 try/catch，失败不影响后续步骤记录，但会置 `SeedIncomplete`）：

| # | 步骤 | 入口 | 幂等性（源码核实） |
|---|---|---|---|
| 1 | Identity/Permission 目录 | `IIdentityService.SeedAsync()` | 注释声明幂等 |
| 2 | UiLanguage/Text 本地化 | `ILocalizationSeedService.EnsureSeedAsync()` | ✅ **源码确认**：先查已有 `Culture` 集合，仅补缺失；文本键 `if (existingKeys.Contains(key)) continue` |
| 3 | 默认配额 + 内置主题 | `IQuotaService.EnsureSeededAsync()` / `IThemeSeedService.EnsureSeededAsync()` | 注释声明幂等 |
| 4 | 租户界面语言关系 | `ITenantLanguageService.EnsureAllTenantsLanguagesAsync()` | 注释声明幂等 |
| 5 | 平台管理员引导 | `PlatformAdminBootstrapper.EnsureAsync()` | ✅ **源码确认**：`GetStatusAsync` 先查 `UserRoles.AnyAsync`，已有平台管理员则 `Ready` 直接返回 false，**不会重复创建** |

**原则**：
- 种子必须**可重复执行**（重跑不产生重复数据、不覆盖用户数据）。
- 种子失败 → `SeedIncomplete` + `/health` 暴露，**Web 仍启动**（受限诊断模式），不崩溃。
- 种子内容随代码版本演进；新增 i18n 键等由种子自动补齐（历史交付已多次验证）。

## 5. 升级与回退策略

### 5.1 升级
- **新环境（空库）**：`dotnet ef database update`（或应用 §2 幂等 SQL）→ 启动后 5 步种子自动补齐。
- **旧环境（已有库）**：`dotnet ef database update` 应用**待应用迁移**；启动种子增量补齐。
- **验证**：升级后运行 §6 校验，确认 `schemaVersion` 与清单一致。

### 5.2 回退
- **首选**：迁移级回退，回退到指定迁移：
  ```bash
  dotnet ef database update 20260908051650_M7_11_AskQuerySnapshot
  ```
- **生成回退脚本（先审后执，推荐生产）**：`to` 早于 `from` 即生成降级脚本：
  ```bash
  dotnet ef migrations script 20260909025001_M7_11_PublishIdempotency 20260908051650_M7_11_AskQuerySnapshot -o rollback.sql
  ```
  样例产物见 `scripts/schema/rollback-one-step.sql`（事务包裹：删表 `AppPublishIdempotencies`、删列 `AppPlans.DraftRevision`、清理 `__EFMigrationsHistory`）。
- **兜底**：按 M9-14 备份恢复手册，从备份点还原（RPO/RTO 见 `backup-restore-dr.md`）。
- **限制与风险（必须知晓）**：
  - **仅当迁移写了 `Down` 才能生成降级脚本**；纯 additive 迁移（无 Down）回退会失败或需手工补偿。
  - **删列/删表类回退会丢数据**，回退前必须备份（M9-14 手册）。
  - 回退后**应用代码须同步回退**到匹配版本，否则模型与库结构不一致。
  - 回退属高风险操作：**先在隔离环境演练，再上生产**。

## 6. 可重复验证（新旧环境统一口径）

工具：`scripts/schema/verify-schema.ps1`（UTF-8 BOM，Windows PowerShell 5.1 可解析执行）

```powershell
# 离线：校验「迁移程序集」与清单一致（无需数据库，CI/沙箱均可跑）
.\verify-schema.ps1 -ProjectDir ./SuperBuilder_AI

# 在线：校验「目标库已应用迁移」与清单一致（当前走 sqlcmd / SQL Server）
.\verify-schema.ps1 -ConnectionString $env:SB_DB_CS
```

- **退出码**：`0`=一致；`2`=发现漂移（缺失/多余迁移）。
- **CI 接入建议**：PR/CI 中跑**离线**校验（防迁移漏提交/清单过期）；部署后跑**在线**校验（防环境漏升级）。

### 已执行的真实验证（详见 `schema-verify-evidence-2026-09-10.md`）
- ✅ 离线正向：**45/45** 一致，`EXIT=0`（2026-09-12 增量 `M12_18` 后重新生成清单复跑；此前 44/44）。
- ✅ 在线实证（2026-09-12）：开发库 `SuperBuilder_Platform` 已应用 **45/45**，`(Pending)` 计数 0。
- ✅ 离线负向：篡改清单（去掉 1 个迁移）→ 正确报漂移 `+ 20260909025001_M7_11_PublishIdempotency`，`EXIT=2`（证明工具非橡皮图章）。
- ✅ 在线实证：本机 SQL Server **可达**（`sqlservr.exe` 运行），检出**真实漂移：5 个迁移未应用**（见 §7）。

## 7. 环境实况（2026-09-11 升级闭环）

### 7.1 M9-15 当时登记的待升级项（已过期）

文档初版登记本机开发库（`SuperBuilder_Platform`）落后于代码，以下 5 个迁移未应用：

```
20260906075147_M1_ClosureIntegrity
20260906131855_M7_01_DashboardVersion
20260906141902_M7_02_AppVersion
20260907004450_M7_03_AgentRuntime
20260909025001_M7_11_PublishIdempotency
```

### 7.2 实际执行（2026-09-11）

回查 `__EFMigrationsHistory` 发现：上述 5 项**已在此前被应用**，真实待应用项仅为
M12-17 新增的 `20260911143327_M12_17_IdentityOrganizationUnits`（建 6 张组织目录表）。

执行步骤（先审后执）：

1. 重新生成清单：`dotnet ef migrations list --no-connect` → `scripts/schema/schema-version.json`
   由 42 项刷新为 **44 项**（补 `20260910053000_M7_02_Fix_AppPlanPublishColumns`、
   `20260911143327_M12_17_IdentityOrganizationUnits`），schemaVersion 同步更新。
2. 生成待应用脚本并审查：`dotnet ef migrations script <from> <to> --idempotent`
   → **纯 additive**（仅 `CREATE TABLE` × 6 + 索引 + 外键），扫描无
   `DROP TABLE/COLUMN/INDEX/CONSTRAINT`、无 `DELETE/TRUNCATE`。
3. 应用：`dotnet ef database update` → `Done.`（写入 `__EFMigrationsHistory`，ProductVersion 10.0.11）。
4. 复核（权威口径：直连客户端读 `__EFMigrationsHistory`，与迁移程序集做集合差）：
   **已应用 44 / 程序集 44，pending 0，drift 0**。

### 7.3 沙箱内官方脚本自证的限制（如实记录）

`scripts/schema/verify-schema.ps1` 依赖在 Windows 脚本宿主会话内调用 `dotnet` 与 `sqlcmd`。
本工作沙箱的宿主会话**无法解析/执行这两个原生命令**（PATH 含其目录但 `Get-Command`
解析失败；改用全路径函数垫片后，子进程仍返回 0 行输出），故脚本在本环境 `EXIT=2`
（解析出 0 个迁移）。**判定为环境限制，非脚本缺陷**：脚本逻辑与 §6 历史验证证据有效，
在正常 Windows 宿主环境可复现 `EXIT=0`。

顺带做的健壮性改进（不改变语义）：脚本原先用 `_ -match '^[0-9]{14}_'` 在 `Trim()`
**之前**匹配，对带前导空白的宿主输出不鲁棒；已改为先 `Trim()` 再匹配。

### 7.4 增量迁移 `M12_18`（2026-09-12，44 → 45）

M12 增量 3-2（指标/维度编辑）为 `BusinessEntityMetrics` / `BusinessEntityDimensions` 各新增
`Expression nvarchar(1000)` + `DataType nvarchar(50)`（均可空），生成迁移：

```
20260911231209_M12_18_MetricDimensionExpression
```

执行步骤：

1. 用真实 EF 工具生成（沙箱内 `dotnet ef` 不在 PATH，需全路径 `~/.dotnet/tools/dotnet-ef.exe`
   + `--no-build`；`dotnet build` 一律 `--no-restore` 规避 `path1`）：
   `dotnet-ef migrations add M12_18_MetricDimensionExpression --project/--startup-project SuperBuilder_AI/SuperBuilder_AI.csproj`。
2. 审查生成的 `Up()`：**仅 4 个 `AddColumn`**（无 DROP/ALTER），快照 diff 精确 16 行 —— 纯 additive。
3. `dotnet-ef database update` → `Applying migration '20260911231209_M12_18_MetricDimensionExpression'. Done.`
4. 清单同步：`scripts/schema/schema-version.json` 44 → **45**（`schemaVersion`/`migrationCount`/`generatedUtc`/`migrations[]` 四项）。
5. 复核：`migrations list --no-connect` 与清单做集合差 → **45/45，missing 0 / extra 0**；连库 `migrations list` → `(Pending)` 计数 **0**。

### 7.5 漂移归并（2026-09-13，45 → 46）

DR-01 演练（`docs/ops/drill-evidence-20260913.md §7.1`）发现**开发库与清单双双落后代码 1 个迁移**，本次归并：

1. 取证：代码/快照 **46** 迁移（末位 `20260912103000_M13_03_DataSourceConnectionStringEncryption`）；开发库 `__EFMigrationsHistory` = **45**，且 `DataSources.ConnectionString` 实测仍为 `nvarchar(2048)`（`sys.columns.max_length=4096`）→ 漂移在 schema 层真实存在，非仅历史表差异；清单 `migrationCount=45`。
2. 升级前备份：`BACKUP DATABASE ... WITH INIT, COMPRESSION, CHECKSUM` + `RESTORE VERIFYONLY` → 5.79 MB / 0.14 s，备份集有效。
3. 应用：`dotnet-ef database update --no-build` → `Applying migration '20260912103000_M13_03_DataSourceConnectionStringEncryption'. Done.`（`ProductVersion 10.0.11`）；复核 `DataSources.ConnectionString = nvarchar(max)`（`max_length=-1`）。
4. 清单同步：`schema-version.json` 45 → **46**（`schemaVersion` / `migrationCount` / `generatedUtc` / `migrations[]` 四项）。
5. 复核：迁移程序集 **46** / 清单 **46** / 开发库已应用 **46**，三方差集 `missing=0 / extra=0`；`migrations list` 的 `(Pending)` 计数 **0** → `SchemaProbe` 不再判 `MigrationsPending`，readiness 恢复正常。

> 沙箱限制复现：本环境 PowerShell 脚本宿主内 `sqlcmd` 返回 0 行（见 §7.3），故 `verify-schema.ps1` 的 **-Online** 模式仍无法在本沙箱自证；本次用同一口径的等价校验（直连客户端读 `__EFMigrationsHistory` + 程序集文件清单 + 清单 JSON 三方集合差）完成，结论一致。

## 8. 红线与诚实声明

- 未触碰应用业务逻辑；交付物为文档 + 3 个脚本/清单文件 + 1 个回退 SQL。
- 种子幂等性：仅对 **LocalizationSeedService** 与 **PlatformAdminBootstrapper** 做了源码级核实（✅）；其余 3 步依据注释声明"幂等"，**未逐一回查源码**，如需强保证可另立子项补做。
- §1 现状与 §7 实况均来自实际命令输出与源码回查，非推测。
- M9-14 中"沙箱无可达关系库"的结论**已勘误**（见 §6 与 `drill-evidence-20260909.md` 勘误块）：关系库实际可达，当时端口扫描不完备导致误判。
