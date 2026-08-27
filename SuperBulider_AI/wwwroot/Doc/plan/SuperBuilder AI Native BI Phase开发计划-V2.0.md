# SuperBuilder AI Native BI Phase开发计划

> 文档版本：v2.22  
> 文档性质：项目正式开发基线 + Phase 开发测试管理总计划  
> **唯一源码基线：GitHub `master`**

## 一、当前项目状态

- 当前 Phase：**Phase 2.7 — DimensionAware QueryPlan**
- Phase 状态：**IN_PROGRESS（功能达标，待提交 Freeze）**
- 当前工作单元：**D14 — Runtime Contract Verification / Golden Regression Root Cause Audit**
- 当前状态：**D14-05 Golden Regression PASS（D18 轮：18/18 全 PASS，failedGates=[]）；本地 Build PASS + Startup PASS；修复链在本地工作区，尚未提交 master，尚未 Freeze**
- D05-D12：全部 FROZEN
- D13：Build PASS + Startup PASS，已 FROZEN
- 当前 master：`252cf2b`（D15-D18 修复链位于本地工作区未提交，详见《D18状态同步-20260827.md》）

### D14 当前 Runtime 证据（D18 轮，2026-08-27）

```text
Build                  PASS
Startup                PASS
Golden Dataset Loaded  PASS (18/18)
All Cases Executed     PASS (18/18)
Overall Pass Rate      100%    ✅ ≥ 90%
Positive Pass Rate     100%    ✅ ≥ 95%
Negative Detection     100%    ✅
Ambiguous Detection    100%    ✅
Unresolved Detection   100%    ✅
Release Gate           PASS（failedGates=[]）
D14 Freeze             待提交 master 后 Freeze
```

**重要：D15-D18 修复链消除全部 Positive 失败，评分器断言未放宽（negative 5/5 保持）。修复以 Resolution 权威绑定为模式：LLM intent 的聚合/漏产出漂移由 QueryPlanBuilder 确定性规则覆盖，详见《D18状态同步-20260827.md》。**

## 二、Phase 2.7 当前冻结链

```text
D05 Entity Key Contract              FROZEN
        ↓
D06 Relation Evidence                FROZEN
        ↓
D07 Dynamic Dimension Resolution     FROZEN
        ↓
D08 QueryPlan Dimension Binding      FROZEN
        ↓
D09 MasterJoin QueryPlan             FROZEN
        ↓
D10 DirectKey QueryPlan              FROZEN
        ↓
D11 SQL Builder 双路径              FROZEN
        ↓
D12 Contract / DI / Namespace       FROZEN
        ↓
D13 Release Build / 实现前基线       FROZEN
        ↓
D14 Runtime Contract Verification   GOLDEN PASS (D18) / 待提交 Freeze
```

## 三、D13 冻结结论

用户已确认当前 `master` 对应本地代码完成同步后：

- 编译通过；
- 应用正常启动。

因此 D13 的目标——确认首轮实现前当前 master 具备稳定的 Build / Startup 基线——已通过并冻结。

**D13 = FROZEN。**

本 STEP 未修改业务源码。D13 冻结只代表 Build / Startup 基线通过，不代表 D07-D12 的功能 Contract 已实现。

## 四、D14 首轮源码实现边界

D14 是 Phase 2.7 首次进入业务源码实现的 STEP。实现必须严格消费 D09-D12 已冻结的范围；若发现冻结 Contract 与当前产品多数据库运行要求存在冲突，必须先记录并更新计划，再实施最小必要 Contract 修正，不得通过业务硬编码绕过。

```text
Models / Contract
 ↓
Interfaces
 ↓
Resolution / Factory
 ↓
QueryPlan Dimension Binding
 ↓
QueryPlanBuilder 旧 JOIN 旁路隔离
 ↓
SqlQueryBuilder 双路径
 ↓
Validator
 ↓
Program.cs DI
 ↓
Build
 ↓
Controller Runtime
 ↓
Golden Regression
```

首轮实现目标：建立唯一 Resolution 真相源，让 Dimension 能在**当前 Metadata Snapshot**下选择：

```text
MasterJoin
DirectKey
Ambiguous
NotResolved
```

并保证：

