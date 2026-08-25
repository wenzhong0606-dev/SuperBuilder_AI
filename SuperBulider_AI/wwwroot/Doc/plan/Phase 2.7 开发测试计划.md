# Phase 2.7 — DimensionAware QueryPlan 开发测试计划

## D09 最终源码 / Contract 审计结论（2026-08-25）

### 一、D09 状态：FROZEN

D09 — MasterJoin QueryPlan 全量源码 / Contract 审计已完成。本次以 GitHub `master` 为唯一源码基线，覆盖 QueryJoin、QueryJoinCandidate、QueryJoinInferenceService、QueryPlanBuilder、QueryPlanBuilder.SemanticResolution、QueryPlan / QueryDimension / QueryTable / QueryJoin、QueryPlanValidator、SqlQueryBuilder，以及 D07 Dimension Resolution、D08 QueryPlan Dimension Binding 的输入边界。

本次未修改源码；冻结的是 D09 审计结论、最终修改范围、禁止修改范围、兼容性要求以及 D09/D10/D11 的职责边界。

### 二、D09 最终结论

当前源码存在两套 JOIN 责任路径：

```text
旧路径
QueryPlanBuilder
    ↓
BuildJoinsAsync
    ↓
QueryJoinInferenceService
    ↓
QueryJoinCandidate
    ↓
QueryPlan.Joins
```

以及 Phase 2.6 C.13.2 的 Resolution 单一真相路径：

```text
Semantic Applicability
    ↓
Dimension Resolution
    ↓
QueryPlanBuilder.BuildAsync(intent, resolution)
    ↓
QueryPlan
```

`QueryJoinInferenceService` 当前应保留为 Relation Evidence Provider，而不能继续作为本次 QueryPlan 的最终 JOIN 决策者。它当前根据字段名称、表名称、数据类型和 MetadataSemantic 计算 Candidate，并采用高阈值“宁可少 JOIN，也不能错误 JOIN”的安全策略；其结果属于 Relation Evidence，不等于本次查询的 Executable MasterJoin。

当前 `QueryJoinCandidate` 仅表达左右表/字段、Confidence、Reason，缺少本次 Dimension Resolution 所需的 FactTable、DimensionTable、FactKey、DimensionKey、Label、ResolutionType、ExecutionCapability、Evidence 等正式 Binding Contract。

因此 D09 的核心不是增强 JOIN 评分算法，而是消除“Candidate 直接进入 QueryPlan.Joins”的旧双路径，建立：

```text
Relation Evidence
        ↓
D07 Dimension Resolution
        ↓
ResolutionType = MasterJoin
        ↓
MasterJoin Binding
        ↓
QueryPlan.Joins
```

QueryPlanBuilder 不得再次自行推理 MasterJoin。

### 三、动态数据库原则

业务数据库是动态接入的，Relation 不允许永久预绑定。当前 Metadata Snapshot 中没有 Master 时，本次查询不得强制 JOIN；若存在稳定 Fact Dimension Key，则由 D10 DirectKey 路径处理。未来新增 DataSource 并重新扫描 Metadata 后，如果形成稳定 Master Relation，同一业务问题可以重新解析为 MasterJoin。

D09 不允许跨独立 DataSource Federation，不允许硬编码 `material_master`、`supplier_master` 等业务主表。

### 四、D09 最终修改范围：FROZEN

1. **MasterJoin Binding Contract / Model**
   - 增加正式 MasterJoin Binding；
   - 至少表达 ResolutionType、ResolutionState、ExecutionCapability、FactTable、DimensionTable、FactKey、DimensionKey、DimensionLabel、Relation Evidence。

2. **D07 Dimension Resolution → D08 QueryPlan Binding**
   - 将 `ResolutionType=MasterJoin` 及其物理关系完整传递到 QueryPlan；
   - Factory / Builder 只消费冻结 Resolution，不重新 Semantic Search、不重新 Relation 推理。

3. **QueryPlan.Joins 装配**
   - 将已确认的 MasterJoin Binding 转换为 `QueryJoin`；
   - 校验 Fact / Dimension Table、Key Column、DataSource、Tenant 与当前 Metadata Snapshot 一致；
   - 防止重复 Join。

4. **QueryJoinCandidate**
   - 保留作为 Relation Evidence；
   - 必要时补充稳定 Evidence 标识/引用，但不得把 Candidate 直接升级为 Executable Join。

5. **QueryPlanBuilder**
   - 移除或隔离“Resolution 路径下自动 BuildJoinsAsync → QueryPlan.Joins”的旧旁路；
   - `BuildAsync(intent, resolution)` 中 MasterJoin 必须只来自 Resolution；
   - 不建立第二套 MasterJoin Resolver。

