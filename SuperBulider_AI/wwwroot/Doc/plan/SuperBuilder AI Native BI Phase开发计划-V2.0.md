# SuperBuilder AI Native BI Phase开发计划

> 文档版本：v2.17  
> 文档性质：项目正式开发基线 + Phase 开发测试管理总计划  
> **唯一源码基线：GitHub `master`**

## 一、当前项目状态

- 当前 Phase：**Phase 2.7 — DimensionAware QueryPlan**
- Phase 状态：**IN_PROGRESS**
- 当前工作单元：**D12 — Contract / DI / Namespace 全量源码审计已冻结**
- 当前状态：**D12 FROZEN / 功能实现 NOT IMPLEMENTED**
- 下一步：**D13 — Release Build / 首轮实现前 Build 基线验证**
- D05-D11：全部 FROZEN
- 本轮未修改业务源码。

## 二、Phase 2.7 当前冻结链

```text
D05 Entity Key Contract              FROZEN
        ↓
D06 Relation Evidence                FROZEN
        ↓
D07 Dynamic Dimension Resolution     FROZEN
        ↓
D08 QueryPlan Dimension Binding      FROZEN
        ↓
D09 MasterJoin QueryPlan             FROZEN
        ↓
D10 DirectKey QueryPlan              FROZEN
        ↓
D11 SQL Builder 双路径              FROZEN
        ↓
D12 Contract / DI / Namespace       FROZEN
        ↓
D13 Release Build / 首轮实现前基线   NEXT
```

## 三、D12 冻结结论

D12 已完成 Model、Interface、Implementation、Caller、Constructor、DI、Namespace、Controller、Golden / Runtime 边界全链路审计，未修改业务源码。

当前 master 的核心状态是：新版 D07-D11 Contract 尚未落地，DI 仍绑定旧职责链。`Program.cs` 当前注册 `QueryPlanBuilder`、`IQueryPlanBuilder`、`QueryJoinInferenceService`、`IQueryJoinInferenceService`、`QueryPlanValidator`、`SemanticApplicabilityEvaluator`、`ISqlQueryBuilder/SqlQueryBuilder` 等服务。fileciteturn443file0L2-L6

`QueryPlanBuilder` 当前仍依赖 `IMetadataSemanticSearchService`、`IQueryJoinInferenceService`、`QueryPlanValidator`；后续实现必须隔离 JoinInference 的 Executable Join 权限。fileciteturn448file0L2-L5

`IQueryPlanBuilder` 已存在，不需要新建平行 Builder；QueryPlanBuilder 使用多个 partial 文件，应沿现有职责边界修改。fileciteturn452file0L2-L5

## 四、D12 冻结后的首轮实现边界

```text
QueryIntent
   ↓
Semantic Applicability / Resolution
   ├── MasterJoin
   ├── DirectKey
   ├── Ambiguous
   └── NotResolved
   ↓
QueryPlan Binding
   ↓
SqlQueryBuilder
```

- Models / Contract：扩展现有 QueryPlan / Dimension Binding Contract，不新建平行模型体系。
- Interfaces：复用 `IQueryPlanBuilder`、`ISqlQueryBuilder`；Relation Service 只提供 Evidence。
- Resolution / Factory：形成单一 Resolution 真相源，不重新搜索。
- QueryPlanBuilder：只装配已确认 Resolution，隔离旧自动 JOIN 旁路。
- SqlQueryBuilder：只消费 QueryPlan；MasterJoin 才生成 JOIN，DirectKey 永不生成 JOIN。
- Validator：只验证，不推理。
- Program.cs：只做必要 DI 注册 / 替换，并保持现有 Scoped 生命周期原则。
- Namespace：统一 `SuperBuilder_AI.Models.*`、`SuperBuilder_AI.Interfaces.*`、`SuperBuilder_AI.Services.BI.*`、`SuperBuilder_AI.Services.BI.Evaluation.*`。

## 五、D12 禁止修改范围

- 不新建平行 QueryPlanBuilder / Interface。
- 不复制 Metadata Semantic Search。
- 不让 SQL Builder 调用 Resolver。
- 不让 Builder 自行推理 Relation。
- 不把 QueryJoinCandidate 当 Executable Join。
- 不把 DirectKey 转成 QueryJoin。
- 不修改 GQ-011、Evaluator、Golden、Coverage、Gate 来掩盖能力缺失。
- 不跨 DataSource / Tenant Federation。
- 不硬编码物料 / 供应商主表或字段。
- 不新建独立 Test Project。

## 六、D12 兼容性 Gate

| 范围 | 强制要求 |
|---|---|
| GQ-001 | 保持 PASS |
| GQ-002 | EntityCount Contract 不变 |
| GQ-003/005/009 | Metric / Filter 不漂移 |
| GQ-006 | 稳定 DirectKey 才执行，无证据仍 BLOCK |
| GQ-010 | DirectKey 不破坏 Ranking / Order / Limit |
| **GQ-011** | **完全绕过 Dimension Resolution，继续 PASS** |
| MasterJoin | 仅由 Resolution 产生 QueryPlan.Joins |
| DirectKey | 不产生 Joins |
| Ambiguous / NotResolved | BLOCK，不猜测 |
| 新 Metadata Snapshot | 允许 DirectKey → MasterJoin 重新解析 |

任何既有 PASS Case 回归，立即停止当前实现验证并先修复兼容性问题。

## 七、项目强制开发规则

1. GitHub `master` 是唯一源码基线。
2. 正式源码与正式计划直接更新 `master`，使用中文 Commit 描述。
3. 不新建独立 Test Project；优先使用 Controller / Action Runtime 真实验证。
4. 代码文件存在不等于功能完成，必须检查 Interface、Implementation、Caller、Model/DTO、Constructor、DI、Runtime、Controller、Golden Regression。
5. AI 生成或修改的代码与人工代码采用相同审计、Build、Runtime、Regression 标准。
6. 禁止边审边改；每个 STEP 必须先完成全量源码 / Contract 审计，再形成一次性修改范围。
7. **每个 STEP 冻结后必须先更新阶段开发计划，再同步主计划，再确认 GitHub `master`，然后才能进入下一 STEP。**
8. 阶段计划任何实质性变化必须同步主计划及受影响 Runtime 记录。
9. 代码修改推送 `master` 后，必须由本地环境先 `git pull`，再 Build，再执行 Controller / Action Runtime 测试；本地结果作为正式验收证据。
10. 未完成 Runtime / Regression / Exit Criteria，不得宣布 Phase COMPLETE。
11. 禁止通过修改 Golden、Evaluator、Coverage、Gate 或删除功能掩盖真实能力缺失。

## 八、长期产品目标

SuperBuilder 最终建设为：

> **支持多语言、多租户、多数据库动态接入的 AI Native Low-code Platform。**

业务数据库关系不得预绑定为永久事实。Metadata Relation 必须基于当前 Snapshot 动态解析；新增数据库、表、字段或 Relation Evidence 后允许重新解析并升级为 MasterJoin；关系证据失效后允许重新计算并安全降级。

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

## 九、下一步

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

D13 仍不得自行扩大 D09-D12 已冻结的源码修改范围。
