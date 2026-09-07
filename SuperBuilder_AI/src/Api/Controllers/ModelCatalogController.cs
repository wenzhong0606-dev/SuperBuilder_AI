using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Application.ModelAccounts;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 模型目录端点（M7-07）。
///
/// <para>
/// <c>GET /api/model-catalog</c>：返回可选模型清单（能力驱动）。目录当前为内置静态种子（<see cref="ModelCatalogProvider"/>），
/// 实体化推迟至 M10-02 BYO。前端据此驱动绑定下拉与默认模型选择，消除硬编码。
/// </para>
///
/// <para>目录为平台级参考数据（无租户隔离语义），对所有已认证请求可见。</para>
/// </summary>
[ApiController]
[Route("api/model-catalog")]
public sealed class ModelCatalogController : ControllerBase
{
	/// <summary>返回模型目录清单。</summary>
	[HttpGet]
	public IActionResult List() => Ok(ModelCatalogProvider.GetCatalog());
}
