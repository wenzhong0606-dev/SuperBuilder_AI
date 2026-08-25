# SuperBuilder AI Native BI Phase开发计划

> 文档版本：v2.18  
> 文档性质：项目正式开发基线 + Phase 开发测试管理总计划  
> **唯一源码基线：GitHub `master`**

## 一、当前项目状态

- 当前 Phase：**Phase 2.7 — DimensionAware QueryPlan**
- Phase 状态：**IN_PROGRESS**
- 当前工作单元：**D13 — Release Build / 首轮实现前 Build 基线验证已冻结**
- 当前状态：**D13 FROZEN / 功能实现 NOT IMPLEMENTED**
- 下一步：**D14 — 首轮源码实现 / Controller / Runtime**
- D05-D12：全部 FROZEN
- D13：Build PASS + Startup PASS

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
D13 Release Build / 实现前基线       FROZEN
        ↓
D14 首轮源码实现 / Runtime           NEXT
```

## 三、D13 冻结结论

用户已确认当前 `master` 对应本地代码完成同步后：

- 编译通过；
- 应用正常启动。

因此 D13 的目标——确认首轮实现前当前 master 具备稳定的 Build / Startup 基线——已通过并冻结。

**D13 = FROZEN。**

本 STEP 未修改业务源码。D13 冻结只代表 Build / Startup 基线通过，不代表 D07-D12 的功能 Contract 已实现。

## 四、D14 首轮源码实现边界

D14 是 Phase 2.7 首次进入业务源码实现的 STEP。实现必须严格消费 D09-D12 已冻结的范围，不重新设计已冻结 Contract。

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

首轮实现目标：建立唯一 Resolution 真相源，让 Dimension 能在当前 Metadata Snapshot 下选择：

```text
MasterJoin
DirectKey
Ambiguous
NotResolved
```

并保证：

- MasterJoin 才产生 `QueryPlan.Joins`；
- DirectKey 永不产生 `QueryJoin`；
- Builder 不重新 Semantic Search / Relation Inference；
- SQL Builder 只消费 QueryPlan；
- Validator 只验证，不推理；
- 不硬编码业务主表、字段或跨库关系。

如果 D14 实现过程中发现实际 master 与 D09-D12 冻结 Contract 不一致，必须立即停止实现并重新进行对应 Contract 审计，不得临时扩大修改范围。

## 五、D14 禁止修改范围

- 不修改 Golden Dataset。
- 不修改 GQ-011 Ranking Contract。
- 不修改 Evaluator / Coverage / Gate 来规避实现问题。
- 不扩大 DirectKey / MasterJoin 的业务范围。
- 不硬编码物料、供应商主表或字段。
- 不跨 DataSource / Tenant Federation。
- 不新建独立 Test Project。
- 不新建平行 QueryPlanBuilder / Interface。
- 不复制 Metadata Semantic Search Service。
- 不让 SQL Builder 调用 Resolver。
- 不让 Builder 自行推理 Relation。
- 不把 QueryJoinCandidate 直接当 Executable Join。
- 不把 DirectKey 转成 QueryJoin。

## 六、D14 兼容性 Gate

| Case | D14 强制要求 |
|---|---|
| GQ-001 | 原 PASS 必须保持 |
| GQ-002 | EntityCount Contract 不变 |
| GQ-003/005/009 | Metric / Filter 不漂移 |
| GQ-006 | 当前无 Master + 稳定 DirectKey → 可执行；无证据 → BLOCK |
| GQ-010 | DirectKey 不破坏 Ranking / Order / Limit |
| **GQ-011** | **继续 PASS，完全绕过 Dimension Resolution** |
| MasterJoin | 仅由 Resolution 产生 QueryPlan.Joins |
| DirectKey | 不产生 Joins |
| Ambiguous | BLOCK，不猜测 |
| NotResolved | BLOCK，不猜测 |
| Future Snapshot | 新增主表 / Relation 后允许 DirectKey → MasterJoin 重新解析 |

任一既有 PASS Case 出现回归，立即停止 D14 当前实现验证，先定位并修复兼容性问题，再继续。

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
D13 FROZEN
 ↓
阶段计划已同步
 ↓
主计划已同步
 ↓
确认 master
 ↓
D14 — 首轮源码实现 / Controller / Runtime
```

D14 不得自行扩大 D09-D13 已冻结的源码修改范围。
