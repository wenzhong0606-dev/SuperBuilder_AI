# Phase 2.7 — DimensionAware QueryPlan 开发测试计划

> 所属项目：SuperBuilder AI Native BI  
> 唯一源码基线：GitHub `master`  
> 主开发计划：`SuperBuilder AI Native BI Phase开发计划-V2.0.md`  
> 管理总则：`Phase开发测试管理总则.md`  
> Runtime 记录：`Phase2.7-Runtime分步测试记录.md`  
> 状态：IN_PROGRESS

## 一、阶段入口与切换依据

Phase 2.7 进入执行前，必须完成当前 `master` 全量源码、调用链、Golden、Runtime、Build、Gate 与文档审计。审计结论必须先写入本计划，再开始编码。

Phase 2.6 已明确：Dimension 不应以“必须存在独立主表”作为 Resolution 前提。真实 Metadata 可以由业务事实表直接承载 `material_id/material_name/material_code`、`company_id/company_name` 等 Dimension 信息。

因此 Phase 2.7 正式采用双路径：

```text
Dimension Resolution
        ↓
 ┌──────┴──────┐
 ↓             ↓
MasterJoin   DirectKey
 ↓             ↓
JOIN 主表     事实表直接 GROUP BY
 └──────┬──────┘
        ↓
DimensionAware QueryPlan
        ↓
SQL Builder
        ↓
SQL Runtime
```

## 二、阶段目标

把 Phase 2.6 的 Semantic Applicability 能力转化为可执行 Dimension QueryPlan，并形成 SQL 闭环。

## 三、阶段边界

### 本阶段包含

- Dimension Entity Key 识别；
- Dimension 承载字段识别；
- Master Table / Relation Detection；
- `MasterJoin` / `DirectKey` Contract；
- QueryPlan Dimension Binding；
- Join 条件生成/复用；
- DirectKey GROUP BY；
- SQL Builder 双路径闭环；
- Controller / Runtime 验证；
- Golden Regression、Coverage、Quality、Release Gate；
- MasterJoin / DirectKey Golden 样本。

### 本阶段不包含

- 重做 Semantic Search 基础设施；
- 修改 Phase 2.3 / 2.4 / 2.5 已冻结 Contract；
- 放宽 Evaluation Gate；
- 为通过 Golden Case 硬编码供应商/物料表；
- 修改 Golden Dataset 掩盖 Metadata 能力缺失。

## 四、Dimension Resolution 规则

### 规则 1：Dimension 是业务语义，不等价于主表

供应商、物料、客户、仓库等 Dimension 可以由事实表字段直接承载。

### 规则 2：优先识别 Dimension Entity Key

优先寻找 Primary Key、Foreign Key、Business Key、业务实体编码 / ID。不得把事实明细表自身主键误认为 Dimension Key。

### 规则 3：存在关联主表 → MasterJoin

如果 Metadata 能证明 Dimension Entity Key 可关联独立实体表，并存在稳定 Label / Name：

```text
Fact.DimensionKey = Master.DimensionKey
```

则 `ResolutionMode = MasterJoin`。

### 规则 4：不存在关联主表 → DirectKey

如果不存在可证明的关联主表，但事实表包含 Dimension Key / Code / Name：

```text
ResolutionMode = DirectKey
```

直接在事实表完成 Dimension Grouping，不因缺少主表而 BLOCK。

### 规则 5：禁止猜测主表

不能因为语义为“供应商/物料”就假设存在 supplier/material 主表。

### 规则 6：不能形成稳定 Entity Binding → NotResolved

既无法 MasterJoin，又无法稳定 DirectKey 时，保持 `NotResolved`，禁止继续构建可执行 QueryPlan。

### 规则 7：统一 Dimension Resolution Contract（2026-08-25 冻结）

本规则来源于 Phase 2.6 GQ-006 真实 Runtime：Metadata 中不存在“物料”独立主表时，旧逻辑直接 `NotResolved`。经业务规则确认，这不是“必须存在主表”的前提，而必须进入 DirectKey 判断。

正式规则如下：

1. 优先检查是否存在可由 Metadata Relation / ForeignKey / BusinessKey 证明的稳定关联主表。
2. 存在稳定关联主表时：`ResolutionMode = MasterJoin`，使用已确认的 Entity Key 建立 JOIN；不得猜测主表。
3. 不存在关联主表时：不得仅因缺少主表直接 `NotResolved`；必须继续检查事实表是否存在稳定 Dimension Key / Code / Name 等可直接绑定字段。
4. 存在稳定 DirectKey 时：`ResolutionMode = DirectKey`，不产生 JOIN，直接使用事实表 Dimension Key / Label 进行 GROUP BY / Ranking。
5. 既无稳定 MasterJoin，也无稳定 DirectKey 时：`ResolutionState = NotResolved`，安全 BLOCK。
6. 存在多个无法消歧的 Entity Binding 候选时：`ResolutionState = Ambiguous`，安全 REVIEW / BLOCK，不得猜测。
7. 禁止为了通过 GQ-006、GQ-010 或任何 Golden Case 硬编码 `material`、`supplier`、`customer` 等主表。
8. `MasterJoin` 与 `DirectKey` 必须使用统一 `DimensionResolution` Contract；QueryPlan、SQL Builder、Runtime 不得各自重新进行无上下文 Semantic Search。
9. 新增 DirectKey 能力不得改变已经正确的 MasterJoin 行为；新增 Dimension Resolution 能力必须通过兼容性 Golden Regression 验证。
10. GQ-006 当前 `NotResolved` 记录为 Dimension Resolution 能力缺口，不归因于 GQ-011 Ranking Order Resolution 修复；不得修改 GQ-011 修复代码以绕过该缺口。

