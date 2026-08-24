# SuperBuilder AI Native BI Phase开发计划

> 文档版本：v2.7  
> 文档性质：项目正式开发基线  
> **唯一源码基线：GitHub `master`**  
> 当前开发阶段：**Phase 2.6 — Query Evaluation Framework**  
> 当前工作单元：**C.13.2 已完成源码审计与 Builder Contract 修复，当前转入 V2.6 Ranking Contract 审计**

---

# 一、最高优先级开发约束

1. **GitHub `master` 是唯一源码基线。** 所有源码审计、Phase 状态、计划校准均以 `master` 最新源码为准。
2. **禁止创建新的开发分支。** 正式源码、修复、文档更新直接修改 `master`。
3. **不得使用其他分支、临时工作区或历史代码冒充 `master` 完成状态。**
4. **GitHub 正式描述、开发计划、源码审计说明、提交说明统一使用中文。** C# 类型、方法、属性和 Namespace 仍遵循代码规范。
5. **源码事实优先于计划。** 计划中的 COMPLETE 必须能在 `master` 找到真实实现，并完成要求的集成验证。
6. **代码文件存在不等于功能完成。** 必须检查 Interface、Implementation、Caller、Model/DTO、Constructor、DI、Runtime、Controller、Golden Regression。
7. **Phase / C 项完成必须满足 Exit Criteria。** 未完成 Runtime / Regression 不得宣布 COMPLETE。
8. **Phase 2.3、2.4、2.5 已冻结。** 后续仅允许明确 Bug、Contract 问题或新阶段证明的兼容性问题修复。
9. **Production Safety Pipeline 与 Evaluation Framework 保持边界。** Evaluation 不替代生产执行安全链。
10. **Golden Dataset 是 Evaluation 的事实数据基础。** 不得使用临时人工判断代替 Golden Regression。
11. **每次正式修改必须明确所属 Phase / C 项，并使用中文 Commit 描述。**
12. **不得为了让开发计划完整而虚构源码能力。**
13. **AI 生成或修改的代码不视为天然正确。** 必须按照与人工代码相同的完整源码审计、DI、Build、Runtime、Regression 标准验收。
14. **禁止以“发现一个编译错误、修一个错误”为主要开发方式。** 编译只能作为完整源码审计后的验证环节。
15. **任何关键 Contract、DI、Namespace、Constructor Dependency 变化，都必须进行上下游闭环检查。**
16. **不新建独立 Test Project。** 优先使用现有 Controller / Runtime 诊断接口进行真实验证。
17. **不得通过删除功能、放宽 Gate、修改 Coverage Analyzer 或伪造 Golden Case 来掩盖能力缺失。**
18. **Ranking / DetailRanking / AggregateRanking 必须遵守自身 Contract 边界。** 不得为了当前 C 项通过 Coverage 而强行删除、绕过或重新定义这些能力。
19. **每次 GitHub 更新后必须能够从 Commit、开发计划和源码恢复当前工作状态，确保会话中断后可继续。**

---

# 二、强制标准工作方式：完整审计后再修改

所有涉及 Model、DTO、Contract、Builder、Semantic、Evaluation、Scoring、Runtime、Controller、DI 或跨文件调用链的任务，统一采用：

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
6. 建立调用链、数据流矩阵
       ↓
7. 建立 Constructor Dependency / DI Matrix
       ↓
8. 找出 Contract Gap / Bug / Drift / DI Missing
       ↓
9. 一次性确定修改面
       ↓
10. 统一修改源码
       ↓
11. 重新读取全部受影响文件
       ↓
12. 静态 Contract / Namespace / DI 复核
       ↓
13. Build
       ↓
14. Controller / Runtime 真实验证
       ↓
15. Golden Dataset Regression
       ↓
16. Coverage / Quality / Release Gate
       ↓
17. 确认无回归
       ↓
18. 更新开发计划
       ↓
