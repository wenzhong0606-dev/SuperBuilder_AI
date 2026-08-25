# Phase 2.7 — DimensionAware QueryPlan 开发测试计划

## D08 最终源码 / Contract 审计结论（2026-08-25）

### 一、D08 状态：FROZEN

D08 — QueryPlan Dimension Binding 全量源码 / Contract 审计已完成。本次审计以 GitHub `master` 为唯一源码基线，覆盖：QueryPlan、QueryDimension、QueryTable、QueryJoin、QueryPlanBuilder、QueryPlanBuilder.SemanticResolution、QueryPlanSemanticResolutionFactory、SemanticApplicabilityEvaluator、QueryPlanValidator、SqlQueryBuilder，以及 D05 EntityKey / D06 Relation / D07 Dimension Resolution 的输入边界。

本次未修改任何源码；冻结的是审计结论、Contract 边界、最终修改范围、禁止修改范围与兼容性要求。

### 二、D08 最终结论

当前 QueryPlan Dimension Binding 仍是“单物理 Column Binding”模型：

```text
QueryPlanSemanticResolution.Dimensions
        ↓
ApplyDimensionResolutions()
        ↓
ValidateColumn()
        ↓
EnsureTable()
        ↓
QueryPlan.Dimensions[i]
        ↓
MetadataColumnId + ColumnName + SemanticText
```

`QueryDimension` 当前只有 `MetadataColumnId / SemanticText / ColumnName / Alias / SemanticType`，无法表达 D07 冻结的 `MasterJoin / DirectKey / ResolutionState / ExecutionCapability / FactTable / DimensionTable / Key / Label / Relation Evidence`。

`QueryPlanSemanticResolutionFactory` 当前只是把 `SemanticApplicabilityResult.DimensionResolutions` 映射成简单 Column Resolution，没有保存 D07 的动态 Dimension Resolution Contract；因此 D08 必须补齐“Resolution → QueryPlan Binding”的正式模型边界，而不是让 QueryPlanBuilder 自行重新推理。

`QueryPlanBuilder.BuildAsync(intent, resolution)` 已正确遵守 C.13.2：只消费 Resolution，不重新进行 Metric / Filter / Dimension Semantic Search。这个 Contract 必须保留。`NormalizeToResolvedTables` 会根据 Resolution 的 TableId 收敛 QueryPlan.Tables，并清理不属于 Resolution 的 Joins，因此新的 Dimension Binding 必须能够稳定提供所属 Fact / Dimension Table 身份，否则后续 Normalize 会产生 Binding 漂移。

当前 `QueryPlanBuilder.BuildResolvedPlanSkeleton` 没有从 Resolution 设置 `QueryPlan.DataSourceId`；这与 `QueryPlanValidator.ValidateBasicStructure` 要求的 `DataSourceId > 0`、且所有 QueryTable.DataSourceId 必须与 QueryPlan.DataSourceId 一致存在潜在 Contract 冲突。该问题属于 D08 的 QueryPlan Binding 基础一致性缺陷，必须在实现阶段修复，不能留到运行期。

当前 `QueryJoin` 已具备左右 Table / Column 的物理连接描述，但它只是 QueryPlan 的 JOIN 模型；D08 不负责决定何时产生 JOIN。MasterJoin 的 QueryPlan 装配属于 D09，DirectKey QueryPlan 路径属于 D10。

当前 `SqlQueryBuilder` 明确拒绝 `QueryPlan.Tables.Count > 1`，并要求多表关系先在 QueryPlan.Joins 中明确建立；因此 D08 不修改 SQL Builder。SQL JOIN 落地属于 D11。

### 三、D08 最终修改范围：FROZEN

#### 3.1 必须修改

1. **QueryPlan Dimension Binding Contract / Model**
   - 扩展 Dimension Binding 所需的正式 Contract；
   - 至少能够表达 `ResolutionType`、`ResolutionState`、`ExecutionCapability`、Fact Table、Dimension Table、Key Column、Label Column、Relation Evidence 引用；
   - 不把 Vector Score 直接等同于物理 Relation。

2. **QueryPlanSemanticResolution / QueryPlanSemanticResolutionFactory**
   - 从 D07 Dimension Resolution 完整传递 Binding Contract；
   - 禁止在 Factory 中重新进行 Semantic Search 或 Relation 推理；
   - 保留现有 Metric / Filter / Order Resolution 行为。

3. **QueryDimension / QueryPlan Dimension Binding**
   - 从“单 Column”升级为可表达 DirectKey / MasterJoin 的 Binding；
   - 保留现有 `MetadataColumnId / SemanticText / ColumnName` 兼容字段，避免既有 Case 的直接序列化 / Evaluator 行为漂移；
   - 新字段必须允许后续 D09 / D10 消费，而不要求 D08 自己创建 JOIN。

4. **QueryPlanBuilder.SemanticResolution**
   - 只消费已经冻结的 Dimension Resolution；
   - 将 Binding 正确写入 QueryPlan；
   - 保证 Fact Table / Dimension Table 的 Table Identity 不被 `NormalizeToResolvedTables` 丢失；
   - 修复 Resolved Plan 的 `QueryPlan.DataSourceId` 来源，使其与当前 Resolution / Execution Context 一致；
   - 不增加第二套 Dimension Resolver。

5. **QueryPlanValidator**
   - 增加对新的 Dimension Binding Contract 的基础结构一致性验证；
   - 验证 Dimension Column、Fact / Dimension Table、DataSource、Binding State 的物理一致性；
   - 不在 Validator 中进行语义搜索或 Relation 推理。

