using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Application.Metadata;

/// <summary>
/// §L.5 消费点过滤（Ask 侧元数据读取必须限定到数据源的 Active MetadataVersion）。
///
/// <para>
/// 语义等价于 SQL：<c>WHERE MetadataVersion = ds.ActiveMetadataVersion</c>。
/// 实现为对 <see cref="SuperBIContext.DataSources"/> 的 EXISTS 关联子查询，
/// 可与 <c>Include</c> / <c>ThenInclude</c> / 既有 <c>Where</c> 自由组合（EF Core 会合成为单条 SQL）。
/// </para>
///
/// <para>
/// 零回归保证：存量数据 <c>ActiveMetadataVersion = 0</c> 且所有元数据行 <c>MetadataVersion = 0</c>，
/// 过滤条件退化为恒等，召回结果与未加过滤逐字节一致。
/// </para>
///
/// <para>
/// 适用边界：仅用于 Ask / 查询理解 / 显示解析等「消费点」读取；扫描器、种子、CI 假数据、
/// 向量索引维护（IndexAll / DetectOrphans / Validate）、管理/诊断控制器等写入或全版本可见路径
/// 不调用本扩展。
/// </para>
/// </summary>
public static class MetadataVersionQueryExtensions
{
	/// <summary>限定 MetadataTable 查询到其所属数据源的 ActiveMetadataVersion。</summary>
	public static IQueryable<MetadataTable> WhereActiveVersion(
		this IQueryable<MetadataTable> query,
		SuperBIContext db) =>
		query.Where(t => db.DataSources.Any(d =>
			d.Id == t.DataSourceId && d.ActiveMetadataVersion == t.MetadataVersion));

	/// <summary>限定 MetadataColumn 查询到其所属数据源的 ActiveMetadataVersion。</summary>
	public static IQueryable<MetadataColumn> WhereActiveVersion(
		this IQueryable<MetadataColumn> query,
		SuperBIContext db) =>
		query.Where(c => db.DataSources.Any(d =>
			d.ActiveMetadataVersion == c.MetadataVersion
			&& db.MetadataTables.Any(t => t.Id == c.MetadataTableId && t.DataSourceId == d.Id)));

	/// <summary>限定 MetadataSemantic 查询到其所属数据源的 ActiveMetadataVersion（经 Column→Table→DataSource 三级归属）。</summary>
	public static IQueryable<MetadataSemantic> WhereActiveVersion(
		this IQueryable<MetadataSemantic> query,
		SuperBIContext db) =>
		query.Where(s => db.DataSources.Any(d =>
			d.ActiveMetadataVersion == s.MetadataVersion
			&& db.MetadataColumns.Any(c => c.Id == s.MetadataColumnId
				&& db.MetadataTables.Any(t => t.Id == c.MetadataTableId && t.DataSourceId == d.Id))));
}
