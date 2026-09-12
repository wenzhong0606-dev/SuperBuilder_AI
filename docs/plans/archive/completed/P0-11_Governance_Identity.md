> **HISTORICAL SNAPSHOT / 历史快照**：本文是归档资料，只描述记录当时的计划、状态或审计判断。文中的“当前、唯一、已完成、未完成、风险、测试基线、HEAD”等均不得解释为现在的项目状态。当前事实请按 `docs/README.md` 的治理顺序核验。\n\n> **治理声明**：本文档是 `Master_Development_Plan.md` 的**输入 / 审计基线**，**不是**独立执行计划。所有里程碑状态、优先级与验收以 `Master_Development_Plan.md`（唯一事实来源）及其 `milestones/` 拆分文档为准；本文与 MDP 冲突时以 MDP 为准。映射见 MDP §17。
>
> **文档角色**：P0-11 专项（映射到 M0-03/M2-01）

# SB-P0-11 平台治理身份

## 边界

- `platform-admin` 是纯治理角色，只包含 `platform:*` 权限，不再包含 Dashboard、Metadata、DataSource、App、Agent、Theme 等租户业务权限。
- 平台身份归属正 Id 的 `platform` 租户；`TenantId=0` 只保留全局角色/权限目录语义，不作为用户所属租户。
- 普通租户 Identity API 不返回 `platform-admin` 或 `platform:*` 权限，也不能创建、指派或把治理权限写入自定义角色。
- 带任意 `platform:*` 权限的令牌访问租户数据面会被拒绝；允许的管理面限定为租户管理、平台审计/配额、诊断和当前身份查询。

## 幂等迁移

应用启动时会幂等创建 `TenantCode=platform`，将 `platform-admin` 的权限绑定校正为权威治理目录，并撤销所有非平台租户用户的历史 `platform-admin` 绑定。迁移不删除用户和业务数据。

## 首个治理账号

应用不提供匿名 bootstrap API，也不会在源码、配置文件或日志中内置治理凭据。首个治理账号必须由受控部署流程或数据库迁移工具在 `platform` 租户下创建，并绑定全局 `platform-admin` 角色；凭据应由外部身份提供方或密钥管理系统注入。该步骤在正式密码认证 SB-P0-04A 完成前不得对公网开放。

重复执行时应按 `TenantCode=platform`、用户名和角色绑定进行幂等查找，不重复创建记录。普通租户管理 API 不是治理账号初始化通道。

## 回滚

代码回滚不会自动恢复已撤销的危险绑定。若业务确认必须恢复，应从审计备份中逐条评审后通过受控迁移处理，禁止重新把 `platform-admin` 分配给普通租户用户。
