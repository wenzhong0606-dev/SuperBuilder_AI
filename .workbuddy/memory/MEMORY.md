# SuperBuilder AI — 项目长期记忆

> 唯一事实来源：`docs/Master_Development_Plan.md` + `docs/milestones/Mn.md`。
> 本文件只记「代码/文档里查不到、会反复踩」的事实。除标注「已核实」外，均属**历史快照**，
> 现状一律按 `docs/README.md` 权威顺序重核：源码/测试 → Master → Milestone → Active Plan → Backlog。

## 权威顺序（docs/README.md）
1 当前源码/迁移/测试 → 2 Master → 3 milestones/ → 4 plans/active/ → 5 Backlog → 6 产品/架构/运维规范 → 7 archive/ 与 `.workbuddy/`（历史，不得覆盖现状）。
状态枚举仅 `ACTIVE/BACKLOG/COMPLETED/SUPERSEDED/ARCHIVED/DEFERRED`。

## 环境与工具链
- .NET 10 + EF Core 10 + Dapper；Qdrant 1.19（1024 维，集合 `superbi_metadata`）；Qwen LLM。
- SQL Server 元库 `localhost/SuperBuilder_Platform`（Uid=live Pwd=root）；业务库 WMS MySQL `192.168.16.120:3306/steccn_wms`（35 表：11 QRTZ_* + 24 wms_*，**无销售域** → 销售额/品类类问句低置信或 `SB_BI_002` 属预期）。
- Qdrant：`~/Downloads/qdrant-x86_64-pc-windows-msvc/qdrant.exe`，storage 同目录 `./storage`；REST 6333 / gRPC 6334。异常退出残留 WAL 锁 → `Stop-Process -Force` 后重启自愈。
- 后端起：`ASPNETCORE_URLS=http://localhost:5032 dotnet bin/Debug/net10.0/SuperBuilder_AI.dll`。
- 租户：1=platform / 2=demo(禁用) / 3=e2e / 4=e2eapp / 5=bizcheck；口令统一 `longping00`；平台管理员 `platform-admin`@1。
- E2E 账号：`e2eadmin`=用户4/租户4；`e2ereader`=用户5/租户4（须共用租户4 数据源；租户3 无数据源 → Ask 必 0 行）。
- Python venv：`~/.workbuddy/binaries/python/envs/default`（含 pymysql；无 mysql CLI）。传脚本路径给 python 必须写 `"C:/tmp/x.py"`，`/c/tmp/x.py` 被 MSYS 转成 `C:\c\tmp\x.py`。
- sqlcmd：`/c/Program Files/Microsoft SQL Server/Client SDK/ODBC/170/Tools/Binn/sqlcmd`，**须 stdin 重定向**（`-i` 被 MSYS 破坏）且加 `-I`。
- 无 `gh` CLI。CI 触发/轮询走 REST：令牌取自 `git credential fill`（GCM 的 `gho_*`），勿 echo；`C:/tmp/ci_check.py`、`ci_jobs.py <run_id>`。⚠️ 后台 shell 里 python 管道 `| head` 会零输出（块缓冲），须前台跑。

### 沙箱特有坑（会阻塞一切构建）
- ⚠️ **`dotnet restore/build/test` 全失败**根因：`Environment.GetFolderPath(CommonApplicationData)` 返回 null → NuGet `XPlatMachineWideSetting`/`ConfigurationDefaults` 静态构造抛 `Value cannot be null (path1)`。**`NUGET_PACKAGES`/`ProgramData` 等 env 均无效**（非 NuGet.config 问题）。
  绕过：预编译 DLL 直接 `dotnet vstest <dll>`；构建统一 `build --no-restore` + `test --no-build`。CI 干净环境不受影响。
- ⚠️ 用 EF 工具前先 `export APPDATA="C:\Users\ThinkPad  X1\AppData\Roaming"`，再直调 `"$HOME/.dotnet/tools/dotnet-ef.exe"`（dotnet-ef 10.0.10 已全局安装）。
- ⚠️ 沙箱内 `ss`/`netstat` 看不到宿主监听 → 可达性必须真实连接实测，端口扫描结论不可信。
- ⚠️ 宿主 `HTTP_PROXY/HTTPS_PROXY=127.0.0.1:53963` → .NET gRPC 连 localhost 报 HTTP/2 失败。`QdrantService` 已改 `SocketsHttpHandler{UseProxy=false}`（**勿回退**）。
- ⚠️ `MSB3027/MSB3021` 文件锁多为**自己先前起的宿主进程**；Git Bash 下须 `MSYS_NO_PATHCONV=1 MSYS2_ARG_CONV_EXCL='*' taskkill /F /PID <pid>`。
- ⚠️ push 凭据会过期，报 `could not read Username ... terminal prompts disabled` 时用 `git -c credential.helper=manager push origin master`（`helper-selector` 仅交互可用）。

