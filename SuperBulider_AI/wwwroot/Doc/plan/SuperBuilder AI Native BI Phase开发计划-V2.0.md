# SuperBuilder AI Native BI Phase开发计划

> 文档版本：v2.9
> 文档性质：项目正式开发基线 + Phase 开发测试管理总计划
> **唯一源码基线：GitHub `master`**
> 主计划职责：**仅记录项目当前进度、当前工作单元、当前 STEP、状态、Commit、阻塞与下一步**
> 阶段完整开发任务、完整测试步骤和完整验收标准由对应 Phase 开发测试计划维护，本文件负责关联与当前进度同步。

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
20. **每次代码修改推送 `master` 后，必须先由本地环境 `git pull` 拉取最新 `master`，再进行本地 Build 与 Controller / Action Runtime 测试。** 本地实际结果作为正式验收证据。
21. **每进入一个新的 Phase，必须先全量审计当前 `master`，再确定本阶段全部开发任务。**
22. **阶段开发计划必须在该 Phase 开始开发前建立，并包含本阶段全部开发与测试步骤。**
23. **每个 STEP 开始与完成时，必须同步更新主开发计划和阶段开发计划。**
24. **主开发计划只记录当前进度，不复制阶段完整技术方案和历史测试过程。**
25. **阶段开发计划是该 Phase 的完整执行契约；新增任务必须先更新阶段计划再开发。**
26. **每个 Phase 必须建立对应 Runtime 分步测试记录，记录真实原始 JSON 和 PASS / FAIL / BLOCK / REVIEW。**
27. **上一 Phase 未满足 Exit Criteria 时，不得把下一 Phase 标记为 COMPLETE；允许提前建立 PLANNED 的入口文档，但不得伪装为已完成。**

---

# 二、Phase 生命周期与文档管理规则

## 2.1 Phase 入口强制流程

每进入一个新 Phase，必须执行：

```text
上一 Phase Exit Review
        ↓
锁定最新 master
        ↓
全量源码 + 文档 + Runtime + Golden 审计
        ↓
确认真实完成度 / 遗留项 / 风险
        ↓
确定本 Phase 全部开发任务
        ↓
建立 PhaseX.Y-主题-开发测试计划.md
        ↓
建立 PhaseX.Y-Runtime分步测试记录.md
        ↓
阶段文档关联本主计划
        ↓
主计划记录当前 Phase / 当前 STEP
        ↓
开始 STEP-01
```

### 入口全量审计至少覆盖

```text
Models / DTO / Contracts
Interfaces
Services
Infrastructure
DI / Program.cs
Controller / Action / Route
调用链 / 数据流
Golden Dataset / Evaluation
现有 Runtime 测试接口
Build / CI
上一 Phase 计划与测试记录
最近 Commit / Workflow
已知 PASS / FAIL / BLOCK / REVIEW
```

审计结论必须区分：`IMPLEMENTED`、`VERIFIED`、`IMPLEMENTED_BUT_UNVERIFIED`、`PARTIAL`、`MISSING`、`BLOCKED`、`REGRESSION`。

## 2.2 阶段开发计划

每个 Phase 必须建立唯一阶段开发计划，至少包含：

1. 阶段目标；
2. 入口全量审计结论；
3. 上一 Phase 边界；
4. Scope / Out of Scope；
5. 核心 Contract；
6. 数据模型 / 接口变化；
7. **全部源码开发任务**；
8. **全部 Runtime / Golden / Regression 测试任务**；
9. 每一步前置条件；
10. 每一步开发完成条件；
11. 每一步测试完成条件；
12. 对应源码文件 / 模块；
13. 对应 Runtime STEP；
14. Exit Criteria；
15. Quality / Release Gate；
16. 下一 Phase 入口条件。

阶段开发计划是本阶段完整执行契约，不得遗漏已经确定的历史计划内容。新增任务必须先补入阶段计划并关联测试步骤，再开始开发。

## 2.3 主开发计划职责

本文件**只记录当前进度**：

```text
当前 Phase
当前 C 项 / 工作单元
当前 STEP
当前状态
当前 Commit
当前阻塞 / 风险
下一步动作
已完成 Phase 的最终状态
```

