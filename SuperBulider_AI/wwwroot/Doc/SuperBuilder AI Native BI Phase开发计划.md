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
3.1.12.1 Models           ✅ PASS
3.1.12.2 EF Core Mapping  ✅ PASS
3.1.12.3 DB Migration     ✅ PASS
3.1.12.4 Interfaces       🟡 CODED / VERIFICATION PENDING
```

# 3.1.12.3 Database Migration / Schema Verification

## 3.1.12.3.1 实际验证结果

用户本地已确认：

```text
EF Migration              ✅ SUCCESS
Database Update           ✅ SUCCESS
Build                     ✅ SUCCESS
Application Startup       ✅ SUCCESS
```

此前 SQL Server Error 1785 已通过调整 `PhysicalBinding` Owner FK 的 DeleteBehavior 修复：

```text
BusinessEntityKey        → PhysicalBinding   NoAction
BusinessEntityAttribute  → PhysicalBinding   NoAction
BusinessEntityMetric     → PhysicalBinding   NoAction
BusinessEntityRelationship → PhysicalBinding NoAction
```

同时保持：

```text
BusinessEntity → Key / Attribute / Metric   Cascade
BusinessEntity → Relationship                Restrict
PhysicalBinding → DataSource                 Restrict
PhysicalBinding → MetadataTable              Restrict
PhysicalBinding → MetadataColumn             Restrict
```

## 3.1.12.3.2 Multi-DataSource Schema Boundary

Migration 只更新 SuperBuilder Metadata DB。

```text
SuperBuilder Metadata DB
        │
        ├── BusinessEntities
        ├── BusinessEntityKeys
        ├── BusinessEntityAttributes
        ├── BusinessEntityMetrics
        ├── BusinessEntityRelationships
        └── PhysicalBindings

        ↓ logical binding

Dynamic Business DBs
        ├── MySQL
        ├── PostgreSQL
        ├── SQL Server
        └── Other supported providers
```

禁止 Entity Migration 修改动态业务数据库 Schema。

**结论：3.1.12.3 PASS。**

---

# 3.1.12.4 Interfaces

## 3.1.12.4.1 Existing Interface Boundary Audit

当前 Phase 2.7 已存在并继续冻结的接口包括：

```text
IQueryPlanBuilder
IQueryPlanContextBuilder
IQueryPlanDecisionGate
IQueryPlanConfidenceService
IQueryPlanExplainabilityService
IQueryPlanRepairService
IQueryPlanValidationPipeline
IQueryJoinInferenceService
IDimensionResolutionEvidenceService
IBIConversationService
```

现有 `IQueryPlanBuilder` 已经接受 `QueryPlanSemanticResolution`，这是 Phase 2.7 的下游稳定绑定入口，因此 Phase 3 不重新定义 QueryPlan Contract。fileciteturn174file0

`IDimensionResolutionEvidenceService` 当前明确只提供 Dimension 物理证据，不生成 SQL、不修改 QueryPlan.Joins，因此保持 Phase 2.7 Frozen Boundary。fileciteturn178file0

`IQueryJoinInferenceService` 当前已经负责动态 JOIN 候选推断，因此 Phase 3 Entity Relationship 不直接替换该接口，而通过后续 Mapping 层提供稳定语义输入。fileciteturn196file0

## 3.1.12.4.2 新增接口 Contract

### IBusinessEntityService

路径：

```text
Interfaces/BI/Entity/IBusinessEntityService.cs
```

职责：

```text
Business Entity CRUD / Query
        ↓
SuperBuilder Metadata DB
```

禁止：

```text
BusinessEntityService
        ↓
Dynamic Business DB
```

接口：

```text
GetAsync(tenantId, id)
ListAsync(tenantId)
CreateAsync(entity)
UpdateAsync(entity)
DeleteAsync(tenantId, id)
```

### IPhysicalBindingResolver

路径：

```text
Interfaces/BI/Entity/IPhysicalBindingResolver.cs
```

职责：

```text
BusinessEntity
        ↓
PhysicalBinding
        ↓
DataSource / MetadataTable / MetadataColumn
```

只读取 SuperBuilder Metadata DB 中的 Binding Record，不直接建立动态数据库连接。

接口：

```text
ResolveAsync(
    tenantId,
    dataSourceId,
    businessEntityId)
```

### IEntityQueryPlanMapper

路径：

```text
Interfaces/BI/Entity/IEntityQueryPlanMapper.cs
```

职责：

```text
Business Entity Semantic Model
        ↓
QueryPlanSemanticResolution
        ↓
现有 QueryPlanBuilder
```

它不生成 SQL、不执行 SQL、不改变 Phase 2.7 QueryPlan Contract。当前 QueryPlan 的 Resolution 模型已经明确承担 Metric / Filter / Dimension / Table / Order 的稳定物理解析结果。fileciteturn184file0

## 3.1.12.4.3 Dynamic Database Compatibility Contract

三个接口均遵守：

```text
Tenant Scope
     ↓
SuperBuilder Metadata DB
     ↓
Semantic Entity / Binding Resolution
     ↓
QueryPlanSemanticResolution
     ↓
Existing Phase 2.7 QueryPlan
     ↓
DataSourceConnectionFactory
     ↓
Dynamic Business DB
```

不得出现：

```text
Entity Interface
    ↓
EF DbContext
    ↓
Customer Business DB
```

现有 `DataSource` 本身保存 `TenantId`、`DbType`、`ConnectionString` 并代表业务数据库连接，因此 Entity Contract 通过 `DataSourceId` 间接进入动态数据源，而不是把连接信息塞进 Entity。fileciteturn179file0

## 3.1.12.4.4 当前源码结果

已写入 master：

```text
IBusinessEntityService        ✅
IPhysicalBindingResolver      ✅
IEntityQueryPlanMapper        ✅
```

对应提交：

```text
c25c975b3467ea363fbc1e1cb77dffad33d32380
4d01359965de00917da960f8dd3335bf5cf76a37
9d17cff6c6fe3727f5f6aaf99d65348117bef199
```

当前仅完成 Interface Contract，不声称已有实现、DI、Runtime 或 Golden 通过。

**结论：3.1.12.4 Interface Contract 已完成源码写入，但 Verification Pending。**

---

# 3.1.12 后续顺序

```text
3.1.12.1 Models
        ✅ PASS
        ↓
3.1.12.2 EF Core Mapping
        ✅ PASS
        ↓
3.1.12.3 DB Migration / Schema
        ✅ PASS
        ↓
3.1.12.4 Interfaces
        🟡 CODED / VERIFICATION PENDING
        ↓
3.1.12.5 Resolution Services
        ⏳ NEXT
        ↓
3.1.12.6 QueryPlan Mapping
        ⏳
        ↓
3.1.12.7 DI
        ⏳
        ↓
3.1.12.8 Golden
        ⏳
        ↓
3.1.12.9 Runtime
        ⏳
        ↓
3.1.12.10 Phase 2.7 Regression
        ⏳
```