6. **Interface / DI 若因上述 Contract 扩展产生必要变化**
   - 只做最小闭环修改；
   - 不改变已有 Resolver / Builder 的职责边界。

#### 3.2 D08 不实现

- MasterJoin 的最终 QueryPlan JOIN 生成（D09）；
- DirectKey 的最终 QueryPlan 聚合 / 分组路径（D10）；
- SqlQueryBuilder 多表 JOIN SQL 落地（D11）；
- 跨独立 DataSource Federation；
- EntityKey Resolver 本身的业务推理；
- Relation Evidence Provider 的核心算法；
- Ranking / DetailRanking / AggregateRanking；
- QueryPlanEvaluator 评分规则；
- Golden 正向 Case 为通过而修改；
- material_master / supplier_master 等业务主表硬编码。

### 四、D08 禁止修改范围：FROZEN

1. 禁止在 QueryPlanBuilder 中重新执行 Metadata Semantic Search。
2. 禁止在 QueryPlanBuilder 中重新判断 MasterJoin / DirectKey。
3. 禁止让 QueryPlanEvaluator 代替 Dimension Resolver。
4. 禁止让 SqlQueryBuilder 自行猜测 Relation。
5. 禁止删除 `QueryDimension.MetadataColumnId / ColumnName / SemanticText` 等既有字段。
6. 禁止通过修改 GQ-011、Golden Contract、Coverage 或 Gate 来掩盖 Binding 缺陷。
7. 禁止把 Vector / Semantic Score 直接作为 Executable JOIN 依据。
8. 禁止跨 Tenant 或 Disabled DataSource 绑定。
9. 禁止让无 Dimension 的 Case 进入 Dynamic Dimension Binding 新路径。

### 五、D08 兼容性影响矩阵：FROZEN

| Case | 当前 | D08 预期 | 兼容性要求 |
|---|---|---|---|
| GQ-001 | PASS | PASS | 无 Dimension，原路径保持 |
| GQ-002 | 正常 EntityCount | 保持 | EntityCount 不受影响 |
| GQ-003 | BLOCK | 进入冻结 Dimension Binding | Metric Contract 不漂移 |
| GQ-004 | 正常 | 保持 | Filter Contract 不漂移 |
| GQ-005 | BLOCK | 进入 MasterJoin / DirectKey 前的 Binding | Date / Metric 不漂移 |
| GQ-006 | BLOCK | 进入 DirectKey / MasterJoin | 不硬编码物料主表 |
| GQ-007 | 正常 Multi-Metric | PASS | Metric 数量 / Binding 不漂移 |
| GQ-008 | 正常 Distinct EntityCount | PASS | EntityCount 不漂移 |
| GQ-009 | BLOCK | 进入 Dimension Binding | Date Filter 不漂移 |
| GQ-010 | BLOCK | 进入 Dimension Binding | Ranking / Order / Limit 不漂移 |
| **GQ-011** | **PASS** | **PASS** | **无 Dimension，完全不进入新 Binding 路径** |
| GQ-N001 | Negative | Negative | Aggregation 不漂移 |
| GQ-N002 | Negative | Negative | Filter 不漂移 |
| GQ-N003 | Negative / BLOCK | Negative / BLOCK | 错误 JOIN 不得被放宽 |
| GQ-N004 | Negative / BLOCK | Negative / BLOCK | Ranking Direction 不漂移 |
| GQ-N005 | Negative / BLOCK | Negative / BLOCK | Limit 不漂移 |
| GQ-A001 | Ambiguous | Ambiguous / BLOCK | Ambiguity Contract 不漂移 |
| GQ-U001 | BLOCK | BLOCK | 不得绕过 Applicability Gate |

任何既有 PASS Case 因 D08 出现回归，必须立即停止实现验证，先解决兼容性问题，再重新进行完整回归。

### 六、D08 Contract 边界

```text
D07
Dimension Semantic
   ↓
EntityKey / Master / DirectKey Resolution
   ↓
Frozen Dimension Resolution

D08
Frozen Dimension Resolution
   ↓
QueryPlan Dimension Binding
   ↓
QueryPlan.Dimensions
   ↓
Fact / Dimension Table Identity
   ↓
供 D09 / D10 消费

D09
MasterJoin QueryPlan

D10
DirectKey QueryPlan

D11
SqlQueryBuilder JOIN / SQL Runtime
```

D08 不提前实现 D09 / D10 / D11。

### 七、D08 冻结规则

**D08：FROZEN。**

冻结内容：
- 全量源码 / Contract 审计结论；
- 最终修改范围；
- 禁止修改范围；
- 兼容性影响矩阵；
- D09 / D10 / D11 职责边界。

功能实现状态仍为：**NOT IMPLEMENTED**。不得把 D08 FROZEN 解释为 D08 功能已完成。

### 八、下一步

D08 冻结后，必须先同步本阶段计划、主开发计划，并确认 GitHub `master` 文档一致；确认完成后才进入：

**D09 — MasterJoin QueryPlan 全量源码 / Contract 审计。**

---

## D07 历史冻结摘要

D07 — Dynamic Dimension Resolution 全量源码 / Contract 审计已冻结。当前 Dimension Applicability 仍是“语义 → 单物理 Column”，必须通过 D05 EntityKey + D06 Relation + MasterJoin / DirectKey 双路径完成动态 Dimension Resolution。D07 已冻结 EntityKey / Relation / ResolutionState / ExecutionCapability 边界、兼容性 Gate 和禁止修改范围。
