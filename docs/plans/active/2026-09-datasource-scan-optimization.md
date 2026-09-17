# 数据源扫描页面优化方案（2026-09-16）

> 范围：数据源扫描相关页面与后端（中断 / 续显 / 提示 / 接入即扫 / 删除 / 失败重试 / 高亮）。
> 现状核对依据：`MetadataController.cs`、`MetadataScanHostedService.cs`、`MetadataScanJob.cs`、`ScanTelemetry.cs`、`MetadataScannerService.cs`、`DataSources.razor`、`DataSourceDetail.razor`。

## 0. 现状盘点（已实现，避免重复造轮子）

| 能力 | 现状 | 证据 |
|---|---|---|
| 任务化扫描 | `ScanDataSource` 建 `MetadataScanJob`(Queued) 入队，202 返回 jobId | `MetadataController.cs:61-98` |
| 富进度遥测 | `ScanTelemetry` 含阶段 / 表·列·语义·向量 processed·total / 当前对象 / 变化摘要 / 最近事件(20条) | `ScanTelemetry.cs:109-151` |
| 后台执行 | `MetadataScanHostedService` 出队执行，`JobProgress` 实时回写 `ProgressDetailsJson` | `MetadataScanHostedService.cs:65-212` |
| 前端轮询 | `DataSourceDetail.PollScanAsync` 每 1.5s 拉 `GetScanJob`，显示阶段/进度/计数/事件/错误 | `DataSourceDetail.razor:323-366` |
| 取消 | **无**。前端 `_scanCts` 只取消"轮询"，后端 `ProcessJobAsync` 仅传 `stoppingToken`(进程关闭) | `DataSourceDetail.razor:325`、`MetadataScanHostedService.cs:122` |
| 重进入续显 | **无**。`OnAfterRenderAsync→Load()` 不加载进行中任务，`_scanJob` 重进为 null | `DataSourceDetail.razor:282-294` |
| 失败重试 | 仅向量"每表重试 1 次"，任一失败即末尾 `throw` → 整扫失败；表/列无重试 | `MetadataScannerService.cs:370-446` |
| 数据源删除 | **无** 端点（grep `DELETE /api/data-sources` 零命中），列表页无删除按钮 | — |
| 状态枚举 | `Queued/Running/Succeeded/Failed`，**缺 Cancelling/Cancelled/部分成功** | `MetadataScanJob.cs:60-73` |

结论：后端已具备"可取消就绪"骨架（`ScanAsync` 全程 `ct.ThrowIfCancellationRequested()`，见 `MetadataScannerService.cs:164,365`），缺的是**触发入口 + 状态机补全 + 前端续接**。

---

## 1. 扫描可中断（R1）

**缺口**：用户无法真正中断后台扫描；`_scanCts` 只停前端轮询。

**改动**
- 状态机：`MetadataScanJobStatus` 增 `Cancelling`、`Cancelled`（可选 `SucceededWithErrors`，见 R6）。
- 后台服务：`MetadataScanHostedService` 维护 `ConcurrentDictionary<long, CancellationTokenSource> _jobCts`（单例内）。`ProcessJobAsync` 改为用 `linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, jobCts.Token)` 传给 `ScanAsync`。
- 取消端点：`POST /api/data-sources/{id}/metadata/scan/{jobId}/cancel`（沿用 `GetScanJob` 的鉴权：租户 + `MetadataScan` 权限 + 数据源授权）。
  - 若 `Queued`：置 `Cancelling`，处理器取走时见 `Cancelling` 直接标记 `Cancelled` 不执行。
  - 若 `Running`：置 `Cancelling` + 持久化，并 `_jobCts[jobId]?.Cancel()` → `ScanAsync` 观察 `ct` 抛出 `OperationCanceledException`。
- 处理器区分：`catch (OperationCanceledException)` 时，若 `job.Status==Cancelling` → `Cancelled`（置 `FinishedAt`、`Stage="Cancelled"`、保留已落库元数据）；`catch(Exception)` 仍 → `Failed`。
- 前端：`_scanJob.Status==Running`（及 `Queued`）显示「中断」按钮 → 调取消端点 → 继续轮询至 `Cancelled`；状态徽章新增"已中断"（灰）。

**验收**：运行中点击中断 → 数秒内 `Running→Cancelling→Cancelled`；已写入元数据保留；无半表孤儿（in-flight `SaveChanges` 撤销由 EF 事务边界控制，需确认是否包阶段提交）。

---

## 2. 退出重进状态继续显示（R2）

**缺口**：重进详情页不加载进行中任务，监控面板消失。

