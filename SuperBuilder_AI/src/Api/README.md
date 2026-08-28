# Api / Presentation 层（SuperBuilder_AI.Api）

对外接口与组合根。**依赖 Application 层**（通过端口解析具体实现）。

包含：
- `Controllers/` — 生产/管理控制器：`HomeController`、`MetadataController`、`MetadataVectorController`（以及开发期冒烟 `QdrantController`）。
- `Diagnostics/` — 12 个诊断/测试/夹具控制器（`*DiagnosticsController`、`AITestController`、`*FixtureController`、`*VerificationController`、`*RuntimeController` 等）。这些**非生产代码**，应限制路由前缀 `evaluation/*`、`test/*`，并在 Release 构建中排除或移到 `tests/` 模块。
- `Program.cs` — 组合根（DI 注册），从 `SuperBuilder_AI/Program.cs` 迁移而来。
- `Views/`、`wwwroot/` — 现有 MVC 视图与静态资源。

> 完整目录树与文件映射见 `docs/ARCHITECTURE.md`。
