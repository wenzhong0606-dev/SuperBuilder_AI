# Phase 2.7 — DimensionResolutionEvidence 参数契约修复记录

> 状态：FIX IMPLEMENTED / WAITING FOR LOCAL BUILD & RUNTIME
> 源码基线：GitHub `master`
> 修复 Commit：`ec89842515d710fc8621dce78a9f826947f4634a`
> 关联能力：C.13.2 Semantic Applicability / Phase 2.7 DimensionAware QueryPlan

## 1. 问题

本地编译错误：

```text
CS1503 参数 1：无法从 string 转换为 long
CS1503 参数 3：无法从 long 转换为 string
SemanticApplicabilityEvaluator.cs 第 115 行
```

## 2. 根因

当前 `IDimensionResolutionEvidenceService.ResolveAsync` 契约为：

```csharp
Task<DimensionResolutionEvidence?> ResolveAsync(
    long factTableId,
    long factDataSourceId,
    string dimensionSemanticText,
    long preferredColumnId,
    CancellationToken cancellationToken = default);
```

`SemanticApplicabilityEvaluator.ResolveDimensionAsync` 原调用为：

```csharp
_dimensionEvidenceService.ResolveAsync(
    dimension.SemanticText,
    metric.TableId,
    metric.DataSourceId,
    topK);
```

该调用同时存在两个问题：

1. 第一个参数把 Dimension SemanticText (`string`) 传给 `factTableId` (`long`)；
2. 第三个参数把 `DataSourceId` (`long`) 传给 `dimensionSemanticText` (`string`)；
3. 第四个参数实际应为已确认 Metric 的 `ColumnId`，而不是 `topK`。

因此这是 **Interface Contract 与 Consumer Call Site 参数顺序/语义漂移**，不是需要通过类型转换解决的问题。

## 3. 修复

已将调用统一修复为：

```csharp
_dimensionEvidenceService.ResolveAsync(
    metric.TableId,
    metric.DataSourceId,
    dimension.SemanticText,
    metric.ColumnId);
```

修复原则：

- `factTableId` 来自已确认 Metric Resolution；
- `factDataSourceId` 来自已确认 Metric Resolution；
- `dimensionSemanticText` 来自 Golden Dimension；
- `preferredColumnId` 来自已确认 Metric Resolution；
- 不进行 `string.Parse` / `ToString` 等类型转换掩盖 Contract 错误；
- 不新增 Semantic Search；
- 不修改 Interface 契约；
- 不修改 Golden Contract；
- 不修改 QueryPlan Builder / Evaluator。

## 4. 源码审计结论

`IDimensionResolutionEvidenceService` 与 `DimensionResolutionEvidenceService` 的实现签名已经一致，均要求：

```text
(long factTableId,
 long factDataSourceId,
 string dimensionSemanticText,
 long preferredColumnId,
 CancellationToken)
```

责任边界因此明确落在 `SemanticApplicabilityEvaluator` Consumer 调用点。

## 5. 后续验证顺序

```text
git pull
↓
dotnet build
↓
STEP-04 Dimension Resolution Contract
↓
STEP-05 MasterJoin Resolution
↓
STEP-06 DirectKey Resolution
↓
STEP-07 SameTable Dimension QueryPlan
↓
STEP-08 CrossTable Dimension QueryPlan
↓
STEP-09 MasterJoin SQL Builder
↓
STEP-10 DirectKey SQL Builder
↓
STEP-11 MasterJoin SQL Runtime Execution
↓
STEP-12 DirectKey SQL Runtime Execution
↓
STEP-13 Ambiguous Safety Regression
↓
STEP-14 NotResolved Safety Regression
↓
STEP-15 Full Golden Regression
```

当前只允许先执行 `dotnet build`。Build 未 PASS 前不得进入 Runtime STEP。

## 6. 与既有 GQ-011 修复的隔离原则

本次编译修复仅恢复 Dimension Evidence Service 的正确 Contract 调用，不修改已经验证通过的 GQ-011 Ranking Order Resolution。

GQ-011 已验证：`Orders.Count=1`、`DESC`、`Aggregation=NONE`、QueryShape PASS、Overall Evaluation Score=100。

因此本次修复不得改变 Ranking Order Resolution 行为。

## 7. Exit 条件

- [ ] 本地 `dotnet build` 0 error；
- [ ] STEP-04 Dimension Resolution Contract PASS；
- [ ] MasterJoin / DirectKey 行为符合 Phase 2.7 Contract；
- [ ] GQ-011 无回归；
- [ ] Full Golden Regression 完成；
- [ ] Coverage / Quality / Release Gate 后续通过。
