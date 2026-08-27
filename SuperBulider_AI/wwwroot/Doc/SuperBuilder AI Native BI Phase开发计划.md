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
  ├── 3.1.8 QueryPlan Mapping          ✅ PASS
  ├── 3.1.9 Golden Contract             ⏳ NEXT
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

**✅ PASS — Contract 已确认**

## 3.1.8.1 Source Audit 结果

当前 master 的 `QueryPlan` 已冻结为运行时执行计划，核心容器包括 `Tables / Fields / Metrics / Dimensions / Filters / Orders / Joins`，并包含 `DataSourceId`、聚合及 Ranking 状态。fileciteturn100file0

当前 `QueryMetric` 明确将 `SemanticText` 与物理 `Field` 分离，并通过 `Aggregation` 进入运行时聚合。fileciteturn101file0

当前 `QueryDimension` 明确要求 `MetadataColumnId`、`SemanticText`、`ColumnName` 以及 Phase 2.7 已冻结的 `ResolutionType / ResolutionState / ExecutionCapability`；DirectKey / MasterJoin 还存在对应 Key / Label Runtime Binding。fileciteturn102file0

当前 `QueryFilter` 是已确认业务语义到物理字段的运行时过滤 Contract，由 `SemanticText / Field / Operator / Value` 构成。fileciteturn103file0

当前 `QueryPlanBuilder.SemanticResolution` 已存在明确的“只消费已确认 Semantic Resolution、不进行二次语义搜索”的 Runtime Mapping 路径，并分别执行 Metric / Filter / Dimension / Table / Order Resolution；同时对 Dimension 的 Executable、ResolutionType、Key Binding 等 Contract 做校验。fileciteturn104file0

因此 3.1.8 的结论不是重新设计 QueryPlan，而是定义：

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
Frozen QueryPlan Runtime
```

## 3.1.8.2 Mapping 总 Contract

```text
BusinessEntity
    ↓
Entity Resolution
    ↓
┌────────────────────────────────────────────┐
│ Entity → QueryPlan Runtime Mapping         │
├────────────────────────────────────────────┤
│ EntityAttribute → QueryDimension / Filter  │
│ EntityMetric   → QueryMetric               │
│ EntityRelationship → QueryJoin             │
│ EntityKey      → Table / Dimension Binding │
└────────────────────────────────────────────┘
    ↓
QueryPlan
```

原则：Business Entity 是语义源；QueryPlan 是执行目标；Mapping Adapter 是唯一桥接层。

## 3.1.8.3 EntityAttribute → QueryDimension

默认映射：

```text
BusinessEntityAttribute
        ↓
Selected PhysicalBinding
        ↓
MetadataColumn
        ↓
QueryDimension
```

字段映射：

| Entity 层 | QueryPlan Runtime |
|---|---|
| Name / DisplayName / Description | SemanticText |
| SemanticType | SemanticType |
| PhysicalBinding.MetadataColumnId | MetadataColumnId |
| Physical Metadata ColumnName | ColumnName |
| Entity Resolution 结果 | ResolutionType / ResolutionState / ExecutionCapability |

DirectKey / MasterJoin 的具体 Runtime Contract 必须继续遵守 Phase 2.7 `QueryDimension` 既有字段，不在 Mapping 层重新发明维度执行模型。

## 3.1.8.4 EntityAttribute → QueryFilter

当用户语义要求对 Attribute 进行过滤：

```text
EntityAttribute
      ↓
PhysicalBinding
      ↓
MetadataColumn
      ↓
QueryFilter
```

映射：

```text
SemanticText = Attribute Business Meaning
Field        = Physical Column
Operator     = Resolved Runtime Operator
Value        = Resolved Runtime Value
```

Filter 的 Operator / Value 不由 EntityAttribute Contract 自行生成；它们属于具体查询意图和既有 Resolution Runtime Contract。

## 3.1.8.5 EntityMetric → QueryMetric

```text
BusinessEntityMetric
        ↓
Selected PhysicalBinding
        ↓
MetadataColumn
        ↓
QueryMetric
```

映射：

```text
Name         = EntityMetric.Name
SemanticText = EntityMetric.Description / Semantic Meaning
Field        = PhysicalBinding → MetadataColumn.ColumnName
Aggregation  = EntityMetric.Aggregation
SemanticType = EntityMetric.SemanticType
```

运行时必须经过 Metric Resolution / Semantic Validation；不能把 EntityMetric 的字段直接写入 QueryPlan 而跳过 Phase 2.7 Contract。

特别是 `EntityCount` 等已存在的确定性 MetricType Contract，必须继续由 Resolution 强制聚合语义，不能被 LLM Runtime Intent 随意覆盖。fileciteturn104file0

## 3.1.8.6 EntityRelationship → QueryJoin

```text
BusinessEntityRelationship
        ↓
