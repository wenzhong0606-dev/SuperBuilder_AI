using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Infrastructure.Security;

namespace SuperBuilder_AI.Api.Background;

/// <summary>
/// 启动期一次性存量再加密：将库中仍以明文持久化的数据源连接串（未以 <c>v1:</c> 开头）改写为 AES-256-GCM 信封。
/// 幂等：仅处理非 v1: 行；<see cref="ISecretStore"/> 在作用域内惰性解析，缺失主密钥时仅记录且不阻塞启动。
/// </summary>
public sealed class DataSourceSecretMigrationHostedService : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<DataSourceSecretMigrationHostedService> _logger;

	public DataSourceSecretMigrationHostedService(
		IServiceScopeFactory scopeFactory,
		ILogger<DataSourceSecretMigrationHostedService> logger)
	{
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		try
		{
			await MigrateAsync(stoppingToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "存量数据源连接串再加密失败（不阻塞启动）。");
		}

		// 仅执行一次，随后空转至应用停止。
		try
		{
			await Task.Delay(Timeout.Infinite, stoppingToken);
		}
		catch (OperationCanceledException)
		{
			// 应用停止，正常退出。
		}
	}

	private async Task MigrateAsync(CancellationToken ct)
	{
		using var scope = _scopeFactory.CreateScope();
		var secrets = scope.ServiceProvider.GetRequiredService<ISecretStore>();
		var db = scope.ServiceProvider.GetRequiredService<SuperBIContext>();

		var pending = await db.DataSources
			.Where(d => d.ConnectionString != null && !d.ConnectionString.StartsWith("v1:", StringComparison.Ordinal))
			.ToListAsync(ct);
		if (pending.Count == 0)
			return;

		foreach (var ds in pending)
		{
			ds.ConnectionString = secrets.Protect(ds.ConnectionString!);
		}
		await db.SaveChangesAsync(ct);
		_logger.LogInformation("已对 {Count} 条存量明文数据源连接串完成加密。", pending.Count);
	}
}
