namespace SuperBuilder_AI.Services.Agent.Runtime;

/// <summary>Agent 运行时异常（M7-03），携带 HTTP 状态码供控制器映射。</summary>
public sealed class AgentRuntimeException : Exception
{
	/// <summary>对应 HTTP 状态码（默认 400）。</summary>
	public int StatusCode { get; }

	/// <summary>构造运行时异常。</summary>
	public AgentRuntimeException(string message, int statusCode = 400) : base(message)
		=> StatusCode = statusCode;
}
