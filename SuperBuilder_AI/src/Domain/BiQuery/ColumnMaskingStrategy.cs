namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// 列脱敏策略（M5-05 预留扩展点）。
///
/// 首切片聚焦"受限列不进入 Plan / SQL / 结果"（deny-by-default，对应 <see cref="ColumnMaskingStrategy.None"/>）。
/// 该枚举为后续"脱敏投影"能力保留类型基础，不进入 M5-05 首切片的行为。
/// </summary>
public enum ColumnMaskingStrategy
{
	/// <summary>不脱敏：受限且未授权列直接拦截、不进入 Plan。</summary>
	None = 0,

	/// <summary>整列置空 / 删除（完全不可见）。</summary>
	Redact,

	/// <summary>单向哈希（可分组但不可还原明文）。</summary>
	Hash,

	/// <summary>部分遮蔽（如手机号 138****8000）。</summary>
	PartialMask
}
