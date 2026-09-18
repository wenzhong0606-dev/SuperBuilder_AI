using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Application.Metadata;

/// <summary>
/// 扫描失败记录器（C7 / 部分成功）。按
/// (JobId, DataSourceId, Database, Schema, TableName, Stage) 幂等 upsert：
/// 已存在则累加 RetryCount 并更新 LastFailedAt；否则新建 RetryCount=1。
/// 仅保存脱敏后的错误类型与摘要，绝不保存连接串等敏感信息。
/// 重试编排（§L.6）在 Step 7 调用本记录器。
/// </summary>
public sealed class MetadataScanFailureRecorder
{
	private readonly SuperBIContext _ctx;

	public MetadataScanFailureRecorder(SuperBIContext ctx) => _ctx = ctx;

	public async Task RecordTableFailureAsync(
		long jobId,
		long dataSourceId,
		long? originalJobId,
		string? database,
		string? schema,
		string? tableName,
		string? stage,
		string? errorType,
		string? errorMessage,
		CancellationToken ct = default)
	{
		var existing = await _ctx.MetadataScanJobFailures
			.Where(f => f.JobId == jobId
				&& f.DataSourceId == dataSourceId
				&& (f.Database == database || (f.Database == null && database == null))
				&& (f.Schema == schema || (f.Schema == null && schema == null))
				&& f.TableName == tableName
				&& (f.Stage == stage || (f.Stage == null && stage == null)))
			.FirstOrDefaultAsync(ct);

		if (existing is null)
		{
			_ctx.MetadataScanJobFailures.Add(new MetadataScanJobFailure
			{
				JobId = jobId,
				OriginalJobId = originalJobId,
				DataSourceId = dataSourceId,
				Database = database,
				Schema = schema,
				TableName = tableName,
				Stage = stage,
				ErrorType = errorType,
				ErrorMessage = errorMessage,
				RetryCount = 1,
				FirstFailedAt = DateTime.UtcNow,
				LastFailedAt = DateTime.UtcNow,
				Resolved = false
			});
		}
		else
		{
			existing.ErrorType = errorType;
			existing.ErrorMessage = errorMessage;
			existing.RetryCount += 1;
			existing.LastFailedAt = DateTime.UtcNow;
			existing.Resolved = false;
		}

		await _ctx.SaveChangesAsync(ct);
	}
}
