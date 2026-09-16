# SuperBuilder AI — 项目长期记忆

> 事实来源：`docs/Master_Development_Plan.md` + `docs/milestones/Mn.md`；现状按 `docs/README.md` 权威顺序重核。
> 本文件只记「代码/文档里查不到、会反复踩」的事实。状态枚举仅 ACTIVE/BACKLOG/COMPLETED/SUPERSEDED/ARCHIVED/DEFERRED。

## 环境

- 栈：.NET 10 + EF Core 10 + Dapper；Qdrant 1.19（1024 维，集合 `superbi_metadata`）；Qwen（阿里云百炼）。
- 元库 SQL Server `localhost/SuperBuilder_Platform`（Uid=live Pwd=root）。
- Qdrant REST 6333 / gRPC 6334；异常退出残留 WAL 锁 → `Stop-Process -Force` 重启自愈。
- 后端起：`ASPNETCORE_ENVIRONMENT=Development` + `dotnet bin/Debug/net10.0/SuperBuilder_AI.dll`。**默认 Production 会因缺 `Auth:SigningKey` fail-fast**；`ASPNETCORE_URLS` 指定端口（默认验证用 5032）。
- 租户：1=platform / 2=demo(禁用) / 3=e2e / 4=e2eapp / 5=bizcheck；口令 `longping00`。
- 账号：`e2eadmin`=用户4/租户4；`e2ereader`=用户5/租户4；`platform-admin`@租户1 走 `/admin/login`（登录 UI 排除 platform 租户）。
- Python venv `~/.workbuddy/binaries/python/envs/default`（含 pymysql，无 mysql CLI），脚本写 `C:/tmp/x.py`。
- sqlcmd：`C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\sqlcmd`，**须 stdin 重定向 + `-I`**；含中文 `.sql` 加 `-f 65001`。
- **数据源与表归属（最易搞错，先查元库 `MetadataTables`）**：
  - 1 = WMS MySQL `192.168.16.120:3306/steccn_wms`（35 表：11 QRTZ_* + 24 wms_*，**无销售域** → 金额/品类问句低置信属预期）。
  - 2 = PMIS（289 表，含 `js_sys_*`、MES 备件仓 `mes_eqp_spare_*`）。
  - **4 = 另一 MySQL 源（`pms_*` 业务表）**：`pms_complete_storage`(397) / `pms_complete_storage_info`(398) / `pms_recipe`(420)。
  - ⚠️ 同一句「入库记录」在 ds=4 命中 `pms_complete_storage`/`pms_recipe`，在 ds=2 命中 `mes_eqp_spare_warehouse_enter` —— **问句必须带对 dataSourceId，否则召回的表完全不同**。

## 沙箱（Windows harness 实测）

- **Bash 工具 PATH 损坏**（`ls`/`git` command not found）→ 一律用 PowerShell 工具。`git.exe` 在 `…\binaries\PortableGit\versions\1.2.0\cmd\git.exe`，用户级 PATH 已修（PowerShell 内可裸调 `git`）。
- PowerShell **stdout 不回显** → 结果写文件再 Read。
- `dotnet` 走 `C:\Program Files\dotnet\dotnet.exe`（v10.0.301）构建/测试**正常**；Git Bash 内才会 NuGet 报错，勿据此判断沙箱不可构建。
- 构建唯一阻塞 = **运行中的 API 进程锁文件**（MSB3027/MSB3021）→ 先 `Stop-Process -Name SuperBuilder_AI -Force`。
- ⚠️ **跨工具调用的后台进程会被回收**：`run_in_background` 起的 API 在随后的前台命令结束后即消失。端到端验证必须**内联成单条 PowerShell**：`Start-Process` 起服务 → 轮询端口就绪 → 登录 → Ask → `Stop-Process`。
- `& C:\tmp\x.ps1` 静默不执行（无输出/无副作用）→ 长流程必须内联。
- 命令含 `%` 被安全扫描器整条拦截（误判 `%VAR%`）→ SQL 写文件走 `sqlcmd -i`。
- `*>` 重定向日志是 UTF-16 → `(Get-Content -Raw) -replace "\`0",""` 后再筛。
- `Invoke-RestMethod -Body <string>` 中文变 `????` → 传 `[Text.Encoding]::UTF8.GetBytes($json)` + `charset=utf-8`。
- `JsonSerializer` 默认把中文转义为 `\uXXXX` → 用 `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`。

## 文档治理与主线

