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

## E8. 5 个 Pending 迁移已应用 + 迁移集缺陷发现与修复（诚实补遗）

E5 检出的 5 个 Pending 迁移因 **M7_02 迁移集缺陷** 无法按原样直接 `database update`，遂按用户授权「**重建开发库（最干净）**」执行：

### E8.1 发现：M7_02 是「全量建表」迁移（迁移集缺陷）
- `20260906141902_M7_02_AppVersion.cs` 的 `Up()` 含 **39 个 `CreateTable`**（Tenants/Users/AppPlans/AgentPlans/Dashboards/DataSources/MetadataTables 等），`Down()` 含对应 39 个 `DropTable`。
- 其中 38 张表已由早期 **P1~P10** 迁移创建，`M7_02` 仅 `AppVersions` 为**真正新增**。
- **证据（Grep 交叉验证）**：`AgentPlans` 同时被 `P9_1_AgentPlan.cs` 与 `M7_02` 创建；`AppPlans` 同时被 `P8_1_AppPlan.cs` 与 `M7_02` 创建。
- **实测冲突**：从空库全新 `dotnet ef database update` 时，M7_01 成功建 `DashboardVersions` 后，M7_02 的 `CREATE TABLE AgentPlans` 撞已存在表 →
  `Microsoft.Data.SqlClient.SqlException (0x80131904): 数据库中已存在名为 'AgentPlans' 的对象。Error Number:2714`。

### E8.2 修复（本地提交 `b99b3dd`，未推送）
- 重写 `M7_02_AppVersion.cs`：`Up()` 仅 `CreateTable AppVersions`（含 `FK_AppVersions_AppPlans_AppId` → `AppPlans` Cascade），`Down()` 仅 `DropTable AppVersions`。
- 净变更：**+5 / −1778 行**；删去 38 个重复 `CreateTable`/`DropTable`。
- ⚠️ **未改 `M7_02_AppVersion.Designer.cs`**：`database update` 仅按 `Up()` 操作建表、不读快照，故应用不受影响；但未来 `dotnet ef migrations add` 会基于旧 39 表快照做 diff，**可能生成意外迁移**（见 E8.5 待办）。

### E8.3 验证：重建后 42/42 干净落地
- 重建：`ALTER DATABASE SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE; CREATE DATABASE`（命名实例 `localhost`，库 `SuperBuilder_Platform`）。
- 应用：`ASPNETCORE_ENVIRONMENT=Local` 下 `dotnet ef database update` 失败于 M7_02（见 E8.1）；修正后于 `Development` 环境（内联 `Auth__SigningKey`/`Cors__AllowAnyOrigin=true`/连接串等）重跑成功，末行 `INSERT INTO [__EFMigrationsHistory] ... M7_11_PublishIdempotency` → `Done.`
- **sqlcmd 实测**（确证对象齐全）：
  - `HISTORY_COUNT = 42`（全部已应用）
  - 缺失对象现已存在：`AgentRuns`、`AppPublishIdempotencies`、`AppVersions`、`DashboardVersions`
  - `AppPlans.DraftRevision` 列存在（`DraftRevision=1`）；`TOTAL_TABLES=46`（含 `__EFMigrationsHistory`）

### E8.4 5 步种子实证（应用启动期）
启动实例（`Development` 环境）日志实证 5 步种子：
- `Platform identity catalog seeded.`（Roles=4 / Permissions=34 / Tenant `platform` 已建）
- `Localization catalog seeded.`（`UiTextResources=6560`）
- `Platform default quota & built-in theme seeded.`（`QuotaPolicies=7` / `Themes=1`）
- `Tenant UI language relationships seeded.`
- 步骤 5（平台管理员引导）因未配置 `PlatformBootstrap:Username/Password` **安全跳过**（非致命，`Users=0`）；需登录 E2E 时再配置。

