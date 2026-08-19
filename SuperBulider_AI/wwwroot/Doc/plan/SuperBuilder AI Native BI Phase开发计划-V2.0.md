# SuperBuilder AI Native BI Phase开发计划

> 文档版本：v2.2  
> 文档性质：项目正式开发基线  
> 当前源码基线：GitHub `master`  
> 当前开发阶段：Phase 2.5  
> 最近完成阶段：Phase 2.4  
> Phase 2.3：COMPLETE / FROZEN  
> Phase 2.4：COMPLETE / FROZEN  
> 下一阶段：Phase 2.5 — QueryPlan Explainability
>
> 本文档不是单纯的架构设计文档。
>
> 本文档同时承担：
>
> 1. 项目 Phase 开发计划
> 2. 当前开发状态基线
> 3. 源码实现范围说明
> 4. Phase Exit Criteria
> 5. 后续开发边界
> 6. 源码与计划一致性校准基准
>
> ---
>
> ## 重要原则
>
> **本项目后续 Phase 状态必须以 GitHub `master` 实际源码为最终事实依据。**
>
> 如果：
>
> - 开发计划已经描述，但源码不存在
> - 开发计划与源码实现存在差异
> - 后续架构演进已经替代原设计
>
> 必须以源码事实为准进行校准。
>
> 不允许为了让计划“看起来完整”而虚构不存在的源码能力。
>
> 同样：
>
> **已经完成并冻结的 Phase，不再为了继续开发而重复扩展。**
>
> ------------------------------------------------------------
>
> # 第一部分 项目总体 Phase 路线
>
> ------------------------------------------------------------
>
> ## Phase 0 — 基础工程与 AI 基础设施
>
> ### 目标
>
> 建立 SuperBuilder AI Native BI 的基础工程、AI 服务、Metadata、Vector、数据库以及基础运行环境。
>
> ### 核心能力
>
> - .NET Core / ASP.NET Core
> - EF Core
> - SQL Server
> - MySQL
> - PostgreSQL
> - Qwen
> - Qdrant
> - Embedding
> - Metadata Scanner
> - Metadata Semantic
> - Metadata Vector
> - Metadata Search
>
> ### 状态
>
> **COMPLETE**
>
> ------------------------------------------------------------
>
> # Phase 1 — AI BI 基础能力
>
> ------------------------------------------------------------
>
> ## Phase 1.1 — Metadata Foundation
>
> 建立：
>
> - DataSource
> - MetadataTable
> - MetadataColumn
> - MetadataSemantic
> - Tenant
> - Metadata Learning
>
> ### 状态
>
> **COMPLETE**
>
> ---
>
> ## Phase 1.2 — Metadata Semantic
>
> 建立：
>
> - Metadata Search Text
> - Semantic Meaning
> - Keywords
> - Synonyms
> - Example Questions
> - Embedding
> - Vector Search
>
> ### 状态
>
> **COMPLETE**
>
> ---
>
> ## Phase 1.3 — Metadata Vector
>
> 建立：
>
> - Embedding Service
> - Qdrant
> - Metadata Vector Index
> - Metadata Semantic Search
>
> ### 状态
>
> **COMPLETE**
>
> ---
>
> ## Phase 1.4 — Component Runtime / Component Storage
>
> 建立：
>
> - Component Domain
> - Component Contracts
> - Component Storage
> - Component Provider
> - Component Runtime
> - Dynamic Component Resolution
>
> ### 状态
>
> **COMPLETE**
>
> ---
>
> ## Phase 1.5 — AI BI Query Foundation
>
> 建立：
>
> - Query Understanding
> - Query Intent
> - Query Filter
> - Query Metric
> - Metadata Semantic Search
> - Prompt Builder
> - SQL Dialect
> - SQL Builder
>
> ### 状态
>
> **COMPLETE**
>
> ------------------------------------------------------------
>
> # 第二部分 Phase 2 — QueryPlan Safety Pipeline
>
> ------------------------------------------------------------
>
> # Phase 2 总体目标
>
> Phase 2 不再只是解决：
>
> > “AI 能不能生成 SQL？”
>
> 而是解决：
>
> > **“AI 生成的 QueryPlan 是否足够正确、稳定、可解释，并且是否允许进入 SQL Builder？”**
>
> Phase 2 的核心架构：
>
> ```text
> User Question
>       ↓
> Query Understanding
>       ↓
> Metadata Semantic Retrieval
>       ↓
> QueryPlan
>       ↓
> Validation
>       ↓
> Repair
>       ↓
> RepairTrace
>       ↓
> Confidence
>       ↓
> Decision Gate
>       ↓
> SQL Builder
>       ↓
> SQL Execution
>       ↓
> Result Understanding
> ```
>
> Phase 2 的核心成果是：
>
> # QueryPlan Safety Pipeline
>
> ------------------------------------------------------------
>
> # Phase 2.1 — QueryPlan Foundation
>
> ------------------------------------------------------------
>
> ## 目标
>
> 将自然语言 QueryIntent 转换为结构化 QueryPlan。
>
> ## 核心模型
>
> ```text
> QueryIntent
> QueryMetric
> QueryFilter
> QueryDimension
> QueryTable
> QueryField
> QueryJoin
> QueryAggregation
> QueryOrder
> QueryPlan
> ```
>
> ## QueryPlan 应支持
>
> - Table
> - Field
> - Metric
> - Dimension
> - Filter
> - Join
> - Aggregation
> - Order
> - DataSource
>
> ## 核心链路
>
> ```text
> Question
>    ↓
> QueryUnderstanding
>    ↓
> QueryIntent
>    ↓
> Metadata Semantic Search
>    ↓
> QueryPlanBuilder
>    ↓
> QueryPlan
> ```
>
> ## 状态
>
> **COMPLETE**
>
> ## Freeze
>
> Phase 2.1 不再继续扩展基础 QueryPlan 模型。
>
> 后续新增 QueryPlan 能力必须通过明确的新 Phase 进行。
>
> ------------------------------------------------------------
>
> # Phase 2.2 — QueryPlan Semantic Validation
>
> ------------------------------------------------------------
>
> ## 目标
>
> 对 QueryPlan 进行 Metadata / Semantic / Structural Validation。
>
> ## 核心能力
>
> - Table Validation
> - Field Validation
> - Metric Validation
> - Dimension Validation
> - Filter Validation
> - Metadata Relationship Validation
> - Semantic Consistency Validation
>
> ## 核心链路
>
> ```text
> QueryPlan
>    ↓
> QueryPlanContextBuilder
>    ↓
> Metadata Context
>    ↓
> QuerySemanticValidator
>    ↓
> QueryPlanValidationResult
> ```
>
> ## Validation Result
>
> 必须能够区分：
>
> - Error
> - Warning
> - PASS
>
> ## 状态
>
> **COMPLETE**
>
> ------------------------------------------------------------
>
> # Phase 2.2.5 — QueryPlan Auto Repair
>
> ------------------------------------------------------------
>
> ## 目标
>
> 当 QueryPlan Validation 失败时，允许系统执行受控自动修复。
>
> ## 核心链路
>
> ```text
> Validation Failed
>       ↓
> QueryPlanRepairService
>       ↓
> RepairResult
>       ↓
> Modified QueryPlan
>       ↓
> Re-Validation
> ```
>
> ## Repair 原则
>
> Repair 不允许无限执行。
>
> Repair 必须：
>
> - 有明确 Attempt
> - 有变化检测
> - 有重新 Validation
> - 有失败状态
> - 有停止条件
>
> ## 状态
>
> **COMPLETE**
>
> ------------------------------------------------------------
>
> # Phase 2.3 — QueryPlan Repair Reliability
>
> ------------------------------------------------------------
>
> ## 目标
>
> 将 QueryPlan Repair 从“一次自动修改”升级为可追踪、可停止、可检测 Loop 的可靠 Pipeline。
>
> ## QueryPlanRepairTrace
>
> 当前源码已经实现：
>
> ```text
> Success
> Status
> TotalAttempts
> ChangedPlanCount
> StopReason
> History
> ```
>
> ## Repair History
>
> 每一次 Repair Attempt 可以记录：
>
> ```text
> Attempt
> RepairSuccess
> PlanChanged
> ValidationPassed
> BeforePlanFingerprint
> AfterPlanFingerprint
> BeforeValidationFingerprint
> AfterValidationFingerprint
> RepairActions
> Explanation
> FailureReason
> StallReason
> ValidationErrorCount
> ```
>
> ## Repair 状态
>
> ```text
> NotRequired
> Repaired
> Stalled
> Failed
> MaxAttemptsReached
> LoopDetected
> ```
>
> ## Reliability 能力
>
> - Repair Attempt Limit
> - Plan Fingerprint
> - Validation Fingerprint
> - Duplicate Detection
> - Loop Detection
> - Stall Detection
> - Re-Validation
> - Repair History
>
> ## 核心链路
>
> ```text
> QueryPlan
>    ↓
> Validation
>    ↓
> Repair
>    ↓
> Re-Validation
>    ↓
> ┌───────────────┐
> │ PASS          │
> │ STALLED       │
> │ LOOP          │
> │ FAILED        │
> │ MAX_ATTEMPTS  │
> └───────────────┘
> ```
>
> ## 状态
>
> # COMPLETE / FROZEN
>
> ## Freeze Rule
>
> Phase 2.3 不再继续增加 Repair 功能。
>
> 后续 Repair 策略优化必须作为独立变更处理。
>
> ------------------------------------------------------------
>
> # Phase 2.4 — QueryPlan Confidence & Decision Gate
>
> ------------------------------------------------------------
>
> # 2.4.1 目标
>
> 回答：
>
> > **“这个 QueryPlan 修复完成以后，到底有多大把握可以进入 SQL Builder？”**
>
> Confidence 不等于 Validation。
>
> 正确关系：
>
> ```text
> Validation PASS
>       ≠
> High Confidence
> ```
>
> ------------------------------------------------------------
>
> # 2.4.2 Confidence Evidence
>
> 当前 Confidence 必须综合：
>
> ```text
> Metadata Semantic Match
> Table Match
> Field Match
> Metric Match
> Dimension Match
> Filter Match
> Candidate Ranking
> Validation Quality
> Repair Count
> Repair Progress
> Repair Stability
> RepairTrace Status
> ```
>
> ## Semantic Evidence
>
> 包括：
>
> - Semantic Match Score
> - Candidate Ranking Score
> - Candidate Ranking Gap
>
> ## Table Evidence
>
> 根据 QueryPlan Table 与 Metadata Semantic Search Candidate 的匹配程度计算。
>
> ## Field Evidence
>
> 根据 QueryPlan Field / Dimension 与 Metadata Column Candidate 的匹配程度计算。
>
> ## Metric Evidence
>
> 根据 Metric Field 与 Metadata Column Candidate 的匹配程度计算。
>
> ## Dimension Evidence
>
> 根据 Dimension 与 Metadata Column Candidate 的匹配程度计算。
>
> ## Filter Evidence
>
> 根据 Filter Field 与 Metadata Column Candidate 的匹配程度计算。
>
> ## Validation Evidence
>
> 包括：
>
> - Validation Score
> - Error Count
> - Warning Count
>
> ## Repair Evidence
>
> 包括：
>
> - Repair Count
> - Changed Plan Count
> - Repair Progress
> - Repair Stability
> - Repair Status
>
> ------------------------------------------------------------
>
> # 2.4.3 Confidence Level
>
> 当前基础阈值：
>
> ```text
> High    >= 0.80
> Medium  >= 0.60
> Low     <  0.60
> ```
>
> 但 Confidence Level 不能只由 Score 决定。
>
> 以下情况必须强制进入 Low / Reject：
>
> - Validation Error
> - Repair Stall
> - Repair Loop
> - Repair Failed
> - Max Repair Attempts
>
> ------------------------------------------------------------
>
> # 2.4.4 Decision Gate
>
> Confidence 与 Decision 必须分离。
>
> ```text
> QueryPlanConfidence
>        ↓
> QueryPlanDecisionGate
>        ↓
> QueryPlanDecision
> ```
>
> Decision 类型：
>
> ```text
> Proceed
> Confirm
> Reject
> ```
>
> ------------------------------------------------------------
>
> # 2.4.5 High Policy
>
> High Confidence 只有在：
>
> ```text
> Confidence Level = High
> +
> Score >= High Threshold
> +
> CanProceed = true
> +
> 无 Hard Block
> ```
>
> 时才允许：
>
> ```text
> Proceed
> ↓
> SQL Builder
> ```
>
> ------------------------------------------------------------
>
> # 2.4.6 Medium Policy
>
> 当前正式策略：
>
> ```text
> Medium
>    ↓
> Confirm
>    ↓
> ShouldExecute = false
> ```
>
> Medium 不自动进入 SQL Builder。
>
> 后续是否执行由受控确认流程决定。
>
> 当前版本不允许把 Medium 自动解释为 SQL Builder 可执行。
>
> ------------------------------------------------------------
>
> # 2.4.7 Low Policy
>
> ```text
> Low
>    ↓
> Reject
> ```
>
> ------------------------------------------------------------
>
> # 2.4.8 Hard Safety Block
>
> 以下任意情况必须 Reject：
>
> ```text
> Validation Error
> Repair Stall
> Repair Loop
> Repair Failed
> Max Repair Attempts
> ```
>
> ------------------------------------------------------------
>
> # 2.4.9 Semantic Evidence Safety Policy
>
> # No Semantic Evidence = Hard Reject
>
> 如果没有有效 Metadata Semantic Evidence：
>
> ```text
> No Semantic Evidence
>       ↓
> BlockingReason
>       ↓
> Decision Gate Hard Block
>       ↓
> Reject
>       ↓
> 禁止进入 SQL Builder
> ```
>
> 不能因为：
>
> - Validation PASS
> - QueryPlan 结构完整
> - SQL Builder 可以生成 SQL
>
> 就绕过 Semantic Evidence Safety Policy。
>
> ## 当前正式产品策略
>
> > **在当前 Phase 2.4 中，如果不存在有效 Metadata Semantic Evidence，系统必须拒绝执行。**
>
> ## Future Policy
>
> 如果未来产品要求：
>
> > Semantic Evidence 缺失时允许人工确认
>
> 则未来可以演进为：
>
> ```text
> No Semantic Evidence
>       ↓
> Requires Confirmation
>       ↓
> Human Approval
>       ↓
> Proceed / Reject
> ```
>
> 但该能力不属于当前 Phase 2.4。
>
> ------------------------------------------------------------
>
> # 2.4.10 Explainability
>
> Confidence 必须提供：
>
> - Reasons
> - BlockingReasons
>
> Decision 必须提供：
>
> - Decision
> - ShouldExecute
> - RequiresConfirmation
> - Reason
> - DecisionTrace
>
> ------------------------------------------------------------
>
> # 2.4.11 DecisionTrace
>
> DecisionTrace 应记录：
>
> ```text
> ConfidenceScore
> ConfidenceLevel
> Decision
> ShouldExecute
> RequiresConfirmation
> HighThreshold
> MediumThreshold
> ValidationErrorCount
> RepairCount
> RepairProgressScore
> RepairStalled
> RepairLoopDetected
> RepairFailed
> MaxRepairAttemptsReached
> RepairStatus
> Reason
> BlockingReasons
> EvaluatedAt
> ```
>
> ------------------------------------------------------------
>
> # 2.4.12 SQL Builder Boundary
>
> Decision Gate 必须位于 SQL Builder 之前。
>
> ```text
> QueryPlan
>    ↓
> Validation / Repair
>    ↓
> Confidence
>    ↓
> Decision Gate
>    ↓
> ShouldExecute
>    ↓
> SQL Builder
> ```
>
> 如果：
>
> ```text
> ShouldExecute = false
> ```
>
> 则不得进入 SQL Builder。
>
> ------------------------------------------------------------
>
> # 2.4.13 Phase 2.4 Exit Criteria
>
> Phase 2.4 Exit Criteria：
>
> # 20 / 20 PASS
>
> 必须确认：
>
> 1. Confidence Model
> 2. Confidence Evidence
> 3. Semantic Match
> 4. Table Match
> 5. Field Match
> 6. Metric Match
> 7. Dimension Match
> 8. Filter Match
> 9. Candidate Ranking
> 10. Validation Evidence
> 11. Repair Count
> 12. Repair Progress
> 13. Repair Stall
> 14. Repair Loop
> 15. Repair Failed
> 16. Max Repair Attempts
> 17. Confidence Level
> 18. Blocking Reasons
> 19. Decision Gate Integration
> 20. SQL Builder Boundary
>
> ------------------------------------------------------------
>
> # 2.4.14 Phase 2.4 状态
>
> # COMPLETE / FROZEN
>
> Phase 2.4 不再继续增加功能。
>
> 当前策略：
>
> > **No Semantic Evidence = Hard Reject**
>
> 正式冻结。
>
> ------------------------------------------------------------
>
> # Phase 2.5 — QueryPlan Explainability
>
> ------------------------------------------------------------
>
> # 2.5.1 目标
>
> 在 Phase 2.3 RepairTrace 和 Phase 2.4 Confidence / DecisionTrace 的基础上，建立面向用户和开发者的完整 QueryPlan Explainability。
>
> Phase 2.5 不重复建设：
>
> - RepairTrace
> - Confidence Reasons
> - BlockingReasons
> - DecisionTrace
>
> 而是将已有信息进一步组织为完整 QueryPlan Explanation。
>
> ------------------------------------------------------------
>
> # 2.5.2 Explainability 内容
>
> 必须能够回答：
>
> ```text
> 为什么选择这个 Table？
> 为什么选择这个 Field？
> 为什么选择这个 Metric？
> 为什么选择这个 Dimension？
> 为什么选择这个 Filter？
> 为什么选择这个 Join？
> 为什么发生 Repair？
> Repair 修改了什么？
> 为什么最终 Confidence 是 High / Medium / Low？
> 为什么 Decision 是 Proceed / Confirm / Reject？
> ```
>
> ------------------------------------------------------------
>
> # 2.5.3 Explainability Architecture
>
> ```text
> QueryPlan
>    ↓
> QueryPlan Explanation
>    ├─ Intent Explanation
>    ├─ Table Selection Explanation
>    ├─ Field Selection Explanation
>    ├─ Metric Explanation
>    ├─ Dimension Explanation
>    ├─ Filter Explanation
>    ├─ Join Explanation
>    ├─ Repair Explanation
>    ├─ Confidence Explanation
>    └─ Decision Explanation
> ```
>
> ------------------------------------------------------------
>
> # 2.5.4 状态
>
> # NEXT
>
> 当前开始 Phase 2.5 前，Phase 2.4 必须保持冻结。
>
> ------------------------------------------------------------
>
> # Phase 2.6 — Query Evaluation Framework
>
> ------------------------------------------------------------
>
> # 2.6.1 目标
>
> 建立 AI BI QueryPlan 的系统化 Evaluation Framework。
>
> ------------------------------------------------------------
>
> # 2.6.2 核心能力
>
> - Evaluation Dataset
> - Expected QueryIntent
> - Expected QueryPlan
> - Expected Tables
> - Expected Fields
> - Expected Metrics
> - Expected Dimensions
> - Expected Filters
> - Expected SQL
> - QueryPlan Accuracy
> - Semantic Retrieval Accuracy
> - Confidence Calibration
> - Repair Success Rate
> - Decision Accuracy
> - Regression Testing
>
> ------------------------------------------------------------
>
> # 2.6.3 状态
>
> # PLANNED
>
> 不提前实现。
>
> ------------------------------------------------------------
>
> # Phase 2.7 — Advanced SQL Planning
>
> ------------------------------------------------------------
>
> # 2.7.1 目标
>
> 在稳定 QueryPlan Safety Pipeline 基础上进一步增强 SQL Planning。
>
> ------------------------------------------------------------
>
> # 2.7.2 核心能力
>
> - Advanced Join Planning
> - Multi-table Planning
> - Aggregation Planning
> - Grouping Planning
> - Ordering Planning
> - Date / Time Planning
> - Nested Query Planning
> - Subquery Planning
> - SQL Dialect Optimization
> - Query Performance Planning
>
> ------------------------------------------------------------
>
> # 2.7.3 边界
>
> Phase 2.7 不允许绕过：
>
> ```text
> Validation
> ↓
> Repair
> ↓
> Confidence
> ↓
> Decision Gate
> ```
>
> 所有 Advanced SQL Planning 必须建立在 QueryPlan Safety Pipeline 之上。
>
> ------------------------------------------------------------
>
> # 2.7.4 状态
>
> # PLANNED
>
> ------------------------------------------------------------
>
> # 第三部分 Phase 3 — Enterprise Business Semantic
>
> ------------------------------------------------------------
>
> # Phase 3 总目标
>
> 从：
>
> ```text
> Metadata Semantic
> ```
>
> 进一步升级到：
>
> ```text
> Enterprise Business Semantic
> ```
>
> ------------------------------------------------------------
>
> ## Phase 3.1 — Business Metric
>
> - Business Metric
> - KPI
> - Metric Definition
> - Calculation Formula
> - Business Owner
>
> 状态：
>
> **DEFERRED**
>
> ---
>
> ## Phase 3.2 — Business Dimension
>
> - Business Dimension
> - Hierarchy
> - Level
> - Attribute
>
> 状态：
>
> **DEFERRED**
>
> ---
>
> ## Phase 3.3 — Business Rule
>
> - Business Rules
> - Data Rules
> - Calculation Rules
> - Security Rules
>
> 状态：
>
> **DEFERRED**
>
> ---
>
> ## Phase 3.4 — Semantic Governance
>
> - Semantic Version
> - Approval
> - Audit
> - Governance
>
> 状态：
>
> **DEFERRED**
>
> ------------------------------------------------------------
>
> # Phase 4 — Enterprise Knowledge Graph
>
> ------------------------------------------------------------
>
> ## 目标
>
> 将：
>
> ```text
> Metadata
> Business Semantic
> Business Rules
> Query History
> ```
>
> 形成企业级 Knowledge Graph。
>
> 状态：
>
> **DEFERRED**
>
> ------------------------------------------------------------
>
> # Phase 5 — AI Native BI Agent
>
> ------------------------------------------------------------
>
> ## 目标
>
> 从单次 Query：
>
> ```text
> Question
> ↓
> SQL
> ```
>
> 升级到：
>
> ```text
> Question
> ↓
> Reasoning
> ↓
> Planning
> ↓
> Execution
> ↓
> Analysis
> ↓
> Follow-up
> ```
>
> 状态：
>
> **DEFERRED**
>
> ------------------------------------------------------------
>
> # Phase 6 — AI Native Low-Code
>
> ------------------------------------------------------------
>
> ## 目标
>
> 将 AI BI 能力与 SuperBuilder AI Native Low-Code Platform 融合。
>
> 核心：
>
> - AI Generated Components
> - Dynamic Components
> - Component Runtime
> - Component Library
> - AI Generated UI
> - AI Generated BI Applications
>
> 状态：
>
> **DEFERRED**
>
> ------------------------------------------------------------
>
> # 第四部分 Phase 开发原则
>
> ------------------------------------------------------------
>
> # 1. Master First Principle
>
> GitHub `master` 是源码事实基线。
>
> 开发计划不得虚构不存在的源码能力。
>
> ------------------------------------------------------------
>
> # 2. Freeze Principle
>
> 已完成 Phase 必须冻结。
>
> 不允许在已冻结 Phase 中无限增加功能。
>
> ------------------------------------------------------------
>
> # 3. Boundary Principle
>
> 后续 Phase 不得绕过前置 Phase 的安全边界。
>
> 特别是：
>
> ```text
> QueryPlan
> ↓
> Validation
> ↓
> Repair
> ↓
> Confidence
> ↓
> Decision Gate
> ↓
> SQL Builder
> ```
>
> 是当前 AI BI 核心安全执行边界。
>
> ------------------------------------------------------------
>
> # 4. No Semantic Evidence Safety Principle
>
> 当前正式策略：
>
> # No Semantic Evidence = Hard Reject
>
> 不允许：
>
> ```text
> Validation PASS
> +
> No Semantic Evidence
> ↓
> SQL
> ```
>
> ------------------------------------------------------------
>
> # 5. Confidence ≠ Validation
>
> 必须明确：
>
> ```text
> Validation
> =
> QueryPlan 是否满足规则
> ```
>
> 而：
>
> ```text
> Confidence
> =
> QueryPlan 是否足够可信
> ```
>
> 二者不能混淆。
>
> ------------------------------------------------------------
>
> # 6. Decision ≠ Confidence
>
> Confidence：
>
> ```text
> High / Medium / Low
> ```
>
> Decision：
>
> ```text
> Proceed / Confirm / Reject
> ```
>
> 两者必须保持架构独立。
>
> ------------------------------------------------------------
>
> # 7. SQL Builder Boundary Principle
>
> SQL Builder 不负责判断 QueryPlan 是否可信。
>
> SQL Builder 的输入必须来自：
>
> ```text
> QueryPlan Decision Gate
> ```
>
> ------------------------------------------------------------
>
> # 第五部分 当前源码基线
>
> ------------------------------------------------------------
>
> 当前 master 已形成以下核心服务链：
>
> ```text
> QueryUnderstandingService
>        ↓
> QueryPlanBuilder
>        ↓
> QueryPlanContextBuilder
>        ↓
> QueryPlanValidator
>        ↓
> QuerySemanticValidator
>        ↓
> QueryPlanRepairService
>        ↓
> QueryPlanValidationPipeline
>        ↓
> QueryPlanConfidenceService
>        ↓
> QueryPlanDecisionGate
>        ↓
> SqlQueryBuilder
>        ↓
> QueryExecutionService
>        ↓
> ResultUnderstandingService
> ```
>
> DI 已注册：
>
> ```text
> IQueryUnderstandingService
> IQueryJoinInferenceService
> IQueryPlanBuilder
> IQueryPlanContextBuilder
> IQueryPlanRepairService
> IQueryPlanValidationPipeline
> IQueryPlanConfidenceService
> IQueryPlanDecisionGate
> ISqlQueryBuilder
> IQueryExecutionService
> IResultUnderstandingService
> ```
>
> ------------------------------------------------------------
>
> # 第六部分 当前项目状态
>
> ------------------------------------------------------------
>
> ## 已完成并冻结
>
> ```text
> Phase 0
> Phase 1
> Phase 2.1
> Phase 2.2
> Phase 2.2.5
> Phase 2.3
> Phase 2.4
> ```
>
> ## 当前开发
>
> ```text
> Phase 2.5
> ```
>
> ## 后续
>
> ```text
> Phase 2.6
> Phase 2.7
> Phase 3
> Phase 4
> Phase 5
> Phase 6
> ```
>
> ------------------------------------------------------------
>
> # 第七部分 Phase 2.x 最终状态矩阵
>
> ------------------------------------------------------------
>
> | Phase | 名称 | 状态 |
> |---|---|---|
> | Phase 2.1 | QueryPlan Foundation | COMPLETE |
> | Phase 2.2 | Semantic Validation | COMPLETE |
> | Phase 2.2.5 | Auto Repair | COMPLETE |
> | Phase 2.3 | Repair Reliability | COMPLETE / FROZEN |
> | Phase 2.4 | Confidence & Decision Gate | COMPLETE / FROZEN |
> | Phase 2.5 | QueryPlan Explainability | NEXT |
> | Phase 2.6 | Query Evaluation Framework | PLANNED |
> | Phase 2.7 | Advanced SQL Planning | PLANNED |
>
> ------------------------------------------------------------
>
> # 第八部分 Phase 2.4 最终冻结声明
>
> ------------------------------------------------------------
>
> Phase 2.4 已满足全部 Exit Criteria。
>
> 当前最终安全策略：
>
> # No Semantic Evidence = Hard Reject
>
> 当前 Decision Policy：
>
> ```text
> High
>     ↓
> Proceed
>
> Medium
>     ↓
> Confirm
>
> Low
>     ↓
> Reject
>
> Hard Block
>     ↓
> Reject
> ```
>
> Phase 2.4 正式：
>
> # COMPLETE / FROZEN
>
> ------------------------------------------------------------
>
> # 第九部分 当前下一步
>
> ------------------------------------------------------------
>
> # Phase 2.5 — QueryPlan Explainability
>
> 当前不进入：
>
> - Phase 3
> - Enterprise Semantic
> - Knowledge Graph
> - AI Agent
> - Low-Code AI Generation
>
> 当前只进入：
>
> ```text
> Phase 2.5
> QueryPlan Explainability
> ```
>
> 目标：
>
> > 让系统不仅能够生成、验证、修复、评估 QueryPlan，还能够清楚解释 QueryPlan 是如何形成的，以及为什么最终允许、要求确认或拒绝执行。
>
> ------------------------------------------------------------
>
> # 第十部分 最终项目原则
>
> ------------------------------------------------------------
>
> SuperBuilder AI Native BI 的核心链路不是：
>
> ```text
> AI
> ↓
> SQL
> ```
>
> 而是：
>
> ```text
> AI
> ↓
> Understand
> ↓
> Retrieve
> ↓
> Plan
> ↓
> Validate
> ↓
> Repair
> ↓
> Evaluate
> ↓
> Decide
> ↓
> Execute
> ↓
> Understand Result
> ↓
> Explain
> ```
>
> 最终目标：
>
> # AI Native BI
>
> 不只是让 AI “生成 SQL”。
>
> 而是让 AI 成为一个：
>
> **可理解、可验证、可修复、可评估、可决策、可解释、可治理的 BI Query Planning Engine。**
>
> ---
>
> **Document Version:** v2.2
>
> **Source Baseline:** GitHub `master`
>
> **Current Phase:** Phase 2.5
>
> **Phase 2.3:** COMPLETE / FROZEN
>
> **Phase 2.4:** COMPLETE / FROZEN
>
> **Next:** Phase 2.5 — QueryPlan Explainability
>
> **Current Semantic Safety Policy:** No Semantic Evidence = Hard Reject