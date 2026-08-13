namespace SuperBulider_AI.Models.AI;

/// <summary>
/// 动态JOIN候选。
///
/// 用于描述两个Metadata字段之间潜在的业务连接关系。
///
/// 注意:
///
/// 该对象不是数据库Foreign Key。
///
/// 动态数据库通常不存在可靠的外键定义，
/// 因此JOIN关系由以下信息综合推断:
///
/// 1. 字段名称
/// 2. 数据类型
/// 3. 表名称
/// 4. MetadataSemantic
/// 5. Embedding语义相似度
///
/// 最终只有通过置信度判断的候选才会进入QueryPlan.Joins。
/// </summary>
public class QueryJoinCandidate
{
	/// <summary>
	/// 左侧Metadata表Id。
	/// </summary>
	public long LeftTableId
	{
		get;
		set;
	}

	/// <summary>
	/// 左侧Metadata字段Id。
	/// </summary>
	public long LeftColumnId
	{
		get;
		set;
	}

	/// <summary>
	/// 右侧Metadata表Id。
	/// </summary>
	public long RightTableId
	{
		get;
		set;
	}

	/// <summary>
	/// 右侧Metadata字段Id。
	/// </summary>
	public long RightColumnId
	{
		get;
		set;
	}

	/// <summary>
	/// 左侧表名。
	/// </summary>
	public string? LeftTableName
	{
		get;
		set;
	}

	/// <summary>
	/// 左侧字段名。
	/// </summary>
	public string? LeftColumnName
	{
		get;
		set;
	}

	/// <summary>
	/// 右侧表名。
	/// </summary>
	public string? RightTableName
	{
		get;
		set;
	}

	/// <summary>
	/// 右侧字段名。
	/// </summary>
	public string? RightColumnName
	{
		get;
		set;
	}

	/// <summary>
	/// JOIN置信度。
	///
	/// 范围:
	///
	/// 0 - 1
	/// </summary>
	public double Confidence
	{
		get;
		set;
	}

	/// <summary>
	/// JOIN推断原因。
	/// </summary>
	public string? Reason
	{
		get;
		set;
	}
}