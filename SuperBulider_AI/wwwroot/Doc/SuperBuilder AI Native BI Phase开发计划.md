# SuperBuilder AI Native BI
# Phase开发计划（正式版）

版本：v3.0  
更新日期：2026-08-27  
源码基线：GitHub `master`  
冻结基线：**Phase 2.7 — CLOSED / FROZEN**  
当前阶段：**Phase 3.1 — Business Entity Model**

> STEP 规则：Source Audit → Contract → 最终结论 → 更新本计划 → 回读 master 验证，完成后才能进入下一 STEP。

---

## 1. Phase 3.1 路线

```text
Phase 2.7 CLOSED / FROZEN
        ↓
Phase 3 Business Semantic Layer
        ↓
Phase 3.1 Business Entity Model
  ├── 3.1.1 Current Source Audit       ✅ PASS
  ├── 3.1.2 Business Entity Contract   ✅ PASS
  ├── 3.1.3 Entity Key Contract        ✅ PASS
  ├── 3.1.4 Entity Attribute Contract  ✅ PASS
  ├── 3.1.5 Entity Metric Contract     ✅ PASS
  ├── 3.1.6 Entity Relationship        ✅ PASS
  ├── 3.1.7 Physical Binding           ✅ PASS
  ├── 3.1.8 QueryPlan Mapping          ✅ PASS
  ├── 3.1.9 Golden Contract            ✅ PASS
  ├── 3.1.10 Runtime Verification     ✅ PASS
  └── 3.1.11 Source Implementation     ⏳ NEXT
```

目标：建立稳定、可执行、可验证的 Business Entity Semantic Layer，并通过 Mapping 进入 Phase 2.7 Frozen QueryPlan。

```text
BusinessEntity
 ├── EntityKey
 ├── EntityAttribute
 ├── EntityMetric
 └── EntityRelationship
          ↓
    PhysicalBinding
          ↓
 Existing Metadata
          ↓
 Entity Resolution
          ↓
 QueryPlan Mapping
          ↓
 Golden Contract
          ↓
 Runtime Verification
          ↓
 Frozen QueryPlan / Evaluator / SQL Builder
```

禁止 Business Entity 绕过 QueryPlan 直接生成 SQL。

---

## 2. Frozen 边界

Phase 2.7 已 CLOSED / FROZEN，不因 Phase 3.1 重构：QueryPlan、QueryDimension、QueryMetric、QueryFilter、QueryJoin、Semantic Resolution、Evaluator、Golden Expected Outcome、Decision Gate、SQL Builder、既有 Runtime Contract。

核心边界：

```text
BusinessEntity       ≠ MetadataTable
EntityKey            ≠ MetadataColumn.BusinessKey
EntityAttribute      ≠ MetadataColumn
EntityMetric         ≠ QueryMetric
EntityRelationship   ≠ QueryJoin
PhysicalBinding      ≠ MetadataColumn.BusinessKey
```

---

# 3.1.1 Current Source Audit

**✅ PASS**

确认现有 `MetadataTable / MetadataColumn / MetadataSemantic / DataSource` 是 Physical Metadata 基础；`QueryPlan / QueryDimension / QueryMetric / QueryFilter / QueryJoin / Evaluator` 是 Phase 2.7 Frozen Runtime。BusinessEntity、EntityKey、EntityAttribute、EntityMetric、EntityRelationship、PhysicalBinding、Entity Resolution 是 Phase 3 新增语义层能力。

---

# 3.1.2 Business Entity Contract

**✅ PASS**

```text
BusinessEntity
├── Id
├── BusinessKey
├── Name
├── DisplayName
├── Description
├── BusinessDomain
├── SemanticText
└── Status
```

BusinessKey 是稳定业务身份，不是物理表名；Entity 不保存 SQL，可映射多个 PhysicalBinding。

---

# 3.1.3 Entity Key Contract

**✅ PASS**

```text
BusinessEntityKey
├── Id
├── BusinessEntityId
├── Name
├── DisplayName
├── Description
├── IsPrimary
├── KeyType
└── PhysicalBindings
```

