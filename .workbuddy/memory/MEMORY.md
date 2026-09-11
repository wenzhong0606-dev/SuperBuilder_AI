# SuperBuilder AI Native BI — 项目长期记忆

> 唯一事实来源：`docs/Master_Development_Plan.md`（v2.3）+ `docs/milestones/Mn.md`。
> 本文件只记「代码/文档中查不到、但会反复踩」的事实。

## 技术栈与环境（2026-09-10/11 实测）
- .NET 10 + EF Core 10 + Dapper；Qdrant 1.19（1024 维，集合 `superbi_metadata`）；Qwen LLM；SQL Server 元库 `localhost/SuperBuilder_Platform`（Uid=live Pwd=root）；业务库 WMS MySQL `192.168.16.120:3306/steccn_wms`（仅 35 表：11 QRTZ_* + 24 wms_*，**无销售域**）。
- Qdrant：`~/Downloads/qdrant-x86_64-pc-windows-msvc/qdrant.exe`，storage 在同目录 `./storage`；REST 6333 / gRPC 6334。异常退出会残留 WAL 锁 → `Stop-Process -Force` + 重启自愈。
- 后端起：`ASPNETCORE_URLS=http://localhost:5032 dotnet bin/Debug/net10.0/SuperBuilder_AI.dll`。
- ⚠️ 宿主有 `HTTP_PROXY/HTTPS_PROXY=127.0.0.1:53963` → .NET gRPC 连 localhost 报 HTTP/2 失败。`QdrantService` 已改 `SocketsHttpHandler{UseProxy=false}`（勿回退）。
- ⚠️ **沙箱内 `ss`/`netstat` 看不到宿主监听**——可达性必须真实连接实测（curl/客户端），端口扫描结论不可信。
- ⚠️ **沙箱 `dotnet restore`/`build`/`test` 全失败**：`Environment.GetFolderPath(CommonApplicationData)` 返回 null（`ProgramData` 已知文件夹不可解析）→ NuGet `XPlatMachineWideSetting`/`ConfigurationDefaults` 静态构造抛 `Value cannot be null (path1)`，**`NUGET_PACKAGES`/`ProgramData` 等 env 设置均无效**（非 NuGet.config 问题）。**绕过**：对预编译 DLL 直接 `dotnet vstest <dll>`（不经 NuGet/SDK 还原管线）可正常实跑 E2E；CI 干净环境不受影响，`dotnet test` 直接可用。
- 租户实测：1=platform / 2=demo(禁用) / 3=e2e / 4=e2eapp / 5=bizcheck；口令统一 `longping00`；平台管理员 `platform-admin`@1。
- E2E 账号真值：管理员 `e2eadmin`=用户4/租户4；读者 `e2ereader`=用户5/租户4（**须共用租户4 数据源**，租户3 无数据源 → Ask 必 0 行）。
- Python venv：`~/.workbuddy/binaries/python/envs/default`（pymysql；无 mysql CLI，脚本 `/c/tmp/checkwms.py`）。
- sqlcmd：`/c/Program Files/Microsoft SQL Server/Client SDK/ODBC/170/Tools/Binn/sqlcmd`，**用 stdin 重定向**（`-i` 被 MSYS 破坏），且须 `-I`。
- **无 `gh` CLI**。触发/轮询 CI 走 REST API：令牌取自 `git credential fill`（GCM 持有的 `gho_*`），勿 echo。脚本 `C:/tmp/ci_check.py`（列最近 run）/ `ci_jobs.py <run_id>`（job+失败步骤）。⚠️ 后台 shell 里给 python 管道 `| head` 会零输出（块缓冲）——须前台跑。
- ⚠️ 传脚本路径给 python 必须写 `"C:/tmp/x.py"`；`/c/tmp/x.py` 被 MSYS 转成 `C:\c\tmp\x.py` → file not found。
- ⚠️ 构建报 `MSB3027/MSB3021` 文件锁（`.NET Host (pid)`）多为**自己先前起的宿主进程**。Git Bash 下 `taskkill //F` 会报无效参数，须 `MSYS_NO_PATHCONV=1 MSYS2_ARG_CONV_EXCL='*' taskkill /F /PID <pid>`。

