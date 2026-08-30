using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Infrastructure.Database;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models;
using SuperBuilder_AI.Services.BI;
using SuperBuilder_AI.Services.BI.Dashboard;
using SuperBuilder_AI.Services.BI.Evaluation;
using SuperBuilder_AI.Services.BI.Planning;
using SuperBuilder_AI.Services;
using SuperBuilder_AI.Configuration;
using SuperBuilder_AI.Interfaces.BI.Evaluation;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.BI.Dashboard;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Interfaces.Database;
using SuperBuilder_AI.Services.Database;
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
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Services.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

builder.Services.AddDbContext<SuperBIContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<QdrantOptions>(builder.Configuration.GetSection("Qdrant"));
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection("Embedding"));
builder.Services.AddScoped<IDataSourceMetadataReader, MySqlMetadataReader>();
builder.Services.AddScoped<MetadataScannerService>();
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
builder.Services.AddScoped<IQueryUnderstandingService>(sp => sp.GetRequiredService<QueryUnderstandingService>());
builder.Services.AddScoped<QueryJoinInferenceService>();
builder.Services.AddScoped<IQueryJoinInferenceService>(sp => sp.GetRequiredService<QueryJoinInferenceService>());
builder.Services.AddScoped<QueryPlanValidator>();
builder.Services.AddScoped<QueryPlanBuilder>();
builder.Services.AddScoped<IQueryPlanBuilder>(sp => sp.GetRequiredService<QueryPlanBuilder>());

// A4 重构：QueryPlanPipeline（从 BIConversationService 抽取的 QueryPlan 编排管线）
builder.Services.AddScoped<QueryPlanPipeline>();
builder.Services.AddScoped<IQueryPlanPipeline>(sp => sp.GetRequiredService<QueryPlanPipeline>());
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

// P10.1 Identity / RBAC（确定性，不调 LLM）
builder.Services.AddScoped<IIdentityService, IdentityService>();

// P6.3 LowcodeRenderer 渲染引擎（对照 QueryPlanPipeline 接入取数）
builder.Services.AddScoped<IDashboardRenderer, DashboardLowcodeRenderer>();
builder.Services.AddScoped<IWidgetDataResolver, QueryPlanWidgetDataResolver>();

// P7.2 Theme 级联解析端口（仪表盘显式键 → 租户默认 → 内置默认）
builder.Services.AddScoped<IThemeResolver, ThemeResolver>();

// ── BIConversationService 依赖链补充注册 ──────────────────
builder.Services.AddScoped<IQueryPlanExplainabilityService>(sp => sp.GetRequiredService<QueryPlanExplainabilityService>());
builder.Services.AddScoped<IMetadataPromptBuilder>(sp => sp.GetRequiredService<MetadataPromptBuilder>());
builder.Services.AddScoped<ResultUnderstandingService>();
builder.Services.AddScoped<IResultUnderstandingService>(sp => sp.GetRequiredService<ResultUnderstandingService>());
builder.Services.AddScoped<QueryPlanMetadataValidator>();
builder.Services.AddScoped<MetadataSearchService>();
builder.Services.AddScoped<IMetadataSearchService>(sp => sp.GetRequiredService<MetadataSearchService>());
builder.Services.AddScoped<GoldenBaselineComparisonService>();

var app = builder.Build();

// P10.1 Identity 全局目录种子（幂等；失败不阻断平台启动）
using (var seedScope = app.Services.CreateScope())
{
    try
    {
        var identity = seedScope.ServiceProvider.GetRequiredService<IIdentityService>();
        await identity.SeedAsync();
    }
    catch (Exception seedEx)
    {
        Console.Error.WriteLine($"[IdentitySeed] skipped: {seedEx.Message}");
    }
}

if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();
app.Run();