- **当前 Metadata Snapshot 中存在对应稳定主表/关联 Relation Evidence：走 MasterJoin。**
- **当前 Metadata Snapshot 中不存在对应主表：不 Join，以事实表关联 ID / Key 汇总（DirectKey）。**
- MasterJoin 才产生 `QueryPlan.Joins`；
- DirectKey 永不产生 `QueryJoin`；
- Builder 不重新 Semantic Search / Relation Inference；
- SQL Builder 只消费 QueryPlan；
- Validator 只验证，不推理；
- 不硬编码物料、供应商、客户等业务主表、字段或固定关系。

## 五、D14-1 ～ D14-5 当前实现进度

### D14-1 — Dimension Resolution Evidence Contract / Provider
已完成首轮落地：建立 `DimensionResolutionEvidence` 及对应 BI Interface / Service，明确 `MasterJoin / DirectKey / Ambiguous / NotResolved` 四态 Resolution 与 ExecutionCapability 分离表达。

### D14-2 — Semantic Applicability 接入 Evidence
已完成首轮落地：`SemanticApplicabilityEvaluator` 已接入 `IDimensionResolutionEvidenceService`，Dimension Resolution 开始消费当前 Metadata Snapshot 的物理 Evidence，而不是仅依赖普通 Semantic Candidate。

### D14-3 — DI / Namespace / Contract 对齐
已完成首轮落地：Dimension Evidence Service 已注册到 DI，并统一到 `Interfaces.BI` Contract，清理重复旧接口路径。

### D14-4 — Resolution → QueryPlan Contract 传递
已完成首轮落地：`MasterJoin / DirectKey` Resolution 及 Master Binding 已传递至 QueryPlan Semantic Resolution / Dimension Binding。

### D14-5 — QueryPlan Dimension 执行绑定
已完成首轮落地：QueryPlan Builder 已能够消费已确认 Dimension Resolution；MasterJoin 生成 `QueryPlan.Joins`，DirectKey 不生成 Join，并保留既有 Ranking / Order Contract 边界。

**D14-1～D14-5：源码首轮实现完成；功能验收尚未完成。**

## 六、D14 多数据库 Metadata 规则（最高优先级，必须遵守）

> **多数据库不是预绑定业务关系；Resolution 必须基于当前用户已绑定 DataSource 的联合 Metadata Snapshot 动态决定。**

### 核心业务规则

```text
当前 Metadata Snapshot
        ↓
查找 Dimension 对应的稳定 Relation / Master
        │
        ├── 存在对应稳定主表 / Relation Evidence
        │       ↓
        │    MasterJoin
        │       ↓
        │    QueryPlan.Joins
        │
        └── 不存在对应主表
                ↓
             DirectKey
                ↓
          不产生 QueryJoin
                ↓
       仅以事实表关联 ID / Key 汇总
```

### 多数据库动态规则

```text
用户绑定数据库 A
        ↓
Metadata Snapshot = A
        ↓
A 有 Fact Key，但没有对应 Master
        ↓
DirectKey / ID 汇总

用户再绑定数据库 B
        ↓
重新扫描并生成联合 Metadata Snapshot
        ↓
A + B 中出现稳定 Relation / Master Evidence
        ↓
重新 Resolution
        ↓
MasterJoin
```

### 强制规则

1. **Metadata Snapshot 中存在对应稳定主表/Relation：允许并优先走 MasterJoin。**
2. **Metadata Snapshot 中不存在对应主表：不得因为缺少主表而 BLOCK；若 Fact Key Evidence 稳定，必须走 DirectKey / 关联 ID 汇总。**
3. 不允许因为“物料”“供应商”等业务词语而硬编码主表或字段。
4. 不允许把某次 `NotResolved` / `DirectKey` 固化成永久事实。
5. 新 DataSource 加入后必须重新生成联合 Metadata Snapshot，并重新 Resolution。
6. 新 Snapshot 出现稳定 Master Relation 时，允许 `DirectKey → MasterJoin`。
7. Relation Evidence 消失后，必须重新 Resolution 并安全降级到 DirectKey（若 Fact Key 仍稳定）。
8. 所有 Dimension / Master / Relation GQ-xx 均遵循同一规则。
9. 多数据库的可见范围由用户当前已绑定 DataSource 决定。
10. 未绑定数据库不属于当前 Snapshot，不能作为 Resolution 目标。
11. **跨 DataSource 本身不是失败理由；是否 JOIN 由当前 Metadata Snapshot 中是否存在稳定 Relation、以及当前 SQL Runtime 是否具备该 Relation 的实际执行能力共同决定。若当前 Runtime 不具备跨源 JOIN，则必须安全降级为 DirectKey，而不是猜测或 BLOCK。**
12. **“没有主表”与“语义没有解析到 Fact Key”必须严格区分：前者允许 DirectKey，后者才允许 NotResolved / BLOCK。**