### E8.5 待办 / 风险提示
- ✅ E5 的「5 个 Pending 迁移」已闭环（重建 + 42/42 应用 + 种子）。
- ⚠️ 建议随后**重生成 `M7_02_AppVersion.Designer.cs` 快照**（或 `dotnet ef migrations add` 时留意 diff），消除 E8.2 注释的潜在漂移。
- 在资源充足环境执行 `backup-qdrant.ps1` 完整备份（含 ≈592MB 快照下载）并用 `verify-backup.ps1` 校验。
- 考虑为 `backup-qdrant.ps1` 增加大文件下载健壮性（超时/流式下载/进度），避免 PS5.1 下大快照失败静默无产物。
- 种子幂等性：Identity / Quota-Theme / TenantLanguages 三步本次已实测成功，但**平台管理员账号**依赖 `PlatformBootstrap` 配置，尚未创建（待 E2E 凭据就绪）。

## E9. 迁移集缺陷续篇：`AppPlans` 4 列缺失 + 「空库重放」验证法（2026-09-10 补）

E8 修复了 M7_02 的 `CreateTable` 重复问题，但**同一迁移还遗漏了 `AppPlans` 的 4 个 `AddColumn`**，属 E8 未覆盖的残留缺陷。

### E9.1 发现路径（用户指定的方法论：模型 vs 数据库全量对比）
- 触发症状：`POST /api/apps/from-ask` 返回 **500 `SB_INTERNAL`**；API 日志 `DbUpdateException` → `SqlException Error Number:207`，列名 `PublishedAt` / `PublishedBy` / `PublishedDslJson` / `PublishedVersion` 无效。
- 根因：`20260906141902_M7_02_AppVersion` 的 `Up()` 在 E8 重写后**只创建 `AppVersions` 表**；注释自称「避免重复建表冲突」而删掉了 `AppPlans` 的 4 个 `AddColumn`。而 `SuperBIContextModelSnapshot` **已记录**这 4 列 → **快照有、迁移无**。
- 对照证据：`Dashboards` 表有对称 4 列（由 `M7_01_DashboardVersion` 正确添加），`AppPlans` 无 → 同批次迁移不对称。
- `dotnet ef migrations script --idempotent` 输出中，`AppPlans` 仅有 4 条 `ALTER TABLE`（`CreatedBy`/`RowVersion`/`UpdatedBy`/`UpdatedTime`）+ `DraftRevision`，**确认这 4 列在任何迁移路径中都不存在**。

### E9.2 修复（新增补丁迁移 20260910053000_M7_02_Fix_AppPlanPublishColumns）
- `Up()` 用 `IF COL_LENGTH('dbo.AppPlans','<列>') IS NULL` 守卫逐列 `ALTER TABLE ADD`，天然幂等；同时用 `IF NOT EXISTS(sys.extended_properties)` 守卫补 4 条 `MS_Description` 列注释。
- `Down()` `DropColumn` 4 列。
- Designer 由 `20260909025001_M7_11_PublishIdempotency.Designer.cs` 复制并替换 `[Migration]` 特性与类名生成（EF 运行迁移依赖 Designer）。
- ⚠️ 踩坑：`dotnet ef migrations add` 对本案**产出空迁移**（EF 对比的是上次快照，而快照已含 4 列，diff 为空）；`migrations remove` 因目标迁移已应用被拒。故手工创建迁移 + Designer，并 `git checkout -- SuperBIContextModelSnapshot.cs` 还原被改动的快照。
- 验证：`dotnet ef database update` 成功；`AppPlans` **15 列 → 19 列**；`from-ask` 由 500 变 **201**，`publish` 200。

### E9.3 验证法：**空库重放迁移 → 与模型快照做差集**（推荐纳入常规门禁）
> 用户原话：「迁移是否有遗漏的表或者字段，通过 dbcontext 和数据库结构对比不就可以辨别了吗？」
> 结论：**对，但必须比对「迁移集的产物」而非「已打过补丁的开发库」**——开发库可能已被手工修复，diff 会假绿。

- 权威判定 `dotnet ef migrations has-pending-model-changes` 输出 `No changes have been made to the model since the last migration.`
  —— **注意这不等于迁移完整**：该命令只比「快照 vs 当前模型」，本案快照本就含 4 列，故照样报「无变更」，**无法检出「快照有、迁移无」**。
- 真正有效的判据是**空库重放**：
  1. `dotnet ef migrations script --idempotent -o full.sql`（5808 行）；
  2. `CREATE DATABASE SuperBuilder_Platform_MigCheck`，`sqlcmd -I -b < full.sql`（**必须 `-I`**，否则 `CREATE INDEX` 因 `QUOTED_IDENTIFIER` 报 1934）；
  3. 解析 `SuperBIContextModelSnapshot.cs` 得「表 → 列」集合，与 `sys.tables`/`sys.columns` 做双向差集。
