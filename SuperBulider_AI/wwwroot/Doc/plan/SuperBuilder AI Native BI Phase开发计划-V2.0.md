# SuperBuilder AI Native BI Phase开发计划

> 文档版本：v2.4  
> 文档性质：项目正式开发基线  
> **唯一源码基线：GitHub `master`**  
> 当前开发阶段：**Phase 2.6 — Query Evaluation Framework**  
> 当前工作单元：**C.13.2 Metric / Dimension / Filter / Table Semantic Evaluation**

---

# 一、最高优先级开发约束

1. **GitHub `master` 是唯一源码基线。** 所有源码审计、Phase 状态、计划校准均以 `master` 最新源码为准。
2. **禁止创建新的开发分支。** 后续正式开发、修复、文档更新均直接修改 `master`。
3. **不得使用其他分支、临时工作区或历史代码冒充 `master` 完成状态。**
4. **GitHub 正式描述、开发计划、源码审计说明、提交说明统一使用中文。** C# 类名、方法名、属性名和 Namespace 仍遵循代码规范。
5. **源码事实优先于计划。** 计划中标记 COMPLETE 的能力必须能在 `master` 中找到真实实现并经过集成验证。
6. **代码文件存在不等于功能完成。** 必须检查依赖注入、实际调用链、Runtime 执行和 Regression。
7. **Phase 完成必须满足 Exit Criteria。** 未完成 Runtime / Regression 验证不得宣布 Phase COMPLETE。
8. **Phase 2.3、2.4、2.5 已冻结。** 后续仅允许修复明确 Bug、Contract 问题或被新阶段证明的兼容问题。
9. **Production Safety Pipeline 与 Evaluation Framework 保持边界。** Evaluation 不直接替代生产执行安全链。
10. **Golden Dataset 是 Evaluation 的事实数据基础。** 不得用临时人工判断代替 Golden Regression。
11. **每次正式修改必须明确所属 Phase / C 项，并使用中文提交说明。**
12. **不得为了让开发计划完整而虚构源码能力。**

---

# 二、当前正式基线

```text
GitHub master
    ↓
源码审计
    ↓
Phase 状态校准
    ↓
直接修改 master
    ↓
编译 / Runtime / Regression 验证
    ↓
中文提交
    ↓
更新开发计划
```

最近源码提交：

```text
59136f99a9b71803738b841cb1974258a8db66bd
Phase 2.6 C.13.1 收敛 QueryPlan Evaluation Contract
```

---

# 三、总体 Phase 状态

| Phase | 状态 | 结论 |
|---|---|---|
| Phase 0 | COMPLETE | 基础设施完成 |
| Phase 1.1 | COMPLETE | Metadata Foundation 完成 |
| Phase 1.2 | COMPLETE | Metadata Semantic 完成 |
| Phase 1.3 | COMPLETE | Vector / Search 完成 |
| Phase 1.4 | COMPLETE | Component Runtime / Storage 完成 |
| Phase 1.5 | COMPLETE | Query Understanding / Semantic Search / SQL Builder 完成 |
| Phase 1.6 | COMPLETE | AI BI Conversation / QueryPlan 链路完成 |
| Phase 2.1 | COMPLETE / FROZEN | QueryPlan Foundation 完成 |
| Phase 2.2 | COMPLETE / FROZEN | Semantic Validation 完成 |
| Phase 2.2.5 | COMPLETE / FROZEN | Auto Repair 完成 |
| Phase 2.3 | COMPLETE / FROZEN | Repair Reliability 完成 |
| Phase 2.4 | COMPLETE / FROZEN | Confidence & Decision Gate 完成 |
| Phase 2.5 | COMPLETE / FROZEN | Explainability 完成 |
| **Phase 2.6** | **IN PROGRESS** | Query Evaluation Framework 正在深化 |

---

# 四、Phase 2 固定生产安全链

