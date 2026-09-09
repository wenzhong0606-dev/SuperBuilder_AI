namespace SuperBuilder_AI.Models.AppBuilder;

/// <summary>
/// 发布/回滚幂等记录（M7-11 契约 §7/§10.9/§10.10/§10.14）。
///
/// <para>
/// 以 <c>(TenantId, AppCode, IdempotencyKey)</c> 唯一约束记录一次成功的发布/回滚结果。
/// 同键重入（响应丢失重试）直接返回既有 <see cref="PublishedVersion"/>，不重复发布、不产生新版本号；
/// 同键但 <see cref="ExpectedDraftRevision"/> 不一致 → 调用方应被视为冲突（409 <c>SB_APP_IDEMPOTENCY_CONFLICT</c>）。
/// </para>
///
/// <para>与 <see cref="AppPlan"/> 一致：<see cref="TenantId"/> 仅作作用域列，不建指向 Tenant 的外键。</para>
/// </summary>
public class AppPublishIdempotency
{
	/// <summary>自增主键。</summary>
	public long Id { get; set; }

	/// <summary>作用域租户 Id。</summary>
	public long TenantId { get; set; }

	/// <summary>应用业务编码（与 <see cref="AppPlan.Code"/> 对应）。</summary>
	public string AppCode { get; set; } = string.Empty;

	/// <summary>客户端幂等键（发布/回滚请求头 <c>Idempotency-Key</c>，UUID）。</summary>
	public string IdempotencyKey { get; set; } = string.Empty;

	/// <summary>本次发布所基于的期望草稿修订号（乐观令牌）；用于冲突检测。</summary>
	public int ExpectedDraftRevision { get; set; }

	/// <summary>成功发布后固化的版本号（同键重入返回此值）。</summary>
	public int PublishedVersion { get; set; }

	/// <summary>记录创建时间（UTC）。</summary>
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
