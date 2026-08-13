using Microsoft.EntityFrameworkCore;
using SuperBulider_AI.Configuration;
using SuperBulider_AI.Data;
using SuperBulider_AI.Infrastructure.Database;
using SuperBulider_AI.Interfaces;
using SuperBulider_AI.Interfaces.BI;
using SuperBulider_AI.Interfaces.Database;
using SuperBulider_AI.Services;
using SuperBulider_AI.Services.BI;
using SuperBulider_AI.Services.Database;

// Program: 应用启动项
// 说明:
// - 注册依赖注入服务
// - 配置 DbContext、HTTP 客户端及 BI 相关服务
// - 配置路由与中间件管线

var builder = WebApplication.CreateBuilder(args);

// -----------------------------
// 基础服务 (Controllers / Views)
// -----------------------------
builder.Services.AddControllersWithViews();

// -----------------------------
// 数据库: SuperBIContext (SQL Server)
// -----------------------------
builder.Services.AddDbContext<SuperBIContext>(options =>
{
	options.UseSqlServer(builder.Configuration.GetConnectionString("SuperBI"));
});

// -----------------------------
// 外部 HTTP 服务 (AI 接口)
// -----------------------------
builder.Services.AddHttpClient<IQwenService, QwenService>(client =>
{
	// 延长超时时间以应对大模型调用延迟
	client.Timeout = TimeSpan.FromMinutes(5);
	client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// -----------------------------
// 配置项绑定
// -----------------------------
builder.Services.Configure<QdrantOptions>(builder.Configuration.GetSection("Qdrant"));

// -----------------------------
// 元数据扫描与向量/语义服务
// -----------------------------
builder.Services.AddScoped<IDataSourceMetadataReader, MySqlMetadataReader>();
builder.Services.AddScoped<MetadataScannerService>();
builder.Services.AddScoped<IMetadataSearchTextBuilder, MetadataSearchTextBuilder>();
builder.Services.AddScoped<IMetadataPromptBuilder, MetadataPromptBuilder>();
builder.Services.AddScoped<IEmbeddingService, FakeEmbeddingService>();
builder.Services.AddScoped<IQdrantService, QdrantService>();
builder.Services.AddScoped<IMetadataVectorService, MetadataVectorService>();
builder.Services.AddScoped<IMetadataSemanticService, MetadataSemanticService>();
builder.Services.AddScoped<IMetadataSemanticSearchService, MetadataSemanticSearchService>();
builder.Services.AddScoped<IMetadataContextBuilder, MetadataContextBuilder>();

// -----------------------------
// BI 核心服务
// -----------------------------
builder.Services.AddScoped<IQueryUnderstandingService, QueryUnderstandingService>();
builder.Services.AddScoped<IQueryJoinInferenceService, QueryJoinInferenceService>();
builder.Services.AddScoped<IQueryPlanBuilder, QueryPlanBuilder>();
builder.Services.AddScoped<IResultUnderstandingService, ResultUnderstandingService>();
builder.Services.AddScoped<IBIConversationService, BIConversationService>();

// -----------------------------
// 数据库连接与 SQL 执行
// -----------------------------
builder.Services.AddScoped<IDataSourceConnectionFactory, DataSourceConnectionFactory>();
builder.Services.AddScoped<ISqlQueryBuilder, SqlQueryBuilder>();
builder.Services.AddScoped<IQueryExecutionService, QueryExecutionService>();

// -----------------------------
// SQL 方言与解析器
// 注意: 同时注册多个 ISqlDialect 实现以供解析器选择
// -----------------------------
builder.Services.AddScoped<ISqlDialect, SqlServerDialect>();
builder.Services.AddScoped<ISqlDialect, MySqlDialect>();
builder.Services.AddScoped<ISqlDialect, PostgreSqlDialect>();
builder.Services.AddScoped<SqlDialectResolver>();

// -----------------------------
// 其它服务
// -----------------------------
builder.Services.AddScoped<MetadataScannerService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Home/Error");
	// The default HSTS value is 30 days. You may want to change this for production scenarios.
	app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

// 静态资源映射（扩展方法）
app.MapStaticAssets();

// 默认路由
app.MapControllerRoute(
	name: "default",
	pattern: "{controller=Home}/{action=Index}/{id?}")
	.WithStaticAssets();

app.Run();
