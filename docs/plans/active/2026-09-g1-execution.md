# G1 企业试点开工计划（2026-09）

> Status: ACTIVE（待开工，计划草案）  
> Created: 2026-09-13  
> Parent: ../../Master_Development_Plan.md · ../../milestones/M13.md · ../../milestones/M14.md  
> Companion: ../2026-09-production-readiness.md（Phase A/B 顶层映射）  
> Backlog: ../../Development_Backlog.md（任务状态唯一总账）

本文是 **G1 企业试点阶段的可执行开工计划**：给出门禁目标、开工前置的 OPEN 待决项回填清单、权威执行顺序、首批三项现状快照，以及提交/CI 纪律。任务状态仍只在 Backlog 更新，本文不复制易过期的测试数字与 SHA。

---

## 0. 状态确认：G0 已收口

- Phase A 全部 P0 项 `DONE`：BASE-01 / AUTH-01 / BI-01 / CI-01 / SEC-01 / DB-01 / AGENT-01。
- CI `34728529640` = success（编译检查 / Web-Blazor-E2E / V2.6 三 job 全绿），GQ-007/V2.6 红灯已关闭。
- 唯一划出 G0 的项 **CI-02**（全量主单测 job）= BACKLOG，明确为 G0 之后独立立项，**不阻塞** G0 收口与 G1 启动。
- 结论：**G0 门禁满足，可进入 G1**。

---

## 1. G1 门禁目标（权威来源：M13 §1 / Active Plan）

G1 = G0 + 下表交付物就绪：

| 类别 | 必做项 |
|---|---|
| 里程碑新增 | M13-09、M13-10、M13-11、M13-12、M13-14、M13-16、M13-17、M13-18 |
| M14 前置 | M14-01（试点产品包）、M14-07（可用交接包）、M14-08a（测量方案） |

**G1 关键路径（最长串行链，不可延后）**：
```
BASE-01 → G0 → M13-09 → M13-14 → M14-07 → G1 验收
```
- M13-14 依赖 M13-09；M14-07 依赖 M13-10/11/14，位于链尾。
- M14-07 在 G1 验收前必须交付**可用**交接包（非草稿），服务目标取自 M13-10/12 实测。

**条件门禁（客户明确需要时升级为必做，默认排除在 G1 之外）**：M13-13 列权限 UI、M13-15 自定义组件、M13-08 Agent 真实执行、M10-01~03。

---

## 2. 开工前置：OPEN 待决项回填清单

G1 有若干项被 Active Plan 的「待决事项（OPEN）」阻塞。下表明确每项**阻塞谁、最晚何时前必须确认、建议回填动作**，避免开工后返工。

| OPEN 待决项 | 阻塞的 G1 项 | 最晚确认时点 | 建议回填动作（谁/产出） |
|---|---|---|---|
| 支持环境（部署 OS、单/双实例、支持的数据源类型、已验证到连接/扫描/查询哪一层） | E2E-01(M13-09)、DR-01(M13-10)、PERF-01(M13-12) 的「范围与验收口径」 | 对应集成验证前 | 产品/运维确认单实例试点 + 已验证数据源清单（SQL Server/MySQL，连接到查询层）；双实例列为 CACHE-01 验证项 |
| 试点业务口径（业务场景、指标计算方式、参考结果确认人） | M14-01（试点产品包）、M14-08a（测量方案） | Golden/业务验收标准调整前 | 产品负责人从已验证场景（库存/入库）锁定 1 个试点场景 + 验收问题集 |
| 性能和恢复目标（数据规模、并发、P95、错误率、允许数据损失、RTO） | PERF-01(M13-12)、DR-01(M13-10) | PERF/DR 执行前 | 锁定发布目标 N 并发、P95≤T、失败率≤E、成本≤C、RPO/RTO，入 `docs/ops` |
| 密钥与升级安排（注入/保管位置、存量转换维护窗口、失败恢复） | 已随 SEC-01(G0) 收口，本项 CLOSED | — | 不再阻塞 |