`MetadataColumn.BusinessKey` 是 Physical Field Identity；`MetadataColumn.IsPrimaryKey` 是物理 PK；均不等同业务 EntityKey。EntityKey 可以映射多个物理字段。

---

# 3.1.4 Entity Attribute Contract

**✅ PASS**

```text
BusinessEntityAttribute
├── Id
├── BusinessEntityId
├── Name
├── DisplayName
├── Description
├── SemanticType
├── IsNullable
├── IsIdentifier
└── PhysicalBindings
```

SemanticType 是业务语义类型，不覆盖物理 DataType；IsIdentifier 不等于 EntityKey.IsPrimary，也不等于 MetadataColumn.IsPrimaryKey。通过 Mapping 进入现有 QueryDimension / QueryFilter / QueryPlan。

---

# 3.1.5 Entity Metric Contract

**✅ PASS**

```text
BusinessEntityMetric
├── Id
├── BusinessEntityId
├── Name
├── DisplayName
├── Description
├── SemanticType
├── Aggregation
├── IsCalculated
└── PhysicalBindings
```

EntityMetric 是稳定业务指标定义；QueryMetric 是查询运行时实例。`Aggregation` 使用既有运行时聚合语义；`IsCalculated=true` 本阶段不引入公式 DSL / 任意 SQL Expression / 计算引擎。

---

# 3.1.6 Entity Relationship Contract

**✅ PASS**

```text
BusinessEntityRelationship
├── Id
├── SourceEntityId
├── TargetEntityId
├── Name
├── DisplayName
├── Description
├── RelationshipType
├── Cardinality
├── IsRequired
└── PhysicalBindings
```

`RelationshipType / Cardinality` 是稳定业务语义；`JoinType` 是查询执行语义。Relationship Resolution 后通过 Physical Binding 产生既有 QueryJoin，不修改 Frozen Evaluator / SQL Builder。

---

# 3.1.7 Physical Binding Contract

**✅ PASS**

```text
PhysicalBinding
├── Id
├── DataSourceId
├── MetadataTableId
├── MetadataColumnId
├── PhysicalRole
├── BindingType
├── Priority
└── IsActive
```

职责：Business Semantic → Existing Metadata Mapping。必须满足：

```text
Binding.DataSourceId == MetadataTable.DataSourceId
Binding.MetadataColumnId → MetadataColumn.MetadataTableId == Binding.MetadataTableId
```

支持跨 DataSource 候选、Priority、IsActive、Relationship 两端 Binding；Ambiguous / Unresolved 不得随机选择。PhysicalBinding 不复制 Metadata Physical Facts、不保存 SQL。

Identity 三层：`BaseEntity.Id = Persistence Identity`；`MetadataColumn.BusinessKey = Physical Field Identity`；`PhysicalBinding.Id = Semantic Mapping Identity`。

---

# 3.1.8 QueryPlan Mapping Contract

**✅ PASS**

```text
Business Entity Semantic
        ↓
Entity Resolution
        ↓
PhysicalBinding
        ↓
Mapping Adapter
        ↓
Existing Semantic Resolution
        ↓
Frozen QueryPlan
```

映射关系：

```text
EntityAttribute    → QueryDimension / QueryFilter
EntityMetric       → QueryMetric
EntityRelationship → QueryJoin
EntityKey          → Table / Dimension / Relationship Binding
BusinessEntity     → QueryTable
```

Mapping 必须确定性执行；不得再次自由语义搜索；Ambiguous / Unresolved / Invalid Binding / NotExecutable 必须显式失败。`QueryPlan.DataSourceId` 必须与选定 PhysicalBinding 保持一致。Entity Model 不直接生成 SQL，不绕过 Evaluator / SQL Builder。

---

# 3.1.9 Golden Contract

**✅ PASS**

