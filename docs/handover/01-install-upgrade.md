# 01 · 安装与升级指南

> 受众：**[运维]** 客户 IT / SRE / 实施工程师
> 所属：M14-07 交接包｜状态：**草稿**（`【待实测回填】` 位待 PERF-01 定稿）

---

## 1. 组件与前置条件

| 组件 | 要求 | 说明 |
|---|---|---|
| 应用运行时 | **.NET 10**（ASP.NET Core） | 应用为 Blazor Server 单体；含 Web + API 同一进程 |
| 元数据库 | **SQL Server 2019+** | 存租户/用户/权限/审计/业务语义/迁移历史 |
| 向量库 | **Qdrant 1.19**（gRPC 6334 / REST 6333） | 集合 `superbi_metadata`，向量维度 **1024** |
| 业务库 | 客户自有的 **SQL Server 或 MySQL** | 被分析的数据源，不由本系统维护 |
| 大模型 | 阿里云百炼（DashScope）兼容模式 | 对话 + 向量化各一个 Key |
| 反向代理 | Nginx / IIS ARR 等（可选） | 生产建议置于 HTTPS 之后 |

**端口**：应用默认 `http://localhost:5032`（开发）／生产由托管方式决定；Qdrant `6334`(gRPC)、`6333`(REST)。

**实测硬件前提**：`【待实测回填 —— 依据 PERF-01/M13-12 的 CPU/内存/磁盘/并发结论】`

## 2. 部署前需准备的密钥与配置

> **红线**：以下全部走**环境变量或密钥管理器**注入。**禁止**写入仓库、写入文档、写入日志。（延续 SEC-01）

### 2.1 必需（缺失即启动失败）

| 键 | 必需性 | 约束 | 说明 |
|---|---|---|---|
| `Auth:SigningKey` | **必需** | 非开发环境：**≥ 32 UTF-8 字节**、**≥ 12 个不同字符**、**不得等于历史开发默认值** | 令牌 HMAC 签名密钥。不满足直接抛异常，**无硬编码回退**。建议 `openssl rand -base64 48` 生成 |
| `SecretStore:MasterKey` | **必需** | Base64 32 字节 | 数据源连接串的落库加密主密钥。**丢失 = 已加密连接串不可解**，务必备份到密钥管理器 |
| `ConnectionStrings:DefaultConnection` | **必需** | — | 元数据库连接串（SQL Server） |
| `ConnectionStrings:SuperBI` | **必需** | — | 同上（语义层使用；应与 DefaultConnection 指向同一库） |
| `Qdrant:Host` / `Qdrant:Port` | **必需** | 默认 `localhost` / `6334` | 向量库地址 |
| `Qwen:ApiKey` | **必需** | — | 对话模型调用 |
| `Embedding:ApiKey` | **必需** | — | 向量化调用 |

### 2.2 按需

| 键 | 默认 | 说明 |
|---|---|---|
| `ConnectionStrings:WmsMySql` | 示例值 | 客户业务库连接串（按实际引擎调整） |
| `Qdrant:CollectionName` / `VectorSize` | `superbi_metadata` / `1024` | **与 Embedding 维度必须一致**，改动需重建集合 |
| `Qwen:Model` | `qwen-plus` | 对话模型 |
| `Embedding:Model` / `Dimensions` / `TimeoutSeconds` | `qwen3.7-text-embedding` / `1024` / `120` | 向量模型 |
| `PlatformBootstrap:AllowAnonymous` | `true` | **生产建议置 `false`**，改由部署配置提供初始平台管理员账号 |
| `SelfRegistration:Enabled` | `false` | 自助注册开关（试点默认关闭） |
| `SelfRegistration:DefaultCulture` / `DefaultAvailableCultures` | `zh-CN` | 新租户默认语言。⚠️ 见 `02` §3 已知限制 |
| `RateLimit:LoginLimit` / `GlobalLimit` | `10` / `120` | 生产按容量调整；CI/压测需放宽 |
| `RateLimit:Store:Type` | `Memory` | 多实例部署**必须**改为共享存储，否则限流与缓存不一致 |

## 3. 首次安装（空环境）

### 步骤

```bash
# 0) 准备：SQL Server 空库 + Qdrant 已启动
# 1) 注入密钥（示例：把值换成真实密钥）
export Auth__SigningKey='<32+字节随机串>'
export SecretStore__MasterKey='<Base64 32字节>'
export ConnectionStrings__DefaultConnection='Server=<host>;Database=SuperBuilder_Platform;User Id=<user>;Password=<pwd>;TrustServerCertificate=True;'
export ConnectionStrings__SuperBI='<同上>'
export Qdrant__Host='<qdrant-host>'
export Qwen__ApiKey='<key>'
export Embedding__ApiKey='<key>'

# 2) 建库表结构（二选一）
#   2a) 有 .NET SDK：用 EF 迁移
dotnet ef database update --project SuperBuilder_AI/SuperBuilder_AI.csproj
#   2b) 无 SDK（离线/受限环境）：应用幂等全量脚本
#       scripts/dr-backup/restore-schema-from-migrations.sql（含全量建表 + 迁移历史）

# 3) 启动应用
ASPNETCORE_ENVIRONMENT=Production dotnet SuperBuilder_AI.dll
```

