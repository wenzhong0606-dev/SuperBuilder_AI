# SuperBuilder AI Native BI Phase开发计划

> 文档版本：v2.16  
> 文档性质：项目正式开发基线 + Phase 开发测试管理总计划  
> **唯一源码基线：GitHub `master`**

## 一、当前项目状态

- 当前 Phase：**Phase 2.7 — DimensionAware QueryPlan**
- Phase 状态：**IN_PROGRESS**
- 当前工作单元：**D11 — SQL Builder 双路径（MasterJoin / DirectKey）审计已冻结**
- 当前状态：**D11 FROZEN / 功能实现 NOT IMPLEMENTED**
- 下一步：**D12 — Contract / DI / Namespace 全量源码审计**
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
D12 Contract / DI / Namespace       NEXT
```

## 三、D11 冻结结论

当前 `SqlQueryBuilder` 仅支持单表 SQL；`QueryPlan.Tables.Count > 1` 当前直接阻断，因此 MasterJoin QueryPlan 尚不能进入 SQL Runtime。Builder 已有 SELECT / WHERE / GROUP BY / ORDER BY / LIMIT 能力，`QueryPlan` 已有 `Joins`，`QueryJoin` 已有两端 Table/Column 和 JoinType，但 JOIN SQL 消费路径尚未实现。fileciteturn423file0L2-L2 fileciteturn424file0L2-L2 fileciteturn427file0L2-L2

D11 冻结的实现边界：

```text
D09 MasterJoin Binding ─┐
                        ├→ QueryPlan → SqlQueryBuilder
D10 DirectKey Binding ──┘
```

- MasterJoin：只消费已确认 `QueryPlan.Joins`，生成受控 JOIN SQL。
- DirectKey：不生成 JOIN，保持单表 SQL，并消费已确认 Dimension Key / Label。
- SQL Builder 不得 Semantic Search、Relation 推理或根据字段名猜 JOIN。
- JoinType 仅允许 `INNER / LEFT / RIGHT`。
- 不允许跨 DataSource / Tenant Federation。
- 不允许业务主表 / 字段硬编码。

当前 `QueryPlanBuilder.PlanAssembly` 仍存在旧的 `BuildJoinsAsync → QueryJoinInferenceService → QueryPlan.Joins` 自动 JOIN 旁路；该旁路属于 D09 已冻结的待隔离实现范围，不能在 D11 中重新设计 Relation。fileciteturn429file0L2-L2

D11 详细审计冻结记录：

`wwwroot/Doc/plan/Phase 2.7-D11 SQL Builder双路径审计冻结.md`

## 四、兼容性 Gate

以下规则为 Phase 2.7 强制 Gate：

1. **GQ-011 必须继续 PASS**，且完全不进入 Dimension / MasterJoin / DirectKey。
2. GQ-001 / GQ-002 等既有正确 Case 不得因为 D09-D11 产生语义漂移。
3. GQ-006 / GQ-010 当前无 Master 时，若存在稳定 DirectKey Evidence，应走 DirectKey；Evidence 不足必须安全 BLOCK。
4. 新增 DataSource 后必须重新扫描 Metadata、生成 Semantic / Vector 并形成新的 Metadata Snapshot；同一业务问题允许从 DirectKey 重新解析为 MasterJoin。
5. Ambiguous / NotResolved 不得猜测 Relation，不得进入错误 SQL Runtime。
6. 任一既有 PASS Case 回归，立即停止当前实现验证，先解决兼容性问题，再继续。

## 五、开发流程强制规则

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

## 六、长期产品目标

SuperBuilder 最终建设为：

> **支持多语言、多租户、多数据库动态接入的 AI Native Low-code Platform。**

业务数据库关系不得预绑定为永久事实。Metadata Relation 必须基于当前 Snapshot 动态解析；新增数据库、表、字段或 Relation Evidence 后允许重新解析并升级为 MasterJoin；关系证据失效时允许重新计算并安全降级。

Metadata 生命周期的长期目标为：

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

该生命周期与 D09/D10 的动态 Relation / Dimension Resolution Contract 必须保持一致。

## 七、下一步

```text
D11 FROZEN
 ↓
阶段计划已同步
 ↓
主计划 v2.16 已同步
 ↓
确认 master
 ↓
D12 — Contract / DI / Namespace 全量源码审计
```

D12 仍然遵守：**完整审计 → 最终结论 → 冻结 → 更新阶段计划 → 同步主计划 → 确认 master → 下一 STEP。**
