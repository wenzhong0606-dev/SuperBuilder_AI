# SuperBuilder AI Native BI Phase开发计划

> 文档版本：v2.19  
> 文档性质：项目正式开发基线 + Phase 开发测试管理总计划  
> **唯一源码基线：GitHub `master`**

## 一、当前项目状态

- 当前 Phase：**Phase 2.7 — DimensionAware QueryPlan**
- Phase 状态：**IN_PROGRESS**
- 当前工作单元：**D14 — 首轮源码实现 / Controller / Runtime**
- 当前状态：**D14 实现进行中，D14-1～D14-5 已完成首轮源码落地，但尚未完成 Runtime / Regression / Exit Gate**
- D05-D12：全部 FROZEN
- D13：Build PASS + Startup PASS，已 FROZEN
- 当前 master：`8d8a4850f0f0b20440084e968cffa5c13b029085`

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
D14 首轮源码实现 / Runtime           IN_PROGRESS
```

## 三、D13 冻结结论

用户已确认当前 `master` 对应本地代码完成同步后：

- 编译通过；
- 应用正常启动。

因此 D13 的目标——确认首轮实现前当前 master 具备稳定的 Build / Startup 基线——已通过并冻结。

**D13 = FROZEN。**

本 STEP 未修改业务源码。D13 冻结只代表 Build / Startup 基线通过，不代表 D07-D12 的功能 Contract 已实现。

## 四、D14 首轮源码实现边界

D14 是 Phase 2.7 首次进入业务源码实现的 STEP。实现必须严格消费 D09-D12 已冻结的范围；若发现冻结 Contract 与当前产品多数据库运行要求存在冲突，必须先记录并更新计划，再实施最小必要 Contract 修正，不得通过业务硬编码绕过。

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

首轮实现目标：建立唯一 Resolution 真相源，让 Dimension 能在**当前 Metadata Snapshot**下选择：

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
- 不硬编码物料、供应商、客户等业务主表、字段或固定关系。

## 五、D14-1 ～ D14-5 当前实现进度

### D14-1 — Dimension Resolution Evidence Contract / Provider
已完成首轮落地：建立 `DimensionResolutionEvidence` 及对应 BI Interface / Service，明确 `MasterJoin / DirectKey / Ambiguous / NotResolved` 四态 Resolution 与 ExecutionCapability 分离表达。

### D14-2 — Semantic Applicability 接入 Evidence
已完成首轮落地：`SemanticApplicabilityEvaluator` 已接入 `IDimensionResolutionEvidenceService`，Dimension Resolution 开始消费当前 Metadata Snapshot 的物理 Evidence，而不是仅依赖普通 Semantic Candidate。

### D14-3 — DI / Namespace / Contract 对齐
已完成首轮落地：Dimension Evidence Service 已注册到 DI，并统一到 `Interfaces.BI` Contract，清理重复旧接口路径。

### D14-4 — Resolution → QueryPlan Contract 传递
已完成首轮落地：`MasterJoin / DirectKey` Resolution 及 Master Binding 已传递至 QueryPlan Semantic Resolution / Dimension Binding。

### D14-5 — QueryPlan Dimension 执行绑定
已完成首轮落地：QueryPlan Builder 已能够消费已确认 Dimension Resolution；MasterJoin 生成 `QueryPlan.Joins`，DirectKey 不生成 Join，并保留既有 Ranking / Order Contract 边界。

**D14-1～D14-5：源码首轮实现完成；功能验收尚未完成。**

## 六、D14 多数据库 Metadata 规则修正（必须遵守）

> **“当前 Metadata Snapshot 不存在主表/字段”不是代码错误，而是当前用户尚未绑定包含该 Metadata 的数据库。**

Dimension Relation 必须按以下规则运行：

```text
用户只绑定数据库 A
        ↓
当前 Metadata Snapshot 只有 A
        ↓
A 中存在 Fact Dimension Key / Code / Label
        ↓
未发现稳定 Master Metadata
        ↓
DirectKey（若 Fact Evidence 稳定）
        ↓
不强行 JOIN

用户随后绑定数据库 B
        ↓
重新扫描 B Metadata
        ↓
当前 Snapshot 同时存在 A + B
        ↓
发现真实稳定 Relation Evidence
        ↓
MasterJoin
        ↓