- M13「产品化收口与企业试点门禁」= ACTIVE（G0 收口，推进 G1）；M14「商业化交付」= BACKLOG。
- 门禁：G0 = M13-01~07；G1 = G0 + M13-09/10/11/12/14/16/17/18 + M14-01/07/08a；G2 = G1 + M14-02~05（自助 SaaS 另加 M14-06）。
- 条件门禁（客户需要才升级）：M13-13 列权限 UI、M13-15 自定义组件、M13-08 Agent 真实执行、M10-01~03。
- 里程碑：M0–M8 / M12 = COMPLETED；M7/M9 = PARTIAL（尾项在 Backlog）；M10 = BACKLOG；M11 = DEFERRED。
- Active Plans 三份并存（README）：`2026-09-production-readiness.md`(G0)、`2026-09-g1-execution.md`(G1)、`2026-09-open-decisions-form.md`。⚠️ Master §4 仍写「仅一个 Active Plan」。

## CI（两个 workflow，职责不同，勿合并）

- `dotnet-build.yml` = **离线确定性门禁**（push/PR master + dispatch）：`changes` → `build` → `unit-tests`/`controller-runtime`(Golden C.13.3)/`e2e`(Playwright) → `quality-gate`。取 trx `total>0` 反假绿；限流注入 `RateLimit__LoginLimit=500`/`GlobalLimit=1000`（默认 10/120 会 429）；依赖 Docker（SQL2022+Qdrant），**不用真 LLM 密钥**。
- `ai-live-regression.yml` = **在线 LLM 回归**，仅 `workflow_dispatch`，跑 `tests/SuperBuilder_AI.LiveAI.Tests`，花真钱且有抖动 → 刻意排除出 PR 门禁。

## 测试基线

- 单测 `SuperBuilder_AI.Tests`：**1264/1264**（2026-09-16 本地全量，0 失败 0 跳过）。旧门槛 431/476/1015/1091/1140/1168/1229/1246/1261 全部过时。
- Golden 契约 18/18，禁止删/跳/降阈变绿。
- `RateLimitMiddlewareTests.Expired_Windows_Are_Evicted…` 时间敏感 flaky（全量偶发、单跑绿）。

## 迁移

- 开发库 45/45 已应用（末位 `20260911231209_M12_18_MetricDimensionExpression`），`scripts/schema/schema-version.json` 同步 45。
- 统计迁移禁用 `grep -v Snapshot`（误过滤 `M7_11_AskQuerySnapshot`），用 `grep -vE '\.Designer\.cs$|SuperBIContextModelSnapshot\.cs$'`。
- 新表一律 `dotnet ef migrations add`，勿手写。

## i18n 四处一致（`ResourceKeyRegistryTests` 护栏）

- 四处：`ResourceKeys.cs` / `Keys.cs` / `LocalizationSeedService.cs` / 消费 razor（`L10n.T(Keys.Content.X,"中文兜底")`）。
- 不变量：Catalog.DefaultValue=en-US；RCL `new(zh,en)` 首参=zh；seed=zh。

## SQL 生成 / 列限定（2026-09-16 修复）

- 现象：多表 JOIN 下 `WHERE del_flag = @p0` 报 **MySQL 1052 `ambiguous`**。
- 根因**不是** SQL Builder 单点，而是**写入端漏填表归属**：`QueryPlanBuilder.ApplyConventionalSoftDeleteFilter` 造 `QueryFilter` 时只给 `Field`；同语义的 `DetailQueryProjectionPolicy.ApplySoftDelete` 却正确填了 `MetadataTableId/TableName/MetadataColumnId` → **两份实现不一致**。
- 修复三处：① 写入端补齐三字段（对齐 DetailQueryProjectionPolicy）；② `SqlQueryBuilder.ResolveTableName` 增 `allowMainTableFallback`，**仅 WHERE / ORDER BY / GROUP BY 开启**（多表无归属时锚定主表 `Tables[0]`，口径与 `QueryPlanMetadataValidator.FindColumns` 的「主表优先」一致）；③ 新增 `QualifyIntentField` 处理 `Intent.OrderBy` / `Intent.Dimensions`。**SELECT 投影刻意不开启** —— `RowLevelSecurityTests.SqlBuilder_QualifiesRlsAcrossJoinAndAggregate` 锁定了「投影无法解析即裸列名」契约，动它会红。
- 实测复现（ds=4，结构与你截图完全一致）：`pms_complete_storage INNER JOIN pms_complete_storage_info ON create_time=create_time` + `WHERE del_flag`：报 1052 → **执行成功 rowCount=10**，SQL 变为 `` WHERE `pms_complete_storage`.`del_flag` = @p0 ``。
- ⚠️ **未修（D11/D12 已冻结项）**：该 JOIN 本身是错的（时间列相等不是业务关联）。来源链 `QueryPlanBuilder.BuildJoinsAsync → IQueryJoinInferenceService → QueryJoinCandidate → QueryPlan.Joins`；评分 = 类型一致 0.20 + 名称一致 0.45 + 语义 ≤0.25，**≥0.75 即产出候选** → `create_time` 恰好 0.90 通过。文档已冻结「JoinCandidate 只能作 Relation Evidence，不得作 Executable Join」。
- ⚠️ **未修**：`SELECT COUNT(type) … GROUP BY type` 未投影 `type` → 结果缺维度标签；且该维度标 `NotResolved`/`NotExecutable`，与「已被正确使用」自相矛盾。