## 七、D14-05 当前 Golden Regression 最终证据

当前 Runtime 接口返回：

```text
Dataset       = query-plan-golden
Version       = 1.3
Total         = 18
Executed      = 18
Passed       = 11
Failed       = 7
Overall       = 61.11%
Positive      = 36.36%
Negative      = 100%
Ambiguous     = 100%
Unresolved    = 100%
Decision      = BLOCK
```

### 7 个 Positive Failure

| Case | 当前结果 | 当前根因簇 | 当前处理 |
|---|---|---|---|
| GQ-002 | Ambiguous | EntityCount Candidate / Ambiguity | 待源码审计，不降 Gate |
| GQ-005 | NotResolved | **Multi-DB / Optional Master / DirectKey Fallback** | **最高优先级审计** |
| GQ-006 | QueryPlan Evaluation FAIL | Ranking / QueryPlan Evaluation | 待源码审计 |
| GQ-007 | NotResolved | Multi-Metric Semantic Evidence | 待源码审计 |
| GQ-008 | Ambiguous | EntityCount Candidate / Ambiguity | 待源码审计，不降 Gate |
| GQ-009 | NotResolved | **Multi-DB / Optional Master / DirectKey Fallback** | **最高优先级审计** |
| GQ-010 | QueryPlan Evaluation FAIL | Ranking / QueryPlan Evaluation | 待源码审计 |

### 当前关键判断

- GQ-002 / GQ-008：不能通过简单降低 Ambiguous Threshold 来换取 Positive PASS；当前 Ambiguous Detection = 100%，必须保持 Negative / Ambiguous Safety。
- GQ-005 / GQ-009：**不能直接认定为“Supplier Master 缺失”或“Supplier Metadata 错误”**。必须先确认当前 Snapshot 是否存在对应 Master、是否存在稳定 Fact Key、是否应走 MasterJoin 或 DirectKey。
- GQ-006 / GQ-010：Applicability 已 Resolved，失败发生在 QueryPlan Evaluation / Decision Gate，不得通过修改 Dimension Resolution 掩盖。
- GQ-007：多 Metric 中至少一个 Metric 未形成直接 Semantic Evidence，必须审计 Metric-by-Metric Resolution，不得直接修改 Factory。

## 八、D14 当前源码审计发现的关键剩余问题

### 1. Dimension Resolution 必须继续验证“Master Optional / DirectKey Fallback”闭环
当前 `SemanticApplicabilityEvaluator.ResolveDimensionAsync` 在 Evidence 返回后要求 `ExecutionCapability == Executable`，并对 `Ambiguous / NotResolved` 直接返回失败；因此必须继续确认 `DimensionResolutionEvidenceService` 在“没有 Master”时是否已经正确产生 `DirectKey + Executable`，以及在跨 DataSource / 当前 Runtime 不可 JOIN 时是否安全降级到 DirectKey。

### 2. Dimension Evidence 与 Semantic Applicability 的职责必须继续保持单向
`ResolveDimensionAsync` 当前在 Evidence 已确认后再次执行 Dimension Semantic Search，只用于补充 `BusinessMeaning`。这不允许重新决定物理绑定；后续修改只能移除职责漂移，不得让二次 Search 成为新的 Resolution 真相源。

### 3. QueryPlan Builder 必须保持 Resolution 唯一真相源
当前 QueryPlan Builder 已消费 `MasterJoin / DirectKey`，但必须继续审计其对 DataSource / Master Binding 的执行限制，确保不会把合法 DirectKey 或可执行 MasterJoin 错误 BLOCK。

### 4. SQL Builder 必须严格消费 QueryPlan
SQL Builder 不得重新 Semantic Search / Relation Inference；Join 是否存在只由 QueryPlan.Joins 决定。DirectKey 必须保持无 Join。

## 九、D14 当前禁止直接修改的内容

在完成全量根因审计前，禁止直接修改：

- `DimensionResolutionEvidenceService` 的阈值 / Candidate Ranking；
- `SemanticApplicabilityEvaluator` 的 Ambiguity Gate；
- `QueryPlanSemanticResolutionFactory` 的 Contract；
- QueryPlan Evaluator / Confidence Gate；
- Golden Dataset Expected；
- Regression Coverage / Release Gate；
- GQ-011 Ranking Contract；
- 任何业务主表/字段硬编码。

