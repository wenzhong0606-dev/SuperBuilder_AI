# SuperBuilder AI Native BI Phase 开发测试管理总则

> 文档性质：项目长期正式管理基线
> 适用范围：Phase 2.1 及后续全部阶段
> 唯一源码基线：GitHub `master`

## 1. 核心原则

每进入一个新的 Phase，第一项工作不是修改源码，而是对当前 `master` 做一次全量源码、运行链路、测试与文档审计。

固定流程：

```text
上一 Phase Exit Review
↓
全量审计当前 master
↓
确认真实完成度 / 未完成项 / 遗留风险
↓
确定本 Phase 全部开发任务与测试任务
↓
建立并冻结 Phase 开发测试计划
↓
建立 Runtime 分步测试记录
↓
关联主开发计划
↓
开始 STEP-01
```

禁止先开发、边开发边补计划、最后补测试文档。

## 2. Phase 入口全量审计

每个新 Phase 必须审计：

1. Models / Interfaces / Services / Infrastructure / DI；
2. Controller / Action / Runtime 调用链；
3. QueryPlan / Semantic / Join / SQL Builder 相关链路；
4. Golden Dataset / Evaluation / Regression；
5. Build / CI / Coverage / Quality / Release Gate；
6. 当前主开发计划；
7. 上一 Phase 开发测试计划与 Runtime 测试记录；
8. 已提交 Commit 与实际实现状态；
9. 当前 FAIL / BLOCK / REVIEW / 未验证项。

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

不得仅依据设计文档、历史讨论或计划文字认定源码已完成。

## 3. Phase 开发测试计划

每个新 Phase 必须建立唯一阶段执行契约：

```text
PhaseX.Y-开发测试计划.md
```

必须包含：

1. 阶段目标；
2. 入口全量审计结论；
3. 上一 Phase 边界；
4. 本阶段 Scope / Non-Scope；
5. 核心 Contract；
6. 数据模型 / 接口变化；
7. 全部源码开发任务；
8. 全部 Runtime 测试任务；
9. Golden / Regression 任务；
10. 每个 STEP 的前置条件；
11. 每个 STEP 的开发完成条件；
12. 每个 STEP 的测试完成条件；
13. 对应源码文件 / 模块；
14. 对应 Controller / Action / Runtime；
15. Exit Criteria；
16. Quality / Release Gate；
17. 下一 Phase 入口条件。

阶段执行中发现新增任务时，必须先更新该阶段计划，再开始开发。

## 4. 主开发计划职责

主开发计划是项目当前进度总览，只回答：

> 项目现在做到哪里？

主计划仅记录：

- 当前 Phase；
- 当前 C 项 / 工作单元；
- 当前 STEP；
- 当前状态；
- 当前 Commit；
- 当前阻塞 / 风险；
- 下一步动作；
- 已完成 Phase 的最终状态。

主计划不重复保存完整技术方案、完整测试步骤和历史过程。完整内容必须保留在对应 Phase 文档。

## 5. 文档关联

固定关系：

```text
主开发计划
  ↓
PhaseX.Y-开发测试计划.md
  ↓
PhaseX.Y-Runtime分步测试记录.md
  ↓
源码 Commit
  ↓
Golden / Runtime 原始结果
```

Phase 开发测试计划必须反向注明主开发计划；Runtime 记录必须注明对应 Phase 计划与当前 STEP。

## 6. STEP 开始 / 完成同步规则

每一个 STEP 都必须同步更新主开发计划和阶段开发测试计划。

```text
STEP 开始
↓
主计划 = 当前 STEP / IN_PROGRESS
阶段计划 = 当前 STEP / IN_PROGRESS
↓
源码开发 / 配置 / Build / Runtime
↓
记录完整原始结果
↓
PASS / FAIL / BLOCK / REVIEW
↓
更新阶段计划
↓
更新主计划
↓
中文 Commit master
↓
进入下一 STEP
```

不得只更新其中一份文档。

## 7. Runtime 分步测试

每个 Phase 必须建立：

```text
PhaseX.Y-Runtime分步测试记录.md
```

