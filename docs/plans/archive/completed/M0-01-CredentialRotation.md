> **HISTORICAL SNAPSHOT / 历史快照**：本文是归档资料，只描述记录当时的计划、状态或审计判断。文中的“当前、唯一、已完成、未完成、风险、测试基线、HEAD”等均不得解释为现在的项目状态。当前事实请按 `docs/README.md` 的治理顺序核验。\n\n> **治理声明**：本文档是 `Master_Development_Plan.md` 的**输入 / 审计基线**，**不是**独立执行计划。所有里程碑状态、优先级与验收以 `Master_Development_Plan.md`（唯一事实来源）及其 `milestones/` 拆分文档为准；本文与 MDP 冲突时以 MDP 为准。映射见 MDP §17。
>
> **文档角色**：M0-01 专项（映射到 M0-01）

# M0-01 凭据迁移与轮换 — 扫描结果与处置手册

> 状态：当前树已清理 ✅；**Git 历史重写已完成并验证 ✅**（见 §5.1）；外部凭据实际轮换仍需用户侧执行（§4）。
> 关联：`docs/Master_Development_Plan.md` §4 M0-01（凭据迁移与轮换）。

## 1. 处置原则（按“已经泄露”处置）

- 假设所有已提交凭据均已泄露，立即轮换：元数据库、业务 MySQL（WMS）、LLM/API、Token 签名密钥及其他已出现密钥。
- 当前树（working tree / 已提交文件）不得再含真实凭据；改用环境变量、本地未提交覆盖文件或生产密钥管理服务注入。
- 完整 Git 历史、制品、CI 日志、备份、镜像均纳入泄露范围评估。

## 2. 当前树（已提交文件）整改结果 ✅

| 文件 | 整改 |
|------|------|
| `SuperBuilder_AI/appsettings.json` | 已移除 3 类真实凭据：① `ConnectionStrings.DefaultConnection/SuperBI`（`User Id=live;***REMOVED***`）② `ConnectionStrings.WmsMySql`（`Uid=admin;Pwd=***REMOVED***`，业务 WMS MySQL `192.168.16.120:3306`）③ `Qwen.ApiKey` / `Embedding.ApiKey`（真实 `***REMOVED*** 密钥）。现仅保留非敏感结构（host/port/db/endpoint/model），凭据值以占位符表示。 |
| `SuperBuilder_AI/src/Api/Program.cs` | 新增 `builder.Configuration.AddJsonFile("appsettings.Local.json", optional:true, reloadOnChange:true)`，支持本地未提交覆盖文件。 |
| `.gitignore` | 新增 `appsettings.Local.json` / `appsettings.*.local.json` / `secrets.json` 忽略规则，确保本地密钥永不入仓。 |
| `SuperBuilder_AI/appsettings.Local.json`（新建，已忽略） | 存放迁移自原 `appsettings.json` 的真实值，本地开发继续可用；**未提交、被忽略**。 |

注入方式（任选，优先级：环境变量 > `appsettings.Local.json` > `appsettings.json` 占位符）：
- 环境变量：`ConnectionStrings__WmsMySql`、`ConnectionStrings__DefaultConnection`、`ConnectionStrings__SuperBI`、`Qwen__ApiKey`、`Embedding__ApiKey`、`Auth__SigningKey`（生产必填，由 `AuthSigningKeyPolicy` 强制校验）。
- 本地：`appsettings.Local.json`（已就位，git 忽略）。
- 生产：密钥管理服务（如云 KMS / Vault）注入上述环境变量。

## 3. Git 历史扫描结果（泄露范围）

使用 `git log --all -S"<secret>"` 全量扫描，命中提交如下：

| 凭据 | 命中提交 |
|------|----------|
| WMS MySQL 密码 `***REMOVED***` | `483a460`（初始提交）、`338ad24`、`8f4b7dc` |
| LLM API Key `***REMOVED*** | `e042827` |
| 元数据库 `***REMOVED***` | `483a460`、`f87ab1e`、`2e212f5`、`ea7e414`、`0535f1e`、`4172084`、`87b95f2` |