## 译码链（详见 `docs/ops/field-decoding-and-correction.md`）

- 四层：① 跨源字典 ✅（10 列绑 PMIS `js_sys_dict_data`）② 同源 FK ❌（**WMS 库 FK 约束 = 0**，6115 列 `ReferencedTable` 全空）③ 学习规则 ✅ ④ 列注释 `ValueMapJson` ✅。
- ④ 写入方 `MetadataDiscoveryHeuristics.TryParseValueMapFromComment`（仅在 `ValueMapJson` 为空时写）：513 条注释接受 **30 条**、零误报。规则：≥2 条、首码值在前 20 字符内、码值递增且**步长 ≤3**（容忍缺号）、标签 ≤12 字符且不含数字。
- 替代 ② 的可用路线 = **同源去规范化映射**：`wms_inventory.warehouse_id→warehouse_name` **18/18**、`shelf_id→shelf_code/name` **645**。⚠️ 待裁决 `446`/`3759` 一名多值（`3759` 含「（作废）」）取重规则，**尚未接入**。
- 字典结构：`js_sys_dict_data`（44 列/2916 行）+ `js_sys_dict_type`；码值是 **bigint 雪花 ID**；映射 `dict_code→dict_label`，分类列 `dict_type`（129 类）。⚠️ 同一列码值**跨多个 dict_type**（`wms_storage_receipt.type` 跨 4 类）→ 单分类过滤必漏译，须用**贪心集合覆盖**推断。软删 `status`（0 正常/1 删除/2 停用）。
- ⚠️ **MySQL `tinyint(1)` 被驱动物化为 `bool`** → `DisplayResolutionService.NormalizeCode` 必须 `case bool b: return b ? "1" : "0"`，否则 `"True"/"False"` 永不匹配字典 `"1"/"0"`，译码**静默失效**（实测 `source_type`）。凡 0/1 码值列译码不生效，先查此处。
- `MetadataColumns.NativeType` 全库非空 = 0（写入方缺失）。
- 难译（非缺陷）：`wms_storage_receipt.shelf_id`（全 NULL）、`.status`（注释无图例）。
- 🔬 **判定「某列不可译码」规程（勿用表名 LIKE 猜）**：取真实值域 → 候选表候选键做**集合重叠**（真 FK 应 100% 覆盖）→ **语义校验**（`part_id`/`tree_sort` 不是仓库）→ **覆盖率校验**（`16/18` 且缺失恰为最大两值 = 稠密整数域巧合；实测假阳性 `mes_monitor_cfg.part_id`、`js_sys_dict_data.tree_sort` 均缺 `3759`/`5933`）→ 捷径：同表若有同语义 `*_name`/`*_code` 列，它**就是**可用的同源映射源。
- `js_sys_office`（PMIS）**不是仓库主数据**：表存在（16324 行）但**无 `id` 列**（PK = `office_code varchar(200)`，查 `id` 报 Unknown column），与 WMS 18 个仓库 id **交集为空**，语义是组织树。PMIS 的仓库语义属 **MES 备件仓**（`mes_eqp_spare_inventory`、`mes_eqp_spare_warehouse_enter`、`T_BAS_PRODUCT_STORAGE`），与 WMS 成品库/原材料库**不同主数据域**。

## 召回/理解层数据源作用域（2026-09-16 已修）

