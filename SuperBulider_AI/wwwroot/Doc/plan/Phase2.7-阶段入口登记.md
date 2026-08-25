# Phase 2.7 阶段入口登记

> 所属项目：SuperBuilder AI Native BI
> 唯一源码基线：GitHub `master`
> 前置阶段：Phase 2.6 — Query Evaluation Framework
> 目标阶段：Phase 2.7 — DimensionAware QueryPlan & SQL Closure
> 登记状态：IN PROGRESS / PLANNING

## 一、阶段切换依据

Phase 2.6 已经明确发现：Dimension 不应以“必须存在独立主表”作为 Resolution 前提。真实 Metadata 中存在业务事实表直接承载 `material_id/material_name/material_code`、`company_id/company_name` 等 Dimension 信息的情况。

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

## 二、必须关联的文档

### 1. 阶段长期管理规则

`Phase阶段开发与测试管理规则.md`

### 2. Phase 2.7 开发 / 测试计划

`Phase2.7-DimensionAware QueryPlan开发测试计划.md`

### 3. Phase 2.7 Runtime 分步测试记录

`Phase2.7-Runtime分步测试记录.md`

### 4. 主开发计划

`SuperBuilder AI Native BI Phase开发计划-V2.0.md`

主开发计划必须在后续状态同步中反映 Phase 2.7 当前工作单元；本登记文档用于保证阶段入口信息在会话中断后可恢复。

## 三、恢复锚点

后续任何会话恢复时，优先读取：

```text
Phase开发计划-V2.0
↓
Phase2.7-阶段入口登记
↓
Phase2.7-DimensionAware QueryPlan开发测试计划
↓
Phase2.7-Runtime分步测试记录
↓
最新 master Commit
```

然后从 Runtime 分步测试记录中最后一个未 PASS 的 STEP 继续，不重复已经有正式证据的步骤。

## 四、当前开发顺序

```text
1. Metadata Dimension Entity Key 审计
2. Master Table / Relation 审计
3. DimensionResolution Contract
4. MasterJoin / DirectKey 实现
5. QueryPlan Dimension Binding
6. SQL Builder 双路径
7. Build
8. Controller / Runtime
9. MasterJoin Runtime
10. DirectKey Runtime
11. Golden Regression
12. Coverage
13. Quality Gate
14. Release Gate
15. Phase 2.7 Exit Review
```

## 五、阶段完成原则

Phase 2.7 未通过完整 Runtime、Golden、Coverage、Quality、Release 及文档同步前，不得标记 COMPLETE，也不得提前进入 Phase 2.8。
