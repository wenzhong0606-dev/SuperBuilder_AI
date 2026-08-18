SuperBuilder AI Native BI
AI Native Business Intelligence Platform
1. 项目定位

SuperBuilder AI Native BI 是一个基于 AI Agent、语义理解、Metadata Knowledge、Vector Search 和动态 SQL Generation 的下一代智能商业分析平台。

目标：

让用户通过自然语言直接完成：

业务问题
    ↓
AI理解
    ↓
业务语义分析
    ↓
自动生成查询计划
    ↓
自动生成SQL
    ↓
数据分析
    ↓
智能解释

实现：

从传统 BI 的“报表驱动”转向 AI Native 的“问题驱动”。

2. 核心理念

传统 BI：

用户
 ↓
报表
 ↓
固定指标
 ↓
数据库

问题：

需要提前设计报表
需要熟悉数据模型
业务变化响应慢

SuperBuilder AI Native BI：

用户自然语言


↓


AI Agent


↓


Business Semantic Understanding


↓


Query Planning


↓


SQL Generation


↓


Data Analysis


↓


Business Answer

特点：

无需提前设计报表
支持自然语言分析
支持动态业务问题
支持多数据库
支持企业知识沉淀
3. 当前系统架构
                    User


                     |
                     ↓


          BI Conversation Layer


                     |
                     ↓


          Query Understanding


                     |
                     ↓


              Query Plan


                     |
        +------------+------------+


        ↓                         ↓


 Metadata Semantic          Validation Engine




        ↓                         ↓


 Vector Search              Repair Loop




        +------------+------------+


                     ↓


              SQL Builder


                     ↓


          Database Execution Layer


                     ↓


             Result Understanding


                     ↓


              AI Business Answer


4. 当前代码完成状态
Overall Status
Version:


SuperBuilder AI Native BI v1




Completion:


80% ~ 85%


Current Phase:


Phase 2.3 QueryPlan Reliability Enhancement


5. 已完成阶段
Phase 0 基础工程架构

状态：

✅ Completed

目标：

建立：

.NET Core架构
服务分层
数据访问基础
DI体系

完成：

Models


Interfaces


Services


Infrastructure


Controllers


Dependency Injection


Phase 1 AI BI查询核心链

状态：

✅ Completed

目标：

实现：

自然语言 → 查询结果

完成：

Phase 1.1 Query Understanding

状态：

✅

能力：

用户问题理解
QueryIntent生成
指标识别
条件识别
Phase 1.2 Metadata Semantic Layer

状态：

✅

完成：

数据库Metadata扫描
Table语义
Column语义
Business Meaning
Synonym
Phase 1.3 Vector Semantic Search

状态：

✅

完成：

Metadata


↓


Embedding


↓


Qdrant


↓


Semantic Retrieval


Phase 1.4 QueryPlan Engine

状态：

✅

完成：

Question


↓


QueryPlan



支持：

Table选择
Field选择
Metric
Filter
Dimension
Phase 1.5 SQL Generation & Execution

状态：

✅ 基础完成

完成：

SQL Dialect

支持：

SQL Server


MySQL


PostgreSQL


SQL Execution

完成：

SqlQuery


↓


ConnectionFactory


↓


Dapper


↓


QueryResult


Phase 2 QueryPlan Reliability

当前阶段：

Phase 2.3 QueryPlan Reliability Enhancement

目标：

让 AI 查询结果从：

“可以运行”

升级：

“可信赖”

Phase 2.1 QueryPlan基础能力

状态：

✅ Completed

完成：

QueryPlan Model

包括：

QueryIntent


QueryMetric


QueryDimension


QueryFilter


QueryAggregation


QueryJoin


QueryPlan


QueryPlan Builder

完成：

QueryPlanBuilder


├── Search


├── TableSelection


├── FieldResolution


├── PlanAssembly


└── Diagnostics


