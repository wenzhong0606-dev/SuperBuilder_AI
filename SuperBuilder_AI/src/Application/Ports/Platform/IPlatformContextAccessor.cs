using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Interfaces.Platform;

/// <summary>
/// 当前请求/作用域内的平台上下文访问器（P4）。
///
/// 以 <c>scoped</c> 生命周期注册，使深层服务（如 <c>SuperBIContext</c> 的全局租户过滤）
/// 可无参获取当前租户，而无需把 <c>tenantId</c> 透传至每一个方法签名。
///
/// 读取策略由消费方决定：为 <c>null</c> 或 <see cref="PlatformContext.Tenant"/> 未作用域时，
/// 不应施加租户隔离（例如 Golden 回归运行时使用全局上下文）。
/// </summary>
public interface IPlatformContextAccessor
{
	/// <summary>当前平台上下文；未设定时为 <c>null</c>。</summary>
	PlatformContext? Current { get; set; }
}
