# SuperBuilder AI — 项目长期记忆

> 只记「代码/文档查不到、会反复踩」的事实。状态枚举：ACTIVE/BACKLOG/COMPLETED/SUPERSEDED/ARCHIVED/DEFERRED。

## 环境
- 栈：.NET 10 + EF Core 10 + Dapper；Qdrant 1.19（1024 维，集合 `superbi_metadata`）；Qwen（阿里云百炼）。
- 元库 SQL Server `localhost/SuperBuilder_Platform`（Uid=live Pwd=root）。
- 后端起：`ASPNETCORE_ENVIRONMENT=Development` + `dotnet bin/Debug/net10.0/SuperBuilder_AI.dll`；Production 缺 `Auth:SigningKey` fail-fast；端口 `ASPNETCORE_URLS`（验证用 5032）。
- 租户：1=platform / 2=demo(禁用) / 3=e2e / 4=e2eapp / 5=bizcheck；口令 `longping00`。
- **数据源与表归属**：1=WMS MySQL `192.168.16.120:3306/steccn_wms`（35 表，无销售域）；2=PMIS（289 表，含 `js_sys_*`、`mes_eqp_spare_*`）；4=另一 MySQL（`pms_*`：`pms_complete_storage`/`pms_recipe`）。同句「入库记录」跨源召回表不同 → 问句必须带对 dataSourceId。

## 沙箱（Windows harness）
- 一律用 PowerShell（Bash PATH 损坏）；stdout 不回显 → 结果写文件再 Read。
- 构建唯一阻塞=运行中的 API 进程锁文件 → 先 `Stop-Process -Name SuperBuilder_AI -Force`。
- 跨工具后台进程被回收 → 端到端验证内联单条 PowerShell（Start-Process→轮询→登录→Ask→Stop-Process）。
- 命令含 `%` 被拦截 → SQL 走 `sqlcmd -i`；UTF-16 日志先 `(Get-Content -Raw) -replace "\`0",""`；中文 body 传 `UTF8.GetBytes` + `charset=utf-8`。

## 文档治理 / CI / 测试
- M13=ACTIVE(G0/G1)，M14=BACKLOG。CI：`dotnet-build.yml`(离线门禁) + `ai-live-regression.yml`(在线 LLM，仅 dispatch)。
- 单测 1269/1269；Golden 18/18。`RateLimitMiddlewareTests.Expired_Windows…` 偶发 flaky。
- **全量 dotnet test 耗时 ~1m51s > 后台默认 120s 上限**：必须显式给 `timeout`（600000），否则日志被掐断只剩几百字节、无汇总，容易误判。
- 迁移 45/45 已应用；新表一律 `dotnet ef migrations add`（勿手写）。

## 扫描 / 译码 / 关键陷阱
- 扫描端点：`POST /api/data-sources/{id}/metadata/scan`；**重扫安全**（按表名原地更新，稳定向量 ID=SHA256("table:{id}")，禁 Guid.NewGuid()）。
- 直连业务库 `DataSources.ConnectionString` = AES-256-GCM `v1:` 信封，master key 在 `appsettings.Local.json:SecretStore:MasterKey`。
- 多表 JOIN `WHERE del_flag` 报 MySQL 1052 ambiguous → 已修（写入端漏填表归属，补 MetadataTableId/TableName/MetadataColumnId + WHERE/ORDER BY/GROUP BY 主表兜底；SELECT 投影不动）。
- 列译码失效先查 `tinyint(1)` 物化 bool → `NormalizeCode` 须 `case bool b: "1"/"0"`。
- 字典 `js_sys_dict_data` 码值=bigint 雪花 ID，跨多个 dict_type → 须贪心集合覆盖；单分类过滤必漏译。
- 同源 FK 不可用（WMS FK=0）→ 用同源去规范化映射（如 `warehouse_id→warehouse_name` 18/18）。
- i18n 四处一致：`ResourceKeys.cs`/`Keys.cs`/`LocalizationSeedService.cs`/razor 消费（护栏 `ResourceKeyRegistryTests`）。
- Blazor/Razor：事件 C# 串用单引号；`void` 包 lambda；注 `IApiClient`/域接口（注具体类 500）；`<details>` 不支持 `@bind-open`；互操作移 `OnAfterRenderAsync(firstRender)`；`ServerPrerendered` 无 JS。
- 端点：登录 `POST /api/auth/login`；BI `POST /api/ask`；无 `GET /api/datasources`（404）；租户 `POST /api/tenant-management`；扫描 `POST /api/data-sources/{id}/metadata/scan`。

## 置信度（QueryPlanConfidence）
- 优先级顺序（`DetermineConfidenceLevel`）：**硬安全门**（ValidationError/Repair 异常 → Low）→ **保底**（`TableCorrectionHonored || LearningApplied` → score≥0.80 给 High，否则 Medium）→ **无语义证据禁 High** → 阈值（High 0.80 / Medium 0.60）。
- 得分 = 可用证据**加权归一化**（`weightedScore/availableWeight`）；权重 Semantic .20 / Table .15 / Field .15 / Metric .15 / Dimension .10 / Filter .10 / Validation .10 / RepairStability .05。
- **造 Low 基线**：用 `EmptyMetadataSemanticSearchService` 时只剩 Validation+RepairStability，归一化恒 ≈1.0 → 基线永远是 Medium，**测「保底」会空转**。须让语义替身返回一条低分命中（`VectorType="semantic"`, `Score≈0.20`, `Table`/`Column`=null）→ 得分 ≈0.543 → Low。
- 保底语义 = **只抬下限、不封顶**；断言写 `>= Medium`，不要写 `== Medium`。
- 扩接口不破坏既有实现：用 **默认接口方法（DIM）** 加 5 参重载委托旧 4 参（本项目惯用，测试替身免改）。