**原因：当前 7 个失败已经被分成 4 个根因簇，必须先确认每个簇的源码根因，再形成一次性最小修改范围。**

## 十、D14 下一轮审计顺序

```text
D14-05 Golden Regression BLOCK
        ↓
① Multi-Database / Optional Master Contract
        ↓
② GQ-005 / GQ-009 DirectKey / MasterJoin 根因
        ↓
③ QueryPlan Builder DataSource / Join Binding
        ↓
④ GQ-006 / GQ-010 Ranking Evaluation
        ↓
⑤ GQ-002 / GQ-008 EntityCount Candidate Contract
        ↓
⑥ GQ-007 Multi-Metric Resolution
        ↓
⑦ 全量兼容性矩阵
        ↓
⑧ 一次性最小修改方案
        ↓
⑨ Build
        ↓
⑩ Controller / Action Runtime
        ↓
⑪ Full Golden Regression
        ↓
⑫ D14 Freeze Gate
```

## 十一、D14 兼容性 Gate

| Case / Contract | D14 强制要求 |
|---|---|
| GQ-001 | 原 PASS 必须保持 |
| GQ-002 | EntityCount Contract 不变；不得为了 Positive PASS 破坏 Ambiguous Safety |
| GQ-003 | 当前 PASS 必须保持 |
| GQ-004 | 当前 PASS 必须保持 |
| GQ-005 | 有稳定 Master → MasterJoin；无 Master → DirectKey / ID 汇总；不得因无 Master 直接 BLOCK |
| GQ-006 | Ranking Contract 必须正确执行；DirectKey / MasterJoin 不得破坏 Ranking |
| GQ-007 | 多 Metric 必须逐项形成稳定 Resolution |
| GQ-008 | EntityCount Ambiguous Safety 必须保持 |
| GQ-009 | 同 GQ-005，必须验证多数据库 / Optional Master 行为 |
| GQ-010 | Ranking + Filter 必须正确执行；Dimension Resolution 不得越权改变 Ranking Contract |
| **GQ-011** | **继续 PASS，完全绕过 Dimension Resolution** |
| MasterJoin | 仅由当前 Snapshot 已确认稳定 Relation 产生 QueryPlan.Joins |
| DirectKey | 不产生 Joins；按 Fact Association Key / ID 汇总 |
| Ambiguous | BLOCK / REVIEW，不猜测 |
| NotResolved | 仅在无法形成稳定 Fact Key / Metric / Relation Resolution 时 BLOCK；“无主表”本身不等于 NotResolved |
| Snapshot Refresh | 新增/删除 DataSource 后重新 Resolution，不复用旧结论 |

任一既有 PASS Case 出现回归，立即停止当前 D14 验证，先定位并修复兼容性问题。

## 十二、D14 Freeze Gate

D14 **不得**因为 Build / Startup PASS 而提前冻结。

必须同时满足：

```text
Build PASS
+
Startup PASS
+
Golden Dataset 18/18 Executed
+
OverallPassRate >= 90%
+
PositivePassRate >= 95%
+
NegativeDetectionRate = 100%
+
AmbiguousDetectionRate = 100%
+
UnresolvedDetectionRate = 100%
+
GQ-011 无回归
+
Multi-DB Optional Master Contract PASS
+
MasterJoin / DirectKey Runtime PASS
```

全部满足后才允许：

```text
D14-05 PASS
   ↓
D14 FREEZE
   ↓
更新阶段计划
   ↓
同步主计划
   ↓
进入 D14-06
```

## 十三、项目强制开发规则

