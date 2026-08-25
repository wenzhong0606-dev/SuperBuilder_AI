# Phase 2.7 — DimensionAware QueryPlan 开发测试计划

## STEP-01 / STEP-02 / D05 / D06 / D07 审计结论与最终修改范围（2026-08-25）

### 一、D07 最终源码 / Contract 审计结论：FROZEN

D07 — Dynamic Dimension Resolution 全量源码 / Contract 审计已完成。审计覆盖 Golden Dimension Contract、SemanticApplicabilityEvaluator、MetadataSemanticSearchService、MetadataScannerService、MetadataTable / MetadataColumn / DataSource、QueryIntent、QueryDimension、QueryPlanSemanticResolution、QueryPlanSemanticResolutionFactory、QueryPlanBuilder、QueryJoinInferenceService、QueryJoin / QueryJoinCandidate、QueryPlanDimensionScoringService、SqlQueryBuilder、EvaluationDiagnosticsController、DI 以及 Golden Dataset。

当前 GQ-011 已 PASS；GQ-006、GQ-010 当前在 SemanticApplicabilityGate 因“物料”无法建立稳定 Dimension 物理绑定而 BLOCK。根因已确认不是 Ranking，而是当前 Dimension Resolution 仍停留在“语义 → 单物理 Column”模型，没有接入 D05 EntityKey + D06 Relation + MasterJoin / DirectKey 双路径。

### 二、D07 根因链

```text
Golden Dimension Semantic
        ↓
SemanticApplicabilityEvaluator.ResolveDimensionAsync
        ↓
MetadataSemanticSearchService.SearchAsync
        ↓
Vector Candidate（Table + Column）
        ↓
ContainsSemanticText / EntityEvidence
        ↓
没有 EntityKey / Master / Relation / DirectKey Resolution
        ↓
DimensionResolutions = []
        ↓
SemanticApplicabilityGate = BLOCK
```

当前 `SemanticApplicabilityResult` 的 Dimension Resolution 只有 TableId/DataSourceId/ColumnId/SemanticText 等简单绑定，不能表达 MasterJoin、DirectKey、ResolutionState、ExecutionCapability。`QueryDimension` 与 `QueryPlanSemanticResolution` 同样只支持单物理 Column。fileciteturn310file0turn315file0turn312file0

当前 master 中未检索到实际 `DimensionEntityKeyResolution` / `DimensionEntityKeyResolver` 源码实体；因此 D05 的设计 Contract 必须与实际代码完成度严格区分，后续实现不得把计划中的实体当作已经存在。MetadataTable / MetadataColumn / DataSource 已具备 Tenant、DataSource、字段、PrimaryKey、BusinessKey、Semantic、SearchText、Vector 等基础信息。fileciteturn321file0turn320file0turn323file0

`MetadataScannerService` 已具备 DataSource → 数据库结构 → Metadata → SearchText → Batch Semantic → Vector 基础链路，但尚未形成完整 Snapshot Reconciliation / Stale Invalidation / Semantic Refresh / Vector Refresh Contract。fileciteturn324file0

`MetadataSemanticSearchService` 负责 table / column / semantic Vector 检索和 Metadata 回载，不负责 EntityKey / Relation / Execution Capability。fileciteturn341file0

### 三、D07 最终修改范围：FROZEN

#### 必须修改

1. **Dimension Entity Key Resolution Contract / Model**：补齐 D05 设计 Contract 对应的实际代码实体，表达 EntityKey、Label、FactTable、Candidate、Evidence、ResolutionState。
2. **Dynamic Dimension / EntityKey Resolver**：基于当前有效 Metadata Snapshot 产生稳定 Dimension Resolution，不硬编码 material / supplier / customer 主表。
3. **Dimension Resolution Contract**：支持 `MasterJoin` / `DirectKey` / `Ambiguous` / `NotResolved`，并分离 `Executable` / `NotExecutable`。
4. **SemanticApplicabilityEvaluator Dimension 接入**：Dimension 不再以“语义 → 单 Column”作为最终 Gate，改为调用 Resolver；Semantic Search 只提供候选证据。
5. **Metadata Resolution Provider / Snapshot 边界**：必须考虑 Tenant、Enabled DataSource、当前有效 Metadata、Table / Column / PrimaryKey / BusinessKey / Semantic 证据。
6. **Interface / DI / Constructor 接入**：Resolver 通过 Interface + DI 注入，不允许 Controller / Evaluator 内部 `new`。

#### D07 明确不修改

- QueryPlanEvaluator；
- QueryPlanDimensionScoringService 的评分规则；
- Ranking / DetailRanking / AggregateRanking；
- GQ-011 Golden Contract；
- Metric / Filter Semantic Resolution；
- QueryJoinInferenceService 的核心评分算法；
- SqlQueryBuilder JOIN 落地（D11）；
- QueryPlan.Joins 最终装配（D08 / D09）；
- 跨独立 DataSource Federation；
- material_master / supplier_master 等业务硬编码；
- 为通过 Gate 修改正向 Golden。

