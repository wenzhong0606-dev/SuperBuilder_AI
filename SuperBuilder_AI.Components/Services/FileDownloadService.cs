using Microsoft.JSInterop;

namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 浏览器端文件下载（S3-3 导出 CSV / Excel）。
/// 通过 Blob + 临时 &lt;a download&gt; 触发下载，纯前端、无第三方依赖。
/// MAUI Hybrid WebView 内 JS 同样执行，但因沙箱限制文件可能不落盘——属已知平台限制。
/// </summary>
public sealed class FileDownloadService
{
	private readonly IJSRuntime _js;

	public FileDownloadService(IJSRuntime js) => _js = js;

	/// <summary>以指定文件名与 MIME 下载一段文本内容（如 text/csv、application/vnd.ms-excel）。</summary>
	public async Task DownloadTextAsync(string fileName, string mime, string content)
	{
		try
		{
			await _js.InvokeVoidAsync("SuperBuilder.downloadTextFile", fileName, mime, content);
		}
		catch
		{
			// 下载失败时静默失败，避免阻断主流程（调用方可改用 Toast 提示）
		}
	}
}
