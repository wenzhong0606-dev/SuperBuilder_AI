> **治理声明**：本文档是 `Master_Development_Plan.md` 的**输入 / 审计基线**，**不是**独立执行计划。所有里程碑状态、优先级与验收以 `Master_Development_Plan.md`（唯一事实来源）及其 `milestones/` 拆分文档为准；本文与 MDP 冲突时以 MDP 为准。映射见 MDP §17。
>
> **文档角色**：P0-09 专项（映射到 M0-08/M9-04）

# SB-P0-09 诊断接口隔离与回滚

## 生效策略

- `/metrics` 和 `api/metadata-vector/status` 要求有效令牌及 `platform:diagnostics:view`；`platform:diagnostics:manage` 同时包含只读访问能力。
- `/test/*` 与 `api/metadata-vector` 的创建、重建等操作要求有效令牌及 `platform:diagnostics:manage`。
- `/evaluation/*` 仅在 Development 映射，用于 Golden 与本地诊断。Production、Staging 及其他非 Development 环境不会注册这些端点，访问结果为 404。
- 应用不接受“内网 Header”或调用方自报来源作为放行依据。

## 部署配置

生产主进程不提供开启 `/evaluation/*` 的配置开关。确需线上诊断时，应启动独立的受控诊断进程，并由反向代理或网络策略限制来源；不得直接对公网暴露。

`platform:diagnostics:*` 权限通过 Identity 权限目录幂等种子写入。当前 `platform-admin` 获得这两项权限；后续 SB-P0-11 会将其迁移为只含最小 `platform:*` 权限的纯治理角色。

## 验证

1. Production 请求 `/evaluation/golden-runtime/run` 应返回 404。
2. `/test/*`、`/metrics` 无令牌返回 401，普通业务令牌返回 403。
3. `api/metadata-vector` 普通业务令牌返回 403。
4. Development 的 Golden 回归保持 18/18 PASS。

## 回滚

回滚应用版本即可恢复旧路由行为；权限目录新增项为兼容性数据，可保留且不会授予普通角色。若必须清理，先确认没有角色绑定后，再通过受控数据库迁移删除对应 `RolePermissions` 与 `Permissions`，禁止直接在生产库手工删除。
