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

## 二、项目长期产品目标与 Phase 2.7 约束

SuperBuilder 的最终产品目标不是面向单一 WMS 数据库的固定 BI，而是建设：

> **支持多语言、多租户、多数据库动态接入的 AI Native Low-code Platform。**

Phase 2.7 的 DimensionAware QueryPlan 必须服务于这一长期目标，因此不得采用“提前绑定固定数据库关系”的架构假设。

### 2.1 多数据库动态 Metadata 原则

1. 业务数据库可以在系统运行生命周期内动态新增、删除或重新同步。
2. 系统不得预先写死 A 数据库与 B 数据库之间的业务关系。
3. Dimension Relation 必须基于**当前 Metadata Snapshot**动态解析，而不是永久绑定。
4. 当前无法 JOIN 不代表未来永久无法 JOIN；新增数据库、表、字段或 Relation Evidence 后必须允许重新解析。
5. 反之，原有 Relation Evidence 消失后，也必须允许 Resolution 降级并重新计算。
6. 不允许把某次 Runtime 的 `NotResolved` 或 `DirectKey` 结果固化为永久业务事实。
7. 新增数据库后，如果出现真实稳定的 Master Relation，应允许同一业务问题从 `DirectKey` 自动升级为 `MasterJoin`，无需修改业务代码或硬编码主表。

### 2.2 Dynamic Relation Resolution 原则

```text
当前 Metadata Snapshot
        ↓
Dimension Resolution
        ↓
┌─────────────────────┐
│ 有稳定 Master Relation │ → MasterJoin
└─────────────────────┘
        ↓ 否
┌─────────────────────┐
│ 有稳定 Fact Key/Code/Label │ → DirectKey
└─────────────────────┘
        ↓ 否
   NotResolved

多候选且无法消歧 → Ambiguous
```

其中 `MasterJoin` 优先于 `DirectKey`；`DirectKey` 是当前 Metadata Snapshot 下的可执行路径，不是永久降级状态。

### 2.3 多语言 / 多租户 / 多数据库兼容要求

Phase 2.7 当前不一次性实现全部平台能力，但所有新 Contract 必须避免阻塞后续：

- 多语言：Dimension、Metric、业务语义不能把单一中文文本作为永久物理身份。
- 多租户：Resolution 不得依赖跨租户共享的全局业务关系；Metadata 与 Resolution 必须保留租户边界。
- 多数据库：DataSource 必须作为物理绑定的重要边界；不得默认所有表属于同一数据库。
- 动态数据库：Resolution 必须可基于最新 Metadata 重新计算。
- 平台化：不得针对 material/supplier/customer 等具体业务实体硬编码特殊分支。

## 三、阶段目标

将 Phase 2.6 Semantic Applicability 能力转化为可执行 DimensionAware QueryPlan，并形成 SQL 闭环，同时为动态多数据库、未来多语言/多租户的 AI Native Low-code Platform 架构保留可扩展边界。

## 四、阶段核心规则

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
11. Relation Resolution 必须面向当前 Metadata Snapshot；不得将一次解析结果永久化为不可更新的业务事实。
12. 新数据库加入后必须允许重新发现 Relation，并在证据充分时由 DirectKey 升级为 MasterJoin。
13. 删除/失效数据库关系后必须允许重新解析并安全降级，不得继续使用失效 Relation。

## 五、D03 / STEP-03 最终审计结论（2026-08-25）

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

## 六、D04 / STEP-04 最终冻结结论（2026-08-25）

D04 Contract / Model 设计完成并正式冻结。本结论是 D05 及后续实现的唯一 Contract 输入。

### 6.1 双层 Contract

```text
DimensionResolution
↓
QueryPlanDimensionResolution
↓
QueryPlan
```

`DimensionResolution` 负责语义 Resolution 决策；`QueryPlanDimensionResolution` 负责最终 QueryPlan 物理绑定。QueryPlan、SQL Builder、Runtime 不得重新进行无上下文 Dimension Search。

### 6.2 ResolutionState

`Resolved | Ambiguous | NotResolved`

### 6.3 ResolutionMode

`MasterJoin | DirectKey`

