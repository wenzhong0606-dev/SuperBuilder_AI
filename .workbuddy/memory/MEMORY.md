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

## 当前阶段状态 — 主阶段脊柱（2026-08-28 统一）
> 旧"Phase 0-6"工程分类与用户"10阶段产品路线"已合并为**单一事实来源**：`docs/DevelopmentPlan.md`（§0 主阶段脊柱）。避免 Phase 编号冲突（旧 Phase3=BusinessEntity已done ≠ 用户 Phase3=Business Semantic Model=下一步）。

- **Stage 0 基础与 AI Native BI 运行时**：✅ 全部完成
  - Phase 0/1/2(2.1-2.5)/2.7(D14-D21 FROZEN, Phase2.7 READY TO CLOSE)/3.1 Golden Runtime+回归恢复
  - Golden 18/18 PASS（11+5+1+1, overall/pos/neg 100%, failedGates=[]）
- **Stage 1 架构治理（A-track，并行）**：🟡 A1/A2/A4 完成，A3/A5 暂缓（用户指令"A3和A5先不做"）
  - A1(4.1) 目标文档/骨架/删40冗余/归档13计划: ✅
  - A2(4.2) 单项目内 src/ 四层物理迁移(234 .cs, 命名空间保留, build 0 error, Golden 18/18): ✅
  - A3(4.3) 抽独立项目 Domain/Application/Infrastructure/Api: ⬜（用户指令暂缓，不随 P3 启动）
  - A4(4.4) 上帝类拆分+依赖倒置(I*端口): ✅ 已完成（commit 9981d73 decompose QueryPlanBuilder，8 partial 拆分；P6 前无阻塞）
  - A5(4.5) 限界上下文解耦/合并重复模型: ⬜（用户指令暂缓）
- **Stage 2 产品演进（P-track，用户10阶段路线）**：✅ P3 已完成，**P4 Multi-Tenant Platform Core ✅ 已全绿（P4.1/P4.2/P4.3/P4.4 均 ✅；build 0 error；跨租户单测 4/4；Golden 18/18 PASS ×P4.2/P4.3/P4.4 三轮）**。**P5 Multi-Language Runtime ✅ 已全绿（P5.1~P5.4 均 ✅；单测 65/65；Golden 18/18 PASS ×4 轮）**。**P6 Low-code BI Engine ✅ 已全绿（P6.1/P6.2/P6.3/P6.4 均 ✅；单测 117/117；Golden 18/18 PASS）**。**P7 Multi-Theme / Style Engine ✅ 已全绿（P7.1~P7.4 均 ✅；单测 147/147；Golden 18/18 PASS ×4 轮 P7.1/P7.2/P7.3/P7.4）**：P7.1 领域聚合+持久化+内置默认主题；P7.2 ThemeContext 接入 PlatformContext + 级联解析服务；P7.3 渲染引擎接入主题（ThemeRenderMapper + 逐组件 StyleSpec）；P7.4 Theme 管理端点（CRUD+指派+复制+蓝图）+ P7 总验收闭合。A3/A5 仍缓（用户指令）。
  - **P5 零回归核心手法（可复用）**：多语言能力一律以「非默认语言才启用」门控隔离（`locale==null || IsDefault || Culture 空` → 短路），默认语言路径行为逐字节不变。这让"触碰 Golden 依赖文件"的改动也能安全落地（P5.3 改检索服务、P5.4 改意图理解服务，Golden 均全绿）。
  - **约束**：Golden 契约 `Evaluation/Golden/query-plan-golden-v1.json` 不可删改 —— 多语言问句验证只能用独立的确定性离线一致性测试，不能改写契约。
  - P3 Business Semantic Model / P4 Multi-Tenant Core / P5 Multi-Language / P6 Low-code BI / P7 Theme ✅(全绿) / P8 AI App Builder ✅(P8.1~P8.4 全绿) / P9 AI Agent / Copilot ✅(P9.1~P9.4 全绿, 单测 231/231, Golden 18/18 PASS ×3 轮) / P10 Enterprise SaaS 进行中(P10.1 ✅ Identity 基础设施：User/Role/Permission/UserRole/RolePermission + 迁移 20260830093125_P10_1_Identity + IIdentityService/IdentityService 幂等种子+确定性 RBAC + IdentityCatalog；单测 240/240；Golden 18/18 PASS ×1 轮)
  - **P8 零回归核心手法（同 P5）**：`IAppBuilderAgent` 双路径 —— 默认路径 `BuildFromDslAsync`（结构化 DSL→AppPlan，确定性、不调 LLM、零回归）；非默认路径 `GenerateFromDescriptionAsync`（自然语言→`IQwenService`→`IAppDslSerializer` 先校验后信任→AppPlan，仅显式传入描述才启用 LLM）。`AppBuildResult` 统一承载含 `UsedAi` 标记。不触碰 Golden 依赖文件。
  - **P9 零回归核心手法（同 P8）**：`IAgentPlanner` 双路径 —— 默认路径 `PlanFromIntentAsync`（意图→`ToolRegistry` 确定性工具选择 + 异常信号附加 `BuildAnomalyChain` 异常链，纯确定性、不调 LLM）；非默认路径 `GenerateFromDescriptionAsync`（自然语言→`IQwenService`→`IAgentDslSerializer` 先校验后信任→AgentPlan，仅显式描述启用 LLM）。`AgentController` 严格复用默认路径处理 CRUD/Update，不触碰 Golden 依赖文件。异常原因分析链路（销售额→同比→环比→区域→客户→产品→渠道）由 `ToolRegistry.BuildAnomalyChain` 确定性生成。
  - 每个 P 阶段退出门槛 = Golden 18/18（硬约束，来自 Phase3 回归事故教训）
- 主交付文档：`docs/DevelopmentPlan.md`(唯一事实来源) + `docs/PhaseChecklist.md`(可执行清单) + `docs/ARCHITECTURE.md`(目标结构)

## 架构治理 Phase 4 (2026-08-28)
- 全量盘点: 236 C# 文件 / ~36,676 行（含 obj 生成件），核心 5 文件 QueryPlan* 约 6.2k 行；16 控制器中 12 个为诊断/测试
- 目标架构: 四层(Domain/Application/Infrastructure/Api) + 5 限界上下文(Organization/Metadata/BI Query/Business Entity/Golden) + 可选多项目拆分
- 设计文档: `docs/ARCHITECTURE.md`（目标树/映射/冗余）；`docs/DevelopmentPlan.md`（阶段+完成情况+路线）
- 历史计划文档归档: `docs/plans/archive/`（原 `wwwroot/Doc/plan/*`，13 份）
- 本次已删冗余: `Evaluation/DebugOutputs/*`(38 调试产物) + `Models/BI/QueryRepairAction.cs`/`QueryRepairResult.cs`(遗留死代码)；`dotnet build` 仍 0 error
- Phase 4.2 已落地: 234 .cs 全部迁至 `src/` 四层（Domain 79 / Application 109 / Infrastructure 29 / Api 17），旧顶层目录(Data/Models/Services/Interfaces/Controllers/Configuration/Infrastructure)已清空删除；git 跟踪为 247 个 rename；Golden 18/18 未破
- 待 Phase 4.5 合并重复: GoldenBaseline↔GoldenBaselinePersistenceRecord；QueryPlanSemanticResolution↔SemanticApplicabilityResult/*Resolution

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
