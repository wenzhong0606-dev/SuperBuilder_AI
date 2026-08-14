using Microsoft.AspNetCore.Mvc;
using SuperBulider_AI.Interfaces;

namespace SuperBulider_AI.Controllers;

/// <summary>
/// Metadata Vector 索引控制器。
///
/// 用于开发阶段执行:
///
/// 1. 查看 Collection
/// 2. 创建 Collection
/// 3. 全量重建 Metadata Vector
///
/// 注意:
///
/// Rebuild 会删除当前:
///
/// superbi_metadata
///
/// 然后重新创建并重新生成全部 Vector。
///
/// 因此该接口属于管理操作。
/// 正式生产环境需要增加权限控制。
/// </summary>
[ApiController]
[Route("api/metadata-vector")]
public class MetadataVectorController
	: ControllerBase
{
	private readonly
		IMetadataVectorIndexService _service;

	private readonly
		IQdrantService _qdrant;

	/// <summary>
	/// 创建 Metadata Vector Controller。
	/// </summary>
	public MetadataVectorController(
		IMetadataVectorIndexService service,
		IQdrantService qdrant)
	{
		_service = service;
		_qdrant = qdrant;
	}

	/// <summary>
	/// 检查 Qdrant Collection 是否存在。
	///
	/// GET:
	///
	/// /api/metadata-vector/status
	/// </summary>
	[HttpGet("status")]
	public async Task<IActionResult> Status()
	{
		var exists =
			await _qdrant.ExistsAsync();

		return Ok(new
		{
			success = true,
			collectionExists = exists
		});
	}

	/// <summary>
	/// 创建 Qdrant Collection。
	///
	/// 如果已经存在则不会重复创建。
	///
	/// GET:
	///
	/// /api/metadata-vector/create
	/// </summary>
	[HttpGet("create")]
	public async Task<IActionResult> Create()
	{
		await _service.CreateAsync();

		return Ok(new
		{
			success = true,
			message =
				"Metadata Vector Collection 创建完成。"
		});
	}

	/// <summary>
	/// 全量重建 Metadata Vector。
	///
	/// 执行:
	///
	/// 1. 删除旧 Collection
	/// 2. 创建 1024 维 Cosine Collection
	/// 3. 读取全部 MetadataTable
	/// 4. 读取全部 MetadataColumn
	/// 5. 读取全部 MetadataSemantic
	/// 6. 重新生成 Embedding
	/// 7. 写入 Qdrant
	///
	/// GET:
	///
	/// /api/metadata-vector/rebuild
	/// </summary>
	[HttpGet("rebuild")]
	public async Task<IActionResult> Rebuild()
	{
		var result =
			await _service
				.RebuildAsync();

		if (!result.Success)
		{
			return StatusCode(
				StatusCodes.Status500InternalServerError,
				result);
		}

		return Ok(result);
	}
}