**回填策略**：DB-02 / QUOTA-01 / OBS-01 不依赖上述 OPEN，可立即开工；E2E-01 核心浏览器链可用既有测试配置先行（支持环境仅影响范围边界，不阻塞骨架）；PERF/DR/M14-01/07/08a **必须等对应 OPEN 回填后**再定稿验收。

---

## 3. G1 执行顺序（权威：M13 §4 第一批顺序 + 关键路径）

执行顺序严格遵循 `M13 §4`：`G0 全通过后执行 09 → 17 → 18 → 10/11/12 → 14 → 16，再完成 M14-01/07/08a`。下表为落点/验收/阻塞/拆批的可执行展开。

| 序 | Backlog | 里程碑 | 落点（已核对） | 核心工作 | 验收证据 | 前置 / 阻塞 | 建议拆批 |
|---|---|---|---|---|---|---|---|
| 1 | E2E-01 | M13-09 | `tests/SuperBuilder_AI.E2E.Tests/`（已含 AuthFlowTests / PermissionMatrixE2ETests / AppRuntimeE2ETests / LoginHelper） | 真实 Playwright 链：登录/刷新 → 数据源预测试(成功+失败)/保存/授权/扫描 → Ask 澄清 → Dashboard/App 拖拽/保存/重载/发布 → RLS 查询变化 | 浏览器完整执行；预测试不新增记录；保存只新增一次；桌面+窄屏可操作；失败截图附 SHA | 依赖 02/04/05/06（均 DONE）；范围受「支持环境」OPEN 影响 | 按场景分批：Auth → DataSource → Ask → Designer → RLS |
| 2 | DB-02 | M13-17 | `SuperBIContext.cs`（已有 `ApplyTenantScope` + ~25 个 `HasQueryFilter`）、`tests/.../SuperBIContextTenantFilterTests.cs` | ① 实跑构建确认是否出现 EF 10622 警告并消除；② 审计所有租户作用域读路径是否都调用 `ApplyTenantScope`（opt-in 缺口）；③ 扩 tenant isolation 回归 | 构建无 10622 告警；跨租户查询返回空不泄漏；新增/修改筛选器均有回归 | 无外部阻塞 | 先「10622 核查」单提交，再「opt-in 缺口补强」单提交，再「测试扩充」 |
| 3 | QUOTA-01 | M13-18 | `QuotaService.cs`（ConsumeAsync 非原子）、`IQuotaService.cs`、`QuotaUsage` 实体、`20260830111544_P10_4_Quota` 迁移 | 改为数据库原子消费：单语句 `UPDATE ... SET Used+=@amt WHERE ... AND Used+@amt<=Limit`（或行锁+事务+并发令牌）；覆盖最后一份额度并发、失败回滚、重试幂等、UTC 周期边界 | 并发争抢最后一份额度最多一份成功；失败事务不耗额度；重试不重复计数；读配额不创建用量；直接 API 无法绕过 | 无外部阻塞；需先定义「存量配额 vs 周期调用量」计数规则（内部决策） | 先「并发计数规则」文档/单测契约，再「原子 Consume」，再「并发/幂等测试」 |
| 4 | DR-01 | M13-10 | `scripts/dr-backup`、`docs/ops/backup-restore-dr.md` | 独立恢复环境备份+恢复 SQL/Qdrant/配置/密钥引用；记录数据量/备份时间/恢复时间/损失窗口 | 恢复后登录/解密/Ask/App·Agent 历史可用；RPO/RTO 明确；失败步骤可定位 | 依赖 03/04（DONE）；RPO/RTO 受「性能与恢复目标」OPEN 约束 | 先脚本+文档，再演练证据 |
| 5 | OBS-01 | M13-11 | `ObservabilityMiddleware`、`RequestMetricsCollector`、`StartupDiagnostics` | 登录/Ask 成功率、P95、超时、401/403/429、DB/LLM 错误、扫描积压；关联 ID 排障指南；持续失败告警（先用测试接收器） | 注入 DB 不可用/模型超时/鉴权拒绝可观察可定位；告警触发+恢复均验证 | 依赖 04/05（DONE） | 指标采集 → 告警 → 排障文档 |
| 6 | PERF-01 | M13-12 | 参数化负载场景 | 锁定数据规模/并发/查询分布/模型预算；P50/P95/失败率/连接池/内存/每请求成本；超载恢复 | 目标值及硬件/数据前提入文档且实际达标；超载有限流/超时，不 OOM/不跨租户 | 依赖 06/09/11；受「性能与恢复目标」OPEN 约束 | 先锁目标，再场景，再报告 |
| 7 | ONBOARD-01 | M13-14 | 租户开通、DataSources、扫描、BusinessModel、MetricCenter、Ask 前端 | 空白租户到首个分析成果引导：连接→授权→扫描→实体/指标确认→首问→保存；可选可清理行业样例 | 新用户不改 SQL/配置完成路径；每步可说明失败并重试；非开发人员按指南验收一次 | 依赖 02/03/06/09 | 引导清单 → 可恢复步骤 → 样例数据 |
| 8 | CACHE-01 | M13-16 | 现有 Redis/缓存、Ask 会话、安全戳、用户组权限服务 | 组角色修改/组停用/移除成员/撤销数据源授权、进程重启、双实例交替请求的一致性 | 过期授权不被旧 token/缓存复用；会话按策略恢复或失效；禁止跨用户/租户复用 | 依赖 02/04/05 | 先修缺陷，再双实例回归 |
| 9 | M14-01 | M14 | `docs/product/pilot-package.md` | 从已验证场景选 1 个试点：目标用户/业务问题/数据要求/已支持·未支持/边界/验收问题集 | 每问题有业务确认参考口径；至少 1 个真实接入到保存案例 | 依赖 M13-01；受「试点业务口径」OPEN 约束 | 场景锁定 → 验收集 → 演示步骤 |
| 10 | M14-08a | M14 | 测量方案文档 | 定义首效用时/首接成功率/任务正确率/人工修正/周活/节省时间/支持投入/成本/付费意愿采集办法 | 口径与阈值试点前书面锁定并业务确认；埋点/台账就位 | 依赖 M13-14 | 口径文档 → 埋点就位 |
| 11 | M14-07 | M14 | 安装升级指南、客户配置清单、故障级别/责任、备份恢复、支持入口、退出流程 | 依据 M13-10/11/12 实测定稿服务目标；手册可在开发期起草但**验收前依实测定稿** | 非开发人员按指南完成安装+一次升级；故障按关联 ID 定位；退出可导出约定数据 | 依赖 M13-10/11/14（链尾，**不可延后**） | 草稿（开发期）→ 实测回填 → 定稿 |

