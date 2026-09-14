# 当前电脑开发预验证：环境检查

> 日期：2026-09-14；用户指定当前电脑；代码 HEAD：445e7a26f2f894ceead5ff021b55204dc2afbf6f。
> 本轮仅查询环境与服务，不启动或停止服务，不修改数据库，不调用模型，不执行压测。工作树包含本次文档准备改动。

| 项目 | 实测结果 | 含义 |
|---|---|---|
| OS | Windows 11 专业版，10.0.26200 | 可作开发预验证；不等同 Windows Server 2022 验收 |
| CPU | Intel Core i7-1260P，12 核/16 逻辑处理器 | 混合核心移动处理器，不能按核数直接等同服务器性能 |
| 内存 | 31.7 GiB | 接近参考配置 32 GB |
| C 盘 | 1905.5 GiB，总可用 1324.9 GiB | 空间充足；本次未检查介质类型或磁盘性能 |
| SDK | 10.0.301（另有 9.0.100） | .NET 10 SDK 已安装 |
| ASP.NET / .NET runtime | 10.0.9 | 已安装；发布与构建仍需实际验证 |
| SQL Server | MSSQLSERVER Running；查询返回 16.0.1000.6 / Developer Edition (64-bit) | 本机集成认证只读查询成功；本轮未查迁移或业务数据，不推断数据库已就绪 |
| Qdrant | localhost:6333 根接口返回 1.19.0；6333/6334 监听；进程名 qdrant | 当前依赖服务可访问 |
| Qdrant 运行形式 | 用户 Downloads 下 qdrant-x86_64-pc-windows-msvc/qdrant.exe | 本机确有原生 Windows 程序；不是容器。路径不适合作为已定稿的交付安装目录 |
| 应用监听抽查 | 本次未发现 5032/5080/5081 监听 | 仅针对列出的端口；不推断其他端口无应用 |

## 证据与限制

硬件、卷与监听状态通过 PowerShell/CIM 读取；SQL 只查询 SERVERPROPERTY；Qdrant 只读取根服务信息及进程路径。初次沙箱读取硬件失败，结果不可用；授权后的读取成功，以上采用后者。SQL 沙箱集成认证失败，授权后同一查询成功，不能把初次失败当成应用连接缺陷。

## 下一步验证范围

1. 复用当前 SQL Server 和原生 Qdrant 做开发预验证；不重装已有依赖。
2. 核对应用发布与启动配置、隔离测试数据库及样例数据，防止启动自动任务影响现有业务数据。
3. 验证 API/Web 构建、实际启动、登录及服务连通；记录目标 SHA 与实际运行产物。
4. 明确 Qdrant 数据目录、启动账号、开机启动和异常恢复，之后再形成可重装的 Windows 交付步骤。
5. 本机可做探索性测量；正式容量目标与恢复目标仍由 O2 确认，Windows Server 2022 兼容性需另测。

关联：[参考配置](windows-pilot-profile.md)、[发布证据](release-evidence.md)。

## 第二轮：构建与隔离启动实测

用户要求继续验证；本轮新建独立数据库 `SuperBuilder_Preflight_20260914`，不连接现有业务库。API/Web 使用本轮新编译产物、随机临时签名与加密密钥，仅绑定 127.0.0.1:15050 / 15080。模型密钥置空，不执行 Ask、扫描或模型调用；测试账号来自既有 E2E 种子。

| 验证项 | 本轮结果 |
|---|---|
| Web Release 构建 | 独立输出目录成功，0 错误、3 警告；包括 Ask.razor CS8602 与两个未使用字段警告，尚未修复 |
| API/测试工程 | 随 dotnet test 完整编译成功，存在 XML 注释、编译器及测试分析器警告；不声称零 warning |
| 隔离空库迁移 | 启动日志记录应用 46 条迁移，最后为 M13_03_DataSourceConnectionStringEncryption；API 最终 Ready |
| 真实登录接口 | E2E 测试租户管理员登录返回 token；仅记录是否存在，不保存 token |
| Web 登录页面 | GET /login 返回 HTTP 200；未验证浏览器交互或 SignalR 登录流程 |
| 进程与数据 | 脚本在 finally 中停止自己启动的 API/Web；原有进程未停止。隔离库保留供排查，临时密钥未持久化，不作为交付数据库 |

首次默认目录构建因已有 .NET Host PID 38436 锁定 RCL DLL 失败，改用 `C:/developer/GIT/sb-preflight-20260914` 输出后成功。未通过终止现有应用解决文件锁。

第一次完整单测为 1163 通过、5 失败、0 跳过；失败全部为测试从 AppContext.BaseDirectory 向上查找源码时找不到仓库。保留失败 TRX，将同一批新构建测试产物复制到仓库 `artifacts/preflight-20260914/test-runtime` 后完整复跑，不修改测试或过滤用例。

本机证据：仓库 `artifacts/preflight-20260914/local-preflight.trx`（首轮）、独立输出目录 `smoke-result.json`（脱敏结果）及 api/web 日志。日志不作为公开交接资料。

这些结果仅覆盖 Windows 11 开发预验证；未覆盖正式服务托管、Windows Server 安装、浏览器 E2E、业务查询、升级恢复或性能门禁。

最终复跑 TRX（local-preflight-final.trx）确认：total=1168、executed=1168、passed=1168、failed=0、notExecuted=0；测试进程退出码 0。首轮失败记录保留用于说明输出目录限制。
