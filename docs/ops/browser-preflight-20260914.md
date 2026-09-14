# 本机浏览器登录预验证

> 日期：2026-09-14；代码：445e7a2；使用前轮新编译产物。
> 浏览器：Codex 内置浏览器；API/Web：127.0.0.1:15050 / 15080；数据库：SuperBuilder_Preflight_20260914。

## 结果

- 未登录访问 /ask 后到达 /login。
- 初次页面显示“初始化入口已关闭”：隔离库未建立首位平台管理员。通过现有 PlatformBootstrap 配置机制、临时随机口令完成隔离库初始化后，登录表单正常显示。未开启匿名初始化，不涉及业务库。
- 表单出现用户名、口令、E2E App 租户及登录按钮。
- 浏览器输入测试账号后，提交仍显示“username 必填”；Web 日志记录 POST /api/auth/login 返回 400。截图曾显示输入框确有文本，不能将 DOM 快照不显示输入值当作未输入证明。
- 分别尝试语义填充、失焦、键盘输入及逐键输入，未成功进入 /ask。部分临时标签失效后重建，未修改应用源码。
- 同一测试账号直接 API 登录成功（前轮与本轮启动检查），不能据此判定表单登录成功。

## 定性与后续

这是一项浏览器验证阻塞，根因尚未确定。需要进一步区分本浏览器输入事件兼容性与应用表单绑定/提交问题；不得未经独立复现就修改安全或绑定逻辑。

现有 PermissionMatrixE2ETests 使用 LoginHelper.ApiLoginByCodeAsync，通过 API 获取会话并注入浏览器，因此权限矩阵通过不等于 Login.razor 表单通过。AuthFlowTests 的有效登录依赖 SB_E2E_TENANT，检查报告时必须区分实际执行与跳过。

待验证：有效表单登录、错误口令提示（本轮停在 username 必填，未到口令判断）、登录后刷新、只读账号权限表现。不得标记通过。

后续先用可观察的真实键盘输入或现有 AuthFlowTests 独立复现，在确认产品缺陷后补回归并修复；本轮不使用 API 注入会话绕开目标流程。

测试账号为仓库 E2E 种子账号，仅用于隔离本机环境。未调用模型、未查询客户业务数据、未执行压测。初始化临时口令与登录 token 不保存于文档。

## 独立复验结果（同日续验，更新上述待验证状态）

复用同一隔离库、API/Web 产物，运行仓库 Microsoft.Playwright 的独立 Chromium 测试，显式配置 SB_E2E_TENANT，防止有效登录被跳过。首轮 AuthFlowTests 为 3 通过、0 失败、0 跳过，真实 LoginHelper.LoginAsync 填表登录进入 /ask，未注入会话绕过该用例。

随后增强 Valid_Login_ReachesAsk：登录后等待 Ask 输入框可见，整页刷新，再次确认输入框可见、路由仍为 /ask 且无登录按钮。只增加测试断言，未修改应用登录实现。

最终 AuthFlowTests + PermissionMatrixE2ETests：**20 项，19 通过、0 失败、1 跳过**。TRX 位于 `artifacts/preflight-20260914/auth-permissions-final.trx`；有效表单登录与刷新用例 Passed。跳过项为 Admin_Agent_RunButton_Visible_WhenAgentsExist，原因是隔离租户无智能体种子。权限矩阵仍使用 API 登录注入，不应描述为每条权限用例都经真实表单登录。

结论：标准 Chromium 中真实表单登录及刷新已通过，之前内置浏览器行为未独立复现，尚不能认定为应用绑定缺陷。内置浏览器输入兼容性保留待查，不盲目修改登录代码。无效登录现有用例仅断言错误提示可见，未精确断言错误类型，不据此声称已验证特定错误密码文案。

临时服务已由测试脚本 finally 关闭，隔离库保留。尚未覆盖：带真实数据的 Agent Run 正向、业务问数/数据源扫描、跨租户业务结果、性能及正式 Windows Server 托管。
