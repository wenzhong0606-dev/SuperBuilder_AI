using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Api.Security;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// AUTH-2 控制器层授权兜底过滤器单元测试（无 DB / 无 LLM，确定性）。
///
/// <para>
/// 过滤器作为第二道防线：即便 <c>AuthMiddleware</c> 被绕过或白名单被误改，
/// 仍应拒绝匿名访问 <c>/api</c>，同时不得影响 <c>[AllowAnonymous]</c> 端点与非 <c>/api</c> 路径。
/// </para>
/// </summary>
public sealed class ApiAuthorizationFilterTests
{
	private readonly ApiAuthorizationFilter _filter = new();

	private static AuthorizationFilterContext CreateContext(
		string path,
		ClaimsPrincipal? user = null,
		IEnumerable<object>? endpointMetadata = null)
	{
		var http = new DefaultHttpContext { Request = { Path = path } };
		if (user is not null) http.User = user;

		var actionContext = new ActionContext(
			http,
			new RouteData(),
			new ControllerActionDescriptor
			{
				// 模拟控制器 / Action 级特性（如 [AllowAnonymous]）
				EndpointMetadata = (endpointMetadata ?? Enumerable.Empty<object>()).ToList()
			});

		return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
	}

	private static ClaimsPrincipal Authenticated() =>
		new(new ClaimsIdentity(new[]
		{
			new Claim(ClaimTypes.NameIdentifier, "11"),
			new Claim("tid", "9")
		}, "Bearer"));

	[Theory]
	[InlineData("/api/ask")]
	[InlineData("/api/data-sources")]
	[InlineData("/api/tenant-management/1")]
	public async Task Api_Path_Without_Authentication_Is_Challenged(string path)
	{
		var context = CreateContext(path);

		await _filter.OnAuthorizationAsync(context);

		var result = Assert.IsType<ObjectResult>(context.Result);
		Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
		var body = Assert.IsType<ApiError>(result.Value);
		Assert.Equal(ErrorCodes.Unauthorized, body.Code);
	}

	[Fact]
	public async Task Api_Path_With_Authenticated_User_Passes()
	{
		var context = CreateContext("/api/ask", Authenticated());

		await _filter.OnAuthorizationAsync(context);

		Assert.Null(context.Result);
	}

	[Fact]
	public async Task AllowAnonymous_Endpoint_Passes_Without_Authentication()
	{
		// 覆盖登录、平台引导、公共语言等既有匿名端点（控制器级或 Action 级标注均生效）
		var context = CreateContext(
			"/api/auth/login",
			endpointMetadata: new object[] { new AllowAnonymousAttribute() });

		await _filter.OnAuthorizationAsync(context);

		Assert.Null(context.Result);
	}

	[Theory]
	[InlineData("/health")]
	[InlineData("/evaluation/golden-runtime/run")]
	[InlineData("/css/site.css")]
	public async Task NonApi_Path_Is_Not_Challenged(string path)
	{
		var context = CreateContext(path);

		await _filter.OnAuthorizationAsync(context);

		Assert.Null(context.Result);
	}

	[Theory]
	[InlineData("/API/ask")]
	[InlineData("/Api/Data-Sources")]
	public async Task Uppercase_Api_Path_Is_Challenged(string path)
	{
		// 与 AUTH-1 保持一致：路径判定大小写不敏感，避免 /API/** 变体绕过兜底。
		var context = CreateContext(path);

		await _filter.OnAuthorizationAsync(context);

		var result = Assert.IsType<ObjectResult>(context.Result);
		Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
	}
}