```text
User Question
    ↓
Query Understanding
    ↓
Metadata Semantic Retrieval
    ↓
QueryPlan Builder
    ↓
Validation
    ↓
Repair / ReValidate
    ↓
Confidence
    ↓
Decision Gate
    ↓
Explainability
    ↓
SQL Builder
    ↓
SQL Execution
    ↓
Result Understanding
```

Phase 2.6 在生产链旁建立独立 Evaluation：

```text
Golden Dataset
    ↓
Semantic Applicability
    ↓
Runtime QueryPlan
    ↓
Section Evaluation
    ↓
Dimension Scoring
    ↓
Overall Score
    ↓
Regression
    ↓
Calibration / Baseline
```

---

# 五、Phase 2.3 / 2.4 / 2.5 冻结规则

## Phase 2.3 — Repair Reliability

已完成：Repair Attempt、Fingerprint、Loop Detection、Stall Detection、ReValidate、Stop Reason。

**状态：COMPLETE / FROZEN。**

## Phase 2.4 — Confidence & Decision Gate

已完成 Confidence、Evidence、Decision、Execution Gate，并进入生产编排。

**状态：COMPLETE / FROZEN。**

后续阈值校准只能由 Golden Evaluation / Calibration 驱动，不通过临时修改生产常数解决。

## Phase 2.5 — Explainability

已完成 QueryPlan / Validation / RepairTrace / Confidence / Decision 的解释聚合。

**状态：COMPLETE / FROZEN。**

---

# 六、Phase 2.6 当前源码结构

当前 `master` 已形成：

```text
Golden Dataset
Golden Runtime
Semantic Applicability
Semantic Resolution
QueryPlan Evaluation
Metric Scoring
Dimension Scoring
Filter Scoring
Query Shape Scoring
Join Scoring
Evaluation Gate
Regression
Coverage
Quality Gate
Scenario Gap
Confidence Calibration
Golden Baseline Lifecycle
```

关键文件族：

- `Services/BI/Evaluation/QueryPlanEvaluator.cs`
- `Services/BI/Evaluation/QueryPlanEvaluationScoringService.cs`
- `Services/BI/Evaluation/QueryPlanMetricScoringService.cs`
- `Services/BI/Evaluation/QueryPlanDimensionScoringService.cs`
- `Services/BI/Evaluation/QueryPlanFilterScoringService.cs`
- `Services/BI/Evaluation/QueryPlanQueryShapeScoringService.cs`
- `Services/BI/Evaluation/QueryPlanJoinScoringService.cs`
- `Services/BI/Evaluation/GoldenDatasetRunner.cs`
- `Services/BI/Evaluation/GoldenDatasetRegressionEvaluator.cs`
- `Services/BI/Evaluation/GoldenDatasetRuntimeService.cs`
- `Services/BI/Evaluation/GoldenDatasetQualityGate.cs`
- `Services/BI/Evaluation/GoldenDatasetCoverageAnalyzer.cs`
- `Services/BI/Evaluation/GoldenConfidenceCalibrationEvaluator.cs`
- `Services/BI/Evaluation/GoldenBaselineReleaseService.cs`
- `Models/BI/Evaluation/QueryPlanEvaluationResult.cs`
- `Evaluation/Golden/query-plan-golden-v1.json`

`Program.cs` 已注册上述主要 Evaluation Service，因此 C.13.1 的统一评分链具备 DI 基础。

---

# 七、C.13.1 — Evaluation Contract 收敛

## 状态

**IMPLEMENTED，待 Runtime Regression 验证。**

### 本次完成

此前存在：

```text
QueryPlanEvaluator.Passed
```

与：

```text
QueryPlanEvaluationScoringService.OverallScore / Outcome
```

两套评价真相。

现在 `QueryPlanEvaluator` 已改为：

```text
Intent
 ↓
Metric Scoring
 ↓
Dimension Scoring
 ↓
Filter Scoring
 ↓
Table Evaluation
 ↓
Join Scoring
 ↓
Query Shape Scoring
 ↓
Binding Consistency
 ↓
QueryPlanEvaluationScoringService
 ↓
唯一 EvaluationResult
```

