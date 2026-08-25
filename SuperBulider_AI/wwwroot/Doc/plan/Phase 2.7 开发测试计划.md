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
| **D12** | **Contract / DI / Namespace 全量源码审计** | **FROZEN** |
| D13 | Release Build / 首轮实现前 Build 基线验证 | NEXT |
| D14 | Controller / Runtime | PLANNED |
| D15 | MasterJoin Golden | PLANNED |
| D16 | DirectKey Golden | PLANNED |
| D17 | SameTable / CrossTable Golden | PLANNED |
| D18 | Ambiguous / NotResolved Safety Regression | PLANNED |
| D19 | Full Golden Regression | PLANNED |
| D20 | Coverage / Quality / Release Gate | PLANNED |
| D21 | Phase 2.7 Exit Review | PLANNED |

# D12 — Contract / DI / Namespace 全量源码审计

## 1. 最终审计结论

D12 已完成 Model、Interface、Implementation、Caller、Constructor、DI、Namespace、Controller、Golden / Runtime 边界全链路审计。本 STEP **未修改业务源码**，结论 **FROZEN / 功能 NOT IMPLEMENTED**。

当前 master 的关键问题不是缺少一个 DI 注册，而是 D07-D11 冻结的新版 Contract 尚未落地，因此 DI 仍绑定旧职责链。`Program.cs` 当前注册 `QueryPlanBuilder`、`IQueryPlanBuilder`、`QueryJoinInferenceService`、`IQueryJoinInferenceService`、`QueryPlanValidator`、`SemanticApplicabilityEvaluator`、`ISqlQueryBuilder/SqlQueryBuilder` 等服务。fileciteturn443file0L2-L6

`QueryPlanBuilder` 当前构造函数仍依赖 `IMetadataSemanticSearchService`、`IQueryJoinInferenceService`、`QueryPlanValidator`；其中 JoinInference 的 Executable Join 权限必须在后续实现中隔离。fileciteturn448file0L2-L5

现有 `IQueryPlanBuilder` 已存在且由 `QueryPlanBuilder` 实现，因此不新建平行 Builder。fileciteturn452file0L2-L5 QueryPlanBuilder 采用 partial 文件拆分，后续必须沿现有职责边界修改，不新建重复 Builder。fileciteturn445file0L2-L4

## 2. D12 最终 Contract

```text
QueryIntent
   ↓
Semantic Applicability / Resolution
   ↓
Dimension Resolution
   ├── MasterJoin
   ├── DirectKey
   ├── Ambiguous
   └── NotResolved
   ↓
QueryPlan Dimension Binding / QueryPlan.Joins
   ↓
SqlQueryBuilder
```

必须保持单一 Resolution 真相源。禁止 QueryPlanBuilder 或 SqlQueryBuilder 再次 Semantic Search / Relation Inference / 字段名猜 JOIN。

## 3. 最终修改范围：FROZEN

### Models / Contract

- 扩展现有 `Models.BI` Contract，不新建第二套 QueryPlan / QueryDimension。
- DirectKey / Dimension Binding 必须表达 `ResolutionType`、`ResolutionState`、`ExecutionCapability`、FactTable、FactDataSource、DimensionKey、可选 DimensionLabel、Evidence / Confidence / Metadata Snapshot Reference。
- MasterJoin 与 DirectKey 必须互斥。

### Interfaces

- 复用 `IQueryPlanBuilder`。
- 收口 `IQueryJoinInferenceService`：只提供 Relation Evidence，不作为 Executable Join 来源。
- 如 D07/D10 确需独立 Resolver，只新增最小 Interface，不复制 Metadata Semantic Search Service。
- SQL Builder 继续复用 `ISqlQueryBuilder`。

### Services / Callers

- `SemanticApplicabilityEvaluator`：成为 Dimension Resolution 决策入口，组合 D05 EntityKey + D06 Relation Evidence。
- `QueryPlanSemanticResolutionFactory`：只做 Resolution → QueryPlan Resolution 传递，不重新搜索。
- `QueryPlanBuilder`：只做已确认 Resolution → QueryPlan 装配，隔离旧 `BuildJoinsAsync → IQueryJoinInferenceService → QueryPlan.Joins` Executable 旁路。
- `SqlQueryBuilder`：只消费 QueryPlan。
- `QueryPlanValidator`：只做物理一致性验证，不推理。

### DI

