# SuperBuilder AI Native BI Phase开发计划

> 文档版本：v2.6  
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
13. **AI 生成或修改的代码不视为天然正确。** 所有 AI 生成代码必须按照本计划的完整源码审计标准重新验证。
14. **禁止以“发现一个编译错误、修一个错误”的方式作为主要开发流程。** 编译只能作为完整源码审计后的验证环节。
15. **任何关键 Contract、DI、Namespace、构造函数依赖变化，都必须进行上下游闭环检查。**

---

# 二、强制标准工作方式：完整审计后再修改

> **本节是后续所有源码开发、修复和 Contract 调整的强制标准，不仅适用于 Phase 2.6。**

## 2.1 禁止“边读边改”

涉及 Model、DTO、Contract、Builder、Semantic、Evaluation、Scoring、Runtime、Controller、DI 或跨文件调用链的任务：

> **在完整读取相关调用链之前，不得修改任何源码。**

禁止采用：

```text
发现一个文件
 ↓
立即修改
 ↓
再读取下一个文件
 ↓
发现 Contract 漂移
 ↓
回头继续修改
```

这种方式容易造成连锁调整和重复提交，不再作为本项目正式开发方式。

## 2.2 标准工作流

以后统一采用：

```text
1. 锁定 master
       ↓
2. 明确 Phase / C 项
       ↓
3. 读取完整相关源码
       ↓
4. 逐文件审计
       ↓
5. 逐字段 / 逐方法追踪
       ↓
6. 建立调用链与数据流矩阵
       ↓
7. 建立 Constructor Dependency / DI Matrix
       ↓
8. 找出 Contract Gap / Bug / Drift / DI Missing
       ↓
9. 一次性确定修改范围
       ↓
10. 统一修改源码
       ↓
11. 重新读取全部受影响文件
       ↓
12. 静态一致性复核
       ↓
13. 编译验证
       ↓
14. 通过现有 Controller / Runtime 接口验证
       ↓
15. Golden Dataset Regression
       ↓
16. 确认无回归
       ↓
17. 更新开发计划
       ↓
18. 中文提交 master
```

## 2.3 “完整相关源码”的定义

不能只读取目标 Service。

根据任务实际依赖范围，至少需要审计：

```text
Model
 ↓
DTO / Contract
 ↓
创建者 / Builder
 ↓
转换器 / Mapper
 ↓
Semantic Resolution
 ↓
Runtime Model
 ↓
Evaluator
 ↓
Scoring
 ↓
Aggregation
 ↓
Runtime Service
 ↓
Controller
 ↓
DI / Program.cs
 ↓
Golden Dataset
 ↓
Regression
```

如果某个文件被调用链引用，也属于本次完整审计范围，不能因为“不属于当前目标文件”而跳过。

## 2.4 逐字段追踪标准

对所有影响 Contract 的关键字段，必须建立完整生命周期：

```text
字段定义
 ↓
字段创建
 ↓
字段赋值
 ↓
字段转换
 ↓
字段传递
 ↓
字段读取
 ↓
字段比较 / 评分
 ↓
最终输出
```

重点字段包括但不限于：

- SemanticText
- Field
- MetadataColumnId
- MetadataTableId
- DataSourceId
- Aggregation
- Operator
- Value
- IntentType
- Score
- Decision
- ApplicabilityState
- Resolution
- OverallScore

任何中间环节缺失都必须记录为 Contract Gap，而不是通过最终层面的猜测补偿。

## 2.5 逐方法验证标准

关键方法必须确认：

1. 输入参数实际来源；
2. nullable 行为；
3. 默认值；
4. 分支条件；
5. 返回值；
6. 异常路径；
7. 是否被实际调用；
8. 是否存在旁路实现；
9. 是否与上游 / 下游 Contract 一致。

## 2.6 修改前必须形成“修改面”

在实际修改之前必须先确定：

```text
目标问题
 ↓
根因
 ↓
涉及文件
 ↓
涉及 Contract
 ↓
影响调用链
 ↓
Constructor Dependencies
 ↓
DI Registration
 ↓
需要同步修改的文件
 ↓
验证方式
```

如果无法确定完整修改面，则继续审计，不得先改代码。

## 2.7 修改后必须验证整个链路

不能只验证修改文件能否编译。

必须验证：

```text
源码
 ↓
Namespace / Type
 ↓
Contract
 ↓
Constructor Dependency
 ↓
DI
 ↓
实际调用链
 ↓
Controller / Runtime
 ↓
Golden Dataset
 ↓
Regression
```

如果 Runtime 无法执行，则必须明确标记“实现完成但 Runtime 未验证”，不得宣布功能 COMPLETE。

## 2.8 审计输出标准

每次重要审计至少形成：