最终：

```text
PASS / PARTIAL / FAIL
OverallScore
DimensionScores
```

由统一 Scoring Contract 产生。

### 本次没有做的事情

- 没有重设计 QueryPlan；
- 没有修改 Phase 2.3 / 2.4 / 2.5；
- 没有创建新分支；
- 没有增加独立 Test Project；
- 没有扩充 Golden Dataset；
- 没有改变 Production Safety Pipeline。

### 当前验证状态

源码已直接提交 `master`，但当前环境未完成实际 .NET 编译和 Golden Runtime 执行，因此 **C.13.1 暂不标记为 COMPLETE，只标记 IMPLEMENTED / PENDING VERIFICATION**。

---

# 八、C.13.2 — Metric / Dimension / Filter / Table Semantic Evaluation【当前工作】

目标：把 Evaluation 从“结构完整性检查”继续提升到“语义 + 物理绑定检查”。

## 8.1 Metric

当前 Metric Scoring 已支持：

- SemanticText
- Runtime Name
- Field
- Aggregation
- Binding
- 多 Metric 逐项匹配

后续重点：

- Semantic Evidence 与 Metric Scoring 统一；
- Primary / Secondary Metric 定义明确；
- 不允许仅依赖第一 Metric 判断整个 Case。

## 8.2 Dimension

当前已支持 SemanticText → Alias / SemanticType / ColumnName 匹配，以及 MetadataColumnId / ColumnName 物理绑定。

后续需要补齐 Golden 与 Runtime MetadataColumn 的语义闭环。

## 8.3 Filter

当前已支持：

- SemanticText
- Field
- Operator
- Value
- Binding

后续需要让 SemanticText 能够通过 Metadata Semantic Evidence 证明 Runtime Filter Field 的选择合理性，而不是简单等值比较。

## 8.4 Table

当前主要验证：

- Table 数量
- MetadataTableId
- DataSourceId
- TableName
- Binding Consistency

后续需要建立 Golden SemanticText → MetadataTable → Runtime Table 的语义证据链。

---

# 九、C.13.3 — Multi-Metric Semantic Closure

目标：形成真正的多 Metric 语义闭环。

```text
Metric 1 → Semantic Evidence → Physical Binding
Metric 2 → Semantic Evidence → Physical Binding
...
Metric N → Semantic Evidence → Physical Binding
```

必须明确：

1. Primary Metric 与 Secondary Metrics；
2. Metric 一对一映射；
3. 顺序与对应关系；
4. Field + Aggregation 联合正确性；
5. DataSource / Table 一致性；
6. Applicability 与 Evaluation 的关系。

**禁止长期使用 `Golden Metrics.FirstOrDefault()` 作为整个 Semantic Evaluation 的唯一依据。**

---

# 十、C.13.4 — Golden Dataset 扩展

扩展按场景覆盖，而不是简单堆数量：

| 场景 | 最低建议 |
|---|---:|
| ColumnMetric | 5 |
| EntityCount | 5 |
| Multi-Metric | 5 |
| Dimension + Metric | 5 |
| Filter | 5 |
| Ranking / TopN | 5 |
| Join | 5 |
| NotResolved | 3 |
| Ambiguous | 3 |
| Negative / Wrong Binding | 3 |

原则：

> **先补代码真实暴露的缺口，再扩大数据集。**

Golden `null` 表示不进行该维度断言。

---

# 十一、C.13.5 — Runtime Regression

C.13 Exit 必须满足：

1. Golden Dataset 可以通过 Runtime Controller 执行；
2. EvaluationResult 包含 Section Result；
3. EvaluationResult 包含 DimensionScores；
4. EvaluationResult 包含 OverallScore；
5. EvaluationResult 包含 Decision；
6. Applicability BLOCK / REVIEW 不得错误变成 Runtime PASS；
7. Semantic Evidence 与 Runtime 物理绑定一致；
8. Multi-Metric 不发生只验证第一项的漏洞；
9. Scoring Service 真正参与最终 Evaluation；
10. Regression 输出失败 Case、失败维度、原因；
11. Regression 支持 Baseline Comparison；
12. Runtime 输出 Dataset / Evaluation / Baseline 版本；
13. 源码注释与实际行为一致。

