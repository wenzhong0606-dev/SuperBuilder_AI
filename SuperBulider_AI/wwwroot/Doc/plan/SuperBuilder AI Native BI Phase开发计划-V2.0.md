# SuperBuilder AI Native BI Phase开发计划

> 文档版本：v2.3  
> 文档性质：项目正式开发基线  
> **唯一源码基线：GitHub `master`**  
> 当前开发阶段：**Phase 2.6 — Query Evaluation Framework**  
> 最近完成阶段：Phase 2.5  
> Phase 2.3：COMPLETE / FROZEN  
> Phase 2.4：COMPLETE / FROZEN  
> Phase 2.5：COMPLETE / FROZEN

---

# 一、最高优先级开发约束

本章是整个开发计划的最高优先级约束，后续所有开发、审计、文档更新、源码修改均必须遵守。

## 1.1 唯一源码基线：master

> **GitHub `master` 是本项目唯一、唯一有效的开发基线。**

后续：

- 源码审计必须以 `master` 最新源码为准；
- Phase 状态必须以 `master` 实际实现为准；
- 开发计划必须反映 `master` 当前事实；
- 架构文档与源码不一致时，以 `master` 源码为事实依据；
- 不允许以历史分支、临时分支、个人工作区代码作为项目正式完成依据。

## 1.2 禁止创建新的开发分支

> **后续开发、修复、计划更新均不创建新的开发分支。**

所有正式修改直接基于：

```text
master
```

并直接提交到 `master`。

不得为了阶段开发、源码审计、计划更新或临时修复创建新的 Git Branch。

## 1.3 不允许伪造分支完成状态

如果某次修改无法直接写入 `master`，必须明确说明“修改未完成/未提交”，不得将其他分支的代码、计划或提交描述为项目正式完成状态。

## 1.4 GitHub 描述与提交说明使用中文

后续对 GitHub 的正式项目描述、开发计划、源码审计说明、提交说明，统一使用**中文**。

代码中的：

- 类名
- 方法名
- 属性名
- Namespace
- API Contract

仍遵循 C# / .NET 命名规范，不要求中文化。

## 1.5 不允许为了计划完整而虚构源码能力

开发计划中出现的任何“已完成”能力必须能够在 `master` 源码中找到实际实现。

如果：

- 计划存在但源码不存在；
- 源码实现已经发生变化；
- 原设计已经被新的实现替代；

必须以源码事实重新校准计划。

## 1.6 已冻结 Phase 不重复开发

以下阶段当前正式冻结：

```text
Phase 2.3 — Repair Reliability
Phase 2.4 — Confidence & Decision Gate
Phase 2.5 — Explainability
```

除非后续 Phase 发现明确的 Contract、Bug 或安全问题，否则不得重新打开这些 Phase 继续无边界扩展。

---

# 二、开发与审计原则

1. **源码优先。**
2. **master 唯一基线。**
3. **禁止创建新的开发分支。**
4. **所有正式修改直接提交 master。**
5. **GitHub 正式描述与提交说明使用中文。**
6. **不允许根据目录名称判断功能完成，必须读取实际源码。**
7. **不允许根据历史计划推断源码状态。**
8. **不允许为了“完成率”虚构不存在的 Service、Model、Controller 或 Runtime 能力。**
9. **Phase 完成必须同时满足实现、集成、运行验证和 Exit Criteria。**
10. **已经完成并冻结的 Phase 不重复扩展。**
11. **Production Safety Pipeline 与 Evaluation Framework 保持边界。**
12. **Golden Dataset 是 Evaluation 的事实数据基础，不得被临时人工判断替代。**

---

# 三、项目总体 Phase 路线

