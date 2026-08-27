# SuperBuilder AI Native BI
# Phase开发计划（正式版）

版本：v3.0

文档状态：**Phase 3.1 开发基线**

当前开发阶段：**Phase 3.1 — Business Entity Model / Contract Design**

当前冻结基线：**Phase 2.7 — CLOSED / FROZEN**

当前源码基线：GitHub `master`

更新日期：2026-08-27

---

# 1. Phase开发总览

```text
Phase 2.7
  CLOSED / FROZEN
       ↓
Phase 3
  Business Semantic Layer
       ↓
Phase 3.1 Business Entity Model
  ├── 3.1.1 Current Source Audit       ✅ PASS
  ├── 3.1.2 Business Entity Contract   ✅ PASS
  ├── 3.1.3 Entity Key Contract        ✅ PASS
  ├── 3.1.4 Entity Attribute Contract  ⏳
  ├── 3.1.5 Entity Metric Contract     ⏳
  ├── 3.1.6 Entity Relationship        ⏳
  ├── 3.1.7 Physical Binding           ⏳
  ├── 3.1.8 QueryPlan Mapping          ⏳
  ├── 3.1.9 Golden Contract             ⏳
  ├── 3.1.10 Runtime Verification      ⏳
  └── 3.1.11 Source Implementation     ⏳
```

规则：每完成一个 STEP，必须先形成最终结论、立即更新本开发计划、回读 `master` 验证，然后才能进入下一 STEP。

---

# 2. Frozen 基线与边界

Phase 2.7 已 CLOSED / FROZEN。Phase 3 只在其上增加 Business Semantic Layer，不重构既有执行基础。

Frozen 对象包括：

- QueryPlan
- QueryPlan Semantic Resolution
- Dimension / Metric Resolution
- Join Resolution
- Evaluator
- Golden Expected Outcome
- Decision Gate
- SQL Builder
- 既有 Runtime Contract

禁止 Business Entity 绕过 QueryPlan 直接生成 SQL。

---

# 3. Phase 3.1 Business Entity Model

目标：建立稳定、可执行、可验证的业务实体语义 Contract，并将其映射到现有 Metadata 与 Frozen QueryPlan。

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

非目标：QueryPlan 重构、Evaluator 重构、Phase 2.7 Golden 修改、Decision Gate 修改、知识图谱、自动实体学习、复杂 Ontology。

---

# 4. 3.1.1 Current Source Audit

状态：**✅ PASS**

基于 master Phase 2.7 Frozen 源码审计：

| 能力 | master 当前状态 | Phase 3.1 动作 |
|---|---|---|
| MetadataTable | 已存在，描述物理表及业务域 | 🟢 复用 |
| MetadataColumn | 已存在，含 BusinessKey、PK、Semantic | 🟢 复用 |
| MetadataSemantic | 已存在，承担字段业务语义 | 🟢 复用 |
| QueryPlan | 已存在，承载 Metrics / Dimensions / Filters / Joins | ⛔ Frozen |
| QueryJoin | 已存在，表示 QueryPlan 动态 JOIN | ⛔ Frozen，不等同 Business Relationship |
| BusinessEntity | 无直接等价物 | 🔴 新增 |
| EntityKey | 无独立业务实体身份 Contract | 🔴 新增 |
| EntityAttribute | 无独立业务实体属性 Contract | 🔴 新增 |
| EntityMetric | QueryMetric / Metric Resolution 可提供基础 | 🟡 新增 Entity 语义层 |
| EntityRelationship | QueryJoin / Join Resolution 不等价 | 🔴 新增 |
| PhysicalBinding | 无独立 Contract，已有 Metadata Identity 可承载 | 🟡 新 Contract |
| Entity Resolution | 当前无独立 Entity 层 | 🔴 新增 |

已审计：

```text
Models/Metadata/MetadataTable.cs
Models/Metadata/MetadataColumn.cs
Models/Metadata/MetadataSemantic.cs
Models/BI/QueryPlan.cs
Models/BI/QueryJoin.cs
```

冻结结论：

> Business Entity ≠ MetadataTable；Business Relationship ≠ QueryJoin；Business Entity 不直接生成 SQL；Phase 3.1 不修改 Phase 2.7 Frozen Contract。

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

核心规则：

- `BusinessKey` 是稳定业务身份，不是数据库表名。
- `BusinessEntity ≠ MetadataTable`。
- Entity 可通过多个 PhysicalBinding 映射多个物理对象。
- Entity 只负责业务语义身份，不保存 SQL。
- Entity Resolution 后才能进入 Physical Binding / QueryPlan Mapping。

3.1.2 最终结论：**PASS。BusinessEntity Contract 作为后续 EntityKey / Attribute / Metric / Relationship 的父级语义边界。**

---

# 6. 3.1.3 Entity Key Contract

状态：**✅ PASS — Contract 已确认**

## 6.1 Contract 定义

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

其中 `Id` 继承现有 `BaseEntity.Id` 的技术身份模型；现有 `MetadataColumn.BusinessKey` 与 `MetadataColumn.IsPrimaryKey` 作为 Physical Metadata 的事实来源，不被重新定义。fileciteturn61file0 fileciteturn63file0

## 6.2 字段职责

