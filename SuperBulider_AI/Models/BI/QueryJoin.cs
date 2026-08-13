namespace SuperBulider_AI.Models.AI;

/// <summary>
/// 查询表连接计划。
///
/// 用于描述本次查询中两个动态数据表之间的JOIN关系。
///
/// 注意:
///
/// QueryJoin不是数据库真实外键关系。
///
/// 对于动态数据库:
///
/// 数据库可能没有定义Foreign Key。
///
/// 因此JOIN关系可以来自:
///
/// 1. 字段名称
/// 2. Metadata字段业务语义
/// 3. 字段数据类型
/// 4. 表业务语义
/// 5. AI语义推理
///
/// QueryJoin只表示:
///
/// 本次QueryPlan认为应该如何连接两个表。
/// </summary>
public class QueryJoin
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
	/// 左侧字段MetadataColumnId。
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
	/// 右侧字段MetadataColumnId。
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
	/// JOIN类型。
	///
	/// INNER
	/// LEFT
	/// RIGHT
	///
	/// 当前阶段默认INNER。
	/// </summary>
	public string JoinType
	{
		get;
		set;
	}
	= "INNER";
}