- 结果（修复后）：**A 模型有表/DB 无表 = 0；B 模型有列/DB 无列 = 0；C DB 有列/模型无列 = 0**。
- 结构对象计数交叉印证（开发库 vs 空库重放库，**7 项全等**）：

  | 指标 | SuperBuilder_Platform（开发库） | SuperBuilder_Platform_MigCheck（空库重放） |
  |---|---|---|
  | tables | 46 | 46 |
  | columns | 1797 | 1797 |
  | indexes | 326 | 326 |
  | foreign_keys | 45 | 45 |
  | default_constraints | 49 | 49 |
  | extended_properties | 317 | 317 |
  | __EFMigrationsHistory | 43 | 43 |

- 结论：**迁移集现与模型完全一致，无其他遗漏表/列**。验证库已 `DROP DATABASE`。

### E9.4 同批次一并修复的关联缺陷
- `QueryPlanMetadataValidator.FindColumns` 改为**主表优先**（先只在 `plan.Tables[0]` 内匹配列名，未命中才回退全表并保留歧义检测）：解决 `del_flag` 在 21 张 WMS 表共存导致的伪歧义 `SB_APP_002「Filter字段存在多个匹配:del_flag」`。
- `AppDataSourceBinding` 新增 `TableId`（`AppDsl.cs`）：应用是「已固化的查询」，目标表在导出时已确定，运行时不得再交语义检索重新选表。
- `AppQueryBindingExporter` 的 `Entity` 取值改为 `SemanticText → TableComment → TableName` 回退链（原先仅回退物理表名，导致语义检索召回无关表），并固化 `TableId`。
- `AppQueryExecutor` 新增 `LockPlanToTableAsync`：收敛 `plan.Tables` 为锁定表、清空 `Joins`、剔除越界字段/筛选/维度，并**以 binding 为权威回写排序与筛选字段名**；`AssertConsistentWithBinding` 失败时把具体原因写入 `InnerException`（对外仍是统一 422 `SB_APP_002`）。

## E10. M7-11 E2E 验收（P0-D2）实证 — 2026-09-10

双进程拓扑：API `:5032`（仅 `/api/*`）+ Web `:5080`（Blazor UI，**不转发 `/api/**`**）。

### E10.1 环境与账号真值（与代码注释不符，已更正）
| 项 | 值 |
|---|---|
| 租户 | `1=platform` / `2=demo(Enabled=0)` / `3=e2e` / `4=e2eapp` / `5=bizcheck` |
| 管理员 | `e2eadmin`（用户 4，租户 4，tenant-admin：含 `app:create/edit/publish`） |
| 读者 | `e2ereader`（用户 5，租户 4，viewer：仅 `app:view`，**无 `app:publish`**） |
| 数据源 | 全库仅 1 个：`Id=1 TenantId=4 WMS入库(MYSQL)`；授权仅租户 4（用户 4、5） |
| ⚠️ 陷阱 | 租户 3 另有同名用户 `e2ereader`（用户 3），**该租户无任何数据源** → Ask 必然 0 行 |

### E10.2 E2E 环境变量（修正后）
```
SB_E2E_BASE_URL=http://localhost:5080   SB_E2E_API_URL=http://localhost:5032
SB_E2E_USER=e2eadmin     SB_E2E_PASSWORD=longping00     SB_E2E_TENANT=4
SB_E2E_READER_USER=e2ereader  SB_E2E_READER_PASSWORD=longping00  SB_E2E_READER_TENANT=4
SB_E2E_ASK_QUESTION=列出最近十张入库凭证
```

### E10.3 修复的两个 E2E 阻断项
1. **登录 400**：`LoginHelper` 用相对路径 `fetch('/api/auth/login')` → 命中 Web 宿主（不转发 `/api/**`）。改为 `SB_E2E_API_URL` 绝对地址。实证：`curl :5080/api/auth/login` → 400，`:5032` → 200。
2. **`app-run[data-state="ok"]` 60s 超时 / API 日志 `/render` 请求数 = 0**：`AppRun.razor` 在 `OnAfterRenderAsync(firstRender)` 即取数，早于 `MainLayout` 的异步会话自举 → `State.TenantId=0` 致请求不发出。改为订阅 `State.SessionRestoredChanged`，待 `State.SessionRestored` 为真再 `TryLoadAsync()`（`_started` 幂等 + `IDisposable` 退订）；`AppPreview.razor` 同构修复。

