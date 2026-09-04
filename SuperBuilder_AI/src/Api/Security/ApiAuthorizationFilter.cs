using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SuperBuilder_AI.Api.Errors;

namespace SuperBuilder_AI.Api.Security;

/// <summary>
/// 控制器层授权兜底（AUTH-2 Defense-in-Depth）。
///
/// <para>
/// 背景：全站鉴权此前<b>完全依赖</b> <c>AuthMiddleware</c>——所有控制器均未标注
/// <c>[Authorize]</c>，一旦中间件被绕过、被调整短路条件，或匿名白名单被误改，
/// 即无任何第二道防线，敏感 API 将直接对匿名开放。
/// </para>
///
/// <para>
/// 本过滤器作为兜底层，在 MVC 过滤器管线中二次校验主体已认证：
/// <list type="bullet">
/// <item>仅作用于 <c>/api</c> 面（与中间件的守卫范围一致），不干扰 <c>/evaluation</c>、<c>/health</c>、静态资源与 Blazor 页面。</item>
/// <item>尊重 <c>[AllowAnonymous]</c>：登录、平台引导、登录前公共语言等既有匿名端点照常放行。</item>
/// <item>路径判断大小写不敏感（同 AUTH-1 修复），与 ASP.NET 路由语义对齐。</item>
/// <item><b>单元测试兼容</b>：测试常直接构造 Controller 而不挂 <c>HttpContext</c>，
/// 此时 <c>HttpContext</c> 为 null，过滤器静默跳过，不改变既有测试语义。</item>
/// </list>
/// </para>
///
/// <para>
/// 注意：本层只校验「已认证」，<b>不替代</b>权限校验——细粒度权限（如
/// <c>identity:manage</c>、<c>platform:diagnostics:view</c>）仍由
/// <c>DiagnosticsAccessPolicy</c> 与各控制器内的权限检查负责。
/// </para>
/// </summary>
public sealed class ApiAuthorizationFilter : IAsyncAuthorizationFilter
{
	public Task OnAuthorizationAsync(AuthorizationFilterContext context)
	{
		var http = context.HttpContext;

		// 单元测试直调控制器时 HttpContext 为 null：静默跳过，保留既有测试语义。
		if (http is null) return Task.CompletedTask;

		// 仅兜底 /api 面；路径大小写不敏感（AUTH-1）。
		if (!http.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
			return Task.CompletedTask;

		// 显式标注 [AllowAnonymous] 的端点放行：优先读取 ActionDescriptor 的终结点元数据
		// （同时覆盖控制器级与 Action 级特性，如 PlatformBootstrapController 的类级标注），
		// 再回退到路由终结点元数据。
		foreach (var metadata in context.ActionDescriptor.EndpointMetadata)
			if (metadata is IAllowAnonymous)
				return Task.CompletedTask;

		var endpoint = http.GetEndpoint();
		if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
			return Task.CompletedTask;

		// AuthMiddleware 校验通过后会写入已认证主体 → 放行。
		if (http.User?.Identity?.IsAuthenticated == true)
			return Task.CompletedTask;

		// 兜底拦截：中间件若被绕过或误改，此处仍然拒绝匿名访问。
		context.Result = new ObjectResult(new ApiError
		{
			Code = ErrorCodes.Unauthorized,
			Message = "未授权：缺少或无效的访问令牌。"
		})
		{
			StatusCode = StatusCodes.Status401Unauthorized
		};

		return Task.CompletedTask;
	}
}
