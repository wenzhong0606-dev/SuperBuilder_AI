# SuperBuilder AI Native BI
# Phase开发计划（正式版）

版本：v3.1  
更新日期：2026-08-27  
源码基线：GitHub `master`  
冻结基线：**Phase 2.7 — CLOSED / FROZEN**  
当前阶段：**Phase 3.1 — Business Entity Model**

> STEP 规则：Source Audit → Contract → Implementation → Verification → 最终结论 → 更新本计划 → 回读 master 验证，完成后才能进入下一 STEP。

---

# Phase 3.1 当前状态

```text
3.1.1  Source Audit / Contract Design       ✅ PASS
3.1.2  BusinessEntity                       ✅ PASS
3.1.3  EntityKey                            ✅ PASS
3.1.4  EntityAttribute                      ✅ PASS
3.1.5  EntityMetric                         ✅ PASS
3.1.6  Relationship                         ✅ PASS
3.1.7  PhysicalBinding                      ✅ PASS
3.1.8  QueryPlanMapping                     ✅ PASS
3.1.9  Golden Contract                      ✅ PASS
3.1.10 Runtime Design                       ✅ PASS
3.1.11 Source Mapping                       ✅ PASS

3.1.12 Implementation / Verification
  3.1.12.1 Models                            ✅ PASS
  3.1.12.2 EF Core Mapping                   ✅ PASS
  3.1.12.3 DB Migration / Schema             ✅ PASS
  3.1.12.4 Interfaces                        ✅ PASS
  3.1.12.5 Resolution Services               ✅ PASS
  3.1.12.6 QueryPlan Mapping                 ✅ PASS
  3.1.12.7 DI                                ✅ PASS
  3.1.12.8 Golden Fixture                    ✅ PASS
  3.1.12.9 Runtime / Golden Verification    ✅ PASS
  3.1.12.10 Phase 2.7 Regression             ⏳ PENDING
```

> 注：实际 Runtime 验证已按当前源码实现落地为 `3.1.12.9`；此前对话中使用的 `3.1.12.6-01 ~ 05` 是 Golden Case ID，而不是本计划的层级编号。为避免计划与源码/Runtime 再次漂移，本版本统一按“实施层级 + Case ID”记录。

---

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
BusinessEntityKey          → PhysicalBinding   NoAction
BusinessEntityAttribute    → PhysicalBinding   NoAction
BusinessEntityMetric       → PhysicalBinding   NoAction
BusinessEntityRelationship → PhysicalBinding   NoAction
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

Phase 2.7 已存在并继续冻结的接口包括：

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

Phase 3 不重新定义 Phase 2.7 QueryPlan Contract，而是在其上游提供稳定的 Entity Semantic Resolution。

## 3.1.12.4.2 新增接口 Contract

### IBusinessEntityService

```text
Interfaces/BI/Entity/IBusinessEntityService.cs
```

职责：Business Entity CRUD / Query，仅操作 SuperBuilder Metadata DB。

### IPhysicalBindingResolver

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

### IEntityQueryPlanMapper

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

不生成 SQL、不执行 SQL、不改变 Phase 2.7 QueryPlan Contract。

## 3.1.12.4.3 Dynamic Database Compatibility Contract

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

Entity Contract 通过 `DataSourceId` 间接进入动态数据源，而不是把连接信息塞进 Entity。

## 3.1.12.4.4 Verification Result

```text
IBusinessEntityService        ✅
IPhysicalBindingResolver      ✅
IEntityQueryPlanMapper        ✅
```

Interface Contract、实现注册及后续 Resolution/Mapping 链已完成并通过当前本地 Runtime 验证。

**结论：3.1.12.4 PASS。**

---

# 3.1.12.5 Resolution Services

当前 Entity Resolution 已形成正式链路：

```text
BusinessEntity
      ↓
EntityKey / Attribute / Metric
      ↓
PhysicalBindingResolver
      ↓
DataSource / MetadataTable / MetadataColumn
      ↓
QueryPlanSemanticResolution
```

