# SuperBuilder AI Native BI
# Phase开发计划（正式版）

版本：v3.0  
更新日期：2026-08-27  
源码基线：GitHub `master`  
冻结基线：**Phase 2.7 — CLOSED / FROZEN**  
当前阶段：**Phase 3.1 — Business Entity Model / Contract Design**

> 规则：每个 STEP 必须完成 Source Audit → Contract → 最终结论 → 更新本计划 → 回读 master 验证，才能进入下一 STEP。

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
  ├── 3.1.8 QueryPlan Mapping          ⏳ NEXT
  ├── 3.1.9 Golden Contract             ⏳
  ├── 3.1.10 Runtime Verification      ⏳
  └── 3.1.11 Source Implementation     ⏳
```

Phase 3.1 的目标：建立稳定、可执行、可验证的 Business Entity Semantic Layer，并通过 Mapping 进入 Phase 2.7 Frozen QueryPlan。

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
 Frozen QueryPlan
```

禁止 Business Entity 绕过 QueryPlan 直接生成 SQL。

---

## 2. Frozen 边界

Phase 2.7 已 CLOSED / FROZEN。以下均不因 Phase 3.1 Contract Design 被重构：

- QueryPlan
- QueryDimension / QueryMetric / QueryFilter
- QueryJoin / Join Resolution
- Semantic Resolution
- Evaluator
- Golden Expected Outcome
- Decision Gate
- SQL Builder
- 既有 Runtime Contract

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

已确认 `MetadataTable`、`MetadataColumn`、`MetadataSemantic` 为现有 Metadata 基础；QueryPlan / QueryJoin / Evaluator 为 Frozen；BusinessEntity、EntityKey、EntityAttribute、EntityMetric、EntityRelationship、PhysicalBinding、Entity Resolution 为 Phase 3 新增语义层能力。

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

`SemanticType` 是业务语义类型，不覆盖物理 DataType；`IsIdentifier` 不等于 EntityKey.IsPrimary，也不等于 MetadataColumn.IsPrimaryKey。通过 Mapping 进入现有 QueryDimension / QueryFilter / QueryPlan。

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

`Aggregation` 支持既有运行时聚合语义；`IsCalculated=true` 只表达业务计算指标，本阶段不引入公式 DSL / 任意 SQL Expression / 计算引擎。

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

`RelationshipType / Cardinality` 属于稳定业务语义；`JoinType` 属于一次查询的执行语义。

```text
BusinessEntityRelationship
        ↓
Relationship Resolution
        ↓
Physical Binding
        ↓
QueryJoin
        ↓
Frozen Evaluator / SQL Builder
```

不得把 Business Relationship 直接当作 QueryJoin；不修改 Phase 2.7 Join / Evaluator Contract。

---

# 3.1.7 Physical Binding Contract

**✅ PASS — Contract 已确认**

## Contract

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

## 字段职责

| 字段 | 语义 |
|---|---|
| Id | Mapping Identity；独立于 MetadataColumn.Id |
| DataSourceId | 物理数据源边界 |
| MetadataTableId | 物理表 |
| MetadataColumnId | 物理字段 |
| PhysicalRole | Key / Attribute / Metric / RelationshipSource / RelationshipTarget 等物理职责 |
| BindingType | Column / Derived / Composite 等映射类型扩展点 |
| Priority | 多候选 Binding 的确定性优先级 |
| IsActive | 是否允许进入 Resolution |

## 与现有 Metadata 的关系

现有 `MetadataTable` 已包含 `DataSourceId`、`TableName`、`Columns`；`MetadataColumn` 已包含 `MetadataTableId`、`BusinessKey`、`ColumnName`、`DataType`、`IsNullable`、`IsPrimaryKey`；`DataSource` 通过自身 Id 与 MetadataTable 建立数据源边界。fileciteturn88file0 fileciteturn89file0 fileciteturn93file0

因此 PhysicalBinding 不复制 Physical Metadata，而表达：