## i18n 四处一致（CI 护栏 `ResourceKeyRegistryTests`）
- 四处：`ResourceKeys.cs`(const+Catalog) / `Keys.cs`(const+Defaults) / `LocalizationSeedService.cs`(ZhCnDefaults) / 消费的 razor（`L10n.T(Keys.Content.X, "中文兜底")`，禁裸中文）。
- **不变量易错点**：后端 Catalog 的 `DefaultValue` = **en-US**；RCL `new(zh, en)` 首参 = **zh**；seed = zh。故须断言 `be.DefaultValue == rcl.en` 与 `seed == rcl.zh`。拿 be 比 rcl 的 zh 会得到「全量键失败」的假象。
- 删除死键须四地同步删；同文件两处**不可并行 Edit**（后者覆盖前者）。校验脚本样例 `C:/tmp/verify_designer_i18n2.py`。

## 测试基线
- 单测 `SuperBuilder_AI.Tests` **1119/1119 绿**（2026-09-11，基线随 M12-11~18 递增：1061→1071→1073→1075→1082→1091→1108→1119）。⚠️ `RateLimitMiddlewareTests.Expired_Windows_Are_Evicted...` 时间敏感 flaky（全量偶发失败、单跑 7/7 绿）。
- E2E `tests/SuperBuilder_AI.E2E.Tests`：**上次记录 10 通过 / 1 跳过（axe 按设计）/ 0 失败**（需 `SB_E2E_BASE_URL`+`SB_E2E_API_URL`+Chromium）。限流依赖 gitignored `appsettings.Local.json`（LoginLimit=500/GlobalLimit=1000）。⚠️ 09-11 新增 `PermissionMatrixE2ETests.cs`（M12-P0 权限矩阵，17 用例：7 管理可见+8 读者隐藏双向+Agent Run 专项×2），编译通过、env 缺失时全跳过；**已本地拉起整套栈经 `dotnet vstest` 实跑 = 15 通过 / 1 失败(`/themes` 测试时序耦合) / 1 跳过（无智能体种子），时序修复已落源码，并已接 CI（51b0d16：E2ESandboxSeedService 建 e2eapp 租户+e2eadmin/e2ereader，E2E_SEED=true + 过滤 PermissionMatrixE2ETests 类），待 CI 实跑确认全绿**。
- **CI 实跑根因已定位（run 34566854796：17 总数 / 5 通过 / 12 失败）**：
  1. **登录 429（主因，12 例全因此）**：默认 `LoginLimit=10`/`GlobalLimit=120`（见 `RateLimitOptions.cs`）在 CI 无 `appsettings.Local.json` 时生效；所有 E2E 登录同源自 `127.0.0.1`+相同 UA → 共用一个限流桶（键 `ip:127.0.0.1:<uahash>`），矩阵 ~19 次登录瞬间触顶 → 后续用例 `SB_TOO_MANY_REQUESTS` 429。已于 `.github/workflows/dotnet-build.yml` 的「启动 Web API」步骤注入 `RateLimit__LoginLimit=500`/`RateLimit__GlobalLimit=1000`（与 Local 配置等价，仅 CI）。
  2. **`ThemeEditor` 调色板 `KeyNotFoundException 'primary'`（次因，admin `/themes` 必崩）**：`_tokens` 初值为空字典，仅 `InitTokensFromNode` 填充；而 `OnAfterRenderAsync(firstRender)` 在会话自举前触发 → `api/themes/editor/blueprint` 以 `authTenant=0` 匿名发出 → 401 → `GetJsonAsync` 抛异常被 catch → `_tokens` 仍空 → 调色板 `_tokens[key]` 抛 `KeyNotFoundException`。已修：`_tokens` 防御性初始化为 `TokenOrder.ToDictionary(k=>k,"")`；并仿 `AppPreview`/`AppRun` 在 `OnInitialized` 订阅 `State.SessionRestoredChanged`、`OnAfterRenderAsync` 仅在 `State.SessionRestored` 后取数。Components 工程 `--no-restore` 编译 0 错通过。
  - **CI 已实跑确认（run 34568568564 / commit 86a2245）：16 通过 / 1 跳过（无智能体种子，`Admin_Agent_RunButton_Visible_WhenAgentsExist` 诚实跳过）/ 0 失败，全绿 ✅**。M12-P0 验收闭合。
- Golden 契约 `Evaluation/Golden/query-plan-golden-v1.json` 18/18，禁止删改绕过。

