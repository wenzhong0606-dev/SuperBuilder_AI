using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SuperBuilder_AI.Interfaces.BI;

/// <summary>
/// Ask 响应缓存的「完整版本上下文」（M6-04 Cache 完整版本 / SB-P1-09）。
///
/// <para>
/// 把影响答案正确性的全部维度折叠进缓存键，使任一维度变化后旧缓存不再复用：
/// <list type="bullet">
/// <item><b>PermissionFingerprint</b>：授权数据源集合指纹（撤权后变化）。</item>
/// <item><b>RlsPolicyFingerprint</b>：行级安全策略指纹（策略调整后变化）。</item>
/// <item><b>Culture</b>：请求文化（语言切换后响应文案变化）。</item>
/// <item><b>ModelVersion</b>：LLM 模型版本（模型升级后行为变化）。</item>
/// <item><b>SemanticVersion</b>：语义标签版本（语义层变动后变化）。</item>
/// <item><b>MetadataVersion</b>：元数据版本（表/列结构扫描后变化）。</item>
/// <item><b>DataSourceVersion</b>：数据源目录版本（新增/启停数据源后变化）。</item>
/// </list>
/// </para>
///
/// <para>零回归：缓存仅作用于 <c>POST api/ask</c>；Golden 走独立端点不受影响。</para>
/// </summary>
public sealed record AskCacheVersionContext(
	string PermissionFingerprint,
	string RlsPolicyFingerprint,
	string Culture,
	string ModelVersion,
	string SemanticVersion,
	string MetadataVersion,
	string DataSourceVersion)
{
	/// <summary>把版本上下文与归一化问题拼成完整缓存键。</summary>
	public string BuildCacheKey(string standaloneQuestion)
	{
		var sb = new StringBuilder();
		sb.Append(standaloneQuestion);
		sb.Append("\u001fperm:").Append(PermissionFingerprint);
		sb.Append("\u001fpolicy:").Append(RlsPolicyFingerprint);
		sb.Append("\u001fculture:").Append(Culture);
		sb.Append("\u001fmodel:").Append(ModelVersion);
		sb.Append("\u001fsemantic:").Append(SemanticVersion);
		sb.Append("\u001fmetadata:").Append(MetadataVersion);
		sb.Append("\u001fds:").Append(DataSourceVersion);
		return sb.ToString();
	}

	/// <summary>
	/// 兼容未注入 <see cref="IAskCacheVersionProvider"/> 的调用点（测试/旧路径）：
	/// 仅填入权限与策略指纹（沿用既有逻辑），其余维度置 <c>na</c>，保持旧键形状不破坏既有命中断言。
	/// </summary>
	public static AskCacheVersionContext Legacy(string? permissionFingerprint, string? rlsPolicyFingerprint, string? culture) =>
		new AskCacheVersionContext(
			permissionFingerprint ?? "legacy",
			rlsPolicyFingerprint ?? "legacy",
			culture ?? CultureInfo.CurrentUICulture.Name,
			"na", "na", "na", "na");
}

/// <summary>解析 Ask 响应缓存所需的完整版本上下文。</summary>
public interface IAskCacheVersionProvider
{
	/// <summary>解析 7 维版本上下文，用于构建缓存键。</summary>
	Task<AskCacheVersionContext> ResolveAsync(
		long tenantId,
		long userId,
		IReadOnlyCollection<long> authorizedDataSourceIds,
		string? permissionFingerprint,
		CancellationToken ct = default);
}

/// <summary>语义标签版本提供器（租户 + 全局标签的 RowVersion 聚合）。</summary>
public interface ISemanticVersionProvider
{
	Task<string> ResolveAsync(long tenantId, CancellationToken ct = default);
}

/// <summary>元数据版本提供器（租户表/列 RowVersion 聚合）。</summary>
public interface IMetadataVersionProvider
{
	Task<string> ResolveAsync(long tenantId, CancellationToken ct = default);
}

/// <summary>数据源目录版本提供器（租户数据源集合指纹）。</summary>
public interface IDataSourceCatalogVersionProvider
{
	Task<string> ResolveAsync(long tenantId, CancellationToken ct = default);
}