Golden 复用现有 `GoldenDatasetRuntimeService`、`GoldenDatasetRunner`、`GoldenDatasetRegressionEvaluator` 和 `GoldenDatasetRuntimeController`，不重新建立 Golden Engine。现有 Runner 的真实运行顺序为 Semantic Applicability → Gate → Query Understanding → QueryPlan → Validation/Repair → Evaluation-aware Confidence → Calibration，且 Runner 不生成 SQL。fileciteturn120file0

Golden Contract 覆盖：

```text
Positive
Negative
Ambiguous
Unresolved
```

核心验证：

```text
Input Semantic
      ↓
Entity Resolution
      ↓
Physical Binding
      ↓
QueryPlan Mapping
      ↓
Expected Outcome
```

Positive 必须满足预期 Runtime Outcome；Negative 必须被正确拒绝；Ambiguous 必须得到 `Ambiguous`；Unresolved 必须得到 `NotResolved`。不得用简单 `Passed=false` 代替 Expected Outcome 判定。

Phase 2.7 Golden Expected Outcome 保持 Frozen，只作为 Regression。

---

# 3.1.10 Runtime Verification Design

**✅ PASS — Runtime Verification Contract 已确认**

## 3.1.10.1 Source Audit

当前 master 已有统一 Golden Runtime Pipeline：`GoldenDatasetRuntimeService.RunAsync()` 负责读取 Golden Dataset、调用 `GoldenDatasetRunner.RunAsync()`，再交给 `GoldenDatasetRegressionEvaluator.Evaluate()` 形成 Scorecard。fileciteturn118file0

`GoldenDatasetRuntimeController` 只负责 HTTP 参数、Case 筛选和响应；完整 Runtime Pipeline 仍由 Service 统一编排，并提供 `run / cases / release-gate` 三类 Runtime 入口。fileciteturn116file0

`GoldenDatasetRunner` 已将 Runtime 固定为：

```text
Semantic Applicability
        ↓
QueryPlan Evaluation Gate
        ↓
Query Understanding
        ↓
Semantic Resolution Factory
        ↓
QueryPlanBuilder
        ↓
QueryPlanContextBuilder
        ↓
QueryPlanValidationPipeline
        ↓
Evaluation-aware Confidence
        ↓
Calibration
```

Runner 本身不生成 SQL，因此 Phase 3.1 Runtime Verification 的责任是验证 Entity Contract 能否正确进入这一 Frozen Pipeline，而不是新建执行链。fileciteturn120file0

## 3.1.10.2 Phase 3.1 Runtime 插入点

```text
Business Entity
      ↓
Entity Resolution
      ↓
Physical Binding Validation
      ↓
Entity → QueryPlan Mapping
      ↓
Existing QueryPlan / Semantic Resolution
      ↓
Frozen QueryPlan Validation
      ↓
Frozen Evaluation / Confidence
      ↓
Golden Regression Scorecard
```

Phase 3.1 不改变现有 Golden Runner 的总体编排，只增加/接入 Entity Resolution、Physical Binding 和 Mapping Adapter 的实际实现。

## 3.1.10.3 Runtime PASS 判定

一个 Phase 3.1 Positive Case 必须同时满足：

1. Entity Resolution 成功；
2. Selected PhysicalBinding 有效；
3. DataSource / MetadataTable / MetadataColumn 三者一致；
4. QueryPlan Mapping 成功；
5. QueryPlan Validation 无 Contract Error；
6. Evaluation Outcome 满足 Golden Expected Outcome；
7. Regression Scorecard 不出现 Unexpected Applicability State；
8. Phase 2.7 Frozen Golden Regression 不漂移。

## 3.1.10.4 Runtime Negative / Ambiguous / Unresolved

```text
Invalid Binding
    ↓
Reject / Block

Multiple valid unresolved candidates
    ↓
Ambiguous

No valid candidate
    ↓
NotResolved
```

三种状态都必须保留诊断信息，不得自动降级为任意 Metadata Column。

## 3.1.10.5 Runtime DataSource Boundary

```text
Selected PhysicalBinding.DataSourceId
        ↓
QueryTable.DataSourceId
        ↓
QueryPlan.DataSourceId
```

任何跨 DataSource 冲突必须显式失败或由既有 Resolution Contract 决定；Mapping Adapter 不得静默切换数据源。