### 四、D07 与 D08 边界

```text
D07
Dimension Semantic
   ↓
EntityKey / Master / DirectKey Resolution
   ↓
冻结 Resolution Contract

D08
Frozen Dimension Resolution
   ↓
QueryPlan Dimension Binding
   ↓
QueryPlan.Joins / DirectKey Plan Binding
```

D07 不提前把 QueryPlanBuilder 改成第二套 Resolver。

### 五、D07 兼容性影响矩阵：FROZEN

| Case | 当前 | D07 后预期 | 影响 | 必须保证 |
|---|---|---|---|---|
| GQ-001 | PASS | PASS | 无 Dimension | 原路径不变 |
| GQ-002 | 正常 EntityCount | 保持 | 无 Dimension | 不受影响 |
| GQ-003 | BLOCK | DirectKey / MasterJoin 判定 | 预期能力提升 | Metric 不漂移 |
| GQ-004 | 正常 | 保持 | 无 Dimension | Filter 不漂移 |
| GQ-005 | BLOCK | MasterJoin / DirectKey 判定 | 预期能力提升 | 证据不足仍 BLOCK |
| GQ-006 | BLOCK | DirectKey 优先；有稳定同 Context Master 才 MasterJoin | 预期能力提升 | 不硬编码物料主表 |
| GQ-007 | 正常 | 保持 | 无 Dimension | Metrics 不漂移 |
| GQ-008 | 正常 | 保持 | 无 Dimension | EntityCount 不漂移 |
| GQ-009 | BLOCK | MasterJoin / DirectKey 判定 | 预期能力提升 | Date Filter 不漂移 |
| GQ-010 | BLOCK | DirectKey / MasterJoin 判定 | 预期能力提升 | Ranking / Order 不漂移 |
| GQ-011 | PASS | **PASS** | 无 Dimension | **必须完全不进入 Dynamic Dimension Resolution** |
| GQ-N001 | Negative | Negative | 无 Dimension | Aggregation Contract 不变 |
| GQ-N002 | Negative | Negative | 无 Dimension | Filter Contract 不变 |
| GQ-N003 | Negative | Negative / BLOCK | Dimension + Wrong Join | 不得放宽 Relation 证据 |
| GQ-N004 | Negative | Negative / BLOCK | Dimension + Wrong Order | Ranking Direction 不变 |
| GQ-N005 | Negative | Negative / BLOCK | Dimension + Wrong Limit | Limit 不变 |
| GQ-A001 | Ambiguous | Ambiguous / BLOCK | 无 Dimension | Metric Ambiguity 不变 |
| GQ-U001 | BLOCK | BLOCK | 无 Dimension | 不得被绕过 |

### 六、D07 安全边界

1. 无稳定 EntityKey Evidence → `NotResolved → BLOCK`。
2. 只有 Vector / Semantic 高分，没有物理 Key / Relation Evidence → 不得 MasterJoin。
3. 多候选无法唯一稳定绑定 → `Ambiguous → BLOCK`。
4. 有 Master 但跨独立 DataSource → Relation 可记录，但当前 `ExecutionCapability=NotExecutable`；本 Phase 不生成普通 SQL JOIN。
5. 无 Master 但事实表存在稳定 Dimension Key / Label → `DirectKey`。
6. 新增 DataSource / Metadata 后必须重新 Resolution，不能永久缓存旧 MasterJoin。
7. Disabled DataSource / Stale Metadata 不得作为有效 Candidate。
8. GQ-011 不进入 Dynamic Dimension Resolution。
9. 任何既有 PASS Case 因 D07 出现行为漂移，立即停止当前实现验证并先修兼容性问题。

### 七、D07 冻结与文档 Gate

**D07：FROZEN。** 冻结的是源码审计结论、Contract 边界、最终修改范围、禁止修改范围和兼容性要求，不代表功能已经开发完成。

强制流程：

```text
D07 FROZEN
   ↓
更新 Phase 2.7 开发测试计划
   ↓
同步主开发计划
   ↓
确认 GitHub master 文档一致
   ↓
进入 D08
```

### 八、D08 入口

**D08 — QueryPlan Dimension Binding 全量源码 / Contract 审计。**

D08 正式输入：D05 EntityKey Contract + D06 Dynamic Relation Contract + D07 Dimension Resolution Contract。D08 继续执行“完整审计 → 一次性最终修改范围 → 冻结 → 更新阶段计划 → 同步主计划 → 确认 master → 下一 STEP”，禁止边审边临时修改 Factory / Builder / Resolver。

---

## 原 Phase 2.7 开发计划

Phase 2.7 继续按 D01～D21 / STEP-01～STEP-19 顺序执行；D07 冻结结果作为 D08 QueryPlan Binding、D09/D10 双路径、D11 SQL Builder 的正式输入。