6. **QueryPlanValidator**
   - 增加 MasterJoin Binding 的物理一致性验证：Table、Column、DataSource、Tenant、Join 两端完整性；
   - 不在 Validator 中推理 Relation。

7. **Interface / DI**
   - 仅因 Contract 传递需要时进行最小闭环修改；保留 `IQueryJoinInferenceService` 的 Evidence Provider 职责，不改变其核心评分算法作为 D09 的必要前提。

### 五、D09 不实现

- DirectKey 最终 QueryPlan 路径（D10）；
- SqlQueryBuilder 多表 JOIN SQL 落地（D11）；
- 跨独立 DataSource Federation；
- EntityKey Resolver 核心算法；
- Relation Evidence Provider 核心评分算法重写；
- Ranking / DetailRanking / AggregateRanking；
- QueryPlanEvaluator；
- Golden 为通过而修改；
- `material_master` / `supplier_master` 等业务主表硬编码。

### 六、D09 禁止修改范围：FROZEN

1. 禁止在 QueryPlanBuilder 中重新进行 Metadata Semantic Search 以决定 MasterJoin。
2. 禁止在 QueryPlanBuilder 中重新判断 MasterJoin / DirectKey。
3. 禁止把 `QueryJoinCandidate` 直接当作 Executable MasterJoin。
4. 禁止让 SqlQueryBuilder 自行猜 Relation。
5. 禁止跨 DataSource / Tenant 绑定。
6. 禁止修改 GQ-011、Golden Contract、Coverage 或 Gate 以掩盖 JOIN 缺陷。
7. 禁止硬编码业务主表。
8. 禁止为了 D09 修改 Ranking / Order / Limit Contract。
9. 禁止删除现有 QueryJoin 物理模型；应在其上建立正式 Binding 来源。

### 七、D09 兼容性影响矩阵：FROZEN

| Case | D09 预期 | 兼容性要求 |
|---|---|---|
| GQ-001 | 保持原路径 | PASS，不产生 MasterJoin |
| GQ-002 | 保持 EntityCount | 不改变 EntityCount Contract |
| GQ-003 | Dimension 解析后决定路径 | Metric / Filter 不漂移 |
| GQ-005 | MasterJoin 或 DirectKey | Date / Metric 不漂移 |
| GQ-006 | 当前无物料主表时不强制 JOIN | 后续可因新 Metadata Snapshot 升级 MasterJoin |
| GQ-009 | Dimension + Date Filter | Date Filter 不漂移 |
| GQ-010 | Dimension + Ranking | Ranking / Order / Limit 必须保持 |
| **GQ-011** | **完全不进入 MasterJoin** | **必须继续 PASS** |
| Negative Cases | 保持安全阻断 | 错误 Relation 不得被放宽 |
| Ambiguous | BLOCK | 不得自动猜测 JOIN |

任何既有 PASS Case 因 D09 实现产生回归，立即停止实现验证，先解决兼容性问题，再重新执行受影响 Case 和完整 Golden Regression。

### 八、D09 Contract 边界

```text
D07
Dimension Resolution
    ↓
D08
QueryPlan Dimension Binding
    ↓
D09
MasterJoin Binding
    ↓
QueryPlan.Joins

D10
DirectKey QueryPlan

D11
SqlQueryBuilder JOIN / SQL Runtime
```

D09 负责“已确认 MasterJoin 如何进入 QueryPlan”，不负责决定 SQL 如何执行。

### 九、D09 冻结规则

**D09：FROZEN。**

冻结内容：全量源码 / Contract 审计结论、最终修改范围、禁止修改范围、兼容性矩阵以及 D10 / D11 职责边界。

功能实现状态仍为：**NOT IMPLEMENTED**。D09 FROZEN 仅表示审计方案冻结，不表示 MasterJoin 功能已经开发完成。

### 十、下一步

D09 冻结后，必须完成：

```text
更新阶段开发计划
    ↓
同步主开发计划
    ↓
确认 GitHub master
    ↓
进入 D10
```

下一步：**D10 — DirectKey QueryPlan 全量源码 / Contract 审计。**

---

## D08 历史冻结摘要

D08 — QueryPlan Dimension Binding 全量源码 / Contract 审计已冻结。当前 QueryPlan Dimension 为单 Column Binding；D08 冻结了 Resolution → QueryPlan Binding Model、QueryDimension、QueryPlanBuilder Semantic Resolution、DataSource 一致性、Validator 以及最小 DI / Interface 修改范围，并明确 D09 负责 MasterJoin、D10 负责 DirectKey、D11 负责 SQL JOIN。

## D07 历史冻结摘要

D07 — Dynamic Dimension Resolution 全量源码 / Contract 审计已冻结。Dimension Resolution 必须支持动态 MasterJoin / DirectKey 双路径，Relation 基于当前 Metadata Snapshot 动态计算，不允许硬编码业务主表或跨独立 DataSource Federation。
