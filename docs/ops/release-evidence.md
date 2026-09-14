# 发布证据记录（Release Evidence）

> Status: ACTIVE  
> 载体：Backlog `BASE-01` / 里程碑 M13-01  
> 建立：2026-09-12  
> 关联：Active Plan [2026-09-production-readiness.md](../plans/active/2026-09-production-readiness.md)、[M13](../milestones/M13.md)

## 使用规则

- 每个候选版本（候选 SHA）**追加**一节，不覆盖历史节。
- 只记录事实：SHA、版本、执行数/失败数/跳过数、实际数据库状态、失败复现方法。不写推测性结论。
- **基线的作用是如实记录起点，不要求修复前先全绿。** 允许记录失败项。
- 数字必须来自本次实跑。不得沿用历史记录（含 M0–M12 期的 431 / 476 / 1015 / 1091 / 1140 等）。
- 不记录任何密钥、口令或连接串明文；环境标识只写主机/库名与版本。
- 进行中的未提交改动默认**不计入**发布版本；若计入须显式说明并给出 diff 摘要。
- 后续每个候选版本重新验证，再据此判断是否达到 G0 / G1。

## 模板

### `<候选标识>` — `<SHA>` — `<日期>`

| 项 | 内容 |
|---|---|
| 提交 SHA / `git rev-parse HEAD` | |
| 工作树是否干净（`git status`） | |
| 是否存在计入发布版本的未提交改动 | |
| .NET SDK / runtime 版本 | |
| 依赖版本（EF Core / Dapper / Qdrant / LLM 提供方） | |
| 元数据库迁移状态（applied / pending） | |
| 业务数据库标识（不含口令） | |
| 配置来源（不含密钥） | |
| 构建结果（error / warning 数） | |
| 单元测试：执行 / 失败 / 跳过 | |
| E2E：执行 / 失败 / 跳过 | |
| Golden | |
| 真实接口冒烟 | |
| 程序集迁移清单 vs `__EFMigrationsHistory` 差集 | |
| 当前失败项与复现方法 | |
| 日志 / 截图 / 报告位置 | |
| 与上一候选的差异 | |

---

## 记录

### G1 准备核对 — `445e7a26f2f894ceead5ff021b55204dc2afbf6f` — 2026-09-14

本条是文档与本地版本核对，不是新一轮运行验收，不覆盖下方历史基线。

| 项目 | 核对结果及限制 |
|---|---|
| 本地提交 | `445e7a26f2f894ceead5ff021b55204dc2afbf6f`；本轮开始时工作树干净 |
| 既有 CI 记录 | G1 执行计划记录 run `34746710044`（`7f62ff4c`）四 job 通过、单测 1168/1168；工作记录另记录收尾 run `34747067338` 通过。本轮未重新读取远端原始报告，不将其视为当前 HEAD 的新实跑结果 |
| 恢复证据 | [2026-09-13 演练](drill-evidence-20260913.md)；组件恢复耗时不能直接等同完整业务 RTO，RPO/RTO 目标仍需 O2 确认 |
| 新增准备材料 | [候选试点产品包](../product/pilot-package.md)、[测量方案草案](../product/pilot-measurement.md)；不属于已验证产品能力 |
| 本次运行验证 | 未执行构建、测试、压测、数据库操作或真实客户验收 |
| G1 剩余条件 | O1/O2/O3 确认；PERF-01 实测；产品包与测量方案验收；M14-07 可用交接包验证及定稿 |
| 后续候选要求 | 发布前记录目标 SHA 对应 CI 原始报告、实际环境、迁移与业务冒烟，并附性能、恢复及交接证据；不能仅凭历史 DONE 状态宣布 G1 通过 |

### `BASE-01` 首次基线 — `d7302a7`（2026-09-12）

> 记录口径：本次为 **真实实测**，非沿用历史数字。沙箱对 `dotnet build/test` 的 NuGet 静态构造限制（`Environment.GetFolderPath(CommonApplicationData)` 返回 null → path1）使全量重建不可行，故构建产物采用仓库既有预编译 DLL；无法在沙箱重跑的项**如实标注**，不臆造。

