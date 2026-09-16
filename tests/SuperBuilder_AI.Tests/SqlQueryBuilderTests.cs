using System;
using System.Threading.Tasks;
using SuperBuilder_AI.Infrastructure.Database;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Services.BI;
using Xunit;

namespace SuperBuilder_AI.Tests;

public sealed class SqlQueryBuilderTests
{
	[Fact]
	public async Task BuildAsync_qualifies_bound_columns_across_joins()
	{
		var plan =
			new QueryPlan
			{
				Tables =
				{
					new QueryTable
					{
						MetadataTableId = 10,
						DataSourceId = 1,
						TableName = "pms_complete_storage"
					},
					new QueryTable
					{
						MetadataTableId = 11,
						DataSourceId = 1,
						TableName = "pms_complete_storage_info"
					}
				},
				Fields =
				{
					new QueryField
					{
						MetadataTableId = 10,
						TableName = "pms_complete_storage",
						MetadataColumnId = 101,
						ColumnName = "unit_id",
						Aggregation = "NONE"
					},
					new QueryField
					{
						MetadataTableId = 10,
						TableName = "pms_complete_storage",
						MetadataColumnId = 102,
						ColumnName = "code",
						Aggregation = "NONE"
					}
				},
				Filters =
				{
					new QueryFilter
					{
						MetadataTableId = 10,
						TableName = "pms_complete_storage",
						MetadataColumnId = 103,
						Field = "del_flag",
						Operator = "=",
						Value = "0"
					}
				},
				Orders =
				{
					new QueryOrder
					{
						MetadataTableId = 10,
						TableName = "pms_complete_storage",
						MetadataColumnId = 104,
						Field = "enter_time",
						Direction = "DESC"
					}
				},
				Joins =
				{
					new QueryJoin
					{
						LeftTableId = 10,
						LeftTableName = "pms_complete_storage",
						LeftColumnId = 105,
						LeftColumnName = "create_time",
						RightTableId = 11,
						RightTableName = "pms_complete_storage_info",
						RightColumnId = 205,
						RightColumnName = "create_time"
					}
				},
				Limit = 10
			};

		var query =
			await new SqlQueryBuilder()
				.BuildAsync(
					plan,
					new MySqlDialect());

		Assert.Contains("`pms_complete_storage`.`unit_id`", query.Sql);
		Assert.Contains("`pms_complete_storage`.`code`", query.Sql);
		Assert.Contains("WHERE `pms_complete_storage`.`del_flag` = @p0", query.Sql);
		Assert.Contains("ORDER BY `pms_complete_storage`.`enter_time` DESC", query.Sql);
		Assert.DoesNotContain("SELECT `unit_id`", query.Sql);
	}

	/// <summary>
	/// 回归（线上报错复现）：多表 JOIN 场景下，未绑定表归属的过滤字段必须锚定主表。
	///
	/// 明细列表运行期注入的软删除过滤此前只写 Field="del_flag"，不带 TableName /
	/// MetadataTableId；事实表与明细表都含 del_flag，裸列名直接触发
	/// MySQL "Column 'del_flag' in where clause is ambiguous"（ERROR 1052）。
	/// </summary>
	[Fact]
	public async Task BuildAsync_anchors_unbound_filter_to_main_table_across_joins()
	{
		var plan = BuildJoinPlan();

		// 模拟旧行为：仅字段名，无表归属（如未修复前的约定软删除过滤）。
		plan.Filters.Add(
			new QueryFilter
			{
				SemanticText = "未删除",
				Field = "del_flag",
				DataType = "tinyint",
				Operator = "=",
				Value = "0"
			});

		var query =
			await new SqlQueryBuilder()
				.BuildAsync(
					plan,
					new MySqlDialect());

		Assert.Contains(
			"WHERE `pms_complete_storage`.`del_flag` = @p0",
			query.Sql,
			StringComparison.Ordinal);
		Assert.DoesNotContain(
			"WHERE `del_flag`",
			query.Sql,
			StringComparison.Ordinal);
	}

	/// <summary>
	/// 回归：Intent 承载的 OrderBy / Dimensions 同样只有名称、无表归属，
	/// 多表 JOIN 时必须锚定主表，避免 `create_time` 这类同名列歧义。
	/// </summary>
	[Fact]
	public async Task BuildAsync_anchors_unbound_intent_order_and_dimension_to_main_table()
	{
		var plan = BuildJoinPlan();
		plan.Intent =
			new QueryIntent
			{
				OriginalQuestion = "查询最近十条入库记录",
				OrderBy = "create_time",
				OrderDirection = "DESC",
				Dimensions = { "status" }
			};

		var query =
			await new SqlQueryBuilder()
				.BuildAsync(
					plan,
					new MySqlDialect());

		Assert.Contains(
			"ORDER BY `pms_complete_storage`.`create_time` DESC",
			query.Sql,
			StringComparison.Ordinal);
		Assert.Contains(
			"GROUP BY `pms_complete_storage`.`status`",
			query.Sql,
			StringComparison.Ordinal);
	}

	/// <summary>
	/// 单表场景不回归：无条件限定主表，SQL 形态与既有行为一致。
	/// </summary>
	[Fact]
	public async Task BuildAsync_single_table_still_qualifies_unbound_filter()
	{
		var plan =
			new QueryPlan
			{
				Tables =
				{
					new QueryTable
					{
						MetadataTableId = 10,
						DataSourceId = 1,
						TableName = "pms_complete_storage"
					}
				},
				Fields =
				{
					new QueryField
					{
						MetadataTableId = 10,
						TableName = "pms_complete_storage",
						MetadataColumnId = 101,
						ColumnName = "code",
						Aggregation = "NONE"
					}
				},
				Filters =
				{
					new QueryFilter
					{
						Field = "del_flag",
						Operator = "=",
						Value = "0"
					}
				},
				Limit = 10
			};

		var query =
			await new SqlQueryBuilder()
				.BuildAsync(
					plan,
					new MySqlDialect());

		Assert.Contains(
			"WHERE `pms_complete_storage`.`del_flag` = @p0",
			query.Sql,
			StringComparison.Ordinal);
	}

	/// <summary>
	/// 两表 + 单 JOIN 的最小计划，字段分别绑定到两张表（无未绑定字段）。
	/// </summary>
	private static QueryPlan BuildJoinPlan()
		=> new()
		{
			Tables =
			{
				new QueryTable
				{
					MetadataTableId = 10,
					DataSourceId = 1,
					TableName = "pms_complete_storage"
				},
				new QueryTable
				{
					MetadataTableId = 11,
					DataSourceId = 1,
					TableName = "pms_complete_storage_info"
				}
			},
			Fields =
			{
				new QueryField
				{
					MetadataTableId = 10,
					TableName = "pms_complete_storage",
					MetadataColumnId = 101,
					ColumnName = "code",
					Aggregation = "NONE"
				},
				new QueryField
				{
					MetadataTableId = 11,
					TableName = "pms_complete_storage_info",
					MetadataColumnId = 201,
					ColumnName = "quantity",
					Aggregation = "NONE"
				}
			},
			Joins =
			{
				new QueryJoin
				{
					LeftTableId = 10,
					LeftTableName = "pms_complete_storage",
					LeftColumnId = 105,
					LeftColumnName = "code",
					RightTableId = 11,
					RightTableName = "pms_complete_storage_info",
					RightColumnId = 205,
					RightColumnName = "storage_code"
				}
			},
			Limit = 10
		};
}
