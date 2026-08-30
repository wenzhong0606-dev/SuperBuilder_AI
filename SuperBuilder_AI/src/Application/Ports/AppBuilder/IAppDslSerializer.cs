using SuperBuilder_AI.Models.AppBuilder;

namespace SuperBuilder_AI.Interfaces.AppBuilder;

/// <summary>
/// 应用 DSL 序列化与校验端口（P8 AI App Builder）。
///
/// 职责边界：
/// <list type="bullet">
/// <item>JSON ⇄ <see cref="AppDsl"/> 的往返转换。</item>
/// <item>结构化校验：版本、标识唯一性、枚举取值、绑定完整性。</item>
/// <item><strong>红线校验</strong>：拒绝任何裸 HTML / 脚本片段混入 DSL。</item>
/// </list>
/// 不负责：把 DSL 渲染成 UI（后续渲染器）。
/// </summary>
public interface IAppDslSerializer
{
	/// <summary>序列化为 JSON（缩进，便于版本库可读与人工审阅）。</summary>
	string Serialize(AppDsl dsl);

	/// <summary>
	/// 反序列化并校验；失败时返回 false 并给出全部错误（而非遇到第一个即抛异常），
	/// 便于编辑器一次性展示所有问题。
	/// </summary>
	bool TryDeserialize(string? json, out AppDsl? dsl, out IReadOnlyList<string> errors);

	/// <summary>校验已构造的 DSL 对象（供保存前的服务端兜底）。</summary>
	IReadOnlyList<string> Validate(AppDsl dsl);
}
