# Phase 2.7 — DimensionAware QueryPlan 开发测试计划

> 所属项目：SuperBuilder AI Native BI
> 唯一源码基线：GitHub `master`
> 主开发计划：`SuperBuilder AI Native BI Phase开发计划-V2.0.md`
> 管理总则：`Phase开发测试管理总则.md`
> Runtime 记录：`Phase2.7-Runtime分步测试记录.md`
> 状态：PLANNED

## 一、阶段入口

Phase 2.7 进入执行前，必须完成当前 `master` 全量源码、调用链、Golden、Runtime、Build、Gate 与文档审计。审计结论必须先写入本计划，再开始 STEP-01。

## 二、阶段目标

把 Phase 2.6 的 Semantic Applicability 能力转化为可执行 Dimension QueryPlan，并形成 SQL 闭环。

核心原则：

> Dimension 不要求必须存在独立主表。存在可识别关联主表时采用 `MasterJoin`；不存在关联主表时采用 `DirectKey`，直接使用业务事实表中的 Dimension Entity Key / Business Key 进行分组汇总。

最终链路：

```text
User Question
↓
Semantic Resolution
↓
Metric Resolution
↓
Dimension Resolution
↓
┌───────────────────┬───────────────────┐
│ MasterJoin        │ DirectKey         │
│ JOIN 关联主表     │ 事实表直接聚合     │
└───────────────────┴───────────────────┘
↓
DimensionAware QueryPlan
↓
SQL Builder
↓
SQL Runtime
↓
Correct Result
```

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

优先寻找：

- Primary Key；
- Foreign Key；
- Business Key；
- 业务实体编码 / ID。

不得把事实明细表自身主键误认为 Dimension Key。

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

## 五、核心 Contract

统一形成：

```text
DimensionResolution
├── SemanticText
├── ResolutionMode
├── FactTable
├── FactKeyColumn
├── DimensionTable
├── DimensionKeyColumn
├── DimensionLabelColumn
├── JoinRequired
├── JoinCondition
└── Confidence
```

`MasterJoin` 必须能解释 DimensionTable、DimensionKeyColumn、DimensionLabelColumn；`DirectKey` 必须能解释 FactTable、FactKeyColumn，Label 存在时同时保留。

## 六、完整开发任务与步骤

### STEP-D01 — Phase 2.6 Exit / master 基线审计

确认上一阶段 Exit 状态、当前源码、文档、Golden、Runtime、Gate 与风险。

### STEP-D02 — Metadata Dimension Entity Key 审计

确认真实 Metadata 中 Dimension Key / Code / Name / Relation，不虚构主表。

### STEP-D03 — Master Table / Relation 审计

确定哪些 Dimension 能走 MasterJoin，哪些只能 DirectKey。

### STEP-D04 — Contract / Model 设计

定义 `DimensionResolution`、`ResolutionMode`、Key / Label / Join 信息。

### STEP-D05 — Dimension Entity Key Resolver 实现

实现稳定 Dimension Entity Key / Business Key 识别，并排除事实明细主键误识别。

### STEP-D06 — Master Table Detection 实现

根据 Metadata Relation / Column / Table Semantic 判断关联主表。

### STEP-D07 — Dimension Resolution 实现

形成 MasterJoin / DirectKey / NotResolved 三种明确结果。

### STEP-D08 — QueryPlan Dimension Binding

QueryPlan 消费 DimensionResolution，不再进行无上下文全库 Semantic Search。

### STEP-D09 — MasterJoin QueryPlan

形成：

```text
Fact → Join → Master Dimension → GroupBy → Metric Aggregation
```

### STEP-D10 — DirectKey QueryPlan

形成：

```text
Fact DimensionKey / Label → GroupBy → Metric Aggregation
```

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

## 七、开发任务与 Runtime STEP 对应

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

## 八、Golden 验收矩阵

| 类型 | 目标 |
|---|---|
| MasterJoin | 有独立主表时正确 JOIN |
| DirectKey | 无主表时直接使用 Dimension Entity Key 聚合 |
| SameTableDimension | Metric 与 Dimension 在同一事实表 |
| CrossTableDimension | Metric 与 Dimension 跨表并正确 Join |
| Ambiguous | 无法稳定判断时 REVIEW / BLOCK |
| NotResolved | 无可执行 Binding 时 BLOCK |

重点使用当前真实 Metadata，不虚构 supplier/material 主表。

## 九、Exit Criteria

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