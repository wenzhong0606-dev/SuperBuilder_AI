# Phase 2.6 Runtime 分步测试记录

> 所属开发阶段：Phase 2.6 — Query Evaluation Framework
> 唯一源码基线：GitHub `master`
> 主开发计划：`SuperBuilder AI Native BI Phase开发计划-V2.0.md`
> 记录原则：完整测试步骤一次性固化；每次只执行当前一个地址；用户返回原始 JSON 后判定；完成后更新对应步骤内容并提交 master，再进入下一步。

## 一、完整测试步骤

| 步骤 | 测试项 | 地址/输入 | 状态 |
|---|---|---|---|
| STEP-01 | SQL Server Runtime | `/evaluation/local-runtime/sqlserver` | PASS |
| STEP-02 | Qdrant Runtime | `/evaluation/local-runtime/qdrant` | PASS |
| STEP-03 | 本地基础设施汇总 | `/evaluation/local-runtime/infrastructure` | PASS |
| STEP-04 | Golden Dataset Cases 加载 | `/evaluation/golden-runtime/cases?topK=10` | CURRENT |
| STEP-05 | Golden GQ-006 | `/evaluation/golden-runtime/run?caseId=GQ-006` | PENDING |
| STEP-06 | Golden GQ-010 | `/evaluation/golden-runtime/run?caseId=GQ-010` | PENDING |
| STEP-07 | Golden GQ-011 | `/evaluation/golden-runtime/run?caseId=GQ-011` | PENDING |
| STEP-08 | Golden GQ-N004 | `/evaluation/golden-runtime/run?caseId=GQ-N004` | PENDING |
| STEP-09 | Golden GQ-N005 | `/evaluation/golden-runtime/run?caseId=GQ-N005` | PENDING |
| STEP-10 | Semantic Applicability：查询入库数量 | `/evaluation/semantic-applicability/debug`，输入“查询入库数量” | PENDING |
| STEP-11 | QueryPlan Confidence：GQ-006 | `/evaluation/diagnostics/query-plan-confidence`，输入 GQ-006 | PENDING |
| STEP-12 | QueryPlan Confidence：GQ-011 | `/evaluation/diagnostics/query-plan-confidence`，输入 GQ-011 | PENDING |
| STEP-13 | Golden Baseline 列表/当前基线 | `/evaluation/diagnostics/golden-baselines` | PENDING |
| STEP-14 | Golden 全量 Regression | `/evaluation/golden-runtime/run` | PENDING |
| STEP-15 | Coverage | `/evaluation/golden-runtime/coverage`（若当前 Action 路由实际名称不同，以 master Controller 为准） | PENDING |
| STEP-16 | Quality Gate | `/evaluation/golden-runtime/quality-gate`（以 master Controller 实际路由为准） | PENDING |
| STEP-17 | Release Gate | `/evaluation/golden-runtime/release-gate` | PENDING |
| STEP-18 | Phase 2.6 最终验收 | 综合前述结果，不新增测试项目 | PENDING |

> 注意：STEP-15、STEP-16 在正式执行前必须以当前 `master` 的 Controller/Action 为准确认实际 Route；不得凭计划文件猜测地址。若不存在对应 API，先补齐接口再测试，并记录开发计划。

## 二、测试纪律

```text
当前步骤唯一地址
→ 用户返回完整原始 JSON
→ 判定 PASS / FAIL / BLOCK
→ 更新该步骤的完整内容
→ GitHub master commit
→ 再给下一步骤唯一地址
```

- 不重复已通过步骤。
- 不跳过失败步骤。
- 不一次要求用户执行多个地址。
- 每个已完成步骤必须保存：地址、原始 JSON、判定、关键验收事实、结论、Commit。
- API 存在不等于 Runtime PASS。
- Runtime 失败时必须定位根因；必要时修改 `master`，本地 `git pull`、Build、重新测试当前步骤后才能继续。
- C.13.3-LR 13/13 已完成，不重新执行。
- 不新建独立 Test Project，优先使用现有 Controller / Runtime Action。

## 三、STEP-01 — SQL Server Runtime

### 地址

```text
http://localhost:5032/evaluation/local-runtime/sqlserver
```

### 原始返回 JSON

