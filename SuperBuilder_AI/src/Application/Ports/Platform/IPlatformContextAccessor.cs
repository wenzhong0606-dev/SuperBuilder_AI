using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Interfaces.Platform;

/// <summary>
/// 当前请求/作用域内的平台上下文访问器（P4）。
///
/// 以 <c>scoped</c> 生命周期注册，使 Runtime 入口（如 <c>BIConversationService</c>）可无参获取当前租户，
/// 并据此驱动下游隔离。P4.3 的 <c>SuperBIContext</c> 全局租户过滤由入口显式调用
/// <c>ApplyTenantScope(tenantId)</c> 开启，而非由 DbContext 直接读取本访问器，
/// 以避免 DbContext 构造期/请求期取值错位（早期方案因读取 System 上下文 TenantId=0 导致过度过滤、Golden 回归）。
///
/// 读取策略由消费方决定：<see cref="PlatformContext.Tenant"/> 的 <see cref="TenantContext.IsScoped"/> 为 <c>false</c>
/// （系统/全局，如 Golden 回归运行时）时，不应施加租户隔离。
/// </summary>
public interface IPlatformContextAccessor
{
	/// <summary>当前平台上下文；未设定时为 <c>null</c>。</summary>
	PlatformContext? Current { get; set; }
}
