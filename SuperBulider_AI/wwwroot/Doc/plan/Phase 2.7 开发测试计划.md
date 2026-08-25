# Phase 2.7 — DimensionAware QueryPlan 开发测试计划

## STEP-01 / STEP-02 审计结论与最终修改范围（2026-08-25）

### 一、审计结论

当前 master 已完成 STEP-01 全量相关源码 / Contract 基线审计及 STEP-02 真实 Metadata Dimension Entity Key 审计。

当前 GQ-011 已 PASS；GQ-006、GQ-010 均在 SemanticApplicabilityGate 因“物料”无法建立稳定 Dimension 物理绑定而 BLOCK。该 BLOCK 不是 GQ-011 Ranking Order 修复产生的回归，而是当前 Dimension Resolution 能力缺口。

当前代码已经具备：
- Metric Semantic Resolution；
- 基础 Dimension Column Binding；
- QueryPlan.Dimensions；
- QueryPlan.Joins / QueryJoin Contract；
- 单表 DirectKey 所需的 GROUP BY 基础能力。

当前代码缺少：
- DimensionResolution 统一 Contract；
- MasterJoin / DirectKey Resolution；
- 基于真实 Metadata Relation / Entity Key 证据的稳定 Dimension 绑定；
- MasterJoin QueryPlan → SQL Builder 的完整 JOIN 闭环。

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
2. 是否 MasterJoin 必须由真实 Metadata / Relation / Entity Key 证据决定。
3. DirectKey 不要求存在独立 Dimension 主表。
4. MasterJoin 必须在 QueryPlan 中形成明确 QueryJoin；SQL Builder 不得自行猜测 JOIN。
5. Ambiguous / NotResolved 必须安全阻断。
6. 新增 DirectKey / MasterJoin 能力不得改变已经 PASS 的既有 GQ Case 行为。
7. 每次修改必须进行兼容性回归，至少覆盖 GQ-011 以及既有正确的 Metric / Filter / Ranking Case。
8. 本规则作为 Phase 2.7 的正式 Contract，不允许针对单一 Golden Case 做硬编码补丁。

### 三、最终修改范围（一次性冻结）

本阶段后续源码修改必须按以下责任边界实施；先完成全部源码修改设计，再开始编码，不允许边读边改：

#### 1. Dimension Resolution Contract

新增统一 DimensionResolution Contract，至少表达：
- ResolutionMode：MasterJoin / DirectKey；
- ResolutionState：Resolved / Ambiguous / NotResolved；
- FactTable / FactKeyColumn / FactLabelColumn；
- DimensionTable / DimensionKeyColumn / DimensionLabelColumn（MasterJoin 时）；
- JoinRequired；
- Confidence / Evidence。

#### 2. Dimension Resolver Interface + Service

新增 `IDimensionResolutionService` 与对应实现 `DimensionResolutionService`。

职责：
- 接收 Dimension Semantic + Fact Context；
- 获取真实 Metadata Candidate；
- 判断稳定 Master Entity；
- 无稳定主表时判断 Fact Dimension Key / Label；
- 输出 MasterJoin / DirectKey / Ambiguous / NotResolved。

不得把该逻辑塞入 Metadata Semantic Search、QueryPlanEvaluator 或 QueryPlanBuilder。

#### 3. Semantic Applicability 接入

`SemanticApplicabilityEvaluator` 负责调用 Dimension Resolver 并把最终 DimensionResolution 结果传入后续 Pipeline；不再自行承担 MasterJoin / DirectKey 推理。

#### 4. QueryPlan Semantic Resolution Contract

扩展 `QueryPlanDimensionResolution` 以承载 ResolutionMode 与必要的物理绑定信息。

`QueryPlanSemanticResolutionFactory` 只负责 Contract 转换，不承担 Dimension Resolution 推理。

#### 5. QueryPlanBuilder

只消费已解析的 DimensionResolution：
- DirectKey → 事实表 Dimension Column；
- MasterJoin → Dimension Binding + QueryJoin；
- 不重新进行语义猜测。

#### 6. SQL Builder

保留现有“SQL Builder 不猜 JOIN”的原则。

增加对 QueryPlan.Joins 的明确 SQL JOIN 生成能力；仅生成 QueryPlan 已明确声明的 Join，不在 SQL Builder 内进行关系推理。

DirectKey 保持单表 GROUP BY 路径。

#### 7. QueryJoinInferenceService

当前不作为 Dimension Resolver 重写对象；保留其既有职责。若最终源码实现证明其可作为辅助证据，仅允许作为辅助，不得取代稳定 Metadata / Relation / Entity Key Contract。

#### 8. DI

注册新增 Dimension Resolution Service；保持现有服务职责隔离。

#### 9. Golden / Runtime

GQ-006、GQ-010 作为 DirectKey 核心验证；GQ-005、GQ-009 等 MasterJoin Case 必须先依据真实 Metadata 确认其 MasterJoin / DirectKey 归属，不允许凭 Golden 文本猜测。

GQ-011 当前 PASS，必须作为兼容性基线，不能因 Phase 2.7 修改回退。

### 四、禁止事项

本阶段禁止：
- 为 GQ-006 / GQ-010 硬编码物料主表；
- 为 GQ-005 / GQ-009 硬编码供应商主表；
- 修改 QueryPlanEvaluator 以绕过 BLOCK；
- 修改 Golden 以掩盖 Resolution 缺口；
- 将 Dimension Resolution 塞入 Factory；
- 将 JOIN 推理塞入 SQL Builder；
- 只验证 GQ-006/010 而不执行兼容性回归。

### 五、实施前置 Gate

在任何源码修改前，必须完成：
1. 当前 master 完整源码基线确认；
2. `wms_storage_receipt_info` 的物料 / 供应商字段及真实 Metadata 证据确认；
3. Relation / FK / BusinessKey / Semantic 证据确认；
4. MasterJoin / DirectKey 分类确认；
5. 最终文件修改清单确认。

确认后才进入编码；编码完成后必须依次执行 Build → Controller Runtime → DirectKey Golden → MasterJoin Golden → Ambiguous / NotResolved Safety → Full Golden Regression → Coverage / Quality / Release Gate。

### 六、兼容性 Gate

任何修改必须证明：
- GQ-011 Ranking Order Binding 继续 PASS；
- 已 PASS 的既有 GQ Case 不出现行为漂移；
- 新增 DirectKey 不改变无 Dimension 的 QueryPlan；
- MasterJoin 只对明确解析为 MasterJoin 的 Dimension 生效；
- Ambiguous / NotResolved 继续 BLOCK。

如出现任何兼容性问题，立即停止后续 STEP，定位回归根因并修复后重新执行受影响的完整验证链。

---

## 原 Phase 2.7 开发计划

Phase 2.7 继续按 D01～D21 / STEP-01～STEP-19 的顺序执行；上述 STEP-01 / STEP-02 审计结果作为后续 D04 Contract、D05 Resolver、D06 Master Detection、D07 Dimension Resolution、D08 QueryPlan Binding、D09/D10 双路径、D11 SQL Builder 的正式输入。
