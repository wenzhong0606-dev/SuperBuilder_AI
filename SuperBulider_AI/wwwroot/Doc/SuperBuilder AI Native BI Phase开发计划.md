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
  ├── 3.1.9 Golden Contract            ✅ PASS
  ├── 3.1.10 Runtime Verification     ✅ PASS
  ├── 3.1.11 Source Implementation Map ✅ PASS
  └── 3.1.12 Source Implementation    ⏳ NEXT
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
 Golden Contract
          ↓
 Runtime Verification
          ↓
 Frozen QueryPlan / Evaluator / SQL Builder
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

# 3.1.1 ～ 3.1.10 已冻结结论

**3.1.1 Source Audit、3.1.2～3.1.8 Contract、3.1.9 Golden Contract、3.1.10 Runtime Verification Design 全部 PASS。**

核心设计保持：

```text
Business Entity Semantic
        ↓
Entity Resolution
        ↓
PhysicalBinding
        ↓
QueryPlan Mapping
        ↓
Frozen QueryPlan
        ↓
Existing Evaluator / SQL Builder
```

Golden Runtime 复用现有 `GoldenDatasetRuntimeService`、`GoldenDatasetRunner`、`GoldenDatasetRegressionEvaluator` 和 `GoldenDatasetRuntimeController`，不重造 Golden Engine。现有 Runtime Pipeline 为 Applicability → Gate → Query Understanding → Semantic Resolution → QueryPlanBuilder → ContextBuilder → Validation/Repair → Confidence → Calibration。Runner 不直接生成 SQL。

---

# 3.1.11 Source Implementation Mapping

**✅ PASS — 源码落地映射审计完成；尚未开始修改 Entity 源码。**

> 本项 PASS 的含义是“已经完成代码级落点设计与缺口确认”，不是“Business Entity 已实现”。真正代码实现从 3.1.12 开始。

## 3.1.11.1 Source Audit 结论

截至当前 `master`，源码中**尚未存在**以下 Phase 3.1 Entity Model / Resolution 实体实现：

```text
BusinessEntity
BusinessEntityKey
BusinessEntityAttribute
BusinessEntityMetric
BusinessEntityRelationship
PhysicalBinding
EntityResolution / Mapping Adapter
```

因此不能把已有 Metadata / QueryPlan 类冒充 Phase 3 Entity Model。仓库当前已有成熟的 Metadata、QueryPlan、Evaluation、Golden Runtime 基础设施，可作为 Phase 3.1 的 Frozen 下游。

代码审计报告同时确认当前仓库为 206 个源文件、约 30,102 行 C#，核心 QueryPlan / Evaluation / Golden 链路已有完整实现；因此本阶段应采用增量新增，而不是重构已有核心链路。fileciteturn132file0

## 3.1.11.2 Model Mapping

| Contract | 目标源码位置 | 当前状态 | 3.1.12 动作 |
|---|---|---|---|
| BusinessEntity | `Models/BI/Entity/BusinessEntity.cs` | 不存在 | 新增 |
| BusinessEntityKey | `Models/BI/Entity/BusinessEntityKey.cs` | 不存在 | 新增 |
| BusinessEntityAttribute | `Models/BI/Entity/BusinessEntityAttribute.cs` | 不存在 | 新增 |
| BusinessEntityMetric | `Models/BI/Entity/BusinessEntityMetric.cs` | 不存在 | 新增 |
| BusinessEntityRelationship | `Models/BI/Entity/BusinessEntityRelationship.cs` | 不存在 | 新增 |
| PhysicalBinding | `Models/BI/Entity/PhysicalBinding.cs` | 不存在 | 新增 |

Model 只表达业务语义和 Mapping，不保存 SQL，不复制 Metadata Physical Facts。

## 3.1.11.3 Interface Mapping

建议接口边界：

```text
IBusinessEntityResolver
IPhysicalBindingResolver
IEntityQueryPlanMapper
```

职责分别为：

```text
IBusinessEntityResolver
    Business Semantic → Entity Resolution

IPhysicalBindingResolver
    Entity Semantic → Valid PhysicalBinding

IEntityQueryPlanMapper
    Resolved Entity → Existing QueryPlan objects
```

接口不得把 SQL 生成、数据库执行、LLM Prompt 责任混入 Entity Model。

## 3.1.11.4 Service Mapping

目标新增服务边界：

```text
Services/BI/Entity/
├── BusinessEntityResolver.cs
├── PhysicalBindingResolver.cs
└── EntityQueryPlanMapper.cs
```

推荐调用链：

```text
BusinessEntityResolver
        ↓
PhysicalBindingResolver
        ↓
EntityQueryPlanMapper
        ↓
QueryPlanBuilder / Frozen Runtime
```

不得让 `EntityQueryPlanMapper` 自己重新执行自由语义搜索；它只消费已确认 Resolution / Binding。

## 3.1.11.5 Existing Source Reuse

现有 `QueryPlanBuilder`、`QueryPlanEvaluator`、`QueryPlanBuilder.SemanticResolution` 是 Phase 2.7 下游，不重写。仓库已经存在这些真实源码文件。fileciteturn127file0 fileciteturn127file1 fileciteturn127file2