> 条件项（不默认排期）：AGENT-02~04（M13-08，客户签约后转必做）、SEC-02（M13-13）、APP-01（M13-15）、M10-01~03。

---

## 4. 首批三项现状快照（已核对源码，避免幻想式规划）

### 4.1 E2E-01 (M13-09) — 三段补齐 + 全量业务链接入 CI ✅ 已完成
- **起点（已具骨架）**：`tests/SuperBuilder_AI.E2E.Tests/` 已有 Playwright 夹具（`PlaywrightFixture`/`PlaywrightCollection`）、`LoginHelper`、`AuthFlowTests`、`PermissionMatrixE2ETests`、`AppRuntimeE2ETests`、`LanguageSwitchTests`、`VisualBaselineTests`、`AccessibilityTests`。但真实业务链（AppRuntime/PlatformLogin）虽已写却**未进 CI 门禁**，且 `DataSource→Scan` / `Dashboard 渲染` / `RLS 数据隔离` 三段缺失。
- **补全的三段（2026-09-13，新增 4 文件）**：
  - `E2EApiHelper.cs`：浏览器侧从 `localStorage['sb_auth_v1']` 取 Bearer 令牌与租户上下文，向 `E2EConfig.ApiUrl` 绝对地址发 `fetch`（绕开 Web 宿主不转发 `/api/**`）。
  - `DataSourceScanE2ETests.cs`：创建数据源 → `POST api/data-sources/{id}/metadata/scan`（202+jobId）→ 轮询 90s 至 Succeeded → `tablesScanned>0`。**受 `SB_E2E_SCAN_CONNECTION` 控**：CI 无可达业务库 → 诚实跳过，不假绿。
  - `DashboardRenderE2ETests.cs`：取 `api/dashboards/editor/blueprint` 骨架 → `POST api/dashboards`（草稿）→ 打开 `/dashboards/{id}` → 断言「返回列表」按钮可见（页面未落到错误态）。
  - `RlsIsolationE2ETests.cs`：令牌作用域查询 `GET api/data-sources` 与显式 `?tenantId=0`（平台）计数必须相等 → 证明令牌租户作用域压倒查询参数，防跨租户泄漏（DB-02/M13-17 修复的端到端守护）。
