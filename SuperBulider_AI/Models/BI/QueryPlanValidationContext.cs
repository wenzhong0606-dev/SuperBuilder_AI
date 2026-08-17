using SuperBulider_AI.Models.Metadata;


namespace SuperBulider_AI.Models.BI;

/// <summary>
/// QueryPlan Metadata验证上下文。
///
/// Phase 2.1.2
///
/// 作用:
///
/// 在一次 QueryPlan 验证过程中，
/// 保存已经解析完成的 Metadata 信息。
///
/// 数据流:
///
/// QueryPlan
///     ↓
/// QueryPlanValidationContext
///     ↓
/// MetadataTable
///     ↓
/// MetadataColumn
///
/// 生命周期:
///
/// 单次查询请求。
///
/// 不持久化。
/// </summary>
public class QueryPlanValidationContext
{
	/// <summary>
	/// 当前 QueryPlan 涉及的 MetadataTable。
	///
	/// Key:
	/// MetadataTable.Id
	/// </summary>
	public Dictionary<long, MetadataTable> Tables
	{
		get;
		set;
	}
	=
	new();



	/// <summary>
	/// 当前 QueryPlan 涉及的 MetadataColumn。
	///
	/// Key:
	/// MetadataColumn.Id
	/// </summary>
	public Dictionary<long, MetadataColumn> Columns
	{
		get;
		set;
	}
	=
	new();



	/// <summary>
	/// Table -> Columns 映射。
	///
	/// Key:
	/// MetadataTable.Id
	///
	/// Value:
	/// MetadataColumn集合
	/// </summary>
	public Dictionary<long, List<MetadataColumn>> TableColumns
	{
		get;
		set;
	}
	=
	new();



	/// <summary>
	/// 获取指定表的字段。
	/// </summary>
	public IReadOnlyList<MetadataColumn> GetColumns(
		long tableId)
	{
		if (TableColumns.TryGetValue(
			tableId,
			out var columns))
		{
			return columns;
		}


		return Array.Empty<MetadataColumn>();
	}



	/// <summary>
	/// 判断字段是否存在。
	/// </summary>
	public bool ContainsColumn(
		long columnId)
	{
		return Columns.ContainsKey(
			columnId);
	}



	/// <summary>
	/// 判断表是否存在。
	/// </summary>
	public bool ContainsTable(
		long tableId)
	{
		return Tables.ContainsKey(
			tableId);
	}
}