## 里程碑真实状态（2026-09-11，已回查源码/git）
- ✅ M0 / M1 / M2 / M3 / M4 / M5 / M6 / M7(01~11) / M8。
- 🟡 **M9 主体交付，范围须限定**：01~11、13~15 均有交付块，但 **M9-06 = 务实子集**（UI 仅按 `code` 分支，未全量改造字符串展示）、**M9-11 未含完整 DTO/枚举合并审计**、**M9-14 仍记完整恢复演练待办**——**不得统称"全部验收完成"**。仅 **M9-12（A3 拆四项目）明确跳过**（用户决策）。
- 🟡 **M12-P0 前端 RBAC 已实施 + 权限矩阵 E2E 已编写（env-gated 待执行，非"完成"）**：`PermissionCodes.cs` 已补 dashboard/agent/theme:edit|publish/metadata:*；`SystemStatus.razor:6` 已补 `Require=PlatformDiagnosticsView`；逐页核实 SystemStatus/Dashboards/Apps/DataSources/ThemeEditor/BusinessModel/SemanticLabels 均正确包裹；**`Agent.razor:33` Run 按钮已补 `agent:manage` 包裹**（原唯一遗漏）。权限矩阵 E2E `tests/SuperBuilder_AI.E2E.Tests/PermissionMatrixE2ETests.cs`（18 用例：8 路由双向矩阵 + Agent Run 专项）已于 09-11 编写，`dotnet build` 通过、无 env 时全跳过（跳过安全网正常）；**尚未在运行实例+凭据+Chromium 集成环境实跑**，故不标记 ✅。`MDP/M12.md` 已同步刷新，文档不再滞后。
- ✅ **M12-P1（M12-09~18）已全部闭环**（2026-09-11）：09=Agent 新建/Run 接真实 API、10=Agent Runs 详情页、11=DataSource 测试连接方案 B、12=P0 收口、13=Dashboard 可视化设计器、14=App 可视化设计器、15=Semantic Model 关系图、16=指标中心只读治理视图、17=Identity 组织目录（组织/部门/用户组 + 组角色并入有效权限）、18=数据权限/RLS 可视化管理（富化列表 + 启用切换 + `/admin/data-policies`）。⚠️ M12-18 仅**行级**策略，**列级（字段）授权未提供**。⬜ M10（多库连接器/BYO 模型/外部 IdP）、M11（暂缓）。
- ⚠️ 本地 `HEAD == origin/master == 87b109e`（0/0 差异）；远端实时态因 Git HTTPS helper 故障未独立确认，故服务器最新状态未知。提交均随用户自行推送。

## 已知缺口（2026-09-11 经用户逐条源码勘误修正）
1. ✅ **M12-P0 遗漏已闭环（09-11）**：`Agent.razor:33` Run 按钮已补 `agent:manage` 包裹；并新增 `PermissionMatrixE2ETests.cs`——覆盖 M12-P0 全部按钮级守卫双向断言（管理员可见/读者隐藏，8 路由）+ Agent Run 专项回归（读者必隐藏、管理员有种子时可见）。env-gated：无 `SB_E2E_*` 时诚实跳过，未实跑不标 ✅。
2. **观测/日志（已修正，非盲区）**：`ObservabilityMiddleware` 在 Auth/RateLimit/Authorization **之前**（`Program.cs:600`），401/403/429 可见。余改进点=响应阶段补全租户上下文（REQ 日志早于 Auth 注入，读不到租户，属低优先优化，非缺陷）。
3. **新租户语言（已修正）**：无显式配置时继承平台**全部**启用语言（`TenantLanguageService.cs:205`）；旧单一 zh-CN 回退记录已处理；语言切换器在全新安装可用。
4. **签名密钥（已修正）**：缺 `Auth:SigningKey` 启动即抛异常（`AuthSigningKeyPolicy.cs:20`）；非 dev 拒绝旧默认值+检查长度/字符多样性（:26-33）。**无硬编码回退，不可伪造**。
5. **CI（已修正）**：`.github/workflows/dotnet-build.yml:180` 已配 Web/Blazor E2E(Playwright) job；MAUI/移动端主流程覆盖仍不足。
6. Agent 新建/Run 仍为 Toast 占位（M12-09/10，后端写接口已存在）；DataSource 新建态测试连接为 Toast（后端 `TestConnection(id)` 已存在，缺"按连接串预测试"端点）。
7. 业务术语治理层空白：`SemanticLabels/BusinessDomains/BusinessEntities`=0（仅 `MetadataSemantics`=513）。超域问句（销售额/品类）低置信或 `SB_BI_002` 是**预期行为**（WMS 库无销售域）。
8. ⚠️ **测试基线措辞**：1061 单测 / E2E 10通过·1跳过 为**上次记录（2026-09-11 日志）**，未每次复跑确认；称"基线"须注明来源日。
9. ⚠️ **CI E2E 无法真验证权限矩阵（2026-09-11 实测）**：`.github/workflows/dotnet-build.yml` 的 e2e job 启动 SQL+Qdrant+API(5050)+Web(5080) 跑 `dotnet test`，凭据读 `${{ secrets.SB_E2E_* }}`，但**全新 CI 库无 e2e 账号**——`IdentityService.SeedAsync()` 仅建 `platform` 租户+全局权限/角色目录；`DemoDataInstaller` 仅建 `demo` 租户+`demo_admin`；源码零 `e2eapp/e2eadmin/e2ereader` 字符串（账号是本地库手动/外部预置）。故矩阵登录必 401（或 secrets 缺失时全 skip=假绿）。已修：`SB_E2E_API_URL=5050` 已补（af46542）。待办：CI 增 E2E 种子步建 e2eapp 租户+e2eadmin/e2ereader + 配 secrets，矩阵方能真验证。

