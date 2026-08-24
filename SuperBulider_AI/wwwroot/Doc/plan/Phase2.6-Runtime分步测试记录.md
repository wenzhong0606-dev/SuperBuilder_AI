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
| STEP-04 | Golden Dataset Cases 加载 | `/evaluation/golden-runtime/cases?topK=10` | **BLOCK** |
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

## 三、STEP-01 — SQL Server Runtime

地址：`http://localhost:5032/evaluation/local-runtime/sqlserver`

原始 JSON：
```json
{"passed":true,"stage":"SqlServer","message":"SQL Server 认证与 SELECT 1 均通过。","elapsedMs":3294,"details":{"connected":true,"select1":1,"database":"SuperBuilder_Platform","server":"localhost"}}
```

判定：**PASS / COMPLETE**

结论：SQL Server 认证、连接和 `SELECT 1` 正常。

## 四、STEP-02 — Qdrant Runtime

地址：`http://localhost:5032/evaluation/local-runtime/qdrant`

原始 JSON：
```json
{"passed":true,"stage":"Qdrant","message":"Qdrant HTTP healthz 通过。","elapsedMs":2056,"details":{"host":"localhost","httpPort":6333,"grpcPort":6334,"httpHealthUrl":"http://localhost:6333/healthz","statusCode":200,"responseBody":"healthz check passed"}}
```

判定：**PASS / COMPLETE**

结论：Qdrant HTTP Healthz 通过，HTTP 200，基础服务健康。

## 五、STEP-03 — 本地基础设施汇总

地址：`http://localhost:5032/evaluation/local-runtime/infrastructure`

原始 JSON：
```json
{"passed":true,"sqlServer":{"passed":true,"stage":"SqlServer","message":"SQL Server 认证与 SELECT 1 均通过。","elapsedMs":101,"details":{"connected":true,"select1":1,"database":"SuperBuilder_Platform","server":"localhost"}},"qdrant":{"passed":true,"stage":"Qdrant","message":"Qdrant HTTP healthz 通过。","elapsedMs":2046,"details":{"host":"localhost","httpPort":6333,"grpcPort":6334,"httpHealthUrl":"http://localhost:6333/healthz","statusCode":200,"responseBody":"healthz check passed"}}}
```

判定：**PASS / COMPLETE**

结论：SQL Server 与 Qdrant 两项基础 Runtime 均通过。

## 六、STEP-04 — Golden Dataset Cases 加载

### 地址

`http://localhost:5032/evaluation/golden-runtime/cases?topK=10`

### 原始返回 JSON