---

# 十二、Phase 2.6 C.14 — Join Evaluation

**状态：IMPLEMENTED / FROZEN FOR CURRENT BASELINE。**

后续只允许：

- 明确 Contract / Bug 修复；
- Golden 新场景暴露的明确缺口。

不得无边界继续扩展 Join 推理算法。

---

# 十三、Golden Baseline

当前已形成：

```text
Registry
 ↓
Release
 ↓
Comparison
 ↓
Regression
 ↓
Validation
```

当前默认 Persistence 为 InMemory，因此：

> **当前属于 Evaluation Framework 能力，不等同于生产级持久化 Baseline。**

正式生产 Baseline 持久化与 Governance 放入 Phase 2.7。

---

# 十四、当前问题分级

## P0

- C.13.1 必须完成 Runtime / Regression 验证；
- C.13.3 必须解决 Multi-Metric Semantic Evidence 的单项问题；
- Evaluation Contract 不得重新出现双重 PASS / FAIL 真相。

## P1

- Dimension / Filter / Table 的 Metadata Semantic Evidence 继续深化；
- Golden Dataset 覆盖继续扩充；
- Baseline InMemory 持久化边界；
- QueryPlanBuilder 历史注释与当前 Join 实现同步；
- Diagnostics Contract 统一。

---

# 十五、Phase 2.6 完成度

当前采用：

```text
Architecture     25%
Implementation   30%
Evaluation       25%
Regression       20%
```

当前源码审计估算仍约：

- Architecture：90%
- Implementation：90%
- Evaluation：75%
- Regression：60%
- 综合约：**79%**

> C.13.1 已进入实现完成、验证待执行状态，因此 Implementation 相比上一版上调；Regression 尚未实际运行，不上调。

这仍然是源码审计估算，不是运行时统计。

---

# 十六、后续严格顺序

```text
C.13.1 Evaluation Contract
        ↓
[Runtime / Regression 验证]
        ↓
C.13.2 Semantic Evaluation
        ↓
C.13.3 Multi-Metric Closure
        ↓
C.13.4 Golden Dataset Expansion
        ↓
C.13.5 Runtime Regression
        ↓
Phase 2.6 Exit Review
        ↓
Phase 2.7 Calibration / Evaluation Governance
```

当前**唯一正式开发入口**：

> **Phase 2.6 C.13.2 Metric / Dimension / Filter / Table Semantic Evaluation**

但在进入 C.13.2 大规模修改前，必须先完成 C.13.1 的编译与 Runtime Regression 验证。

---

# 十七、禁止事项

- 禁止创建新的 Git Branch；
- 禁止把其他分支当正式基线；
- 禁止直接重设计 QueryPlan；
- 禁止重新开放 Phase 2.3 / 2.4 / 2.5；
- 禁止在没有 Runtime 验证的情况下宣布 Phase 完成；
- 禁止为了提高完成率虚构代码能力；
- 禁止先堆 Golden Case、后修 Evaluation Contract；
- 禁止 Evaluation 逻辑侵入生产 SQL Execution Safety Pipeline；
- 禁止新增独立 Test Project，除非未来开发计划明确重新批准。

---

# 十八、正式开发原则

1. **master 是唯一基线。**
2. **直接修改 master。**
3. **禁止创建新分支。**
4. **GitHub 正式说明与提交说明使用中文。**
5. **源码事实优先。**
6. **计划必须持续与 master 一致。**
7. **先 Contract，再扩 Dataset。**
8. **Evaluation 必须唯一真相。**
9. **Golden Dataset 必须可复现。**
10. **Runtime / Regression 是 Phase 完成必要条件。**
11. **冻结 Phase 不无边界返工。**
12. **每次修改必须明确 Phase / C 项。**
