using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Application.Metadata;

/// <summary>
/// 向量 GC 后台任务（§L.4 / §L.7）：轮询 MetadataVectorGcRequest 的 Pending 待办，
/// 按 data_source_id（删除数据源）或 旧版本（激活后清理）删 Qdrant point。
/// 进程退出前未执行的待办持久化，重启后继续，不丢。失败仅影响存储，不影响正确性（过滤已屏蔽）。
/// </summary>
public sealed class MetadataVectorGcJob
{
	private readonly SuperBIContext _context;
	private readonly IQdrantService _qdrant;
	private readonly IAuditLogService _audit;

	public MetadataVectorGcJob(
		SuperBIContext context,
		IQdrantService qdrant,
		IAuditLogService audit)
	{
		_context = context;
		_qdrant = qdrant;
		_audit = audit;
	}

	public async Task RunAsync(CancellationToken ct = default)
	{
		var retryAfter = System.DateTime.UtcNow.AddMinutes(-5);
		var abandonedAfter = System.DateTime.UtcNow.AddMinutes(-10);
		var pending = await _context.MetadataVectorGcRequests
			.Where(r => r.Status == "Pending"
				|| (r.Status == "Failed" && (r.LastAttemptAt == null || r.LastAttemptAt < retryAfter))
				|| (r.Status == "Running" && (r.LastAttemptAt == null || r.LastAttemptAt < abandonedAfter)))
			.ToListAsync(ct);

		var failed = 0;
		foreach (var req in pending)
		{
			req.Status = "Running";
			req.Error = null;
			req.LastAttemptAt = System.DateTime.UtcNow;
			await _context.SaveChangesAsync(ct);

			try
			{
				await ProcessAsync(req, ct);
				req.Status = "Done";
			}
			catch (System.Exception ex)
			{
				req.Status = "Failed";
				req.Error = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
				failed++;
				await AuditAsync("failure", $"向量 GC 待办 {req.Id} 失败：{req.Error}", ct);
			}

			await _context.SaveChangesAsync(ct);
		}

		await AuditAsync(failed == 0 ? "success" : "failure",
			$"向量 GC 周期完成：处理 {pending.Count} 个待办，失败 {failed} 个。", ct);
	}

	private async Task ProcessAsync(MetadataVectorGcRequest req, CancellationToken ct)
	{
		// 1) 快照优先：删除前枚举的 point ID 列表直接删除（覆盖从未回填、无 data_source_id 的旧 point）。
		if (!string.IsNullOrWhiteSpace(req.PayloadJson))
		{
			var ids = JsonSerializer.Deserialize<List<string>>(req.PayloadJson);
			if (ids is { Count: > 0 })
			{
				await _qdrant.DeleteBatchAsync(ids, ct);
				return;
			}
		}

		// 2) 按 data_source_id 枚举删除（删除数据源场景；或激活后旧版本清理）。
		if (req.DataSourceId is not { } dsId) return;

		var active = await _context.DataSources
			.Where(d => d.Id == dsId)
			.Select(d => d.ActiveMetadataVersion)
			.FirstOrDefaultAsync(ct);

		var idsToDelete = new List<string>();
		var all = await _qdrant.ListPointIdsAsync(ct);
		foreach (var id in all)
		{
			if (ct.IsCancellationRequested) break;
			var retrieved = await _qdrant.RetrieveVectorAsync(id, ct);
			if (retrieved is null) continue;

			var payload = retrieved.Value.Payload;
			if (!payload.TryGetValue("data_source_id", out var dsVal)) continue;
			if (!long.TryParse(dsVal?.ToString(), out var pointSourceId) || pointSourceId != dsId) continue;

			// 删除数据源：删该 ds 全部 point。
			if (req.Reason == "DataSourceDeleted")
			{
				idsToDelete.Add(id);
				continue;
			}

			// 旧版清理：metadata_version < 当前 active。
			if (req.Reason == "StagingGc"
				&& payload.TryGetValue("metadata_version", out var verVal)
				&& int.TryParse(verVal?.ToString(), out var ver)
				&& ver < active)
			{
				idsToDelete.Add(id);
			}

			// 孤儿 staging 版本精确清理（Step 5）：metadata_version == 指定 OldVersion（从未激活）。
			if (req.Reason == "OrphanStagingGc"
				&& req.OldVersion.HasValue
				&& payload.TryGetValue("metadata_version", out var ovVal)
				&& int.TryParse(ovVal?.ToString(), out var ov)
				&& ov == req.OldVersion.Value)
			{
				idsToDelete.Add(id);
			}
		}

		if (idsToDelete.Count > 0)
			await _qdrant.DeleteBatchAsync(idsToDelete, ct);
	}

	private async Task AuditAsync(string result, string message, CancellationToken ct)
	{
		try
		{
			await _audit.LogAsync(new AuditLogEntry(
				TenantId: 0,
				Action: "metadata:vector:gc",
				EntityType: "VectorIndex",
				Actor: "scheduler",
				Result: result,
				Message: message), ct);
		}
		catch
		{
			// 审计写入失败不应影响主流程。
		}
	}
}