主计划不重复完整技术方案、不复制完整 Runtime JSON、不保存已经结束阶段的逐步过程；历史详细内容保留在对应 Phase 文档中。

它必须回答：

> **项目现在做到哪里？下一步做什么？为什么停在这里？**

## 2.4 STEP 同步规则

每个步骤都必须执行：

```text
STEP 开始
↓
主计划：当前 STEP = N / IN_PROGRESS
阶段计划：STEP-N = IN_PROGRESS
↓
开发 / Build / Runtime
↓
记录完整结果
↓
PASS / FAIL / BLOCK / REVIEW
↓
阶段计划更新结果与证据
↓
主计划更新当前进度 / Commit / 下一步
↓
提交 master
↓
进入下一 STEP
```

**步骤开始和步骤完成必须同时更新两份计划。**

## 2.5 Runtime 分步测试规则

每个 Phase 必须建立：

`PhaseX.Y-Runtime分步测试记录.md`

格式参考 Phase 2.6 Runtime 分步测试记录。每个 STEP 原则上只执行一个明确 Runtime 动作 / 地址 / 输入，并记录：

- STEP 编号；
- 目的；
- 前置条件；
- Controller / Action / 地址；
- 参数；
- 预期结果；
- 完整原始 JSON；
- 实际结果；
- PASS / FAIL / BLOCK / REVIEW；
- Commit；
- 下一 STEP。

API 存在不等于 Runtime PASS；必须真实执行。

## 2.6 失败规则

FAIL / BLOCK 时停止所有依赖当前步骤的后续步骤：

```text
失败
↓
根因审计
↓
更新阶段计划
↓
修改源码
↓
Build
↓
git pull
↓
重新执行当前 STEP
```

禁止跳步、降低 Gate、删除 Case、修改 Expected 制造 PASS。

## 2.7 Phase Exit 规则

Phase 只有满足：

```text
入口审计完成
AND 全部计划任务完成
AND 关键源码实现
AND Build PASS
AND Controller / Runtime PASS
AND Golden / Regression PASS（适用时）
AND 安全 Gate 回归 PASS
AND Coverage / Quality / Release Gate PASS（适用时）
AND 阶段计划闭环
AND Runtime 记录闭环
AND 主计划同步
```

才能标记 COMPLETE；缺证据使用 `IMPLEMENTED_BUT_UNVERIFIED`。

## 2.8 会话恢复规则

任何会话中断后优先读取：

1. 本主开发计划；
2. 当前 Phase 开发测试计划；
3. 当前 Phase Runtime 分步测试记录；
4. 最近相关源码 Commit；
5. 当前 FAIL / BLOCK / REVIEW。

从主计划的当前 STEP 直接恢复，不重复已有 PASS 步骤，除非源码、配置、数据、环境或依赖发生影响性变化。

---

# 三、强制标准工作方式：完整审计后再修改

所有涉及 Model、DTO、Contract、Builder、Semantic、Evaluation、Scoring、Runtime、Controller、DI 或跨文件调用链的任务，统一采用：

```text
锁定 master
↓
明确 Phase / C
↓
完整读取相关源码
↓
逐文件审计
↓
逐字段 / 逐方法追踪
↓
建立调用链 / 数据流矩阵
↓
建立 Constructor Dependency / DI Matrix
↓
找出 Contract Gap / Bug / Drift / DI Missing
↓
一次性确定修改面
↓
统一修改
↓
重新读取受影响文件
↓
静态 Contract / Namespace / DI 复核
↓
Build
↓
推送 master
↓
本地 git pull
↓
Controller / Action Runtime
↓
Golden Regression
↓
Coverage / Quality / Release Gate
↓
确认无回归
↓
同步主计划 + 阶段计划 + Runtime 记录
```

完整相关源码至少包括：

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

关键字段必须形成：

`定义 → 创建 → 赋值 → 转换 → 传递 → 读取 → 比较/评分 → 输出`

重点字段继续包括：`SemanticText`、`Field`、`MetadataColumnId`、`MetadataTableId`、`DataSourceId`、`Aggregation`、`Operator`、`Value`、`IntentType`、`OrderBy`、`OrderDirection`、`Limit`、`IsRanking`、`IsDetailRanking`、`IsAggregateRanking`、`Score`、`Decision`、`ApplicabilityState`、`Resolution`、`OverallScore`。

