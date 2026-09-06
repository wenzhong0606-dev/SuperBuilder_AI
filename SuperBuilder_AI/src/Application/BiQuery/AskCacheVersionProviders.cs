using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.Identity;

namespace SuperBuilder_AI.Application.BiQuery;

/// <summary>
/// 组合式 Ask 缓存版本提供器（M6-04）。
///
/// <para>
/// 汇聚 7 个版本维度；任一维度来源缺失或抛错一律降级为该维度的稳定占位值
/// （<c>legacy</c> / <c>na</c>），绝不向主查询链路传播异常。
/// 默认实现即满足「撤权/策略/元数据/语义/语言/模型/数据源集合变化后不复用旧结果」。
/// </para>
/// </summary>
public sealed class CompositeAskCacheVersionProvider : IAskCacheVersionProvider
{
	private readonly IRowLevelSecurityService? _rowSecurity;
	private readonly IOptions<QwenOptions>? _qwen;
	private readonly IMetadataVersionProvider? _metadata;
	private readonly ISemanticVersionProvider? _semantic;
	private readonly IDataSourceCatalogVersionProvider? _dataSource;

	public CompositeAskCacheVersionProvider(
		IRowLevelSecurityService? rowSecurity = null,
		IOptions<QwenOptions>? qwen = null,
		IMetadataVersionProvider? metadata = null,
		ISemanticVersionProvider? semantic = null,
		IDataSourceCatalogVersionProvider? dataSource = null)
	{
		_rowSecurity = rowSecurity;
		_qwen = qwen;
		_metadata = metadata;
		_semantic = semantic;
		_dataSource = dataSource;
	}

	/// <inheritdoc />
	public async Task<AskCacheVersionContext> ResolveAsync(
		long tenantId,
		long userId,
		IReadOnlyCollection<long> authorizedDataSourceIds,
		string? permissionFingerprint,
		CancellationToken ct = default)
	{
		// 策略指纹：复用既有 RLS 服务；缺失或失败 → legacy。
		var policy = "legacy";
		try
		{
			if (_rowSecurity is not null)
				policy = await _rowSecurity.GetPolicyFingerprintAsync(tenantId, userId, ct);
		}
		catch
		{
			// 降级为 legacy
		}

		// 模型版本：来自配置；缺配置 → na。
		var model = _qwen?.Value?.Model ?? "na";

		// 语义/元数据/数据源目录：可空，缺失或失败 → na。
		var semantic = "na";
		try { if (_semantic is not null) semantic = await _semantic.ResolveAsync(tenantId, ct); } catch { }

		var metadata = "na";
		try { if (_metadata is not null) metadata = await _metadata.ResolveAsync(tenantId, ct); } catch { }

		var dataSource = "na";
		try { if (_dataSource is not null) dataSource = await _dataSource.ResolveAsync(tenantId, ct); } catch { }

		var culture = CultureInfo.CurrentUICulture.Name;
		return new AskCacheVersionContext(
			permissionFingerprint ?? "legacy", policy, culture, model, semantic, metadata, dataSource);
	}
}

/// <summary>
/// 元数据版本提供器：租户内全部元数据表与列的 <see cref="BaseEntity.RowVersion"/> 求和。
/// 表/列结构扫描会使 RowVersion 递增，从而令缓存键变化。
/// </summary>
public sealed class MetadataVersionProvider : IMetadataVersionProvider
{
	private readonly SuperBIContext _db;

	public MetadataVersionProvider(SuperBIContext db) => _db = db;

	public async Task<string> ResolveAsync(long tenantId, CancellationToken ct = default)
	{
		var tableRv = await _db.MetadataTables
			.Where(t => t.TenantId == tenantId)
			.Select(t => t.RowVersion)
			.SumAsync(ct);

		var columnRv = await _db.MetadataColumns
			.Where(c => c.MetadataTable.TenantId == tenantId)
			.Select(c => c.RowVersion)
			.SumAsync(ct);

		return (tableRv + columnRv).ToString(CultureInfo.InvariantCulture);
	}
}

/// <summary>
/// 语义版本提供器：租户自身语义标签 + 全局共享标签（TenantId==0）的
/// <see cref="BaseEntity.RowVersion"/> 求和。全局标签变化对所有租户生效。
/// </summary>
public sealed class SemanticVersionProvider : ISemanticVersionProvider
{
	private readonly SuperBIContext _db;

	public SemanticVersionProvider(SuperBIContext db) => _db = db;

	public async Task<string> ResolveAsync(long tenantId, CancellationToken ct = default)
	{
		var rv = await _db.SemanticLabels
			.Where(l => l.TenantId == tenantId || l.TenantId == 0)
			.Select(l => l.RowVersion)
			.SumAsync(ct);

		return rv.ToString(CultureInfo.InvariantCulture);
	}
}

/// <summary>
/// 数据源目录版本提供器：租户全部数据源的 <c>Id:Enabled:RowVersion</c> 拼接指纹。
/// 新增/启停数据源或数据源连接配置变更（RowVersion 递增）均会改变指纹。
/// </summary>
public sealed class DataSourceCatalogVersionProvider : IDataSourceCatalogVersionProvider
{
	private readonly SuperBIContext _db;

	public DataSourceCatalogVersionProvider(SuperBIContext db) => _db = db;

	public async Task<string> ResolveAsync(long tenantId, CancellationToken ct = default)
	{
		var rows = await _db.DataSources
			.Where(d => d.TenantId == tenantId)
			.Select(d => new { d.Id, d.Enabled, d.RowVersion })
			.ToListAsync(ct);

		if (rows.Count == 0) return "empty";

		var sb = new StringBuilder();
		foreach (var r in rows.OrderBy(r => r.Id))
			sb.Append(r.Id).Append(':').Append(r.Enabled ? 1 : 0).Append(':').Append(r.RowVersion).Append(';');

		return sb.ToString();
	}
}