```json
{"passed":true,"stage":"SqlServer","message":"SQL Server 认证与 SELECT 1 均通过。","elapsedMs":3294,"details":{"connected":true,"select1":1,"database":"SuperBuilder_Platform","server":"localhost"}}
```

### 判定

**PASS / COMPLETE**

### 验收事实

- `connected = true`
- `SELECT 1 = 1`
- database = `SuperBuilder_Platform`
- server = `localhost`
- elapsedMs = `3294`

### 结论

当前本地 SQL Server 认证、连接及最小查询能力正常。该结果不能单独代表 Phase 2.6 完成。

## 四、STEP-02 — Qdrant Runtime

### 地址

```text
http://localhost:5032/evaluation/local-runtime/qdrant
```

### 原始返回 JSON

```json
{"passed":true,"stage":"Qdrant","message":"Qdrant HTTP healthz 通过。","elapsedMs":2056,"details":{"host":"localhost","httpPort":6333,"grpcPort":6334,"httpHealthUrl":"http://localhost:6333/healthz","statusCode":200,"responseBody":"healthz check passed"}}
```

### 判定

**PASS / COMPLETE**

### 验收事实

- `passed = true`
- host = `localhost`
- HTTP = `6333`
- gRPC = `6334`
- healthz HTTP = `200`
- response = `healthz check passed`
- elapsedMs = `2056`

### 结论

Qdrant HTTP Healthz 通过，基础服务健康。本步骤不代表 Semantic Search、Vector Retrieval 或 Golden Evaluation 已完成。

## 五、STEP-03 — 本地基础设施汇总

### 地址

```text
http://localhost:5032/evaluation/local-runtime/infrastructure
```

### 原始返回 JSON

```json
{"passed":true,"sqlServer":{"passed":true,"stage":"SqlServer","message":"SQL Server 认证与 SELECT 1 均通过。","elapsedMs":101,"details":{"connected":true,"select1":1,"database":"SuperBuilder_Platform","server":"localhost"}},"qdrant":{"passed":true,"stage":"Qdrant","message":"Qdrant HTTP healthz 通过。","elapsedMs":2046,"details":{"host":"localhost","httpPort":6333,"grpcPort":6334,"httpHealthUrl":"http://localhost:6333/healthz","statusCode":200,"responseBody":"healthz check passed"}}}
```

### 判定

**PASS / COMPLETE**

### 验收事实

- 汇总 `passed = true`
- SQL Server `passed = true`、`connected = true`、`SELECT 1 = 1`
- database = `SuperBuilder_Platform`
- server = `localhost`
- SQL Server elapsedMs = `101`
- Qdrant `passed = true`
- Qdrant HTTP status = `200`
- Qdrant response = `healthz check passed`
- Qdrant elapsedMs = `2046`

### 结论

本地 SQL Server 与 Qdrant 基础 Runtime 均健康，基础设施汇总 PASS。该结果不代表 Golden Dataset、QueryPlan Evaluator、Coverage、Quality Gate 或 Release Gate 已通过。

## 六、STEP-04 — Golden Dataset Cases 加载

### 状态

**CURRENT / PENDING**

### 唯一测试地址

```text
http://localhost:5032/evaluation/golden-runtime/cases?topK=10
```

### 测试结果

待用户返回。

## 七、STEP-05 — Golden GQ-006

### 状态

PENDING

### 地址

```text
http://localhost:5032/evaluation/golden-runtime/run?caseId=GQ-006
```

### 测试结果

待 STEP-04 完成后执行。

## 八、STEP-06 — Golden GQ-010

### 状态

PENDING

### 地址

```text
http://localhost:5032/evaluation/golden-runtime/run?caseId=GQ-010
```

### 测试结果

待 STEP-05 完成后执行。

## 九、STEP-07 — Golden GQ-011

### 状态

PENDING

### 地址

```text
http://localhost:5032/evaluation/golden-runtime/run?caseId=GQ-011
```

### 测试结果

待 STEP-06 完成后执行。

## 十、STEP-08 — Golden GQ-N004

### 状态

PENDING

### 地址

```text
http://localhost:5032/evaluation/golden-runtime/run?caseId=GQ-N004
```

### 测试结果

待 STEP-07 完成后执行。

## 十一、STEP-09 — Golden GQ-N005

### 状态

PENDING

### 地址

