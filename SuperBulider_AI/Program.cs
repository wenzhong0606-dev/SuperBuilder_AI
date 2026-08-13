using Microsoft.EntityFrameworkCore;
using SuperBulider_AI.Configuration;
using SuperBulider_AI.Data;
using SuperBulider_AI.Infrastructure.Database;
using SuperBulider_AI.Interfaces;
using SuperBulider_AI.Services;
using SuperBulider_AI.Services.BI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<SuperBIContext>(options =>
{

	options.UseSqlServer(
		builder.Configuration
		.GetConnectionString("SuperBI"));

});

builder.Services.AddHttpClient<IQwenService, QwenService>(client =>
{
	client.Timeout = TimeSpan.FromMinutes(5);

	client.DefaultRequestHeaders.Add(
		"Accept",
		"application/json");
});

builder.Services.Configure<QdrantOptions>(builder.Configuration.GetSection("Qdrant"));

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

builder.Services.AddScoped<IQueryUnderstandingService, QueryUnderstandingService>();

builder.Services.AddScoped<IQueryPlanBuilder, QueryPlanBuilder>();

builder.Services.AddScoped<IDataSourceConnectionFactory, DataSourceConnectionFactory>();

builder.Services.AddScoped<ISqlDialect, SqlServerDialect>();

builder.Services.AddScoped<ISqlDialect, MySqlDialect>();

builder.Services.AddScoped<ISqlDialect, PostgreSqlDialect>();

builder.Services.AddScoped<SqlDialectResolver>();

builder.Services.AddScoped<ISqlQueryBuilder, SqlQueryBuilder>();

builder.Services.AddScoped<IQueryExecutionService, QueryExecutionService>();

builder.Services.AddScoped<IResultUnderstandingService, ResultUnderstandingService>();

builder.Services.AddScoped<IBIConversationService, BIConversationService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Home/Error");
	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
	app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
	name: "default",
	pattern: "{controller=Home}/{action=Index}/{id?}")
	.WithStaticAssets();


app.Run();
