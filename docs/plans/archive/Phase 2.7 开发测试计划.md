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
| D11 | SQL Builder 双路径（MasterJoin / DirectKey） | FROZEN |
| D12 | Contract / DI / Namespace 全量源码审计 | FROZEN |
| **D13** | **Release Build / 首轮实现前 Build 基线验证** | **FROZEN** |
| D14 | 首轮源码实现 / Controller / Runtime | NEXT |
| D15 | MasterJoin Golden | PLANNED |
| D16 | DirectKey Golden | PLANNED |
| D17 | SameTable / CrossTable Golden | PLANNED |
| D18 | Ambiguous / NotResolved Safety Regression | PLANNED |
| D19 | Full Golden Regression | PLANNED |
| D20 | Coverage / Quality / Release Gate | PLANNED |
| D21 | Phase 2.7 Exit Review | PLANNED |

# D13 — Release Build / 首轮实现前 Build 基线验证

## 1. 验证结果

用户已在当前 GitHub `master` 对应的本地代码完成：

- `git pull` 后代码同步；
- 项目编译通过；
- 应用正常启动。

因此 D13 的目标——确认首轮实现前当前 master 具备可编译、可启动的稳定基线——已通过。

**D13 = FROZEN。**

本 STEP 不修改业务源码；本次冻结的是 Build / Startup 基线，不代表 D07-D12 的功能 Contract 已经实现。

## 2. D13 基线结论

```text
GitHub master
    ↓
git pull
    ↓
Build = PASS
    ↓
Startup = PASS
    ↓
首轮源码实现可以开始
```

当前功能实现状态仍为：**NOT IMPLEMENTED**。

## 3. D13 对首轮实现的强制边界

首轮源码实现只能消费 D09-D12 已冻结的修改范围：

```text
Models / Contract
 ↓
Interfaces
 ↓
Resolution / Factory
 ↓
QueryPlan Dimension Binding
 ↓
QueryPlanBuilder 旧 JOIN 旁路隔离
 ↓
SqlQueryBuilder 双路径
 ↓
Validator
 ↓
Program.cs DI
 ↓
Build
 ↓
Controller Runtime
```

禁止在 D14 实现过程中重新设计 D09-D12 已冻结 Contract。若发现实际源码与冻结 Contract 不一致，必须先暂停实现并重新进行对应 STEP 的 Contract 审计。

## 4. D13 禁止修改范围

- 不修改 Golden Dataset。
- 不修改 GQ-011 Ranking Contract。
- 不修改 Evaluator / Coverage / Gate 来规避实现问题。
- 不扩大 DirectKey / MasterJoin 的业务范围。
- 不硬编码物料、供应商主表或字段。
- 不跨 DataSource / Tenant Federation。
- 不新建独立 Test Project。
- 不在 D14 中临时增加未审计的 Factory / Resolver / Builder 平行实现。

## 5. D13 兼容性 Gate

首轮实现必须以当前可编译、可启动 master 为回归基线：

| Case | 首轮实现要求 |
|---|---|
| GQ-001 | 不得破坏原 PASS |
| GQ-002 | EntityCount Contract 不变 |
| GQ-006 | 稳定 DirectKey 才执行；无证据安全 BLOCK |
| GQ-010 | Ranking / Order / Limit 不漂移 |
| **GQ-011** | **继续 PASS，完全绕过 Dimension Resolution** |
| MasterJoin | 仅由已确认 Resolution 生成 QueryPlan.Joins |
| DirectKey | 不生成 QueryJoin |
| Ambiguous / NotResolved | BLOCK，不猜测 |

任何既有 PASS Case 出现回归，立即停止 D14 当前实现验证，先定位并修复兼容性问题。

# D12 历史冻结

D12 已完成 Model、Interface、Implementation、Caller、Constructor、DI、Namespace、Controller、Golden / Runtime 边界全链路审计。新版 D07-D11 Contract 尚未落地；DI 仍绑定旧职责链。D12 最终冻结了 Contract、Interface、Caller、DI、Namespace、职责边界、最终修改范围、禁止修改范围和兼容性矩阵。

# Phase 2.7 全局兼容性原则

1. GQ-011 必须保持 PASS，且完全不进入 Dimension / MasterJoin / DirectKey。
2. GQ-006 / GQ-010 当前无 Master 时，只要存在稳定 DirectKey Evidence，应允许 DirectKey；Evidence 不足必须安全 BLOCK。
3. 新增 DataSource 后重新扫描 Metadata、生成 Semantic / Vector 并形成新的 Metadata Snapshot，允许同一业务问题从 DirectKey 重新解析为 MasterJoin。
4. 不允许硬编码业务主表、字段或固定跨库关系。
5. 任一既有 PASS Case 出现回归，立即停止当前实现验证并先解决兼容性问题。
6. **每个 STEP 冻结后必须先更新阶段计划、同步主计划并确认 master，否则不得进入下一 STEP。**
7. 计划中的模型、接口或服务不存在于当前 master 时，不得当作已实现能力。
8. 不新建独立 Test Project；正式验证使用现有 Controller / Action Runtime 与 Golden Regression。

# 长期产品目标

SuperBuilder 最终建设为：

> **支持多语言、多租户、多数据库动态接入的 AI Native Low-code Platform。**

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
Semantic / SearchText
 ↓
Embedding
 ↓
Vector Database
 ↓
当前 Metadata Snapshot
 ↓
Dynamic Resolution
```

业务数据库关系不得预绑定为永久事实；新增 DataSource / Table / Column / Relation Evidence 后允许重新解析并升级为 MasterJoin，关系证据失效后允许重新计算并安全降级。

# 下一步

```text
D13 FROZEN
 ↓
阶段计划已同步
 ↓
主计划同步
 ↓
确认 master
 ↓
D14 — 首轮源码实现 / Controller / Runtime
```