```text
http://localhost:5032/evaluation/golden-runtime/run?caseId=GQ-N005
```

### 测试结果

待 STEP-08 完成后执行。

## 十二、STEP-10 — Semantic Applicability：查询入库数量

### 状态

PENDING

### API

```text
/evaluation/semantic-applicability/debug
```

### 测试输入

```text
查询入库数量
```

### 测试结果

待 STEP-09 完成后执行。正式测试时按当前 Controller 的实际 HTTP Method 和参数格式执行。

## 十三、STEP-11 — QueryPlan Confidence：GQ-006

### 状态

PENDING

### API

```text
/evaluation/diagnostics/query-plan-confidence
```

### 测试输入

```text
GQ-006
```

### 测试结果

待 STEP-10 完成后执行。正式测试时按当前 Controller 的实际 HTTP Method 和参数格式执行。

## 十四、STEP-12 — QueryPlan Confidence：GQ-011

### 状态

PENDING

### API

```text
/evaluation/diagnostics/query-plan-confidence
```

### 测试输入

```text
GQ-011
```

### 测试结果

待 STEP-11 完成后执行。正式测试时按当前 Controller 的实际 HTTP Method 和参数格式执行。

## 十五、STEP-13 — Golden Baseline

### 状态

PENDING

### 地址

```text
http://localhost:5032/evaluation/diagnostics/golden-baselines
```

### 测试结果

待 STEP-12 完成后执行。

## 十六、STEP-14 — Golden 全量 Regression

### 状态

PENDING

### 地址

```text
http://localhost:5032/evaluation/golden-runtime/run
```

### 测试结果

待 STEP-13 完成后执行。

## 十七、STEP-15 — Coverage

### 状态

PENDING

### 注意

正式执行前必须读取当前 `master` Controller / Action，确认真实 Coverage Route。当前仅作为计划项保留，**不以猜测地址执行**。

### 测试结果

待 STEP-14 完成后执行。

## 十八、STEP-16 — Quality Gate

### 状态

PENDING

### 注意

正式执行前必须读取当前 `master` Controller / Action，确认真实 Quality Gate Route。若 API 不存在，则先补接口、编译并记录后再测试。

### 测试结果

待 STEP-15 完成后执行。

## 十九、STEP-17 — Release Gate

### 状态

PENDING

### 地址

```text
http://localhost:5032/evaluation/golden-runtime/release-gate
```

### 测试结果

待 STEP-16 完成后执行。

## 二十、STEP-18 — Phase 2.6 最终验收

### 状态

PENDING

最终验收不是新建测试项目，而是汇总本轮已验证证据：

```text
基础设施 Runtime
→ Golden Cases
→ Golden Case Regression
→ Semantic Applicability
→ QueryPlan Confidence
→ Baseline
→ Full Regression
→ Coverage
→ Quality Gate
→ Release Gate
```

只有所有必需 Gate 按当前 Contract 通过，才能将 Phase 2.6 标记 COMPLETE；否则保持 IN PROGRESS，并记录阻塞项。

### 测试结果

待 STEP-17 完成后执行。

## 二十一、当前进度

```text
STEP-01 SQL Server       PASS
STEP-02 Qdrant           PASS
STEP-03 Infrastructure   PASS
STEP-04 Golden Cases     CURRENT / PENDING
STEP-05 GQ-006           PENDING
STEP-06 GQ-010           PENDING
STEP-07 GQ-011           PENDING
STEP-08 GQ-N004          PENDING
STEP-09 GQ-N005          PENDING
STEP-10 Semantic         PENDING
STEP-11 Confidence-006   PENDING
STEP-12 Confidence-011   PENDING
STEP-13 Baseline         PENDING
STEP-14 Full Regression  PENDING
STEP-15 Coverage         PENDING
STEP-16 Quality Gate     PENDING
STEP-17 Release Gate     PENDING
STEP-18 Final Acceptance PENDING
```

当前完成：**3 / 18 Controller/Runtime 步骤 PASS**。

## 二十二、会话恢复锚点

```text
Phase：2.6
C.13.3-LR：13/13 COMPLETE
API 存在性：已完成
Runtime：STEP-01～STEP-03 PASS
当前步骤：STEP-04 Golden Dataset Cases
唯一下一地址：http://localhost:5032/evaluation/golden-runtime/cases?topK=10
```