```json
{"passed":false,"decision":"BLOCK","dataset":"query-plan-golden","version":"1.3","cases":[{"caseId":"GQ-001","category":"positive","enabled":true,"stage":"SemanticApplicabilityGate","decision":"BLOCK","passed":false,"expectedOutcomeSatisfied":false,"applicabilityState":"NotResolved","queryPlanEvaluationPassed":null,"confidenceDecision":null,"confidenceLevel":null,"confidenceScore":null,"reason":"Top Candidate 缺少 Golden SemanticText 的直接语义证据。"},{"caseId":"GQ-002","category":"positive","enabled":true,"stage":"SemanticApplicabilityGate","decision":"BLOCK","passed":false,"expectedOutcomeSatisfied":false,"applicabilityState":"NotResolved","queryPlanEvaluationPassed":null,"confidenceDecision":null,"confidenceLevel":null,"confidenceScore":null,"reason":"No direct EntityCount semantic evidence could be resolved."},{"caseId":"GQ-003","category":"positive","enabled":true,"stage":"SemanticApplicabilityGate","decision":"BLOCK","passed":false,"expectedOutcomeSatisfied":false,"applicabilityState":"NotResolved","queryPlanEvaluationPassed":null,"confidenceDecision":null,"confidenceLevel":null,"confidenceScore":null,"reason":"Top Candidate 缺少 Golden SemanticText 的直接语义证据。"},{"caseId":"GQ-004","category":"positive","enabled":true,"stage":"SemanticApplicabilityGate","decision":"BLOCK","passed":false,"expectedOutcomeSatisfied":false,"applicabilityState":"NotResolved","queryPlanEvaluationPassed":null,"confidenceDecision":null,"confidenceLevel":null,"confidenceScore":null,"reason":"Top Candidate 缺少 Golden SemanticText 的直接语义证据。"},{"caseId":"GQ-005","category":"positive","enabled":true,"stage":"SemanticApplicabilityGate","decision":"BLOCK","passed":false,"expectedOutcomeSatisfied":false,"applicabilityState":"NotResolved","queryPlanEvaluationPassed":null,"confidenceDecision":null,"confidenceLevel":null,"confidenceScore":null,"reason":"Top Candidate 缺少 Golden SemanticText 的直接语义证据。"},{"caseId":"GQ-006","category":"positive","enabled":true,"stage":"SemanticApplicabilityGate","decision":"BLOCK","passed":false,"expectedOutcomeSatisfied":false,"applicabilityState":"NotResolved","queryPlanEvaluationPassed":null,"confidenceDecision":null,"confidenceLevel":null,"confidenceScore":null,"reason":"Top Candidate 缺少 Golden SemanticText 的直接语义证据。"},{"caseId":"GQ-007","category":"positive","enabled":true,"stage":"SemanticApplicabilityGate","decision":"BLOCK","passed":false,"expectedOutcomeSatisfied":false,"applicabilityState":"NotResolved","queryPlanEvaluationPassed":null,"confidenceDecision":null,"confidenceLevel":null,"confidenceScore":null,"reason":"Top Candidate 缺少 Golden SemanticText 的直接语义证据。"},{"caseId":"GQ-008","category":"positive","enabled":true,"stage":"SemanticApplicabilityGate","decision":"BLOCK","passed":false,"expectedOutcomeSatisfied":false,"applicabilityState":"NotResolved","queryPlanEvaluationPassed":null,"confidenceDecision":null,"confidenceLevel":null,"confidenceScore":null,"reason":"No direct EntityCount semantic evidence could be resolved."},{"caseId":"GQ-009","category":"positive","enabled":true,"stage":"SemanticApplicabilityGate","decision":"BLOCK","passed":false,"expectedOutcomeSatisfied":false,"applicabilityState":"NotResolved","queryPlanEvaluationPassed":null,"confidenceDecision":null,"confidenceLevel":null,"confidenceScore":null,"reason":"Top Candidate 缺少 Golden SemanticText 的直接语义证据。"},{"caseId":"GQ-010","category":"positive","enabled":true,"stage":"SemanticApplicabilityGate","decision":"BLOCK","passed":false,"expectedOutcomeSatisfied":false,"applicabilityState":"NotResolved","queryPlanEvaluationPassed":null,"confidenceDecision":null,"confidenceLevel":null,"confidenceScore":null,"reason":"Top Candidate 缺少 Golden SemanticText 的直接语义证据。"},{"caseId":"GQ-011","category":"positive","enabled":true,"stage":"SemanticApplicabilityGate","decision":"BLOCK","passed":false,"expectedOutcomeSatisfied":false,"applicabilityState":"NotResolved","queryPlanEvaluationPassed":null,"confidenceDecision":null,"confidenceLevel":null,"confidenceScore":null,"reason":"Top Candidate 缺少 Golden SemanticText 的直接语义证据。"},{"caseId":"GQ-N001","category":"negative","enabled":true,"stage":"SemanticApplicabilityGate","decision":"BLOCK","passed":false,"expectedOutcomeSatisfied":true,"applicabilityState":"NotResolved","queryPlanEvaluationPassed":null,"confidenceDecision":null,"confidenceLevel":null,"confidenceScore":null,"reason":"No direct EntityCount semantic evidence could be resolved."},{"caseId":"GQ-N002","category":"negative","enabled":true,"stage":"SemanticApplicabilityGate","decision":"BLOCK","passed":false,"expectedOutcomeSatisfied":true,"applicabilityState":"NotResolved","queryPlanEvaluationPassed":null,"confidenceDecision":null,"confidenceLevel":null,"confidenceScore":null,"reason":"Top Candidate 缺少 Golden SemanticText 的直接语义证据。"},{"caseId":"GQ-N003","category":"negative","enabled":true,"stage":"SemanticApplicabilityGate","decision":"BLOCK","passed":false,"expectedOutcomeSatisfied":true,"applicabilityState":"NotResolved","queryPlanEvaluationPassed":null,"confidenceDecision":null,"confidenceLevel":null,"confidenceScore":null,"reason":"Top Candidate 缺少 Golden SemanticText 的直接语义证据。"},{"caseId":"GQ-N004","category":"negative","enabled":true,"stage":"SemanticApplicabilityGate","decision":"BLOCK","passed":false,"expectedOutcomeSatisfied":true,"applicabilityState":"NotResolved","queryPlanEvaluationPassed":null,"confidenceDecision":null,"confidenceLevel":null,"confidenceScore":null,"reason":"Top Candidate 缺少 Golden SemanticText 的直接语义证据。"},{"caseId":"GQ-N005","category":"negative","enabled":true,"stage":"SemanticApplicabilityGate","decision":"BLOCK","passed":false,"expectedOutcomeSatisfied":true,"applicabilityState":"NotResolved","queryPlanEvaluationPassed":null,"confidenceDecision":null,"confidenceLevel":null,"confidenceScore":null,"reason":"Top Candidate 缺少 Golden SemanticText 的直接语义证据。"},{"caseId":"GQ-A001","category":"ambiguous","enabled":true,"stage":"SemanticApplicabilityGate","decision":"BLOCK","passed":false,"expectedOutcomeSatisfied":false,"applicabilityState":"NotResolved","queryPlanEvaluationPassed":null,"confidenceDecision":null,"confidenceLevel":null,"confidenceScore":null,"reason":"Golden Case 预期 Applicability=Ambiguous，实际为 NotResolved。"},{"caseId":"GQ-U001","category":"unresolved","enabled":true,"stage":"SemanticApplicabilityGate","decision":"BLOCK","passed":true,"expectedOutcomeSatisfied":true,"applicabilityState":"NotResolved","queryPlanEvaluationPassed":null,"confidenceDecision":null,"confidenceLevel":null,"confidenceScore":null,"reason":"Golden Case 预期 Applicability=NotResolved，实际匹配。"}],"failedCases":[{"caseId":"GQ-001","category":"positive","stage":"SemanticApplicabilityGate","decision":"BLOCK","applicabilityState":"NotResolved","reason":"Top Candidate 缺少 Golden SemanticText 的直接语义证据。"},{"caseId":"GQ-002","category":"positive","stage":"SemanticApplicabilityGate","decision":"BLOCK","applicabilityState":"NotResolved","reason":"No direct EntityCount semantic evidence could be resolved."},{"caseId":"GQ-003","category":"positive","stage":"SemanticApplicabilityGate","decision":"BLOCK","applicabilityState":"NotResolved","reason":"Top Candidate 缺少 Golden SemanticText 的直接语义证据。"},{"caseId":"GQ-004","category":"positive","stage":"SemanticApplicabilityGate","decision":"BLOCK","applicabilityState":"NotResolved","reason":"Top Candidate 缺少 Golden SemanticText 的直接语义证据。"},{"caseId":"GQ-005","category":"positive","stage":"SemanticApplicabilityGate","decision":"BLOCK","applicabilityState":"NotResolved","reason":"Top Candidate 缺少 Golden SemanticText 的直接语义证据。"},{"caseId":"GQ-006","category":"positive","stage":"SemanticApplicabilityGate","decision":"BLOCK","applicabilityState":"NotResolved","reason":"Top Candidate 缺少 Golden SemanticText 的直接语义证据。"},{"caseId":"GQ-007","category":"positive","stage":"SemanticApplicabilityGate","decision":"BLOCK","applicabilityState":"NotResolved","reason":"Top Candidate 缺少 Golden SemanticText 的直接语义证据。"},{"caseId":"GQ-008","category":"positive","stage":"SemanticApplicabilityGate","decision":"BLOCK","applicabilityState":"NotResolved","reason":"No direct EntityCount semantic evidence could be resolved."},{"caseId":"GQ-009","category":"positive","stage":"SemanticApplicabilityGate","decision":"BLOCK","applicabilityState":"NotResolved","reason":"Top Candidate 缺少 Golden SemanticText 的直接语义证据。"},{"caseId":"GQ-010","category":"positive","stage":"SemanticApplicabilityGate","decision":"BLOCK","applicabilityState":"NotResolved","reason":"Top Candidate 缺少 Golden SemanticText 的直接语义证据。"},{"caseId":"GQ-011","category":"positive","stage":"SemanticApplicabilityGate","decision":"BLOCK","applicabilityState":"NotResolved","reason":"Top Candidate 缺少 Golden SemanticText 的直接语义证据。"},{"caseId":"GQ-A001","category":"ambiguous","stage":"SemanticApplicabilityGate","decision":"BLOCK","applicabilityState":"NotResolved","reason":"Golden Case 预期 Applicability=Ambiguous，实际为 NotResolved。"}],"scorecard":{"passed":false,"total":18,"executed":18,"passedCases":6,"failedCases":12,"overallPassRate":0.3333333333333333,"positivePassRate":0,"negativeDetectionRate":1,"ambiguousDetectionRate":0,"unresolvedDetectionRate":1,"hasUnexpectedApplicabilityState":true,"decision":"BLOCK","failedGates":["OverallPassRate=33.33% < minimum=90.00%","PositivePassRate=0.00% < minimum=95.00%","AmbiguousDetectionRate=0.00% < minimum=90.00%","UnexpectedApplicabilityState"]}}
```