Source PhysicalBinding
Target PhysicalBinding
        ↓
Resolved physical columns
        ↓
QueryJoin
```

业务层：

```text
RelationshipType
Cardinality
IsRequired
```

运行时层：

```text
LeftTableId
LeftColumnId
RightTableId
RightColumnId
JoinType
```

`JoinType` 由 QueryPlan Runtime 决策产生，不由 RelationshipType 直接转换。Business Relationship 只提供关系语义和候选物理绑定。

## 3.1.8.7 EntityKey → QueryPlan

EntityKey 不直接成为 SQL 字符串，也不强制等价于 QueryDimension。

它主要承担：

```text
EntityKey
   ↓
PhysicalBinding
   ↓
MetadataColumn
   ↓
Table / Dimension / Relationship Binding
```

在 DirectKey Dimension 场景中，EntityKey 可以提供 `DimensionKeyColumnId / DimensionKeyColumnName` 的来源；在 Relationship 场景中，EntityKey 可以提供 Relationship 两端的物理连接候选。

具体落入哪个 QueryPlan Runtime 字段，由对应 Resolution Contract 决定。

## 3.1.8.8 BusinessEntity → QueryTable

BusinessEntity 不等于 QueryTable。

```text
BusinessEntity
        ↓
PhysicalBinding
        ↓
MetadataTable
        ↓
QueryTable
```

QueryTable 是本次 QueryPlan 的执行表实例；BusinessEntity 是稳定业务对象。

同一个 BusinessEntity 可以根据 DataSource / PhysicalBinding 在不同查询中映射到不同物理表。

## 3.1.8.9 Mapping Determinism

Mapping 必须满足：

```text
同一
Business Entity
+ 同一 Resolution
+ 同一 Physical Binding
        ↓
必须产生
同一 QueryPlan Runtime Binding
```

禁止 Mapping Adapter 再次调用自由语义搜索。

如果 Resolution 返回：

```text
Ambiguous
Unresolved
Invalid Binding
NotExecutable
```

则 Mapping 必须失败或显式返回对应状态，不得静默降级到任意物理字段。

## 3.1.8.10 DataSource Boundary

`QueryPlan.DataSourceId` 是 Runtime Contract 的一部分。当前 Resolved Build 路径会在物理表已确认后补齐 DataSourceId，保证 Runtime DataSource 与 Resolution 保持一致。fileciteturn104file0

因此 Mapping 必须保证：

```text
Selected PhysicalBinding.DataSourceId
        ↓
QueryTable.DataSourceId
        ↓
QueryPlan.DataSourceId
```

出现跨 DataSource 冲突时，必须由 Resolution / Contract 显式拒绝，而不是在 Mapping 阶段偷偷切换数据源。

## 3.1.8.11 Frozen QueryPlan 边界

3.1.8 明确禁止：

- 修改 QueryPlan 字段定义
- 修改 QueryMetric / QueryDimension / QueryFilter / QueryJoin Contract
- 让 Entity Model 直接生成 SQL
- 让 Entity Model 绕过 Semantic Resolution
- 让 Evaluator 重新执行 Entity Resolution
- 用 Entity Contract 替换 Phase 2.7 Golden Contract

正确路径：

```text
Entity Model
    ↓
Entity Resolution
    ↓
Physical Binding
    ↓
Mapping Adapter
    ↓
Existing Semantic Resolution Contract
    ↓
Frozen QueryPlan
    ↓
Frozen Evaluator / SQL Builder
```

## 3.1.8.12 3.1.8 最终结论

**PASS。QueryPlan Mapping Contract 已冻结。**

它正式规定 BusinessEntity / EntityKey / EntityAttribute / EntityMetric / EntityRelationship 只能通过 Entity Resolution + PhysicalBinding + Mapping Adapter 进入现有 QueryPlan Runtime；不改变 Phase 2.7 Frozen QueryPlan，不进行二次自由语义搜索，不绕过 Evaluator / SQL Builder。

本 PASS 仅代表 Mapping Contract Design 完成，不代表 Golden、Runtime、代码实现已经完成。

---

# 3.1.9 Golden Contract

**⏳ NEXT**

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
        └── 3.1.8 QueryPlanMapping    ✅ PASS
```

**下一动作：3.1.9 Golden Contract。**
