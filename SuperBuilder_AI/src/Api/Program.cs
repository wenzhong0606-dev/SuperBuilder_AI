using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Infrastructure.Database;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Services.BI;
using SuperBuilder_AI.Services.BI.Dashboard;
using SuperBuilder_AI.Services.BI.Evaluation;
using SuperBuilder_AI.Services.BI.Planning;
using SuperBuilder_AI.Services;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Interfaces.BI.Evaluation;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.BI.Dashboard;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Interfaces.Database;
using SuperBuilder_AI.Interfaces.BI.Entity;
using SuperBuilder_AI.Infrastructure.Persistence;
using SuperBuilder_AI.Services.BI.Entity;
using SuperBuilder_AI.Interfaces.Platform;
using SuperBuilder_AI.Services.Platform;
using SuperBuilder_AI.Interfaces.Theme;
using SuperBuilder_AI.Services.Theming;
using SuperBuilder_AI.Interfaces.AppBuilder;
using SuperBuilder_AI.Services.AppBuilder;
using SuperBuilder_AI.Interfaces.Agent;
using SuperBuilder_AI.Services.Agent;
using SuperBuilder_AI.Interfaces.Agent.Runtime;
using SuperBuilder_AI.Services.Agent.Runtime;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Services.Identity;
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Services.Audit;
using SuperBuilder_AI.Interfaces.Localization;
using SuperBuilder_AI.Services.Localization;
using SuperBuilder_AI.Interfaces.Quota;
using SuperBuilder_AI.Services.Quota;
using SuperBuilder_AI.Api.Background;
using SuperBuilder_AI.Middleware;
using SuperBuilder_AI.Api.Diagnostics;
using SuperBuilder_AI.Interfaces.Seed;
using SuperBuilder_AI.Services.Seed;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// M0-01：支持本地未提交覆盖文件（appsettings.Local.json），用于存放开发/本地密钥，禁止提交。
// 该文件已被 .gitignore 忽略；生产环境应通过环境变量（如 ConnectionStrings__WmsMySql / Qwen__ApiKey）注入。
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
// AddJsonFile 是后加入的配置源；必须重新把环境变量和命令行放到最高优先级，
// 否则 appsettings.Local.json 会意外覆盖容器/生产环境注入的密钥与连接字符串。
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);

builder.Services.AddControllersWithViews(options =>
{
	// SB-P0-09: Golden/evaluation controllers are Development-only infrastructure.
	if (!builder.Environment.IsDevelopment())
		options.Conventions.Add(new SuperBuilder_AI.Api.Security.ProductionEvaluationRouteConvention());

	// AUTH-2 Defense-in-Depth：控制器层授权兜底。
	// 鉴权主力仍是 AuthMiddleware；本过滤器作为第二道防线，
	// 在中间件被绕过或白名单被误改时，仍然拒绝匿名访问 /api（标注 [AllowAnonymous] 的端点除外）。
	options.Filters.Add<SuperBuilder_AI.Api.Security.ApiAuthorizationFilter>();
});
builder.Services.AddHttpClient();

// M9-02：迁移命名空间已统一收敛为 SuperBuilder_AI.Infrastructure.Persistence.Migrations
// （原分散于 SuperBuilder_AI.Migrations 与 SuperBuilder_AI.src.Infrastructure.Persistence.Migrations 两种偏差，共 41 个文件）。
// EF 通过程序集扫描发现迁移，不再依赖固定命名空间前缀，物理位置与命名空间已一致。
builder.Services.AddDbContext<SuperBIContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<QdrantOptions>(builder.Configuration.GetSection("Qdrant"));
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection("Embedding"));

// M7-07：机密存储（AES-256-GCM 信封加密）。主密钥须来自环境变量 SecretStore__MasterKey 或 gitignored 的 appsettings.Local.json；
// 缺失即解析失败（fail-fast，M9-07）——绝不回退到代码内硬编码密钥。
// 注意：校验延迟到解析期执行，避免在 dotnet ef 生成迁移（不解析 ISecretStore）时误触发启动失败。
builder.Services.AddSingleton<SuperBuilder_AI.Infrastructure.Security.ISecretStore>(sp =>
{
    var masterKey = builder.Configuration["SecretStore:MasterKey"];
    if (string.IsNullOrWhiteSpace(masterKey))
        throw new InvalidOperationException(
            "缺少 SecretStore:MasterKey（base64 编码的 32 字节 AES-256 主密钥）；生产环境须通过环境变量 SecretStore__MasterKey 注入。");
    return new SuperBuilder_AI.Infrastructure.Security.AesGcmSecretStore(Convert.FromBase64String(masterKey));
});
// M2-06：自助注册配置（默认关闭，平台按环境开启；审批/验证码待裁决）。
builder.Services.Configure<SelfRegistrationOptions>(builder.Configuration.GetSection(SelfRegistrationOptions.SectionName));
builder.Services.Configure<CostGovernanceOptions>(builder.Configuration.GetSection(CostGovernanceOptions.SectionName));
builder.Services.AddScoped<IDataSourceMetadataReader, MySqlMetadataReader>();
builder.Services.AddScoped<PlatformAdminBootstrapper>();
builder.Services.AddScoped<MetadataScannerService>();
// M4-05：扫描任务队列与后台处理器（Channel + BackgroundService）。
builder.Services.AddSingleton<IMetadataScanQueue, MetadataScanQueue>();
builder.Services.AddHostedService<MetadataScanHostedService>();
builder.Services.AddScoped<MetadataSearchTextBuilder>();
builder.Services.AddScoped<IMetadataSearchTextBuilder>(sp => sp.GetRequiredService<MetadataSearchTextBuilder>());
builder.Services.AddScoped<MetadataPromptBuilder>();
builder.Services.AddScoped<MetadataContextBuilder>();
builder.Services.AddScoped<IMetadataContextBuilder>(sp => sp.GetRequiredService<MetadataContextBuilder>());
builder.Services.AddScoped<IQueryPlanContextBuilder, QueryPlanContextBuilder>();

