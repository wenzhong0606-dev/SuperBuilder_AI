namespace SuperBuilder_AI.Models;

/// <summary>
/// 基础实体
/// </summary>
public abstract class BaseEntity
{

	/// <summary>
	/// 主键
	/// </summary>
	public long Id { get; set; }


	/// <summary>
	/// 创建时间
	/// </summary>
	public DateTime CreatedTime { get; set; }
		= DateTime.Now;

}