---

# 四、Contract / Namespace / DI 强制闭环

所有关键对象必须验证：

```text
Interface ↕ Implementation ↕ Caller ↕ DTO / Model / Contract
```

必须检查类型、方法、参数、返回值、nullable、collection、字段名称及 Resolution 类型一致性。

Namespace 必须逐文件以 `master` 实际源码为准。

所有 Service 修改前必须读取完整 Constructor Dependency Graph：

```text
依赖存在 → Implementation → Interface → Program.cs 注册 → Lifetime → 上游可构造
```

禁止“Unable to resolve 一个服务就补一个 DI 注册”的循环方式。

---

# 五、Golden / Regression / 安全规则

Golden Dataset 是 Evaluation 事实基础，不是为了让测试通过而修改的目标。

修改 Golden 必须记录：修改原因、原契约、新契约、影响 Case、对应阶段计划、回归结果。

Golden Regression 必须分别验证：

```text
语义解析正确性
QueryPlan 正确性
SQL 生成正确性
SQL Runtime 正确性
安全 Gate 正确性
```

不得通过删除功能、放宽 Gate、修改 Coverage Analyzer、伪造 Golden Case 掩盖真实能力缺失。

不新建独立 Test Project；优先使用现有 Controller / Action / Runtime 诊断接口。

---

# 六、当前正式源码基线与历史事实

当前主计划原有基线记录：

```text
Commit: c403a68ab705b4a96fc770d87e0abaf0f31775d0
中文描述：修正 C.13.3 本地基础设施诊断汇总 Action
```

历史关键修复：

```text
Commit: debfefa1dc0360fd79eda37582061913b9e3a68e
中文描述：修复 C.13.2 Semantic Resolution 二次语义搜索漂移
Workflow Run: 32684194952
Release Build：PASS
Controller 启动：PASS
```

**注意：以上为上一版本主计划保存的历史证据。后续必须以 GitHub `master` 最新 Commit 为当前事实，不得继续把旧 Commit 当作当前基线。**

---

# 七、总体 Phase 历史计划与状态

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
| Phase 2.7 | PLANNED | Dimension-Aware QueryPlan & SQL Closure；已建立入口规划，尚未因计划文档建立而宣布 COMPLETE |

---

# 八、Phase 2.6 生产链与 Evaluation 边界

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

# 九、C.13.1 — Evaluation Contract 收敛

**状态：IMPLEMENTED，已通过当前 Runtime 基础链验证；继续由后续 Regression 保护。**

`QueryPlanEvaluator`、Scoring Service、Evaluation Result 已形成统一 Evaluation Contract。禁止重新出现多个独立 PASS / FAIL 真相。

---

# 十、C.13.2 — Metric / Dimension / Filter / Table Semantic Evaluation

### 审计结论

源码全链路审计已完成；Metric、EntityCount、Filter、Dimension、Table 的 Semantic / Physical Binding Evaluation、Golden Dataset、Applicability、Resolution、Builder、Evaluator、Scoring、Controller、DI 链路已形成。

已确认关键 Contract 漂移并修复：

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

原则：Semantic Resolution 是 C.13.2 Runtime Semantic Binding 的可信来源，Builder 不得再次进行无契约语义搜索。

历史验证：GitHub Actions `32684194952` 的 Release Build、Web API / Controller、Serialization、Three-State、Golden Dataset Contract 均 PASS；Dataset 当时为 17 Cases / 17 Enabled / 无重复 ID / 无非法 Case。

此前 Coverage 记录为 Ranking=4、DetailRanking=0、AggregateRanking=0，但当前 Golden Ground Truth 已包含相关 AggregateRanking / DetailRanking 期望，因此旧数字与当前事实存在漂移，必须重新以当前 master Runtime / Coverage 实测为准。

禁止：

```text
❌ 修改 Coverage Analyzer 让 0 通过
❌ 修改 Coverage Gate 忽略 0
❌ 删除 DetailRanking / AggregateRanking
❌ 伪造 Golden Case
❌ 为 C.13.2 强行重定义 Ranking Contract
```

C.13.2 不因历史 Build PASS 而自动 COMPLETE；必须以当前 Runtime / Regression / Coverage 证据闭环。

