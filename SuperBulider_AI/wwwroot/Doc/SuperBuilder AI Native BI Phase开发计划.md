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
  ├── 3.1.6 Entity Relationship        ⏳ NEXT
  ├── 3.1.7 Physical Binding           ⏳
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

确认：

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
| PhysicalBinding | 无独立 Contract | 🟡 新 Contract，复用 Metadata Identity |
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

状态：**✅ PASS — Contract 已确认**

## 8.1 Contract 定义

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

## 8.2 字段职责

| 字段 | 职责 | 规则 |
|---|---|---|
| Id | Metric 技术身份 | EntityMetric 自身唯一标识 |
| BusinessEntityId | 所属实体 | 必须指向 BusinessEntity |
| Name | 稳定指标语义名称 | 用于 Metric Resolution |
| DisplayName | 业务展示名称 | 面向业务用户 |
| Description | 指标业务定义 | 用于语义理解 / Explainability |
| SemanticType | 指标业务类型 | 表达 Amount / Quantity / Count 等语义，不是物理 DataType |
| Aggregation | 默认聚合语义 | SUM / COUNT / AVG / MAX / MIN / DISTINCTCOUNT / NONE 等；必须经过 Contract 校验 |
| IsCalculated | 是否为业务计算指标 | true 时允许后续由 Metric Resolution 定义计算规则；本阶段不直接生成 SQL |
| PhysicalBindings | 指标物理来源 | 可映射一个或多个物理字段 / MetadataColumn |

## 8.3 与现有 QueryMetric 的边界

当前 `QueryMetric` 已明确分离 `SemanticText` 与 `Field`，并通过 `Aggregation` 表达查询时聚合。现有 `GetAggregation()` 支持 SUM / COUNT / AVG / MAX / MIN / DISTINCTCOUNT 等聚合。fileciteturn75file0

因此：

```text
BusinessEntityMetric
        ↓
Metric Resolution
        ↓
QueryMetric
        ↓
Frozen QueryPlan
```

**禁止：**

```text
BusinessEntityMetric = QueryMetric
```

EntityMetric 是稳定的业务指标定义；QueryMetric 是一次查询中的运行时指标实例。

## 8.4 Aggregation 规则

业务指标的默认 Aggregation 属于 Entity Contract，但运行时必须经过 Metric Resolution / Semantic Validation 后才能落入 QueryMetric。

例如：

```text
BusinessEntityMetric
  入库单数量
       ↓
Aggregation = COUNT
       ↓
QueryMetric
       ↓
QueryPlan
```

不得因为 LLM 对自然语言产生 `SUM(id)` 等不稳定猜测而覆盖已确认的 Entity Metric Contract。

当前 master 的 Semantic Resolution 已存在根据已解析 `MetricType=EntityCount` 强制运行时使用 `COUNT` 的机制，说明 EntityMetric 应位于运行时 QueryMetric 之前，而不是取代该运行时 Contract。fileciteturn77file0

## 8.5 Calculated Metric 边界

`IsCalculated=true` 只表达“业务指标需要计算”，不在 3.1.5 直接引入公式 DSL、SQL 表达式或计算引擎。

因此：

```text
EntityMetric
  ↓
Metric Resolution
  ↓
[未来计算定义]
  ↓
QueryMetric
```

本阶段禁止：

- 在 EntityMetric 中直接保存任意 SQL
- 让 EntityMetric 绕过 QueryPlan
- 引入复杂指标编排语言

## 8.6 Physical Binding 边界

EntityMetric 不把单一物理字段当作唯一身份。一个业务指标可以在不同数据源 / 物理模型中拥有不同 Binding。

```text
BusinessEntityMetric
        ↓
PhysicalBindings
        ├── ERP.amount_column
        └── WMS.quantity_column
```

具体 Binding Contract 在 3.1.7 再冻结。

## 8.7 与 EntityAttribute 的边界

```text
EntityAttribute
    ↓
描述业务对象“有什么属性”

EntityMetric
    ↓
描述业务对象“如何度量”
```

例如：

```text
Product
├── Attribute: ProductName
├── Attribute: Category
└── Metric: InventoryQuantity
```

Attribute 不因为是数值字段就自动成为 Metric；Metric 必须具有明确的业务度量语义。

## 8.8 与 Phase 2.7 Frozen Contract 的关系

3.1.5 不修改：

- QueryMetric
- QueryPlan
- Metric Resolution
- Evaluator
- Golden Expected Outcome
- Decision Gate
- SQL Builder

EntityMetric 通过 Metric Resolution / Mapping 进入现有 QueryMetric。

## 8.9 3.1.5 最终结论

**PASS。EntityMetric Contract 已冻结为“稳定业务指标定义层”，QueryMetric 继续作为 Frozen QueryPlan 的运行时指标实例。EntityMetric 提供默认聚合和业务语义，但不直接生成 SQL、不替换 QueryMetric、不侵入 Phase 2.7。**

本 PASS 仅表示 Contract Design 完成，不代表 EntityMetric 已完成代码、数据库、Golden 或 Runtime 实现。

---

# 9. 3.1.6 Entity Relationship Contract

状态：**⏳ NEXT**

目标：定义 BusinessEntity 之间的稳定业务关系；明确与 QueryJoin 的边界。

---

# 10. 3.1.7 Physical Binding Contract

状态：**⏳**

目标：统一 Entity Key / Attribute / Metric / Relationship 到现有 MetadataTable / MetadataColumn 的物理映射。

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
        ├── 3.1.3 EntityKey         ✅ PASS
        ├── 3.1.4 EntityAttribute   ✅ PASS
        ├── 3.1.5 EntityMetric      ✅ PASS
        └── 3.1.6 EntityRelationship ⏳ NEXT
```

下一动作：**3.1.6 Entity Relationship Contract**。