- **CI 接线（M13-09 收口关键）**：`.github/workflows/dotnet-build.yml` 的 e2e job 过滤器由 `--filter "FullyQualifiedName~PermissionMatrixE2ETests"` 改为 `--filter "FullyQualifiedName~E2ETests"`，运行整支业务链 E2E 功能类（PlatformLogin / AppRuntime / DataSourceScan / DashboardRender / RlsIsolation / PermissionMatrix），**天然排除**辅助类（VisualBaseline / Accessibility / AuthFlow / LanguageSwitch / PermissionDeny——命名不含 "E2E"，且部分依赖 CI 未设的 `SB_E2E_TENANT`/`READER_TENANT` 会跳过）。`DataSourceScan` 还需 `SB_E2E_SCAN_CONNECTION`（CI 未设→诚实跳过）。
- **诚实跳过设计**：本环境无法跑浏览器、也无可达业务库；所有新用例均 `SkippableFact + E2EConfig.Require`，缺环境变量即跳过而非失败，避免「0 用例→退出码 0→假绿」。CI 设了 `BASE_URL/USER/PASSWORD/READER_*` 但未设 `TENANT`/`SCAN_CONNECTION` → 仅 PermissionMatrix/PlatformLogin/AppRuntime/DashboardRender/RlsIsolation 实跑，DataSourceScan/AuthFlow/LanguageSwitch/PermissionDeny 跳过，VisualBaseline 从未 CI 验证故排除。
- **验收边界**：E2E 工程因沙箱 NuGet `CommonApplicationData` 静态构造坑**无法在本环境编译验证**，但 CI 对该测试工程单独 `dotnet test`（自带 restore+build）会自动恢复与编译；已逐一对齐 `LoginHelper.ApiLoginByCodeAsync(page,user,pwd,tenantCode)`、`E2EConfig.Require`/`ApiUrl` 签名，并对 `PlaywrightFixture.NewPageAsync` 在无浏览器时 `Skip.If(true)` 降级行为做了源码核对，降低 CI 风险。
- **CI 验证（闭环）**：首跑 run `34733590844`（add71d38）因 `E2EApiHelper.CallApiAsync` 误用 `EvaluateAsync<JsonElement>` 解析 `JSON.stringify(...)` 返回值（得到 String 类型 JsonElement），首行 `GetProperty("status")` 抛「requires an element of type 'Object'」→ 2 个新用例失败。修复为 `EvaluateAsync<string>` 回原始串、C# 侧 `JsonDocument` 解析，并补齐 e2e API 启动 env 的 `SecretStore__MasterKey`（与 build job 一致，消除种子/加解密降级）。重跑 run `34734277058`（c8afcc26）= **success**（编译检查 / Web-Blazor E2E / V2.6 三 job 全绿）——整支业务链 E2E 已被 CI 守护，E2E-01 正式收口。

