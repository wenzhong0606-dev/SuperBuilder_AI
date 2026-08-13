using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SuperBulider_AI.Interfaces;


namespace SuperBulider_AI.Services;


/// <summary>
/// 千问 AI 服务的实现类，负责与外部千问模型接口交互以生成 SQL。
/// 实现 <see cref="IQwenService"/> 接口。
/// </summary>
/// <remarks>
/// 通过注入的 <see cref="HttpClient"/> 发起 HTTP 请求，使用配置项（Qwen:ApiKey、Qwen:Model、Qwen:Endpoint）控制行为。
/// 返回值为 AI 生成的 SQL 文本（已清理格式）。
/// </remarks>
public class QwenService
	: IQwenService
{


	/// <summary>
	/// 用于发送 HTTP 请求到千问模型的客户端（通过 DI 注入）。
	/// </summary>
	private readonly HttpClient _httpClient;

	/// <summary>
	/// 应用配置，用于读取千问相关配置（ApiKey/Model/Endpoint）。
	/// </summary>
	private readonly IConfiguration _configuration;



	/// <summary>
	/// 创建 <see cref="QwenService"/> 实例。
	/// </summary>
	/// <param name="httpClient">用于发送 HTTP 请求的 <see cref="HttpClient"/>，由依赖注入提供并可被配置。</param>
	/// <param name="configuration">应用配置，用于读取千问服务相关设置。</param>
	public QwenService(
		HttpClient httpClient,
		IConfiguration configuration)
	{

		_httpClient = httpClient;

		_configuration = configuration;

	}



	public async Task<string>
		GenerateSqlAsync(string prompt)
	{


		var apiKey =
		_configuration["Qwen:ApiKey"];



		var model =
		_configuration["Qwen:Model"];



		var request =
		new
		{

			model = model,

			messages = new[]
			{
				new { role = "system", content = "You are a helpful assistant." },
				new { role = "user", content = prompt }
			}

		};



		var json =
		JsonSerializer.Serialize(
			request);



		var content =
		new StringContent(
			json,
			Encoding.UTF8,
			"application/json");



		_httpClient.DefaultRequestHeaders
			.Authorization =
			new AuthenticationHeaderValue(
				"Bearer",
				apiKey);



		var response =
		await _httpClient.PostAsync(
			_configuration["Qwen:Endpoint"],
			content);



		response.EnsureSuccessStatusCode();



		var result =
		await response.Content
		.ReadAsStringAsync();



		return ParseSql(result);

	}

	/// <summary>
	/// 解析千问 API 返回的 JSON 响应，从中抽取模型生成的消息内容（SQL）。
	/// </summary>
	/// <param name="response">千问 API 返回的原始 JSON 字符串。</param>
	/// <returns>提取出的 SQL 文本（未经完全清理）。</returns>
	private string ParseSql(
		string response)
	{

		using var doc =
			JsonDocument.Parse(response);



		var sql =
			doc.RootElement
			.GetProperty("choices")
			.EnumerateArray()
			.First()
			.GetProperty("message")
			.GetProperty("content")
			.GetString();



		return CleanSql(sql ?? string.Empty);

	}


	/// <summary>
	/// 清理AI返回内容
	/// 
	/// 处理:
	/// 1. Markdown代码块
	/// 2. json/sql标记
	/// 3. 前后空白
	/// </summary>
	private string CleanSql(string text)
	{

		if (string.IsNullOrWhiteSpace(text))
		{
			return string.Empty;
		}



		text =
			text.Trim();



		// 移除 Markdown代码块

		if (text.StartsWith("```"))
		{

			var firstLineEnd =
				text.IndexOf(
					'\n');


			if (firstLineEnd > 0)
			{

				text =
					text.Substring(
						firstLineEnd + 1);

			}


			var last =
				text.LastIndexOf(
					"```");


			if (last >= 0)
			{

				text =
					text.Substring(
						0,
						last);

			}

		}



		// 移除 json 标识

		if (text.StartsWith("json",
			StringComparison.OrdinalIgnoreCase))
		{

			text =
				text.Substring(4)
				.Trim();

		}



		// 移除 sql 标识

		if (text.StartsWith("sql",
			StringComparison.OrdinalIgnoreCase))
		{

			text =
				text.Substring(3)
				.Trim();

		}



		return text.Trim();

	}

}
