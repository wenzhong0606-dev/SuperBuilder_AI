# SuperBuilder AI Native BI
# Phase开发计划（正式版）

版本：v3.0
更新日期：2026-08-27
文档状态：**Phase 3.1 — Business Entity Model / Contract Design**
源码基线：GitHub `master`
冻结基线：**Phase 2.7 — CLOSED / FROZEN**

---

# 1. 当前 Phase 路线

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
  ├── 3.1.7 Physical Binding           ⏳ NEXT
  ├── 3.1.8 QueryPlan Mapping          ⏳
  ├── 3.1.9 Golden Contract             ⏳
  ├── 3.1.10 Runtime Verification      ⏳
  └── 3.1.11 Source Implementation     ⏳
```

规则：每完成一个 STEP，必须形成最终结论、立即更新本计划、回读 `master` 验证后才进入下一 STEP。

---

# 2. Frozen 基线

Phase 2.7 的 QueryPlan、Semantic Resolution、Dimension / Metric Resolution、Join Resolution、Evaluator、Golden Expected Outcome、Decision Gate、SQL Builder 和既有 Runtime Contract 均视为 Frozen Foundation。

Phase 3 不允许 Business Entity 绕过 QueryPlan 直接生成 SQL。

---

# 3. Phase 3.1 目标

建立稳定、可执行、可验证的 Business Entity Model：

```text
BusinessEntity
 ├── EntityKey
 ├── EntityAttribute
 ├── EntityMetric
 └── EntityRelationship
          ↓
    Physical Binding
          ↓
 Existing Metadata
          ↓
 Entity Resolution
          ↓
 Frozen QueryPlan