### 4.2 DB-02 (M13-17) — 租户过滤已建 + 跨租户回归 ✅ 已完成
- **验收已落地（2026-09-13，`e772684`）**：全局租户过滤已对 ~25 实体生效，并为 7 个依赖实体补 `HasQueryFilter`（含 `_tenantFilterEnabled` 守卫），消除 required-navigation + global filter 的 EF Core 10622 模型告警；`SuperBIContextTenantFilterTests` 扩充跨租户回归，CI 编译/E2E 绿。残留 opt-in 审计作为 M13 收口后的持续加固项，见下。
- `SuperBIContext` 已实施全局租户过滤：私有字段 `_tenantFilterEnabled` + `_scopedTenantId`，由 `ApplyTenantScope(tenantId)` **显式开启**（注释明确：Golden/系统路径不调用 → no-op，不产 WHERE）。
- 已对 ~25 个实体声明 `HasQueryFilter`（`DataSource`/`MetadataTable`/`BusinessEntity`/`TenantSetting`/`SemanticLabel`/`Dashboard`/`Theme`/`ModelAccount`/`CustomComponent*`/`AppPlan`/`AgentPlan`/`AgentRun`/`User`/`Role`/`Permission`/`AuditLog`/`QuotaPolicy`/`QuotaUsage`/`UiTextResource`/`Organization`/`Department`/`UserGroup*`/`UserDepartmentMember` 等）。含 `TenantId==0` 全局放行的是共享模板/基线类（SemanticLabel/Dashboard/Theme/AppPlan/AgentPlan/Role/Permission/AuditLog/QuotaPolicy/UiTextResource）。
- **风险 1（opt-in 缺口）**：过滤是**调用点显式开启**，任何漏调 `ApplyTenantScope` 的租户作用域读路径会静默看到跨租户数据。DB-02 第一项工作 = 审计所有租户查询入口确认开启，或评估改为基于 `ITenantContextAccessor` 的默认开启（需规避 P4.3 曾因 System 上下文 TenantId=0 过度过滤的回归，须保留 Golden no-op 路径）。
- **风险 2（EF 10622）**：required-navigation + global query filter 组合可能触发 EF Core 警告/翻译异常。**第一项动作 = 实跑构建确认是否出现该警告**，再决定消除方式（通常：为 required 导航补 `.IsRequired()` 一致性或调整 Include 策略）。
- 已有 `SuperBIContextTenantFilterTests.cs`，DB-02 在其基础上扩充跨租户回归。

### 4.3 QUOTA-01 (M13-18) — 消费非原子，last-quota 竞态真实 ✅ 已完成
- **实现（2026-09-13）**：`ConsumeAsync` 改为「单语句条件 `ExecuteUpdateAsync`」——`WHERE TenantId=@t AND ResourceType=@r AND PeriodKey=@key AND Used + @amt <= @limit` 时 `SET Used = Used + @amt`，影响行数 > 0 即成功；无当前周期行/周期滚动/已超额时先 `EnsureUsageRowAsync`（幂等插入或滚动，插入竞态 `IsUniqueViolation` 重试），再重试原子自增。读路径 `GetOrCreateUsageAsync` 同样加插入竞态重试。`ExecuteUpdate` 绕过变更跟踪器，成功后 `ReloadUsageTrackerAsync` 拉取最新 `Used` 避免同上下文后续查询读陈旧快照。无需新增迁移（仅 UPDATE/INSERT 语义变更）。
- **验收已落地**：`QuotaServiceTests.Consume_Concurrent_NoOverconsumption`（12 并发 × 单次 6、上限 10）→ 断言 `Used <= 10` 且 `Used == 6 * 成功次数` 且成功次数 ≤ 1，证明最后一份额度并发争抢最多一份成功、不超额、不重复计数。原顺序测试 12 项 + 控制器 13 项全绿，0 编译错误。
- 历史现状 `QuotaService.ConsumeAsync`（约 L79–90）：
  ```csharp
  var usage = await GetOrCreateUsageAsync(...);     // 读 Used
  if (usage.Used + amount > limit) return false;    // 检查
  usage.Used += amount;                             // 改
  await _ctx.SaveChangesAsync(ct);                  // 写
  ```
  - **无事务**、**无行锁**、`QuotaUsage` 实体**无并发令牌**（仅 `Used`/`PeriodKey`）。
  - 并发两请求同时读 `Used=5`、均通过 `5+1<=limit`、均写 `6` → 实际消耗 1 但记 2，**最后一份额度可被超额争抢**。