## 3.1.10.6 Runtime HTTP Boundary

现有 Controller 已确认：HTTP 层只做参数校验、Case 选择、状态码和响应包装；Golden Runtime Pipeline 统一由 `GoldenDatasetRuntimeService` 编排。fileciteturn116file0

因此 Phase 3.1 不在 Controller 中直接实现 Entity Resolution / Mapping / SQL。

## 3.1.10.7 Runtime Verification Cases

至少需要：

```text
R1 Positive Entity → Binding → QueryPlan
R2 Negative Invalid Binding
R3 Ambiguous Multiple Binding
R4 Unresolved No Binding
R5 Metric → QueryMetric
R6 Attribute → QueryDimension
R7 Attribute → QueryFilter
R8 Relationship → QueryJoin
R9 EntityKey → Key/Table Binding
R10 Multi-DataSource Conflict
R11 Phase 2.7 Golden Regression
```

## 3.1.10.8 Runtime Gate

```text
Entity Contract Verification       PASS
Physical Binding Verification      PASS
QueryPlan Mapping Verification     PASS
Golden Expected Outcome            PASS
Phase 2.7 Regression               PASS
Runtime Exception                  0
Unexpected Applicability State     0
```

Build / Startup 仍是最终实现阶段的运行验收，不在本 Design PASS 中提前声称已通过。

## 3.1.10.9 3.1.10 最终结论

**PASS。Runtime Verification Design 已冻结。**

已经确认 Phase 3.1 的 Runtime 应接入现有 Golden Runtime Pipeline，而不是重新设计 Runtime：Entity Resolution → Physical Binding → QueryPlan Mapping → Frozen QueryPlan Validation → Evaluation / Confidence → Golden Regression。HTTP Controller 继续保持薄层，Golden Service 继续作为统一编排入口。

> 注意：本 PASS 仅表示 Runtime Verification Design 完成；实际 Entity Runtime、Golden Case 扩展、Build、Startup 和运行结果尚未宣称完成。

---

# 3.1.11 Source Implementation Mapping

**⏳ NEXT**

下一步正式进入代码落地前的 Source Implementation Mapping：

```text
Business Entity Models
        ↓
Interfaces
        ↓
Services / Resolution
        ↓
Physical Binding
        ↓
Mapping Adapter
        ↓
DI
        ↓
Controller / Runtime
        ↓
EF Core / Migration
        ↓
Golden Cases
```

必须逐项确认 Model / Interface / Service / DI / Controller / Runtime / Database Migration，禁止出现“计划已完成但源码不存在”的漂移。

---

# 4. Phase 3.1 总验收

只有全部满足才允许 CLOSED / FROZEN：

- 3.1 Contract 全部 PASS
- Source Mapping 完成
- Entity Resolution Golden PASS
- Entity → Metadata Binding Golden PASS
- Entity → QueryPlan Golden PASS
- Phase 2.7 Golden Regression PASS
- Runtime PASS
- Build PASS
- Startup PASS
- Plan / Source / Golden / Runtime 无漂移

---

# 5. 当前正式结论

```text
Phase 2.7
    ✅ CLOSED / FROZEN
        ↓
Phase 3.1 Business Entity Model
        │
        ├── 3.1.1 Source Audit        ✅ PASS
        ├── 3.1.2 BusinessEntity      ✅ PASS
        ├── 3.1.3 EntityKey           ✅ PASS
        ├── 3.1.4 EntityAttribute     ✅ PASS
        ├── 3.1.5 EntityMetric        ✅ PASS
        ├── 3.1.6 EntityRelationship  ✅ PASS
        ├── 3.1.7 PhysicalBinding     ✅ PASS
        ├── 3.1.8 QueryPlanMapping    ✅ PASS
        ├── 3.1.9 Golden Contract     ✅ PASS
        ├── 3.1.10 Runtime Design     ✅ PASS
        └── 3.1.11 Implementation     ⏳ NEXT
```

**下一动作：3.1.11 Source Implementation Mapping。**