现有 `SuperBIContext` 是 EF Core 数据上下文；因此 Entity 持久化应扩展该 Context，而不是新建第二个 DbContext。fileciteturn128file0

## 3.1.11.6 DI Mapping

当前 `Program.cs` 已统一注册 QueryPlan、Golden、Evaluation 等服务，并已有 `IQueryPlanBuilder → QueryPlanBuilder` 等注册模式。fileciteturn133file0

3.1.12 只增加 Entity Resolver / Binding Resolver / Mapping Adapter 的 DI 注册；不改变 Frozen QueryPlan / Golden / SQL Builder 注册。

## 3.1.11.7 Controller Mapping

**本阶段不新增 Entity Controller 作为 Runtime 必需入口。**

Entity Runtime 应由现有 Golden Runtime / BI 主链路消费。若后续需要 Entity 管理 API，应单独作为管理面设计，不得把 HTTP Controller 变成 Entity → SQL 的执行层。

现有 `GoldenDatasetRuntimeController` 继续保持 HTTP 薄层。

## 3.1.11.8 EF Core / Database Mapping

当前仓库已有 `SuperBIContext` 和历史 Migration / ModelSnapshot。fileciteturn128file0 fileciteturn128file1

3.1.12 如果确认 Entity 需要持久化，则新增单一 Migration，至少覆盖：

```text
BusinessEntity
BusinessEntityKey
BusinessEntityAttribute
BusinessEntityMetric
BusinessEntityRelationship
PhysicalBinding
```

外键必须保持 Entity → 子对象 → Binding 的关系完整性；PhysicalBinding 到 MetadataTable / MetadataColumn / DataSource 的引用必须可验证。

在没有实际 Migration 生成和 Build 验证前，不得声称数据库层完成。

## 3.1.11.9 Golden Mapping

新增 Golden Case 不修改 Phase 2.7 Frozen Case：

```text
Phase 3.1 Golden
├── Entity Positive
├── Entity Negative
├── Entity Ambiguous
├── Entity Unresolved
├── Entity → Metadata Binding
├── Entity → QueryDimension
├── Entity → QueryFilter
├── Entity → QueryMetric
├── Entity → QueryJoin
└── EntityKey → Table/Binding
```

Phase 2.7 Golden 只做 Regression。

## 3.1.11.10 Source Modification Boundary

**允许新增：**

```text
Models/BI/Entity/*
Interfaces/BI/Entity/*
Services/BI/Entity/*
必要的 EF Core Mapping / Migration
Phase 3.1 Golden Cases
```

**谨慎修改：**

```text
Program.cs
SuperBIContext.cs
现有 Golden Runtime Service
```

仅允许增加依赖接入，不改变既有 Frozen 行为。

**默认禁止修改：**

```text
QueryPlan.cs
QueryDimension.cs
QueryMetric.cs
QueryFilter.cs
QueryJoin.cs
QueryPlanEvaluator.cs
SqlQueryBuilder.cs
Golden Expected Outcome
```

除非后续 Source Audit 发现 Contract 编译/运行确实要求最小兼容性修改，并且必须单独记录原因与 Regression 结果。

## 3.1.11.11 3.1.11 最终结论

**PASS。Source Implementation Mapping 已冻结。**

当前 master 的真实状态是：

```text
Phase 3.1 Entity Code
        ❌ 尚未实现

Phase 2.7 Frozen Runtime
        ✅ 已存在

Golden Runtime
        ✅ 已存在

EF Core Context
        ✅ 已存在

DI / Runtime Integration Point
        ✅ 已明确
```

因此从现在开始，**第一次真正修改 Entity 源码的步骤是 3.1.12，而不是 3.1.11。**

---

# 3.1.12 Source Implementation

**⏳ NEXT — 开始真正编码。**

严格顺序：

```text
3.1.12.1 Models
      ↓
3.1.12.2 EF Core Mapping
      ↓
3.1.12.3 Interfaces
      ↓
3.1.12.4 Resolution Services
      ↓
3.1.12.5 QueryPlan Mapping Adapter
      ↓
3.1.12.6 DI
      ↓
3.1.12.7 Golden Cases
      ↓
3.1.12.8 Build
      ↓
3.1.12.9 Runtime
      ↓
3.1.12.10 Phase 2.7 Regression
```

每一步必须遵循：

```text
修改源码
  ↓
Build
  ↓
Golden
  ↓
Runtime
  ↓
记录结果
  ↓
更新本开发计划
```

禁止一次性修改所有层后再统一猜测问题。

---

# 4. Phase 3.1 总验收

只有全部满足才允许 CLOSED / FROZEN：

- 3.1 Contract 全部 PASS
- Source Mapping PASS
- Entity Model 实际实现
- EF Core Migration PASS（如持久化）
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
        ├── 3.1.9 Golden Contract     ✅ PASS
        ├── 3.1.10 Runtime Design     ✅ PASS
        ├── 3.1.11 Source Mapping     ✅ PASS
        └── 3.1.12 Implementation     ⏳ NEXT
```

**当前唯一下一动作：3.1.12 Source Implementation。**