### 判定

**BLOCK / FAIL**

### 验收统计

- dataset = `query-plan-golden`
- version = `1.3`
- total/executed = `18/18`
- passedCases = `6`
- failedCases = `12`
- overallPassRate = `33.33%`
- positivePassRate = `0%`
- negativeDetectionRate = `100%`
- ambiguousDetectionRate = `0%`
- unresolvedDetectionRate = `100%`
- failedGates：OverallPassRate、PositivePassRate、AmbiguousDetectionRate、UnexpectedApplicabilityState

### 阻塞模式

1. GQ-001、003、004、005、006、007、009、010、011 及部分 negative cases：`Top Candidate 缺少 Golden SemanticText 的直接语义证据`。
2. GQ-002、GQ-008 及部分 EntityCount：`No direct EntityCount semantic evidence could be resolved.`
3. GQ-A001：期望 `Ambiguous`，实际 `NotResolved`。
4. GQ-U001：期望 `NotResolved`，实际匹配，是当前唯一明确通过的 unresolved 检测。

### 当前源码审计结论

当前 `SemanticApplicabilityEvaluator` 的 Metric Resolution 只从 `IsSemanticVector` 且具有物理表/字段绑定的候选中选择，并使用 `ContainsSemanticText(top, metric.SemanticText)` 判断直接证据；EntityCount 另走 `ExtractEntitySemanticText` + `ContainsDirectEntityEvidence` 路径。当前 Golden v1.3 中 GQ-001 的 `semanticText` 明确为 `入库数量`，字段为 `quantity`；因此本次 Runtime BLOCK 已经证明“当前线上/本地 Semantic Search 返回结果”与 Golden Applicability 判定之间存在真实契约断点，不能简单把问题归因于 Golden 数据。源码事实与 Golden 数据分别需要继续核查。fileciteturn52file0 fileciteturn54file0