`Program.cs` 为正式 DI 根。实现阶段仅在现有 DI 链增加 / 替换必要服务，并保持 BI / Metadata 服务的 Scoped 生命周期；无理由不得改 Singleton。fileciteturn443file0L2-L6

```text
Semantic Applicability
    ↓
Dimension Resolution Contract
    ↓
QueryPlan Resolution Factory
    ↓
QueryPlanBuilder
    ↓
ISqlQueryBuilder
```

### Namespace

统一使用现有：

```text
SuperBuilder_AI.Models.*
SuperBuilder_AI.Interfaces.*
SuperBuilder_AI.Services.BI.*
SuperBuilder_AI.Services.BI.Evaluation.*
```

不得引入第二套 C# namespace。

## 4. 禁止修改范围：FROZEN

- 不新建平行 `IQueryPlanBuilder` / `QueryPlanBuilder`。
- 不复制 Metadata Semantic Search Service。
- 不让 SQL Builder 调用 Resolver。
- 不让 QueryPlanBuilder 自行重新推理 Relation。
- 不把 `QueryJoinCandidate` 直接转为 Executable Join。
- 不把 DirectKey 转成 QueryJoin。
- 不修改 GQ-011 Ranking Contract。
- 不修改 Evaluator / Golden Contract 掩盖实现缺失。
- 不跨 DataSource / Tenant Federation。
- 不硬编码物料、供应商主表或字段。
- 不修改 Golden Dataset。
- 不新建独立 Test Project。

## 5. 兼容性影响矩阵：FROZEN

| 范围 | 预期 | 强制要求 |
|---|---|---|
| GQ-001 | 单表 Metric | 保持 PASS |
| GQ-002 | EntityCount | 不改变 EntityCount Contract |
| GQ-003/005/009 | Dimension / Filter | 不得导致 Metric / Filter 漂移 |
| GQ-006 | 当前无 Master | 稳定 DirectKey 才可执行；Evidence 不足仍 BLOCK |
| GQ-010 | Dimension + Ranking | DirectKey 不得破坏 Order / Limit |
| GQ-011 | Detail Ranking，无 Dimension | **完全绕过 Dimension Resolution，继续 PASS** |
| MasterJoin | 稳定 Relation | 只能由 Resolution 产生 QueryPlan.Joins |
| DirectKey | 无 Master + 稳定 Fact Key | 不产生 Joins |
| Ambiguous | 多候选 | BLOCK |
| NotResolved | 无稳定绑定 | BLOCK |
| Future Snapshot | 新增主表 / 关系 | 允许重新解析 DirectKey → MasterJoin |

任一既有 PASS Case 回归，立即停止当前实现验证，先修复兼容性问题，再重新执行受影响 Case 与完整 Golden Regression。

## 6. 首轮实现顺序：FROZEN

D12 后不得拆成临时修改链，必须依据冻结范围一次性形成源码修改清单：

```text
Models / Contract
 ↓
Interfaces
 ↓
Resolution / Factory
 ↓
QueryPlan Binding
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

任何超出 D09-D12 冻结范围的文件，必须重新进行 Contract 审计。

## 7. D12 冻结

**D12 = FROZEN。**

功能实现状态：**NOT IMPLEMENTED**。

冻结内容：Contract、Interface、Caller、DI、Namespace、职责边界、最终修改范围、禁止修改范围、兼容性矩阵。

下一步：**D13 — Release Build / 首轮实现前 Build 基线验证**。

---

# Phase 2.7 全局兼容性原则

1. GQ-011 必须保持 PASS，且完全不进入 Dimension / MasterJoin / DirectKey。
2. GQ-006 / GQ-010 当前无 Master 时，只要存在稳定 DirectKey Evidence，应允许 DirectKey；Evidence 不足必须安全 BLOCK。
3. 新增 DataSource 后重新扫描 Metadata、生成 Semantic / Vector 并形成新的 Metadata Snapshot，允许同一业务问题从 DirectKey 重新解析为 MasterJoin。
4. 不允许硬编码业务主表、字段或固定跨库关系。
5. 任一既有 PASS Case 出现回归，立即停止当前实现验证并先解决兼容性问题。
6. 每个 STEP 冻结后必须先更新阶段计划、同步主计划并确认 master，否则不得进入下一 STEP。
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
D12 FROZEN
 ↓
阶段计划已同步
 ↓
主计划已同步
 ↓
确认 master
 ↓
D13 — Release Build / 首轮实现前 Build 基线验证
```
