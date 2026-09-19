namespace SuperBuilder_AI.Components.Services;

/// <summary>刷新调用结果（验收 #4）：与后端 <c>AuthResult</c> 的令牌字段对齐。</summary>
public sealed record RefreshCallResult(
	string AccessToken,
	string RefreshToken,
	int ExpiresInSeconds);

/// <summary>
/// 抽象「用刷新令牌换取新访问令牌」的远程调用（验收 #4），便于单测时替换为假实现。
/// Web 端实现 <see cref="HttpRefreshTokenCaller"/> 经命名 HttpClient POST <c>api/auth/refresh</c>。
/// </summary>
public interface IRefreshTokenCaller
{
	/// <summary>用给定刷新令牌赎回新令牌；网络/服务端失败时返回 <c>null</c>（不抛异常）。</summary>
	Task<RefreshCallResult?> CallRefreshAsync(string refreshToken, CancellationToken ct = default);
}
