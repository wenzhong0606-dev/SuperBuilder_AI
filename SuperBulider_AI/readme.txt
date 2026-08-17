当前阶段路线

先把整个后续路线固定下来，名称全部中文：

Phase 1
AI BI 查询核心链路
        ✅ 当前基本完成


        ↓


Phase 2
查询计划验证与可靠性
        ← 当前阶段
Phase 2.1.1 QueryPlan基础能力
Phase 2.1.2 QueryPlan Metadata关系完整性验证 ✅
Phase 2.2：查询计划语义一致性验证
Phase 2.2.1：Metadata语义模型读取与查询计划语义验证设计
Phase 2.2.2：QuerySemanticValidator 实现设计
Phase 2.2.2.1：QueryPlanContextBuilder 设计
Phase 2.2.2.2：QueryPlanContextBuilder 实现
Phase 2.2.3：QuerySemanticValidator 实现
Phase 2.2.3.1：语义验证模型实现
Phase 2.2.3.2：QuerySemanticValidator 实现
Phase 2.2.4：验证链集成
Phase 2.2.5：QueryPlan 自动修复链（AI Repair Loop）

        ↓


Phase 3
业务语义层


        ↓


Phase 4
数据关系知识图谱


        ↓


Phase 5
SQL 安全与查询治理


        ↓


Phase 6
多轮 BI 对话与上下文理解


        ↓


Phase 7
智能可视化


        ↓


Phase 8
反馈学习与持续优化


        ↓


Phase 9
AI BI 评测与基准体系


        ↓


Phase 10
企业级生产能力


格式会严格按照：

Phase 2：查询计划验证与可靠性


一、当前 master 实际状态


二、现有能力


三、现有问题


四、目标架构


五、文件修改总表


┌───────────────┬────────┬──────────┐
│ 文件          │ 操作   │ 原因     │
├───────────────┼────────┼──────────┤
│ xxx.cs        │ 新增   │ ...      │
│ xxx.cs        │ 修改   │ ...      │
│ xxx.cs        │ 不修改 │ ...      │
└───────────────┴────────┴──────────┘


六、逐文件详细设计


七、完整源码


八、DI 修改


九、数据库修改（如果需要）


十、调用链


十一、测试用例


十二、编译验收标准


十三、Phase 2 完成标准