# Phase 2.7 — DimensionAware QueryPlan 开发与测试计划

> 所属项目：SuperBuilder AI Native BI
> 唯一源码基线：GitHub `master`
> 前置阶段：Phase 2.6 — Query Evaluation Framework
> 本阶段主题：Dimension-Aware QueryPlan & SQL Closure
> 关联管理规则：`Phase阶段开发与测试管理规则.md`
> 关联测试记录：`Phase2.7-Runtime分步测试记录.md`

## 一、阶段目标

Phase 2.7 负责把 Phase 2.6 已完成/正在收敛的 Semantic Applicability 能力进一步转化为可执行的 Dimension QueryPlan。

核心原则：

> Dimension 不要求必须存在独立主表。存在可识别关联主表时采用 `MasterJoin`；不存在关联主表时采用 `DirectKey`，直接使用业务事实表中的 Dimension Entity Key / Business Key 进行分组汇总。

最终形成：

```text
User Question
 ↓
Semantic Resolution
 ↓
Metric Resolution
 ↓
Dimension Resolution
 ↓
 ┌───────────────┬───────────────┐
 │ MasterJoin    │ DirectKey     │
 │ JOIN 主表     │ 事实表直接聚合 │
 └───────────────┴───────────────┘
 ↓
DimensionAware QueryPlan
 ↓
SQL Builder
 ↓
SQL Execution
 ↓
Correct Result
```

## 二、阶段边界

### 本阶段包含

- Dimension Entity Key 识别；
- Dimension 承载字段识别；
- Master Table Detection；
- `MasterJoin` / `DirectKey` 双模式 Contract；
- QueryPlan Dimension Binding；
- Join 条件生成/复用；
- DirectKey GROUP BY 生成；
- SQL Builder 双路径闭环；
- Controller / Runtime 验证；
- Golden Regression、Coverage、Quality、Release Gate；
- 形成 MasterJoin 与 DirectKey 两类 Golden 验收样本。

### 本阶段不包含

- 重新设计 Semantic Search 基础设施；
- 修改 Phase 2.3 / 2.4 / 2.5 已冻结 Contract；
- 删除或放宽 Evaluation Gate；
- 为通过 Golden Case 硬编码供应商/物料表；
- 修改 Golden Dataset 以掩盖 Metadata 能力缺失。

## 三、Dimension Resolution 规则

### 规则 1：Dimension 是业务语义，不等价于主表

`供应商`、`物料`、`客户`、`仓库`等 Dimension 可以由事实表中的实体字段承载。

### 规则 2：优先识别 Dimension Entity Key

优先寻找能够稳定标识 Dimension Entity 的：

- Primary Key；
- Foreign Key；
- Business Key；
- 业务实体编码/ID。

不得把事实明细表自身主键误认为 Dimension Key。

### 规则 3：存在关联主表 → MasterJoin

如果 Metadata 能证明 Dimension Entity Key 能关联到独立实体表，并存在稳定 Label / Name 字段：

```text
Fact.DimensionKey = Master.DimensionKey
```

则 ResolutionMode = `MasterJoin`。

### 规则 4：不存在关联主表 → DirectKey

如果不存在可证明的关联主表，但事实表已经包含 Dimension Key / Code / Name：

ResolutionMode = `DirectKey`。

直接在事实表上完成 Dimension Grouping，不因缺少主表而 BLOCK。

### 规则 5：禁止猜测主表

不能因为 Dimension 名称为“供应商/物料”就假设存在 supplier/material 主表。

### 规则 6：不能形成稳定 Entity Binding → NotResolved

如果既无法找到 MasterJoin，也无法找到稳定 DirectKey，则保持 `NotResolved`，禁止继续构建可执行 QueryPlan。

## 四、核心 Contract

建议形成统一 `DimensionResolution`：

```text
SemanticText
ResolutionMode
FactTable
FactKeyColumn
DimensionTable
DimensionKeyColumn
DimensionLabelColumn
JoinRequired
JoinCondition
Confidence
```

其中：

- `MasterJoin`：DimensionTable、DimensionKeyColumn、DimensionLabelColumn 必须可解释；
- `DirectKey`：FactTable、FactKeyColumn 必须可解释；Label 存在时可同时保留；
- 两种模式最终均必须能够生成完整 QueryPlan。