```

非目标：QueryPlan / Evaluator / Phase 2.7 Golden / Decision Gate 重构、知识图谱、自动实体学习、复杂 Ontology。

---

# 4. 3.1.1 Current Source Audit

状态：**✅ PASS**

| 能力 | 当前状态 | Phase 3.1 动作 |
|---|---|---|
| MetadataTable | 已存在 | 🟢 复用 |
| MetadataColumn | 已存在，含 BusinessKey / PK / Semantic | 🟢 复用 |
| MetadataSemantic | 已存在，承担字段级业务语义 | 🟢 复用 |
| QueryPlan | 已存在 | ⛔ Frozen |
| QueryJoin | 已存在，为动态 QueryPlan JOIN | ⛔ Frozen，不等同 Business Relationship |
| BusinessEntity | 无直接等价物 | 🔴 新增 |
| EntityKey | 无独立业务身份 Contract | 🔴 新增 |
| EntityAttribute | 无独立业务属性 Contract | 🔴 新增 |
| EntityMetric | QueryMetric / Resolution 提供基础 | 🟡 新增 Entity 语义层 |
| EntityRelationship | QueryJoin 不等价 | 🔴 新增 |
| PhysicalBinding | 无独立 Contract | 🟡 新 Contract |
| Entity Resolution | 无独立 Entity 层 | 🔴 新增 |

冻结边界：`Business Entity ≠ MetadataTable`；`Business Relationship ≠ QueryJoin`；Business Entity 不直接生成 SQL；Phase 3.1 不修改 Phase 2.7 Frozen Contract。

---

# 5. 3.1.2 Business Entity Contract

状态：**✅ PASS**

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

规则：BusinessKey 是稳定业务身份而非表名；Entity 可以映射多个 PhysicalBinding；Entity 不保存 SQL；Resolution 后进入 Physical Binding / QueryPlan Mapping。

---

# 6. 3.1.3 Entity Key Contract

状态：**✅ PASS**

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

`MetadataColumn.BusinessKey` 是字段级 Physical Identity，`MetadataColumn.IsPrimaryKey` 是物理 PK；二者均不等同 BusinessEntityKey。

规则：EntityKey 可以映射多个物理字段；IsPrimary 表示业务语义主 Key；暂不引入复杂 CompositeKey / Ontology。

---

# 7. 3.1.4 Entity Attribute Contract

状态：**✅ PASS**

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

`SemanticType` 是业务语义类型，不覆盖物理 DataType；`IsIdentifier` 不等于 EntityKey.IsPrimary，也不等于 MetadataColumn.IsPrimaryKey。

Attribute 复用 MetadataColumn / MetadataSemantic 的物理事实和字段语义，通过 Mapping 进入现有 QueryDimension / QueryFilter / QueryPlan，不修改 Frozen Contract。

---

# 8. 3.1.5 Entity Metric Contract

状态：**✅ PASS**

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

EntityMetric 是稳定的业务指标定义；QueryMetric 是一次查询中的运行时指标实例。EntityMetric 提供默认聚合和业务语义，通过 Metric Resolution / Mapping 进入 Frozen QueryMetric / QueryPlan，不直接生成 SQL。

`IsCalculated=true` 仅表达计算指标语义，本阶段不引入公式 DSL、SQL Expression 或计算引擎。

---

# 9. 3.1.6 Entity Relationship Contract

状态：**✅ PASS — Contract 已确认**

## 9.1 Contract 定义

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

## 9.2 字段职责

| 字段 | 职责 | 规则 |
|---|---|---|
| Id | Relationship 技术身份 | Relationship 自身唯一标识 |
| SourceEntityId | 源业务实体 | 必须指向 BusinessEntity |
| TargetEntityId | 目标业务实体 | 必须指向 BusinessEntity |
| Name | 稳定关系语义名称 | 用于 Relationship Resolution |
| DisplayName | 业务展示名称 | 面向业务用户 |
| Description | 关系业务定义 | 用于语义理解 / Explainability |
| RelationshipType | 业务关系类型 | 如 BelongsTo / Has / References / Measures 等；由 Contract 校验 |
| Cardinality | 业务基数 | 1:1 / 1:N / N:1 / N:N |
| IsRequired | 业务关系是否必需 | 仅表达业务语义约束，不等同数据库 FK Nullable |
| PhysicalBindings | 关系的物理实现集合 | 后续绑定到 MetadataColumn 对及 JOIN 条件 |

## 9.3 与 QueryJoin 的核心边界

现有 `QueryJoin` 明确是“本次 QueryPlan 中两个动态数据表之间的 JOIN”，拥有 Left/Right TableId、ColumnId、物理名称、语义文本以及 JoinType；它不是数据库真实外键关系。fileciteturn82file0

因此正式冻结：

```text
BusinessEntityRelationship
        ≠
QueryJoin
```

两者职责：

```text
BusinessEntityRelationship
    ↓
稳定的业务关系知识
    ↓
Relationship Resolution

QueryJoin
    ↓
一次查询的运行时物理 JOIN
    ↓
SQL Builder
```

Relationship 是长期业务语义；QueryJoin 是一次 QueryPlan 的执行决策。

## 9.4 与 QueryPlan Join Evaluation 的关系

当前 `QueryPlanJoinScoringService` 只评价 Runtime Join 的物理 Contract，并要求 Runtime 提供有效的 TableId / ColumnId / JoinType；Evaluator 不重新从 Metadata 做第二次 Semantic Resolution。fileciteturn84file0

因此 Phase 3.1 Relationship 层必须在 QueryPlan 构建 / Resolution 阶段完成语义映射，然后生成既有 `QueryJoin`；不能让 Evaluator 反向读取 BusinessEntityRelationship 来修改 Frozen Evaluation Contract。

```text
BusinessEntityRelationship
        ↓
Relationship Resolution
        ↓
Physical Binding
        ↓
QueryJoin
        ↓
Frozen Evaluator
```

## 9.5 RelationshipType 与 JoinType 边界

```text
RelationshipType
    = 业务语义

JoinType
    = 查询执行语义
