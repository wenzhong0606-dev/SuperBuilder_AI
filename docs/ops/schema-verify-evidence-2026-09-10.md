# Migration / SchemaVersion 校验证据 — 2026-09-10

> 关联：M9-15 / `docs/ops/migration-seed-schemaversion.md`。
> 环境：Windows + PowerShell 5.1 + .NET 10 SDK；**关系库（SQL Server, localhost）与 Qdrant（1.19.0, :6333）均可达**。
> ⚠️ 方法学教训：本沙箱内 `ss`/`netstat` **看不到宿主监听**，端口扫描判定可达性不可靠——必须做真实连接实测（本文件 E5/E7 均为实测）。

## E1. 权威迁移清单（Schema Version 基线）
- 命令：`dotnet ef migrations list --no-connect`
- 产出：`scripts/schema/schema-version.json`
- 结果：**42 个迁移**；`schemaVersion = 20260909025001_M7_11_PublishIdempotency`
- 首尾：`20260810081146_CreateDB` … `20260909025001_M7_11_PublishIdempotency`

## E2. 离线校验（正向）—— 真实执行通过
```
EXIT=0
[schema] 清单 schemaVersion=20260909025001_M7_11_PublishIdempotency count=42
[schema] 离线模式：迁移程序集 42 个迁移
[schema] OK 一致：schemaVersion=20260909025001_M7_11_PublishIdempotency（42 个迁移全部匹配）
```
含义：「迁移程序集」与「权威清单」完全一致，可在任何有 SDK 的机器（含 CI、离线沙箱）**重复执行**。

## E3. 离线校验（负向）—— 证明能检出漂移
构造：清单去掉末项（模拟漏提交/清单过期）。
```
EXIT=2
[schema] 清单 schemaVersion=20260909025001_M7_11_PublishIdempotency count=41
[schema] 离线模式：迁移程序集 42 个迁移
[schema] DRIFT 目标存在但清单未记录 (1):
   + 20260909025001_M7_11_PublishIdempotency
[schema] FAIL 发现漂移。
```
含义：工具**不是橡皮图章**，能真实检出缺失并以退出码 2 失败（CI 可据此阻断）。

## E4. 回退能力（真实生成降级脚本）
- 命令：`dotnet ef migrations script 20260909025001_M7_11_PublishIdempotency 20260908051650_M7_11_AskQuerySnapshot -o rollback-one-step.sql`
- 产出：`scripts/schema/rollback-one-step.sql`（643 字节）
- 内容核验：事务包裹，含 `DROP TABLE [AppPublishIdempotencies]`、`ALTER TABLE [AppPlans] DROP COLUMN [DraftRevision]`、`DELETE FROM [__EFMigrationsHistory] WHERE MigrationId = N'20260909025001_M7_11_PublishIdempotency'`
- 结论：迁移**具备可用 Down 路径**，回退可真实生成、可先审后执。

## E5. 在线实证 —— 关系库可达，并检出真实漂移
启动实例（`ASPNETCORE_ENVIRONMENT=Development`）后日志实证：
- 成功 `SELECT [MigrationId], [ProductVersion] FROM [__EFMigrationsHistory]`
- 5 步启动种子全部成功：`Platform identity catalog seeded.` / `Localization catalog seeded.` / `Platform default quota & built-in theme seeded.` / `Tenant UI language relationships seeded.`

`dotnet ef migrations list`（带连接）检出 **5 个迁移未应用（Pending）**：
```
20260906075147_M1_ClosureIntegrity
20260906131855_M7_01_DashboardVersion
20260906141902_M7_02_AppVersion
20260907004450_M7_03_AgentRuntime
20260909025001_M7_11_PublishIdempotency
```
含义：本机开发库落后于代码，是**真实的"旧环境待升级"样本**；校验工具在真实环境上有效。
**未擅自应用这些迁移**（避免改动开发库状态），待确认后再升级并复验。

## E6. 安全修复：Microsoft.OpenApi 高危漏洞（M9-13 引入的依赖）
- 发现：构建期 NuGet 审计 `NU1903` —— `Microsoft.OpenApi 2.0.0` 存在已知**高严重性漏洞**（GHSA-v5pm-xwqc-g5wc），由 M9-13 新增的 `Microsoft.AspNetCore.OpenApi 10.0.10` 传递引入。
- 处置：csproj 显式提升 `Microsoft.OpenApi` 至 **2.7.5**（本地 NuGet 缓存已有，离线可解析）。
- 验证：
  - 构建 **0 错误**，且 `NU1903` 告警**消失**。
  - 全量单测 **1059/1059 绿**（0 回归）。
  - M9-13 契约端点复验：`GET /openapi/v1.json` HTTP 200，与已提交契约**结构化完全一致**（3.1.1 / 162 paths / 200 operations / 全部 security + 401/403/422 / `ApiError` 存在）。

## E7. Qdrant 实证（经用户指出后复核 —— 更正「不可达」结论）
- **可达性**：`curl http://localhost:6333/` → **HTTP 200**，`{"title":"qdrant - vector search engine","version":"1.19.0"}`。
- **集合**：`GET /collections` → 仅 `superbi_metadata`；`GET /collections/superbi_metadata` → `points_count=961`、`indexed_vectors_count=0`、`vectors{size:1024,distance:Cosine}`、`status=green`。
- **快照 API（curl）**：`POST /collections/superbi_metadata/snapshots` → `status=ok`，产出
  `superbi_metadata-...-2026-09-10-00-58-27.snapshot`，**size=621,157,888（≈592MB）**，带 `checksum`。
- **快照 API（PowerShell 路径）**：`Invoke-RestMethod -Method Post .../snapshots` → `PS_CREATE_STATUS=ok`、
  `PS_CREATE_NAME=superbi_metadata-...-00-59-14.snapshot`、`PS_CREATE_SIZE=621157888`
  → 证明 `scripts/dr-backup/backup-qdrant.ps1` 所用的 PowerShell 调用路径**真实可用**。
- **清理**：已删除全部 2 个快照（≈1.2GB），`GET /snapshots` 返回 `[]`，未占用磁盘。
- **未完整执行的部分（诚实标注）**：`backup-qdrant.ps1` 的**下载步骤**未在沙箱跑通——
  单次快照 ≈592MB，PS5.1 `Invoke-RestMethod -OutFile` 在沙箱资源下未产出文件（脚本 manifest 在下载后才写，故无产物）。
  **属体积/资源限制，非能力缺失**：快照创建已双向（curl/PowerShell）验证成功，下载需在资源充足环境执行。
- **对 M9-14 的影响**：M9-14 曾记「Qdrant 不可达 → Qdrant 侧演练为环境门禁」，该结论**作废**（已在三处文档加勘误）。

## 待办（需确认后执行）
- 对开发库应用 5 个 Pending 迁移（`dotnet ef database update`），随后重跑在线校验闭环 —— **需用户确认**（会改动开发库结构）。
- 在资源充足环境执行 `backup-qdrant.ps1` 完整备份（含 ≈592MB 快照下载）并用 `verify-backup.ps1` 校验。
- 考虑为 `backup-qdrant.ps1` 增加大文件下载健壮性（超时/流式下载/进度），避免 PS5.1 下大快照失败静默无产物。
- 种子幂等性：另 3 步（Identity / Quota-Theme / TenantLanguages）目前依据注释声明，未逐一源码回查，可另立子项补做。
