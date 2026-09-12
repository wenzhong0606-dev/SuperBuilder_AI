> **HISTORICAL SNAPSHOT / 历史快照**：本文是归档资料，只描述记录当时的计划、状态或审计判断。文中的“当前、唯一、已完成、未完成、风险、测试基线、HEAD”等均不得解释为现在的项目状态。当前事实请按 `docs/README.md` 的治理顺序核验。\n\n# Phase 2.6 C.13.2 — GQ-011 Ranking Order Resolution 修复进度记录

> 状态：FIX IMPLEMENTED / WAITING FOR BUILD & RUNTIME
> 当前 STEP：STEP-2.6-EVAL-02-FIX-01
> 源码基线：GitHub `master`
> 目标 Case：GQ-011
> 原问题：Detail Ranking 的 QueryPlan.Orders 实际为 0，QueryShape Contract FAIL

## 1. 根因冻结

完整读取当前 master 的相关 Model / Producer / Consumer / Controller / Golden / Evaluator 后，根因冻结为：

```text
QueryIntent
  ├─ IsRanking
  ├─ OrderBy
  └─ OrderDirection
        +
SemanticApplicability
  └─ 已确认 Metric Resolution
        ↓
QueryPlanSemanticResolutionFactory
        ↓
缺少 Ranking Order Resolution 汇合
        ↓
QueryPlanSemanticResolution.Orders = 0
        ↓
QueryPlanBuilder
        ↓
QueryPlan.Orders = 0
        ↓
GQ-011 QueryShape FAIL
```

责任边界：`QueryPlanSemanticResolutionFactory`。

不修改：`SemanticApplicabilityEvaluator`、`QueryPlanBuilder`、`QueryPlanQueryShapeScoringService`、`QueryPlanEvaluator`、Golden Contract。

## 2. 本次最小修复

新增 Ranking-aware Factory overload：

```csharp
From(SemanticApplicabilityResult applicability, QueryIntent intent)
```

规则：

1. 先复用既有 `From(applicability)`；
2. 只有 `intent.IsRanking == true` 且 `OrderBy` 非空时才生成 Order Resolution；
3. Order 的物理 Table / DataSource / Column 必须来自已确认的 Metric Resolution；
4. 禁止重新 Semantic Search；
5. 0 个候选或多个候选均抛出明确异常，禁止猜测；
6. 非 Ranking 调用保持原行为。

## 3. 调用链修改

已修改：

- `Services/BI/Evaluation/QueryPlanSemanticResolutionFactory.cs`
- `Services/BI/Evaluation/GoldenDatasetRunner.cs`
- `Controllers/EvaluationDiagnosticsController.cs`
- `Controllers/QueryPlanConfidenceDiagnosticsController.cs`

其中诊断 Controller 必须同步使用 Ranking-aware Resolution，避免 Golden Runner 与单 Case Runtime 使用不同 Contract。

## 4. 兼容性矩阵

| Case | 类型 | 预期影响 |
|---|---|---|
| GQ-001~GQ-005 | 非 Ranking Positive | Orders 保持 0，不改变 |
| GQ-006 | Aggregate Ranking Positive | Orders 应为 1，DESC，SUM |
| GQ-010 | Aggregate Ranking + Filter | Orders 应为 1，DESC，SUM |
| GQ-011 | Detail Ranking Positive | Orders 应为 1，DESC，NONE |
| GQ-N004 | Wrong Ranking Direction | 实际仍应 DESC，Golden ASC 应继续 FAIL |
| GQ-N005 | Wrong Ranking Limit | 实际仍应 Limit=10，Golden 20 应继续 FAIL |
| GQ-A001 | Ambiguous | Applicability 阶段处理，不进入 Ranking Order Resolution |
| GQ-U001 | Unresolved | Gate BLOCK，不进入 Ranking Order Resolution |

## 5. 安全原则

本修复不得：

- 修改 Golden Expected 以制造 PASS；
- 修改 Evaluator 降低 Assertion；
- 让 Builder 二次 Semantic Search；
- 将 Ranking 逻辑扩散到 SemanticApplicabilityEvaluator；
- 让非 Ranking QueryPlan 自动产生 Orders；
- 通过修改 Coverage / Quality / Release Gate 掩盖失败。

## 6. 当前 Commit

- 管理总则新增规则：`c74fc1447698adbcac66b3cfc3b3b1c72aedbf7b`
- Factory 修复：`544815518add98fe52f37898812bb943f075705d`
- Golden Runner：`66730983273d98a5c77a04285c256ce082332043`
- Evaluation Diagnostics：`36ae3b17e6bb0a6c29d9dfd36d27ec7ba4374067`
- Confidence Diagnostics：`0c184905a120206892383d626462ce791151621a`

## 7. 下一验证顺序

```text
当前 master
↓
git pull
↓
dotnet build
↓
GQ-011 单 Case Runtime
↓
GQ-006
↓
GQ-010
↓
GQ-N004
↓
GQ-N005
↓
Full Golden Regression
```

任何 Build / Runtime / Regression 出现 FAIL、BLOCK、REVIEW 或无法解释的回归，立即停止并标记 `COMPATIBILITY_RISK`，重新进行源码根因审计。

## 8. Exit 条件

本修复只有同时满足以下条件才标记 COMPLETE：

- GQ-011 QueryShape / Evaluation PASS；
- GQ-006 / GQ-010 无回归；
- GQ-N004 / GQ-N005 保持 Negative Contract 行为；
- 全 Golden Regression PASS；
- Coverage / Quality / Release Gate 后续通过；
- 主开发计划与 Runtime 记录同步。
