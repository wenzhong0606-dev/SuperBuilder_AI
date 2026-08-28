# Phase 2.7-D12 Contract / DI / Namespace 全量源码审计冻结

> 日期：2026-08-25
> 源码基线：GitHub `master`
> 状态：FROZEN
> 本 STEP：只审计，不修改业务源码

## 一、最终审计结论

D12 完成对 Phase 2.7 D07-D11 形成的 Contract 在 Model、Interface、Implementation、Caller、Constructor、DI、Namespace、Controller、Golden/Runtime 边界的全链路审计。

当前 master 的核心问题不是“缺少某一个 DI 注册”，而是 **新 Contract 尚未落地，因此 DI 当前仍绑定旧职责链**。

已确认：`Program.cs` 当前注册 `QueryPlanBuilder`、`IQueryPlanBuilder`、`QueryJoinInferenceService`、`IQueryJoinInferenceService`、`QueryPlanValidator`、`SemanticApplicabilityEvaluator`、`ISqlQueryBuilder/SqlQueryBuilder` 等服务，说明现有运行链仍以旧 QueryPlanBuilder + JoinInference 为实际构造关系。fileciteturn443file0L2-L6

`QueryPlanBuilder` 当前构造函数仍直接依赖 `IMetadataSemanticSearchService`、`IQueryJoinInferenceService` 和 `QueryPlanValidator`，其中 `IQueryJoinInferenceService` 属于 D09 已冻结的 Relation Evidence / 旧自动 JOIN 边界，后续实现必须隔离其 Executable Join 权限。fileciteturn448file0L2-L5

`IQueryPlanBuilder` 已存在且由 `QueryPlanBuilder` 实现，说明无需新建平行 QueryPlanBuilder 接口。fileciteturn452file0L2-L5

QueryPlanBuilder 采用 partial 文件拆分（Search、Helpers、PlanAssembly、FieldResolution、SemanticResolution、Diagnostics 等），因此后续实现必须优先修改现有 partial 责任边界，不新建重复 Builder。fileciteturn445file0L2-L4 fileciteturn445file4L22-L24 fileciteturn445file5L27-L29

## 二、D12 Contract 结论

最终实现必须形成单一 Resolution 真相源：

```text
QueryIntent
   ↓
Semantic Applicability / Resolution
   ↓
Dimension Resolution
   ├── MasterJoin
   ├── DirectKey
   ├── Ambiguous
   └── NotResolved
   ↓
QueryPlan Dimension Binding / QueryPlan.Joins
   ↓
SqlQueryBuilder
```

禁止形成：

```text
QueryPlanBuilder
   └── 重新 Semantic Search / Relation Inference
```

或：

```text
SqlQueryBuilder
   └── 自己猜 JOIN / Dimension Key
```

## 三、最终修改范围：FROZEN

### A. Models / Contract

1. 在现有 `Models.BI` / 相关 Contract 命名空间中扩展 DirectKey / Dimension Binding Model。
2. 不新建第二套 QueryPlan / QueryDimension。
3. Contract 必须表达：
   - ResolutionType
   - ResolutionState
   - ExecutionCapability
   - FactTable
   - FactDataSource
   - DimensionKey
   - DimensionLabel（可选）
   - Evidence / Confidence / Metadata Snapshot Reference
4. MasterJoin 与 DirectKey 必须互斥。

### B. Interfaces

1. 复用 `IQueryPlanBuilder`。
2. 对已有 `IQueryJoinInferenceService` 的职责进行收口：只能提供 Relation Evidence，不得作为 Executable Join 来源。
3. 如果 D07/D10 实现需要独立 Resolver，新增最小 Interface；不得复制 `IMetadataSemanticSearchService`。
4. SQL Builder 继续复用 `ISqlQueryBuilder`。

### C. Services / Callers

1. `SemanticApplicabilityEvaluator` 成为 Dimension Resolution 的决策入口；它必须把 D05 EntityKey + D06 Relation Evidence 转换为明确 Resolution。
2. `QueryPlanSemanticResolutionFactory` 只负责 Resolution → QueryPlan Resolution 的传递，不重新搜索。
3. `QueryPlanBuilder` 只负责已确认 Resolution → QueryPlan 装配；必须隔离旧 `BuildJoinsAsync → IQueryJoinInferenceService → QueryPlan.Joins` Executable 旁路。
4. `SqlQueryBuilder` 只消费 QueryPlan，不调用 Semantic / Relation Resolver。
5. `QueryPlanValidator` 只验证物理一致性，不推理 Resolution。