19. 中文提交 master
```

### 完整相关源码至少包括

```text
Model / DTO / Contract
 ↓
创建者 / Builder / Mapper
 ↓
Semantic Applicability / Resolution
 ↓
Runtime Model
 ↓
Evaluator / Scoring / Aggregation
 ↓
Runtime Service
 ↓
Controller
 ↓
Program.cs / DI
 ↓
Golden Dataset
 ↓
Regression / Coverage / Quality / Release Gate
```

### 逐字段追踪

关键字段必须形成：

```text
定义 → 创建 → 赋值 → 转换 → 传递 → 读取 → 比较/评分 → 输出
```

重点包括：`SemanticText`、`Field`、`MetadataColumnId`、`MetadataTableId`、`DataSourceId`、`Aggregation`、`Operator`、`Value`、`IntentType`、`OrderBy`、`OrderDirection`、`Limit`、`IsRanking`、`IsDetailRanking`、`IsAggregateRanking`、`Score`、`Decision`、`ApplicabilityState`、`Resolution`、`OverallScore`。

### 修改面规则

修改前必须明确：

```text
目标问题 → 根因 → 文件 → Contract → 调用链 → Constructor → DI → 同步文件 → 验证方式
```

无法确定完整修改面时继续审计，不得先改代码。

### 修改后规则

重新读取所有受影响文件，并验证：

```text
源码 → Type / Namespace → Contract → Constructor → DI → Runtime → Controller → Golden → Regression
```

Runtime 无法执行时必须标记 `IMPLEMENTED_BUT_UNVERIFIED`，不得标记 COMPLETE。

---

# 三、Contract / Namespace / DI 强制闭环

所有关键对象必须同时验证：

```text
Interface ↕ Implementation ↕ Caller ↕ DTO / Model / Contract
```

必须检查类型、方法、参数、返回值、nullable、collection、字段名称及 Resolution 类型的一致性。

Namespace 必须逐文件以 `master` 源码为准，不根据旧目录或历史版本猜测。

所有 Service 修改前必须读取完整 Constructor Dependency Graph，并一次性确认：

```text
依赖存在 → Implementation → Interface → Program.cs 注册 → Lifetime → 上游可构造
```

禁止采用“Unable to resolve 一个服务就补一个 DI 注册”的循环方式。

---

# 四、当前正式源码基线

最近已完成的源码修复：

```text
Commit: debfefa1dc0360fd79eda37582061913b9e3a68e
中文描述：修复 C.13.2 Semantic Resolution 二次语义搜索漂移
```

该修改已通过 GitHub Actions：

```text
Workflow Run: 32684194952
Release Build: PASS
Controller 启动: PASS
```

当前必须以该 Commit 之后的 `master` 作为后续审计基线。

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

# 六、Phase 2.6 生产链与 Evaluation 边界

生产链保持：

```text
User Question → Query Understanding → Metadata Semantic Retrieval → QueryPlan Builder
→ Validation → Repair / ReValidate → Confidence → Decision Gate → Explainability
→ SQL Builder → SQL Execution → Result Understanding
```

Evaluation 独立验证：

```text
Golden Dataset → Semantic Applicability → Semantic Resolution → Runtime QueryPlan
→ Section Evaluation → Dimension Scoring → Overall Score → Regression
→ Coverage / Quality / Release Gate → Calibration / Baseline
```

---

# 七、C.13.1 — Evaluation Contract 收敛

**状态：IMPLEMENTED，已通过当前 Runtime 基础链验证；继续由后续 Regression 保护。**

`QueryPlanEvaluator`、Scoring Service、Evaluation Result 已形成统一 Evaluation Contract。禁止重新出现多个独立 PASS / FAIL 真相。

---

# 八、C.13.2 — Metric / Dimension / Filter / Table Semantic Evaluation

## 8.1 审计结论

**源码全链路审计：COMPLETE。**

已确认：

- Metric Semantic / Field / Aggregation Evaluation 已存在；
- Dimension Semantic / Physical Binding Evaluation 已存在；
- Filter Semantic / Operator / Value / Physical Binding Evaluation 已存在；
- Table Semantic / Physical Binding Evaluation 已存在；
- Golden Dataset、Applicability、Resolution、Builder、Evaluator、Scoring、Controller、DI 链路已形成；
- C.13.2 的关键 Contract 漂移已定位并修复。

## 8.2 已修复 Contract Drift

原问题：

```text
Semantic Resolution
        ↓
