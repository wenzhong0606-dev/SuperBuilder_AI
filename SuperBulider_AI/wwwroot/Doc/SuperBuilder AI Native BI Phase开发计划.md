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
  ├── 3.1.9 Golden Contract             ✅ PASS
  ├── 3.1.10 Runtime Verification      ⏳ NEXT
  └── 3.1.11 Source Implementation     ⏳
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
 Frozen QueryPlan
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

EntityMetric 是稳定业务指标定义；QueryMetric 是查询运行时实例：

```text
BusinessEntityMetric → Metric Resolution → QueryMetric → Frozen QueryPlan
```

Aggregation 使用既有运行时聚合语义；IsCalculated=true 仅表示业务计算指标，本阶段不引入公式 DSL / 任意 SQL Expression / 计算引擎。

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

**✅ PASS — Golden Contract 已确认**

## 3.1.9.1 Source Audit

当前 master 已存在 Golden Dataset Runtime 与 `GoldenDatasetRegressionEvaluator`。Regression Evaluator 明确按 `positive / negative / ambiguous / unresolved` 分类计算 Expected Outcome：Positive 需要 Runtime Passed；Negative 需要被正确拒绝；Ambiguous / Unresolved 需要得到预期 Applicability State。`PassedCases` 表示满足 Expected Outcome 的 Case，而不是简单统计 Runtime `Passed`。fileciteturn109file0

仓库已有多个 Golden result 文件以及 `GoldenDatasetRuntimeController`，说明 Phase 2.7 已有 Golden Runtime 基础设施，本阶段应扩展 Case Contract，而不是重新建立 Golden Engine。fileciteturn108file0 fileciteturn108file12

## 3.1.9.2 Golden Contract 总体结构

Phase 3.1 Golden 不直接比较 SQL 字符串，而验证 Entity Semantic → Physical Binding → QueryPlan Mapping 的 Expected Outcome：

```text
GoldenCase
├── CaseId
├── Category
├── InputSemantic
├── ExpectedEntity
├── ExpectedBinding
├── ExpectedQueryPlanMapping
└── ExpectedOutcome
```

Expected Outcome 至少覆盖：

```text
Positive
Negative
Ambiguous
Unresolved
```

## 3.1.9.3 Positive Case

```text
Input
 ↓
唯一 Entity
 ↓
唯一有效 PhysicalBinding
 ↓
确定 QueryPlan Mapping
 ↓
ExpectedOutcome = PASS
```

必须验证 Entity、Binding、QueryPlan Runtime Mapping 均与 Contract 一致。

## 3.1.9.4 Negative Case

用于验证非法 Contract / Invalid Binding / NotExecutable 等场景：

```text
Input
 ↓
Contract violation
 ↓
Resolution / Validation Reject
 ↓
ExpectedOutcome = REJECT
```

根据现有 Regression Contract，Negative Case 被正确拒绝才算 Golden PASS，而不是因为 Runtime `Passed=false` 就算失败。fileciteturn109file0

## 3.1.9.5 Ambiguous Case

多个有效候选且无法确定时：

```text
Candidates > 1
 ↓
无法消歧
 ↓
ApplicabilityState = Ambiguous
 ↓
ExpectedOutcome = AMBIGUOUS
```

禁止随机选择物理字段。

## 3.1.9.6 Unresolved Case

没有可用 Entity / Binding / Mapping 时：

```text
No valid resolution
 ↓
ApplicabilityState = NotResolved
 ↓
ExpectedOutcome = UNRESOLVED
```

## 3.1.9.7 Entity → Metadata Golden

至少验证：

```text
BusinessEntity
   ↓
PhysicalBinding
   ↓
DataSource / MetadataTable / MetadataColumn
```

必须验证 DataSourceId、MetadataTableId、MetadataColumnId 一致性，不接受幽灵 Binding。

## 3.1.9.8 Entity → QueryPlan Golden

至少验证：

```text
EntityAttribute    → QueryDimension / Filter
EntityMetric       → QueryMetric
EntityRelationship → QueryJoin
EntityKey          → Key / Table Binding
BusinessEntity     → QueryTable
```

Golden 不要求修改 QueryPlan Contract，只验证 Mapping 是否正确落入 Frozen Runtime。

## 3.1.9.9 Phase 2.7 Regression Boundary

```text
Phase 3.1 Golden
      ↓
验证新增 Entity Contract
      ↓
Phase 2.7 Golden Regression
      ↓
验证既有 Frozen Behavior 未漂移
```

Phase 3.1 不得修改既有 Golden Expected Outcome；既有 Golden 只作为 Regression。

## 3.1.9.10 Golden Gate

建议正式 Gate：

- Positive Expected Outcome 100%
- Negative Detection 100%
- Ambiguous Detection 100%
- Unresolved Detection 100%
- Unexpected Applicability State = 0
- Phase 2.7 Regression = PASS

最终是否达到发布门槛继续由既有 `GoldenDatasetRegressionPolicy` 决定，不在 Entity Contract 中复制另一套 Gate。fileciteturn109file0

## 3.1.9.11 3.1.9 最终结论

**PASS。Golden Contract 已冻结。**

Phase 3.1 Golden 正式验证四类 Expected Outcome，并覆盖 Entity Resolution、Entity → Metadata Binding、Entity → QueryPlan Mapping；复用现有 Golden Runtime / Regression Evaluator，不重造 Golden Engine，不修改 Phase 2.7 Golden Expected Outcome。

> 注意：本 PASS 表示 Golden Contract Design 完成，不代表 Golden Case 已全部实现并运行通过。

---

# 3.1.10 Runtime Verification Design

**⏳ NEXT**

```text
Contract Verification
 ↓
Entity Resolution
 ↓
Physical Binding
 ↓
Entity → QueryPlan Mapping
 ↓
Phase 2.7 Validation / Evaluator
 ↓
Existing SQL / Runtime Path
```

要求：Runtime PASS + Phase 2.7 Regression PASS + Build PASS + Startup PASS。

---

# 3.1.11 Source Implementation Mapping

**⏳**

最终确认 Model / Interface / Service / DI / Controller / Runtime 调用链、数据库迁移及实际代码落地。

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
        ├── 3.1.9 Golden Contract      ✅ PASS
        └── 3.1.10 Runtime             ⏳ NEXT
```

**下一动作：3.1.10 Runtime Verification Design。**