// Phase 3.1 Entity semantic services: Metadata DB only; never connect to dynamic business DB.
builder.Services.AddScoped<IBusinessEntityService, BusinessEntityService>();
builder.Services.AddScoped<IPhysicalBindingResolver, PhysicalBindingResolver>();
builder.Services.AddScoped<IEntityQueryPlanMapper, EntityQueryPlanMapper>();

// P3 Business Semantic Model：业务实体注册表 + 语义映射（确定性匹配，不调 LLM）
builder.Services.AddScoped<IBusinessEntityRegistryService, BusinessEntityRegistryService>();
builder.Services.AddScoped<IBusinessSemanticMappingService, BusinessSemanticMappingService>();

// P3 批次2：业务域 / 维度仓储（A4 依赖倒置准备，仅操作 Metadata DB）
builder.Services.AddScoped<IBusinessEntityRepository, BusinessEntityRepository>();

// P4 Multi-Tenant Platform Core：平台上下文访问器（scoped）
// 由 Runtime 入口在每个请求作用域内写入；P4.3 的 SuperBIContext 全局租户过滤将读取它。
builder.Services.AddScoped<IPlatformContextAccessor, PlatformContextAccessor>();

// P5 Multi-Language Runtime：本地化服务（无状态确定性实现，注册为 singleton）
// 负责语言区域解析、标签回退链与本地化文案取值；不触碰查询链路。
builder.Services.AddSingleton<ILocalizationService, LocalizationService>();

// P5.2：业务语义多语言标签服务（依赖 SuperBIContext，随 DbContext 注册为 scoped）
builder.Services.AddScoped<ISemanticLabelService, SemanticLabelService>();

// P5.3：多语言标签召回服务（非默认语言时提升检索排序；默认语言路径完全不参与）
builder.Services.AddScoped<ISemanticLabelRecallService, SemanticLabelRecallService>();

