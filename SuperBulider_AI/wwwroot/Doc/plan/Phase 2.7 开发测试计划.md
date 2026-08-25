# Phase 2.7 — DimensionAware QueryPlan 开发测试计划

## STEP-01 / STEP-02 / D05 / D06 审计结论与最终修改范围（2026-08-25）

### 一、审计结论

当前 master 已完成 STEP-01 全量相关源码 / Contract 基线审计、STEP-02 真实 Metadata Dimension Entity Key 审计、D05 Dimension Entity Key Resolver 完整责任链审计，以及 D06 Dynamic Master Table / Relation Detection 全量源码 / Contract 审计。

当前 GQ-011 已 PASS；GQ-006、GQ-010 当前仍在 SemanticApplicabilityGate 因“物料”无法建立稳定 Dimension 物理绑定而 BLOCK。该 BLOCK 不是 GQ-011 Ranking Order 修复产生的回归，而是当前 Dimension Resolution 双路径尚未实现的能力缺口。

### 二、冻结业务规则

Dimension Resolution 必须采用双路径：

```text
Dimension Semantic
      ↓
是否存在稳定关联主表？
  ├─ YES → MasterJoin → JOIN
  └─ NO
       ↓
是否存在事实表稳定 Dimension Key / Label？
  ├─ YES → DirectKey → 不 JOIN，按事实表 Dimension Key / Label 汇总
  └─ NO → NotResolved → BLOCK
```

规则：
1. 禁止硬编码 material_master、supplier_master 等主表。
2. 是否 MasterJoin 必须由当前有效 Metadata Snapshot 中的真实 Metadata / Relation / Entity Key 证据决定。
3. DirectKey 不要求存在独立 Dimension 主表。
4. MasterJoin 必须在 QueryPlan 中形成明确 QueryJoin；SQL Builder 不得自行猜测 JOIN。
5. Ambiguous / NotResolved 必须安全阻断。
6. 新增 DirectKey / MasterJoin 能力不得改变已经 PASS 的既有 GQ Case 行为。
7. 每次修改必须进行兼容性回归，至少覆盖 GQ-011 以及既有正确的 Metric / Filter / Ranking Case。
8. 本规则作为 Phase 2.7 的正式 Contract，不允许针对单一 Golden Case 做硬编码补丁。
9. BusinessKey 仅用于跨 DataSource / Table / Column 的物理字段唯一标识，不得直接视为 Dimension Entity Key。
10. Vector / Semantic 相似度只能作为语义候选证据，不得单独作为物理 Relation / MasterJoin 证据。
11. MasterJoin / DirectKey 是当前 Metadata Snapshot 下的动态 Resolution 结果，不是永久业务事实；新增或失效 DataSource / Table / Column / Relation Evidence 后必须允许重新解析。
12. DataSource 失效后，其 Metadata 不得继续作为有效 Dimension / Relation Candidate。
13. Metadata Relation 与 Executable SQL Join 必须分离建模；Relation Resolved 不等于 SQL Join 可执行。
14. QueryJoinInferenceService 保留为 Relation Evidence Provider，不升级为“大而全”的最终 Relation Resolver。
15. QueryPlanBuilder 只消费已经解析完成的 Relation，不得再次自行猜测 JOIN。
16. SQL Builder 只消费 QueryPlan.Joins 并负责 SQL JOIN 落地，不负责 Relation 推理。
17. 当前 Phase 2.7 不实现跨独立 DataSource 的 Federation；不同 DataSource 即使存在 Metadata Relation，也必须明确记录 ExecutionCapability，不得伪装成普通 SQL JOIN。
18. 无 Dimension 的既有正确 Case 不进入 Dynamic Relation Resolution，确保 GQ-011 等既有 Ranking / Order Contract 保持原路径。

### 三、D05 最终结论：FROZEN

D05 — Dimension Entity Key Resolver 已完成完整源码 / Contract / Metadata 生命周期审计，正式冻结。

D05 最终职责：

```text
Current Metadata Snapshot
        ↓
Dimension Entity Key Evidence
        ↓
DimensionEntityKeyResolution
```

D05 不负责 JOIN、QueryPlan、SQL、Ranking、Evaluator、Metadata 扫描、Semantic 生成或 Vector 生成。

### 四、D05 新增发现：Metadata Lifecycle 独立缺口

当前 `MetadataScannerService` 已确认实现：

```text
DataSource
   ↓
IDataSourceMetadataReader
   ↓
Tables / Columns
   ↓
MetadataTable / MetadataColumn
   ↓
SearchText
   ↓
Batch Semantic
   ↓
Vector
```

但当前扫描逻辑主要是“新增 / 更新 / 缺失补齐型同步”，尚未形成完整 Snapshot Reconciliation：

- 当前数据库已删除的旧 Table 未形成明确失效 / 删除 Contract；
- 当前数据库已删除的旧 Column 未形成明确失效 / 删除 Contract；
- ColumnComment / SearchText 变化时，现有 Semantic 不会自动按内容变化重新生成；
- Semantic / SearchText 变化后的 Vector 不具备完整内容版本感知刷新 Contract；
- 因此未来 Dynamic Relation 必须依赖“当前有效 Metadata Snapshot”，不能直接把现有 Metadata 表中所有历史记录都视为有效候选。

该缺口不并入 D05 Resolver 实现，新增为后续独立 Metadata Lifecycle / Snapshot Reconciliation 任务。

### 五、D06 最终审计结论：FROZEN

