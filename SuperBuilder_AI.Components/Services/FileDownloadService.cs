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
	/// <returns>下载是否成功触发；失败时返回 false（不抛异常），由调用方决定如何提示（如 Toast）。</returns>
	public async Task<bool> DownloadTextAsync(string fileName, string mime, string content)
	{
		try
		{
			await _js.InvokeVoidAsync("SuperBuilder.downloadTextFile", fileName, mime, content);
			return true;
		}
		catch (Exception)
		{
			// 不在服务内静默吞掉失败：返回 false，交由调用方显示失败 Toast（M8-03 红线：禁止假成功）。
			return false;
		}
	}
}
