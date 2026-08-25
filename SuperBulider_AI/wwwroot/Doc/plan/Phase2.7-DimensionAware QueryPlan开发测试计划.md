# Phase 2.7 — DimensionAware QueryPlan 开发测试计划

> 所属项目：SuperBuilder AI Native BI  
> 唯一源码基线：GitHub `master`  
> 主开发计划：`SuperBuilder AI Native BI Phase开发计划-V2.0.md`  
> 管理总则：`Phase开发测试管理总则.md`  
> Runtime 记录：`Phase2.7-Runtime分步测试记录.md`  
> 状态：IN_PROGRESS

## 一、阶段入口与切换依据

Phase 2.7 进入执行前，必须完成当前 `master` 全量源码、调用链、Golden、Runtime、Build、Gate 与文档审计。审计结论必须先写入本计划，再开始编码。

### 强制步骤切换规则（2026-08-25）

所有 Dxx 开发/审计步骤必须严格执行：

```text
当前 STEP
↓
完整源码 / Contract / Runtime 审计
↓
最终结论
↓
冻结当前 STEP
↓
【必须先更新 GitHub master 开发计划】
↓
确认开发计划已记录：状态 / 结论 / 修改范围 / 兼容性约束 / 下一 STEP
↓
才能进入下一 STEP
```

**冻结但未更新开发计划，不视为正式完成；未确认 master 中已记录冻结结果，不得开始下一 STEP。**

每一步必须记录：
1. 最终审计结论；
2. 最终允许修改范围；
3. 明确禁止修改范围；
4. 对既有 PASS GQ-*** 的兼容性约束；
5. Runtime / Build / Gate 结果；
6. 下一 STEP 及其入口条件。

发现兼容性回归时必须停止后续步骤，重新进行源码根因审计，不允许为了修复单一 GQ-*** 破坏其他已正确 Case。

不得边审边改造成责任范围漂移；必须先完成完整责任链审计、冻结修改范围，再编码。

## 二、阶段目标

将 Phase 2.6 Semantic Applicability 能力转化为可执行 DimensionAware QueryPlan，并形成 SQL 闭环。

## 三、阶段核心规则

1. Dimension 是业务语义，不等价于主表。
2. 优先寻找稳定 Entity Key / Business Key，不得把事实明细表自身主键误认为 Dimension Key。
3. 存在可由真实 Metadata Relation / FK / BusinessKey 证明的稳定关联主表 → `MasterJoin`。
4. 不存在关联主表时，不得直接 `NotResolved`；必须继续检查事实表是否存在稳定 Dimension Key / Code / Name → `DirectKey`。
5. MasterJoin 与 DirectKey 使用统一 `DimensionResolution` Contract。
6. 两者都无法稳定绑定 → `NotResolved`；多候选无法消歧 → `Ambiguous`；均安全 BLOCK/REVIEW。
7. 禁止为 GQ-006、GQ-010 或任何 Golden Case 硬编码 material/supplier/customer 主表。
8. QueryPlan、SQL Builder、Runtime 不得自行重新进行无上下文 Semantic Search。
9. DirectKey 新能力不得改变既有 MasterJoin 正确行为。
10. 所有修改必须通过既有 PASS GQ-*** 兼容性回归。

## 四、D03 / STEP-03 最终审计结论（2026-08-25）

当前 master Metadata 已确认 `wms_storage_receipt_info` 承载 `material_id`、`material_code`、`material_name` 与 `quantity`。因此 GQ-006、GQ-010 的物料 Dimension 具备事实表直接绑定条件。

当前 `QueryJoinInferenceService` 是 QueryPlan 层 JOIN 候选推断能力，不等同于真实 Metadata Relation/FK；`QueryJoin` 是执行层 QueryPlan Contract，也不能反向证明存在主表关系。

因此：

```text
GQ-006 / GQ-010
Dimension = 物料
↓
未证明稳定独立 Master Relation
↓
Fact 存在 material_id / material_code / material_name
↓
目标 ResolutionMode = DirectKey
```

D03 冻结：不修改 `QueryPlanEvaluator`、GQ-011 Ranking Order Binding、GQ-011 Golden、`QueryJoin` Model，不把 `QueryJoinInferenceService` 改造成 DirectKey Resolver，SQL Builder 不自行推理 Dimension。

兼容性要求：已有 MasterJoin 必须保持行为；DirectKey 仅覆盖无稳定 MasterJoin 但存在稳定 Fact Dimension Key/Code/Label 的场景；Ambiguous / NotResolved 继续安全阻断；GQ-011 与其他 PASS Case 必须保持 PASS。

## 五、D04 / STEP-04 最终冻结结论（2026-08-25）

D04 Contract / Model 设计完成并正式冻结。本结论是 D05 及后续实现的唯一 Contract 输入。

### 5.1 双层 Contract