### 规则 8：兼容性与回归要求

任何 Dimension Resolution 修改必须证明：

```text
已有正确 MasterJoin
        ↓
行为不变

新增 DirectKey
        ↓
仅覆盖原本因缺少主表而无法解析、但存在稳定事实表 Dimension Key / Label 的 Case

Ambiguous / NotResolved
        ↓
继续安全阻断
```

如果发现兼容性问题，必须停止后续依赖步骤，完成源码根因审计后再修改；不得以“修复一个 Golden Case”为理由破坏其他已正确 Case。

## 五、D03 / STEP-03 最终审计结论（2026-08-25）

### 5.1 `wms_storage_receipt_info` 真实 Fact Dimension 证据

当前 master Metadata 已确认入库事实表承载：

- `material_id`：物料实体 ID；
- `material_code`：物料编码；
- `material_name`：物料名称；
- `quantity`：入库数量 Metric。

因此 GQ-006、GQ-010 的 Metric Fact Context 已具备稳定的 Fact Dimension Key / Code / Label 承载条件。

### 5.2 当前 Relation Contract 边界

当前 `QueryJoinInferenceService` 是 QueryPlan 层的 JOIN 候选推断能力，其输入/证据包括字段名称、表名称、数据类型、Metadata Semantic 等；它不是数据库真实 ForeignKey / Metadata Relation Contract。

当前 `QueryJoin` 也只是“本次 QueryPlan 认为应该如何连接两个表”的执行 Contract，不能反向作为真实 Metadata Relation/FK 证据。

因此当前源码不能证明“物料”必须通过某个独立 Material Master 走 `MasterJoin`。

### 5.3 GQ-006 / GQ-010 最终 Resolution 分类

在当前真实 Metadata 证据下：

```text
GQ-006 / GQ-010
        ↓
Dimension = 物料
        ↓
未证明稳定独立 Master Relation
        ↓
Fact 已存在 material_id / material_code / material_name
        ↓
目标 ResolutionMode = DirectKey
```

DirectKey 应直接使用事实表 Dimension Key / Label 完成 Dimension Grouping，不生成 JOIN；随后按 Metric 聚合并支持 Ranking / Limit。

### 5.4 当前明确不修改对象

D03 审计冻结以下边界：

- `QueryPlanEvaluator`：不修改；
- GQ-011 Ranking Order Binding 修复：不修改；
- GQ-011 Golden Contract：不修改；
- `QueryJoin` Model：不因 DirectKey 重写；
- `QueryJoinInferenceService`：不改造成 DirectKey Resolver；
- SQL Builder：不得自行推理 Dimension / JOIN。

### 5.5 后续 Contract 必须满足的兼容性条件

- 有稳定真实 Master Relation 的 Dimension 继续走 `MasterJoin`；
- 没有稳定 Master Relation、但事实表存在稳定 Dimension Key / Code / Name 的 Dimension 才进入 `DirectKey`；
- 两者都不存在则 `NotResolved → BLOCK`；
- 多候选无法消歧则 `Ambiguous → BLOCK/REVIEW`；
- GQ-011 必须保持 PASS；
- 已 PASS 的其他 GQ Case 必须执行回归，不允许为了 GQ-006/GQ-010 改坏已有正确行为。

## 六、核心 Contract

统一形成：

```text
DimensionResolution
├── SemanticText
├── ResolutionMode
├── ResolutionState
├── FactTable
├── FactKeyColumn
├── FactCodeColumn
├── FactLabelColumn
├── DimensionTable
├── DimensionKeyColumn
├── DimensionLabelColumn
├── JoinRequired
├── JoinCondition
├── Confidence
└── Evidence
```

D03 完成后，D04 必须以本节作为 Contract 设计输入；不得重新发明 MasterJoin / DirectKey 判断规则。

## 七、完整开发任务与步骤

### STEP-D01 — Phase 2.6 Exit / master 基线审计
确认上一阶段 Exit 状态、当前源码、文档、Golden、Runtime、Gate 与风险。

### STEP-D02 — Metadata Dimension Entity Key 审计
确认真实 Metadata 中 Dimension Key / Code / Name / Relation，不虚构主表。