```

例如：

```text
Supplier
   ── Supplies ──>
PurchaseOrder

RelationshipType = Supplies

本次查询可能选择：
JoinType = INNER
```

不能把 `Supplies` 直接当作 `INNER / LEFT / RIGHT`。

## 9.6 Cardinality 边界

`Cardinality` 描述业务关系的稳定基数，不等同于一次 SQL JOIN 的执行类型。

例如：

```text
Supplier 1 ─── N PurchaseOrder
```

可以在不同查询中采用 INNER 或 LEFT JOIN，具体执行由 QueryPlan 决策，不改变业务关系本身。

## 9.7 Physical Binding 边界

Relationship 不直接保存任意 SQL JOIN 表达式。

```text
BusinessEntityRelationship
        ↓
PhysicalBindings
        ↓
Source Entity Key / Target Entity Key
        ↓
MetadataColumn
        ↓
QueryJoin
```

具体 PhysicalBinding Contract 在 3.1.7 冻结。

## 9.8 与 Phase 2.7 Frozen Contract 的关系

3.1.6 不修改：

- QueryJoin
- QueryPlan
- Join Resolution
- QueryPlanJoinScoringService
- Evaluator
- Golden Expected Outcome
- Decision Gate
- SQL Builder

BusinessEntityRelationship 只增加业务语义关系层，通过 Resolution / Mapping 产生已有 QueryJoin。

## 9.9 3.1.6 最终结论

**PASS。BusinessEntityRelationship Contract 已冻结为“稳定业务关系层”；QueryJoin 继续作为 QueryPlan 的运行时物理 JOIN。RelationshipType / Cardinality 属于业务语义，JoinType 属于查询执行语义。两者不得混淆。**

本 PASS 仅表示 Contract Design 完成，不代表 Relationship 已完成代码、数据库、Golden 或 Runtime 实现。

---

# 10. 3.1.7 Physical Binding Contract

状态：**⏳ NEXT**

目标：统一 EntityKey / Attribute / Metric / Relationship 到现有 MetadataTable / MetadataColumn 的物理映射。

---

# 11. 3.1.8 QueryPlan Mapping Contract

状态：**⏳**

```text
Business Semantic
      ↓
Entity Resolution
      ↓
Mapping Adapter
      ↓
Frozen QueryPlan
```

---

# 12. 3.1.9 Golden Contract

状态：**⏳**

覆盖：Entity Resolution、Entity → Metadata Binding、Entity → QueryPlan；Phase 2.7 Golden 只做 Regression，不修改既有 Expected Outcome。

---

# 13. 3.1.10 Runtime Verification Design

状态：**⏳**

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

---

# 14. 3.1.11 Source Implementation Mapping

状态：**⏳**

最终需要确认 Model / Interface / Service / DI / Controller / Runtime 调用链及数据库迁移。

---

# 15. Phase 3.1 总验收

Phase 3.1 只有同时满足以下条件才能 CLOSED：

- Contract 全部冻结
- Source Mapping 全部完成
- Golden 建立并通过
- Runtime 验证通过
- Phase 2.7 Regression PASS
- Build PASS
- Startup PASS
- Plan / Source / Golden / Runtime 一致

---

# 16. 当前阶段结论

```text
Phase 2.7
    ✅ CLOSED / FROZEN
        ↓
Phase 3.1
    🚧 CURRENT
        │
        ├── 3.1.1 Source Audit       ✅ PASS
        ├── 3.1.2 BusinessEntity     ✅ PASS
        ├── 3.1.3 EntityKey          ✅ PASS
        ├── 3.1.4 EntityAttribute    ✅ PASS
        ├── 3.1.5 EntityMetric       ✅ PASS
        ├── 3.1.6 EntityRelationship ✅ PASS
        └── 3.1.7 PhysicalBinding    ⏳ NEXT
```

下一动作：**3.1.7 Physical Binding Contract**。