| 项目 | 内容 |
|---|---|
| 文件 | 实际审计文件 |
| 状态 | COMPLETE / ACTIVE / GAP / DRIFT / BLOCKED / DI_MISSING |
| Contract | 输入输出关系 |
| 数据流 | 字段来源与去向 |
| Constructor | 构造函数依赖 |
| DI | 注册、Lifetime、Implementation |
| 问题 | 明确根因 |
| 影响 | 上下游影响 |
| 修改面 | 必须修改的文件 |
| 验证 | 编译 / Runtime / Regression |

## 2.9 禁止重复返工原则

如果后续发现新的相关文件，首先判断：

> 该文件是否属于之前应当被完整审计的调用链？

如果属于，则视为本次审计遗漏，必须补充审计并修正工作流程，而不能继续采用“发现一个改一个”的方式。

---

# 三、Contract / Namespace / DI 闭环强制标准

## 3.1 Contract 四方一致性

所有关键对象必须同时验证：

```text
Interface
   ↕
Implementation
   ↕
Caller
   ↕
DTO / Model / Contract
```

必须检查：

- 类型是否一致；
- 方法名是否一致；
- 参数数量是否一致；
- 参数类型是否一致；
- 返回类型是否一致；
- nullable 是否一致；
- collection 类型是否一致；
- 字段名称是否一致；
- Resolution 类型是否一致。

禁止通过“看起来类似”来判断两个 Contract 可以互换。

例如：

```text
SemanticApplicabilityFilterResolution
```

不能未经明确转换就作为：

```text
SemanticApplicabilityResolution
```

使用。

## 3.2 Namespace 必须以源码为准

禁止根据历史目录、文件名或旧版本推测 namespace。

必须逐文件确认：

```text
文件路径
 ↓
namespace
 ↓
类型声明
 ↓
引用方 using
 ↓
实际编译类型
```

## 3.3 Constructor Dependency 完整检查

任何 Service 修改前，必须读取完整构造函数，并列出：

```text
Service
 ├─ Dependency A
 ├─ Dependency B
 ├─ Dependency C
 └─ Dependency N
```

然后逐项确认：

```text
依赖类型存在
 ↓
实现存在
 ↓
Interface 对应
 ↓
Program.cs 注册
 ↓
Lifetime 正确
 ↓
上游 Service 可构造
```

## 3.4 DI 不允许“一个异常补一个注册”

以下方式禁止作为正式开发方式：

```text
启动
 ↓
Unable to resolve A
 ↓
注册 A
 ↓
启动
 ↓
Unable to resolve B
 ↓
注册 B
 ↓
继续循环
```

正确方式：

```text
读取完整 Constructor Dependency Graph
 ↓
一次性检查全部 DI
 ↓
统一修复
 ↓
BuildServiceProvider / Application Build
 ↓
Runtime 验证
```

## 3.5 AI 生成代码专项要求

任何 AI 生成的 Service、Interface、Model、DTO、Controller 或 Program.cs 修改必须重新执行：

```text
文件存在性
 ↓
Namespace
 ↓
Contract
 ↓
Constructor
 ↓
Caller / Callee
 ↓
DI
 ↓
编译
 ↓
Runtime
 ↓
Regression
```

因此：

> **AI 生成代码只能视为候选实现，不能视为验收完成。**

---

# 四、当前正式基线

```text
GitHub master
    ↓
完整源码审计
    ↓
Phase 状态校准
    ↓
一次性确定修改面
    ↓
统一修改 master
    ↓
重新读取修改后的源码
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

# 五、总体 Phase 状态

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

# 六、Phase 2 固定生产安全链

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

# 七、Phase 2.3 / 2.4 / 2.5 冻结规则

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

# 八、Phase 2.6 当前源码结构

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

# 九、C.13.1 — Evaluation Contract 收敛

## 状态

**IMPLEMENTED，待 Runtime Regression 验证。**

`QueryPlanEvaluator` 已将 Intent、Metric、Dimension、Filter、Table、Join、Query Shape、Binding Consistency 统一组织，并最终交由 `QueryPlanEvaluationScoringService` 产生唯一 EvaluationResult。

当前没有重新设计 QueryPlan，没有修改冻结 Phase，没有创建新分支，没有增加独立 Test Project，也没有改变 Production Safety Pipeline。

---

# 十、C.13.2 — Metric / Dimension / Filter / Table Semantic Evaluation【当前完整审计阶段】

当前不直接修改源码。

必须先完成 C.13.2 全调用链审计：

```text
Golden Dataset
      ↓
GoldenQueryExpectation
      ↓
QueryIntent / QueryFilter / QueryMetric
      ↓
QueryPlanBuilder
      ↓
QueryPlan
      ↓
Semantic Resolution
      ↓
QueryPlan Evaluation
      ↓
Metric / Dimension / Filter / Table Scoring
      ↓
