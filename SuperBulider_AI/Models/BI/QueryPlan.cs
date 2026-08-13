namespace SuperBulider_AI.Models.AI;

/// <summary>
/// 查询执行计划。
///
/// QueryIntent:
///
/// 用户语言理解层。
///
/// QueryPlan:
///
/// 数据执行层。
///
/// </summary>
public class QueryPlan
{


	/// <summary>
	/// 原始意图。
	/// </summary>
	public QueryIntent? Intent
	{
		get;
		set;
	}




	/// <summary>
	/// 数据源Id。
	/// </summary>
	public long DataSourceId
	{
		get;
		set;
	}




	/// <summary>
	/// 查询表集合。
	/// </summary>
	public List<QueryTable> Tables
	{
		get;
		set;
	}
	=
	new();





	/// <summary>
	/// 查询字段集合。
	/// </summary>
	public List<QueryField> Fields
	{
		get;
		set;
	}
	=
	new();



	/// <summary>
	/// 过滤条件。
	/// </summary>
	public List<QueryFilter> Filters
	{
		get;
		set;
	}
	=
	new();



	/// <summary>
	/// 是否需要聚合。
	/// </summary>
	public bool IsAggregate
	{
		get;
		set;
	}


	/// <summary>
	/// 查询表连接集合。
	///
	/// JOIN关系不是数据库外键关系，
	/// 而是本次查询过程中根据Metadata和AI推断得到的连接计划。
	/// </summary>
	public List<QueryJoin> Joins
	{
		get;
		set;
	}
	=
	new();
}