## 文档治理与当前主线（2026-09-12 核实）
- Master 主线：M13「产品化收口与企业试点发布门禁」= ACTIVE；M14「商业化交付与经营闭环」= BACKLOG（G1 后启动主体）。
- 唯一 Active Plan：`docs/plans/active/2026-09-production-readiness.md`（Phase A=G0 六项 / Phase B=G1 / Phase C=Release）。任务状态只在 Backlog 更新，Plan 不复制测试数字与 SHA。
- 门禁：G0 = M13-01~07；G1 = G0 + M13-09~12/14/16 + M14-01/07/08；G2 = G1 + M14-02~05（自助 SaaS 另加 M14-06）。
- 条件项：M13-13 列权限 UI、M13-15 自定义组件（客户需要才升级为门禁）；M9-12 四项目拆分跳过；M10-01~03 保原编号。
- 承接关系：M7-12→M13-07/08、M9-14→M13-10、M9-15→M13-04、M7-11+M12 交互验收→M13-09。
- 里程碑状态：M0–M8 COMPLETED；M7/M9 PARTIAL（尾项在 Backlog）；M12 COMPLETED（含两项产品化增量）；M10 BACKLOG；M11 DEFERRED。
- Backlog P0 = DOC-00 / AUTH-01 / BI-01 / CI-01 / CI-02 / SEC-01 / DB-01 / AGENT-01。

## CI 真实结构（2026-09-12 核实 `.github/workflows/dotnet-build.yml`）
- 仅 3 个 job：`build`、`controller-runtime`、`e2e`（Playwright）。**无全量主单测 job** → CI-02 仍是待办；现有的 `dotnet test` 只在 e2e job 内且带 `--filter FullyQualifiedName~PermissionMatrixE2ETests`。
- ⚠️ `controller-runtime` 的 C.13.3 步骤把 GQ-007 第二指标断言写成 `details[1].semanticText = 入库金额`，而权威 fixture `SuperBuilder_AI/Evaluation/Golden/query-plan-golden-v1.json` 中 GQ-007（"查询入库数量和入库单数量"）第二指标是**入库单数量 / count** → 断言错误，CI-01 成立。
- e2e job 依赖 `secrets.SB_E2E_*`；限流在 CI 靠 `RateLimit__LoginLimit=500`/`GlobalLimit=1000` 注入（默认 10/120 会 429）。
- 「执行数 > 0」不足以证明全量跑过（0 用例时 `dotnet test` 退出码 0 → 假绿）。

## 测试基线（历史，须注明来源日）
- 单测 `SuperBuilder_AI.Tests`：1140/1140（2026-09-12 记录）。⚠️ 不得直接当当前基线引用，须实跑。
- Golden 契约 18/18，禁止删用例/跳过来变绿。
- 旧门槛 431 / 476 / 1015 / 1091 等散落在 `docs/plans/archive/`、`M7.md`、`M12.md`、`audits/archive/` → M13-01 验收已声明不再作为当前基线。
- `RateLimitMiddlewareTests.Expired_Windows_Are_Evicted…` 时间敏感 flaky（全量偶发失败、单跑绿）。

## 迁移集
- 开发库 `SuperBuilder_Platform` 45/45 已应用（末位 `20260911231209_M12_18_MetricDimensionExpression`），清单 `scripts/schema/schema-version.json` 同步 45。
- 权威验证：`migrations script --idempotent` → **空库重放** → 与 `SuperBIContextModelSnapshot.cs` 双向差集。⚠️ `has-pending-model-changes` 检不出「快照有、迁移无」；对已打补丁的库 diff 会假绿。
- ⚠️ 统计迁移文件禁用 `grep -v Snapshot`（会误过滤 `20260908051650_M7_11_AskQuerySnapshot`）→ 用 `grep -vE '\.Designer\.cs$|SuperBIContextModelSnapshot\.cs$'`。
- 新表一律走 `dotnet ef migrations add`，**勿手工写迁移**（历史缺陷来源：M7_02 重复 CreateTable、AppPlans 缺发布列）。
- ⚠️ 元数据扫描是「追加」非「重建」：重扫后须按 `tableId` 范围删旧向量，否则 ID 错位 → 伪装成 `SB_AUTHZ_001 403`。

