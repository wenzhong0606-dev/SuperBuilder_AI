# Phase 2.7 — DimensionAware QueryPlan 开发测试计划

## D10 最终源码 / Contract 审计结论（2026-08-25）

### 一、D10 状态：FROZEN

D10 — DirectKey QueryPlan 全量源码 / Contract 审计已完成。本次以 GitHub `master` 为唯一源码基线，覆盖 QueryDimension、QueryPlan、QueryPlanBuilder.SemanticResolution、QueryPlanSemanticResolutionFactory、SemanticApplicabilityEvaluator、QueryPlanValidator、SqlQueryBuilder、Program.cs / DI，以及 D05 EntityKey、D06 Relation、D07 Dimension Resolution、D08 QueryPlan Dimension Binding、D09 MasterJoin 的输入边界。

本次未修改源码；冻结的是 D10 审计结论、最终修改范围、禁止修改范围、兼容性要求以及 D10 / D11 的职责边界。

### 二、D10 最终结论

当前 master **不存在实际的 DirectKey QueryPlan Contract / 实现路径**。现有 Dimension Resolution 仍然只产生单一物理 Column Binding：`TableId + DataSourceId + ColumnId + Table + Column + SemanticText`；`QueryDimension` 也只有 `MetadataColumnId / SemanticText / ColumnName / Alias / SemanticType`，没有 DirectKey 的 Key / Label / ResolutionType / ExecutionCapability 等信息。fileciteturn401file0L2-L10

`SemanticApplicabilityEvaluator.ResolveDimensionAsync()` 当前通过 Dimension 语义搜索并返回一个候选 Column；它没有识别“当前事实表上的稳定 Dimension Key / Label”，也没有在“没有独立 Master”时建立 DirectKey Resolution。fileciteturn405file0L2-L2

`QueryPlanSemanticResolutionFactory` 当前仅把 `DimensionResolutions` 转换成单 Column `QueryPlanDimensionResolution`；不存在 DirectKey Binding 的转换路径。fileciteturn404file0L2-L10

`QueryPlanBuilder.SemanticResolution` 当前只执行 `ApplyDimensionResolutions()`，将一个 Dimension 绑定到一个 Metadata Column；不存在 DirectKey Join/Key/Label 装配。fileciteturn402file0L2-L2

`QueryPlan` 虽有 `Dimensions` 与 `Joins`，但没有 Dimension Resolution Mode / DirectKey Binding Contract。fileciteturn407file0L2-L10

因此 D10 的核心不是“修一个现有 DirectKey Bug”，而是建立完整的 **DirectKey Contract**，并让它成为与 MasterJoin 并列的、由当前 Metadata Snapshot 动态决定的第二条 Dimension Execution Path。

### 三、动态数据库原则：D10 正式冻结

DirectKey 的含义不是“永远没有主表”，而是：

```text
当前 Metadata Snapshot
        ↓
存在稳定 Dimension Key / Label
        ↓
当前没有可执行 Master Relation
        ↓
ResolutionType = DirectKey
        ↓
FactTable 内直接按 DimensionKey / Label 执行
```

未来新增 DataSource / Table / Column 后重新扫描 Metadata：

```text
Metadata Snapshot N
    ↓
DirectKey

新增数据库 / 主表
    ↓
Metadata Scan + Semantic Refresh + Relation Evidence
    ↓
Metadata Snapshot N+1
    ↓
重新 Resolution
    ↓
MasterJoin
```

因此 DirectKey Resolution 不得持久化成永久事实，也不得阻止未来升级为 MasterJoin。

### 四、D10 正式 Contract

DirectKey 必须至少表达：

```text
ResolutionType
    = DirectKey

ResolutionState
    = Resolved / Ambiguous / NotResolved

ExecutionCapability
    = Executable / NotExecutable

FactTable
FactTableId
FactDataSourceId

DimensionKeyColumn
DimensionKeyColumnId

DimensionLabelColumn
DimensionLabelColumnId（可选）

SemanticText
BusinessMeaning
Confidence / Evidence
MetadataSnapshot / Relation Evidence Reference
```