格式统一参考 Phase 2.6 Runtime 记录。

每个 STEP 原则上只执行一个明确 Runtime 动作 / 地址 / 输入，并记录：

- STEP 编号；
- 目的；
- 前置条件；
- Controller / Action / 地址；
- 参数；
- 预期结果；
- 完整原始 JSON；
- 实际结果；
- PASS / FAIL / BLOCK / REVIEW；
- Commit；
- 下一步。

禁止一次要求执行多个互相依赖的 Runtime 地址。

## 8. FAIL / BLOCK 处理

```text
FAIL / BLOCK
↓
停止后续依赖 STEP
↓
源码根因审计
↓
更新 Phase 开发测试计划
↓
修改 master
↓
git pull
↓
dotnet build
↓
重新执行当前 STEP
```

不得跳步、降低 Gate、删除失败 Case 或硬编码业务答案制造 PASS。

## 8.1 全链路源码审计后再修改规则（新增正式规则）

所有涉及已有 Contract、QueryPlan、Semantic Resolution、Ranking、Evaluation、Runtime、Controller、DI 或跨文件调用链的修复，必须遵守：

```text
锁定 master
↓
完整读取相关调用链
↓
完整读取相关 Model / DTO / Contract
↓
完整读取 Producer / Mapper / Consumer
↓
完整读取 Controller / Action / Runtime 调用方
↓
完整读取 DI / Constructor Dependency
↓
完整读取 Golden / Evaluator / Regression 影响面
↓
形成字段级数据流与调用链
↓
闭合根因
↓
一次性冻结唯一修改范围
↓
才允许修改源码
```

禁止在完整审计完成前根据单个文件、单个类名或局部现象提前确定修改点。

禁止出现：

```text
边读源码
↓
先改 A
↓
发现不对
↓
改 B
↓
再改 C
```

如果完整审计后发现原先判断错误，必须明确废弃旧判断，重新基于 `master` 全链路源码形成唯一方案后再修改。

## 8.2 单 Case 修复的全 GQ 兼容性规则（新增正式规则）

任何针对单个 `GQ-***` Case 的修复，禁止只验证该 Case PASS 后即认为修复完成。

必须首先识别该 Contract 所覆盖的全部相关 Golden Case，并建立兼容性影响矩阵：

```text
目标 Case
↓
同 Contract / 同字段 / 同执行路径 Case
↓
已有正确 Positive Case
↓
Ranking / DetailRanking / AggregateRanking 相关 Case
↓
Negative / Ambiguous / Unresolved Safety Case
↓
全 Golden Regression
```

修复验收必须同时证明：

1. 目标 Case 达到预期 Contract；
2. 已经正确的 GQ-*** 不出现回归；
3. 同一 Contract 的其他 Positive Case 不被破坏；
4. Negative Case 仍保持预期的 FAIL / BLOCK / Safety 行为；
5. Ambiguous / NotResolved Case 不因修复被错误放行；
6. 不通过修改 Coverage Analyzer、Gate、Golden Expected 或删除 Case 制造 PASS。

对于 Ranking 修复，至少必须覆盖：

```text
GQ-006
GQ-010
GQ-011
GQ-N004
GQ-N005
```

以及当前 Golden Dataset 中所有新增或后续发现的 Ranking / DetailRanking / AggregateRanking Case。

## 8.3 兼容性风险必须先报告

如果源码审计无法证明某项修改对其他正确 GQ-*** 无副作用，或者发现存在以下情况之一：

- Contract 行为改变会影响多个既有 Case；
- 公共 Model / Interface / Factory 签名变化存在未审计调用方；
- Ranking 修复可能改变非 Ranking QueryPlan；
- Semantic Resolution 修改可能改变已有 Metric / Dimension / Filter Resolution；
- Controller / Runtime 与内部调用链存在不同 Contract；
- Golden Regression 结果出现无法解释的回归；

必须先将风险标记为 `COMPATIBILITY_RISK`，明确告知当前风险与受影响范围，**在风险未闭合前不得直接宣布修复完成**。

