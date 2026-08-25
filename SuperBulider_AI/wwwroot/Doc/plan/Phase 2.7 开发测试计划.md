# Phase 2.7 — DimensionAware QueryPlan 开发测试计划

## STEP-01 / STEP-02 / D05 审计结论与最终修改范围（2026-08-25）

### 一、审计结论

当前 master 已完成 STEP-01 全量相关源码 / Contract 基线审计及 STEP-02 真实 Metadata Dimension Entity Key 审计，并完成 D05 Dimension Entity Key Resolver 的完整责任链审计。

当前 GQ-011 已 PASS；GQ-006、GQ-010 均在 SemanticApplicabilityGate 因“物料”无法建立稳定 Dimension 物理绑定而 BLOCK。该 BLOCK 不是 GQ-011 Ranking Order 修复产生的回归，而是当前 Dimension Resolution 能力缺口。

当前代码已经确认具备：
- Metric Semantic Resolution；
- 基础 Dimension Column Binding；
- QueryPlan.Dimensions；
- QueryPlan.Joins / QueryJoin Contract；
- 单表 DirectKey 所需的 GROUP BY 基础能力；
- DataSource → 数据库结构扫描 → MetadataTable / MetadataColumn → SearchText → AI Semantic → Vector 的基础链路；
- Metadata 按 TenantId + DataSourceId 隔离的基础能力。

当前代码缺少：
- DimensionResolution 统一 Contract；
- MasterJoin / DirectKey Resolution；
- 基于真实 Metadata Relation / Entity Key 证据的稳定 Dimension 绑定；
- MasterJoin QueryPlan → SQL Builder 的完整 JOIN 闭环；
- 完整的 Metadata Snapshot Reconciliation / Stale Metadata Invalidation；
- Metadata 内容变化后的 Semantic Refresh / Vector Refresh 生命周期 Contract。

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
  ├─ YES → DirectKey → 不 JOIN，按事实表 Dimension Key/Label 汇总
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

D05 输入至少包含：
- TenantId；
- DataSourceId；
- Dimension Semantic；
- 当前有效 Metadata Snapshot / Candidate。

D05 输出至少表达：
- Dimension Semantic；
- Candidate Fact Table；
- Candidate Entity Key Column；
- Candidate Label Column（如存在）；
- DataSource；
- Key Evidence；
- Semantic Evidence；
- Confidence / Evidence Trace。

D05 明确不负责：
- JOIN 推理；
- QueryPlan 生成；
- SQL 生成；
- Ranking / Order；
- QueryPlanEvaluator；
- Metadata 扫描；
- Semantic 生成；
- Vector 生成。

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

该缺口不并入 D05 Resolver 实现，新增为后续独立 Metadata Lifecycle / Snapshot Reconciliation 任务；不得为了 GQ-006 / GQ-010 通过而把生命周期逻辑临时塞入 Dimension Resolver。

### 五、D05 最终源码责任边界

#### D05 允许修改

- Dimension Entity Key Resolution Contract；
- Dimension Entity Key Resolver Interface / Service；
- Semantic Applicability 的 Dimension Resolution 接入点；
- 必要的 Resolution Evidence DTO / Model；
- 必要 DI 注册。

#### D05 禁止修改

- MetadataScannerService 的业务职责边界；
- QueryPlanEvaluator；
- GQ-011 Ranking / Order Contract；
- Golden Contract；
- SQL Builder 的关系推理职责；
- 针对 material / supplier / customer 的硬编码。

### 六、兼容性 Gate

任何后续修改必须证明：
- GQ-011 Ranking Order Binding 继续 PASS；
- 已 PASS 的既有 GQ Case 不出现行为漂移；
- 新增 DirectKey 不改变无 Dimension 的 QueryPlan；
- MasterJoin 只对明确解析为 MasterJoin 的 Dimension 生效；
- Ambiguous / NotResolved 继续 BLOCK；
- DataSource / Tenant 边界不发生跨租户、跨数据源错误绑定。

如出现任何兼容性问题，立即停止后续 STEP，定位回归根因并修复后重新执行受影响的完整验证链；不得以修改既有正确 Case Contract 的方式消除回归。

### 七、实施前置 Gate

D05 已满足“审计 → 最终结论 → 冻结”条件。正式进入下一 STEP 前必须完成：
1. 更新本阶段开发计划；
2. 同步更新主开发计划；
3. 如 Runtime 记录受影响，同步 Runtime 记录；
4. 确认 GitHub `master` 上阶段计划、主计划、Runtime 记录状态一致；
5. 完成后才进入 D06。

### 八、后续顺序

D05 冻结后，下一步进入：

**D06 — Dynamic Master Table / Relation Detection 全量源码 / Contract 审计。**

D06 必须继续遵守：先完整审计当前 master → 形成一次性最终修改方案 → 冻结 → 更新阶段计划 → 同步主计划 → 确认 master → 再进入下一步。

---

## 原 Phase 2.7 开发计划

Phase 2.7 继续按 D01～D21 / STEP-01～STEP-19 的顺序执行；上述 STEP-01 / STEP-02 / D05 审计结果作为后续 D06 Master Detection、D07 Dimension Resolution、D08 QueryPlan Binding、D09/D10 双路径、D11 SQL Builder 的正式输入。