关键边界：

```text
Resolution
    ✅ 读取 Metadata DB
    ✅ 按 Tenant / DataSource Scope 解析
    ✅ 输出语义到物理的 Resolution
    ❌ 不生成 SQL
    ❌ 不执行动态数据库查询
```

**结论：3.1.12.5 PASS。**

---

# 3.1.12.6 QueryPlan Mapping

`IEntityQueryPlanMapper` 已将 Business Entity Semantic Model 映射为现有 `QueryPlanSemanticResolution`，并继续交给 Phase 2.7 的 QueryPlan 下游。

验证覆盖：

```text
Metric       → Resolution
Dimension    → Resolution
Filter       → Resolution
Table        → Resolution
DataSource   → Scope
```

**结论：3.1.12.6 PASS。**

---

# 3.1.12.7 DI

Entity Contract / Resolution / QueryPlan Mapping 已接入现有应用 DI 链路。

Runtime 已实际通过正式 `IEntityQueryPlanMapper` 执行 Resolution，而不是 Controller 内部复制解析逻辑。

**结论：3.1.12.7 PASS。**

---

# 3.1.12.8 Golden Fixture

## 3.1.12.8.1 Fixture 原则

Golden Fixture 不创建动态业务数据库表，也不修改动态业务数据库真实业务数据。

Fixture 写入范围：

```text
SuperBuilder Metadata DB
        ↓
BusinessEntity
BusinessEntityKey
BusinessEntityAttribute
BusinessEntityMetric
PhysicalBinding
```

## 3.1.12.8.2 当前 Golden Fixture

用户本地已成功创建并运行：

```text
TenantId       = 2
BusinessEntity = Phase 3.1 Golden Customer (#1)
DataSource     = C.13.3 CSV Metadata Fixture (#2)
```

Fixture Key：

```text
phase3.1.golden.customer
```

当前 active PhysicalBinding：

```text
3 active Binding(s)
DataSourceId = 2
```

## 3.1.12.8.3 Multi-DataSource Boundary

Fixture Provisioning 强制验证：

```text
DataSource.TenantId == BusinessEntity.TenantId
MetadataTable.TenantId == BusinessEntity.TenantId
MetadataTable.DataSourceId == DataSource.Id
```

并显式绑定：

```text
PhysicalBinding.DataSourceId
PhysicalBinding.MetadataTableId
PhysicalBinding.MetadataColumnId
```

避免 EF Core 产生 `DataSourceId1 / MetadataTableId1 / MetadataColumnId1` Shadow FK。

**结论：3.1.12.8 PASS。**

---

# 3.1.12.9 Runtime / Golden Verification

## 3.1.12.9.1 验证方式

按照 Phase 3.1 的约束：

```text
不使用 Swagger
不使用 Postman
不创建独立测试工程
```

采用浏览器 Controller 验证：

```text
/evaluation/business-entity/fixture
/evaluation/business-entity
```

数据来自当前真实 SuperBI Metadata DB。

## 3.1.12.9.2 Runtime 主链路

```text
Browser Controller
       ↓
BusinessEntity
       ↓
Entity Key / Attribute / Metric
       ↓
PhysicalBinding
       ↓
DataSource / MetadataTable / MetadataColumn
       ↓
IEntityQueryPlanMapper
       ↓
QueryPlanSemanticResolution
```

Runtime 使用正式 Resolution / Mapping Contract，不绕过服务层直接伪造 QueryPlan。

## 3.1.12.9.3 Golden Runtime 实际结果

用户本地实际执行结果：

```text
Phase 3.1 Golden Runtime Result

TenantId=2
BusinessEntity=Phase 3.1 Golden Customer (#1)
DataSource=C.13.3 CSV Metadata Fixture (#2)

Result: 5 PASS / 0 NOT PASS
```

### G-3.1.12.6-01 — Metric Resolution

```text
PASS
Tables=1
Metrics=1
Dimensions=0
Filters=0
Orders=0
```