---

# 十一、V2.6 Ranking Contract — 历史计划内容保留

当前 Golden Dataset 已存在 Ranking、DetailRanking、AggregateRanking Ground Truth；此前 Coverage 记录与当前 Dataset 存在状态漂移。因此首先审计分类链和 Coverage 实际运行结果，而不是立即新增 Case。

强制审计链：

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

同时追踪：`OrderBy`、`OrderDirection`、`Limit`、`Aggregation`、`IsOrderingMetric`、`QueryOrder`。

必须首先审计：`GQ-006`、`GQ-010`、`GQ-N004`、`GQ-N005`、`GQ-011`。

Ranking Contract：

```text
Ranking
├── DetailRanking
└── AggregateRanking
```

必须先定义真实语义边界，再决定 Runtime Contract 修复、现有 Case 分类修正或新增真实 Golden Case。禁止为了 Coverage 变绿而添加没有真实业务语义依据的 Case。

---

# 十二、C.13.3 — Multi-Metric Semantic Closure 历史计划

禁止长期使用 `Golden Metrics.FirstOrDefault()` 作为整个 Semantic Evaluation 的唯一依据；所有 Multi-Metric 必须逐项产生 Semantic Evidence、Physical Binding 和评分。

---

# 十三、C.13.4 — Golden Dataset Expansion 历史计划

扩展必须以真实业务场景覆盖为原则，不以数量为目标。方向包括：ColumnMetric、EntityCount、Multi-Metric、Dimension + Metric、Filter、Ranking / TopN、Join、NotResolved、Ambiguous、Negative / Wrong Binding。

Golden `null` 表示该维度不进行断言，不等价于能力缺失。

新增 Case 必须有真实业务语义、明确 Contract、正负向理由，并通过 Dataset Contract Validation。

---

# 十四、C.13.5 — Runtime Regression 历史计划

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

# 十五、Phase 2.6 C.14 — Join Evaluation

**状态：IMPLEMENTED / FROZEN FOR CURRENT BASELINE。**

后续只允许明确 Contract / Bug 修复或 Golden 新场景暴露的明确缺口，不得无边界扩展 Join 推理算法。

---

# 十六、Golden Baseline

当前已形成 Registry、Release、Comparison、Regression、Validation。默认 Persistence 为 InMemory，属于 Evaluation Framework 能力，不等同于生产级持久化 Baseline。生产 Baseline 持久化与 Governance 放入 Phase 2.7。

---

# 十七、C.13.3-LR — 本地 Runtime 测试与 API 存在性审计历史记录

C.13.3-LR-01 ～ C.13.3-LR-13：**13/13 已完成**，按既定要求冻结，不重复执行。

此前 Phase 2.6 测试 API 存在性审计确认：SQL Server、Qdrant、本地基础设施、Golden Runtime、Golden Cases、Release Gate、Semantic Applicability、Golden Baseline、QueryPlan Confidence API 均存在；API 存在不等于 Runtime PASS，真实验收仍要求 `git pull → Build → Controller / Action → Golden Regression → Coverage / Quality / Release Gate`。

历史测试基址：`http://localhost:5032`；当前实际本地端口必须以最新 `launchSettings.json` / Runtime 为准，不得将历史端口当作永久事实。

---

# 十八、Phase 2.7 入口定义（PLANNED，进入执行前必须先完成入口全量审计）

> **Phase 2.7 — Dimension-Aware QueryPlan & SQL Closure**

核心目标：把 Dimension 从“必须找到主表”的旧隐含假设中解耦，形成可执行的两条闭环路径：

```text
Dimension Semantic
        ↓
Dimension Entity Resolution
        ↓
┌───────────────────────────┐
│ 是否存在可识别关联主表？   │
└─────────────┬─────────────┘
        YES   │   NO
          ↓   │   ↓
     MasterJoin   DirectKey
          ↓   │   ↓
         JOIN │ GROUP BY
          └───┬───┘
              ↓
        Dimension Binding
              ↓
          QueryPlan
              ↓
         SQL Builder
              ↓
         SQL Runtime
```

规则：

