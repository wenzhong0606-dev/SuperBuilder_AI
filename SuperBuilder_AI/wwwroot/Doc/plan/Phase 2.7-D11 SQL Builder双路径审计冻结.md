# Phase 2.7 — D11 SQL Builder 双路径（MasterJoin / DirectKey）审计冻结

> 日期：2026-08-25  
> 状态：**FROZEN**  
> 源码基线：GitHub `master`  
> 本次：**只审计，不修改业务源码**

## 一、D11 最终审计结论

当前 `SqlQueryBuilder` 只有单表 SQL 生成能力。

源码明确要求：

```text
QueryPlan.Tables.Count == 0 → FAIL
QueryPlan.Tables.Count > 1  → FAIL
```

并明确声明 SQL Builder 不自动生成 JOIN；只有 QueryPlan 明确建立关系后才允许进入 SQL 生成。当前实现实际上连“已确认 QueryPlan.Joins 如何转换为 SQL JOIN”都没有完成。fileciteturn423file0L2-L2

因此 D11 不是修复一个错误 JOIN，而是补齐：

```text
D07 Dimension Resolution
        ↓
 ┌──────┴──────┐
MasterJoin   DirectKey
   ↓             ↓
D09 Binding   D10 Binding
   └──────┬──────┘
          ↓
      QueryPlan
          ↓
D11 SqlQueryBuilder
   ├── MasterJoin → JOIN SQL
   └── DirectKey  → 单表 SQL + Dimension Key/Label
```

## 二、当前源码事实

### 1. QueryPlan 已经存在 JOIN 容器

`QueryPlan` 已有：

```text
Tables
Fields
Metrics
Dimensions
Filters
Orders
Joins
Limit
```

但没有 SQL Builder 对 `Joins` 的实际执行路径。fileciteturn424file0L2-L2

### 2. QueryJoin 已具备基本物理字段

`QueryJoin` 已经表达：

- LeftTableId / LeftColumnId
- RightTableId / RightColumnId
- LeftTableName / LeftColumnName
- RightTableName / RightColumnName
- JoinType

因此 D11 不需要重新设计一套 JOIN 模型。fileciteturn427file0L2-L2

### 3. 当前 QueryPlanBuilder 仍存在旧 JOIN 旁路

`QueryPlanBuilder.PlanAssembly` 当前仍执行：

```text
BuildJoinsAsync
 ↓
IQueryJoinInferenceService
 ↓
QueryJoinCandidate
 ↓
BuildQueryJoin
 ↓
QueryPlan.Joins
```

并且还会自动向 `QueryPlan.Tables` 添加 JOIN 目标表。fileciteturn429file0L2-L2

这属于 D09 已冻结的旧路径，D11 不负责重新设计 Relation；后续实现必须让 D09 的 Resolution Binding 成为唯一 Executable MasterJoin 来源。

### 4. SQL Builder 当前明确禁止多表

当前 `SqlQueryBuilder` 在 `Tables.Count > 1` 时直接抛出异常，因此 MasterJoin QueryPlan 当前无法进入 SQL Runtime。fileciteturn423file0L2-L2

### 5. SQL Builder 已经具备其他正确能力

当前 Builder 已经负责：

```text
SELECT
FROM
WHERE
GROUP BY
ORDER BY
LIMIT
```

并且 `QueryPlan.Dimensions` 优先于 Legacy `QueryIntent.Dimensions`，`QueryPlan.Orders` 优先于 Legacy `QueryIntent.OrderBy`。fileciteturn423file0L2-L2

这些能力不是 D11 的重构目标。

## 三、D11 最终 Contract

### MasterJoin

只有：

```text
QueryPlan.Joins
```

已经包含一个**冻结、可执行、经过 D09 Resolution 的 MasterJoin Binding**时，SQL Builder 才允许生成：

```sql
FROM FactTable
INNER JOIN DimensionTable
    ON FactTable.DimensionKey = DimensionTable.DimensionKey
```

具体 JOIN 类型由 QueryJoin.JoinType 决定，但只允许受控白名单：

```text
INNER
LEFT
RIGHT
```

不允许 SQL Builder 自行推理 JOIN。

### DirectKey

DirectKey 不产生 JOIN：

```text
FROM FactTable
```

Dimension Key / Label 来自 D10 QueryPlan Binding，由 SQL Builder 按 QueryPlan 中已确认的字段生成 SELECT / GROUP BY / ORDER BY。

因此：

```text
DirectKey ≠ JOIN
```

## 四、D11 最终修改范围：FROZEN