### D. DI

`Program.cs` 是当前正式 DI 根。后续只在已有 DI 链中增加/替换必要服务注册：

```text
Semantic Applicability
    ↓
Dimension Resolution Contract
    ↓
QueryPlan Resolution Factory
    ↓
QueryPlanBuilder
    ↓
ISqlQueryBuilder
```

必须保持 Scoped 生命周期与当前 BI / Metadata 服务一致；不得无理由改 Singleton。现有 `IGoldenBaselineRegistry` / `IGoldenBaselinePersistence` 的 Singleton 生命周期不属于 D12 修改范围。fileciteturn443file0L2-L6

### E. Namespace

统一遵循当前项目命名空间：

```text
SuperBuilder_AI.Models.*
SuperBuilder_AI.Interfaces.*
SuperBuilder_AI.Services.BI.*
SuperBuilder_AI.Services.BI.Evaluation.*
```

不得引入第二套旧 `SuperBulider_AI.*` namespace。目录名称历史拼写 `SuperBulider_AI` 不等于 C# namespace 可以随意漂移。

## 四、禁止修改范围：FROZEN

- 不新建平行 `IQueryPlanBuilder` / `QueryPlanBuilder`。
- 不复制 Metadata Semantic Search Service。
- 不让 SQL Builder 调用 Resolver。
- 不让 QueryPlanBuilder 自己重新推理 Relation。
- 不把 `QueryJoinCandidate` 直接转为 Executable Join。
- 不把 DirectKey 转成 QueryJoin。
- 不修改 GQ-011 Ranking Contract。
- 不修改 Evaluator / Golden Contract 来掩盖实现缺失。
- 不跨 DataSource / Tenant Federation。
- 不硬编码物料、供应商主表或字段。
- 不修改 Golden Dataset 本身。
- 不新建独立 Test Project。

## 五、兼容性影响矩阵：FROZEN

| 范围 | 预期 | 强制要求 |
|---|---|---|
| GQ-001 | 单表 Metric | 保持 PASS |
| GQ-002 | EntityCount | 不改变 EntityCount Contract |
| GQ-003/005/009 | Dimension / Filter | Resolution 不得导致已有 Filter/Metric 漂移 |
| GQ-006 | 当前无 Master | 稳定 DirectKey 才可执行；Evidence 不足仍 BLOCK |
| GQ-010 | Dimension + Ranking | DirectKey 不得破坏 Order / Limit |
| GQ-011 | Detail Ranking，无 Dimension | **必须完全绕过 Dimension Resolution，继续 PASS** |
| MasterJoin | 有稳定 Relation | 只能由 Resolution 产生 QueryPlan.Joins |
| DirectKey | 无 Master、有稳定 Fact Key | 不产生 Joins |
| Ambiguous | 多候选 | BLOCK |
| NotResolved | 无稳定绑定 | BLOCK |
| Future Metadata Snapshot | 新增主表/关系 | 允许重新解析 DirectKey → MasterJoin |

## 六、最终实现顺序冻结

D12 后的第一轮源码实现不得拆成“先改 Factory、再临时改 Builder、再回来改 DI”。必须按以下一次性变更设计执行：

```text
1. Models / Contract
        ↓
2. Interfaces
        ↓
3. Resolution / Factory
        ↓
4. QueryPlan Binding
        ↓
5. QueryPlanBuilder 旧 JOIN 旁路隔离
        ↓
6. SqlQueryBuilder 双路径
        ↓
7. Validator
        ↓
8. Program.cs DI
        ↓
9. Build
        ↓
10. Controller Runtime
```

具体文件清单以实现前的冻结修改清单为准；任何超出 D09-D12 冻结范围的文件必须重新进行 Contract 审计，不得边改边扩范围。

## 七、D12 冻结

**D12 = FROZEN。**

**功能实现状态：NOT IMPLEMENTED。**

冻结的是：Contract、Interface、Caller、DI、Namespace、职责边界、最终修改范围、禁止修改范围和兼容性矩阵。

下一步：

> **D13 — Release Build / 首轮实现前 Build 基线验证**

D13 之后才能依据 D09-D12 的冻结范围形成一次性源码修改提交；不得在 D13 阶段自行扩大功能范围。
