# SuperBuilder AI Native BI Phase 开发测试管理规则

> **规则已合并进入：`SuperBuilder AI Native BI Phase开发计划-V2.0.md` V2.9**
> 本文件保留为兼容入口与备份，不再维护第二套独立规则。

## 当前唯一执行基线

Phase 管理、阶段入口全量审计、阶段开发计划、Runtime 分步测试、STEP 同步、Exit Criteria、会话恢复等规则，统一以主开发计划中的：

- **第二章《Phase 生命周期与文档管理规则》**
- **第三章《强制标准工作方式：完整审计后再修改》**
- **第四章《Contract / Namespace / DI 强制闭环》**
- **第五章《Golden / Regression / 安全规则》**
- **第二十一章《最终开发原则》**

为准。

## 核心流程

```text
上一 Phase Exit Review
        ↓
全量审计当前 master
        ↓
确定本阶段全部开发任务 / 全部测试任务
        ↓
建立 Phase 开发测试计划
        ↓
建立 Phase Runtime 分步测试记录
        ↓
关联主开发计划
        ↓
STEP 开始：同步主计划 + 阶段计划
        ↓
开发 / Build / Runtime
        ↓
STEP 完成：同步主计划 + 阶段计划 + Runtime 记录
        ↓
进入下一 STEP
```

## 主计划与阶段计划职责

**主开发计划：**只记录当前 Phase、当前 C / 工作单元、当前 STEP、状态、Commit、阻塞、下一步和已完成 Phase 状态。

**阶段开发计划：**记录该 Phase 的完整入口审计、全部开发任务、全部测试任务、源码范围、步骤完成条件、Runtime 步骤、Exit Criteria、Quality / Release Gate。

**Runtime 分步测试记录：**记录每个 STEP 的真实地址 / 输入、完整原始 JSON、PASS / FAIL / BLOCK / REVIEW 和证据。

## 规则优先级

如果本文件与主开发计划存在任何差异：

> **以 `SuperBuilder AI Native BI Phase开发计划-V2.0.md` 最新版本为准。**

后续不再在本文件单独增加管理规则，避免产生两套管理基线。