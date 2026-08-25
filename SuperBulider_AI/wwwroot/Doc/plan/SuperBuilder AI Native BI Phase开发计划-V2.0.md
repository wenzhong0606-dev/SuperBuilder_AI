# SuperBuilder AI Native BI Phase开发计划

> 文档版本：v2.9
> 文档性质：项目正式开发基线 + Phase 开发测试管理总计划
> **唯一源码基线：GitHub `master`**
> 主计划职责：**仅记录项目当前进度、当前工作单元、当前 STEP、状态、Commit、阻塞与下一步**
> 阶段完整开发任务、完整测试步骤和完整验收标准由对应 Phase 开发测试计划维护，本文件负责关联与当前进度同步。

---

# 一、最高优先级开发约束

1. **GitHub `master` 是唯一源码基线。** 所有源码审计、Phase 状态、计划校准均以 `master` 最新源码为准。
2. **禁止创建新的开发分支。** 正式源码、修复、文档更新直接修改 `master`。
3. **不得使用其他分支、临时工作区或历史代码冒充 `master` 完成状态。**
4. **GitHub 正式描述、开发计划、源码审计说明、提交说明统一使用中文。** C# 类型、方法、属性和 Namespace 仍遵循代码规范。
5. **源码事实优先于计划。** 计划中的 COMPLETE 必须能在 `master` 找到真实实现，并完成要求的集成验证。
6. **代码文件存在不等于功能完成。** 必须检查 Interface、Implementation、Caller、Model/DTO、Constructor、DI、Runtime、Controller、Golden Regression。
7. **Phase / C 项完成必须满足 Exit Criteria。** 未完成 Runtime / Regression 不得宣布 COMPLETE。
8. **Phase 2.3、2.4、2.5 已冻结。** 后续仅允许明确 Bug、Contract 问题或新阶段证明的兼容性问题修复。
9. **Production Safety Pipeline 与 Evaluation Framework 保持边界。** Evaluation 不替代生产执行安全链。
10. **Golden Dataset 是 Evaluation 的事实数据基础。** 不得使用临时人工判断代替 Golden Regression。
11. **每次正式修改必须明确所属 Phase / C 项，并使用中文 Commit 描述。**
12. **不得为了让开发计划完整而虚构源码能力。**
13. **AI 生成或修改的代码不视为天然正确。** 必须按照与人工代码相同的完整源码审计、DI、Build、Runtime、Regression 标准验收。
14. **禁止以“发现一个编译错误、修一个错误”为主要开发方式。** 编译只能作为完整源码审计后的验证环节。
15. **任何关键 Contract、DI、Namespace、Constructor Dependency 变化，都必须进行上下游闭环检查。**
16. **不新建独立 Test Project。** 优先使用现有 Controller / Runtime 诊断接口进行真实验证。
17. **不得通过删除功能、放宽 Gate、修改 Coverage Analyzer 或伪造 Golden Case 来掩盖能力缺失。**
18. **Ranking / DetailRanking / AggregateRanking 必须遵守自身 Contract 边界。** 不得为了当前 C 项通过 Coverage 而强行删除、绕过或重新定义这些能力。
19. **每次 GitHub 更新后必须能够从 Commit、开发计划和源码恢复当前工作状态，确保会话中断后可继续。**
20. **每次代码修改推送 `master` 后，必须先由本地环境 `git pull` 拉取最新 `master`，再进行本地 Build 与 Controller / Action Runtime 测试。** 本地实际结果作为正式验收证据。
21. **每进入一个新的 Phase，必须先全量审计当前 `master`，再确定本阶段全部开发任务。**
22. **阶段开发计划必须在该 Phase 开始开发前建立，并包含本阶段全部开发与测试步骤。**
23. **每个 STEP 开始与完成时，必须同步更新主开发计划和阶段开发计划。**
24. **主开发计划只记录当前进度，不复制阶段完整技术方案和历史测试过程。**
25. **阶段开发计划是该 Phase 的完整执行契约；新增任务必须先更新阶段计划再开发。**
26. **每个 Phase 必须建立对应 Runtime 分步测试记录，记录真实原始 JSON 和 PASS / FAIL / BLOCK / REVIEW。**
27. **上一 Phase 未满足 Exit Criteria 时，不得把下一 Phase 标记为 COMPLETE；允许提前建立 PLANNED 的入口文档，但不得伪装为已完成。**
28. **阶段开发计划发生任何实质性变更时，必须同步更新本主开发计划。** 该同步不仅适用于 STEP 开始/完成，也适用于审计结论、冻结结果、修改范围、禁止修改范围、兼容性约束、Runtime / Build / Gate 结果、下一 STEP、Exit Criteria 等任何影响当前开发状态或执行契约的变化。
29. **阶段计划与主计划必须保持状态一致。** 阶段计划已冻结或进入下一 STEP，而主计划未同步时，视为开发流程阻断条件；不得进入下一 STEP。
30. **标准步骤必须执行“审计 → 最终结论 → 冻结 → 更新阶段计划 → 同步主计划 → 确认 GitHub `master` → 下一 STEP”。** 仅在聊天中宣布冻结或完成，不视为正式完成。
31. **任何一份正式开发计划发生实质性变更后，必须检查并同步所有受影响的计划 / Runtime 记录。** 不得出现阶段计划、主计划、Runtime 记录三者描述互相矛盾的状态。

---

# 二、Phase 生命周期与文档管理规则

## 2.1 Phase 入口强制流程

每进入一个新 Phase，必须执行：

```text
上一 Phase Exit Review
        ↓
锁定最新 master
        ↓
全量源码 + 文档 + Runtime + Golden 审计
        ↓
确认真实完成度 / 遗留项 / 风险
        ↓
确定本 Phase 全部开发任务
        ↓
建立 PhaseX.Y-主题-开发测试计划.md
        ↓
建立 Runtime 分步测试记录
        ↓
同步主开发计划
        ↓
确认 master
        ↓
开始 STEP-01
```

## 2.2 STEP 强制闭环

```text
当前 STEP
   ↓
完整源码 / Contract / Runtime 审计
   ↓
最终结论
   ↓
冻结当前 STEP
   ↓
更新阶段开发计划
   ↓
同步更新主开发计划
   ↓
同步 Runtime 记录（如受影响）
   ↓
确认 GitHub master
   ↓
才能进入下一 STEP
```

## 2.3 文档一致性 Gate

任何以下情况均触发同步检查：

- Phase / STEP 状态变化
- 审计结论变化
- 修改 / 禁止修改范围变化
- 兼容性约束变化
- Runtime / Build / Gate 结果变化
- 下一 STEP 变化
- Exit Criteria 变化

若阶段计划已更新而主计划未同步，则当前 STEP **不得继续**。

## 2.4 主计划职责

主计划只记录当前进度、当前 STEP、状态、Commit、阻塞、下一步以及必要的冻结摘要；不得复制阶段完整技术方案。阶段计划仍是 Phase 的完整执行契约。