## i18n 四处一致（CI 护栏 `ResourceKeyRegistryTests`）
- 四处：`ResourceKeys.cs`(const+Catalog) / `Keys.cs`(const+Defaults) / `LocalizationSeedService.cs`(ZhCnDefaults) / 消费的 razor（`L10n.T(Keys.Content.X, "中文兜底")`，禁裸中文）。
- 易错不变量：后端 Catalog `DefaultValue` = **en-US**；RCL `new(zh, en)` 首参 = **zh**；seed = zh。须断言 `be.DefaultValue == rcl.en` 且 `seed == rcl.zh`；拿 be 比 rcl 的 zh 会得到「全量键失败」假象。
- 删死键须四地同步；同文件两处**不可并行 Edit**（后者覆盖前者）。

## 已知缺口 / 陷阱
- **新租户语言**：`POST /api/tenant-management` 走 `request.AvailableCultures ?? new[]{"zh-CN"}`（`TenantManagementController.cs:146`）→ 显式单语言，永不触发全语言回退 ⇒ 新租户只有 zh-CN，语言切换器（需 Count>1）不出现。缺陷在创建 UI 缺语言多选。
- **列权限**：M12-18 仅**行级** RLS；列级（字段）授权未提供 → M13-13。
- **业务语义层模型**：`BusinessEntityMetric` 继承 `BaseEntity`（**无 TenantId**）→ 隔离须经 `BusinessEntity.TenantId`（EF INNER JOIN）；`BusinessEntityDimension` 自带 TenantId；`BusinessEntityRelationship` 靠两端实体。返回带反向导航的实体会循环引用 → 一律只读投影 record。
- **术语治理**：`SemanticLabels/BusinessDomains/BusinessEntities`=0（仅 `MetadataSemantics`=513）。
- **签名密钥**：缺 `Auth:SigningKey` 启动即抛异常（`AuthSigningKeyPolicy.cs:20`）；非 dev 拒绝旧默认值并校验长度/字符多样性。**无硬编码回退**。
- **观测**：`ObservabilityMiddleware` 在 Auth/RateLimit/Authorization **之前**（`Program.cs:600`），401/403/429 可见；余优化点=响应阶段补租户上下文（低优先）。

## 高频踩坑
- **Blazor/Razor**：① 事件含 C# 字符串用单引号属性；② 渲染变量写 `@(x)`；③ void 方法须包 lambda；④ 注入 `IApiClient`/域 `I*ApiClient`，注具体类会 500；⑤ `<details>` 不支持 `@bind-open`；⑥ 子组件 `OnAfterRender` 早于 `MainLayout` 自举（等 `AppState.SessionRestored`）；⑦ 并行 `dotnet build` 同方案报 MSB3030；⑧ **`IJSRuntime.InvokeAsync` 必须是方法内首个 await**（否则 `TaskCanceledException`）；⑨ `ServerPrerendered` 阶段无 JS，主题/JS 互操作移 `OnAfterRenderAsync(firstRender)`。
- **登录/权限**：`platform` 租户被登录 UI 排除（`platform-admin` 走 `/admin/login`）；`ApiClientBase.OnUnauthorized(bool requestCarriedToken)` 仅在「请求确带令牌且已登录」时清会话。
- **端点易混**：租户管理 `POST /api/tenant-management`（非 `/api/platform/tenants`）；元数据扫描 `POST /api/data-sources/{id}/metadata/scan`（在 **MetadataController**）。
- **PowerShell 5.1**：原生命令 stderr 在 `$ErrorActionPreference='Stop'` 下误判终止；`Write-Host` 无法捕获（用 `Write-Output`）；BOM 叠加破坏 `<#` 帮助块；Windows curl `-o` 须相对路径。
- **EF/SQLite 测试**：SQLite 内存库 `Tenants.TenantCode` 唯一约束 → 同测试种多租户须显式给 TenantCode（否则 `UNIQUE constraint failed`）。
- **架构治理（M9-02/03）**：四层 `src/`（Domain/Application/Infrastructure/Api）；命名空间双轨（目录=分层，命名空间=关注点）；迁移统一 `Infrastructure.Persistence.Migrations`；`ArchitectureTests` 4 不变量。业务术语须 `BusinessTerm` 强类型。

## 历史教训
- `MetadataCsvFixtureService.ImportAsync` 曾全局清空全部租户 → 已限自身租户子树。
- Golden 实时调 Qwen 有抖动（GQ-008/403）非环境问题；判定看 `expectedOutcomeSatisfied`。
- Git 历史重写：filter-repo 必后台跑（前台 120s SIGTERM 损 HEAD）；先 `git bundle` 备份。
- **文档状态须以 `git status`/`git log` 实测为准**，不得凭记忆断言。
