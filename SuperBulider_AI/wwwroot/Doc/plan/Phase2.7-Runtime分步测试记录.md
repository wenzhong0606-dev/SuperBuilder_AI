# Phase 2.7 Runtime 分步测试记录

> 所属开发阶段：Phase 2.7 — DimensionAware QueryPlan & SQL Closure
> 唯一源码基线：GitHub `master`
> 开发测试计划：`Phase2.7-DimensionAware QueryPlan开发测试计划.md`
> 管理规则：`Phase阶段开发与测试管理规则.md`
> 记录原则：完整测试步骤提前固化；一次只执行当前一个 STEP；用户返回完整原始 JSON 后判定；当前 STEP 未 PASS 不进入下一 STEP；每次结果及时更新并提交 master。

## 一、完整 Runtime 测试步骤

| STEP | 测试项 | 地址/输入 | 初始状态 |
|---|---|---|---|
| STEP-01 | Phase 2.7 当前源码 / Contract 基线 | 读取 GitHub master | PLANNED |
| STEP-02 | Dimension Metadata Entity Key 审计 | 使用现有 Metadata Runtime / 诊断 Action | PLANNED |
| STEP-03 | Master Table / Relation 审计 | 使用现有 Metadata Runtime / 诊断 Action | PLANNED |
| STEP-04 | Dimension Resolution Contract | `/evaluation/semantic-applicability/debug`，当前 Dimension Case | PLANNED |
| STEP-05 | MasterJoin Resolution | `/evaluation/diagnostics/golden-regression?topK=10`，重点 MasterJoin Case | PLANNED |
| STEP-06 | DirectKey Resolution | `/evaluation/diagnostics/golden-regression?topK=10`，重点 DirectKey Case | PLANNED |
| STEP-07 | SameTable Dimension QueryPlan | 使用现有 QueryPlan Runtime / Controller Action | PLANNED |
| STEP-08 | CrossTable Dimension QueryPlan | 使用现有 QueryPlan Runtime / Controller Action | PLANNED |
| STEP-09 | MasterJoin SQL Builder | 使用现有 SQL Builder / Controller Action | PLANNED |
| STEP-10 | DirectKey SQL Builder | 使用现有 SQL Builder / Controller Action | PLANNED |
| STEP-11 | MasterJoin SQL Runtime Execution | 使用现有 SQL Runtime / Controller Action | PLANNED |
| STEP-12 | DirectKey SQL Runtime Execution | 使用现有 SQL Runtime / Controller Action | PLANNED |
| STEP-13 | Ambiguous Safety Regression | `/evaluation/diagnostics/golden-regression?topK=10` | PLANNED |
| STEP-14 | NotResolved Safety Regression | `/evaluation/diagnostics/golden-regression?topK=10` | PLANNED |
| STEP-15 | Full Golden Regression | `/evaluation/diagnostics/golden-regression?topK=10` | PLANNED |
| STEP-16 | Coverage | 以当前 master Controller / Action 为准 | PLANNED |
| STEP-17 | Quality Gate | 以当前 master Controller / Action 为准 | PLANNED |
| STEP-18 | Release Gate | 以当前 master Controller / Action 为准 | PLANNED |
| STEP-19 | Phase 2.7 Exit Review | 综合 STEP-01~18，不新增测试项目 | PLANNED |

> 测试地址必须以执行前最新 master Controller / Action 为准；文档中的地址不是对未来接口存在性的假设。

## 二、强制测试纪律

```text
当前 STEP
→ 唯一地址 / 唯一输入
→ 用户返回完整原始 JSON
→ PASS / FAIL / BLOCK
→ 记录关键事实
→ 更新本文件
→ 中文 Commit master
→ 下一 STEP
```

如果 FAIL / BLOCK：

```text
停止
↓
源码根因审计
↓
修改 master
↓
git pull
↓
dotnet build
↓
重新执行当前 STEP
```

不得跳步、不得一次执行多个地址、不得通过修改 Golden Case / Gate 绕过失败。

## 三、Phase 2.7 核心验收契约

### A. MasterJoin

```text
Dimension Semantic
 ↓
Dimension Entity Key
 ↓
Master Table Detection = YES
 ↓
Join Condition
 ↓
Dimension Label
 ↓
GROUP BY
 ↓
Metric Aggregation
```

### B. DirectKey

```text
Dimension Semantic
 ↓
Dimension Entity Key
 ↓
Master Table Detection = NO
 ↓
Fact Dimension Key / Label
 ↓
GROUP BY
 ↓
Metric Aggregation
```

### C. 安全边界

```text
Ambiguous → REVIEW / BLOCK
NotResolved → BLOCK
```

不能为了提高 Positive Pass Rate 而将 Ambiguous / NotResolved 强制改为 Resolved。

## 四、每个 STEP 的记录模板

### STEP-XX — <名称>

地址 / 输入：

```text
待执行
```

原始 JSON：

```json
待用户返回
```

判定：**PLANNED**

关键验收事实：

- 待执行

结论：

- 待执行

Commit：

- 待执行

## 五、阶段 Exit

STEP-01~18 全部完成后，执行 STEP-19。

只有满足开发测试计划中的全部 Exit Criteria，才将本文件顶部状态更新为 `COMPLETE`，并同步主开发计划。

若源码已实现但 Runtime 未验证，状态必须写为：

`IMPLEMENTED_BUT_UNVERIFIED`