结论：泄露凭据分布在多个历史提交中，**仅靠“当前树清理”不足以消除泄露**——任何可访问历史的人仍能取得旧凭据。因此必须执行 §5 的历史重写（需授权）**且**执行 §4 的实际轮换（旧凭据作废后历史中的旧值自然失效）。

## 4. 外部凭据实际轮换（用户侧动作，必需）

历史重写**不能替代**凭据轮换。请立即在对应系统中轮换：

1. **业务 WMS MySQL（`192.168.16.120:3306`）**：在 MySQL 侧 `ALTER USER 'admin'@'%' IDENTIFIED BY '<新强密码>';`，记录旧凭据撤销时间；更新本地 `appsettings.Local.json` 与所有部署环境变量；验证新凭据可连、旧凭据已拒绝。
2. **LLM API Key（DashScope / 阿里云百炼 `***REMOVED*** Key 并签发新 Key；更新 `Qwen__ApiKey` / `Embedding__ApiKey`；验证调用成功、旧 Key 返回 401。
3. **Token 签名密钥 `Auth:SigningKey`**：生产已通过 `AuthSigningKeyPolicy` 强制显式配置（非开发环境禁用旧 dev 回退串）；生产签发新的强随机密钥（≥32 字节、≥8 个不同字符），经环境变量注入；旧 Key 签发的令牌在密钥轮换后失效（配合 `SecurityStamp` 吊销链）。
4. **元数据库（Platform）**：若 `live/root` 为真实凭据，同样在数据库侧重置口令并同步注入。

验证：逐项证明“旧值不可用、新值可用”，并记录轮换时间与验证结果（满足验收“新旧凭据逐项轮换并证明旧值不可用”）。

## 5. Git 历史重写（已完成 ✅）

> **执行状态：✅ 已完成并验证（2026-09-04）。** 用户经 AskUserQuestion 明确选择「授权重写(推荐)」；本仓库全部提交均为本地未推送，重写后所有克隆需重新 clone。

推荐方案（二选一）：

- **BFG Repo-Cleaner**（简单）：`java -jar bfg.jar --replace-text secrets.txt SuperBuilder_AI.git`，其中 `secrets.txt` 列出需替换的明文（如 `***REMOVED***`、`***REMOVED***` 全串、`***REMOVED***`）。
- **git filter-repo**（更现代）：`git filter-repo --replace-text secrets.txt` —— **本次实际采用此方案**。

执行前清单：
1. 已取得用户明确授权（本文件 §5 动作需用户确认）。
2. 全量备份当前仓库（含所有分支/标签）：`git bundle create ../sb-ai-prerewrite.bundle --all`。
3. 冻结推送；通知所有持有克隆的协作者暂停同步。
4. 执行重写后，所有克隆必须重新 clone（旧克隆含旧历史，不可用）。
5. 重写后强制推送需全员协调（当前无远端推送，风险低）。
6. 重写后重新运行 §3 扫描，确认历史中不再含明文凭据。

### 5.1 实际执行记录（2026-09-04，已完成 ✅）

| 步骤 | 结果 |
|------|------|
| 授权 | 用户通过 AskUserQuestion 明确选择「授权重写(推荐)」。 |
| 全量备份 | `git bundle create SuperBuilder_AI-prerewrite-20260904T153230.bundle --all`（3.2 MB，含所有分支/标签）。 |
| 工具 | `git filter-repo`（托管 Python 3.13.12 安装，版本 `a40bce548d2c`）。 |
| 替换规则 `secrets-replace.txt` | `literal:***WMS_PASSWORD***`；`literal:***META_DB_PASSWORD***`；`literal:***OLD_SIGNING_KEY***`（真实 `Auth:SigningKey` 旧值，64 字符 HMAC 密钥）；`regex:sk-<PREFIX>-[^\s"']*`（清掉 LLM Key 残留与文档记号）。 |
| 过程 | 首次重写因 SIGTERM 中断，致原仓库 `HEAD` 损坏（`bad object`）；从 bundle 克隆恢复干净副本，二次重写成功（`FILTER_REPO_EXIT=0`，629 提交全部重写，新 HEAD=`a2eae96`）；将干净 `.git` 置回工作区 `SuperBuilder_AI` 并 `git reset --hard` 对齐工作树。 |
| 历史验证 | `git log --all -S` 对全部 5 类明文签名（WMS 口令 / 元库口令 / 旧 `Auth:SigningKey` / LLM Key 前缀 / LLM 账户标识）均为 **0 提交**；另扫 `AKIA`/`eyJ`/`Bearer `/`AIza`/`-----BEGIN`/`P@ssw0rd` 等均为 0 或仅测试/代码误报（如 minified JS 中 `AIza` 子串、测试用 `Bearer secret-token`）。 |
| 本地密钥 | `appsettings.Local.json` 从 `.bak` 还原（gitignore，已确认忽略），本地开发不受影响。 |
| 收尾遗留 | `SuperBuilder_AI/.git-corrupt/`（旧损坏 .git）与 `/c/developer/GIT/SuperBuilder_AI-clean/`（git-less 副本）待手动删除（受批量删除保护拦截，需用户确认）；bundle 备份保留为安全网。 |

> 注意：历史重写**不能替代** §4 的外部凭据实际轮换——旧凭据在云端/数据库侧仍有效，必须逐一作废并证明旧值不可用。

## 6. 日志与敏感字段防护（代码侧核查）

- `AuthSigningKeyPolicy` 已 fail-fast，禁止 dev 回退串用于非开发环境。
- `UnifiedExceptionMiddleware` / `ApiClient.ParseApiError` 统一错误治理，业务抛点零改动；需确保异常消息不回显连接串 / 口令 / Key（现有 `ErrorCodes` 友好文案不含敏感字段）。
- 既有规约：**禁止记录口令、连接串、Token、模型 API Key**；新增日志点须避免 `request` / `connectionString` 全文写入。

## 7. 验收对照（Master §4 M0-01）

| 验收项 | 状态 |
|--------|------|
| 新旧凭据逐项轮换并证明旧值不可用 | ⏳ 待 §4 用户侧执行 |
| 当前树与 Git 历史扫描结果归档 | ✅ 本文件 + §3 |
| 必要的历史重写已按审批方案完成 | ✅ 2026-09-04 已完成并验证（§5.1） |
| 数据库备份不能直接恢复明文连接串 | ⏳ 随 §4/§5 达成 |
| 日志和错误无敏感字段 | ✅ 代码侧已满足（§6） |
| 当前树无真实凭据（环境变量/密钥管理注入） | ✅ §2 |

## 8. 下一步

1. **（待用户侧）外部凭据实际轮换（§4）**：WMS MySQL / LLM API Key / `Auth:SigningKey` / 元数据库口令逐一作废并证明旧值不可用，回填本地 `appsettings.Local.json` 与部署环境变量。此项历史重写无法替代。
2. **（待用户侧）最终 `git push`**：M0-02~M0-08、M0-01 当前树清理及历史重写均为本地未推送提交（新 HEAD=`a2eae96`），需在已登录 GCM 终端执行 `git push`，或提供 PAT。历史已重写，旧克隆需重新 clone。
3. 清理遗留目录 `SuperBuilder_AI/.git-corrupt/` 与 `/c/developer/GIT/SuperBuilder_AI-clean/`（受批量删除保护，需显式确认）。
4. 上述 §4 完成后，M0-01 方可标记完全 ✅，M0 阶段全部 🔴 清零。