1. GitHub `master` 是唯一源码基线。
2. 正式源码与正式计划直接更新 `master`，使用中文 Commit 描述。
3. 不新建独立 Test Project；优先使用 Controller / Action Runtime 真实验证。
4. 代码文件存在不等于功能完成，必须检查 Interface、Implementation、Caller、Model/DTO、Constructor、DI、Runtime、Controller、Golden Regression。
5. AI 生成或修改的代码与人工代码采用相同审计、Build、Runtime、Regression 标准。
6. **禁止边审边改；每个 STEP 必须先完成全量源码 / Contract 审计，再形成一次性修改范围。**
7. **每个 STEP 冻结后必须先更新阶段开发计划，再同步主计划，再确认 GitHub `master`，然后才能进入下一 STEP。**
8. 阶段计划任何实质性变化必须同步主计划及受影响 Runtime 记录。
9. 代码修改推送 `master` 后，必须由本地环境先 `git pull`，再 Build，再执行 Controller / Action Runtime 测试；本地结果作为正式验收证据。
10. 未完成 Runtime / Regression / Exit Criteria，不得宣布 Phase COMPLETE。
11. 禁止通过修改 Golden、Evaluator、Coverage、Gate 或删除功能掩盖真实能力缺失。
12. **开发计划必须记录每次 Runtime Regression 的实际结果、失败 Case、根因簇、禁止修改范围与下一轮审计顺序，避免后续重新读取计划时丢失上下文。**
13. **多数据库 / Optional Master Contract 属于 Phase 2.7 D14 的一级冻结约束，不得在后续审计中退化成“单数据库 JOIN”模型。**

## 十四、D14 全量源码逐行审计标准（强制，最高优先级）

为避免出现“先根据局部源码作出结论，后续重新读取完整源码又纠正”的误判，从本版本起，所有 D14 及后续全量源码审计必须执行**逐行审计（Line-by-Line Audit）**。本条属于正式开发基线，不是建议项。

### 14.1 审计前置条件

1. 必须锁定本次审计的 Git commit SHA；审计过程中不得混用不同 commit 的源码结论。
2. 必须读取目标文件的**完整源码**，不得仅依赖搜索摘要、函数片段、目录结构或历史对话结论。
3. 对跨文件 Contract，必须同时读取：Interface、Implementation、DTO/Model、Caller、Factory、DI、Builder、Evaluator、Controller/Action，以及相关 Golden / Runtime Contract。
4. 如果文件过长，必须分段读取直至覆盖**全部行**；不得以“已读取关键函数”为理由视为全量审计完成。
5. 必须记录每个被审计文件的实际范围：文件路径、commit SHA、起止行号、总行数、已审计行数；未覆盖的行数必须为 0。

### 14.2 逐行审计要求

每一行必须至少归入以下一种审计结论：

```text
Contract / Model
Interface / Dependency
Input / Validation
Semantic Resolution
Candidate / Ranking
State Transition
DataSource / Multi-DB
QueryPlan Binding
Join / DirectKey
Evaluation / Confidence
Error / Fallback
DI / Runtime
Dead / Legacy / Compatibility
```

对以下代码尤其禁止跳读：

- `if / else`
- `switch`
- `return`
- `null` / `NotResolved` / `Ambiguous`
- `ExecutionCapability`
- `DataSourceId`
- `MasterJoin / DirectKey`
- Candidate Score / Threshold
- Exception / Fallback
- Factory / DI
- Builder / Evaluator

### 14.3 跨文件调用链必须闭环

逐行审计不能停留在单文件。必须形成：

```text
Input
 ↓
Controller / Action
 ↓
Service
 ↓
Interface
 ↓
Resolution / Factory
 ↓
QueryPlan
 ↓
Evaluator
 ↓
Confidence / Decision Gate
 ↓
SQL Builder
 ↓
Runtime
```

任何一个环节没有实际源码证据，都只能标记为 **UNVERIFIED**，不得写成 PASS 或根因结论。

### 14.4 根因结论分级

审计结论必须区分：

```text
CONFIRMED
    = 已读取完整相关源码并能由明确代码路径证明

PROBABLE
    = 已有强证据，但仍缺少至少一个关键调用链/数据结构的完整源码验证

HYPOTHESIS
    = 仅基于现象或局部代码推测

UNVERIFIED
    = 尚未取得完整源码证据
```

**只有 CONFIRMED 才允许进入最终一次性修改清单。**

### 14.5 Golden Case 必须逐 Case 追踪

每个失败 Case 必须建立：

```text
Golden Input
 ↓
Expected Contract
 ↓
实际 Resolution
 ↓
实际 QueryPlan
 ↓
实际 Evaluation
 ↓
实际 Confidence
 ↓
实际 Decision
 ↓
最终 Runtime 输出
```

必须逐字段比较，不得仅根据 `reason` 文本推断根因。

### 14.6 修改前强制复核

形成最终修改清单前必须执行第二次复核：