QueryPlanBuilder
        ↓
再次 Semantic Search
        ↓
Runtime Binding
```

已修复为：

```text
Semantic Applicability
        ↓
Semantic Resolution
        ↓
QueryPlanBuilder
        ↓
Runtime Plan Skeleton
        ↓
Apply Metric / Filter / Dimension / Table / Order Resolution
        ↓
QueryPlan
```

原则：**Semantic Resolution 是 C.13.2 Runtime Semantic Binding 的唯一可信来源，Builder 不得再次进行语义搜索。**

## 8.3 验证结果

GitHub Actions `32684194952` 已证明：

- Release Build：PASS；
- Web API / Controller：启动成功；
- Serialization：PASS；
- Three-State：PASS；
- Golden Dataset Contract：PASS；
- Dataset 17 Cases / 17 Enabled / 无重复 ID / 无非法 Case：PASS。

## 8.4 当前 Exit Blocker

当前 Golden Coverage 返回：

```text
Ranking          = 4
DetailRanking    = 0
AggregateRanking = 0
```

因此 Golden Coverage Gate FAIL，后续 Quality / Release Gate 被正确阻断。

**该失败不能通过以下方式解决：**

```text
❌ 修改 Coverage Analyzer 让 0 通过
❌ 修改 Coverage Gate 忽略 0
❌ 删除 DetailRanking / AggregateRanking
❌ 伪造 Golden Case
❌ 为当前 C.13.2 强行重定义 Ranking Contract
```

## 8.5 C.13.2 当前状态

```text
源码审计       COMPLETE
Contract 修复  COMPLETE
Release Build  PASS
Controller     PASS
Golden 基础链  PASS
Coverage Gate  BLOCKED（Ranking Contract Coverage）
C.13.2 全阶段   NOT YET COMPLETE
```

因此 C.13.2 暂不标记 COMPLETE；但**后续工作不得继续扩大 C.13.2 的 Semantic Evaluation 修改面**，而应转入 V2.6 Ranking Contract 专项审计。

---

# 九、V2.6 Ranking Contract — 当前下一工作单元

> **这是当前会话中断后必须能够直接恢复的工作锚点。**

## 9.1 当前问题

现有 Golden Dataset 已有 Ranking 正负 Case，但 Coverage 结果：

```text
Ranking = 4
DetailRanking = 0
AggregateRanking = 0
```

因此当前首先审计分类链，而不是立即新增 Case。

## 9.2 强制审计链

```text
Golden Case
   ↓
Golden Expectation
   ↓
QueryIntentNormalizer
   ↓
QueryIntent
   ↓
QueryPlanBuilder
   ↓
QueryPlan
   ↓
IsRanking / IsDetailRanking / IsAggregateRanking
   ↓
QueryPlanEvaluator / Query Shape Scoring
   ↓
GoldenDatasetCoverageAnalyzer
   ↓