Phase 2.2 QueryPlan Semantic Validation

状态：

✅ Completed

目标：

验证：

AI生成计划是否符合业务语义。

完成：

QueryPlanContextBuilder

负责：

QueryPlan


↓


Metadata Context


QuerySemanticValidator

验证：

Metric
指标是否存在
聚合是否合法
Dimension
维度是否合法
Filter
条件是否合法
Phase 2.2.5 QueryPlan Repair Loop

状态：

✅ Completed

能力：

Invalid QueryPlan


        ↓


Validation


        ↓


Repair Intent


        ↓


Rebuild


        ↓


Validate Again



实现：

AI自动修复查询计划。

当前开发阶段
Phase 2.3 QueryPlan Reliability Enhancement

状态：

🚧 In Progress

Phase 2.3.1 QueryPlan Graph Engine

目标：

当前：

QueryPlan


Tables


Fields


Metrics



升级：

QueryPlan Graph




Table Node


Column Node


Metric Node


Dimension Node


Join Node




Semantic Relationship



价值：

支持复杂分析场景。

Phase 2.3.2 QueryPlan Explainability

目标：

让AI解释：

“为什么这么查询？”

新增：

QueryPlan Explanation



输出：

例如：

选择订单表原因:


用户问题包含:


销售额


时间趋势




匹配:


order.amount


order.create_time


Phase 2.3.3 Query Evaluation Framework

目标：

建立AI BI质量体系。

建立：

Question Dataset


        ↓


Expected QueryPlan


        ↓


Generated SQL


        ↓


Execution Result


        ↓


Evaluation Score



用于：

Prompt优化
模型评估
回归测试
Phase 2.3.4 Advanced SQL Generation

当前限制：

基础SQL已经完成。

下一步：

增强：

Join Engine

支持：

QueryJoin


↓


INNER JOIN


LEFT JOIN


Group Analysis

支持：

Dimension


↓


GROUP BY


Sorting

支持：

ORDER BY


Top Analysis

支持：

TOP N


LIMIT


Phase 3 Business Semantic Layer

目标：

从：

数据库字段理解

升级：

业务对象理解。

Phase 3.1 Business Entity Model

建立：

Customer


Product


Order


Revenue


Cost



业务实体。

Phase 3.2 Business Metric Engine

建立：

企业指标体系：

例如：

销售额


增长率


利润率


客户价值


Phase 3.3 Semantic Knowledge Layer

建立：

Business Term


↓


Metric


↓


Dimension


↓


Data Source


Phase 4 Enterprise Knowledge Graph

目标：

构建企业数据知识大脑。

能力：

Business Knowledge Graph




Entity


Relation


Metric


Rule


Event



支持：

企业知识发现
自动分析
智能推荐
Phase 5 AI Native BI Agent

最终目标：

从：

AI查询助手

升级：

AI业务分析Agent。

能力：

Autonomous Analysis
发现问题


↓


分析原因


↓


生成报告


↓


提出建议


Multi Agent

包括：

Query Agent


Analysis Agent


Report Agent


Decision Agent


Phase 6 AI Native Low-Code Integration

与 SuperBuilder 平台融合。

目标：

实现：

AI Native Application Builder


+


AI Native BI



能力：

用户描述：

我要一个销售分析系统

AI自动生成：

页面
组件
数据模型
BI分析能力
当前开发优先级
P0

必须完成：

Phase 2.3 QueryPlan Reliability Enhancement


P1

增强：

Phase 3 Business Semantic Layer


P2

平台化：

Phase 4 Knowledge Graph


P3

最终目标：

Phase 5 AI Native BI Agent


Phase 6 AI Native Low-Code Platform


项目最终愿景

SuperBuilder AI Native BI:

不是一个 BI 报表工具。

而是：

一个能够理解企业业务、自动规划分析路径、自动访问数据、自动生成洞察的 AI Native Business Intelligence Agent。