# Phase 2.6 C.13.3 最终修复进度记录

> 日期：2026-08-24  
> 所属阶段：Phase 2.6 — Query Evaluation Framework  
> 工作单元：C.13.3 / Golden Runtime Evaluation  
> 源码基线：GitHub `master`  
> 原则：先完成完整根因审计，再统一修复；修复后必须本地 `git pull`、Build、Controller/Action Runtime、Golden Regression 验证。

## 一、当前发现

STEP-04 Golden Dataset 全量运行最初被 Semantic Applicability Gate 阻塞。随后 GQ-001 的 Semantic Applicability 已成功进入 `Resolved`，但 QueryPlan Evaluation 仍失败：

- Intent：期望 `Aggregate`，实际 `Detail`；
- Metric：期望 `SUM`，实际 `NONE`；
- QueryShape：期望 `IsAggregate=True`，实际 `False`；
- Semantic Resolution 已正确绑定 `wms_storage_receipt_info.quantity`，因此本次根因不是 Qdrant Semantic Resolution。

## 二、Qwen 调用链审计结论

当前生产查询理解链明确为：

```text
EvaluationDiagnosticsController.QueryPlanEvaluation
    ↓
QueryUnderstandingService.UnderstandAsync
    ↓
QueryIntentNormalizer.Normalize
    ↓
QwenService.GenerateSqlAsync
    ↓
Qwen HTTP Chat Completions
```

源码已确认 `QueryUnderstandingService` 直接调用 `_qwenService.GenerateSqlAsync(prompt)`；`QwenService` 使用 `Qwen:Endpoint`、`Qwen:Model`、`Qwen:ApiKey` 发起 HTTP POST。此前本地实际调用原工作空间 URL 返回 HTTP 404，因此该地址不能作为当前 Runtime 验收地址。

## 三、本次修复

### 1. Qwen / Embedding Endpoint 修复

将百炼兼容接口统一切换到官方北京地域公共兼容地址：

```text
Qwen:
https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions

Embedding:
https://dashscope.aliyuncs.com/compatible-mode/v1/embeddings
```

Qwen 模型保留 `qwen3.8-max`；Embedding 保留 `qwen3.7-text-embedding` / 1024 维。官方文档确认北京地域 OpenAI Compatible Chat Completions 使用上述公共地址，并支持 Qwen Max 系列；Embedding 接口支持 `qwen3.7-text-embedding` 与 1024 维输出。

### 2. API Key 安全修复

`appsettings.json` 不再保存 API Key，改为空值，由本地环境变量提供：

```text
Qwen__ApiKey
Embedding__ApiKey
```

由于本次会话中 API Key 已经出现在终端/聊天内容中，应视为已暴露凭据；正式使用前应在百炼控制台撤销旧 Key 并重新生成新 Key。

### 3. QueryIntent 聚合 Contract 修复

修改：

```text
Services/AI/QueryUnderstanding/QueryIntentNormalizer.cs
```

新增确定性 `MetricOnlyAggregate` 规范化：

```text
无维度
+ 无 OrderBy
+ 无 OrderDirection
+ 无 Limit
+ 存在 Metric
+ 非明细/记录/列表语义
        ↓
Aggregate
```

当模型返回 `NONE` 时，对明显业务指标执行确定性聚合推断：

```text
普通数量 / 金额 / 销量 → SUM
单数量 / 单据数量 / 订单数量 / 供应商数量 / 客户数量 → COUNT
```

Ranking 查询不进入该规则，避免破坏 `DetailRanking / AggregateRanking` Contract。

## 四、当前 GitHub 提交

```text
1aef64bd2d91b9db7817e71d35c85493430fca95
修复 Phase 2.6 Qwen 与 Embedding API 接入地址

ee32aae744012077597e086e43c56ff4165cddb8
修复 Phase 2.6 QueryIntent 指标聚合语义漂移

aa2ba96a063538c0f342173e878a9c9de32fd65a
移除配置文件中的 API Key，改为环境变量注入并保留百炼兼容接口
```

## 五、当前状态

```text
源码根因审计                  COMPLETE
Qwen 调用链审计               COMPLETE
原 404 Endpoint 根因确认       COMPLETE
Qwen / Embedding Endpoint 修复 COMPLETE
API Key 配置安全修复           COMPLETE
Metric-only Aggregate 修复     IMPLEMENTED / UNVERIFIED
本地 git pull                  待执行
本地 Build                     待执行
Qwen Runtime 调用              待执行
GQ-001 QueryPlan Evaluation    待重新执行
Golden 全量 Regression         待重新执行
Coverage                       待重新确认
Quality Gate                   待重新确认
Release Gate                   待重新确认
Phase 2.6                     IN PROGRESS
```

## 六、下一步唯一动作

本次代码修改完成后，不直接宣布通过。严格执行：

```text
git pull
↓
dotnet build
↓
启动项目
↓
Qwen 连通性 / Query Understanding Runtime
↓
GQ-001 QueryPlan Evaluation
↓
STEP-04 Golden 全量
↓
GQ-006 / GQ-010 / GQ-011 / GQ-N004 / GQ-N005
↓
Coverage
↓
Quality Gate
↓
Release Gate
```

任何一步失败，继续定位当前根因，不跳过，不通过修改 Gate 掩盖失败。