if (string.Equals(
        Environment.GetEnvironmentVariable("CI"),
        "true",
        StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<IEmbeddingService, FakeEmbeddingService>();
}
else
{
    builder.Services.AddHttpClient<QwenEmbeddingService>();
    builder.Services.AddScoped<IEmbeddingService>(sp => sp.GetRequiredService<QwenEmbeddingService>());
}

builder.Services.AddScoped<QwenService>();
builder.Services.AddScoped<IQwenService>(sp => sp.GetRequiredService<QwenService>());
builder.Services.AddScoped<MetadataSemanticService>();
builder.Services.AddScoped<IMetadataSemanticService>(sp => sp.GetRequiredService<MetadataSemanticService>());
builder.Services.AddScoped<QdrantService>();
builder.Services.AddScoped<IQdrantService>(sp => sp.GetRequiredService<QdrantService>());
builder.Services.AddScoped<MetadataVectorService>();
builder.Services.AddScoped<IMetadataVectorService>(sp => sp.GetRequiredService<MetadataVectorService>());
builder.Services.AddScoped<MetadataVectorIndexService>();
builder.Services.AddScoped<IMetadataVectorIndexService>(sp => sp.GetRequiredService<MetadataVectorIndexService>());
builder.Services.AddScoped<MetadataCsvFixtureService>();
builder.Services.AddScoped<IMetadataCsvFixtureService>(sp => sp.GetRequiredService<MetadataCsvFixtureService>());
builder.Services.AddScoped<MetadataSemanticSearchService>();
builder.Services.AddScoped<IMetadataSemanticSearchService>(sp => sp.GetRequiredService<MetadataSemanticSearchService>());
builder.Services.AddScoped<SuperBuilder_AI.Interfaces.BI.IDimensionResolutionEvidenceService, DimensionResolutionEvidenceService>();
builder.Services.AddScoped<QueryIntentNormalizer>();
builder.Services.AddScoped<QueryUnderstandingService>();
builder.Services.AddSingleton<SuperBuilder_AI.Services.BI.IAskConversationService, SuperBuilder_AI.Services.BI.AskConversationService>();
builder.Services.AddScoped<IQueryUnderstandingService>(sp => sp.GetRequiredService<QueryUnderstandingService>());
builder.Services.AddScoped<QueryJoinInferenceService>();
builder.Services.AddScoped<IQueryJoinInferenceService>(sp => sp.GetRequiredService<QueryJoinInferenceService>());
builder.Services.AddScoped<QueryPlanValidator>();
builder.Services.AddScoped<QueryPlanDataSourceScope>();
builder.Services.AddScoped<IQueryPlanDataSourceScope>(sp => sp.GetRequiredService<QueryPlanDataSourceScope>());
builder.Services.AddScoped<QueryPlanBuilder>();
builder.Services.AddScoped<IQueryPlanBuilder>(sp => sp.GetRequiredService<QueryPlanBuilder>());

// M5-03：QueryPlanPipeline 阶段化——管线本身 + 有序阶段注册
builder.Services.AddScoped<QueryPlanPipeline>();
builder.Services.AddScoped<IQueryPlanPipeline>(sp => sp.GetRequiredService<QueryPlanPipeline>());

// M5-09：AI Decision Audit——决策全过程审计 Sink
// 默认 Mode=None → NoOp，零行为变更；配置切 Log 启用结构化日志输出（对 Golden 免疫）。
builder.Services.Configure<DecisionAuditOptions>(builder.Configuration.GetSection(DecisionAuditOptions.SectionName));
builder.Services.AddScoped<IDecisionAuditSink, ConfigurableDecisionAuditSink>();

// M6-05：Ask 审计——会话/轮次/问题/授权源/Decision/SQL/耗时/状态审计 Sink
// 默认 Mode=None → NoOp，零行为变更；配置切 Log 启用结构化日志输出（对 Golden 免疫）。
builder.Services.Configure<AskAuditOptions>(builder.Configuration.GetSection(AskAuditOptions.SectionName));
builder.Services.AddScoped<IAskAuditSink, ConfigurableAskAuditSink>();

// M5-10：Production Feedback 闭环（默认 Mode=Off → NoOp，零行为变更；对 Golden 免疫）
// 通过 IFeedbackBaselineGateway(默认 NoOp) 解耦既有 Golden Baseline 服务，闭环不触达真实回归。
builder.Services.Configure<ProductionFeedbackOptions>(builder.Configuration.GetSection(ProductionFeedbackOptions.SectionName));
builder.Services.AddScoped<IProductionFeedbackStore, InMemoryProductionFeedbackStore>();
builder.Services.AddScoped<IFeedbackBaselineGateway, NoOpFeedbackBaselineGateway>();
builder.Services.AddScoped<IProductionFeedbackSink, ConfigurableProductionFeedbackSink>();
builder.Services.AddScoped<ProductionFeedbackLoop>();

// 阶段按执行顺序注册；MS DI 解析 IEnumerable<IQueryPlanStage> 时保持注册顺序：
// Build → Context → ColumnSecurity → MetadataIntegrity → DetailProjection →
// SemanticValidation → Confidence → DecisionGate → CostGovernance → Explainability
builder.Services.AddScoped<IQueryPlanStage, QueryPlanBuildStage>();
builder.Services.AddScoped<IQueryPlanStage, QueryPlanContextStage>();
// M5-05 + M5-08：列级安全净化（Context 之后、MetadataIntegrity 之前）
// M5-08：可配置分类器（默认 DenyNothing → 零行为变更；配置切 PolicyDriven 启用真实治理）
builder.Services.Configure<ColumnSecurityOptions>(builder.Configuration.GetSection(ColumnSecurityOptions.SectionName));
builder.Services.AddScoped<DenyNothingColumnClassifier>();
builder.Services.AddScoped<PolicyDrivenColumnClassifier>();
builder.Services.AddScoped<IColumnSensitivityClassifier, ConfigurableColumnClassifier>();
builder.Services.AddScoped<IColumnSecurityPolicy, ColumnSecurityPolicy>();
builder.Services.AddScoped<IColumnSecurityContextResolver, DefaultColumnSecurityContextResolver>();
builder.Services.AddScoped<IQueryPlanStage, QueryPlanColumnSecurityStage>();
// M5-01：规范化语义模型解析器（默认透传，零行为变更；M5-02/M5-08 将接入真实实现）
builder.Services.AddScoped<ICanonicalSemanticResolver, PassThroughCanonicalResolver>();
builder.Services.AddScoped<IQueryPlanStage, QueryPlanMetadataIntegrityStage>();
builder.Services.AddScoped<IQueryPlanStage, QueryPlanDetailProjectionStage>();
builder.Services.AddScoped<IQueryPlanStage, QueryPlanSemanticValidationStage>();
builder.Services.AddScoped<IQueryPlanStage, QueryPlanConfidenceStage>();
builder.Services.AddScoped<IQueryPlanStage, QueryPlanDecisionGateStage>();
// M5-06：查询成本治理（DecisionGate 之后、Explainability 之前）
builder.Services.AddScoped<IQueryCostClassifier, StructuralQueryCostClassifier>();
builder.Services.AddScoped<ICostGovernancePolicy, ThresholdCostGovernancePolicy>();
builder.Services.AddScoped<ICostGovernanceContextResolver, DefaultCostGovernanceContextResolver>();
// M5-08：成本治理灰度主开关 + 模型成本遥测（默认 Off / None → 零行为变更）
builder.Services.Configure<CostGovernanceOptions>(builder.Configuration.GetSection(CostGovernanceOptions.SectionName));
builder.Services.AddScoped<NoOpModelCostTelemetry>();
builder.Services.AddScoped<LogModelCostTelemetry>();
builder.Services.AddScoped<IModelCostTelemetry, ConfigurableModelCostTelemetry>();
builder.Services.AddScoped<IQueryPlanStage, QueryPlanCostGovernanceStage>();
builder.Services.AddScoped<IQueryPlanStage, QueryPlanExplainabilityStage>();
builder.Services.AddScoped<QuerySemanticValidator>();
builder.Services.AddScoped<IQueryPlanRepairService, QueryPlanRepairService>();
builder.Services.AddScoped<IQueryPlanValidationPipeline, QueryPlanValidationPipeline>();
builder.Services.AddScoped<QueryPlanEvaluationGate>();
builder.Services.AddScoped<QueryPlanJoinScoringService>();
builder.Services.AddScoped<QueryPlanMetricScoringService>();
builder.Services.AddScoped<QueryPlanDimensionScoringService>();
builder.Services.AddScoped<QueryPlanFilterScoringService>();
builder.Services.AddScoped<QueryPlanQueryShapeScoringService>();
builder.Services.AddScoped<QueryPlanEvaluationScoringService>();
builder.Services.AddScoped<QueryPlanEvaluator>();
builder.Services.AddScoped<QueryPlanEvaluationConfidenceEvidenceAdapter>();
builder.Services.AddScoped<QueryPlanEvaluationConfidenceService>();
builder.Services.AddScoped<IQueryPlanConfidenceService, QueryPlanConfidenceService>();
builder.Services.AddScoped<IQueryPlanDecisionGate, QueryPlanDecisionGate>();
builder.Services.AddScoped<QueryPlanExplainabilityService>();
builder.Services.AddScoped<GoldenQueryDatasetSerializer>();
builder.Services.AddScoped<SemanticApplicabilityEvaluator>();
builder.Services.AddScoped<GoldenConfidenceCalibrationEvaluator>();
builder.Services.AddScoped<GoldenConfidenceCalibrationRunner>();
builder.Services.AddScoped<GoldenDatasetCoverageAnalyzer>();
builder.Services.AddScoped<GoldenDatasetQualityGate>();
builder.Services.AddScoped<GoldenBaselineReleaseService>();
builder.Services.AddSingleton<IGoldenBaselineRegistry, GoldenBaselineRegistry>();
builder.Services.AddSingleton<IGoldenBaselinePersistence, InMemoryGoldenBaselinePersistence>();
builder.Services.AddScoped<GoldenBaselinePersistenceService>();
builder.Services.AddScoped<GoldenBaselineLifecycleValidator>();
builder.Services.AddScoped<GoldenDatasetRunner>();
builder.Services.AddScoped<GoldenDatasetRegressionEvaluator>();
builder.Services.AddScoped<GoldenDatasetRuntimeService>();
builder.Services.AddScoped<IDataSourceConnectionFactory, DataSourceConnectionFactory>();
builder.Services.AddScoped<ISqlQueryBuilder, SqlQueryBuilder>();
builder.Services.AddScoped<IQueryExecutionService, QueryExecutionService>();
builder.Services.AddScoped<ISqlDialect, SqlServerDialect>();
builder.Services.AddScoped<ISqlDialect, MySqlDialect>();
builder.Services.AddScoped<ISqlDialect, PostgreSqlDialect>();
builder.Services.AddScoped<SqlDialectResolver>();
builder.Services.AddScoped<ISqlDialectResolver>(sp => sp.GetRequiredService<SqlDialectResolver>());

// ── BI 查询主链路编排服务 (Phase 1 核心) ──────────────────
builder.Services.AddScoped<BIConversationService>();
builder.Services.AddScoped<IBIConversationService>(sp => sp.GetRequiredService<BIConversationService>());

// M7-11：Ask 查询快照存储（可选依赖；未注册时 BIConversationService 跳过写入，零回归）
builder.Services.AddScoped<IAskQuerySnapshotStore, AskQuerySnapshotStore>();

// M7-11 C1：Ask 快照 → 应用取数绑定导出器（from-ask）
builder.Services.AddScoped<IAppQueryBindingExporter, AppQueryBindingExporter>();

// M7-11 C2：确定性取数执行器（绕过 NLU，复用当前访问者安全链路；依赖必需，缺依赖即拒）
builder.Services.AddScoped<IAppQueryExecutor, AppQueryExecutor>();

// P6.1/P6.2 DSL 序列化与校验端口
builder.Services.AddScoped<IDashboardDslSerializer, DashboardDslSerializer>();

// P8.1 App DSL 序列化与校验端口
builder.Services.AddScoped<IAppDslSerializer, AppDslSerializer>();

// P9.1 Agent DSL 序列化与校验端口
builder.Services.AddScoped<IAgentDslSerializer, AgentDslSerializer>();

// P8.2 App 编排端口（默认路径确定性、非默认路径启用 LLM）
builder.Services.AddScoped<IAppBuilderAgent, AppBuilderAgent>();

// P9.2 Agent 编排端口（默认路径确定性、非默认路径启用 LLM）
builder.Services.AddScoped<IAgentPlanner, AgentPlanner>();

// M7-03 Agent 运行时（受控工具执行 + 权限 deny-by-default + 状态机 + 重试 + 审批闸门）
builder.Services.AddScoped<IToolPermissionPolicy, ToolPermissionPolicy>();
builder.Services.AddScoped<IRetryPolicy, RetryPolicy>();
// M7-04：确定性、只读、无 LLM 的 Safe 工具接真实后端（live）；其余 Read/Write 工具仍为诚实受控信封（pending）。
builder.Services.AddScoped<ITool, LiveMetadataTool>();
builder.Services.AddScoped<ITool, LiveSemanticTool>();
builder.Services.AddScoped<ITool, QueryTool>();
builder.Services.AddScoped<ITool, DashboardTool>();
builder.Services.AddScoped<ITool, ForecastTool>();
builder.Services.AddScoped<ITool, ReportTool>();
builder.Services.AddScoped<ITool, AlertTool>();
builder.Services.AddScoped<ITool, WorkflowTool>();
builder.Services.AddScoped<IToolCatalog, ControlledToolCatalog>();
builder.Services.AddScoped<IAgentRuntime, AgentRuntime>();

// P10.1 Identity / RBAC（确定性，不调 LLM）
builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddScoped<ITenantMembershipService, TenantMembershipService>();
builder.Services.AddScoped<ISelfRegistrationService, SelfRegistrationService>();
builder.Services.AddScoped<IDemoDataInstaller, DemoDataInstaller>();
builder.Services.AddScoped<IPlatformAdminService, PlatformAdminService>();
builder.Services.AddScoped<IPlatformAdminScopeService, PlatformAdminScopeService>();
builder.Services.AddScoped<IDataSourceAuthorizationService, DataSourceAuthorizationService>();
builder.Services.AddScoped<IDataSourceExecutionIdentityAccessor, DataSourceExecutionIdentityAccessor>();
builder.Services.AddScoped<IRowLevelSecurityService, RowLevelSecurityService>();
builder.Services.AddScoped<IQueryPlanSecurityGate, QueryPlanSecurityGate>();

// P10.3 Audit Log（确定性，不调 LLM）
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

// P10.4 Quota / Billing（确定性，不调 LLM）
builder.Services.AddScoped<IQuotaService, QuotaService>();

// P6.3 LowcodeRenderer 渲染引擎（对照 QueryPlanPipeline 接入取数）
builder.Services.AddScoped<IDashboardRenderer, DashboardLowcodeRenderer>();
builder.Services.AddScoped<IWidgetDataResolver, QueryPlanWidgetDataResolver>();

// P7.2 Theme 级联解析端口（仪表盘显式键 → 租户默认 → 内置默认）
builder.Services.AddScoped<IThemeResolver, ThemeResolver>();

// M7-07：模型账号（BYO）服务——加密绑定 / 租户隔离 / 服务端解密（绝不下发明文）。
builder.Services.AddScoped<SuperBuilder_AI.Application.ModelAccounts.ModelAccountService>();

// M7-09：租户自定义组件 DSL 白名单与安全校验。
builder.Services.AddScoped<SuperBuilder_AI.Services.Components.CustomComponentDslSerializer>();

// ── BIConversationService 依赖链补充注册 ──────────────────
builder.Services.AddScoped<IQueryPlanExplainabilityService>(sp => sp.GetRequiredService<QueryPlanExplainabilityService>());
builder.Services.AddScoped<IMetadataPromptBuilder>(sp => sp.GetRequiredService<MetadataPromptBuilder>());
builder.Services.AddScoped<ResultUnderstandingService>();
builder.Services.AddScoped<IResultUnderstandingService>(sp => sp.GetRequiredService<ResultUnderstandingService>());
builder.Services.AddScoped<QueryPlanMetadataValidator>();
builder.Services.AddScoped<MetadataSearchService>();
builder.Services.AddScoped<IMetadataSearchService>(sp => sp.GetRequiredService<MetadataSearchService>());
builder.Services.AddScoped<GoldenBaselineComparisonService>();

// SB-P0-03：所有环境均须显式配置 SigningKey；非开发环境额外执行强度校验并 fail-fast。
var authSigningKey = SuperBuilder_AI.Services.Auth.AuthSigningKeyPolicy.Validate(
	builder.Configuration["Auth:SigningKey"],
	builder.Environment.IsDevelopment());
builder.Services.AddSingleton<SuperBuilder_AI.Services.Auth.ITokenService>(
	new SuperBuilder_AI.Services.Auth.TokenService(authSigningKey));
builder.Services.AddSingleton<SuperBuilder_AI.Services.Auth.IPasswordHasher>(
	new SuperBuilder_AI.Services.Auth.PasswordHasher());

// SB-P0-08：开放跨域仅允许 Development 显式启用；其他环境必须提供有限白名单并 fail-fast。
var corsPolicy = SuperBuilder_AI.Api.Security.CorsOriginPolicy.Resolve(
	builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>(),
	builder.Configuration.GetValue<bool>("Cors:AllowAnyOrigin"),
	builder.Environment.IsDevelopment());
builder.Services.AddCors(o => o.AddPolicy("P11Cors", p =>
{
	if (corsPolicy.AllowAnyOrigin)
		p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
	else
		p.WithOrigins(corsPolicy.AllowedOrigins.ToArray()).AllowAnyHeader().AllowAnyMethod();
}));

// P11.5.1 性能/成本轨：Ask 语义响应缓存（租户 + 归一化问题为键，TTL 默认 60s，LRU 淘汰）
// 只作用于 POST api/ask；Golden 回归走独立 evaluation/golden-runtime 端点，不经过本缓存。
var askCacheSection = builder.Configuration.GetSection(SuperBuilder_AI.Api.Caching.AskCacheOptions.SectionName);
builder.Services.Configure<SuperBuilder_AI.Api.Caching.AskCacheOptions>(askCacheSection);
var askCacheMax = askCacheSection.GetValue<int?>(nameof(SuperBuilder_AI.Api.Caching.AskCacheOptions.MaxEntries)) ?? 200;
builder.Services.AddMemoryCache(o =>
{
	if (askCacheMax > 0) o.SizeLimit = askCacheMax;
});
builder.Services.AddSingleton<SuperBuilder_AI.Api.Caching.IAskResponseCache,
	SuperBuilder_AI.Api.Caching.MemoryAskResponseCache>();

// M6-04 Cache 完整版本：把撤权/策略/语言/模型/语义/元数据/数据源集合 7 维版本折叠进 api/ask 缓存键。
// 各维度提供器可空且内部 try/catch 降级，缺失即该维度置 na/legacy，绝不影响主查询链路。
builder.Services.Configure<SuperBuilder_AI.Application.Common.Options.QwenOptions>(
	builder.Configuration.GetSection(SuperBuilder_AI.Application.Common.Options.QwenOptions.SectionName));
builder.Services.AddScoped<SuperBuilder_AI.Interfaces.BI.IMetadataVersionProvider,
	SuperBuilder_AI.Services.BI.MetadataVersionProvider>();
builder.Services.AddScoped<SuperBuilder_AI.Interfaces.BI.ISemanticVersionProvider,
	SuperBuilder_AI.Services.BI.SemanticVersionProvider>();
builder.Services.AddScoped<SuperBuilder_AI.Interfaces.BI.IDataSourceCatalogVersionProvider,
	SuperBuilder_AI.Services.BI.DataSourceCatalogVersionProvider>();
builder.Services.AddScoped<SuperBuilder_AI.Interfaces.BI.IAskCacheVersionProvider,
	SuperBuilder_AI.Services.BI.CompositeAskCacheVersionProvider>();

// P11.5.2 安全/运维轨：请求指标采集器（请求数 / 错误数 / P95 延迟，按路由聚合）
builder.Services.AddSingleton<SuperBuilder_AI.Middleware.RequestMetricsCollector>();
// M9-05：同一单例同时注册为端口抽象，使 Application 层经接口写入同一份内存指标（不反向依赖 Middleware）。
builder.Services.AddSingleton<SuperBuilder_AI.Interfaces.BI.IPipelineMetricsSink>(sp =>
	sp.GetRequiredService<SuperBuilder_AI.Middleware.RequestMetricsCollector>());

// M0-05：受控启动诊断单例（供 /health 与初始化端点读取，绝不向普通用户输出堆栈）
builder.Services.AddSingleton<StartupDiagnostics>();
// M0-05：本地化目录种子服务，使 UiLanguage/Text 在启动序列中固定顺序执行
builder.Services.AddScoped<ILocalizationSeedService, LocalizationSeedService>();
builder.Services.AddScoped<IThemeSeedService, ThemeSeedService>();
// M3-G0：用户级界面语言偏好服务（按租户维度持久化，写入校验可用语言范围）
builder.Services.AddScoped<IUserLanguagePreferenceService, UserLanguagePreferenceService>();
// M3-01：租户界面语言关系服务（替代 localization JSON，约束在事务内强制）
builder.Services.AddScoped<ITenantLanguageService, TenantLanguageService>();
builder.Services.AddScoped<IPlatformLanguageService, PlatformLanguageService>();
// M0-08：限流阈值（绑定配置节 "RateLimit"，缺省使用安全默认值）
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection("RateLimit"));
// RL-1/RL-2：限流存储。
// 默认内存实现（MemoryRateLimitStore）会定期逐出过期窗口，杜绝原 static 字典的内存泄漏；
// 但进程内存仅单实例有效——多实例（负载均衡）部署下各实例独立计数，攻击者可分散绕过。
// 故提供 Redis 后端（RedisRateLimitStore）：配置 RateLimit:Store:Type=Redis 即切换为全局共享计数，
// 满足「上线多实例前」的去中心化前置条件。默认仍为 Memory，单实例无需 Redis 依赖。
var rateLimitStoreType = builder.Configuration["RateLimit:Store:Type"] ?? "Memory";
if (string.Equals(rateLimitStoreType, "Redis", StringComparison.OrdinalIgnoreCase))
{
	var redisConfig = builder.Configuration["RateLimit:Redis:Configuration"]
		?? throw new InvalidOperationException(
			"RateLimit:Store:Type 已设为 Redis，但未配置 RateLimit:Redis:Configuration（Redis 连接字符串）。");
	var redisInstanceName = builder.Configuration["RateLimit:Redis:InstanceName"] ?? "rls:";
	var multiplexer = StackExchange.Redis.ConnectionMultiplexer.Connect(redisConfig);
	builder.Services.AddSingleton(multiplexer);
	builder.Services.AddSingleton<SuperBuilder_AI.Middleware.IRateLimitStore>(
		_ => new SuperBuilder_AI.Middleware.RedisRateLimitStore(multiplexer, redisInstanceName));
}
else
{
	builder.Services.AddSingleton<SuperBuilder_AI.Middleware.IRateLimitStore>(
		_ => new SuperBuilder_AI.Middleware.MemoryRateLimitStore());
}