1. 重新读取所有拟修改文件完整源码；
2. 重新读取所有禁止修改文件中与该 Contract 相邻的实现；
3. 验证修改不会改变已有 PASS Case；
4. 验证修改不会削弱 Negative / Ambiguous / Unresolved Detection；
5. 验证修改文件不是由于之前的误判而被列入；
6. 如果第二次完整读取推翻第一次结论，必须以第二次完整源码证据为准，并将第一次结论标记为 **RETRACTED**，不得直接实施旧方案。

### 14.7 修改后再次全量审计

源码修改完成后，在 Build 前必须重新逐行审计所有修改文件；Build 通过后还必须执行 Runtime + Golden Regression。

因此完整闭环固定为：

```text
锁定 Commit
 ↓
完整源码读取
 ↓
逐行审计（0 行遗漏）
 ↓
跨文件调用链闭环
 ↓
Golden Case 逐 Case 对照
 ↓
根因分级
 ↓
二次完整源码复核
 ↓
一次性修改
 ↓
修改后逐行复审
 ↓
Build
 ↓
Startup
 ↓
Controller / Action Runtime
 ↓
Golden Regression
 ↓
兼容性矩阵
 ↓
Freeze / BLOCK
```

### 14.8 严禁的审计方式

以下方式不得作为“全量源码审计”结论依据：

- 只读取函数名或搜索摘要；
- 只读取目录结构；
- 只读取疑似相关的 20～50 行；
- 根据上一轮对话记忆代替当前 commit 源码；
- 根据 Runtime `reason` 直接反推源码根因；
- 看到一个 `if` 就假设整个 Service 的行为；
- 未读取 Caller / Factory / DI 就修改 Service；
- 未读取 Builder / Evaluator 就修改 Resolution；
- 未读取 Golden Contract 就修改 Ranking；
- 先修改后补审计。

### 14.9 审计交付物强制格式

每次 D14 全量审计最终必须输出：

```text
D14-X 全量源码审计结论

1. 审计 Commit SHA
2. 全量文件清单
3. 每个文件总行数 / 已审计行数
4. 跨文件调用链
5. Golden Case 逐 Case 结果
6. CONFIRMED 根因
7. PROBABLE / HYPOTHESIS / UNVERIFIED（如有）
8. 唯一修改文件清单
9. 每个文件修改原因
10. 明确禁止修改文件
11. 兼容性矩阵
12. 修改前二次复核结果
13. 修改后逐行复审结果
14. Build / Startup / Runtime / Golden Regression 证据
15. Freeze / BLOCK 最终判定
```

**本标准立即生效。后续任何重新读取开发计划的 AI / 开发人员必须把“逐行、全文件、跨文件、逐 Case、二次复核”视为 D14 全量源码审计的最低验收标准。**

## 十五、长期产品目标

SuperBuilder 最终建设为：

> **支持多语言、多租户、多数据库动态接入的 AI Native Low-code Platform。**

业务数据库关系不得预绑定为永久事实。Metadata Relation 必须基于当前 Snapshot 动态解析；新增数据库、表、字段或 Relation Evidence 后允许重新解析并升级为 MasterJoin；关系证据失效后允许重新计算并安全降级。

```text
DataSource 添加
 ↓
数据库扫描
 ↓
Metadata Schema
 ↓
字段 + 备注
 ↓
AI Semantic Generation
 ↓
Semantic / SearchText
 ↓
Embedding
 ↓
Vector Database
 ↓
当前 Metadata Snapshot
 ↓
Dynamic Resolution
```

## 十六、当前下一步

**当前仍不因 Build / Startup PASS 而冻结 D14。**

D14-05 Golden Regression 当前仍为 BLOCK；在任何源码修改进入 `master` 前，必须按照第十四章新增的“全量源码逐行审计标准”重新锁定 commit 并完成全文件、跨文件、逐 Case 审计及二次复核。

后续实施固定顺序：

```text
D14-05 Golden Regression BLOCK
        ↓
逐行全量源码审计
        ↓
R1 EntityCount
R2 Multi-Database / Optional Master
R3 QueryPlan Evaluation / Ranking
R4 Multi-Metric Resolution
        ↓
CONFIRMED 根因闭合
        ↓
一次性最小修改
        ↓
修改后逐行复审
        ↓
Build
        ↓
本地 Pull
        ↓
Startup
        ↓
Controller / Action Runtime
        ↓
Full Golden Regression
        ↓
D14 Freeze Gate
```

**D14 当前不得宣布完成或冻结。**

源码首轮实现已落地；Build / Startup 已通过；Golden Regression 当前 BLOCK；7 个 Positive Failure 尚未完成根因修复与回归验证。