QueryPlanEvaluationScoringService
      ↓
EvaluationResult
      ↓
GoldenDatasetRuntime
      ↓
Controller
      ↓
DI / Program.cs
```

## C.13.2 审计要求

### 10.1 Model / Contract

逐文件、逐属性确认：

- Golden Metric / Dimension / Filter / Table；
- QueryIntent；
- QueryMetric；
- QueryFilter；
- QueryPlan；
- QueryField / QueryTable / QueryJoin；
- Evaluation Result；
- Semantic Applicability / Resolution。

### 10.2 Builder / Resolution

确认每一个关键字段的真实来源、赋值和转换：

```text
SemanticText
Field
MetadataColumnId
MetadataTableId
DataSourceId
Aggregation
Operator
Value
```

### 10.3 Evaluation / Scoring

逐方法验证：

- Metric Scoring；
- Dimension Scoring；
- Filter Scoring；
- Table Evaluation；
- Join Scoring；
- Query Shape Scoring；
- Binding Consistency；
- Overall Score；
- Decision。

### 10.4 Runtime

确认：

- Golden Dataset 如何进入 Runtime；
- Applicability 如何进入 Evaluation；
- QueryPlan 如何产生；
- EvaluationResult 如何返回 Controller；
- DI 是否完整。

### 10.5 字段生命周期矩阵

必须形成：

| 字段 | 定义 | 创建 | 赋值 | 传递 | 读取 | 评分 | 输出 |
|---|---|---|---|---|---|---|---|
| SemanticText | 待审计 | 待审计 | 待审计 | 待审计 | 待审计 | 待审计 | 待审计 |
| Field | 待审计 | 待审计 | 待审计 | 待审计 | 待审计 | 待审计 | 待审计 |
| MetadataColumnId | 待审计 | 待审计 | 待审计 | 待审计 | 待审计 | 待审计 | 待审计 |
| MetadataTableId | 待审计 | 待审计 | 待审计 | 待审计 | 待审计 | 待审计 | 待审计 |
| DataSourceId | 待审计 | 待审计 | 待审计 | 待审计 | 待审计 | 待审计 | 待审计 |
| Aggregation | 待审计 | 待审计 | 待审计 | 待审计 | 待审计 | 待审计 | 待审计 |
| Operator | 待审计 | 待审计 | 待审计 | 待审计 | 待审计 | 待审计 | 待审计 |
| Value | 待审计 | 待审计 | 待审计 | 待审计 | 待审计 | 待审计 | 待审计 |

### 10.6 审计结论分类

每个文件必须标记：

```text
COMPLETE
ACTIVE
GAP
DRIFT
BLOCKED
DI_MISSING
ORPHAN_CODE
DOCUMENT_ONLY
IMPLEMENTED_BUT_UNVERIFIED
```

### 10.7 修改原则

在完整审计结束前：

> **不得修改 C.13.2 相关源码。**

审计完成后一次性确定修改面，再统一修改，避免后续读取新文件导致重复返工。

---

# 十一、C.13.3 — Multi-Metric Semantic Closure

在 C.13.2 完整审计和 Contract 收敛之后进入。

```text
Metric 1 → Semantic Evidence → Physical Binding
Metric 2 → Semantic Evidence → Physical Binding
...
Metric N → Semantic Evidence → Physical Binding
```

禁止长期使用 `Golden Metrics.FirstOrDefault()` 作为整个 Semantic Evaluation 的唯一依据。

---

# 十二、C.13.4 — Golden Dataset 扩展

扩展按场景覆盖，而不是简单堆数量。

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

# 十三、C.13.5 — Runtime Regression

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

# 十四、Phase 2.6 C.14 — Join Evaluation

**状态：IMPLEMENTED / FROZEN FOR CURRENT BASELINE。**

后续只允许明确 Contract / Bug 修复或 Golden 新场景暴露的明确缺口，不得无边界扩展 Join 推理算法。

---

# 十五、Golden Baseline

当前已形成 Registry、Release、Comparison、Regression、Validation。

当前默认 Persistence 为 InMemory，因此当前属于 Evaluation Framework 能力，不等同于生产级持久化 Baseline。

正式生产 Baseline 持久化与 Governance 放入 Phase 2.7。

---

# 十六、当前问题分级

## P0

- C.13.1 必须完成 Runtime / Regression 验证；
- C.13.2 必须完成完整源码审计后再修改；
- C.13.3 必须解决 Multi-Metric Semantic Evidence 的单项问题；
- Evaluation Contract 不得重新出现双重 PASS / FAIL 真相。

## P1

- Dimension / Filter / Table 的 Metadata Semantic Evidence 继续深化；
- Golden Dataset 覆盖继续扩充；
- Baseline InMemory 持久化边界；
- QueryPlanBuilder 历史注释与当前 Join 实现同步；
- Diagnostics Contract 统一。

---

# 十七、Phase 2.6 完成度

当前采用：

```text
Architecture     25%
Implementation   30%
Evaluation       25%
Regression       20%
```

当前源码审计估算：

- Architecture：90%
- Implementation：90%
- Evaluation：75%
- Regression：60%
- 综合约：**79%**

该数值为源码审计估算，不是运行时统计。

---

# 十八、后续严格顺序

```text
C.13.1 Evaluation Contract
        ↓
