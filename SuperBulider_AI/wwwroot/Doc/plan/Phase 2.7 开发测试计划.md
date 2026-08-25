# Phase 2.7 — DimensionAware QueryPlan 开发测试计划

> 状态：IN_PROGRESS  
> 唯一源码基线：GitHub `master`  
> 执行规则：每个 STEP 必须执行“完整源码 / Contract 审计 → 最终结论 → 冻结 → 更新阶段计划 → 同步主计划 → 确认 master → 下一 STEP”。

## 一、STEP 冻结状态

| STEP | 主题 | 状态 |
|---|---|---|
| D05 | Dimension Entity Key Resolver Contract | FROZEN |
| D06 | Dynamic Master Table / Relation Detection | FROZEN |
| D07 | Dynamic Dimension Resolution | FROZEN |
| D08 | QueryPlan Dimension Binding | FROZEN |
| D09 | MasterJoin QueryPlan | FROZEN |
| D10 | DirectKey QueryPlan | FROZEN |
| **D11** | **SQL Builder 双路径（MasterJoin / DirectKey）** | **FROZEN** |
| D12 | Contract / DI / Namespace 全量审计 | NEXT |
| D13 | Release Build | PLANNED |
| D14 | Controller / Runtime | PLANNED |
| D15 | MasterJoin Golden | PLANNED |
| D16 | DirectKey Golden | PLANNED |
| D17 | SameTable / CrossTable Golden | PLANNED |
| D18 | Ambiguous / NotResolved Safety Regression | PLANNED |
| D19 | Full Golden Regression | PLANNED |
| D20 | Coverage / Quality / Release Gate | PLANNED |
| D21 | Phase 2.7 Exit Review | PLANNED |

---

# D11 — SQL Builder 双路径（MasterJoin / DirectKey）全量源码 / Contract 审计

## 1. 最终审计结论

D11 已完成完整源码 / Contract 审计，**本 STEP 未修改业务源码**，结论冻结为 **FROZEN / 功能 NOT IMPLEMENTED**。

当前 `SqlQueryBuilder` 只有单表 SQL 执行路径；源码在 `QueryPlan.Tables.Count > 1` 时直接抛出异常，因此当前 MasterJoin QueryPlan 无法进入 SQL Runtime。Builder 已具备 SELECT、WHERE、GROUP BY、ORDER BY、LIMIT 能力，并优先消费 `QueryPlan.Dimensions` / `QueryPlan.Orders`。fileciteturn423file0L2-L2

`QueryPlan` 已存在 `Tables / Fields / Metrics / Dimensions / Filters / Orders / Joins / Limit`，`QueryJoin` 已具备左右 Table / Column 以及 JoinType，因此不需要重新设计基础 QueryJoin 模型。fileciteturn424file0L2-L2 fileciteturn427file0L2-L2

当前 `QueryPlanBuilder.PlanAssembly` 仍存在旧的：

```text
BuildJoinsAsync
    ↓
IQueryJoinInferenceService
    ↓
QueryJoinCandidate
    ↓
QueryPlan.Joins
```

该旧路径已在 D09 冻结为待隔离路径。D11 不重新推理 Relation；Executable MasterJoin 必须来自 D09 Resolution。fileciteturn429file0L2-L2

## 2. D11 最终 Contract

### MasterJoin

```text
D09 MasterJoin Binding
        ↓
QueryPlan.Joins
        ↓
SqlQueryBuilder
        ↓
FROM FactTable
JOIN DimensionTable
  ON FactKey = DimensionKey
```

SQL Builder 只能消费已确认 `QueryPlan.Joins`，不得 Semantic Search、Relation 推理或根据字段名猜 JOIN。

JoinType 仅允许：`INNER / LEFT / RIGHT`。

### DirectKey

```text
D10 DirectKey Binding
        ↓
QueryPlan Dimension
        ↓
SqlQueryBuilder
        ↓
单表 FROM
+ Dimension Key / Label
```

**DirectKey 不生成 JOIN。**

## 3. D11 最终修改范围：FROZEN

1. `SqlQueryBuilder` 移除“多表一律失败”的限制，但仅允许包含完整、已确认 `QueryPlan.Joins` 的多表 QueryPlan。
2. 增加 JOIN SQL 生成专用路径，例如 `BuildJoins` / `BuildJoinExpression`。
3. JOIN 只能消费 `QueryPlan.Joins`。
4. JOIN 两端 Table / Column 必须存在于当前 QueryPlan 并且物理绑定一致。
5. JoinType 使用白名单校验。
6. DirectKey 不生成 JOIN。
7. 保持现有 SELECT / WHERE / GROUP BY / ORDER BY / LIMIT Contract。
8. 保持 `ISqlDialect` 抽象；只有 Dialect Contract 不足时才做最小通用扩展。
9. 必要时由 Validator 增加 QueryJoin 引用完整性、Table / Column / DataSource 一致性验证；Validator 只验证、不推理。

