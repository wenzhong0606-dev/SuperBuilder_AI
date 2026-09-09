# Api / Presentation 层（SuperBuilder_AI.Api）

对外接口与组合根。**依赖 Application 层**（通过端口解析具体实现）。

包含：
- `Controllers/` — 生产/管理控制器：`HomeController`、`MetadataController`、`MetadataVectorController`（以及开发期冒烟 `QdrantController`）。
- `Diagnostics/` — 诊断/测试/夹具控制器（`*DiagnosticsController`、`AITestController`、`*FixtureController`、`*VerificationController`、`*RuntimeController` 等）。这些**非生产代码**；路由前缀 `evaluation/*` 与 `test` 已在非 Development 环境下由 `ProductionEvaluationRouteConvention` 从 endpoint discovery 移除（M9-04 / SB-P0-09 诊断分区），Production 返回 404。Release 构建排除 / 移至 `tests/` 模块为后续可选增强。
- `Program.cs` — 组合根（DI 注册），从 `SuperBuilder_AI/Program.cs` 迁移而来。
- `Views/`、`wwwroot/` — 现有 MVC 视图与静态资源。

> 完整目录树与文件映射见 `docs/ARCHITECTURE.md`。
