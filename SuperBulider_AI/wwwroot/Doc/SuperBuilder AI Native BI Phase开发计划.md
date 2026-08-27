# SuperBuilder AI Native BI
# Phase开发计划（正式版）

版本：v3.0  
更新日期：2026-08-27  
源码基线：GitHub `master`  
冻结基线：**Phase 2.7 — CLOSED / FROZEN**  
当前阶段：**Phase 3.1 — Business Entity Model**

> STEP 规则：Source Audit → Contract → Implementation → Verification → 最终结论 → 更新本计划 → 回读 master 验证，完成后才能进入下一 STEP。

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
3.1.12.2 EF Core Mapping  🟡 CODED / VERIFICATION PENDING
```

# 3.1.12.2 EF Core Mapping

## 3.1.12.2.1 多动态数据库兼容性 Contract

**结论：不冲突。**

Phase 3.1 Entity Persistence 只持久化到 SuperBuilder 自己的 Metadata DB；动态业务数据库仍由 `DataSource` + Metadata Discovery + Runtime Connection Factory 动态访问。

```text
SuperBuilder Metadata DB
 ├── BusinessEntity
 ├── BusinessEntityKey
 ├── BusinessEntityAttribute
 ├── BusinessEntityMetric
 ├── BusinessEntityRelationship
 ├── PhysicalBinding
 ├── DataSource
 ├── MetadataTable
 └── MetadataColumn
             │
             │ logical binding
             ↓
      Dynamic Business DBs
      ├── MySQL
      ├── PostgreSQL
      ├── SQL Server
      └── Oracle / other supported providers
```

禁止：

```text
Entity Migration → Dynamic Business DB
Entity FK         → Dynamic Business DB
Entity Model      → Customer Physical Schema
Entity            → Direct SQL generation
```

允许：

```text
PhysicalBinding.DataSourceId
PhysicalBinding.MetadataTableId
PhysicalBinding.MetadataColumnId
        ↓
SuperBuilder Metadata records
        ↓
Runtime resolves DataSource
        ↓
Dynamic connection
```

因此 Entity Migration 只改变 SuperBuilder Metadata DB Schema，不要求任何客户业务数据库做 Schema Migration。

## 3.1.12.2.2 EF Mapping 实现

新增：

```text
Data/Configurations/Phase31EntityModelConfiguration.cs
```

并在 `SuperBIContext.OnModelCreating()` 中启用：

```csharp
builder.ApplyConfigurationsFromAssembly(
    typeof(BusinessEntityConfiguration).Assembly);
```

`SuperBIContext` 同时增加六个 `DbSet`：

```text
BusinessEntities
BusinessEntityKeys
BusinessEntityAttributes
BusinessEntityMetrics
BusinessEntityRelationships
PhysicalBindings
```

现有 `SuperBIContext` 本身就是平台基础数据、Metadata、AI 语义和向量索引关系的 EF Core Context，因此没有新建第二个 Context。fileciteturn151file0

## 3.1.12.2.3 FK / Relationship

### Tenant → BusinessEntity

```text
Tenant 1 ─── N BusinessEntity
```

`TenantId`：FK，`DeleteBehavior.Restrict`。

### BusinessEntity → Key / Attribute / Metric

```text
BusinessEntity 1 ─── N BusinessEntityKey
BusinessEntity 1 ─── N BusinessEntityAttribute
BusinessEntity 1 ─── N BusinessEntityMetric
```

三者采用 `Cascade`，因为它们属于 Entity 的生命周期。

### BusinessEntity → Relationship

```text
BusinessEntity 1 ─── N SourceRelationships
BusinessEntity 1 ─── N TargetRelationships
```

Source / Target 两条 FK 均采用 `Restrict`，避免删除一个 Entity 时形成关系级联删除链。

### PhysicalBinding → Entity Semantic Owner

`PhysicalBinding` 增加四个可空 Owner FK：

```text
BusinessEntityKeyId
BusinessEntityAttributeId
BusinessEntityMetricId
BusinessEntityRelationshipId
```

数据库 Check Constraint：

```text
ExactlyOneOwner = 1
```

即一条 PhysicalBinding 必须且只能属于一种 Entity Semantic Owner。

对应 Owner 删除采用 `Cascade`：删除语义对象时清理其 Binding；不会删除 Metadata。

## 3.1.12.2.4 Physical Metadata FK

PhysicalBinding 对 SuperBuilder 自己的三类 Metadata Record 建立 FK：

```text
PhysicalBinding.DataSourceId
        → DataSource.Id