| 字段 | 职责 | 规则 |
|---|---|---|
| Id | Key Contract 技术身份 | EntityKey 自身唯一标识 |
| BusinessEntityId | 所属 BusinessEntity | 必须指向稳定 Entity 身份 |
| Name | Key 语义名称 | 如 CustomerId / SupplierCode |
| DisplayName | 业务展示名称 | 面向业务语义层 |
| Description | Key 业务说明 | 用于 Resolution / Explainability |
| IsPrimary | 是否为实体主业务 Key | 同一 Entity 原则上只有一个 Primary Key Contract；复合 Key 另行表达 |
| KeyType | Key 类型 | 区分 Identifier / Code / NaturalKey 等语义，不等同数据库类型 |
| PhysicalBindings | 物理映射集合 | 一个业务 Key 可映射多个 MetadataColumn |

## 6.3 核心设计结论

### A. Business Key 与 Physical Key 分离

```text
BusinessEntityKey
       ↓
Business Identity
       ↓
PhysicalBinding
       ↓
MetadataColumn
```

现有 `MetadataColumn.BusinessKey` 是字段级 Physical Identity，用于跨数据源唯一定位；它**不是** Phase 3 的 BusinessEntityKey。其注释明确指出该字段不是数据库主键，`Id` 仍是 EF Core 主键。fileciteturn61file0

### B. 不复制 Physical Identity

Phase 3 不重新设计：

```text
DataSourceId
Schema
TableName
ColumnName
MetadataColumn.Id
MetadataColumn.BusinessKey
```

而是通过 PhysicalBinding 关联现有 Metadata。

### C. Primary Key 语义升级但不覆盖物理 PK

```text
MetadataColumn.IsPrimaryKey
        ↓
Physical DB fact

BusinessEntityKey.IsPrimary
        ↓
Business semantic fact
```

二者可以相关，但不能混为同一 Contract。

### D. 支持一个 Entity 多个物理 Key 映射

```text
Customer
  │
  └── CustomerId
       ├── ERP.Customer.CustomerId
       └── CRM.Customer.CustomerCode
```

因此 EntityKey 不允许直接持有单一 `MetadataColumnId` 作为唯一映射。

### E. 复合 Key 暂不引入复杂结构

3.1.3 先通过 `KeyType` + 多个 PhysicalBinding 保留扩展空间；复杂 CompositeKey Contract 不在本 STEP 扩张，避免提前进入 Ontology / Knowledge Graph 范畴。

## 6.4 与 Frozen Phase 2.7 的关系

3.1.3 不修改 QueryPlan、QueryJoin、Evaluator、Golden、Decision Gate、SQL Builder。

EntityKey 只为后续 Entity Resolution 提供稳定业务身份：

```text
User Question
    ↓
Entity Resolution
    ↓
BusinessEntity
    ↓
BusinessEntityKey
    ↓
PhysicalBinding
    ↓
Existing Metadata / QueryPlan
```

## 6.5 3.1.3 最终结论

**PASS。EntityKey Contract 已冻结为“业务身份层”，Physical Metadata 的 `BusinessKey` / `IsPrimaryKey` 只作为物理事实来源，不与 BusinessEntityKey 混用。**

注意：本 PASS 代表 Contract Design 完成，不代表 EntityKey 已实现代码、数据库迁移、Golden 或 Runtime 已完成。

---

# 7. 3.1.4 Entity Attribute Contract

状态：**⏳ NEXT**

---

# 8. 3.1.5 Entity Metric Contract

状态：**⏳**

---

# 9. 3.1.6 Entity Relationship Contract

状态：**⏳**

---

# 10. 3.1.7 Physical Binding Contract

状态：**⏳**

---

# 11. 3.1.8 QueryPlan Mapping Contract

状态：**⏳**

原则：Entity Resolution 产生业务语义结果，再通过 Adapter / Mapping 转换到现有 Frozen QueryPlan Contract，不直接修改 QueryPlan。

---

# 12. 3.1.9 Golden Contract

状态：**⏳**

目标：建立 Entity Resolution、Entity → Metadata Binding、Entity → QueryPlan 的 Golden 覆盖；Phase 2.7 Frozen Golden 只回归，不修改既有 Expected Outcome。

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

待完成最终新增 Model、Interface、Service、DI、Controller / Runtime 调用链及数据库迁移映射。

---

# 15. Phase 3.1 总验收标准

Phase 3.1 只有同时满足以下条件才能 CLOSED：

### Contract

- BusinessEntity
- EntityKey
- EntityAttribute
- EntityMetric
- EntityRelationship
- PhysicalBinding
- QueryPlan Mapping

全部冻结。

### Source

- 逐文件源码审计完成
- 新增 / 复用 / 扩展 / Frozen 范围明确
- Model / Interface / Service / DI / Controller 调用链明确

### Golden

- Entity Resolution Golden
- Entity → Metadata Binding Golden
- Entity → QueryPlan Golden
- Phase 2.7 Golden Regression PASS

### Runtime

- Entity Resolution PASS
- Physical Binding PASS
- Entity → QueryPlan PASS
- Phase 2.7 Runtime Regression PASS

### Engineering

- Build PASS
- Startup PASS
- 无 Contract Drift
- Plan / Source / Golden / Runtime 一致

最终才能：

```text
Phase 3.1
    CLOSED / FROZEN
```

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
        └── 3.1.4 EntityAttribute   ⏳ NEXT
```

下一动作：**3.1.4 Entity Attribute Contract**。