其中：

- **DimensionKey**：事实表中稳定承载该业务维度的键字段；
- **DimensionLabel**：事实表中可以直接展示 / GROUP BY 的名称字段（如果存在）；
- Key / Label 必须来自当前 Metadata，不允许硬编码字段名；
- DirectKey 不创建 Master Table，不伪造 QueryJoin；
- DirectKey 与 MasterJoin 互斥：同一个 Dimension Resolution 不得同时标记为两种执行路径。

### 五、D10 最终修改范围：FROZEN

1. **DirectKey Binding Contract / Model**
   - 增加正式 DirectKey Resolution / Binding Model；
   - 明确 ResolutionType、ResolutionState、ExecutionCapability、FactTable、Key、Label、Evidence。

2. **Dimension Resolution Contract 扩展**
   - D07 的 Dimension Resolution 必须能够输出 `MasterJoin | DirectKey | Ambiguous | NotResolved`；
   - DirectKey 只在当前 Metadata Snapshot 没有稳定 MasterJoin、但事实表存在稳定 Dimension Key / Label 时成立。

3. **SemanticApplicabilityEvaluator**
   - 不再把“搜索到一个物理 Column”直接等价为 Dimension 可执行；
   - 接入 D05 EntityKey Evidence + D06 Relation Evidence；
   - 在当前 Snapshot 中形成 DirectKey 时返回完整 DirectKey Resolution；
   - MasterJoin、DirectKey、Ambiguous、NotResolved 必须明确区分。

4. **QueryPlanSemanticResolutionFactory**
   - 将 DirectKey Binding 从 Semantic Applicability / Resolution 完整传递到 QueryPlan Resolution；
   - 禁止重新 Semantic Search / Relation 推理。

5. **QueryDimension / QueryPlan Binding**
   - 扩展 QueryDimension，使 QueryPlan 能表达 DirectKey Key / Label / ResolutionType / ExecutionCapability，而不是只有单 Column。

6. **QueryPlanBuilder**
   - 增加 `ApplyDirectKeyBindings()` 或等价的单一职责装配路径；
   - DirectKey 不生成 QueryJoin；
   - DirectKey 只消费已冻结 Resolution；
   - 不在 Builder 再次判断“是否 MasterJoin / DirectKey”。

7. **QueryPlanValidator**
   - 验证 DirectKey Key / Label 是否真实存在于当前 Metadata、属于当前 FactTable、DataSource 一致；
   - 校验 DirectKey 与 MasterJoin 互斥；
   - Validator 只验证，不推理。

8. **Interface / DI**
   - 仅为 DirectKey Contract 所需的 Resolver / Evidence Provider / Binding 传递增加最小闭环；
   - 不复制已有 Semantic Search Service。

### 六、D10 明确不实现

- MasterJoin 最终实现（D09 已冻结）；
- QueryPlan 多表 JOIN SQL（D11）；
- SqlQueryBuilder JOIN SQL 落地；
- 跨独立 DataSource Federation；
- 固定 `material_id`、`material_name`、`supplier_id`、`supplier_name` 等字段名；
- 固定 `material_master`、`supplier_master` 等表名；
- QueryPlanEvaluator / Golden Scoring；
- Ranking / Order / Limit Contract；
- Metadata Scanner 本身的全量 Snapshot Reconciliation 改造。

### 七、D10 禁止修改范围：FROZEN

1. 禁止把“Dimension Search Candidate”直接当成 DirectKey。
2. 禁止通过字段名猜测 `xxx_id / xxx_name` 并绕过 Metadata Evidence。
3. 禁止 DirectKey 自动生成 QueryJoin。
4. 禁止 DirectKey 与 MasterJoin 同时生效。
5. 禁止 QueryPlanBuilder 自行重新 Semantic Search / Relation 推理。
6. 禁止跨 DataSource / Tenant 绑定。
7. 禁止修改 GQ-011 使其进入 Dimension / DirectKey 路径。
8. 禁止修改 Golden Contract、Coverage、Evaluator 以掩盖 DirectKey 缺失。
9. 禁止为了当前数据库结构硬编码物料 / 供应商字段或主表。
10. 禁止修改 SqlQueryBuilder JOIN 生成逻辑；D11 单独负责。