### STEP-D03 — Master Table / Relation 审计 ✅
完成真实 Fact Dimension Key / Code / Label 与 QueryJoin / Relation Contract 边界审计；GQ-006 / GQ-010 正式确定为 DirectKey 目标路径。

### STEP-D04 — Contract / Model 设计 ← CURRENT
定义 `DimensionResolution`、`ResolutionMode`、Key / Code / Label / Join / Evidence 信息；必须兼容 D03 结论。

### STEP-D05 — Dimension Entity Key Resolver 实现
实现稳定 Dimension Entity Key / Business Key 识别，并排除事实明细主键误识别。

### STEP-D06 — Master Table Detection 实现
根据 Metadata Relation / Column / Table Semantic 判断关联主表。

### STEP-D07 — Dimension Resolution 实现
形成 MasterJoin / DirectKey / NotResolved / Ambiguous 明确结果。

### STEP-D08 — QueryPlan Dimension Binding
QueryPlan 消费 DimensionResolution，不再进行无上下文全库 Semantic Search。

### STEP-D09 — MasterJoin QueryPlan
形成 Fact → Join → Master Dimension → GroupBy → Metric Aggregation。

### STEP-D10 — DirectKey QueryPlan
形成 Fact DimensionKey / Label → GroupBy → Metric Aggregation。

### STEP-D11 — SQL Builder 双路径闭环
分别生成 MasterJoin JOIN SQL 与 DirectKey GROUP BY SQL。

### STEP-D12 — Static Contract / DI / Namespace 审计
确认 Models / Interfaces / Services / Infrastructure / DI / Controller 调用链一致。

### STEP-D13 — Build
执行 Release Build，失败不得进入 Runtime。

### STEP-D14 — Controller / Runtime
使用现有 Controller / Action 验证，不新建独立 Test Project。

### STEP-D15 — MasterJoin Golden
验证有真实关联主表时 JOIN 正确。

### STEP-D16 — DirectKey Golden
验证无关联主表时事实表直接聚合正确。

### STEP-D17 — SameTable / CrossTable Golden
覆盖同表 Dimension 与跨表 Dimension。

### STEP-D18 — Ambiguous / NotResolved Safety Regression
确保安全边界不因 Positive Pass 增强而回归。

### STEP-D19 — Full Golden Regression
执行项目正式 Golden Regression。

### STEP-D20 — Coverage / Quality / Release Gate
依次完成 Coverage、Quality、Release Gate。

### STEP-D21 — Phase 2.7 Exit Review
确认所有任务、Runtime、Golden、Gate、文档、Commit 均闭环。

## 八、开发任务与 Runtime STEP 对应

| 开发步骤 | Runtime 验证 |
|---|---|
| D01 | STEP-01 |
| D02 | STEP-02 |
| D03 | STEP-03 |
| D04 | STEP-04 |
| D05-D07 | STEP-05~06 |
| D08-D10 | STEP-07~08 |
| D11 | STEP-09~10 |
| D12-D14 | STEP-11~12 |
| D15-D18 | STEP-13~15 |
| D19 | STEP-16 |
| D20 | STEP-17~18 |
| D21 | STEP-19 |

每个开发步骤开始/完成必须同步主开发计划与本文件；Runtime 结果必须同步 Runtime 分步测试记录。

## 九、Golden 验收矩阵

| 类型 | 目标 |
|---|---|
| MasterJoin | 有独立主表时正确 JOIN |
| DirectKey | 无主表时直接使用 Dimension Entity Key 聚合 |
| SameTableDimension | Metric 与 Dimension 在同一事实表 |
| CrossTableDimension | Metric 与 Dimension 跨表并正确 Join |
| Ambiguous | 无法稳定判断时 REVIEW / BLOCK |
| NotResolved | 无可执行 Binding 时 BLOCK |

重点使用当前真实 Metadata，不虚构 supplier/material 主表。

## 十、恢复锚点

任何会话恢复时，按以下顺序读取：

```text
主开发计划
↓
本 Phase 开发测试计划
↓
本 Phase Runtime 分步测试记录
↓
最新 master Commit
```

然后从 Runtime 记录中最后一个未 PASS 的 STEP 继续，不重复已有正式证据。

## 十一、Exit Criteria

必须全部满足：

1. 完成入口全量审计；
2. 全部 D01-D21 完成；
3. MasterJoin Runtime PASS；
4. DirectKey Runtime PASS；
5. SameTable / CrossTable PASS；
6. Ambiguous / NotResolved 安全边界无回归；
7. QueryPlan 完整表达 Dimension Resolution；
8. SQL Builder 正确生成 JOIN / Direct GROUP BY；
9. Golden Regression 达到正式 Gate；
10. Coverage / Quality / Release Gate 通过；
11. Runtime 记录完整；
12. 主开发计划同步；
13. GitHub master 可从文档恢复当前状态。

未满足任一关键条件不得标记 COMPLETE。
