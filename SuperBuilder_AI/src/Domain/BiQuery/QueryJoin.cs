namespace SuperBuilder_AI.Models.BI;

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
	/// 左侧表语义文本（业务语义，如"入库单"）。
	/// 物理表名仍以 LeftTableName 为准用于 SQL 生成；本字段仅供
	/// QueryPlan Evaluation 的 Join Contract 语义匹配使用。
	/// </summary>
	public string? LeftTableSemanticText
	{
		get;
		set;
	}

	/// <summary>
	/// 右侧表语义文本（业务语义，如"供应商"）。
	/// </summary>
	public string? RightTableSemanticText
	{
		get;
		set;
	}

	/// <summary>
	/// 左侧字段语义文本（可选；缺省时 Evaluation 回退使用 LeftColumnName）。
	/// </summary>
	public string? LeftColumnSemanticText
	{
		get;
		set;
	}

	/// <summary>
	/// 右侧字段语义文本（可选；缺省时 Evaluation 回退使用 RightColumnName）。
	/// </summary>
	public string? RightColumnSemanticText
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