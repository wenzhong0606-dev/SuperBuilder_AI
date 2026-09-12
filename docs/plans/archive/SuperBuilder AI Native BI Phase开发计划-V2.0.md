> **HISTORICAL SNAPSHOT / 历史快照**：本文是归档资料，只描述记录当时的计划、状态或审计判断。文中的“当前、唯一、已完成、未完成、风险、测试基线、HEAD”等均不得解释为现在的项目状态。当前事实请按 `docs/README.md` 的治理顺序核验。\n\n# SuperBuilder AI Native BI Phase开发计划

> 文档版本：v2.24  
> 文档性质：项目正式开发基线 + Phase 开发测试管理总计划  
> **唯一源码基线：GitHub `master`**

## 一、当前项目状态

- 当前 Phase：**Phase 2.7 — DimensionAware QueryPlan**
- Phase 状态：**CLOSED**
- 当前工作单元：**D21 — Phase 2.7 Exit Review**
- 当前状态：**D14-D20 已实际完成并冻结；D21 Exit Review 已通过；Phase 2.7 正式关闭。**
- 当前 master 基线：`f96c2945`；该基线在文档收口后已由用户本地确认 Build PASS、Startup PASS。
- Golden Dataset：`query-plan-golden` v1.3，18/18 Expected Outcome PASS。

### 当前 Runtime / Release 证据

```text
Build                  PASS
Startup                PASS
Golden Dataset Loaded  PASS (18/18)
All Cases Executed     PASS (18/18)
Expected Outcome       18/18 PASS
Positive               11/11 PASS
Negative                5/5 PASS
Ambiguous               1/1 PASS
Unresolved              1/1 PASS
Overall Pass Rate      100%
Positive Pass Rate     100%
Release Gate           PASS
failedGates            []
D14                     FROZEN
D15-D18                 FROZEN
D19-D20                 FROZEN
D21 Exit Review         FROZEN
Phase 2.7               CLOSED
Phase 3                 READY TO START
```

> 历史记录中的 `master=252cf2b`、`master=6a5f7d6` 以及 D15-D18“尚未提交 master”等均属于状态同步前的历史基线，不再作为当前基线。

## 二、Phase 2.7 最终冻结链

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
D14 Runtime Contract Verification   FROZEN
        ↓
D15 MasterJoin Golden               FROZEN
        ↓
D16 DirectKey Golden                FROZEN
        ↓
D17 SameTable / CrossTable Golden   FROZEN
        ↓
D18 Ambiguous / NotResolved Safety  FROZEN
        ↓
D19 Full Golden Regression          FROZEN
        ↓
D20 Coverage / Quality / Release    FROZEN
        ↓
D21 Phase 2.7 Exit Review           FROZEN
        ↓
Phase 2.7                           CLOSED
```

## 三、D21 Exit Review 最终结论

D21 已完成三方状态收口：

1. 当前 master 为 `f96c2945`，包含本轮状态同步文档；
2. D15-D18 修复链已经进入此前的 master 基线；
3. 用户已确认当前最新 master 编译通过；
4. 用户已确认当前最新 master 正常启动；
5. Golden Dataset v1.3 的 18 Case Runtime / Regression 证据为 18/18 PASS；
6. Positive 11/11、Negative 5/5、Ambiguous 1/1、Unresolved 1/1 全部通过；
7. Release Gate PASS，`failedGates=[]`；
8. 本次状态收口不修改 QueryPlan / Evaluator / Golden Expected Outcome / SQL Builder 业务逻辑。

**D21 Exit Review = FROZEN。**

**Phase 2.7 = CLOSED。**

## 四、三方同步基线

```text
Phase Plan
    │
    ├── D14-D20 FROZEN
    └── D21 FROZEN
             │
             ▼
Runtime
    │
    ├── Build PASS
    ├── Startup PASS
    ├── Golden 18/18 PASS
    └── failedGates=[]
             │
             ▼
master
    │
    └── f96c2945
             │
             ▼
Phase 2.7 CLOSED
             │
             ▼
Phase 3 READY TO START
```

## 五、Phase 3 入口

Phase 3 现在具备正式启动条件。后续业务开发必须从新的 Phase / STEP 开始，不再修改已冻结的 Phase 2.7 Contract。

建议 Phase 3 第一工作单元继续保持独立的 Contract → Source → Runtime → Freeze 闭环。

```text
Phase 2.7 CLOSED
        ↓
Phase 3 START
        ↓
Phase 3.1 Business Entity Model
```

## 六、变更边界

本次 D21 收口只同步 Phase 状态与验证证据，不修改 QueryPlan、Evaluator、Golden Expected Outcome、SQL Builder 或其他 Phase 2.7 业务逻辑。