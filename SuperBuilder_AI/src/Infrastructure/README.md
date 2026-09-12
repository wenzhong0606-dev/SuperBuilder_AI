# Infrastructure 层（SuperBuilder_AI.Infrastructure）

外部 IO 与技术方案实现层。**依赖 Application（实现其端口）+ Domain**。

包含：
- `Persistence/` — `SuperBIContext`(EF Core DbContext)、`Data/Configurations/*`、`Migrations/*`、仓储实现（`IMetadataRepository` 等）。
- `Vector/` — `QdrantService`、`MetadataVectorService`/`MetadataVectorIndexService` 的 Qdrant 适配。
- `Llm/` — `QwenService`（SQL 生成）、`QwenEmbeddingService`、`FakeEmbeddingService`（CI 桩，仅 Development 注册）。
- `Database/` — `MySqlMetadataReader`（及未来的 `SqlServerMetadataReader`/`PostgreSqlMetadataReader`）、`DataSourceConnectionFactory`、`QueryExecutionService`、`Infrastructure/Database/*Dialect` 与 `SqlDialectResolver`。

注意：当前 `MySqlMetadataReader`、`DataSourceConnectionFactory`、`QueryExecutionService`、`QdrantService`、`QwenService`、`QwenEmbeddingService` 都错误地处位于 `Services/` 下，应迁移到本层对应子目录。

> 完整目录树与文件映射见 `docs/architecture/ARCHITECTURE.md`。
