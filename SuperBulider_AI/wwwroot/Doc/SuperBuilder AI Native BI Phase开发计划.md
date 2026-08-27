# SuperBuilder AI Native BI
# Phase开发计划（正式版）

版本：v3.0  
更新日期：2026-08-27  
源码基线：GitHub `master`  
冻结基线：**Phase 2.7 — CLOSED / FROZEN**  
当前阶段：**Phase 3.1 — Business Entity Model**

> STEP 规则：Source Audit → Contract → 最终结论 → 更新本计划 → 回读 master 验证，完成后才能进入下一 STEP。

## Phase 3.1 当前状态

```text
3.1.1 Source Audit        ✅ PASS
3.1.2 BusinessEntity      ✅ PASS
3.1.3 EntityKey           ✅ PASS
3.1.4 EntityAttribute     ✅ PASS
3.1.5 EntityMetric        ✅ PASS
3.1.6 Relationship        ✅ PASS
3.1.7 PhysicalBinding     ✅ PASS
3.1.8 QueryPlanMapping    ✅ PASS
3.1.9 Golden Contract     ✅ PASS
3.1.10 Runtime Design     ✅ PASS
3.1.11 Source Mapping     ✅ PASS
3.1.12.1 Models           ✅ IMPLEMENTED
3.1.12.2 EF Core Mapping  ⏳ NEXT
```

## 3.1.12.1 Entity Models Implementation

**✅ PASS — 六个 Phase 3.1 Entity Model 已实际写入 master。**

新增源码：

```text
Models/BI/Entity/BusinessEntity.cs
Models/BI/Entity/BusinessEntityKey.cs
Models/BI/Entity/BusinessEntityAttribute.cs
Models/BI/Entity/BusinessEntityMetric.cs
Models/BI/Entity/BusinessEntityRelationship.cs
Models/BI/Entity/PhysicalBinding.cs
```

### Contract 对齐

```text
BusinessEntity
 ├── TenantId
 ├── BusinessKey
 ├── Name / DisplayName
 ├── Description
 ├── BusinessDomain
 ├── SemanticText
 └── Status

BusinessEntityKey
 ├── BusinessEntityId
 ├── Name / DisplayName
 ├── Description
 ├── IsPrimary
 └── KeyType

BusinessEntityAttribute
 ├── BusinessEntityId
 ├── Name / DisplayName
 ├── Description
 ├── SemanticType
 ├── IsNullable
 └── IsIdentifier

BusinessEntityMetric
 ├── BusinessEntityId
 ├── Name / DisplayName
 ├── Description
 ├── SemanticType
 ├── Aggregation
 └── IsCalculated

BusinessEntityRelationship
 ├── SourceEntityId
 ├── TargetEntityId
 ├── Name / DisplayName
 ├── Description
 ├── RelationshipType
 ├── Cardinality
 └── IsRequired

PhysicalBinding
 ├── DataSourceId
 ├── MetadataTableId
 ├── MetadataColumnId
 ├── PhysicalRole
 ├── BindingType
 ├── Priority
 └── IsActive
```

所有模型继承现有 `BaseEntity`，因此沿用项目已有 `Id` / `CreatedTime` Persistence Identity。fileciteturn140file0

`MetadataTable` 当前仍然以 `DataSourceId`、Table 信息和 `MetadataColumn` 集合表达 Physical Metadata，因此 Phase 3 Entity Model 不复制这些 Physical Facts。fileciteturn142file0

### 3.1.12.1 边界检查

- Entity 模型不保存 SQL。
- Entity 模型不生成 QueryPlan。
- Entity 模型不执行数据库查询。
- EntityKey / Attribute / Metric / Relationship 通过 `PhysicalBinding` 指向 Physical Metadata。
- Relationship 同时保存 Source / Target Entity 语义。
- 不修改 Phase 2.7 QueryPlan / Evaluator / SQL Builder。

### 3.1.12.1 最终结论

**PASS。Model Contract 已从“设计存在”进入“源码实际存在”。**

本步骤只完成 Model，不提前声称 EF Core、DI、Resolution、Golden、Runtime、Build、Startup 完成。

下一步：**3.1.12.2 EF Core Mapping**。
