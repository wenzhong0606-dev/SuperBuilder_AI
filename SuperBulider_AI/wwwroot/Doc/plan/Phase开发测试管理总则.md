# SuperBuilder AI Native BI Phase 开发测试管理总则

> 文档性质：项目长期正式管理基线
> 唯一源码基线：GitHub `master`
> 适用范围：Phase 2.1 及后续全部阶段

## 1. 总原则

每进入一个新的 Phase，第一项工作不是修改源码，而是对当前 `master` 做一次**全量源码与文档审计**。

阶段必须按照以下顺序进入：

```text
上一阶段 Exit Criteria
        ↓
全量审计当前 master
        ↓
确认真实完成度 / 未完成项 / 遗留风险
        ↓
确定本阶段全部开发任务
        ↓
建立阶段开发计划
        ↓
建立阶段 Runtime 分步测试记录
        ↓
关联主开发计划
        ↓
开始本阶段 STEP-01
```

不得采用“先开发、边开发边补计划、最后补测试文档”的方式。

---

## 2. Phase 入口全量审计规则

每个新 Phase 必须先审计：

1. 当前全部核心源码；
2. Models / Interfaces / Services / Infrastructure / DI；
3. Controller / Action / Runtime 调用链；
4. 当前 Golden Dataset / Evaluation；
5. 当前测试 Controller / Action；
6. 当前 Build / CI 状态；
7. 当前主开发计划；
8. 上一 Phase 开发计划与 Runtime 测试记录；
9. 已提交 Commit 与实际实现状态；
10. 当前已知 FAIL / BLOCK / REVIEW / 未验证项。

审计结论必须区分：

```text
IMPLEMENTED
VERIFIED
IMPLEMENTED_BUT_UNVERIFIED
PARTIAL
MISSING
BLOCKED
REGRESSION
```

不得根据开发计划中的文字描述直接认定源码已经完成。

---

## 3. 阶段开发计划规则

完成 Phase 入口审计后，必须确定该阶段的**全部开发任务**，形成唯一阶段开发计划文档：

```text
PhaseX.Y-<主题>-开发测试计划.md
```

阶段开发计划必须至少包含：

1. 阶段目标；
2. 入口审计结论；
3. 与上一 Phase 的边界；
4. 本阶段 Scope；
5. 明确不属于本阶段的 Scope；
6. 核心 Contract；
7. 数据模型 / 接口变化；
8. 完整源码任务清单；
9. 完整 Runtime 测试任务清单；
10. Golden / Regression 测试清单；
11. 每个任务的前置条件；
12. 每个任务的开发完成条件；
13. 每个任务的测试完成条件；
14. 每个任务对应源码文件 / 模块；
15. 每个任务对应测试步骤；
16. Exit Criteria；
17. Release Gate；
18. 下一 Phase 入口条件。

阶段开发计划一经开始执行，不得遗漏已经确定的任务。若发现新增任务，必须先更新阶段开发计划，再开始开发。

---

## 4. 主开发计划与阶段开发计划的职责必须分离

### 主开发计划

主开发计划是**项目当前进度总览**，只记录：

- 当前 Phase；
- 当前 C 项 / 工作单元；
- 当前 STEP；
- 当前状态；
- 当前 Commit；
- 当前阻塞 / 风险；
- 下一步动作；
- 已完成 Phase 的最终状态。

主开发计划**不重复记录完整技术方案、完整测试步骤或历史过程**。

它的职责是回答：

> “项目现在做到哪里？”

### 阶段开发计划

阶段开发计划是该 Phase 的完整执行契约，记录：

- 为什么做；
- 做什么；
- 全部开发任务；
- 全部测试任务；
- 每一步如何验收；
- Exit Criteria。

它的职责是回答：

> “这个 Phase 完整要做什么、怎么做、怎么证明完成？”

---

## 5. 文档关联规则

主开发计划必须关联当前 Phase 的阶段开发计划与 Runtime 测试记录。

关系固定为：

```text
主开发计划
    │
    ├── PhaseX.Y 开发测试计划
    │       │
    │       ├── STEP-01 ... STEP-N
    │       │
    │       └── Exit Criteria
    │
    └── PhaseX.Y Runtime 分步测试记录
            │
            └── 原始 Runtime JSON / 判定
```

阶段开发计划必须反向注明主开发计划位置。

Runtime 测试记录必须注明对应阶段开发计划和当前 STEP。

---

## 6. STEP 执行与同步规则

阶段中的每一个 STEP 都必须经历：

```text
STEP 开始
   ↓
更新主开发计划：当前 STEP = 该步骤，状态 = IN_PROGRESS
   ↓
更新阶段开发计划：该步骤 = IN_PROGRESS
   ↓
源码开发 / 配置 / 测试
   ↓
Build
   ↓
Runtime / Golden 验证
   ↓
记录完整原始结果
   ↓
判定 PASS / FAIL / BLOCK / REVIEW
   ↓
更新阶段开发计划
   ↓
更新主开发计划
   ↓
Commit
   ↓
进入下一 STEP
```

因此，**每个步骤开始和完成都必须同步更新两份计划**。