1. Dimension 不要求必须存在独立主表；
2. 如果存在可靠关联主表，使用 `MasterJoin`；
3. 如果不存在关联主表，允许使用事实表中的 `Dimension Entity Key / Business Key` 直接分组汇总；
4. 不得把事实明细表自身主键错误当作 Dimension Key；
5. Dimension Entity Key 优先是能够稳定标识业务实体的 Key，例如 `material_id`、`supplier_id` 等；
6. MasterJoin 与 DirectKey 都必须形成统一 `DimensionResolution`，进入 QueryPlan；
7. 最终必须闭环到 SQL Builder 与真实 SQL Runtime；
8. 不允许硬编码“供应商表”“物料表”等业务答案；必须以当前 Metadata 事实为依据；
9. Ambiguous / NotResolved 仍必须安全阻断；
10. 2.7 的验收不以“是否存在主表”为单一标准，而以“是否形成可执行 Dimension Entity Binding”为标准。

### 2.7 预定完整开发任务

```text
2.7.01 Phase入口全量源码 / 文档 / Runtime / Golden 审计
2.7.02 Dimension Entity Key Resolver
2.7.03 Dimension Master Table / Relation Detection
2.7.04 MasterJoin / DirectKey Resolution Contract
2.7.05 Dimension Resolution 与 Metric / Table / Join Context 绑定
2.7.06 QueryPlan Dimension Binding
2.7.07 MasterJoin QueryPlan
2.7.08 DirectKey QueryPlan
2.7.09 SQL Builder MasterJoin 路径
2.7.10 SQL Builder DirectKey 路径
2.7.11 MasterJoin Runtime
2.7.12 DirectKey Runtime
2.7.13 Ambiguous / NotResolved 安全回归
2.7.14 Golden Regression
2.7.15 Coverage / Quality Gate
2.7.16 Release Gate
2.7.17 Phase Exit Review
```

上述任务必须在 Phase 2.7 阶段开发测试计划中进一步逐项拆成开发步骤、测试步骤、完成条件、源码范围和 Runtime 地址；开始 2.7 执行前必须完成入口全量审计并校准该清单。

### 2.7 文档关联

- `Phase开发测试管理总则.md`：长期管理规则；
- `Phase2.7-DimensionAware QueryPlan开发测试计划.md`：Phase 2.7 完整开发 / 测试契约；
- `Phase2.7-Runtime分步测试记录.md`：Phase 2.7 逐 STEP 原始 Runtime 记录；
- `Phase2.7-阶段入口登记.md`：阶段入口与恢复锚点。

---

# 十九、当前项目进度（主计划当前状态区）

> 本章是**唯一当前进度区**。完成任何 STEP 后只更新本章和对应 Phase 文档，不在主计划复制历史过程。

```text
当前 Phase：Phase 2.6 — Query Evaluation Framework
当前 C / 工作单元：V2.6 Ranking Contract / Coverage 一致性审计
当前 STEP：Phase 2.6 Exit 校准 / Ranking Contract 审计
当前状态：IN_PROGRESS
当前 master：以 GitHub master 最新 Commit 为准
当前阻塞：Golden Coverage 旧记录与当前 Golden Ground Truth 存在状态漂移，必须 Runtime 重新确认；Dimension Resolution 相关 Case 需要结合真实 Metadata 能力重新定义执行路径
下一步：完成 2.6 当前 Exit 审计 → 更新 2.6 Runtime / Coverage 证据 → 确认 Exit → 执行 Phase 2.7 入口全量审计 → 冻结 2.7 全部开发任务 → 从 STEP-01 开始
```

### 当前必须保留的恢复信息

```text
C.13.3-LR：13/13 已完成，不重复执行
Phase 2.6 测试 API 存在性：已审计全部存在
Phase 2.6：仍未 COMPLETE
Phase 2.7：仅 PLANNED，不因已建立文档而视为 COMPLETE
```

---

# 二十、Phase 文档索引

