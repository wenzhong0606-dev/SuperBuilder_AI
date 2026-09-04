# 迁移、种子与 Schema 版本管理

> 对应里程碑：**M0-05 受控 Migration 与启动顺序**（INIT-1）。
> 本文档为《Master Development Plan》的补充操作手册，描述迁移如何生成/应用、启动序列如何探测与降级、以及升级与回滚流程。

## 1. 策略总览

- **Code-First EF Core 迁移**：迁移文件位于 `SuperBuilder_AI/SuperBuilder_AI/src/Infrastructure/Persistence/Migrations/`，由 `SuperBIContext` 作为起点生成。
- **生产由「部署工具迁移」或「启动迁移」二选一**（M0-05 明确要求二选一且显式）：
  - **部署工具迁移（默认，推荐）**：CI / 部署脚本在启动 API 前执行 `dotnet ef database update`（见 `.github/workflows/dotnet-build.yml`）。API 运行时**不再**执行任何 `Database.Migrate()`，避免多实例并发迁移竞态。
  - **启动迁移（可选）**：设置 `Startup:MigrateOnStartup = true` 后，`Program.cs` 在 Schema 探测阶段代执行 `Database.MigrateAsync()`。仅适用于单实例或维护窗口场景。
- **Schema 版本表**：使用 EF 内置的 `__EFMigrationsHistory`，不自定义 SchemaVersion 表（保持与 `dotnet ef` 工具链一致）。

## 2. 迁移分类与约定

| 类型 | 特征 | 命名示例 | 说明 |
| --- | --- | --- | --- |
| 空迁移 | 无 `Up`/`Down` 模型变更，仅承载说明或后续步骤占位 | `AddUiLocalizationGovernance` | 用于「先占位、后补列」的跨阶段协调，明确标注意图，禁止悄悄塞入不相关变更 |
| 真实建表迁移 | 新增/修改表、列、索引、约束 | `CreateDB`、`SyncUiLocalizationModel` | 每个迁移只解决一个内聚主题，提交信息写明影响面 |
| 升级迁移 | 向前演进 schema | 递增时间戳前缀 | 必须同时提供可向下兼容或明确的回滚路径 |
| 回滚迁移 | 通过 `dotnet ef migrations remove` 或显式 `Down` 撤销 | — | 见第 4 节 |

生成命令（需先安装 `dotnet-ef` 10.x）：
```bash
dotnet tool install --global dotnet-ef --version 10.0.10
dotnet ef migrations add <Name> \
  --project SuperBuilder_AI/SuperBuilder_AI.csproj \
  --startup-project SuperBuilder_AI/SuperBuilder_AI.csproj
```

## 3. 启动序列与可诊断状态（M0-05 核心）

`Program.cs` 在构建后执行固定顺序的启动序列，**每一步独立异常隔离**，任一环节失败都不会导致进程崩溃：

1. **Schema**：`SchemaProbe.ProbeAsync` 区分三种状态：
   - `DatabaseUnreachable` —— 连接失败；
   - `SchemaNotCreated` —— 数据库可达但 `__EFMigrationsHistory` 为空/缺失（尚未迁移）；
   - `Ready` —— 迁移已应用。
2. **Identity / Permission**：`IIdentityService.SeedAsync`（幂等，创建 platform 租户、全局权限/角色与绑定）。
3. **UiLanguage / Text**：`ILocalizationSeedService.EnsureSeedAsync`（幂等，初始化语言目录与默认界面文案）。
4. **默认策略/主题**：`IQuotaService.EnsureSeededAsync`（平台默认配额，主题回退到内置默认）。
5. **Bootstrap**：`PlatformAdminBootstrapper.EnsureAsync`（仅在 `NeedsInitialization` 时创建首个平台管理员；`NeedsMigration` 时安全返回 false，**绝不抛出**）。

诊断状态写入单例 `StartupDiagnostics`，并通过以下端点暴露（均不向普通用户输出堆栈）：
- `GET /health`：就绪/种子不完整 → `status:"healthy"`；不可达/未建表 → `status:"degraded"` + `state` + `reason` + `steps`。
- `GET /api/platform-bootstrap/status`：返回 `status`（`Ready`/`NeedsInitialization`/`NeedsMigration`）、`required`、可空 `platformTenantId`；数据库不可达时返回 `503` 与可诊断 `ApiError`。

> 在 `Development` 之外，`UseExceptionHandler("/Home/Error")` 与 `UnifiedExceptionMiddleware` 已确保普通页面/接口不会向用户泄露堆栈。

## 4. 升级与回滚流程

**升级（新环境 / 版本演进）**
1. 备份数据库（含 `__EFMigrationsHistory`）。
2. 应用迁移：CI 执行 `dotnet ef database update`；或临时设 `Startup:MigrateOnStartup=true` 启动一次后改回。
3. 启动 API，观察 `GET /health` 返回 `status:"healthy"` 且 `steps` 包含 `Schema,Identity,Localization,Quota,Bootstrap`。

**回滚**
- **未污染环境（迁移已生成但未应用）**：`dotnet ef migrations remove` 删除最新未应用迁移。
- **已应用迁移需撤销**：新增一个反向迁移（`dotnet ef migrations add RollbackXxx` 显式在 `Down` 中撤销前序变更），再 `dotnet ef database update`；**禁止**直接对生产库手动 `DROP`。
- **数据不可丢失的回滚**：先 `dotnet ef database update <TargetMigration>` 回退 schema 到目标点，再用备份恢复数据，二者需在维护窗口内配合完成。

## 5. 常见故障对照

| 现象 | `/health` 状态 | 处理 |
| --- | --- | --- |
| 连接串错误 / 数据库宕机 | `degraded` + `DatabaseUnreachable` | 修正 `ConnectionStrings:DefaultConnection` 或恢复数据库 |
| 忘了跑迁移 | `degraded` + `SchemaNotCreated` | 执行 `dotnet ef database update` |
| 平台目录种子失败但表已建 | `degraded` + `SeedIncomplete` | 查看结构化日志（`Platform identity seed failed`），修复后重启 |
| 全新库且无初始化配置 | `healthy` + `steps` 缺 `Bootstrap` | 通过本机 `POST /api/platform-bootstrap` 完成一次性初始化 |