**改动**
- 端点：`GET /api/data-sources/{id}/metadata/scan/latest` 返回该租户+数据源最新非终态（Queued/Running）任务，复用 `GetScanJob` 响应体；无则 204/null。
- 前端：`DataSourceDetail.OnAfterRenderAsync→Load()` 增 `await LoadLatestScanAsync()`；若存在非终态任务 → 设 `_scanJob` 并 `PollScanAsync(jobId)` 续轮询（**不重复发起扫描**）。
- 列表页增强（可选但推荐）：`DataSources.razor` 行内对"最新任务 Running/Queued"显示「扫描中」小徽章 + 迷你进度，点击直达详情续看。
- 生命周期：`_scanCts` 已 `Dispose` 取消轮询；续接时复用 jobId，避免重复 CTS。

**验收**：起扫 → 返回列表或跳其他页 → 回详情 → 监控面板按当前阶段/进度/计数**继续**显示；轮询自动恢复；不触发二次扫描。

---

## 3. 扫描过程提示优化，显示更多数据（R3）

**现状已显示**：阶段、耗时、ETA、进度%、表/列/语义/向量进度、当前对象、变化摘要、最近 8 条事件、失败码/信息。

**增量**
- 阶段步进器：7 阶段（连接→发现→比对→同步→语义→向量→收尾）横向/纵向 stepper，已完成打勾、进行中高亮、未到灰显 —— 直接满足"合理显示所有阶段数据"。
- 吞吐：由 `ElapsedMs`+`Processed` 派生「~X 表/分、Y 列/分」。
- 分母显式化：「表 12/35（已发现 35）」，区分 discovered vs processed。
- 警告计数徽章 + 可展开警告列表（事件流里 `Level=Warning` 已有，补汇总）。
- 重试指示（联动 R6）：某对象重试时显示「重试 第2/3次」。
- 顶部显示 dbType + 已发现表数。

**验收**：监控含阶段 stepper、吞吐、发现总数、警告、重试次数。

---

## 4. 添加数据库未直接扫描（R4 — 口径待确认）

> ⚠️ 这条语义有歧义，给出推荐口径，请确认是否一致。

**推荐口径**：接入数据源后默认进入「待扫描」；在新增弹窗提供「保存并立即扫描」一键接入+扫描，并在列表/详情提供醒目「立即扫描」入口（当前新增仅 `POST` 建连、不扫描，需手动进详情点扫描，见 `DataSources.razor:275-297` `Save()`）。

**备选口径**：
- (B) 支持"仅注册不扫描"（延后扫描）—— 但当前 `Save()` 本就不扫描，疑似已默认，故不优先。
- (C) 列表增加「未扫描」筛选 + 行内快捷扫描（针对历史已加未扫的数据源）。

**改动（按推荐口径）**
- 新增弹窗：「保存并接入」(现状) 之外加「保存并扫描」主按钮；成功后 `StartScanAsync` 并导航到 `data-sources/{id}` 直接轮询。
- 列表行：对 `lastScanAt` 为空（"从未扫描"）的数据源显示「立即扫描」快捷按钮（复用 `StartScanAsync`）。
- 所有新文案走 i18n 四处一致（见 §6）。

请回复确认采用 A / B / C，或给出确切语义。

---

## 5. 数据源删除（R5）

**缺口**：无 `DELETE /api/data-sources/{id}` 端点，列表页无删除按钮（`DataSources.razor:58-71` 行操作仅 管理/编辑/启用停用/测试）。

**改动**
- 端点：`DELETE /api/data-sources/{id}`（租户 + `MetadataEdit` 权限 + 数据源授权）。
  - 守卫：① `Enabled==true` → 409 须先停用；② 存在 Queued/Running 扫描任务 → 409「扫描中不可删除」。
  - 清理（显式有序，参照孤儿逻辑保留 RLS/学习保护）：`DataSourceAccess` 授权、`RowLevelSecurityPolicies`、`MetadataScanJobs`、`MetadataDictionaryConfigs`、再 `MetadataTables`(+`Columns`+`Semantics`+向量)——或确认 `DataSource.cs`/`SuperBIContext` 已有级联后直接删源。须确认关系与级联策略。
  - 响应不回显连接串。
- 前端：`DataSources.razor` 行内加「删除」(`PermissionGuard MetadataEdit`) → `SbModal` 确认 → `Api.DeleteAsync` → toast + `LoadSources()`。Enabled/扫描中时禁用并给提示。

**验收**：删除连带其元数据/授权/策略/任务清理；启用中或扫描中被阻；需二次确认。

---

## 6. 失败数据重扫、多次失败才跳出、记录错误（R6）

**缺口**：向量任一失败即整扫失败（`MetadataScannerService.cs:445` `throw`）；表/列无重试；错误只在顶层 `ErrorCode/ErrorMessage` 一瞥，无逐项记录。

