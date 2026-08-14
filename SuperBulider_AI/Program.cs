using Microsoft.EntityFrameworkCore;
using SuperBulider_AI.Configuration;
using SuperBulider_AI.Data;
using SuperBulider_AI.Infrastructure.Database;
using SuperBulider_AI.Interfaces;
using SuperBulider_AI.Interfaces.BI;
using SuperBulider_AI.Interfaces.Database;
using SuperBulider_AI.Models.BI;
using SuperBulider_AI.Services;
using SuperBulider_AI.Services.BI;
using SuperBulider_AI.Services.BI.Planning;
using SuperBulider_AI.Services.Database;

// Program: 应用启动项
//
// 说明:
// - 注册依赖注入服务
// - 配置 DbContext
// - 配置 Qwen Chat
// - 配置 Qwen Embedding
// - 配置 Qdrant
// - 配置 Metadata
// - 配置 BI 核心服务

var builder =
	WebApplication.CreateBuilder(args);

// -----------------------------
// 基础服务
// -----------------------------
builder.Services
	.AddControllersWithViews();

// -----------------------------
// 数据库
// -----------------------------
builder.Services
	.AddDbContext<SuperBIContext>(
		options =>
		{
			options.UseSqlServer(
				builder.Configuration
					.GetConnectionString(
						"SuperBI"));
		});

// -----------------------------
// Qwen Chat
// -----------------------------
builder.Services
	.AddHttpClient<
		IQwenService,
		QwenService>(
		client =>
		{
			client.Timeout =
				TimeSpan.FromMinutes(5);

			client.DefaultRequestHeaders
				.Add(
					"Accept",
					"application/json");
		});

// -----------------------------
// 配置项
// -----------------------------
builder.Services
	.Configure<QdrantOptions>(
		builder.Configuration
			.GetSection("Qdrant"));

builder.Services
	.Configure<EmbeddingOptions>(
		builder.Configuration
			.GetSection("Embedding"));

// -----------------------------
// Metadata
// -----------------------------
builder.Services
	.AddScoped<
		IDataSourceMetadataReader,
		MySqlMetadataReader>();

builder.Services
	.AddScoped<
		MetadataScannerService>();

builder.Services
	.AddScoped<
		IMetadataSearchTextBuilder,
		MetadataSearchTextBuilder>();

builder.Services
	.AddScoped<
		IMetadataPromptBuilder,
		MetadataPromptBuilder>();

// -----------------------------
// Embedding
//
// Production:
//     qwen3.7-text-embedding
// -----------------------------
builder.Services
	.AddHttpClient<
		IEmbeddingService,
		QwenEmbeddingService>(
		client =>
		{
			client.Timeout =
				TimeSpan.FromSeconds(120);

			client.DefaultRequestHeaders
				.Add(
					"Accept",
					"application/json");
		});

// -----------------------------
// Qdrant
// -----------------------------
builder.Services
	.AddScoped<
		IQdrantService,
		QdrantService>();

// -----------------------------
// Metadata Vector
// -----------------------------
builder.Services
	.AddScoped<
		IMetadataVectorService,
		MetadataVectorService>();

builder.Services
	.AddScoped<
		IMetadataVectorIndexService,
		MetadataVectorIndexService>();

// -----------------------------
// Metadata Semantic
// -----------------------------
builder.Services
	.AddScoped<
		IMetadataSemanticService,
		MetadataSemanticService>();

builder.Services
	.AddScoped<
		IMetadataSemanticSearchService,
		MetadataSemanticSearchService>();

builder.Services
	.AddScoped<
		IMetadataContextBuilder,
		MetadataContextBuilder>();

// -----------------------------
// BI 核心服务
// -----------------------------
builder.Services.AddScoped<QueryPlanValidator>();
builder.Services.AddScoped<QueryIntentNormalizer>();

builder.Services
	.AddScoped<
		IQueryUnderstandingService,
		QueryUnderstandingService>();

builder.Services
	.AddScoped<
		IQueryJoinInferenceService,
		QueryJoinInferenceService>();

builder.Services
	.AddScoped<
		IQueryPlanBuilder,
		QueryPlanBuilder>();

builder.Services
	.AddScoped<
		IResultUnderstandingService,
		ResultUnderstandingService>();

builder.Services
	.AddScoped<
		IBIConversationService,
		BIConversationService>();

// -----------------------------
// 数据库连接与 SQL 执行
// -----------------------------
builder.Services
	.AddScoped<
		IDataSourceConnectionFactory,
		DataSourceConnectionFactory>();

builder.Services
	.AddScoped<
		ISqlQueryBuilder,
		SqlQueryBuilder>();

builder.Services
	.AddScoped<
		IQueryExecutionService,
		QueryExecutionService>();

// -----------------------------
// SQL 方言
// -----------------------------
builder.Services
	.AddScoped<ISqlDialect, SqlServerDialect>();

builder.Services
	.AddScoped<ISqlDialect, MySqlDialect>();

builder.Services
	.AddScoped<ISqlDialect, PostgreSqlDialect>();

builder.Services
	.AddScoped<
		SqlDialectResolver>();

var app =
	builder.Build();

// -----------------------------
// HTTP Pipeline
// -----------------------------
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler(
		"/Home/Error");

	app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
		name: "default",
		pattern:
			"{controller=Home}/{action=Index}/{id?}")
	.WithStaticAssets();

app.Run();