### E10.4 结果
- `AppRuntimeE2ETests.Admin_Ask_Publish_TwoStage_Run_Renders`：**通过**（Ask → 发布草稿 → publish → `/apps/{code}/run` 渲染 `data-state=ok`）。
- `AppRuntimeE2ETests.Reader_Without_AppPublish_Cannot_SeePublishButton`：**通过**（`ask-publish` 计数 = 0）。
- 后端 `render` 端到端返回 **真实 WMS 数据**：`succeeded=true, rows=10, cols=8, row0.code="SI-20260910-0004"`。
- 全量单测 **1059/1059 绿、0 失败**（零回归）。

### E10.5 遗留（非本链路）
- `publish-box`（`data-testid=ask-publish-box`）仅在 `resp.Data.Rows.Count > 0` 时渲染 —— 这是 Reader 用例误配租户 3 时 60s 超时的**直接原因**，非 UI 缺陷。
- 其余 E2E 用例（AuthFlow / LanguageSwitch / PermissionDeny / PlatformLogin）的失败与本次改动无关，另行定位。

## E11. 硬加载深层路由导致「有效会话被误清」+ 观测盲区（2026-09-10 追加发现）

在排查是否存在**同形态的会话时序缺陷**时，用 Playwright 逐路由实测（每条路由独立登录，避免互相污染）。

### E11.1 现象
硬加载（等价 F5 / 直接输入 URL）9 条深层路由，**2 条被强制登出**：

| 路由 | 结果 |
|---|---|
| `/ask` `/apps` `/dashboards` `/data-sources` `/semantic-labels` `/model-accounts` `/admin/tenants` | ✅ 正常渲染（会话已还原） |
| `/agent` | ❌ 落到 `/login`（**登出**） |
| `/business-model` | ❌ 落到 `/login`（**登出**） |

### E11.2 根因链（两条独立缺陷叠加）
1. **取数早于会话自举**：页面自身 `OnAfterRenderAsync(firstRender)` 早于 `MainLayout` 的异步 `RestoreAsync`（子组件 `OnAfterRender` 先于父布局）。此时 `AppState.Token` 尚未写入 → `ApiClientBase.CreateClient()` **不带 Authorization 头** → API 返回 **401**。`Agent.razor` 还显式把 `State.TenantId` 放进查询串（`api/agent/plans?tenantId=0`）。
2. **401 误判为「会话过期」**：`ApiClientBase.OnUnauthorized()` 的判据是 `if (!AppState.IsAuthenticated) return;`。由于 401 是**异步返回**的，若在此期间 `MainLayout` 的自举已完成（`IsAuthenticated == true`），则该 401 被当成真实过期 → `ClearSession()` + `NotifySessionExpired()` → `MainLayout` 执行 `Session.ClearAsync()`（**清掉 localStorage**）+ `NavigateTo("/login")`。**一个"无令牌请求"的 401 杀死了刚还原成功的有效会话**（竞态，非确定性）。
   - 未受影响的路由只是"跑赢了竞态"（自举先于 401 处理完成），并不代表没有该缺陷。

### E11.3 附带发现：观测盲区（取证陷阱）
`src/Api/Program.cs` 中间件顺序：
```
591 UnifiedExceptionMiddleware → 593 ConcurrencyExceptionMiddleware → 595 AuditMiddleware
597 UseCors → 599 AuthMiddleware → 600 RateLimitMiddleware → 601 UseAuthorization
603 ObservabilityMiddleware   <- 日志 RES 在此产生
```
`AuthMiddleware` / `RateLimitMiddleware` / `UseAuthorization` **均早于** `ObservabilityMiddleware`，
因此 **401 / 403 / 429 一律短路，观测日志完全看不到**。