### 八、D10 兼容性影响矩阵：FROZEN

| Case | D10 预期 | 兼容性要求 |
|---|---|---|
| GQ-001 | 无 Dimension | 必须保持原 PASS 路径 |
| GQ-002 | EntityCount | 不改变 EntityCount Contract |
| GQ-003 | Dimension | 有稳定 DirectKey 时进入 DirectKey，否则安全 BLOCK |
| GQ-005 | Dimension + Filter | Filter / Metric 不漂移 |
| **GQ-006** | 物料当前无 Master 时允许 DirectKey | 不得因缺少 Master 被强制 BLOCK；若 DirectKey Evidence 不足仍安全 BLOCK |
| GQ-009 | Dimension + Date Filter | Date Filter 不漂移 |
| **GQ-010** | Dimension + Ranking | Ranking / Order / Limit 必须保持；DirectKey 不能破坏 TopN Contract |
| **GQ-011** | 无 Dimension | **必须继续 PASS，完全不进入 DirectKey** |
| Negative Cases | 不稳定 Dimension | BLOCK，不得猜测 Key |
| Ambiguous | 多个 DirectKey 候选 | BLOCK，不得自动选择 |
| Future Snapshot | 新增 Master Relation | 允许从 DirectKey 重新解析为 MasterJoin |

任何既有 PASS Case 因 D10 实现产生回归，立即停止实现验证，先解决兼容性问题，再重新执行受影响 Case 和完整 Golden Regression。

### 九、D10 与 D09 / D11 职责边界

```text
D05
Entity Key Evidence
        ↓
D06
Relation Evidence
        ↓
D07
Dimension Resolution
   ┌────┴────┐
   ↓         ↓
MasterJoin  DirectKey
   ↓         ↓
D09         D10
   └────┬────┘
        ↓
D08 QueryPlan Dimension Binding
        ↓
QueryPlan
        ↓
D11 SQL Builder / SQL Runtime
```

D10 只负责 DirectKey 如何成为 QueryPlan 可执行 Binding；不负责 SQL 如何生成。

### 十、D10 冻结规则

**D10：FROZEN。**

冻结内容：完整源码 / Contract 审计结论、最终修改范围、禁止修改范围、兼容性影响矩阵以及 D10 / D11 职责边界。

功能实现状态：**NOT IMPLEMENTED**。D10 FROZEN 仅表示审计方案冻结，不表示 DirectKey 功能已经实现。

### 十一、下一步

D10 冻结后，必须：

```text
更新阶段开发计划
    ↓
同步主开发计划
    ↓
同步 Runtime 记录（如受影响）
    ↓
确认 GitHub master
    ↓
进入 D11
```

下一步：**D11 — SQL Builder 双路径（MasterJoin / DirectKey）全量源码 / Contract 审计。**

---

## D09 历史冻结摘要

D09 — MasterJoin QueryPlan 全量源码 / Contract 审计已冻结。`QueryJoinInferenceService` 保留为 Relation Evidence Provider；Executable MasterJoin 必须来自 D07/D08 Resolution；Builder 不得重新推理 JOIN；D09 不实现 SQL JOIN。

## D08 历史冻结摘要

D08 — QueryPlan Dimension Binding 全量源码 / Contract 审计已冻结。当前 QueryPlan Dimension 为单 Column Binding；D08 冻结 Resolution → QueryPlan Binding、QueryDimension、DataSource 一致性与 Validator 边界。

## D07 历史冻结摘要

D07 — Dynamic Dimension Resolution 全量源码 / Contract 审计已冻结。Dimension Resolution 必须支持动态 MasterJoin / DirectKey 双路径，Relation 基于当前 Metadata Snapshot 动态计算，不允许硬编码业务主表或跨独立 DataSource Federation。