### A. `SqlQueryBuilder`

1. 移除“`Tables.Count > 1` 一律失败”的限制，改为只允许**已确认 QueryPlan.Joins**的多表计划。
2. 增加 JOIN SQL 生成专用方法，例如：

```text
BuildJoins(...)
BuildJoinExpression(...)
```

3. JOIN 只能消费 `QueryPlan.Joins`。
4. JOIN 两端必须能在 `QueryPlan.Tables` 找到对应表。
5. JOIN 两端 Column 必须来自对应表。
6. JoinType 使用白名单校验。
7. 禁止 Builder 根据字段名、表名、AI 语义重新推理 Relation。
8. DirectKey 不生成 JOIN。
9. 保持现有 SELECT / WHERE / GROUP BY / ORDER BY / LIMIT 逻辑。
10. 保持 SQL Dialect 抽象，不把 SQL Server 特殊语法写死到通用 Builder。

### B. SQL Dialect Contract（仅必要时）

如果现有 `ISqlDialect` 无法安全表达 JOIN 所需标识符转义，则只增加最小通用能力；不得为某一个数据库写专用 JOIN 推理。

### C. QueryPlan Validator / Runtime 前置验证

必要时增加：

```text
QueryJoin 引用完整性
Table/Column/DataSource 一致性
JoinType 合法性
Join 两端存在
```

但 Validator 只验证，不推理。

## 五、D11 明确禁止修改

1. ❌ 禁止重新实现 `QueryJoinInferenceService`。
2. ❌ 禁止在 SQL Builder 中根据 `xxx_id / xxx_code / xxx_name` 猜 JOIN。
3. ❌ 禁止在 SQL Builder 中调用 Semantic Search。
4. ❌ 禁止在 SQL Builder 中调用 Relation Resolver。
5. ❌ 禁止把 `QueryJoinCandidate` 当作 Executable Join。
6. ❌ 禁止 DirectKey 生成 JOIN。
7. ❌ 禁止跨 DataSource Federation。
8. ❌ 禁止跨 Tenant JOIN。
9. ❌ 禁止硬编码 `material_master` / `supplier_master` 等业务表。
10. ❌ 禁止修改 GQ-011 / Ranking / Evaluator / Golden Contract。
11. ❌ 禁止重写现有 WHERE / ORDER / LIMIT Contract。
12. ❌ 禁止为了 JOIN SQL 通过修改 Golden 掩盖 QueryPlan 缺陷。

## 六、兼容性影响矩阵：FROZEN

| Case | D11 预期 | 兼容性要求 |
|---|---|---|
| GQ-001 | 单表原路径 | PASS，SQL 不发生语义漂移 |
| GQ-002 | EntityCount | COUNT / Limit 不漂移 |
| GQ-003 | Dimension | DirectKey 时仍为单表 SQL；MasterJoin 时正确 JOIN |
| GQ-005 | Dimension + Filter | WHERE 必须保持 |
| GQ-006 | 当前无 Master | DirectKey 不 JOIN；后续 Snapshot 出现 Master 时允许 JOIN |
| GQ-009 | Dimension + Date | Date Filter 不漂移 |
| GQ-010 | Dimension + Ranking | ORDER BY + LIMIT 必须保持；MasterJoin 只增加正确 JOIN |
| **GQ-011** | 无 Dimension | **必须继续原单表 PASS；不得增加 JOIN** |
| Negative | NotResolved | 不进入 SQL Runtime |
| Ambiguous | BLOCK | 不允许猜 JOIN |
| MasterJoin | Executable | 生成准确 JOIN |
| DirectKey | Executable | 不生成 JOIN |

**任何既有 PASS Case 因 D11 产生回归，立即停止后续实现验证，先解决兼容性问题。**

## 七、D11 实现前提

D11 不能独立实现完成。它依赖：

```text
D09 MasterJoin Binding Contract
D10 DirectKey Binding Contract
D08 QueryPlan Dimension Binding
```

因此当前只冻结 SQL Builder 的消费边界，不提前修改代码。

## 八、D11 冻结

**D11：FROZEN**

冻结内容：

- 全量源码 / Contract 审计结论
- 最终修改范围
- 禁止修改范围
- 兼容性影响矩阵
- D09 / D10 → D11 消费边界

功能实现状态：**NOT IMPLEMENTED**。

## 九、下一步

```text
D11 FROZEN
   ↓
更新 Phase 2.7 开发计划
   ↓
同步主计划
   ↓
确认 master
   ↓
D12 — Contract / DI / Namespace 全量审计
```