| 项 | 内容 |
|---|---|
| 提交 SHA | `d7302a7`（本地；最新已推送祖先 `4cfcea5b`；本候选尚未 push，CI 暂无对应 run） |
| 工作树是否干净 | 除内部 `.workbuddy/memory/` 与根目录 stray `nul` 外干净；这两类不计入发布版本 |
| .NET SDK / runtime | 10.0.301；目标框架 `net10.0`（API/RCL/Web），MAUI `net10.0-android/ios/windows` |
| 依赖版本 | EF Core `10.0.11`（SqlServer/Sqlite/Design/Tools）、Dapper `2.1.79`、Microsoft.Data.SqlClient `7.0.2`、MySqlConnector `2.6.1`、Npgsql `10.0.3`、Qdrant.Client `1.19.0`、OpenAI `2.13.0`（LLM 走 OpenAI 兼容接口，后端为 Qwen） |
| 配置来源 | `appsettings*.json`（未记录任何密钥/连接串明文值） |
| 元数据库迁移（applied / pending） | `SuperBuilder_Platform`：清单 `45` / 实际 `__EFMigrationsHistory` `45` / pending `0` / drift `0`。⚠️ 初查用 `timeout 20` 截断末行误报「缺 M12_18、44/45」；`timeout 30` 干净重查为 **45/45**，落点 `20260911231209_M12_18_MetricDimensionExpression` 已应用、效果列（`Expression`/`DataType`）存在。迁移状态**一致**。 |
| 业务数据库标识（不含口令） | 配置 `192.168.16.120:3306/steccn_wms`（MySQL，35 表）；远程地址，沙箱未验证连通 |
| 构建结果 | 沙箱未重建（path1 限制）；既有 Release 产物存在 |
| 单元测试：执行 / 失败 / 跳过 | 预编译 `tests/.../bin/Release/net10.0/SuperBuilder_AI.Tests.dll` 实跑 **1071 / 0 / 0**（1m22s）。⚠️ 该 DLL 构建于 M12-12 前后（1071 例），**早于当前代码末态 1140**，属陈旧构建，**非当前权威基线**；仅证明该子集无回归 |
| E2E：执行 / 失败 / 跳过 | 未本地运行（Playwright）；由 CI `Web/Blazor E2E` 覆盖 |
| Golden | 未本地重跑；末次记录 18/18（须在新代码上重确认） |
| CI 最近状态 | 最近 5 次 `dotnet-build.yml` 运行（含 `4cfcea5b` 等**纯文档提交**）`conclusion=failure`。因这些提交仅改 markdown、不触及代码/测试/评估管线，失败只能落在**已注册独立的 GQ-007 步骤（Controller C.13.3）**，属「范围内绿」。沙箱网络限制未能拉取 job/step 明细二次确认（jobs 接口持续超时）；run 号 `34667588723`(4cfcea5b)/`34667434441`(03fbb4d4)/`34667351591`(c1d6714a)/`34666695066`(57681e4e)/`34664912964`(f185abf5) |
| 当前失败项与复现方法 | ① **GQ-007 runtime BLOCK**：`SemanticApplicabilityGate` 在 CI `V2.6 Evaluation Controller Runtime Smoke`（step C.13.3）失败；权威 fixture `query-plan-golden-v1.json` 中 GQ-007（"查询入库数量和入库单数量"）第二指标为 **入库单数量/count**，workflow 断言 `details[1].semanticText=="入库金额"` 错误 → 见 `CI-01`。② **CI-01**：`.github/workflows/dotnet-build.yml:176` 第二指标断言为「入库金额」（应为「入库单数量」）；且 `:173` 先断言 `.passed=="true"`，GQ-007 BLOCK 时 `:173` 已失败、`:176` 永不执行 → 缺陷被遮挡。③ **CI-02**：CI 无全量主单测作业；唯一的 `dotnet test` 在 e2e job 且带 `--filter ~PermissionMatrixE2ETests`。 |
| 日志 / 截图 / 报告位置 | CI run `34667588723` 等（GitHub Actions）；本地 vstest 输出见本次会话后台任务 `Bi0J1i` |