```text
DimensionResolution
↓
QueryPlanDimensionResolution
↓
QueryPlan
```

`DimensionResolution` 负责语义 Resolution 决策；`QueryPlanDimensionResolution` 负责最终 QueryPlan 物理绑定。QueryPlan、SQL Builder、Runtime 不得重新进行无上下文 Dimension Search。

### 5.2 ResolutionState

`Resolved | Ambiguous | NotResolved`

### 5.3 ResolutionMode

`MasterJoin | DirectKey`

State 与 Mode 独立。合法语义包括：`Resolved + MasterJoin`、`Resolved + DirectKey`、`Ambiguous + null`、`NotResolved + null`。

### 5.4 DimensionResolution 字段

```text
SemanticText
ResolutionState
ResolutionMode
FactTable
FactKeyColumn          required
FactCodeColumn         optional
FactLabelColumn        optional
DimensionTable         optional for DirectKey
DimensionKeyColumn     required for MasterJoin
DimensionLabelColumn   optional
JoinRequired
JoinCondition
Confidence
Evidence
```

Evidence 至少覆盖 RelationEvidence、EntityKeyEvidence、FactKeyEvidence、LabelEvidence、CandidateCount、Score、Reason。

### 5.5 D04 修改边界

D04 本身不修改业务实现。后续实现允许新增/修改 Dimension Resolution Models、Interfaces、Services，但不得通过修改 `QueryPlanEvaluator`、`QueryPlanOrderResolution`/GQ-011 Ranking Order Binding、GQ-011 Golden、`QueryJoin` Model 或改变 `QueryJoinInferenceService` 职责来绕过 Contract。

QueryPlanBuilder Dimension Binding 在 D08 处理；SQL Builder MasterJoin / DirectKey 执行路径在 D11 处理。

### 5.6 D04 兼容性冻结

GQ-011 当前 PASS 必须保持 PASS；其他既有 PASS GQ-*** 必须保持行为兼容；MasterJoin 不得被 DirectKey 抢占；Ambiguous / NotResolved 必须继续安全阻断。发现兼容性回归立即停止后续 STEP，回到源码根因审计。

## 六、开发步骤

| STEP | 状态 | 内容 |
|---|---|---|
| D01 | COMPLETE | Phase 2.6 / master 基线审计 |
| D02 | COMPLETE | Metadata Dimension Entity Key 审计 |
| D03 | COMPLETE | Master Table / Relation 审计 |
| D04 | **COMPLETE / FROZEN** | Contract / Model 设计 |
| D05 | **CURRENT** | Dimension Entity Key Resolver 实现 |
| D06 | PLANNED | Master Table Detection |
| D07 | PLANNED | Dimension Resolution |
| D08 | PLANNED | QueryPlan Dimension Binding |
| D09 | PLANNED | MasterJoin QueryPlan |
| D10 | PLANNED | DirectKey QueryPlan |
| D11 | PLANNED | SQL Builder 双路径闭环 |
| D12 | PLANNED | Static Contract / DI / Namespace 审计 |
| D13 | PLANNED | Release Build |
| D14 | PLANNED | Controller / Runtime |
| D15 | PLANNED | MasterJoin Golden |
| D16 | PLANNED | DirectKey Golden |
| D17 | PLANNED | SameTable / CrossTable Golden |
| D18 | PLANNED | Ambiguous / NotResolved Safety Regression |
| D19 | PLANNED | Full Golden Regression |
| D20 | PLANNED | Coverage / Quality / Release Gate |
| D21 | PLANNED | Phase 2.7 Exit Review |

**D04 冻结后，已先完成本计划更新；只有本次 master 更新成功后，D05 才允许开始。**

## 七、Runtime 对应

D01→STEP-01；D02→STEP-02；D03→STEP-03；D04→STEP-04；D05-D07→STEP-05~06；D08-D10→STEP-07~08；D11→STEP-09~10；D12-D14→STEP-11~12；D15-D18→STEP-13~15；D19→STEP-16；D20→STEP-17~18；D21→STEP-19。

Runtime 结果也必须执行“冻结 → 更新 Runtime/开发计划 → 确认 master → 下一 STEP”闭环。

## 八、兼容性验收

任何新增 DirectKey 能力必须验证：既有正确 MasterJoin 不变；GQ-011 Ranking 不变；既有 PASS GQ-*** 不回归；Ambiguous / NotResolved 仍安全阻断。出现兼容性问题立即停止并回到根因审计。

## 九、Exit Criteria

D01-D21 全部完成；MasterJoin、DirectKey、SameTable/CrossTable、Ambiguous/NotResolved、Golden Regression、Coverage、Quality、Release Gate 全部达到正式要求；Runtime、Commit、主计划、本计划均同步；每个 STEP 均完成“冻结 → 更新开发计划 → master 确认 → 下一 STEP”闭环。
