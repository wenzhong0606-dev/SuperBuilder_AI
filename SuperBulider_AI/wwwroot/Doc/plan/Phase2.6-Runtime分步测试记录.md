# Phase 2.6 Runtime 分步测试记录

> 所属开发阶段：Phase 2.6 — Query Evaluation Framework
> 唯一源码基线：GitHub `master`
> 主开发计划：`SuperBuilder AI Native BI Phase开发计划-V2.0.md`
> 管理总则：`Phase开发测试管理总则.md`
> 记录原则：完整测试步骤一次性固化；每次只执行当前一个地址；用户返回原始 JSON 后判定；完成后更新对应步骤内容并提交 master，再进入下一步。

## 一、完整测试步骤

| 步骤 | 测试项 | 地址/输入 | 状态 |
|---|---|---|---|
| STEP-01 | SQL Server Runtime | `/evaluation/local-runtime/sqlserver` | PASS |
| STEP-02 | Qdrant Runtime | `/evaluation/local-runtime/qdrant` | PASS |
| STEP-03 | 本地基础设施汇总 | `/evaluation/local-runtime/infrastructure` | PASS |
| STEP-04 | Golden Dataset Cases 加载 | `/evaluation/golden-runtime/cases?topK=10` | BLOCK |
| STEP-05 | Golden GQ-006 | `/evaluation/golden-runtime/run?caseId=GQ-006` | BLOCKED |
| STEP-06 | Golden GQ-010 | `/evaluation/golden-runtime/run?caseId=GQ-010` | BLOCKED |
| STEP-07 | Golden GQ-011 | `/evaluation/golden-runtime/run?caseId=GQ-011` | BLOCKED |
| STEP-08 | Golden GQ-N004 | `/evaluation/golden-runtime/run?caseId=GQ-N004` | BLOCKED |
| STEP-09 | Golden GQ-N005 | `/evaluation/golden-runtime/run?caseId=GQ-N005` | BLOCKED |
| STEP-10 | Semantic Applicability：查询入库数量 | `/evaluation/semantic-applicability/debug`，输入“查询入库数量” | BLOCKED |
| STEP-11 | QueryPlan Confidence：GQ-006 | `/evaluation/diagnostics/query-plan-confidence`，输入 GQ-006 | BLOCKED |
| STEP-12 | QueryPlan Confidence：GQ-011 | `/evaluation/diagnostics/query-plan-confidence`，输入 GQ-011 | BLOCKED |
| STEP-13 | Golden Baseline 列表/当前基线 | `/evaluation/diagnostics/golden-baselines` | BLOCKED |
| STEP-14 | Golden 全量 Regression | `/evaluation/golden-runtime/run` | BLOCKED |
| STEP-15 | Coverage | `/evaluation/golden-runtime/coverage`（执行前以 master Controller 为准） | BLOCKED |
| STEP-16 | Quality Gate | `/evaluation/golden-runtime/quality-gate`（执行前以 master Controller 为准） | BLOCKED |
| STEP-17 | Release Gate | `/evaluation/golden-runtime/release-gate` | BLOCKED |
| STEP-18 | Phase 2.6 最终验收 | 综合结果，不新增测试项目 | BLOCKED |

> STEP-04 为当前阻塞步骤。根据测试纪律，后续步骤不得在 Golden Applicability Gate 阻塞未解决前跳过执行。

## 二、测试纪律

```text
当前步骤唯一地址
→ 用户返回完整原始 JSON
→ 判定 PASS / FAIL / BLOCK
→ 更新该步骤的完整内容
→ GitHub master commit
→ 若 FAIL/BLOCK：源码定位根因 → 修复 → 本地 git pull → Build → 重新测试当前步骤
→ PASS 后才进入下一步骤
```

- 不重复已通过步骤。
- 不跳过失败/阻塞步骤。
- 不一次要求用户执行多个地址。
- 每个已完成步骤必须保存：地址、原始 JSON、判定、关键验收事实、结论、Commit。
- API 存在不等于 Runtime PASS。
- C.13.3-LR 13/13 已完成，不重新执行。
- 不新建独立 Test Project，优先使用现有 Controller / Runtime Action。

## 三、本地 Runtime 测试规则

Phase 2.6 的 C.13.3 本地 Runtime 规则统一收录于本文件，不再建立独立补充文档。

### 3.1 测试链路

```text
GitHub master
↓
开发机拉取源码
↓
本地 Release Build
↓
本地 SQL Server / Qdrant
↓
SuperBuilder Web API
↓
现有 Controller / Action
↓
Metadata Fixture / Metadata Vector
↓
QueryPlan Evaluation
↓
Golden / Gate
```