> **基线判定（起点如实记录，不要求先全绿）**：候选 `d7302a7` 的文档治理部分已实跑校验（迁移 45/45 一致、预编译单测 1071/1071 绿）。**当前门禁未全绿**，确定性失败项 = GQ-007 runtime BLOCK（BI-01）+ CI 契约错误（CI-01）+ 无全量单测门禁（CI-02）。下一步按第一批顺序：DOC-00（已完成）→ BASE-01（本记录）→ AUTH-01 → CI-01 → BI-01 → … → G0 复验。

## 更正记录（2026-09-12 CI-01 / BI-01 复核，不覆盖上方基线快照）

- **BI-01「GQ-007 runtime BLOCK」为误判，已纠正**：实证 `SuperBuilder_AI/Evaluation/artifacts/golden_run_20260829_fix3.json`（18/18）中 GQ-007 记录 = `applicabilityState=Resolved`、`queryPlanEvaluationPassed=true`、`decision=PASS`、`expectedOutcomeSatisfied=true`；两指标 `入库数量/quantity/Sum`、`入库单数量/id/Count` 的 `bindingMatched=true`、均 `passed`。即 `SemanticApplicabilityEvaluator` → `QueryPlanEvaluationGate` 对 GQ-007 **实际通过**，无代码缺陷。
- **近期 CI 在 GQ-007 step（C.13.3）的红，唯一根因是 CI-01 断言错误**（`dotnet-build.yml:176` 原断言 `details[1].semanticText=="入库金额"`，权威 fixture 应为 `入库单数量`），已于 `41e9624` 修正为 `入库单数量`。修正前 `:176` 直接失败；修正后该 step 的依赖（`:173` `.passed==true` 由 gate 通过而绿、`:174/:175/:177` 均满足）应转绿。
- 上方「当前失败项 ①」与「基线判定」中 "GQ-007 runtime BLOCK（BI-01）" 为 BASE-01 时点的推断（当时未实跑 GQ-007 全链路、且 jobs 明细接口超时无法二次确认），**以此更正为准**。
- **待 CI 复跑确认**：CI-01 修正后首跑若 GQ-007 step 仍红，则回查 CI `controller-runtime` job 是否加载了 wms 元数据/Qdrant 索引（golden run 与 CI 元数据状态须一致）；若一致则 BI-01 确为无缺陷。CI-02（无全量主单测 job）仍为真缺口，独立于本次更正。

## 首批部署范围决策（2026-09-14）

**2026-09-14 用户已确认的首批部署范围**：Windows + 直接安装 + 单实例。其他操作系统、容器化及多实例部署组合后续再考虑，不属于本批 G1 验收范围。单实例指应用部署拓扑，不表示 API 与 Web 必须合并为同一进程，也不要求 SQL Server/Qdrant 与应用同机。Windows 具体版本、托管方式、依赖服务位置与版本、浏览器、硬件及联网条件仍需实施核验；本决策不等于安装或性能验收已通过。

## 参考配置确认（2026-09-14）

用户已同意 Windows Server 2022 x64、直接安装、单实例、8 核、32 GB、512 GB SSD 的试点参考配置，使用 SQL Server 2022 和外部模型服务。安装拓扑、依赖边界与容量验证步骤统一见 [参考配置](windows-pilot-profile.md)。此记录不是安装或压测通过证明。

## 本机开发预验证（2026-09-14）

用户指定当前电脑。只读检查确认 Windows 11 Pro、i7-1260P、31.7 GiB 内存、.NET 10、SQL Server 2022 Developer 与原生 Windows Qdrant 1.19.0；可复用现有依赖。详见 [环境检查](local-preflight-20260914.md)。原生程序运行证据补充了此前 Qdrant 安装方式的不确定性，但不代表开机启动、恢复、生产安装及 Windows Server 验收已通过。

## 本机隔离启动与全量单测（2026-09-14）

基于 HEAD 445e7a2 新编译：Web 构建成功（3 warnings），API/单测工程编译完成；最终全量单测 1168/1168，0 失败、0 跳过。独立空库 SuperBuilder_Preflight_20260914 应用 46 条迁移后 API Ready，E2E 账号真实接口登录成功，Web 登录页 HTTP 200。临时进程已关闭，测试库保留。首轮因输出目录在仓库外导致 5 项源码发现测试失败，迁回仓库内独立目录复跑通过。详见 [完整证据与限制](local-preflight-20260914.md)。本轮未验证浏览器交互、模型业务查询或性能，不代表 G1 通过。