## 五、开发任务

### 2.7.1 Dimension Entity Key Resolver

识别 Dimension 的 Entity Key / Business Key，并区分事实表主键与 Dimension Key。

### 2.7.2 Master Table Detection

根据 Metadata Relation / Column / Table Semantic 判断是否存在稳定关联主表。

### 2.7.3 Dimension Resolution Contract

将 `MasterJoin` 与 `DirectKey` 纳入统一 Resolution Contract。

### 2.7.4 QueryPlan Dimension Binding

QueryPlan 必须消费 Resolution，不得再次进行无上下文的全库 Semantic Search。

### 2.7.5 MasterJoin QueryPlan

形成：

```text
Fact → Join → Master Dimension → GroupBy → Metric Aggregation
```

### 2.7.6 DirectKey QueryPlan

形成：

```text
Fact DimensionKey/Label → GroupBy → Metric Aggregation
```

### 2.7.7 SQL Builder Closure

分别验证 MasterJoin 与 DirectKey 生成正确 SQL。

### 2.7.8 Runtime / Golden Regression

通过现有 Controller / Action 验证，不新建独立 Test Project。

## 六、完整开发顺序

```text
STEP-D01 读取 Phase 2.6 Exit / 当前 master
 ↓
STEP-D02 Metadata Dimension Entity Key 审计
 ↓
STEP-D03 Master Table / Relation 审计
 ↓
STEP-D04 Contract / Model 设计
 ↓
STEP-D05 Implementation
 ↓
STEP-D06 QueryPlan Binding
 ↓
STEP-D07 SQL Builder 双路径
 ↓
STEP-D08 Static Contract / DI / Namespace 审计
 ↓
STEP-D09 dotnet build
 ↓
STEP-D10 Controller Runtime
 ↓
STEP-D11 MasterJoin Golden
 ↓
STEP-D12 DirectKey Golden
 ↓
STEP-D13 Full Golden Regression
 ↓
STEP-D14 Coverage
 ↓
STEP-D15 Quality Gate
 ↓
STEP-D16 Release Gate
 ↓
STEP-D17 Phase 2.7 Exit Review
```

## 七、测试原则

Runtime 测试必须严格按照 `Phase2.7-Runtime分步测试记录.md` 执行：

```text
一次只执行当前 STEP
→ 用户返回完整原始 JSON
→ 判定
→ 更新测试记录
→ Commit master
→ 再进入下一 STEP
```

不得一次要求执行多个地址。

## 八、Golden 测试矩阵

至少覆盖：

| 类型 | 目标 |
|---|---|
| MasterJoin | 有独立主表时正确 JOIN |
| DirectKey | 无主表时直接使用 Dimension Entity Key 聚合 |
| SameTableDimension | Metric 与 Dimension 在同一事实表 |
| CrossTableDimension | Metric 与 Dimension 跨表，需要 Join |
| Ambiguous | 多个候选无法稳定判断时保持 REVIEW/BLOCK |
| NotResolved | 无可执行 Dimension Binding 时保持 BLOCK |

重点使用当前真实 Metadata 中存在的业务字段，不虚构主表。

## 九、Exit Criteria

Phase 2.7 只有同时满足以下条件才能 COMPLETE：

1. MasterJoin Contract 已实现并通过 Runtime；
2. DirectKey Contract 已实现并通过 Runtime；
3. SameTableDimension 已通过；
4. CrossTableDimension 已通过；
5. Ambiguous / NotResolved 安全边界未回归；
6. QueryPlan 能完整表达 Dimension Resolution；
7. SQL Builder 能分别生成 JOIN / Direct GROUP BY；
8. Golden Regression 达到项目正式 Gate；
9. Coverage / Quality / Release Gate 通过；
10. 所有测试结果已写入 Runtime 分步测试记录；
11. 主开发计划已同步 Phase 2.7 当前状态与 Exit 结果；
12. GitHub master Commit 可恢复当前开发状态。

## 十、阶段完成后的入口

Phase 2.7 COMPLETE 后，再规划 Phase 2.8。不得在 Phase 2.7 Exit Criteria 未满足时提前进入下一 Phase。