// M9-07：统一 Dev/Test/Production 配置校验。
// 非开发环境缺失关键配置（主库连接串 / LLM·Embedding 密钥 / 向量库端点）一律 fail-fast 拒绝启动；
// 开发/测试环境降级为告警，不阻断启动。校验信息仅含配置键路径，绝不输出密钥值（防泄密）。
var configReport = new SuperBuilder_AI.Application.Common.Configuration.StartupConfigurationValidator()
	.Validate(builder.Configuration, builder.Environment);
foreach (var w in configReport.Warnings)
	Console.Error.WriteLine($"[配置告警] {w.Key}: {w.Message}");
if (configReport.HasErrors)
{
	var detail = string.Join(
		Environment.NewLine,
		configReport.Errors.Select(e => $"  - {e.Key}: {e.Message}"));
	throw new InvalidOperationException(
		"启动配置校验失败：生产/预发环境缺失关键配置，已拒绝启动以防带不安全默认值运行。" + Environment.NewLine + detail);
}

var app = builder.Build();

// M0-08：反向代理可信列表（仅信任明确配置的代理；默认 KnownProxies/KnownNetworks 为空，
// 即不消费任何 X-Forwarded-*，RemoteIpAddress 直接为连接对端地址，避免伪造客户端 IP）
var forwardOpts = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
};
foreach (var p in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
    if (IPAddress.TryParse(p, out var ip)) forwardOpts.KnownProxies.Add(ip);
foreach (var n in builder.Configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? Array.Empty<string>())
    if (System.Net.IPNetwork.TryParse(n, out var net)) forwardOpts.KnownIPNetworks.Add(net);
app.UseForwardedHeaders(forwardOpts);

// ── M0-05：受控启动序列 ────────────────────────────────────────────────
// 固定顺序：Schema → Identity/Permission → UiLanguage/Text → 默认策略/主题 → Bootstrap。
// 每一步独立异常隔离（不抛到宿主进程），并记录结构化日志与可诊断状态；
// 数据库不可达 / Schema 未创建 / 种子不完整均被区分，Web 仍可启动到受限诊断模式。
var diagnostics = app.Services.GetRequiredService<StartupDiagnostics>();
var migrateOnStartup = builder.Configuration.GetValue<bool>("Startup:MigrateOnStartup");

using (var startupScope = app.Services.CreateScope())
{
    var db = startupScope.ServiceProvider.GetRequiredService<SuperBIContext>();
    var logger = app.Logger;

    // 步骤 1：Schema / 数据库可达性（可区分 DB 不可达与 Schema 未创建）
    var schema = await SchemaProbe.ProbeAsync(db, migrateOnStartup);
    if (schema.State != BootstrapState.Ready)
    {
        diagnostics.State = schema.State;
        diagnostics.Reason = schema.Reason;
        logger.LogError("Startup schema probe failed: {State} - {Reason}", schema.State, schema.Reason);
    }
    else
    {
        diagnostics.State = BootstrapState.Ready;
        diagnostics.MarkStep("Schema");

        // 步骤 2：Identity / Permission 全局目录种子（幂等）
        try
        {
            var identity = startupScope.ServiceProvider.GetRequiredService<IIdentityService>();
            await identity.SeedAsync();
            diagnostics.MarkStep("Identity");
            logger.LogInformation("Platform identity catalog seeded.");
        }
        catch (Exception seedEx)
        {
            diagnostics.State = BootstrapState.SeedIncomplete;
            diagnostics.Reason = $"平台目录种子失败：{seedEx.Message}";
            logger.LogError(seedEx, "Platform identity seed failed.");
        }

        // 步骤 3：UiLanguage / Text 本地化目录种子（幂等）
        try
        {
            var localizationSeed = startupScope.ServiceProvider.GetRequiredService<ILocalizationSeedService>();
            await localizationSeed.EnsureSeedAsync();
            diagnostics.MarkStep("Localization");
            logger.LogInformation("Localization catalog seeded.");
        }
        catch (Exception seedEx)
        {
            diagnostics.State = BootstrapState.SeedIncomplete;
            diagnostics.Reason = $"本地化种子失败：{seedEx.Message}";
            logger.LogError(seedEx, "Localization seed failed.");
        }

        // 步骤 4：默认策略/主题（Quota 平台默认配额 + 内置默认主题，均幂等）
        try
        {
            var quota = startupScope.ServiceProvider.GetRequiredService<IQuotaService>();
            await quota.EnsureSeededAsync();

            var themeSeed = startupScope.ServiceProvider.GetRequiredService<IThemeSeedService>();
            await themeSeed.EnsureSeededAsync();

            diagnostics.MarkStep("Quota");
            logger.LogInformation("Platform default quota & built-in theme seeded.");
        }
        catch (Exception seedEx)
        {
            diagnostics.State = BootstrapState.SeedIncomplete;
            diagnostics.Reason = $"默认配额/主题种子失败：{seedEx.Message}";
            logger.LogError(seedEx, "Quota/Theme seed failed.");
        }

        // 步骤 4.5：租户界面语言关系播种（M3-01；幂等）。
        // 按 localization:availableCultures/defaultCulture JSON 或平台默认 zh-CN 迁移，
        // 确保每租户至少一种启用语言且恰一个默认语言。须在平台默认主题之后、Bootstrap 之前。
        try
        {
            var tenantLang = startupScope.ServiceProvider.GetRequiredService<ITenantLanguageService>();
            await tenantLang.EnsureAllTenantsLanguagesAsync();
            diagnostics.MarkStep("TenantLanguages");
            logger.LogInformation("Tenant UI language relationships seeded.");
        }
        catch (Exception seedEx)
        {
            diagnostics.State = BootstrapState.SeedIncomplete;
            diagnostics.Reason = $"租户语言关系播种失败：{seedEx.Message}";
            logger.LogError(seedEx, "Tenant languages seed failed.");
        }

        // 步骤 5：平台管理员引导（幂等；缺 Schema/目录时安全返回，不抛异常）
        try
        {
            var bootstrapper = startupScope.ServiceProvider.GetRequiredService<PlatformAdminBootstrapper>();
            var created = await bootstrapper.EnsureAsync();
            diagnostics.MarkStep("Bootstrap");
            if (created) logger.LogInformation("First platform administrator created.");
        }
        catch (Exception seedEx)
        {
            diagnostics.State = BootstrapState.SeedIncomplete;
            diagnostics.Reason = $"平台管理员引导失败：{seedEx.Message}";
            logger.LogError(seedEx, "Platform bootstrap failed.");
        }
    }
}

if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
// 本地 Blazor Server 与 API 是两个独立进程；Development 直连 HTTP 避免开发证书/
// Schannel 故障被前端误表现为“空数据”。非开发环境仍强制 HTTPS。
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseRouting();
// P11 错误治理：统一异常 → 结构化友好 JSON（错误码 + 关联ID），须位于路由之后、端点之前，包裹后续所有中间件
app.UseMiddleware<UnifiedExceptionMiddleware>();
// M1-01：乐观并发冲突 → HTTP 409（须位于 UnifiedExceptionMiddleware 之后/更内层，优先捕获 DbUpdateConcurrencyException）
app.UseMiddleware<ConcurrencyExceptionMiddleware>();
// P0-10：审计必须包裹鉴权/限流/端点，确保入口拒绝与异常拒绝均入账。
app.UseMiddleware<AuditMiddleware>();
// P11.0 安全轨道：CORS → 鉴权 → 限流（顺序：路由之后、授权之前；与 Observability/Audit 互不干扰）
app.UseCors("P11Cors");
// M0-08：鉴权先于限流，使限流键可基于已认证身份（TenantId+UserId）而非可伪造令牌头
app.UseMiddleware<AuthMiddleware>();
app.UseMiddleware<RateLimitMiddleware>();
app.UseAuthorization();
// P10.5 可观测性中间件（关联ID透传 + 请求/响应日志 + 耗时，非阻塞、异常静默，不影响 Golden 行为契约）
app.UseMiddleware<ObservabilityMiddleware>();
app.MapStaticAssets();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();
// P11.0 健康探测（匿名白名单，供运维/可观测面使用）
// M0-05：在受限诊断模式下仍返回 200，但通过 state/reason 暴露可诊断状态，避免启动崩溃或堆栈泄漏。
app.MapGet("/health", (StartupDiagnostics d) => new
{
    status = d.State is BootstrapState.Ready or BootstrapState.SeedIncomplete ? "healthy" : "degraded",
    state = d.State.ToString(),
    reason = d.Reason,
    steps = d.CompletedSteps,
    ts = System.DateTime.UtcNow,
});
// SB-P0-09 请求指标端点：由 AuthMiddleware 强制 platform:diagnostics:view 权限。
app.MapGet("/metrics", (SuperBuilder_AI.Middleware.RequestMetricsCollector metrics,
		SuperBuilder_AI.Api.Caching.IAskResponseCache cache) =>
{
	try
	{
		var routes = metrics.Snapshot();
		var stages = metrics.StageSnapshot();
		var outcomes = metrics.OutcomeSnapshot();
		var (hits, misses) = cache.Snapshot();
		var total = hits + misses;
		return Results.Ok(new
		{
			generatedAt = System.DateTime.UtcNow,
			routes,
			pipelineStages = stages,
			outcomes,
			askCache = new
			{
				hits,
				misses,
				hitRate = total == 0 ? 0.0 : Math.Round(hits / (double)total, 4)
			}
		});
	}
	catch
	{
		return Results.Ok(new
		{
			generatedAt = System.DateTime.UtcNow,
			routes = Array.Empty<object>(),
			pipelineStages = Array.Empty<object>(),
			outcomes = Array.Empty<object>()
		});
	}
});
app.Run();
