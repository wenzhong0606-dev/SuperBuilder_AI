# SuperBuilder AI Native BI Phase 阶段开发与测试管理规则

> 文档性质：项目长期开发管理规则
> 唯一源码基线：GitHub `master`
> 适用范围：Phase 2.7 及后续所有新阶段

## 一、阶段切换强制规则

每进入一个新的 Phase，必须先完成“阶段定义 → 开发计划 → 测试计划 → 文档关联”，再开始该阶段源码开发。

不得出现：

```text
先改代码
↓
做测试
↓
最后才补开发计划
```

必须采用：

```text
上一 Phase Exit Criteria
        ↓
确认阶段切换条件
        ↓
定义新 Phase Scope / Contract / Exit Criteria
        ↓
建立新 Phase 开发计划
        ↓
建立新 Phase Runtime 分步测试记录
        ↓
将开发计划与测试记录互相引用
        ↓
开始源码开发
```

## 二、每个新 Phase 必须形成的文档

每进入一个新阶段，至少必须建立：

1. `PhaseX.Y-主题-开发测试计划.md`
2. `PhaseX.Y-Runtime分步测试记录.md`

必要时增加：

3. `PhaseX.Y-源码审计记录.md`
4. `PhaseX.Y-Golden验收记录.md`
5. `PhaseX.Y-问题与决策记录.md`

其中“开发测试计划”定义本阶段为什么做、做什么、怎么完成；“Runtime 分步测试记录”定义每一步具体执行什么地址、返回什么 JSON、如何判定。

## 三、开发计划必须包含的内容

每个 Phase 开发计划至少包含：

```text
1. 阶段目标
2. 阶段边界
3. 前置阶段状态
4. 核心 Contract
5. 数据流 / 调用链
6. 源码修改范围
7. 开发任务分解
8. Golden Dataset / Runtime 测试范围
9. 每一步完成条件
10. Exit Criteria
11. 禁止绕过项
12. 下一 Phase 入口条件
```

## 四、Runtime 分步测试必须包含的内容

每个 Phase 必须提前定义完整测试序列，格式参考：

`Phase2.6-Runtime分步测试记录.md`

该记录必须遵守：

```text
STEP-N
 ↓
唯一地址 / 唯一输入
 ↓
用户执行
 ↓
返回完整原始 JSON
 ↓
判定 PASS / FAIL / BLOCK
 ↓
记录关键验收事实
 ↓
更新文档
 ↓
提交 master
 ↓
进入下一 STEP
```

禁止一次要求用户执行多个 Runtime 地址。

## 五、失败 / BLOCK 处理规则

如果当前 STEP 为 FAIL / BLOCK：

```text
停止进入下一 STEP
↓
定位源码根因
↓
明确修改面
↓
修改 master
↓
git pull
↓
dotnet build
↓
重新执行当前 STEP
```

不得通过跳过当前步骤、修改测试契约或删除失败 Case 进入下一阶段。

## 六、阶段完成规则

Phase 只有在满足本阶段 Exit Criteria 后才能标记 COMPLETE。

```text
源码实现
AND
Build
AND
Controller / Runtime
AND
Golden Regression（如适用）
AND
Coverage / Quality / Release Gate（如适用）
AND
文档更新
AND
开发计划状态同步
```

缺少任一关键证据时使用：

`IMPLEMENTED_BUT_UNVERIFIED`

不得使用 COMPLETE。

## 七、阶段文档关联规则

每个新 Phase 的开发计划必须明确关联：

```text
主开发计划
   ↕
PhaseX.Y-开发测试计划
   ↕
PhaseX.Y-Runtime分步测试记录
   ↕
源码 Commit
   ↕
Golden / Runtime 原始 JSON
```

因此即使会话中断，也必须能够仅通过 GitHub master 中的文档恢复：

```text
当前 Phase
→ 当前 C 项
→ 当前 STEP
→ 当前源码 Commit
→ 下一步做什么
```

## 八、禁止重复工作的规则

任何已经完成的审计、测试或结论，必须记录到对应 Phase 文档。

后续会话开始时优先读取：

1. 当前主开发计划；
2. 当前 Phase 开发测试计划；
3. 当前 Phase Runtime 分步测试记录；
4. 最新相关源码 Commit。

不得因为会话中断而重新执行已经 PASS 的基础 Runtime 步骤，除非源码 / 环境发生影响该步骤的变更。

## 九、文档更新时机

以下事件发生后必须同步文档：

- Phase 切换；
- C 项完成；
- 当前 STEP PASS / FAIL / BLOCK；
- Contract 发生变化；
- Golden Dataset 发生变化；
- Runtime 测试结果发生变化；
- Exit Criteria 发生变化；
- 新增或冻结开发方向。

## 十、Git 提交规则

正式源码、开发计划、测试记录均直接提交 `master`，Commit 描述统一使用中文，并明确：

```text
Phase / C 项 + 本次动作 + 结果
```

示例：

```text
完成 Phase 2.7 Dimension Resolution 开发测试计划与 Runtime 分步测试记录
```