## 4. D11 禁止修改范围：FROZEN

- 禁止在 SQL Builder 中调用 Semantic Search。
- 禁止在 SQL Builder 中调用 Relation Resolver。
- 禁止根据 `xxx_id / xxx_code / xxx_name` 猜 JOIN。
- 禁止把 `QueryJoinCandidate` 直接当 Executable Join。
- 禁止 DirectKey 生成 JOIN。
- 禁止跨 DataSource / Tenant Federation。
- 禁止硬编码 `material_master` / `supplier_master` 等业务表。
- 禁止修改 GQ-011、Ranking、Evaluator、Golden Contract。
- 禁止为了 JOIN 修改 WHERE / ORDER / LIMIT Contract。

## 5. 兼容性影响矩阵：FROZEN

| Case | D11 预期 | 兼容性要求 |
|---|---|---|
| GQ-001 | 单表原路径 | 必须继续 PASS |
| GQ-002 | EntityCount | COUNT / Limit 不漂移 |
| GQ-003 | Dimension | DirectKey 单表；MasterJoin 正确 JOIN |
| GQ-005 | Dimension + Filter | WHERE 不漂移 |
| GQ-006 | 当前无 Master | DirectKey 不 JOIN；未来 Snapshot 可升级 MasterJoin |
| GQ-009 | Dimension + Date | Date Filter 不漂移 |
| GQ-010 | Dimension + Ranking | ORDER BY / LIMIT 必须保持 |
| **GQ-011** | 无 Dimension | **必须继续原单表 PASS，不增加 JOIN** |
| Negative | NotResolved | 不进入 SQL Runtime |
| Ambiguous | BLOCK | 不允许猜 JOIN |

任何既有 PASS 因 D11 实现产生回归，立即停止后续实现验证，先修复兼容性问题，再重新执行受影响 Case 和完整 Golden Regression。

## 6. D11 冻结

**D11 = FROZEN；功能实现 = NOT IMPLEMENTED。**

独立审计记录：

`wwwroot/Doc/plan/Phase 2.7-D11 SQL Builder双路径审计冻结.md`

## 7. 下一步

```text
D11 FROZEN
 ↓
更新阶段计划
 ↓
同步主计划 v2.16
 ↓
确认 GitHub master
 ↓
D12 — Contract / DI / Namespace 全量源码审计
```

---

# 全局 Phase 2.7 兼容性原则

1. GQ-011 必须保持 PASS，且完全不进入 Dimension / MasterJoin / DirectKey。
2. GQ-006 / GQ-010 当前无 Master 时，只要存在稳定 DirectKey Evidence，应允许 DirectKey；Evidence 不足仍必须安全 BLOCK。
3. 新增 DataSource 后重新扫描 Metadata、生成 Semantic / Vector 并形成新的 Metadata Snapshot，允许同一业务问题从 DirectKey 重新解析为 MasterJoin。
4. 不允许硬编码业务主表、字段或固定跨库关系。
5. 任一既有 PASS Case 出现回归，立即停止当前实现验证并先解决兼容性问题。
6. 每个 STEP 冻结后必须先更新阶段计划、同步主计划并确认 `master`，否则不得进入下一 STEP。
7. 计划中的模型、接口或服务不存在于当前 master 时，不得当作已实现能力。
8. 不新建独立 Test Project；正式验证使用现有 Controller / Action Runtime 与 Golden Regression。

# 长期产品目标

SuperBuilder 最终建设为：

> **支持多语言、多租户、多数据库动态接入的 AI Native Low-code Platform。**

动态 Metadata Relation 不得预绑定为永久事实：

```text
DataSource 添加
 ↓
数据库扫描
 ↓
Metadata Schema
 ↓
字段 + 备注
 ↓
AI Semantic Generation
 ↓
Embedding / Vector
 ↓
Metadata Snapshot
 ↓
Dynamic Relation / Dimension Resolution
```

新增 DataSource / Table / Column / Relation Evidence 后允许重新解析；Relation Evidence 失效后允许重新计算并安全降级。

# D12 入口

**D12 — Contract / DI / Namespace 全量源码 / Contract 审计**

D12 开始前必须重新读取当前 GitHub `master`，不修改业务源码；完成后仍执行：

> **最终结论 → 最终修改范围 → 禁止修改范围 → 兼容性影响矩阵 → 冻结 → 更新阶段计划 → 同步主计划 → 确认 master → 下一 STEP。**