State 与 Mode 独立。合法语义包括：`Resolved + MasterJoin`、`Resolved + DirectKey`、`Ambiguous + null`、`NotResolved + null`。

### 6.4 DimensionResolution 字段

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

### 6.5 D04 修改边界

D04 本身不修改业务实现。后续实现允许新增/修改 Dimension Resolution Models、Interfaces、Services，但不得通过修改 `QueryPlanEvaluator`、`QueryPlanOrderResolution`/GQ-011 Ranking Order Binding、GQ-011 Golden、`QueryJoin` Model 或改变 `QueryJoinInferenceService` 职责来绕过 Contract。

QueryPlanBuilder Dimension Binding 在 D08 处理；SQL Builder MasterJoin / DirectKey 执行路径在 D11 处理。

### 6.6 D04 兼容性冻结

GQ-011 当前 PASS 必须保持 PASS；其他既有 PASS GQ-*** 必须保持行为兼容；MasterJoin 不得被 DirectKey 抢占；Ambiguous / NotResolved 必须继续安全阻断。发现兼容性回归立即停止后续 STEP，回到源码根因审计。

## 七、D05 当前入口约束（待完整审计冻结）

D05 在编码前必须进一步确认：

1. 当前 Metadata 中 Entity Key / Code / Label 的真实证据来源；
2. `MetadataColumn.BusinessKey`、`IsPrimaryKey`、Semantic 等字段的实际语义；
3. 当前 DataSource 边界与跨数据库 Relation 证据；
4. Dynamic Metadata Snapshot / Refresh 后 Resolution 是否重新计算；
5. 多候选时如何进入 `Ambiguous`，而不是任意选择一个候选。

D05 不负责决定 `MasterJoin` / `DirectKey` 最终模式；D05 负责产生 Entity Key / Fact Key 及其 Evidence，供后续 Resolution Contract 使用。

## 八、开发步骤

| STEP | 状态 | 内容 |
|---|---|---|
| D01 | COMPLETE | Phase 2.6 / master 基线审计 |
| D02 | COMPLETE | Metadata Dimension Entity Key 审计 |
| D03 | COMPLETE | Master Table / Relation 审计 |
| D04 | **COMPLETE / FROZEN** | Contract / Model 设计 |
| D05 | **CURRENT / AUDIT** | Dimension Entity Key Resolver 完整源码与 Contract 审计 |
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

## 九、Runtime 对应

D01→STEP-01；D02→STEP-02；D03→STEP-03；D04→STEP-04；D05-D07→STEP-05~06；D08-D10→STEP-07~08；D11→STEP-09~10；D12-D14→STEP-11~12；D15-D18→STEP-13~15；D19→STEP-16；D20→STEP-17~18；D21→STEP-19。

Runtime 结果也必须执行“冻结 → 更新 Runtime/开发计划 → 确认 master → 下一 STEP”闭环。

## 十、兼容性验收

任何新增 DirectKey 能力必须验证：既有正确 MasterJoin 不变；GQ-011 Ranking 不变；既有 PASS GQ-*** 不回归；Ambiguous / NotResolved 仍安全阻断。出现兼容性问题立即停止并回到根因审计。

### 动态数据库兼容性验收

必须覆盖至少以下状态转换：

```text
A 数据库单独存在
→ 当前无 Master Relation
→ DirectKey / NotResolved

新增 B 数据库
→ 发现 A.fact_key ↔ B.dimension_key 的稳定 Relation
→ MasterJoin

Relation Evidence 消失
→ 重新 Resolution
→ DirectKey / NotResolved
```

不得通过修改代码、Golden 或硬编码业务主表实现上述状态转换。

## 十一、Exit Criteria

D01-D21 全部完成；MasterJoin、DirectKey、SameTable/CrossTable、Ambiguous/NotResolved、Golden Regression、Coverage、Quality、Release Gate 全部达到正式要求；Runtime、Commit、主计划、本计划均同步；每个 STEP 均完成“冻结 → 更新开发计划 → master 确认 → 下一 STEP”闭环；并验证动态 Metadata 新增/失效后 Relation Resolution 可以重新计算。