| Phase | 目标 | 当前状态 |
|---|---|---|
| Phase 0 | 工程、AI、Metadata、Vector、数据库基础设施 | COMPLETE |
| Phase 1.1 | Metadata Foundation | COMPLETE |
| Phase 1.2 | Metadata Semantic | COMPLETE |
| Phase 1.3 | Metadata Vector / Search | COMPLETE |
| Phase 1.4 | Component Runtime / Storage | COMPLETE |
| Phase 1.5 | Query Understanding / Semantic Search / SQL Builder | COMPLETE |
| Phase 1.6 | AI BI Conversation / QueryPlan 基础链路 | COMPLETE |
| Phase 2.1 | QueryPlan Foundation | COMPLETE / FROZEN |
| Phase 2.2 | Semantic Validation | COMPLETE / FROZEN |
| Phase 2.2.5 | Auto Repair | COMPLETE / FROZEN |
| Phase 2.3 | Repair Reliability | COMPLETE / FROZEN |
| Phase 2.4 | Confidence & Decision Gate | COMPLETE / FROZEN |
| Phase 2.5 | Explainability | COMPLETE / FROZEN |
| **Phase 2.6** | **Query Evaluation Framework** | **IN PROGRESS** |

---

# 四、Phase 2 Safety Pipeline 固定架构

```text
User Question
    ↓
Query Understanding
    ↓
Metadata Semantic Retrieval
    ↓
QueryPlan Builder
    ↓
QueryPlan Validation
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

Phase 2.6 Evaluation 不直接替代生产 Safety Pipeline，而是在其旁建立可复现、可比较、可回归的 Evaluation 层：

```text
Golden Dataset
      ↓
Evaluation Runtime
      ↓
Semantic Applicability
      ↓
QueryPlan Evaluation
      ↓
Scoring
      ↓
Regression
      ↓
Calibration / Baseline
```

---

# 五、Phase 2.1 — QueryPlan Foundation

## 状态

**COMPLETE / FROZEN**

当前 QueryPlan 已具备：

- DataSource
- Tables
- Fields
- Metrics
- Dimensions
- Filters
- Orders
- Joins
- Aggregations
- Ranking
- Distinct
- Limit

后续不得无边界扩展基础 QueryPlan 模型。

---

# 六、Phase 2.2 / 2.2.5 / 2.3 — Validation / Repair / Reliability

## 状态

**COMPLETE / FROZEN**

当前生产链已经具备：

```text
Validation
 ↓
Repair
 ↓
ReValidate
 ↓
RepairTrace
 ↓
Fingerprint
 ↓
Loop / Stall Detection
 ↓
Stop Reason
```

后续只允许处理明确 Bug、安全问题或被后续 Contract 明确影响的兼容问题。

---

# 七、Phase 2.4 — Confidence & Decision Gate

## 状态

**COMPLETE / FROZEN**

Confidence 与 Decision 已形成独立层，并已经进入生产编排。

正式原则：

```text
Validation
    ↓
Confidence
    ↓
Decision Gate
    ↓
ShouldExecute
```

Medium 不自动进入 SQL Builder；Low / Hard Block 必须阻断。

Phase 2.4 后续阈值校准必须由 Phase 2.6 Golden Evaluation / Calibration 驱动，不再通过临时修改生产评分常数解决。

---

# 八、Phase 2.5 — Explainability

## 状态

**COMPLETE / FROZEN**

Explainability 负责聚合：

- QueryPlan
- Validation
- RepairTrace
- Confidence
- Decision

不得在 Explainability 层重新计算核心 Evaluation 结果。

---

# 九、Phase 2.6 — Query Evaluation Framework

## 当前状态

**IN PROGRESS**

源码已经形成 Evaluation 主体框架，但尚未达到 Phase Exit Criteria。

当前核心能力包括：

```text
Golden Dataset
Golden Dataset Runtime
QueryPlan Evaluation
Semantic Applicability
Semantic Resolution
Metric / Dimension / Filter / Shape Scoring
Join Scoring
Regression
Coverage
Quality Gate
Scenario Gap
Confidence Calibration
Golden Baseline Lifecycle
```

注意：**代码文件存在 ≠ Phase 完成。**

必须同时验证：

```text
实现
 ↓
依赖注入
 ↓
实际调用链
 ↓
Runtime 可执行
 ↓
结果 Contract 一致
 ↓