- 原缺陷：`MetadataSemanticSearchService.SearchAsync` 是**全局 top-K**（Qdrant payload 未写 `dataSourceId`）→ 跨源同名列进理解提示词 → 维度被解析到他源表。
- 修复：三条链透传 `dataSourceIds`（`IMetadataSemanticSearchService.SearchAsync(...,4参)` / `IMetadataContextBuilder.BuildAsync(q,ids)` / `IQueryUnderstandingService.UnderstandAsync(q,ctx,ids)`，三者均为**默认接口实现** → 既有实现类/测试替身零改动）；`BIConversationService.ResolveMetadataSearchScope` 口径 = 请求源 > 授权集合 > `null`（Golden 零回归）。
- 四不变量：① 过滤在**落库读回之后**按元库 `MetadataTable.DataSourceId`（**免重建向量**）；② **必须过采样** `topK×5`（上限 200），否则他源候选挤占名额；③ **空作用域 ≠ null**（空集合 → 空上下文 + 下游 403）；④ `null` 走原路径。
- 实测 6/6：0.742 Medium→**0.800 High**，`requiresConfirmation` True→False，维度落点 `wms_storage_receipt_info.quantity`→`wms_storage_receipt.type`。回归 `MetadataSearchScopeTests` 15 例。

## 其他陷阱

- 字典表自动发现：宽度闸门与角色解析**必须跑在「语义列」**（剔 `extend_*`/审计/`tree_*`/`parent_*`/`css_*`），否则 JeeSite 系 44 列字典表被永久误判为业务表。
- 重扫**不会**造成向量错位（2026-09-16 核实，推翻旧记录）：按表名原地更新，主键与稳定向量 ID（`SHA256("table:{id}")`，明令禁用 `Guid.NewGuid()`）均不变；仅源侧删表/列才走孤儿清理（有 RLS/学习保护名单）。
- 直连业务库：`DataSources.ConnectionString` 为 AES-256-GCM `v1:` 信封（12B nonce | ct | 16B tag），master key 在 `appsettings.Local.json:SecretStore:MasterKey`。
- Blazor/Razor：① 事件 C# 串用单引号；② 渲染变量 `@(x)`；③ void 须包 lambda；④ 注 `IApiClient`/域接口，注具体类 500；⑤ `<details>` 不支持 `@bind-open`；⑥ 子组件 `OnAfterRender` 早于 `MainLayout` 自举（等 `AppState.SessionRestored`）；⑦ 并行 build 同方案 MSB3030；⑧ `IJSRuntime.InvokeAsync` 须方法内首个 await；⑨ `ServerPrerendered` 无 JS，互操作移 `OnAfterRenderAsync(firstRender)`。
- 端点：登录 `POST /api/auth/login` `{username,tenantId,password}`；BI `POST /api/ask` `{question,dataSourceId}`（**无 `GET /api/datasources`，返回 404**）；多轮 `POST /api/ask/refine`；租户管理 `POST /api/tenant-management`；扫描 `POST /api/data-sources/{id}/metadata/scan`。
- EF/SQLite 测试：`Tenants.TenantCode` 唯一约束 → 多租户须显式给 TenantCode。
- 观测：`ObservabilityMiddleware` 在 Auth/RateLimit/Authorization **之前**，401/403/429 可见。
- 业务语义层：`BusinessEntityMetric` 继承 `BaseEntity`（无 TenantId）→ 隔离须经 `BusinessEntity.TenantId`；返回带反向导航会循环引用 → 一律只读投影 record。
- 签名密钥：缺 `Auth:SigningKey` 启动即抛异常；非 dev 拒绝旧默认值，无硬编码回退。
- 架构治理（M9-02/03）：四层 `src/`(Domain/Application/Infrastructure/Api)；命名空间双轨；迁移统一 `Infrastructure.Persistence.Migrations`；`ArchitectureTests` 4 不变量。
- 列权限：M12-18 仅行级 RLS；列级授权未提供 → M13-13（条件门禁）。
- 新租户语言：`POST /api/tenant-management` 走 `request.AvailableCultures ?? new[]{"zh-CN"}` → 新租户仅 zh-CN，语言切换器不出现（创建 UI 缺语言多选）。不在 G1。
- 术语治理：`SemanticLabels/BusinessDomains/BusinessEntities`=0（仅 `MetadataSemantics`=513）。

## 历史教训

- `MetadataCsvFixtureService.ImportAsync` 曾全局清空全部租户 → 已限自身租户子树。
- Golden 实时调 Qwen 有抖动（GQ-008/403）非环境问题；判定看 `expectedOutcomeSatisfied`。
- Git 历史重写：filter-repo 必后台跑；先 `git bundle` 备份。
- 文档状态须以源码/测试/CI 证据反推，不得凭记忆断言。
- 🔬 有效工作法：启用新启发式/阈值前，先在真实数据上**离线预演**（Python 跑同一算法看接受/拒绝名单）再落 C#。