Coverage Gate
```

同时追踪：

```text
OrderBy
OrderDirection
Limit
Aggregation
IsOrderingMetric
QueryOrder
```

## 9.3 必须首先审计的 Case

```text
GQ-006
GQ-010
GQ-N004
GQ-N005
```

目标不是修改 Case，而是确定它们为什么被识别为 `Ranking`，却没有产生 `DetailRanking / AggregateRanking` 覆盖。

## 9.4 Ranking Contract 原则

```text
Ranking
├── DetailRanking
└── AggregateRanking
```

必须先定义真实语义边界，再决定：

- Runtime Contract 修复；或
- 现有 Case 分类修正；或
- 新增真实 Golden Case。

禁止先为了 Coverage 变绿而添加没有真实业务语义依据的 Case。

## 9.5 当前明确禁止

```text
❌ 删除 DetailRanking
❌ 删除 AggregateRanking
❌ 允许 Coverage = 0
❌ 修改 Coverage Analyzer 掩盖缺失
❌ 修改 Coverage Gate 掩盖缺失
❌ 伪造两个测试 Case
❌ 在未完成 Ranking Contract 审计前修改 Normalizer / Builder / Evaluator
```

## 9.6 Ranking 审计完成后的验证链

```text
Ranking Contract
 ↓
Model
 ↓
Normalizer
 ↓
Builder
 ↓
Evaluator
 ↓
Coverage Analyzer
 ↓
Golden Dataset
 ↓
Controller
 ↓
Golden Regression
 ↓