允许 QueryPlan.Joins
```

### 强制规则

1. **Metadata 中不存在关联主表/字段：不 Join。**
2. **Metadata 中存在稳定关联主表/字段：允许 Join。**
3. 不允许因为“物料”“供应商”等词语而硬编码主表或字段。
4. 不允许把某次 `NotResolved` / `DirectKey` 固化成永久事实。
5. 新 DataSource 加入后必须重新生成 Metadata Snapshot，并重新 Resolution。
6. 新 Snapshot 出现稳定 Master Relation 时，允许 `DirectKey → MasterJoin`。
7. Relation Evidence 消失后，必须重新 Resolution 并安全降级。
8. 所有 Dimension / Master / Relation GQ-xx 均遵循同一规则。
9. 多数据库是**用户已绑定 DataSource 的 Metadata 联合可见范围**，不是预先硬编码的跨库业务关系。
10. 未绑定的数据库不属于当前 Snapshot，因此不能作为 Join 目标。

## 七、D14 当前源码审计发现的关键剩余问题

### 1. Evidence Service 当前仍把跨 DataSource Master 标记为不可执行
当前 `DimensionResolutionEvidenceService` 已能发现不同 `DataSourceId` 的 Master Candidate，但仍以 `sameDataSource ? Executable : NotExecutable` 限制 MasterJoin。

这与产品多数据库规则不一致：如果 A、B 均已由用户绑定并共同进入当前 Metadata Snapshot，稳定 Relation 存在时不应因为 `DataSourceId` 不同而自动降级为不可执行。

### 2. QueryPlan Builder 当前仍明确禁止跨 DataSource MasterJoin
当前 `QueryPlanBuilder.SemanticResolution` 仍存在 `MasterDataSourceId != FactDataSourceId → throw`，会阻断“数据库 A 中事实表 + 数据库 B 中主表”的合法场景。

### 3. SQL Builder 当前尚未消费 QueryPlan.Joins
当前 `SqlQueryBuilder` 仍在 `Tables.Count > 1` 时直接失败；D11 已冻结的 JOIN SQL 消费路径尚未实现。

### 4. Semantic Applicability 仍存在 Dimension 二次 Semantic Search
`ResolveDimensionAsync` 在 Evidence 已确认后再次执行 Dimension Semantic Search，用于补充 `BusinessMeaning`。这属于 D14 当前剩余的职责漂移：QueryPlan 应消费已确认 Resolution，不能重新寻找物理绑定。后续应移除该二次 Search，并直接消费 Evidence 中已经确认的物理绑定/语义证据。

## 八、D14 下一轮最小修改范围

下一轮不是重新设计 D14-1～D14-5，而是补齐已审计出的执行闭环：

```text
D14-1～D14-5 已落地
        ↓
① 当前 Snapshot DataSource 边界修正
        ↓
② 允许“已绑定 A + 已绑定 B + 稳定 Relation”进入 MasterJoin
        ↓
③ QueryPlanBuilder 删除跨 DataSource 的错误硬阻断
        ↓
④ Semantic Applicability 删除 Dimension 二次 Search
        ↓
⑤ SqlQueryBuilder 消费已确认 QueryPlan.Joins
        ↓
⑥ Controller / Runtime
        ↓
⑦ Build
        ↓
⑧ Golden / Regression
```

**这里允许的是当前用户已绑定 DataSource 范围内的动态 Relation；不是允许任意跨库猜测，也不是允许 Tenant Federation。**

## 九、D14 禁止修改范围

- 不修改 Golden Dataset 以掩盖实现问题。
- 不修改 GQ-011 Ranking Contract。
- 不修改 Evaluator / Coverage / Gate 来规避实现问题。
- 不硬编码物料、供应商、客户主表或字段。
- 不根据表名、字段名后缀自行猜 JOIN。
- 不把未绑定数据库的 Metadata 当作当前可用 Relation。
- 不新建独立 Test Project。
- 不新建平行 QueryPlanBuilder / Interface。
- 不复制 Metadata Semantic Search Service。
- 不让 SQL Builder 调用 Resolver / Semantic Search。
- 不把 QueryJoinCandidate 直接当 Executable Join。
- 不把 DirectKey 转成 QueryJoin。
- 不跨 Tenant JOIN。

## 十、D14 兼容性 Gate

| Case | D14 强制要求 |
|---|---|
| GQ-001 | 原 PASS 必须保持 |
| GQ-002 | EntityCount Contract 不变 |
| GQ-003/005/009 | Metric / Filter 不漂移 |
| GQ-006 | 当前无 Master + 稳定 DirectKey → 可执行；无证据 → BLOCK；已绑定新 DataSource 后发现稳定 Master → MasterJoin |
| GQ-010 | DirectKey 不破坏 Ranking / Order / Limit；MasterJoin 只增加已确认 Join |
| **GQ-011** | **继续 PASS，完全绕过 Dimension Resolution** |
| MasterJoin | 仅由当前 Snapshot 已确认 Resolution 产生 QueryPlan.Joins |
| DirectKey | 不产生 Joins |
| Ambiguous | BLOCK，不猜测 |
| NotResolved | BLOCK，不猜测 |
| Snapshot Refresh | 新增/删除 DataSource 后重新 Resolution，不复用旧结论 |

任一既有 PASS Case 出现回归，立即停止当前 D14 验证，先定位并修复兼容性问题。

## 十一、项目强制开发规则

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

## 十二、长期产品目标

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

## 十三、下一步

```text
D14-1～D14-5 首轮实现已完成
        ↓
计划已同步 master
        ↓
修正多数据库 Snapshot / MasterJoin 执行边界
        ↓
移除 Dimension 二次 Semantic Search
        ↓
补齐 SqlQueryBuilder QueryPlan.Joins 消费
        ↓
Build
        ↓
Controller / Action Runtime
        ↓
GQ-006 / GQ-010 / MasterJoin / DirectKey / Negative Regression
        ↓
D14 冻结
        ↓
D15
```

**D14 当前不得宣布完成。源码首轮实现已落地，但 Runtime / Regression / Exit Criteria 尚未完成。**
