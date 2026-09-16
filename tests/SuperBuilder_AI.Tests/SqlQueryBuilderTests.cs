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
}