正式 SQL Server / Qdrant 用于真实 Runtime 验证；GitHub Actions 用于本地通过后的可重复 CI 验证，不应因 Runner 基础设施差异反复修改业务源码。

### 3.2 基础设施要求

- SQL Server 必须通过正式 `SuperBIContext` 验证，并执行 `SELECT 1`。
- Qdrant 当前正式版本为 1.19.0；HTTP 6333 与 gRPC 6334 必须可用。
- `TCP 端口开放` 不等于 `Ready`，至少应验证 Process/Container、TCP、Protocol、Authentication、Application Operation。

### 3.3 Metadata Fixture 测试

C.13.3 测试 Fixture 仅用于测试环境，不替代生产 Metadata 来源。

```text
Document/table.csv
Document/column.csv
Document/Semantic.csv
↓
MetadataCsvFixtureService
↓
SuperBIContext
↓
Metadata Vector
↓
Qdrant
```

生产链路仍为：

```text
SQL Server
↓
SuperBIContext
↓
Metadata
```

测试时优先通过现有 Controller / Action 验证真实 Service，不新建独立 Test Project、临时测试数据库项目或测试应用。

### 3.4 Runtime 结果记录

每次执行必须保存完整原始结果；异常必须记录：

```text
exceptionType
message
innerMessage
```

测试顺序原则为：

```text
Build
↓
Infrastructure
↓
Metadata Source / Import
↓
Metadata Vector
↓
Semantic / QueryPlan
↓
Golden Regression
↓
Coverage
↓
Quality
↓
Release Gate
```

## 四、STEP-01 — SQL Server Runtime

地址：`http://localhost:5032/evaluation/local-runtime/sqlserver`

原始 JSON：
```json
{"passed":true,"stage":"SqlServer","message":"SQL Server 认证与 SELECT 1 均通过。","elapsedMs":3294,"details":{"connected":true,"select1":1,"database":"SuperBuilder_Platform","server":"localhost"}}
```

判定：**PASS / COMPLETE**

结论：SQL Server 认证、连接和 `SELECT 1` 正常。

## 五、STEP-02 — Qdrant Runtime

地址：`http://localhost:5032/evaluation/local-runtime/qdrant`

原始 JSON：
```json
{"passed":true,"stage":"Qdrant","message":"Qdrant HTTP healthz 通过。","elapsedMs":2056,"details":{"host":"localhost","httpPort":6333,"grpcPort":6334,"httpHealthUrl":"http://localhost:6333/healthz","statusCode":200,"responseBody":"healthz check passed"}}
```

判定：**PASS / COMPLETE**

结论：Qdrant HTTP Healthz 通过，HTTP 200，基础服务健康。

## 六、STEP-03 — 本地基础设施汇总

地址：`http://localhost:5032/evaluation/local-runtime/infrastructure`

原始 JSON：
```json
{"passed":true,"sqlServer":{"passed":true,"stage":"SqlServer","message":"SQL Server 认证与 SELECT 1 均通过。","elapsedMs":101,"details":{"connected":true,"select1":1,"database":"SuperBuilder_Platform","server":"localhost"}},"qdrant":{"passed":true,"stage":"Qdrant","message":"Qdrant HTTP healthz 通过。","elapsedMs":2046,"details":{"host":"localhost","httpPort":6333,"grpcPort":6334,"httpHealthUrl":"http://localhost:6333/healthz","statusCode":200,"responseBody":"healthz check passed"}}}
```

判定：**PASS / COMPLETE**

结论：SQL Server 与 Qdrant 两项基础 Runtime 均通过。

## 七、STEP-04 — Golden Dataset Cases 加载

### 地址

`http://localhost:5032/evaluation/golden-runtime/cases?topK=10`

### 原始返回 JSON

保留本次实际运行的完整 JSON 作为历史证据；当前结果为 Golden Applicability Gate 阻塞，后续步骤不得跳过。

判定：**BLOCK**

关键事实：当前 Golden Regression 入口曾出现 Positive Case 大面积 `NotResolved`，其中 GQ-U001 的 `NotResolved` 预期匹配；该结果说明安全 Gate 工作，但 Positive Applicability 尚未完成闭环。

## 八、Phase 2.6 Exit

Phase 2.6 只有在开发计划全部任务、Runtime、Golden、Coverage、Quality、Release Gate 均有证据后才能 COMPLETE。

如果源码已实现但 Runtime 未验证：

`IMPLEMENTED_BUT_UNVERIFIED`

不得标记 COMPLETE。