### 结论

STEP-04 不通过。后续 STEP-05～STEP-18 暂停，不允许跳过当前阻塞。下一动作不是继续测试，而是针对 SemanticApplicabilityGate 做源码级根因定位；修复后重新执行 STEP-04，只有 STEP-04 PASS 才恢复后续分步测试。

## 七、STEP-05～STEP-18

保持 BLOCKED，具体步骤、地址/输入和执行顺序已经预先固化在本文档第一节；不得因 STEP-04 BLOCK 而删除或改变这些步骤。

## 八、当前进度

```text
STEP-01 SQL Server       PASS
STEP-02 Qdrant           PASS
STEP-03 Infrastructure   PASS
STEP-04 Golden Cases     BLOCK
STEP-05 GQ-006           BLOCKED
STEP-06 GQ-010           BLOCKED
STEP-07 GQ-011           BLOCKED
STEP-08 GQ-N004          BLOCKED
STEP-09 GQ-N005          BLOCKED
STEP-10 Semantic         BLOCKED
STEP-11 Confidence-006   BLOCKED
STEP-12 Confidence-011   BLOCKED
STEP-13 Baseline         BLOCKED
STEP-14 Full Regression  BLOCKED
STEP-15 Coverage         BLOCKED
STEP-16 Quality Gate     BLOCKED
STEP-17 Release Gate     BLOCKED
STEP-18 Final Acceptance BLOCKED
```

完成：**3 / 18 PASS；1 / 18 BLOCK；14 / 18 BLOCKED**。

## 九、会话恢复锚点

```text
Phase：2.6
C.13.3-LR：13/13 COMPLETE
API 存在性：已完成
Runtime：STEP-01～STEP-03 PASS
当前步骤：STEP-04 BLOCK
阻塞层：SemanticApplicabilityGate
下一动作：源码级根因定位与修复，不跳过 STEP-04
```