验证结论：Metric 能通过 BusinessEntity → PhysicalBinding → DataSource 正确解析，并且 DataSource Scope 正确。

### G-3.1.12.6-02 — Dimension Resolution

```text
PASS
Tables=1
Metrics=0
Dimensions=1
Filters=0
Orders=0
```

验证结论：Dimension 能正确解析，并且 DataSource Scope 正确。

### G-3.1.12.6-03 — Filter Resolution

```text
PASS
Tables=1
Metrics=0
Dimensions=0
Filters=1
Orders=0
```

验证结论：Filter 能正确解析，并且 DataSource Scope 正确。

### G-3.1.12.6-04 — DataSource Isolation

```text
PASS
3 active Binding(s), all DataSourceId=2
```

验证结论：当前 Entity 的 active PhysicalBinding 均限定在所选 DataSource=2，没有跨 DataSource Binding 泄漏。

### G-3.1.12.6-05 — Wrong DataSource

```text
PASS
No active PhysicalBinding found for BusinessEntity '1' in DataSource '3'.
```

验证结论：错误 DataSource 不会发生 fallback，Mapper 正确拒绝跨 DataSource Resolution。

## 3.1.12.9.4 Golden Verification 总结

```text
Metric Resolution       ✅ PASS
Dimension Resolution    ✅ PASS
Filter Resolution       ✅ PASS
DataSource Isolation    ✅ PASS
Wrong DataSource        ✅ PASS

TOTAL                   5 / 5 PASS
```

**结论：3.1.12.9 PASS。**

---

# Phase 3.1 当前最终结论

截至 2026-08-27：

```text
Phase 2.7 Frozen Baseline
        ↓
        ✅ CLOSED / FROZEN

Phase 3.1 Business Entity Model
        ↓
Contract                     ✅
Models                       ✅
EF Mapping                   ✅
Migration                    ✅
Interfaces                   ✅
Resolution                   ✅
QueryPlan Mapping            ✅
DI                           ✅
Golden Fixture               ✅
Runtime / Golden             ✅
        ↓
Golden Cases                 5 / 5 PASS
```

### 可以确认的工程结论

1. Business Entity 已能作为 Phase 2.7 Frozen QueryPlan 的上游语义模型使用。
2. PhysicalBinding 已能把 Business Entity 语义稳定映射到 Metadata Table / Column。
3. Entity Resolution 遵守 Tenant + DataSource Scope。
4. 错误 DataSource 不会发生 fallback。
5. Entity Fixture 不依赖动态业务数据库 Schema Migration。
6. 当前 Runtime 已通过真实 SuperBI Metadata DB 完成 5 个 Golden Case。

### 当前仍未关闭的项目

```text
3.1.12.10 Phase 2.7 Regression    ⏳ PENDING
```

因此：

> **Phase 3.1 的 Business Entity Runtime / Golden Verification 已 PASS，但在完成 Phase 2.7 Regression 前，不宣布整个 Phase 3.1 最终 Frozen。**

---

# 3.1.12 下一步

```text
3.1.12.1 Models
        ✅
        ↓
3.1.12.2 EF Core Mapping
        ✅
        ↓
3.1.12.3 DB Migration / Schema
        ✅
        ↓
3.1.12.4 Interfaces
        ✅
        ↓
3.1.12.5 Resolution Services
        ✅
        ↓
3.1.12.6 QueryPlan Mapping
        ✅
        ↓
3.1.12.7 DI
        ✅
        ↓
3.1.12.8 Golden Fixture
        ✅
        ↓
3.1.12.9 Runtime / Golden
        ✅ 5/5 PASS
        ↓
3.1.12.10 Phase 2.7 Regression
        ⏳ NEXT
```

**下一 STEP：Phase 2.7 Regression。**

Regression 完成并通过后，再执行：

```text
Phase 3.1 Final Audit
        ↓
Plan / Source / Runtime 三方一致性检查
        ↓
Phase 3.1 FREEZE
        ↓
进入 Phase 3.2
```