**改动**
- 重试策略：新增 `ScanRetryPolicy`（如 `MaxAttempts=3`）。覆盖：表结构读取、单表列读取、向量索引（取代当前"重试1次"）、语义批次。
- 逐项容错：`ScanAsync` 对非关键失败 **不整扫中止**：耗尽重试后标记该对象「跳过并记录」，继续后续；仅"连接/发现表"等关键阶段失败才 `throw`→`Failed`。
- 错误记录：扩展 `ScanProgressDetails` 增 `FailedItems: List<ScanFailureItem>`（`ObjectType/ObjectName/Attempts/LastErrorCode/LastErrorMessage`，**脱敏**，复用 `Sanitize` 口径）+ `ErrorLog`(append-only)。持久化进 `ProgressDetailsJson`。
- 终态：有失败项但元数据已产出 → 新状态 `SucceededWithErrors`（或沿用 `Succeeded`+`WarningsCount>0` 并在面板提示）；仅关键阶段失败才 `Failed`。
- 监控（联动 R3/R7）：面板「失败项 N」可展开，显示每项重试次数 + 最后错误；终态横幅区分"完成(有错误)"。
- 可选：详情页加「仅重扫失败项」按钮。

**验收**：抖动表/向量重试至 3 次后跳过并记录，其余完成并显示"完成(有错误)"；错误在面板与任务记录可见。

---

## 7. 提示文本高亮 / 不同颜色标明关键信息（R7）

**现状**：监控有 `is-warning`(事件)、`is-fail/is-ok/is-running`(状态)、徽章类；较基础。

**改动（CSS + 标记，置于 RCL）**
- 定义语义文本色变量：`--sb-text-ok`(绿) / `--sb-text-warn`(琥珀) / `--sb-text-err`(红) / `--sb-text-info`(蓝) / `--sb-text-key`(强/主色，用于关键数字)。
- 应用：阶段标签(info)、当前对象名(key)、计数(key)、警告(warn)、错误(err)、成功横幅(ok)、ETA/吞吐(muted→info)；阶段 stepper 三态着色。
- 事件流按 `Level` 着色（Info 默认 / Warning 琥珀 / Error 红，补 `is-error`）。
- 无障碍：不只靠颜色——配图标/文字标签；关键信息（表名/计数/错误码）用 `<strong class="hl-…">` 包裹。

**验收**：监控配色一致、关键数字高亮、错误/警告视觉可辨。

---

## 8. 接口 / 数据模型变更清单

- `MetadataScanJobStatus`：+ `Cancelling`、`Cancelled`、`SucceededWithErrors`(或 `Partial`)。
- `MetadataScanJob`：+ `CancelledAt?`；失败项可并入 `ProgressDetailsJson`（或加 `FailedItemsJson?`）。
- `ScanProgressDetails`：+ `FailedItems`、`Throughput*PerMin`、`DiscoveredTables/Columns`、`RetryAttempts`。
- `ScanTelemetry`：+ `RecordFailure(...)`、`MarkStageSkipped(...)`。
- 新端点：`POST …/scan/{jobId}/cancel`、`GET …/scan/latest`、`DELETE /api/data-sources/{id}`。
- `IMetadataScanQueue`：取消可借 `_jobCts` 注册表，无需改队列契约。

## 9. 风险与取舍

- 取消时机：in-flight `SaveChanges` 可能留半表，属可接受"部分可见"；若需原子，须将阶段提交包进事务（需评估大扫描事务成本）。
- 续接语义：重进仅**续接监控**进行中任务，不从中断点恢复扫描逻辑（过重）；后台扫到完成即可。
- 逐项重试会拉长抖动库的总时长 → 封顶尝试次数 + 每对象超时。
- 删除级联须尊重 RLS/学习保护，参照孤儿清理逻辑，勿误删受保护对象。
- 错误脱敏：`FailedItems` 错误同 `FailJobAsync.Sanitize`，禁含连接串/SQL。
- i18n：所有新增中文串经四处一致护栏（`Keys.cs`/`ResourceKeys.cs`/`LocalizationSeedService.cs`/razor），新增即补 `LocalizationSeedService` 种子。

## 10. 实施顺序（建议）

1. R1 后端：取消端点 + 状态 + `_jobCts` 注册表（地基，亦便于测试）。
2. R2 后端 `latest` 端点 + 前端续接。
3. R6 后端：逐项重试 + 错误记录 + 遥测扩展。
4. R5 删除端点 + UI。
5. R3 + R7 前端监控增强（stepper / 吞吐 / 高亮 CSS）。
6. R4 接入即扫 + 列表快捷扫描（待 §4 确认口径）。
7. 测试：扩展 `MetadataScanControllerTests` / `MetadataScannerServiceTests` / `ScanTelemetryTests`，新增取消/续接/重试用例；`DataSourceScanE2ETests.cs` 补充；视情况补 Golden。

## 11. 待确认

- **R4 口径**（A 接入即扫 / B 仅注册 / C 未扫筛选+快捷扫）。
- 删除级联：确认 `DataSource.cs` 关系与 `SuperBIContext` 级联策略，决定显式删还是靠级联。
- 终态命名：`SucceededWithErrors` 是否新增状态，还是复用 `Succeeded`+警告。
