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
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Services.Audit;
using SuperBuilder_AI.Interfaces.Quota;
using SuperBuilder_AI.Services.Quota;
using SuperBuilder_AI.Middleware;
using SuperBuilder_AI.Api.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// M0-01：支持本地未提交覆盖文件（appsettings.Local.json），用于存放开发/本地密钥，禁止提交。
// 该文件已被 .gitignore 忽略；生产环境应通过环境变量（如 ConnectionStrings__WmsMySql / Qwen__ApiKey）注入。
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddControllersWithViews(options =>
{
	// SB-P0-09: Golden/evaluation controllers are Development-only infrastructure.
	if (!builder.Environment.IsDevelopment())
		options.Conventions.Add(new SuperBuilder_AI.Api.Security.ProductionEvaluationRouteConvention());
});
builder.Services.AddHttpClient();

builder.Services.AddDbContext<SuperBIContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<QdrantOptions>(builder.Configuration.GetSection("Qdrant"));
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection("Embedding"));
builder.Services.AddScoped<IDataSourceMetadataReader, MySqlMetadataReader>();
builder.Services.AddScoped<PlatformAdminBootstrapper>();
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
builder.Services.AddSingleton<SuperBuilder_AI.Services.BI.IAskConversationService, SuperBuilder_AI.Services.BI.AskConversationService>();
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

// P11.5.2 安全/运维轨：请求指标采集器（请求数 / 错误数 / P95 延迟，按路由聚合）
builder.Services.AddSingleton<SuperBuilder_AI.Middleware.RequestMetricsCollector>();

// M0-05：受控启动诊断单例（供 /health 与初始化端点读取，绝不向普通用户输出堆栈）
builder.Services.AddSingleton<StartupDiagnostics>();
// M0-05：本地化目录种子服务，使 UiLanguage/Text 在启动序列中固定顺序执行
builder.Services.AddScoped<ILocalizationSeedService, LocalizationSeedService>();
// M0-08：限流阈值（绑定配置节 "RateLimit"，缺省使用安全默认值）
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection("RateLimit"));

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

        // 步骤 4：默认策略/主题（Quota 平台默认配额，幂等）
        try
        {
            var quota = startupScope.ServiceProvider.GetRequiredService<IQuotaService>();
            await quota.EnsureSeededAsync();
            diagnostics.MarkStep("Quota");
            logger.LogInformation("Platform default quota seeded.");
        }
        catch (Exception seedEx)
        {
            diagnostics.State = BootstrapState.SeedIncomplete;
            diagnostics.Reason = $"默认配额种子失败：{seedEx.Message}";
            logger.LogError(seedEx, "Quota seed failed.");
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
app.UseHttpsRedirection();
app.UseRouting();
// P11 错误治理：统一异常 → 结构化友好 JSON（错误码 + 关联ID），须位于路由之后、端点之前，包裹后续所有中间件
app.UseMiddleware<UnifiedExceptionMiddleware>();
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
		var (hits, misses) = cache.Snapshot();
		var total = hits + misses;
		return Results.Ok(new
		{
			generatedAt = System.DateTime.UtcNow,
			routes,
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
		return Results.Ok(new { generatedAt = System.DateTime.UtcNow, routes = Array.Empty<object>() });
	}
});
app.Run();
