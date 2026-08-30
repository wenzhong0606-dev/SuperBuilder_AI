using SuperBuilder_AI.Models.Agent;

namespace SuperBuilder_AI.Interfaces.Agent;

/// <summary>
/// Agent DSL 序列化与校验端口（P9 AI Agent / Copilot）。
///
/// 职责边界：
/// <list type="bullet">
/// <item>JSON ⇄ <see cref="AgentDsl"/> 的往返转换。</item>
/// <item>结构化校验：版本、标识、枚举取值、工具/维度/方向白名单、绑定完整性。</item>
/// <item><strong>红线校验</strong>：拒绝任何裸 HTML / 脚本片段混入 DSL。</item>
/// </list>
/// 不负责：把 DSL 执行成动作（后续执行器/Planner）。
/// </summary>
public interface IAgentDslSerializer
{
	/// <summary>序列化为 JSON（缩进，便于版本库可读与人工审阅）。</summary>
	string Serialize(AgentDsl dsl);

	/// <summary>
	/// 反序列化并校验；失败时返回 false 并给出全部错误（而非遇到第一个即抛异常），
	/// 便于编辑器一次性展示所有问题。
	/// </summary>
	bool TryDeserialize(string? json, out AgentDsl? dsl, out IReadOnlyList<string> errors);

	/// <summary>校验已构造的 DSL 对象（供保存前的服务端兜底）。</summary>
	IReadOnlyList<string> Validate(AgentDsl dsl);
}