## 8.4 修复后的验证顺序

目标 Case 修复完成后，固定执行：

```text
源码重新读取
↓
Static Contract / Namespace / DI 检查
↓
Build
↓
目标 Case Runtime
↓
同 Contract Positive Regression
↓
Negative / Ambiguous / Unresolved Safety Regression
↓
Full Golden Regression
↓
Coverage
↓
Quality
↓
Release Gate
```

任何一层出现 FAIL / BLOCK / REVIEW，都必须停止后续依赖步骤并回到根因审计。

## 9. Golden / Regression 规则

Golden Dataset 是测试契约，不是为了通过测试而修改的目标。

任何 Golden 修改必须记录：

- 原契约；
- 新契约；
- 修改原因；
- Metadata / 业务事实依据；
- 影响 Case；
- 回归结果。

Regression 必须区分：

```text
Semantic Resolution
QueryPlan
SQL Generation
SQL Runtime
Safety Gate
```

不能只用 Pass 数量判定 Phase 完成。

## 10. Phase COMPLETE 规则

Phase 只有同时满足以下条件才能 COMPLETE：

```text
入口全量审计完成
AND 全部计划任务完成
AND 核心源码实现完成
AND Build PASS
AND Controller / Action Runtime PASS
AND Golden / Regression PASS（适用时）
AND Safety Gate PASS
AND Coverage / Quality PASS（适用时）
AND Release Gate PASS
AND Phase 开发测试计划闭环
AND Runtime 测试记录闭环
AND 主开发计划同步
```

缺少关键证据只能标记：

`IMPLEMENTED_BUT_UNVERIFIED`

不得标记 COMPLETE。

## 11. Phase 切换

进入下一 Phase 前：

```text
当前 Phase Exit Review
↓
确认所有任务状态
↓
确认 Runtime / Golden / Gate
↓
更新主计划为上一 Phase COMPLETE
↓
新 Phase 入口全量审计
↓
建立新 Phase 全部开发 / 测试计划
```

允许提前创建下一 Phase 的 PLANNED 文档，但未通过上一 Phase Exit 时不得宣称下一 Phase 已 COMPLETE。

## 12. 防止重复工作

会话中断或重新进入项目时，优先读取：

1. 主开发计划；
2. 当前 Phase 开发测试计划；
3. 当前 Phase Runtime 测试记录；
4. 最近源码 Commit；
5. 当前 FAIL / BLOCK / REVIEW。

直接从主计划记录的当前 STEP 恢复。

已经有有效 PASS 证据的 STEP 不得无理由重复执行；只有源码、配置、数据、环境或依赖发生影响性变化时才重新验证。

## 13. 文档更新时机

以下事件发生后必须同步相关文档：

- Phase 开始；
- 入口全量审计完成；
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

## 14. 命名规则

统一使用：

```text
PhaseX.Y-开发测试计划.md
PhaseX.Y-Runtime分步测试记录.md
PhaseX.Y-<C项>-修复进度记录.md
PhaseX.Y-<C项>-专项测试补充.md
```

禁止：

- 同一文档存在 V1 / V2 / V2.0 多份正式副本；
- 同一 Phase 同时存在多个“主开发计划”；
- 使用“最终版”“新版”“补充版”作为长期正式文档的唯一身份。

版本变化记录在文档头部和 Git Commit 中，不通过复制新文件制造版本。

## 15. Git 提交规则

正式源码、主计划、Phase 计划、Runtime 记录均直接提交 `master`。

Commit 使用中文，并包含：

```text
Phase + C项/STEP + 动作 + 结果
```

## 16. 当前项目恢复规则

Phase 2.6 → Phase 2.7 时必须执行：

```text
Phase 2.6 Exit 校准
↓
Phase 2.7 master 全量审计
↓
确定 Phase 2.7 全部任务
↓
冻结 Phase2.7-开发测试计划.md
↓
冻结 Phase2.7-Runtime分步测试记录.md
↓
主计划只记录当前进度
↓
STEP-01
```

本总则是 `wwwroot/Doc/plan` 内唯一的阶段管理规则文档。
