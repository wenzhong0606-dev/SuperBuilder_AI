using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Interfaces.Audit;

namespace SuperBuilder_AI.Application.Metadata;

/// <summary>
/// 存量向量回填（§L.4）：遍历三类 Metadata 中 VectorId 非空的行，用 RetrieveVectorAsync
/// 取回向量+旧 payload，按 data_source_id 反查（column→table→ds；semantic→column→table→ds），
/// 以该 ds 当前 ActiveMetadataVersion 为 metadata_version 重新 Upsert（同 ID，向量不变）。
/// 跑完置 DataSource.VectorsBackfilled=true。幂等：已回填的 ds 直接跳过。
/// </summary>
public sealed class MetadataVectorBackfillJob
{
	private readonly SuperBIContext _context;
	private readonly IQdrantService _qdrant;
	private readonly IAuditLogService? _audit;

	public MetadataVectorBackfillJob(
		SuperBIContext context,
		IQdrantService qdrant,
		IAuditLogService? audit = null)
	{
		_context = context;
		_qdrant = qdrant;
		_audit = audit;
	}

	public async Task RunAsync(CancellationToken ct = default)
	{
		try
		{
			var dsIds = await _context.DataSources
				.Select(d => d.Id)
				.ToListAsync(ct);

			foreach (var dsId in dsIds)
			{
				if (ct.IsCancellationRequested) break;

				var ds = await _context.DataSources
					.FirstOrDefaultAsync(d => d.Id == dsId, ct);
				if (ds is null) continue;

				if (ds.VectorsBackfilled) continue; // 已回填则跳过，幂等

				await BackfillDataSourceAsync(ds, ct);

				ds.VectorsBackfilled = true;
				await _context.SaveChangesAsync(ct);
			}

			await AuditAsync("success", "向量存量回填周期完成。", ct);
		}
		catch (Exception ex)
		{
			await AuditAsync("failure", "向量存量回填异常：" + ex.Message, ct);
			throw;
		}
	}

	private async Task BackfillDataSourceAsync(DataSource ds, CancellationToken ct)
	{
		var tables = await _context.MetadataTables
			.Where(t => t.DataSourceId == ds.Id && t.VectorId != null)
			.ToListAsync(ct);
		foreach (var t in tables)
			await ReUpsertAsync(t.VectorId!, ds, t.TenantId, "table", ct);

		var columns = await _context.MetadataColumns
			.Where(c => c.VectorId != null)
			.Include(c => c.MetadataTable)
			.Where(c => c.MetadataTable != null && c.MetadataTable.DataSourceId == ds.Id)
			.ToListAsync(ct);
		foreach (var c in columns)
			await ReUpsertAsync(c.VectorId!, ds, c.MetadataTable!.TenantId, "column", ct);

		var semantics = await _context.MetadataSemantics
			.Where(s => s.VectorId != null)
			.Include(s => s.MetadataColumn).ThenInclude(c => c!.MetadataTable)
			.Where(s => s.MetadataColumn != null && s.MetadataColumn.MetadataTable != null
				&& s.MetadataColumn.MetadataTable.DataSourceId == ds.Id)
			.ToListAsync(ct);
		foreach (var s in semantics)
			await ReUpsertAsync(s.VectorId!, ds, s.MetadataColumn!.MetadataTable!.TenantId, "semantic", ct);
	}

	private async Task ReUpsertAsync(
		string id,
		DataSource ds,
		long tenantId,
		string type,
		CancellationToken ct)
	{
		var retrieved = await _qdrant.RetrieveVectorAsync(id, ct);
		if (retrieved is null)
			throw new InvalidOperationException($"向量回填失败：point {id} 不存在。");

		var (vector, oldPayload) = retrieved.Value;
		var payload = new System.Collections.Generic.Dictionary<string, object>(oldPayload)
		{
			["tenant_id"] = tenantId,
			["data_source_id"] = ds.Id,
			// 存量 point 初始归属当前 active 版本（未扫描前为 0）。
			["metadata_version"] = ds.ActiveMetadataVersion,
			["metadata_type"] = type
		};

		await _qdrant.UpsertAsync(id, vector, payload, ct);
	}

	private async Task AuditAsync(string result, string message, CancellationToken ct)
	{
		try
		{
			if (_audit is null) return;
			await _audit.LogAsync(new AuditLogEntry(
				TenantId: 0,
				Action: "metadata:vector:backfill",
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