**实证**：
```
curl /api/agent/plans?tenantId=0  -> 401
curl /api/agent/tools             -> 401
观测中间件 RES 行数：调用前 143 → 调用后 143（新增 0）
```
> ⚠️ 教训：本项目 API 日志**不能**作为「无鉴权失败」的证据；判定 401/403/429 必须直接复现（curl/客户端）或调整中间件顺序后重测。

### E11.4 影响面（静态扫描）
按「页面级组件在 `firstRender` 内取数 且 无 `SessionRestored` 守卫」扫描，命中 **30 个页面**，例如
`/apps` `/apps/{Code}` `/dashboards` `/dashboards/{Id}` `/data-sources` `/data-sources/{Id}`
`/agent` `/agent/plans/{Code}` `/business-model` `/semantic-labels` `/themes` `/admin/*` 等。
`AppRun` / `AppPreview` / `Ask` 已有守卫（前两者为本次修复）。

### E11.5 修复（已实施并验证）
**已修复 1/3：会话误清（缺陷 1）—— 中心化一处收口**
- 改动文件：`SuperBuilder_AI.Components/Services/Clients/ApiClientBase.cs`
- 手法：新增 `private static readonly AsyncLocal<bool> RequestCarriedToken`，由 `CreateClient()` 在**发出请求时**写入「本次请求是否携带令牌」；`OnUnauthorized()` 增加首道判据
  `if (!RequestCarriedToken.Value) return;` —— **无令牌请求的 401 一律不视为会话失效**。
- 为什么用 `AsyncLocal` 而非实例字段：同一电路可能并发多请求，实例字段会互相覆盖；`AsyncLocal` 的写入只对当前异步流可见，天然按调用隔离。
- 为什么改基类而非逐点传参：`OnUnauthorized()` 全仓 **21 处调用点**（基类 4 + 派生客户端 17），改基类内部即可全覆盖，避免 17 处机械改动带来的评审噪音与回归面。
- 回归测试（`tests/SuperBuilder_AI.Tests/Clients/FocusedApiClientTests.cs`）：
  - `Tokenless_Request_401_Does_Not_Expire_Concurrently_Restored_Session` —— 在 Stub handler 内改写 `AppState.Token`，**精确复现竞态**（请求发出时无令牌、响应返回前自举已完成）。
  - `Token_Carrying_401_On_Base_GetJson_Path_Still_Expires_Session` —— 反向保障，携带令牌的 401 仍正常回收（覆盖基类 `GetJsonAsync` 路径；既有用例覆盖派生客户端原生 HttpClient 路径）。
  - **负向验证（证明测试不是橡皮图章）**：临时移除 `if (!RequestCarriedToken.Value) return;` → 该用例**失败**；恢复后通过。
- 端到端验证（Playwright 逐路由独立登录 + 硬加载，修复前后对照）：

  | 路由 | 修复前 | 修复后 |
  |---|---|---|
  | `/agent` | ❌ 落到 `/login` | ✅ OK |
  | `/business-model` | ❌ 落到 `/login` | ✅ OK |
  | 其余 7 条 | ✅ | ✅ |
  | **登出路由数** | **2 / 9** | **0 / 9** |

**未实施 2（待决策）：观测中间件前移**
- 直接前移会让 `TenantDataPlanePolicy.ReadObserved` 失去 `context.User`（由 `AuthMiddleware` 写入）→ REQ 日志的 `authTenant/effTenant` 退化为 0。**属需权衡的设计变更**，可选方案：
  (a) 前移并把 `ReadObserved` 改到 `finally`（响应阶段）读取，REQ 行不再带租户；
  (b) 保持顺序，改由 `AuthMiddleware`/`RateLimitMiddleware` 在拒绝时自行记录 401/403/429；
  (c) 前端埋点兜底（不解决服务端取证问题）。
- 补充：`Agent.razor` 把 `State.TenantId` 放进查询串（`api/agent/plans?tenantId=0`）本身也值得清理——租户应由令牌承载，不该由前端传值。

**未实施 3（待决策）：30 个页面的 `SessionRestored` 等待范式**
- 中心化修复只消除了"误清会话"，**未解决"取数过早拿不到数据"**（请求无令牌 → 401 → 页面数据为空）。
- 兜底方案：30 页统一采用 `AppRun` 的 `SessionRestoredChanged` 等待范式（`AppRun`/`AppPreview`/`Ask` 已具备）。
