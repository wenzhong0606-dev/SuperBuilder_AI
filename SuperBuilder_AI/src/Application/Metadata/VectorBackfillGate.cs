using System;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Application.Metadata;

/// <summary>
/// 向量回填闸门（§10.4 / §L.4）。
/// 严格过滤阶段（Features.MetadataVersionFilterEnabled=true）下，未回填完成的源禁止激活：
/// 其存量 point 缺 metadata_version payload，一旦严格过滤生效将整体丢失召回。
/// 阶段 A（过滤关闭）不拦激活，允许回填在后台进行。
/// </summary>
public sealed class VectorBackfillGate
{
	private readonly Features _features;

	public VectorBackfillGate(IOptions<Features> features)
	{
		_features = features.Value;
	}

	/// <summary>严格过滤阶段下，源未回填则禁止激活。</summary>
	public bool CanActivate(DataSource ds) =>
		!_features.MetadataVersionFilterEnabled || ds.VectorsBackfilled;

	/// <summary>不满足时抛 MetadataActivationBlockedException("vector_backfill_required")，由宿主写 409。</summary>
	public void AssertCanActivate(DataSource ds)
	{
		if (!CanActivate(ds))
			throw new MetadataActivationBlockedException("vector_backfill_required");
	}
}