PhysicalBinding.MetadataTableId
        → MetadataTable.Id

PhysicalBinding.MetadataColumnId
        → MetadataColumn.Id
```

全部 `DeleteBehavior.Restrict`。

原因：

```text
删除 Entity
    ↓
删除 Binding
    ↓
Metadata 保留
    ↓
Dynamic DataSource 完全不受影响
```

而且现有 MetadataTable 已经以 `DataSourceId` 关联 DataSource，MetadataColumn 以 `MetadataTableId` 关联 MetadataTable。fileciteturn152file0 fileciteturn153file0

## 3.1.12.2.5 Index

已配置：

```text
BusinessEntity
    (TenantId, BusinessKey) UNIQUE

BusinessEntityKey
    (BusinessEntityId, Name) UNIQUE
    (BusinessEntityId, IsPrimary)

BusinessEntityAttribute
    (BusinessEntityId, Name) UNIQUE
    (BusinessEntityId, IsIdentifier)

BusinessEntityMetric
    (BusinessEntityId, Name) UNIQUE

BusinessEntityRelationship
    (SourceEntityId, TargetEntityId, Name) UNIQUE

PhysicalBinding
    (DataSourceId, MetadataTableId, MetadataColumnId, Priority)
    OwnerId + IsActive indexes
    RelationshipId + PhysicalRole + IsActive
```

这些索引属于 SuperBuilder Metadata DB，不会进入动态业务数据库。

## 3.1.12.2.6 Relationship Binding 特别说明

当前 `PhysicalBinding` 每条记录只指向一个 `MetadataColumn`。因此一个 `BusinessEntityRelationship` 的物理关系可以由多条 Binding 表达，例如：

```text
Relationship R
 ├── Binding(SourceKey → DB_A.TableA.ColumnA)
 └── Binding(TargetKey → DB_A.TableB.ColumnB)
```

通过 `PhysicalRole` 区分 Source / Target / JoinKey 等角色。

因此当前模型不需要把 SourceColumnId / TargetColumnId 强行塞进 Relationship Entity，也不要求跨数据库 FK。

后续如果 Golden / Runtime 证明 Join Binding 需要更强的原子 Contract，再单独进入 Relationship Binding Enhancement，不在本次偷偷扩大 Contract。

## 3.1.12.2.7 当前验证状态

已完成源码写入：

```text
Phase31EntityModelConfiguration.cs   ✅
SuperBIContext DbSet                 ✅
SuperBIContext ApplyConfigurations    ✅
PhysicalBinding Owner FK              ✅
Metadata FK                           ✅
Indexes                               ✅
DeleteBehavior                        ✅
Multi-DataSource boundary             ✅
```

但当前 GitHub Commit 没有可用 CI Status，不能据此声称本机 `dotnet build` / EF Migration / Startup 已通过。当前 commit status 查询返回空状态。fileciteturn156file0

因此本步骤状态必须保持：

> **🟡 CODED / VERIFICATION PENDING**

而不是误报为 PASS。

## 3.1.12.2 最终结论

**🟡 实现完成，验证未完成。**

架构上已经确认与多动态数据库兼容，并且 FK、关系、索引、DeleteBehavior 已完成代码配置；但必须经过实际 `dotnet build`、EF Core Migration/ModelSnapshot 检查以及 Runtime 验证后，才能把 3.1.12.2 标记为 PASS。

下一步：**3.1.12.3 Build / EF Migration Verification**。

# 3.1.12 后续顺序

```text
3.1.12.1 Models           ✅ IMPLEMENTED
        ↓
3.1.12.2 EF Mapping       🟡 CODED
        ↓
3.1.12.3 Build / Migration ⏳ NEXT
        ↓
3.1.12.4 Interfaces
        ↓
3.1.12.5 Resolution Services
        ↓
3.1.12.6 QueryPlan Mapping
        ↓
3.1.12.7 DI
        ↓
3.1.12.8 Golden
        ↓
3.1.12.9 Runtime
        ↓
3.1.12.10 Phase 2.7 Regression
```