| 文档 | 职责 | 状态 |
|---|---|---|
| `SuperBuilder AI Native BI Phase开发计划-V2.0.md` | **主开发计划 / 当前进度唯一总览 / Phase历史计划索引** | 当前主计划 |
| `Phase开发测试管理总则.md` | 历史管理规则独立备份；新规则以本主计划为准 | 已建立 |
| `Phase2.6-Runtime分步测试记录.md` | Phase 2.6 Runtime 逐步测试证据 | 已建立 |
| `Phase2.7-DimensionAware QueryPlan开发测试计划.md` | Phase 2.7 完整开发测试契约 | PLANNED |
| `Phase2.7-Runtime分步测试记录.md` | Phase 2.7 逐 STEP Runtime 证据 | PLANNED |
| `Phase2.7-阶段入口登记.md` | Phase 2.7 入口 / 恢复信息 | PLANNED |

> `Phase开发测试管理总则.md` 保留为独立备份，但**本主计划中的“Phase 生命周期与文档管理规则”为项目当前执行规则的合并版本**。后续若两者出现冲突，以本主计划最新版本为准，并应同步更新备份规则文档。

---

# 二十一、最终开发原则

1. `master` 是唯一源码基线。
2. 不创建新的开发分支。
3. 正式修改直接提交 `master`。
4. GitHub 正式描述、审计说明、Commit 描述使用中文。
5. **进入新 Phase 前先全量审计当前 master。**
6. **全量审计后一次性确定该 Phase 全部开发任务和全部测试任务。**
7. **阶段开发计划必须覆盖该 Phase 全部步骤，不能边做边临时决定主线任务。**
8. **每个 STEP 开始和完成必须同步更新主开发计划、阶段开发计划和 Runtime 记录。**
9. 主开发计划仅记录当前进度；完整技术方案、完整测试步骤和历史证据保留在 Phase 文档。
10. 完整相关源码审计完成前不得修改源码。
11. 先建立字段生命周期和调用链，再确定修改面。
12. 一次性确定修改面后统一修改，避免连续返工。
13. 修改后重新读取全部受影响文件。
14. AI 生成代码必须经过完整源码、DI、Build、Runtime、Regression 验证。
15. 编译通过不等于功能完成。
16. Runtime / Regression 未验证不得宣布 COMPLETE。
17. Golden Dataset 是 Evaluation 事实基础。
18. Evaluation 必须保持唯一 Contract。
19. 已冻结 Phase 不无边界重新开发。
20. 不创建独立 Test Project，优先通过现有 Controller / Runtime 验证。
21. 不允许通过 Coverage Gate、Coverage Analyzer 或伪造 Golden Case 掩盖真实能力缺失。
22. 每次修改必须明确 Phase / C / STEP。
23. 发现审计边界遗漏必须回溯审计，而不是继续“发现一个改一个”。
24. Constructor Dependency 必须与 DI Registration 闭环。
25. Contract 类型变化必须检查所有 Caller / Callee。
26. Namespace、Interface、Implementation、DTO、Model、Resolution 以 master 实际源码为准。
27. 阶段验收必须同时证明源码、Contract、DI、Runtime、Golden Dataset 与开发计划一致。
28. 每次代码修改推送 master 后必须本地 `git pull`，再 Build、Controller / Action Runtime、Golden Regression。
29. 测试地址必须先完成 Route → Controller → Action 存在性审计；不存在时先补齐接口并更新阶段计划。
30. 每次形成阶段性最终结论后立即更新文档，使开发计划成为可靠的会话恢复锚点。

---

# 二十二、版本更新记录

## V2.9

本次更新：

1. 将原有长期 Phase 管理规则与主开发计划合并；
2. 明确“进入新 Phase 必须先全量审计当前 master”；
3. 明确“全量审计后确定本阶段全部开发 / 测试任务”；
4. 明确阶段开发计划是完整执行契约；
5. 明确每个 STEP 开始 / 完成必须同步更新主计划与阶段计划；
6. 明确主计划仅记录当前进度；
7. 保留并整合 Phase 0～2.6 历史计划、C.13.1～C.14、Ranking Contract、Golden、Runtime API 审计等既有内容；
8. 保留 Phase 2.7 Dimension-Aware QueryPlan & SQL Closure 的已确定方向；
9. 增加 Phase 2.7 完整任务入口清单，但在 2.6 Exit 前保持 PLANNED；
10. 增加会话恢复和防止重复工作的正式规则。

> 本版本的目的不是重新定义历史计划，而是将既有计划内容与新的阶段开发测试管理规则统一到一个可恢复、可执行的主基线中。