不得只更新阶段文档而不更新主计划，也不得只更新主计划而不更新阶段文档。

---

## 7. Runtime 分步测试规则

每个 Phase 必须建立：

```text
PhaseX.Y-Runtime分步测试记录.md
```

格式参考：

`Phase2.6-Runtime分步测试记录.md`

每个 STEP 原则上只要求用户执行一个明确的 Runtime 动作 / 地址 / 输入，记录：

- STEP 编号；
- 目的；
- 前置条件；
- 调用地址 / Controller / Action；
- 参数；
- 预期结果；
- 完整原始返回 JSON；
- 实际结果；
- PASS / FAIL / BLOCK / REVIEW；
- 对应 Commit；
- 下一步。

不得要求用户一次执行一批互相依赖的 Runtime 步骤，以避免无法定位失败原因。

---

## 8. 失败与阻塞规则

如果一个 STEP 为 FAIL / BLOCK：

```text
停止当前 Phase 后续依赖步骤
↓
源码根因审计
↓
明确修复范围
↓
更新阶段开发计划
↓
修改源码
↓
Build
↓
重新执行当前 STEP
```

不得跳过失败步骤进入后续步骤。

不得通过修改 Golden Expected、删除失败 Case、降低 Gate、硬编码业务答案来制造 PASS。

---

## 9. Golden / Regression 规则

Golden Dataset 是测试契约，不是为了让测试通过而修改的目标。

修改 Golden 必须有明确的业务 / Metadata 事实依据，并同时记录：

- 修改原因；
- 原契约；
- 新契约；
- 影响 Case；
- 对应阶段计划；
- 回归结果。

Golden Regression 必须区分：

```text
语义解析正确性
QueryPlan 正确性
SQL 生成正确性
SQL Runtime 正确性
安全 Gate 正确性
```

不能只以 Passed 数量作为 Phase 完成依据。

---

## 10. COMPLETE 规则

Phase 只有满足以下全部条件才能标记 COMPLETE：

```text
入口全量审计完成
AND
全部计划任务完成
AND
全部关键源码已实现
AND
Build PASS
AND
Controller / Action Runtime PASS
AND
Golden / Regression PASS（适用时）
AND
安全 Gate 回归 PASS
AND
Coverage / Quality Gate PASS（适用时）
AND
Release Gate PASS
AND
阶段开发计划已闭环
AND
Runtime 测试记录已闭环
AND
主开发计划已同步
```

任何关键项缺少证据时只能标记：

`IMPLEMENTED_BUT_UNVERIFIED`

不能标记 COMPLETE。

---

## 11. 阶段切换规则

进入下一 Phase 前必须完成：

```text
当前 Phase Exit Review
↓
确认所有任务状态
↓
确认所有 Runtime 记录
↓
确认 Golden / Regression
↓
确认 Release Gate
↓
更新主开发计划为上一 Phase COMPLETE
↓
新 Phase 入口全量审计
↓
建立新 Phase 全部开发 / 测试计划
```

不得在上一 Phase 尚未完成关键 Exit Criteria 时直接宣称下一 Phase COMPLETE。

允许提前创建下一 Phase 的“入口规划 / 待办”，但状态必须明确为 `PLANNED`，不得伪装成已进入执行阶段。

---

## 12. 防止重复工作的恢复规则

任何会话中断、人员切换或重新进入项目时，优先读取：

1. 主开发计划；
2. 当前 Phase 开发测试计划；
3. 当前 Phase Runtime 分步测试记录；
4. 最近相关源码 Commit；
5. 当前 FAIL / BLOCK / REVIEW 记录。

恢复后直接从主计划记录的当前 STEP 继续。

已经有 PASS 证据的步骤不得无理由重复执行；只有当源码、配置、数据、环境或依赖发生影响性变化时才重新验证。

---

## 13. 文档更新时机

以下任何事件发生后，必须同步更新相关文档：

- Phase 开始；
- Phase 入口审计完成；
- 新增开发任务；
- STEP 开始；
- STEP 完成；
- STEP FAIL / BLOCK / REVIEW；
- Contract 变化；
- Golden 变化；
- Runtime 结果变化；
- Commit 完成；
- Phase Exit；
- Phase 切换。

---

## 14. Git Commit 规则

正式源码、阶段计划、Runtime 记录和主计划均直接提交 `master`。

Commit 描述统一使用中文，并包含：

```text
Phase + C项/STEP + 动作 + 结果
```

例如：

```text
完成 Phase 2.7 STEP-02 Dimension Entity Key 审计与开发测试计划更新
```

---

## 15. 本项目当前执行规则

按照本总则，当前从 Phase 2.6 向 Phase 2.7 的正确动作不是立即修改源码，而是：

```text
Phase 2.6 Exit 状态校准
        ↓
Phase 2.7 入口全量审计 master
        ↓
确认全部 Dimension / QueryPlan / Join / SQL 任务
        ↓
建立并冻结 Phase 2.7 完整开发测试计划
        ↓
建立 Phase 2.7 Runtime 分步测试记录
        ↓
主开发计划仅更新当前进度
        ↓
开始 STEP-01
```

该规则优先于任何单次会话中的临时操作。