Coverage / Quality / Release Gate
```

---

# 十、C.13.3 — Multi-Metric Semantic Closure

在 C.13.2 Contract 收敛和 Ranking 边界明确后进入。

重点：禁止长期使用 `Golden Metrics.FirstOrDefault()` 作为整个 Semantic Evaluation 的唯一依据；所有 Multi-Metric 必须逐项产生 Semantic Evidence、Physical Binding 和评分。

---

# 十一、C.13.4 — Golden Dataset Expansion

扩展必须以真实业务场景覆盖为原则，而不是简单增加数量。最低方向包括：ColumnMetric、EntityCount、Multi-Metric、Dimension + Metric、Filter、Ranking / TopN、Join、NotResolved、Ambiguous、Negative / Wrong Binding。

**Golden null 表示该维度不进行断言，不等价于能力缺失。**

新增 Case 必须有真实业务语义、明确 Contract、正负向理由，并通过 Dataset Contract Validation。

---

# 十二、C.13.5 — Runtime Regression

C.13 Exit 必须同时满足：

1. Golden Dataset 可以通过真实 Controller / Runtime 执行；
2. EvaluationResult 包含 Section Result、DimensionScores、OverallScore、Decision；
3. Applicability BLOCK / REVIEW 不得错误变成 Runtime PASS；
4. Semantic Evidence 与 Runtime 物理绑定一致；
5. Multi-Metric 不得只验证第一项；
6. Scoring Service 必须真正参与最终 Evaluation；
7. Regression 输出失败 Case、失败维度和原因；
8. Regression 支持 Baseline Comparison；
9. Runtime 输出 Dataset / Evaluation / Baseline 版本；
10. 源码注释、开发计划和实际行为一致。

---

# 十三、Phase 2.6 C.14 — Join Evaluation

**状态：IMPLEMENTED / FROZEN FOR CURRENT BASELINE。**

后续只允许明确 Contract / Bug 修复或 Golden 新场景暴露的明确缺口，不得无边界扩展 Join 推理算法。

---

# 十四、Golden Baseline

当前已形成 Registry、Release、Comparison、Regression、Validation。默认 Persistence 为 InMemory，属于 Evaluation Framework 能力，不等同于生产级持久化 Baseline。生产 Baseline 持久化与 Governance 放入 Phase 2.7。

---

# 十五、当前阶段完成度与门禁

当前 Phase 2.6 仍为 **IN PROGRESS**。

完成度不得只根据源码数量估算，必须结合：

```text
Architecture
Implementation
Evaluation
Build
Controller Runtime
Golden Regression
Coverage
Quality
Release Gate
```

当前已确认：

```text
C.13.2 Builder Contract 修复       PASS
Release Build                       PASS
Controller 启动                     PASS
Golden Dataset Contract             PASS
Golden Coverage                    BLOCKED
```

因此当前不更新为 Phase 2.6 COMPLETE。

---

# 十六、会话中断后的恢复协议

为保证新的 ChatGPT 会话可以直接从 GitHub 开发计划恢复，任何后续会话开始时必须按以下顺序：

```text
1. 读取本文件 V2.0 最新版本
2. 读取 master 最新 Commit
3. 读取最近一次源码 Commit / Workflow Run
4. 根据“当前工作单元”恢复任务
5. 不重新假设历史状态
6. 不重复已经完成的源码审计
7. 若发现计划与 master 不一致，以 master 为事实并先校准计划
```

当前恢复锚点：

```text
Phase：2.6
工作单元：V2.6 Ranking Contract
最近源码修复：debfefa1dc0360fd79eda37582061913b9e3a68e
Workflow Run：32684194952
当前阻塞：DetailRanking = 0；AggregateRanking = 0
下一步：完整审计 GQ-006 / GQ-010 / GQ-N004 / GQ-N005 → Normalizer → Builder → Evaluator → Coverage
```

---

# 十七、最终开发原则

1. `master` 是唯一源码基线。
2. 不创建新的开发分支。
3. 正式修改直接提交 `master`。
4. GitHub 正式描述、审计说明、Commit 描述使用中文。
5. 完整相关源码审计完成前不得修改源码。
6. 先建立字段生命周期和调用链，再确定修改面。
7. 一次性确定修改面后统一修改，避免连续返工。
8. 修改后重新读取全部受影响文件。
9. AI 生成代码必须经过完整源码、DI、Build、Runtime、Regression 验证。
10. 编译通过不等于功能完成。
11. Runtime / Regression 未验证不得宣布 COMPLETE。
12. Golden Dataset 是 Evaluation 事实基础。
13. Evaluation 必须保持唯一 Contract。
14. 已冻结 Phase 不无边界重新开发。
15. 不创建独立 Test Project，优先通过现有 Controller / Runtime 验证。
16. 不允许通过 Coverage Gate、Coverage Analyzer 或伪造 Golden Case 掩盖真实能力缺失。
17. 每次修改必须明确 Phase / C 项。
18. 发现审计边界遗漏时必须回溯审计，而不是继续“发现一个改一个”。
19. Constructor Dependency 必须与 DI Registration 闭环。
20. Contract 类型变化必须检查所有 Caller / Callee。
21. Namespace、Interface、Implementation、DTO、Model、Resolution 以 master 实际源码为准。
22. 阶段验收必须同时证明源码、Contract、DI、Runtime、Golden Dataset 与开发计划一致。
23. **开发计划本身必须成为会话恢复锚点：每次形成阶段性最终结论后及时更新，避免会话中断导致工作状态丢失。**

---

# 十八、本次 V2.7 更新记录

本次更新基于 `master` 当前真实状态，不提前宣称 C.13.2 或 Phase 2.6 完成。

新增 / 固化：

1. C.13.2 全链路源码审计完成；
2. `QueryPlanBuilder` Semantic Resolution 二次语义搜索漂移已修复；
3. Commit `debfefa1dc0360fd79eda37582061913b9e3a68e` 已进入 master；
4. Workflow Run `32684194952` 已确认 Release Build 与 Controller 启动成功；
5. Golden Dataset Contract 已通过；
6. Golden Coverage 当前真实阻塞为 `DetailRanking = 0`、`AggregateRanking = 0`；
7. 明确禁止通过修改 Coverage Gate / Analyzer、删除 Ranking 能力或伪造 Case 解决阻塞；
8. 当前正式工作单元切换为 **V2.6 Ranking Contract 审计**；
9. 固化 Controller 验证、不新建 Test Project、中文 Commit、先完整审计再修改、严格 Phase/C 项边界等既有要求；
10. 增加“会话中断恢复协议”，确保后续新会话可通过本文件直接恢复。

> **本计划的核心原则：源码事实优先、完整审计后修改、真实 Runtime 验证、Golden Regression 验收、开发计划作为持续恢复锚点。**