D06 — Dynamic Master Table / Relation Detection 已完成 Metadata Scanner、Semantic / Vector、Relation Inference、QueryPlan JOIN、SQL Builder、SQL Runtime、DataSource Execution Context 的全量源码 / Contract 审计，正式冻结。

#### D06 根因结论

当前 `QueryJoinInferenceService` 本质是 Query-scoped Relation Candidate / Evidence Provider：输入是当前问题召回的 Metadata Search Results，通过 DataType、ColumnName、TableName、Semantic 等证据产生 `QueryJoinCandidate`。它不是平台级 Dynamic Relation Registry，也不能直接承担 MasterJoin / DirectKey 最终决策。

当前 QueryPlanBuilder 已存在 `QueryPlan.Joins` 与 QueryJoin 组装能力，但 SQL Builder 当前只接受已经形成的单表安全路径，多表 QueryPlan 尚未形成完整 JOIN SQL 落地闭环；因此必须补齐 QueryPlan.Joins → SQL JOIN 的明确 Contract。

当前 SQL Runtime 以单 `dataSourceId` / 单 `DbConnection` 执行一条 SQL；因此跨独立 DataSource 的 Metadata Relation 与可执行 SQL JOIN 必须分离，本 Phase 不实现 Federation。

#### D06 冻结架构

```text
Current Metadata Snapshot
        ↓
Relation Evidence
        ↓
Dynamic Relation Resolution
        ↓
┌──────────────────────────┐
│                          │
MasterJoin              DirectKey
│                          │
稳定 Master +              无稳定 Master +
当前 Execution Context     稳定 Fact Dimension Key / Label
可执行 JOIN                │
│                          │
└────────────┬─────────────┘
             ↓
         QueryPlan
             ↓
       SqlQueryBuilder
             ↓
         SQL Runtime
```

#### D06 最终职责边界

**保留：**
- `QueryJoinInferenceService`：Relation Evidence Provider；
- `QueryJoinCandidate`：候选证据模型；
- `QueryJoin`：QueryPlan JOIN 基础模型；
- Metadata Scanner、Semantic、Vector 现有基础链路。

**新增 / 调整方向：**
- Dynamic Relation Evidence Contract；
- Dynamic Relation Resolver；
- Relation Resolution State：Resolved / Ambiguous / NotResolved；
- Execution Capability：Executable / NotExecutable；
- QueryPlanBuilder 的已解析 Relation 接入；
- QueryPlan.Joins → SQL Builder 的 JOIN 落地 Contract。

**明确不做：**
- 不将 `QueryJoinInferenceService` 改造成“大而全 Resolver”；
- 不修改 QueryPlanEvaluator；
- 不修改 Ranking / Order Contract；
- 不修改 GQ-011 Golden Contract；
- 不为 material / supplier / customer 等业务实体增加硬编码主表；
- 不在 Phase 2.7 实现跨独立 DataSource Federation。

#### D06 最终修改范围

**必须修改：**
1. Relation Resolution Model / Contract；
2. Dynamic Relation Resolver；
3. QueryPlanBuilder Relation 接入；
4. QueryJoin Contract；
5. SqlQueryBuilder JOIN 落地。

**需在实现前根据最终 Contract 确认：**
6. Metadata Relation Model；
7. Current Metadata Snapshot / Provider。

**禁止修改：**
- QueryPlanEvaluator；
- Ranking / DetailRanking / AggregateRanking；
- Ranking Order Binding；
- GQ-011 Golden；
- Metric Semantic Resolution。

#### D06 兼容性 Gate

后续任何 D07+ 修改必须证明：

```text
既有无 Dimension PASS Case
        ↓
不进入 Dynamic Relation Resolution
        ↓
原 QueryPlan 路径保持不变
```

特别要求：
- GQ-011 必须继续 PASS；
- 已 PASS 的 Metric / Filter / Ranking Case 不得行为漂移；
- DirectKey 不得改变无 Dimension QueryPlan；
- MasterJoin 只对明确解析为 MasterJoin 的 Dimension 生效；
- Ambiguous / NotResolved 继续 BLOCK；
- Tenant / DataSource 边界不得发生错误绑定；
- 发现兼容性问题必须停止当前 STEP，定位根因并修复后重新执行完整受影响验证链，不得修改既有正确 Contract 或放宽 Gate。

### 六、D06 冻结后的强制文档流程

D06 已满足“完整审计 → 最终结论 → 冻结”条件。进入 D07 前必须严格执行：

```text
D06 FROZEN
   ↓
更新本 Phase 开发测试计划
   ↓
同步更新主开发计划
   ↓
同步 Runtime 记录（如受影响）
   ↓
确认 GitHub master 上三类文档状态一致
   ↓
才允许进入 D07
```

任何一步未完成，D07 不得开始。

### 七、D07 入口

文档同步完成后，正式进入：

**D07 — Dynamic Dimension Resolution 全量源码 / Contract 审计。**

D07 必须以 D05 Entity Key Contract + D06 Dynamic Relation Contract 为正式输入，先审计完整源码与调用链，再一次性形成修改范围；不得边审边临时修改 Factory / Builder / Resolver。

---

## 原 Phase 2.7 开发计划

Phase 2.7 继续按 D01～D21 / STEP-01～STEP-19 的顺序执行；上述 STEP-01 / STEP-02 / D05 / D06 审计结果作为后续 D07 Dimension Resolution、D08 QueryPlan Binding、D09/D10 双路径、D11 SQL Builder 的正式输入。
