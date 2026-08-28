# SuperBuilder AI Native BI - 项目长期记忆

## 项目定位
AI Native BI 平台 — 自然语言 → AI理解 → 语义分析 → 查询计划 → SQL生成 → 数据分析 → 业务答案

## 技术栈
- .NET 10 (net10.0) + EF Core 10 + Dapper
- Qdrant 向量数据库 (1024维 Embedding)
- Qwen LLM (通义千问 qwen3.7-plus)
- 多数据库: SQL Server / MySQL / PostgreSQL
- 业务数据库: WMS MySQL (192.168.16.120 steccn_wms)

## 代码规模
- 总计 ~34,127 行 C# 代码
- Services: 74 文件 / ~21,096 行 (核心)
- Models: 78 文件 / ~4,912 行
- Controllers: 15 文件 / ~2,259 行

## 当前 Phase 状态 (2026-08-28)
- Phase 0 (基础架构): Done
- Phase 1 (AI BI查询核心链 1.1-1.5): Done
- Phase 2 (QueryPlan可靠性): In Progress
  - 2.1/2.2/2.2.5/2.4/2.5: Done
  - 2.7 DimensionAware: Golden Regression 已达标（D18: 18/18 全 PASS，overall/positive/negative 均 100%，failedGates=[]），Gate 解除
- Phase 3.1 (Golden Runtime + CSV Fixture): 已实现并修复回归（2026-08-28 恢复 18/18）
- Phase 4-6: 未开始

## D18 达标结论 (2026-08-27)
- Golden Dataset 18 cases: 11 positive + 5 negative + 1 ambiguous + 1 unresolved，全部 expectedOutcome 满足
- 关键修复链: GQ-005/009 供应商 MasterJoin 语义对齐 → GQ-008 confidence → GQ-002 EntityCount COUNT 强制 → GQ-010 Dimension 漏产出兜底
- 核心模式: Resolution 是权威绑定，LLM intent 的漂移（聚合、漏 Filter/Dimension）在 QueryPlanBuilder 用确定性规则覆盖，评分器断言不放宽

## Phase 3.1 回归事故与恢复 (2026-08-26~28)
- 事故根因: `MetadataCsvFixtureService.ImportAsync` 全局 RemoveRange（无租户过滤）清空全部租户，WMS golden (Tenant 1/DS 1) 被误删 → 18 case 全 BLOCK
- 已根治: ImportAsync 删除范围改为仅限 C13_3_CSV_FIXTURE 自身租户子树（已编译部署到 5032）
- 恢复路径: 重建 Tenant1/DS1(SET QUOTED_IDENTIFIER ON + IDENTITY_INSERT) → GET /Metadata/Scan 重扫 WMS → 413 业务语义 + Qdrant 961 点 → come_time(SemanticId=1910) 语义补"入库日期"(SQL+Qdrant payload 整体 upsert；Qdrant1.19 PATCH payload 端点 404)
- 恢复后 18/18 全 PASS；run 间抖动源于 golden 运行时实时调 Qwen LLM 的非确定性（GQ-008 幽灵维度/403 限流），非环境问题
- 服务启动: `ASPNETCORE_URLS="http://localhost:5032" dotnet bin/Debug/net10.0/SuperBuilder_AI.dll`（默认 5000，必须显式设）；Qdrant 需从中性可写 CWD 启动（/c/tmp/qdrant_run）
- runtime JSON 判定字段是 `expectedOutcomeSatisfied`（负例的 decision 预期即 FAIL/BLOCK/REVIEW）

## D14 关键约束
- 多数据库动态 Resolution: MasterJoin / DirectKey / Ambiguous / NotResolved
- 禁止硬编码业务表/字段
- 必须逐行全量源码审计后一次性最小修改
- Golden Dataset: 18 cases (18/18 pass as of D18)
