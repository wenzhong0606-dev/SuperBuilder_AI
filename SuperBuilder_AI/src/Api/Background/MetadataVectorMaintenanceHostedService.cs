using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SuperBuilder_AI.Application.Metadata;
using SuperBuilder_AI.Data;

namespace SuperBuilder_AI.Api.Background;

/// <summary>
/// 向量维护后台处理器（§L.4）：周期性运行存量回填（MetadataVectorBackfillJob）与
/// 向量 GC（MetadataVectorGcJob）。两个任务均幂等、可重试；回填仅在 VectorsBackfilled=false 的
/// 源上工作，GC 仅处理 Pending 待办，因此常驻运行成本低。
/// </summary>
public sealed class MetadataVectorMaintenanceHostedService : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<MetadataVectorMaintenanceHostedService> _logger;
	private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

	public MetadataVectorMaintenanceHostedService(
		IServiceScopeFactory scopeFactory,
		ILogger<MetadataVectorMaintenanceHostedService> logger)
	{
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(Interval, stoppingToken);
			}
			catch (OperationCanceledException)
			{
				break;
			}

			try
			{
				using var scope = _scopeFactory.CreateScope();
				var backfill = scope.ServiceProvider.GetRequiredService<MetadataVectorBackfillJob>();
				var gc = scope.ServiceProvider.GetRequiredService<MetadataVectorGcJob>();
				await backfill.RunAsync(stoppingToken);
				await gc.RunAsync(stoppingToken);
			}
			catch (Exception ex)
			{
				// 单个维护周期异常不应拖垮循环。
				_logger.LogError(ex, "向量维护周期发生异常。");
			}
		}
	}
}
