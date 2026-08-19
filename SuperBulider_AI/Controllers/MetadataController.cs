using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Services;


namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 元数据控制器，提供元数据扫描相关的管理接口。
/// 注意：当前示例 Scan 方法使用硬编码连接字符串，仅用于测试或本地调试，生产环境应通过配置或安全存储传入。
/// </summary>
public class MetadataController : Controller
{
	private readonly MetadataScannerService _scanner;
	private readonly IMetadataPromptBuilder _builder;

	public MetadataController(MetadataScannerService scanner, IMetadataPromptBuilder builder)
	{
		_scanner = scanner;
		_builder = builder;
	}

	/// <summary>
	/// 触发元数据扫描并将结果持久化到本地数据库。
	/// </summary>
	/// <returns>操作结果内容（扫描完成）。</returns>
	[HttpGet]
	public async Task<IActionResult> Scan()
	{
		// 示例连接字符串（仅用于演示）
		var connectionString = "Server=192.168.16.120;Port=3306;Database=steccn_wms;Uid=admin;Pwd=***REMOVED***;CharSet=utf8mb4;SslMode=Required;";

		await _scanner.ScanAsync(1, 1, connectionString);

		return Content("Metadata Scan Complete");
	}

	[HttpGet("Prompt")]
	public async Task<IActionResult> Prompt(string question)
	{

		var result =
			await _builder
			.BuildAsync(question);



		return Content(
			result.Prompt);

	}
}