```text
Business Semantic Object
        ↓
PhysicalBinding
        ↓
DataSource
        ↓
MetadataTable
        ↓
MetadataColumn
```

## 一致性规则

必须满足：

```text
Binding.DataSourceId == MetadataTable.DataSourceId

Binding.MetadataColumnId
        ↓
MetadataColumn.MetadataTableId
        == Binding.MetadataTableId
```

不一致的 Binding 必须判定 Invalid，不得进入 Entity Resolution / QueryPlan Mapping。

## 多数据源规则

一个 Entity Key / Attribute / Metric / Relationship 可以拥有多个 PhysicalBinding：

```text
Business Semantic
       ↓
PhysicalBindings
   ├── ERP physical field
   └── CRM physical field
```

只允许 `IsActive=true` 的候选参与 Resolution；多个候选使用 Priority 和后续语义评分确定性选择。无法消歧必须返回 `Ambiguous / Unresolved`，禁止随机选择。

## Relationship Binding

```text
BusinessEntityRelationship
        ↓
PhysicalBindings
   ├── RelationshipSource → MetadataColumn
   └── RelationshipTarget → MetadataColumn
        ↓
QueryJoin
```

PhysicalBinding 不保存任意 SQL JOIN 字符串。

## BindingType 边界

`Column` 表示直接绑定 MetadataColumn；`Derived` / `Composite` 仅保留扩展点，本阶段不实现任意 SQL Expression 或复杂 CompositeKey Engine。

## 三层 Identity

现有 `BaseEntity.Id` 继续作为 Persistence Identity。fileciteturn94file0

```text
BaseEntity.Id
    = Persistence Identity

MetadataColumn.BusinessKey
    = Physical Field Identity

PhysicalBinding.Id
    = Semantic Mapping Identity
```

三者不能互相替代。

## Frozen 边界

3.1.7 不修改 MetadataTable、MetadataColumn、DataSource、QueryPlan、QueryDimension、QueryMetric、QueryFilter、QueryJoin、Evaluator、Golden、Decision Gate、SQL Builder。

## 3.1.7 最终结论

**PASS。PhysicalBinding Contract 已冻结为“业务语义 → 物理 Metadata 映射层”。**

它支持跨 DataSource 映射、Table/Column 一致性校验、Active / Priority 确定性选择、Relationship 两端物理 Binding，以及 Ambiguous / Unresolved 显式失败；不复制 Physical Metadata、不保存 SQL、不侵入 Phase 2.7 Frozen Runtime。

> 注意：本 PASS 仅表示 Contract Design 完成；代码、数据库迁移、Golden、Runtime 尚未实现。

---

# 3.1.8 QueryPlan Mapping Contract

**⏳ NEXT**

```text
Business Entity / Key / Attribute / Metric / Relationship
                         ↓
                 Entity Resolution
                         ↓
                  PhysicalBinding
                         ↓
                   Mapping Adapter
                         ↓
                  Frozen QueryPlan
```

目标：定义 Entity Semantic Layer 到现有 QueryDimension / QueryMetric / QueryFilter / QueryJoin 的确定性映射；禁止修改 Phase 2.7 Frozen Contract。

---

# 3.1.9 Golden Contract

**⏳**

覆盖 Entity Resolution、Entity → Metadata Binding、Entity → QueryPlan；Phase 2.7 Golden 仅 Regression，不修改既有 Expected Outcome。

# 3.1.10 Runtime Verification Design

**⏳**

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

# 3.1.11 Source Implementation Mapping

**⏳**

最终确认 Model / Interface / Service / DI / Controller / Runtime 调用链及数据库迁移。

---

# 4. Phase 3.1 总验收

只有以下全部 PASS 才能 CLOSED / FROZEN：

- 所有 Contract 冻结
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
        └── 3.1.8 QueryPlanMapping    ⏳ NEXT
```

**下一动作：3.1.8 QueryPlan Mapping Contract。**