## 迁移集完整性
- 权威验证法：`migrations script --idempotent` → **空库**重放 → 与 `SuperBIContextModelSnapshot.cs` 双向差集。⚠️ `has-pending-model-changes` 检不出「快照有、迁移无」；**对已打补丁的开发库 diff 会假绿**。
- 🟢 **沙箱内可直接用真实 EF 工具（2026-09-11 突破）**：`dotnet-ef 10.0.10` **已全局安装**于 `~/.dotnet/tools/dotnet-ef.exe`；沙箱 `APPDATA` 为空 → `dotnet ef` / `dotnet nuget` 报 `Value cannot be null. (Parameter 'path1')`。**先 `export APPDATA="C:\Users\ThinkPad  X1\AppData\Roaming"`，再直接调 exe**：`"$HOME/.dotnet/tools/dotnet-ef.exe" migrations add <Name> --project SuperBuilder_AI/SuperBuilder_AI.csproj --startup-project SuperBuilder_AI/SuperBuilder_AI.csproj --framework net10.0 --no-build`（`--no-build` 前须先 `dotnet build ... -f net10.0 --no-restore`）。**新表一律走此路径，勿手工写迁移**（手工迁移是本项目历史缺陷来源）。
- 历史缺陷：`M7_02_AppVersion` 曾含 39 个重复 CreateTable（已重写，仅建 `AppVersions`）；`AppPlans` 4 个发布列曾缺失（补丁 `20260910053000_M7_02_Fix_AppPlanPublishColumns`）。当前 **44 迁移、45→51 张表**（M12-17 新增 6 张）、差集 0。
- **开发库 `SuperBuilder_Platform` 已于 2026-09-11 同步到 44/44（pending 0 / drift 0）**；清单 `scripts/schema/schema-version.json` 同步刷新为 44（schemaVersion=`20260911143327_M12_17_IdentityOrganizationUnits`）。⚠️ 每次新增迁移必须重生成清单，否则校验报漂移。
- ⚠️ **统计迁移文件禁用 `grep -v Snapshot`**：会把 `20260908051650_M7_11_AskQuerySnapshot` 误过滤（文件名含 Snapshot）→ 假漂移。正确：`grep -vE '\.Designer\.cs$|SuperBIContextModelSnapshot\.cs$'`。
- ⚠️ **沙箱内 `verify-schema.ps1` 无法自证**：宿主脚本会话解析不到 `dotnet`/`sqlcmd`（PATH 有目录但 `Get-Command` 失败，全路径函数垫片后子进程仍 0 行输出）→ EXIT=2 假漂移；且需 `Set-ExecutionPolicy -Scope Process Bypass` 才能调用脚本。复核改用 bash 直连 sqlcmd + `comm` 集合差。
- ⚠️ **元数据扫描是「追加」非「重建」**：重扫后须按 `tableId` 范围删旧向量，否则 ID 空间错位 → 回查落空 → 伪装成 `SB_AUTHZ_001 403`。
- M9-15 产物：`scripts/schema/schema-version.json`、`verify-schema.ps1`（离线/在线双模，EXIT 0/2）、`rollback-one-step.sql`、`docs/ops/migration-seed-schemaversion.md`。