Regression 可复现
```

---

# 十、Phase 2.6 当前源码审计矩阵

| 文件/文件族 | 状态 | 审计结论 |
|---|---|---|
| `Models/BI/QueryPlan.cs` | COMPLETE | QueryPlan 执行模型稳定 |
| `Services/BI/QueryPlanBuilder.cs` | COMPLETE | 主构建链稳定 |
| `QueryPlanBuilder.FieldResolution.cs` | COMPLETE | 字段物理绑定已形成 |
| `QueryPlanBuilder.SemanticResolution.cs` | ACTIVE | Semantic Resolution 仍在深化 |
| `QueryPlanBuilder.TableSelection.cs` | COMPLETE | 表选择已形成 |
| `QueryPlanBuilder.Search.cs` | COMPLETE | Metadata 搜索链存在 |
| `QueryPlanBuilder.PlanAssembly.cs` | COMPLETE | Plan 组装存在 |
| `QueryPlanBuilder.Diagnostics.cs` | COMPLETE | 诊断能力存在 |
| `QueryJoinInferenceService.cs` | COMPLETE | Join 推理已进入 QueryPlan |
| `QueryPlanConfidenceService.cs` | FROZEN | 不在当前 Phase 扩展 |
| `QueryPlanDecisionGate.cs` | FROZEN | 不在当前 Phase 扩展 |
| `QueryPlanExplainabilityService.cs` | FROZEN | 不在当前 Phase 扩展 |
| `QueryPlanEvaluator.cs` | ACTIVE | Evaluation Contract 需要收敛 |
| `SemanticApplicabilityEvaluator.cs` | ACTIVE | 需要继续扩充适用性场景 |
| `QueryPlanSemanticResolutionFactory.cs` | ACTIVE | 需要完善多 Metric / 多候选规则 |
| `QueryPlanEvaluationGate.cs` | COMPLETE | PASS / BLOCK / REVIEW 语义已形成 |
| `QueryPlanJoinScoringService.cs` | COMPLETE | Join Evaluation 主体已形成 |
| `QueryPlanEvaluationScoringService.cs` | ACTIVE | 已实现，但必须真正接入统一 Evaluation 主链 |
| `QueryPlanMetricScoringService.cs` | ACTIVE | 需要进入统一评分链 |
| `QueryPlanDimensionScoringService.cs` | ACTIVE | 需要进入统一评分链 |
| `QueryPlanFilterScoringService.cs` | ACTIVE | 需要进入统一评分链 |
| `QueryPlanQueryShapeScoringService.cs` | ACTIVE | 需要进入统一评分链 |
| `GoldenDatasetRunner.cs` | COMPLETE | Runtime 入口存在 |
| `GoldenDatasetRuntimeService.cs` | COMPLETE | Runtime 桥接存在 |
| `GoldenDatasetRegressionEvaluator.cs` | COMPLETE | Regression 主体存在 |
| `GoldenEvaluationRegressionService.cs` | COMPLETE | Regression 聚合存在 |
| `GoldenDatasetQualityGate.cs` | COMPLETE | Quality Gate 已存在 |
| `GoldenDatasetCoverageAnalyzer.cs` | COMPLETE | Coverage 分析存在 |
| `GoldenScenarioGapGenerator.cs` | COMPLETE | Gap 生成存在 |
| `GoldenCaseDraftGenerator.cs` | COMPLETE | Golden 草稿生成存在 |
| `GoldenConfidenceCalibrationEvaluator.cs` | COMPLETE | Calibration 主体存在 |
| `GoldenConfidenceCalibrationRunner.cs` | COMPLETE | Calibration Runtime 存在 |
| `GoldenBaseline*` | PARTIAL | 生命周期已形成，默认持久化仍需明确生产边界 |
| `query-plan-golden-v1.json` | ACTIVE | 数据集需要扩充 |
| `BIConversationService.cs` | FROZEN | Safety Pipeline 已完成串接 |
| `Program.cs` | COMPLETE | Evaluation 主要依赖已注册 |

---

# 十一、Phase 2.6 C.13 — QueryPlan Evaluation 深化

## C.13.1 — Evaluation Contract 收敛【当前第一优先级】

必须统一：

```text
Applicability Gate
        ↓
Semantic Evidence
        ↓
Runtime QueryPlan Evaluation
        ↓
Metric / Dimension / Filter / Shape Scoring
        ↓
Overall Score
        ↓