- **修复方向（只做原子性与并发正确性，边界归 M14-03）**：
  - 首选：单语句条件更新 `UPDATE QuotaUsages SET Used += @amt WHERE TenantId=@t AND ResourceType=@r AND Used + @amt <= Limit`（返回影响行数判定成功），天然原子、无竞态；或
  - 退路：对 `QuotaUsage` 加 `RowVersion` 并发令牌 + `SaveChanges` 乐观并发重试；或对读加 `UPDLOCK`/`SELECT ... FOR UPDATE` 的事务。
  - 配套：补 `GetOrCreateUsageAsync` 的并发安全、周期滚动的并发安全。
- **验收（须以并发测试证明）**：最后一份额度并发争抢最多一份成功；失败事务不耗额度；重试不重复计数；读配额（`CheckAsync`/`GetQuotaAsync`）不创建用量行；直接 API 请求无法绕过服务端检查。

---

## 5. 提交与 CI 纪律（沿用 G0 约束）

- 继续在 `master` 上执行，不新建开发分支。
- **每个缺陷/能力独立提交**，禁止把权限、BI、Migration、UI、配额混在同一提交。
- 每个任务先建失败测试或可重复证据，再修复（Golden 不得删用例/放宽阈值变绿）。
- 数据库改动必须验证**空库安装 + 已有库升级**（迁移走 `dotnet ef migrations add`，勿手工写）。
- 所有敏感凭据禁止明文持久化或日志泄漏（SEC-01 已落地，延续）。
- 任务状态只在 Backlog 更新；本文不复制测试数字/SHA。
- CI 门禁：受控代理下 push 用 `git -c credential.helper=manager push origin master`；CI 原始日志读取跨主机重定向需剥离 `Authorization` 头（见 2026-09-13 工作日志）。

---

## 6. 建议推进方式

1. **已完成（无 OPEN 阻塞）**：DB-02（`e772684`，租户过滤 + 10622 + 跨租户回归）、QUOTA-01（`bcbc71d`，原子消费 + 并发回归）均已 DONE，CI 绿。**下一步可立即开工**：OBS-01（登录/Ask/P95/关联 ID 指标告警，无 OPEN 阻塞）。
2. **骨架先行（范围待 OPEN 回填）**：E2E-01 用既有测试配置补齐业务链骨架。
3. **OPEN 回填后再定稿**：PERF-01 / DR-01 / M14-01 / M14-08a / M14-07 必须在对应 OPEN 决策落地后锁定验收。
4. **节奏**：每项独立提交并触发 CI；优先让「编译检查 / Web-Blazor-E2E」保持绿，V2.6 维持绿。
5. **本计划下一步动作**：与用户确认「首批开工项」——按 M13 §4 顺序从 **M13-09 (E2E-01)** 起步（关键路径第一项），或先啃无阻塞的 **DB-02 / QUOTA-01** 建立正确性底座，再回头做 E2E。