## 高频踩坑
- **Blazor/Razor**：① 事件含 C# 字符串用单引号属性；② 渲染变量写 `@(x)`；③ void 方法须包 lambda；④ 注入 `IApiClient`/域 `I*ApiClient`，注具体类会 500；⑤ `<details>` 不支持 `@bind-open`；⑥ 子组件 `OnAfterRender` 早于 `MainLayout` 自举（等 `AppState.SessionRestored`）；⑦ 并行 `dotnet build` 同方案会 MSB3030；⑧ **`IJSRuntime.InvokeAsync` 必须是方法内首个 await**（否则 Blazor Server 抛 `TaskCanceledException`——语言偏好持久化 Bug 即此因）。
- **登录/权限**：`platform` 租户被登录 UI 排除，但 `POST /api/auth/login` 仅按 TenantId 校验 → `platform-admin` 须走 `/admin/login`。登出缺陷已中心化修复：`ApiClientBase.OnUnauthorized(bool requestCarriedToken)` 仅在「请求确带令牌且已登录」时清会话。
- **端点易混**：租户管理 `POST /api/tenant-management`（非 `/api/platform/tenants`）；元数据扫描 `POST /api/data-sources/{id}/metadata/scan`（在 **MetadataController**）。
- **PowerShell 5.1**：原生命令 stderr 在 `$ErrorActionPreference='Stop'` 下误判终止；`Write-Host` 无法捕获（用 `Write-Output`）；BOM 重复叠加会破坏 `<#` 帮助块；Windows curl `-o` 须相对路径。
- **业务语义层模型**：`BusinessEntityMetric` 继承 `BaseEntity`（**BaseEntity 无 TenantId**，仅 Id/审计/RowVersion）→ 指标无自身 TenantId，租户隔离须经所属 `BusinessEntity.TenantId`（EF INNER JOIN）；`BusinessEntityDimension` 自带 TenantId；`BusinessEntityRelationship` 亦无 TenantId，靠两端实体。返回带反向导航的 EF 实体会循环引用 → 一律用只读投影 record。
- **EF/SQLite 测试**：SQLite 内存库 `Tenants.TenantCode` 有唯一约束 → 同测试种多租户须显式给 TenantCode（默认空串冲突，报 `UNIQUE constraint failed: Tenants.TenantCode`）。`dotnet test` **不带 `--no-restore`** 报 `Value cannot be null (Parameter 'path1')`（本沙箱还原不可用）→ 统一 `build --no-restore` + `test --no-build`。
- **P11 前端**：`ServerPrerendered` 阶段无 JS → 主题/JS 互操作移 `OnAfterRenderAsync(firstRender)`；RCL 不可引 `Components.WebView.Maui`；`TokenService` 缺 `Auth:SigningKey` **拒绝启动**（`AuthSigningKeyPolicy.cs`），非 dev 还校验强度——生产密钥管理由配置校验强制保障，无硬编码回退。

## 架构治理（M9-02/03 固化）
- 四层 `src/`（Domain/Application/Infrastructure/Api）；命名空间双轨：目录=分层，命名空间=关注点（Models=Domain、Services=Application、Controllers=Api、Interfaces=Ports、Data+Infrastructure=Infra）。迁移统一 `Infrastructure.Persistence.Migrations`。`ArchitectureTests` 4 不变量防回归。
- 业务术语须 `BusinessTerm`（`Models.BI`）强类型，禁裸 string。
- 零回归手法：门控隔离（多语言/AI 路径非默认短路）+ 双路径 Agent/AppBuilder（默认确定性不调 LLM）。

## 历史教训
- Phase3.1：`MetadataCsvFixtureService.ImportAsync` 曾全局清空全部租户 → 限自身租户子树。
- Golden 实时调 Qwen 有抖动（GQ-008/403）非环境问题；判定 `expectedOutcomeSatisfied`。
- Git 历史重写复盘：filter-repo 必后台跑（前台 120s SIGTERM 损 HEAD）；先 `git bundle` 备份；替换规则须覆盖全部真实值（含旧 `appsettings.json` 的 64 字符 SigningKey）。
- **文档状态须以 `git status`/`git log` 实测为准**，不得凭记忆断言（曾误判"M7-11 未提交"）。