Evaluation Outcome
```

## 强制约束

当前存在两类评价逻辑时，必须收敛为唯一真相：

```text
QueryPlanEvaluator.Passed
```

与：

```text
QueryPlanEvaluationScoringService.OverallScore / Outcome
```

不得允许同一个 Case 同时出现互相矛盾的 PASS / FAIL / PARTIAL 解释。

最终 Evaluation Result 必须能够统一表达：

- Passed
- Decision
- OverallScore
- Section Results
- Dimension Scores
- Semantic Evidence
- Reasons
- Blocking Reasons

---

# 十二、C.13.2 — Metric / Dimension / Filter / Table Semantic Evaluation

当前部分 Evaluation 仍偏向结构和物理绑定检查。

后续需要逐步加强：

```text
Golden SemanticText
        ↓
Metadata Semantic Evidence
        ↓
Expected Field
        ↓
Runtime Field
        ↓
Physical Binding
```

必须能够回答：

> “为什么 Runtime QueryPlan 选择这个字段/表？”

而不仅仅是：

> “Runtime QueryPlan 有没有这个字段/表？”

Golden 中 `null` 仍表示“不进行该维度断言”，不得强行增加断言。

---

# 十三、C.13.3 — Multi-Metric Semantic Closure

当前 Semantic Evidence 不得长期只依赖：

```text
Golden Metrics.FirstOrDefault()
```

必须形成：

```text
Metric 1
 ↓
Semantic Evidence
 ↓
Physical Binding

Metric 2
 ↓
Semantic Evidence
 ↓
Physical Binding

Metric N
 ↓
Semantic Evidence
 ↓
Physical Binding
```

必须明确：

- Primary Metric 与 Secondary Metrics 的定义；
- Metric 一对一语义映射；
- 多 Metric 的顺序与对应关系；
- Metric Field + Aggregation 联合正确性；
- 多 Metric 的 DataSource / Table 一致性；
- 不允许只验证第一 Metric 就把整个 Case 判定为 Semantic PASS。

---

# 十四、C.13.4 — Golden Dataset 扩展

扩展目标不是单纯增加 Case 数量，而是补齐场景维度。

最低建议覆盖：

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

上述数量是最低建议，不要求一次性全部完成。

**优先补齐代码真实暴露出的缺口。**

---

# 十五、C.13.5 — Runtime Regression

C.13 不能以“代码文件存在”为完成标准。

必须满足：

1. Golden Dataset 可以通过 Runtime Controller 执行；
2. Evaluation Result 包含 Section Result、Dimension Score、Overall Score、Decision；
3. Applicability BLOCK / REVIEW 不会被错误解释成 Runtime PASS；
4. Semantic Evidence 与 Runtime QueryPlan 物理绑定一致；
5. Multi-Metric 不发生只验证第一项的漏洞；
6. Scoring Service 真正参与最终 Evaluation；
7. Regression 能输出失败 Case、失败维度、原因和 Baseline 对比；
8. Runtime 结果包含 Golden Dataset 版本；
9. Runtime 结果包含 Evaluation / Baseline 版本；
10. 源码注释与实际行为一致。

---

# 十六、Phase 2.6 C.14 — Join Evaluation

## 当前状态

**IMPLEMENTED / FROZEN FOR CURRENT BASELINE**

Join Evaluation 已具备独立 Scoring 能力。

后续只允许：

1. 修复明确 Contract / Bug；
2. Golden Dataset 新增 Join 场景暴露明确缺口。

不得无边界继续扩展 Join 算法。

---

# 十七、Golden Baseline Lifecycle

当前已具备：

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

但当前默认 Persistence 为 InMemory 类型，因此必须明确：

> **当前属于 Evaluation Framework 能力，不等同于生产级持久化 Baseline。**

后续 Phase 2.7 才正式处理：

- Baseline 持久化；
- Baseline Versioning；
- Release Governance；
- Audit Trail。

---

# 十八、当前项目问题分级

## P0 — C.13 Exit 前必须解决

### P0-1 Evaluation Contract 双重真相

`QueryPlanEvaluator` 与 `QueryPlanEvaluationScoringService` 必须统一。

### P0-2 Scoring Service 未完全进入主 Evaluation 链

已经存在的 Metric / Dimension / Filter / Shape Scoring 不得继续成为旁路代码。

### P0-3 Multi-Metric Semantic Evidence

不得长期只验证第一 Metric。

---

## P1 — C.13 / C.14 后续解决

- Dimension / Filter / Table Semantic Assertion 深度不足；
- Golden Dataset 规模与覆盖不足；
- Baseline 默认 InMemory；
- QueryPlanBuilder 历史注释与当前 Join 实现存在漂移；
- Evaluation / Diagnostics Contract 需要进一步统一。

---

# 十九、Phase 2.6 完成度

不再使用“文件数量完成率”。

采用：

```text
Architecture     25%
Implementation   30%
Evaluation       25%
Regression       20%
```

当前源码审计估算：

| 维度 | 完成度 |
|---|---:|
| Architecture | 90% |
| Implementation | 85% |
| Evaluation | 70% |
| Regression | 60% |
| **综合** | **约 76%** |

该数值是源码审计估算，不是运行时统计。

正式 Phase Exit 后重新计算。

当前正式结论：

> **Phase 2.6 主体框架已经建立，但 Evaluation Contract、Scoring Integration、Multi-Metric Semantic Closure、Golden Regression 尚未全部达到 Exit Criteria。**

---

# 二十、Phase 2.6 → Phase 2.7 路线

严格顺序：

```text
C.13.1 Evaluation Contract
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