[Runtime / Regression 验证]
        ↓
C.13.2 完整源码审计
        ↓
C.13.2 Contract / Gap 收敛
        ↓
C.13.2 一次性统一修改
        ↓
重新读取全部受影响源码
        ↓
静态 Contract / DI / Namespace 复核
        ↓
编译
        ↓
Controller Runtime
        ↓
Golden Regression
        ↓
C.13.3 Multi-Metric Semantic Closure
        ↓
C.13.4 Golden Dataset Expansion
        ↓
C.13.5 Runtime Regression
        ↓
Phase 2.6 Exit Review
        ↓
Phase 2.7 Calibration / Evaluation Governance
```

---

# 十九、最终开发原则

1. `master` 是唯一源码基线。
2. 禁止创建新的开发分支。
3. 所有正式修改直接提交 `master`。
4. GitHub 正式描述、审计说明、提交说明使用中文。
5. **完整相关源码审计完成前不得修改源码。**
6. **先建立字段生命周期和调用链，再确定修改面。**
7. **一次性确定修改面后统一修改，避免连续返工。**
8. **修改后必须重新读取全部受影响文件，确认没有因 Contract 变化产生新的遗漏。**
9. **AI 生成代码必须经过与人工代码相同的源码审计、DI 审计和 Runtime 验证。**
10. 编译通过不等于功能完成。
11. Runtime / Regression 未验证不宣布 COMPLETE。
12. Golden Dataset 是 Evaluation 事实基础。
13. Evaluation 必须保持唯一 Contract。
14. 已冻结 Phase 不无边界重新开发。
15. 不创建独立 Test Project，优先通过现有 Controller / Runtime 验证。
16. 不允许为了计划完整而虚构源码能力。
17. 每次修改必须明确所属 Phase / C 项。
18. **发现新的下游遗漏时，必须回溯审计边界，不能继续采用“发现一个改一个”的模式。**
19. **任何 Service 的构造函数依赖必须与 Program.cs DI 注册形成闭环。**
20. **任何 Contract 类型变化必须检查所有 Caller / Callee，而不是只修复当前编译错误。**
21. **Namespace、Interface、Implementation、DTO、Model、Resolution 类型均以 `master` 实际源码为准，不允许猜测。**
22. **阶段验收必须同时证明源码、Contract、DI、Runtime、Golden Dataset 与开发计划一致。**

---

# 二十、V2.6 本次更新记录

本次更新是在现有 v2.5 计划基础上增量强化，保留原有 Phase 2.6 / C.13.2-C.13.5 规划，不覆盖既有阶段内容。

新增并正式固化：

1. `master` 继续作为唯一源码基线。
2. 不创建任何新的开发分支。
3. GitHub 项目描述、开发计划、审计说明和提交说明使用中文。
4. **完整源码读取必须先于源码修改。**
5. **禁止以编译错误驱动开发。**
6. **AI 生成或修改代码与人工代码采用完全相同的审计标准。**
7. 强制执行 Interface → Implementation → Caller → DTO / Model Contract 闭环。
8. 强制执行 Constructor Dependency → Implementation → DI Registration → Lifetime → 上游可构造闭环。
9. 禁止采用“Unable to resolve service 一个异常补一个 DI 注册”的方式。
10. 强制逐文件核对 Namespace、类型、继承/实现关系、构造函数、方法签名和返回类型。
11. 强制检查 QueryPlan / Evaluation / Golden Dataset 的字段、类型和 Resolution 漂移。
12. 修改前必须形成完整修改面；无法确定修改面时继续审计而不是先修改。
13. 修改后必须重新读取全部受影响文件并进行静态一致性复核。
14. 编译、启动、Controller / Runtime Smoke Test、Golden Regression 组成正式验收链。
15. 继续坚持不新建独立 Test Project，优先通过现有 Controller / Runtime 接口验证。
16. 如果后续发现原本应该审计但遗漏的文件，必须记录为审计边界遗漏并回溯，而不是继续“发现一个改一个”。
17. 本次更新不改变 Phase 2.3 / 2.4 / 2.5 冻结状态，不改变 Production Safety Pipeline 边界。

> **V2.6 的核心目的不是增加文档内容，而是把“完整读取、完整理解、统一修改、全链路验证”从工作习惯升级为项目强制标准。**