### 校验（三步，必须全绿）

| # | 检查 | 期望 |
|---|---|---|
| 1 | `curl -s http://<host>:<port>/health` | `status=healthy`、`state=Ready` |
| 2 | 迁移一致性 | `schemaVersion` 等于 `scripts/schema/schema-version.json` 的 `schemaVersion`；见 §4 |
| 3 | 登录一次 | 用平台管理员登录成功，且 `/metrics` 可访问（需 `platform:diagnostics:view`） |

### 启动时自动执行的种子（5 步，固定顺序，幂等）

1. Identity/Permission 目录
2. UiLanguage/Text 本地化
3. 默认配额 + 内置主题
4. 租户界面语言关系
5. 平台管理员引导（已存在则不重复创建）

> 任一步失败 → `SeedIncomplete`（`/health` 返回 `degraded` + `reason`），**Web 仍可启动**，不崩溃。修复后重启即自动补齐。

### 初始化平台管理员

- 若 `PlatformBootstrap:AllowAnonymous=true`：`GET api/platform-bootstrap/status` 查看状态，`POST api/platform-bootstrap` 创建初始平台管理员。
- **生产建议关闭匿名**：置 `AllowAnonymous=false`，改由部署配置提供 `Username`/`Password`。

## 4. 升级（版本 → 版本）

### 4.1 升级前

1. **备份**：按 `05-backup-restore.md` 做关系库 + Qdrant 备份（推荐用 `scripts/dr-backup/backup-all.ps1`）。
2. **比对 schema 版本**：
   ```powershell
   # 离线：迁移程序集 vs 清单
   .\scripts\schema\verify-schema.ps1 -ProjectDir .\SuperBuilder_AI
   # 在线：目标库已应用 vs 清单
   .\scripts\schema\verify-schema.ps1 -ConnectionString $env:SB_DB_CS
   ```
   退出码 `0`=一致；`2`=发现漂移（**停止升级**，先查 §4.3）。
3. 记下当前 `schemaVersion`（`SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC`）。

### 4.2 升级步骤

```bash
# 1) 停应用（停机窗口按 【待实测回填 —— DR-01 实测还原 ~0.1s 级；整体窗口含备份+迁移】）
# 2) 应用迁移（仅增量，幂等）
dotnet ef database update --project SuperBuilder_AI/SuperBuilder_AI.csproj
#    或离线：dotnet ef migrations script <当前版本> <目标版本> --idempotent 后执行
# 3) 部署新版本程序集并启动
# 4) 校验 /health = healthy、state = Ready
# 5) 登录 + 一次 Ask 冒烟
```

### 4.3 升级判定与漂移处置

| 现象 | 判定 | 处置 |
|---|---|---|
| 库 `schemaVersion` < 清单 | 需升级 | 执行 §4.2 |
| 库 `schemaVersion` = 清单，集合一致 | 一致 | 无需动作 |
| 库缺少清单中的某迁移 | **漂移** | 补应用该迁移；查是否漏部署 |
| 库存在清单外的迁移 | **漂移** | 停止，确认是否非受控变更（**不得**直接删除历史记录） |
| `/health` = `degraded` 且 `state=MigrationsPending` | 有待应用迁移 | 执行 §4.2；系统据此判 readiness=false（DB-01） |

### 4.4 回退

见 `scripts/schema/rollback-one-step.sql`（单步回退样例）与 `docs/ops/migration-seed-schemaversion.md` §5.2。
**回退前必须已备份**；涉及列收窄的回退（如 `nvarchar(max)` → `nvarchar(2048)`）若存在超长数据会失败，属预期。

## 5. 安装完成检查清单

- [ ] `/health` = `healthy` / `Ready`
- [ ] `schemaVersion` 与清单一致
- [ ] 平台管理员可登录
- [ ] 至少接入 1 个数据源且「连接测试」通过
- [ ] 元数据扫描成功（`tableCount > 0`）
- [ ] 完成一次 Ask 提问并保存一个成果
- [ ] `/metrics` 可访问（`platform:diagnostics:view`）
- [ ] 备份任务已按 `05` 配置并成功跑通一次

## 6. 【M14-07 验收证据】非开发人员安装 + 一次升级记录

| 项 | 记录 |
|---|---|
| 执行人（非开发角色） | 【待填写】 |
| 安装日期 / 耗时 | 【待填写】 |
| 安装受阻步骤与原因 | 【待填写】 |
| 升级前版本 → 升级后版本 | 【待填写】 |
| 升级日期 / 停机时长 | 【待填写】 |
| 升级受阻步骤与原因 | 【待填写】 |
| 结论（无外部求助完成 / 需协助点） | 【待填写】 |
| 证据（截图/日志） | 【待附】 |