在 C.13 Exit 前，不进入：

- 新复杂 Join 推理算法；
- 新 SQL 方言；
- 大规模 UI；
- 新独立 Test Project；
- QueryPlan 基础模型重设计；
- Phase 2.3 / 2.4 / 2.5 重新开放。

---

# 二十一、Phase 2.7 预定义方向

Phase 2.7 暂不开发，仅定义边界：

```text
Golden Dataset
      ↓
Evaluation
      ↓
Score Distribution
      ↓
Confidence Calibration
      ↓
Decision Threshold Calibration
      ↓
Baseline Release
      ↓
Regression Governance
```

重点：

- Golden Dataset Versioning
- Baseline Versioning
- Confidence Calibration
- Threshold Calibration
- Evaluation Drift Detection
- Regression Governance
- Evaluation Audit Trail

进入条件：Phase 2.6 Exit Criteria 全部通过。

---

# 二十二、正式 GitHub 开发规则

以后所有开发任务统一遵循：

```text
GitHub master
     ↓
源码审计
     ↓
确定 Phase / C 项
     ↓
直接修改 master
     ↓
编译 / Runtime 验证
     ↓
中文提交说明
     ↓
更新开发计划
     ↓
继续下一项
```

## 严禁

- 创建新的开发分支；
- 使用分支代码冒充 master 完成状态；
- 未读取源码就修改开发计划；
- 以文件存在代替功能完成；
- 未通过 Runtime 验证就宣布 Phase 完成；
- 为了解决局部问题重新设计已经冻结的 Phase；
- 在计划中写入源码不存在的能力。

---

# 二十三、当前唯一正式开发入口

> **Phase 2.6 — C.13.1 QueryPlan Evaluation Contract 收敛**

第一步只处理：

```text
QueryPlanEvaluator
        ↓
QueryPlanEvaluationScoringService
        ↓
Metric / Dimension / Filter / Shape Scoring
        ↓
OverallScore
        ↓
唯一 EvaluationResult
```

完成这一条链以后，再扩充 Golden Dataset。

---

# 二十四、最终项目原则

1. **master 是唯一源码基线。**
2. **禁止创建新的开发分支。**
3. **所有正式源码和文档修改直接提交 master。**
4. **GitHub 正式描述和提交说明统一使用中文。**
5. **源码事实优先于开发计划。**
6. **开发计划必须持续反映 master 实际状态。**
7. **已冻结 Phase 不重复扩展。**
8. **Evaluation 必须有唯一 Contract。**
9. **Golden Dataset 是 Evaluation 的事实数据基础。**
10. **Production Safety Pipeline 与 Evaluation Framework 保持边界。**
11. **没有 Runtime / Regression 验证，不宣布 Phase 完成。**
12. **每次修改必须明确属于哪个 Phase / C 项。**
13. **所有正式提交使用中文提交说明。**
14. **不允许为了计划完整而虚构源码能力。**
