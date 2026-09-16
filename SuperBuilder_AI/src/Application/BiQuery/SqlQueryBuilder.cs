using System.Text;
using SuperBuilder_AI.Infrastructure.Database;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 根据 QueryPlan 和数据库方言生成动态 SQL。
///
/// D14 Runtime JOIN Contract：
/// 只有 QueryPlan 明确提供 Joins 时才生成 JOIN；
/// 不根据“物料”“供应商”等业务词硬编码任何表或字段。
/// Metadata 中不存在可验证关系时，QueryPlan 不应包含 Join，
/// 此处保持单表 SQL，不主动推断关系。
/// </summary>
public class SqlQueryBuilder : ISqlQueryBuilder
{
    public Task<SqlQuery> BuildAsync(QueryPlan plan, ISqlDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(dialect);

        if (plan.Tables.Count == 0)
            throw new InvalidOperationException("QueryPlan没有查询表。");

        var validTables = plan.Tables
            .Where(t => !string.IsNullOrWhiteSpace(t.TableName))
            .ToList();

        if (validTables.Count == 0)
            throw new InvalidOperationException("QueryPlan中的TableName不能为空。");

        var joins = NormalizeJoins(plan, validTables, dialect);
        var sql = new StringBuilder("SELECT ");
        var parameters = new Dictionary<string, object?>();

        var selectFields = BuildSelectFields(plan, dialect, joins, validTables);
        if (selectFields.Count == 0)
            selectFields.Add("*");
        sql.Append(string.Join(", ", selectFields));

        sql.Append(" FROM ");
        sql.Append(dialect.EscapeIdentifier(validTables[0].TableName!));

        foreach (var join in joins)
        {
            sql.Append(' ');
            sql.Append(join.JoinType);
            sql.Append(" JOIN ");
            sql.Append(dialect.EscapeIdentifier(join.RightTableName!));
            sql.Append(" ON ");
            sql.Append(dialect.EscapeIdentifier(join.LeftTableName!));
            sql.Append('.');
            sql.Append(dialect.EscapeIdentifier(join.LeftColumnName!));
            sql.Append(" = ");
            sql.Append(dialect.EscapeIdentifier(join.RightTableName!));
            sql.Append('.');
            sql.Append(dialect.EscapeIdentifier(join.RightColumnName!));
        }

        BuildWhere(sql, parameters, plan, dialect, joins, validTables);
        BuildGroupBy(sql, plan, dialect, joins, validTables);
        BuildOrderBy(sql, plan, dialect, joins, validTables);

        var finalSql = sql.ToString();
        var limit = ResolveLimit(plan);
        if (limit.HasValue)
        {
            if (limit.Value <= 0)
                throw new InvalidOperationException("查询Limit必须大于0。");
            finalSql = dialect.ApplyLimit(finalSql, limit.Value);
        }

        return Task.FromResult(new SqlQuery
        {
            Sql = finalSql,
            Parameters = parameters
        });
    }

    /// <summary>
    /// 仅接受 QueryPlan 已明确声明、且左右表/字段均存在于本次计划中的 Join。
    /// 绝不根据业务词或表名自动补 Join。
    /// </summary>
    private static List<QueryJoin> NormalizeJoins(
        QueryPlan plan,
        List<QueryTable> tables,
        ISqlDialect dialect)
    {
        if (plan.Joins.Count == 0)
            return new List<QueryJoin>();

        var tableNames = new HashSet<string>(
            tables.Select(t => t.TableName!),
            StringComparer.OrdinalIgnoreCase);

        var result = new List<QueryJoin>();
        foreach (var join in plan.Joins)
        {
            if (join == null ||
                string.IsNullOrWhiteSpace(join.LeftTableName) ||
                string.IsNullOrWhiteSpace(join.LeftColumnName) ||
                string.IsNullOrWhiteSpace(join.RightTableName) ||
                string.IsNullOrWhiteSpace(join.RightColumnName))
            {
                continue;
            }

            if (!tableNames.Contains(join.LeftTableName) ||
                !tableNames.Contains(join.RightTableName))
            {
                throw new InvalidOperationException(
                    "QueryPlan中的JOIN引用了未声明的Metadata表。");
            }

            if (string.Equals(join.LeftTableName, join.RightTableName,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "QueryPlan中的JOIN不能连接同一张表。");
            }

            var type = (join.JoinType ?? "INNER").Trim().ToUpperInvariant();
            if (type != "INNER" && type != "LEFT" && type != "RIGHT")
                throw new InvalidOperationException($"不支持的JOIN类型：{join.JoinType}");

            join.JoinType = type;
            result.Add(join);
        }

        return result;
    }

    private static List<string> BuildSelectFields(
        QueryPlan plan,
        ISqlDialect dialect,
        List<QueryJoin> joins,
        List<QueryTable> tables)
    {
        var result = new List<string>();
        foreach (var field in plan.Fields)
        {
            if (string.IsNullOrWhiteSpace(field.ColumnName))
                continue;

            var column = QualifyColumn(
                field.MetadataTableId,
                field.TableName,
                field.MetadataColumnId,
                field.ColumnName,
                joins,
                tables,
                dialect);

            var aggregation = NormalizeAggregation(field.Aggregation);
            if (aggregation == "NONE")
            {
                result.Add(column);
                continue;
            }

            if (aggregation == "COUNT" && field.ColumnName == "*")
                result.Add("COUNT(*)");
            else
                result.Add($"{aggregation}({column})");
        }

        return result;
    }

    private static string QualifyColumn(
        long metadataTableId,
        string? tableName,
        long metadataColumnId,
        string columnName,
        List<QueryJoin> joins,
        List<QueryTable> tables,
        ISqlDialect dialect,
        bool allowMainTableFallback = false)
    {
        var resolvedTableName =
            ResolveTableName(
                metadataTableId,
                tableName,
                tables,
                allowMainTableFallback);

        if (!string.IsNullOrWhiteSpace(resolvedTableName))
        {
            return dialect.EscapeIdentifier(resolvedTableName) + "." +
                   dialect.EscapeIdentifier(columnName);
        }

        foreach (var join in joins)
        {
            if (join.LeftColumnId == metadataColumnId &&
                !string.IsNullOrWhiteSpace(join.LeftTableName))
                return dialect.EscapeIdentifier(join.LeftTableName) + "." +
                       dialect.EscapeIdentifier(columnName);

            if (join.RightColumnId == metadataColumnId &&
                !string.IsNullOrWhiteSpace(join.RightTableName))
                return dialect.EscapeIdentifier(join.RightTableName) + "." +
                       dialect.EscapeIdentifier(columnName);
        }

        return dialect.EscapeIdentifier(columnName);
    }

    /// <summary>
    /// 解析字段的物理归属表名；无法解析时返回 null（调用方退化为裸列名）。
    ///
    /// <para>
    /// <paramref name="allowMainTableFallback"/> 只在 WHERE / ORDER BY / GROUP BY
    /// 这类「条件与排序」位置开启。多表（JOIN）场景下无表归属的字段会以裸列名落进 SQL，
    /// 而 <c>del_flag</c> / <c>create_time</c> / <c>status</c> 这类多表同名列会直接触发
    /// MySQL "Column 'x' in where clause is ambiguous"（实测 1052，场景：
    /// pms_complete_storage INNER JOIN pms_complete_storage_info 的软删除过滤）。
    /// </para>
    ///
    /// <para>
    /// 开启时锚定主表（<c>plan.Tables[0]</c>，即事实表），口径与
    /// <c>QueryPlanMetadataValidator.FindColumns</c> 的「主表优先」歧义消解规则一致：
    /// 该规则已用于消解只承载字段名、无 MetadataColumnId 的 QueryFilter / QueryMetric
    /// 在多表场景下的伪歧义，SqlQueryBuilder 此前未同步该口径。
    /// </para>
    ///
    /// <para>
    /// SELECT 投影刻意不开启（保持既有契约：无法解析即裸列名），避免改变既有投影行为。
    /// </para>
    /// </summary>
    private static string? ResolveTableName(
        long metadataTableId,
        string? tableName,
        List<QueryTable> tables,
        bool allowMainTableFallback = false)
    {
        if (!string.IsNullOrWhiteSpace(tableName))
            return tableName;

        if (metadataTableId > 0)
        {
            var table = tables.FirstOrDefault(
                t => t.MetadataTableId == metadataTableId);

            if (!string.IsNullOrWhiteSpace(table?.TableName))
                return table.TableName;
        }

        if (tables.Count == 1)
            return tables[0].TableName;

        return allowMainTableFallback
            ? tables[0].TableName
            : null;
    }

    private static void BuildWhere(
        StringBuilder sql,
        Dictionary<string, object?> parameters,
        QueryPlan plan,
        ISqlDialect dialect,
        List<QueryJoin> joins,
        List<QueryTable> tables)
    {
        if (plan.Filters.Count == 0 && plan.MandatoryRowFilters.Count == 0)
            return;

        var conditions = new List<string>();
        for (var i = 0; i < plan.Filters.Count; i++)
        {
            var filter = plan.Filters[i];
            if (string.IsNullOrWhiteSpace(filter.Field))
                continue;

            var field = QualifyColumn(
                filter.MetadataTableId,
                filter.TableName,
                filter.MetadataColumnId,
                filter.Field,
                joins,
                tables,
                dialect,
                allowMainTableFallback: true);
            var operation = NormalizeOperator(filter.Operator);

            if (operation == "IS NULL" || operation == "IS NOT NULL")
            {
                conditions.Add($"{field} {operation}");
                continue;
            }

            if (operation == "IN")
            {
                var values = ParseInValues(filter.Value);
                if (values.Count == 0)
                    continue;

                var names = new List<string>();
                for (var j = 0; j < values.Count; j++)
                {
                    var name = dialect.GetParameterName(i * 1000 + j);
                    names.Add(name);
                    parameters[name] = values[j];
                }
                conditions.Add($"{field} IN ({string.Join(", ", names)})");
                continue;
            }

            var parameterName = dialect.GetParameterName(i);
            var dataType = filter.DataType ?? plan.Fields.FirstOrDefault(f =>
                string.Equals(f.ColumnName, filter.Field,
                    StringComparison.OrdinalIgnoreCase))?.DataType;

            conditions.Add($"{field} {operation} {parameterName}");
            parameters[parameterName] = ConvertParameterValue(filter.Value, dataType);
        }

		// P0-06: security filters live in a separate collection and are always ANDed
		// with user conditions. Applicable Allow policies on one table are ORed;
		// Deny policies are enforced as NOT predicates and cannot be removed by input.
		var rlsIndex = 100000;
		foreach (var group in plan.MandatoryRowFilters.GroupBy(x => x.MetadataTableId))
		{
			var allow = new List<string>();
			var deny = new List<string>();
			foreach (var filter in group)
			{
				var predicate = BuildMandatoryPredicate(filter, dialect, parameters, ref rlsIndex);
				if (filter.Deny) deny.Add($"NOT ({predicate})"); else allow.Add(predicate);
			}
			if (allow.Count == 0)
				throw new InvalidOperationException("RLS 治理表缺少适用的 Allow 条件。");
			conditions.Add($"({string.Join(" OR ", allow)})");
			conditions.AddRange(deny);
		}

        if (conditions.Count > 0)
            sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
    }

	private static string BuildMandatoryPredicate(
		MandatoryRowFilter filter,
		ISqlDialect dialect,
		Dictionary<string, object?> parameters,
		ref int parameterIndex)
	{
		if (string.IsNullOrWhiteSpace(filter.TableName) || string.IsNullOrWhiteSpace(filter.Field))
			throw new InvalidOperationException("RLS 策略缺少物理表或字段绑定。");
		var field = dialect.EscapeIdentifier(filter.TableName) + "." + dialect.EscapeIdentifier(filter.Field);
		var operation = NormalizeMandatoryOperator(filter.Operator);
		if (operation is "IS NULL" or "IS NOT NULL") return $"{field} {operation}";
		if (operation == "IN")
		{
			var values = ParseInValues(filter.Value);
			if (values.Count == 0) throw new InvalidOperationException("RLS IN 策略值不能为空。");
			var names = new List<string>();
			foreach (var value in values)
			{
				var name = dialect.GetParameterName(parameterIndex++);
				names.Add(name);
				parameters[name] = ConvertParameterValue(value, filter.DataType);
			}
			return $"{field} IN ({string.Join(", ", names)})";
		}
		var parameter = dialect.GetParameterName(parameterIndex++);
		parameters[parameter] = ConvertParameterValue(filter.Value, filter.DataType);
		return $"{field} {operation} {parameter}";
	}

	private static string NormalizeMandatoryOperator(string? value) =>
		(value ?? string.Empty).Trim().ToUpperInvariant() switch
		{
			"=" => "=", "<>" => "<>", "!=" => "<>", ">" => ">", ">=" => ">=", "<" => "<", "<=" => "<=",
			"LIKE" => "LIKE", "IN" => "IN", "IS NULL" => "IS NULL", "IS NOT NULL" => "IS NOT NULL",
			_ => throw new InvalidOperationException("RLS 策略包含不支持的操作符。")
		};

    private static void BuildGroupBy(
        StringBuilder sql,
        QueryPlan plan,
        ISqlDialect dialect,
        List<QueryJoin> joins,
        List<QueryTable> tables)
    {
        var groups = new List<string>();

        foreach (var dimension in plan.Dimensions)
        {
            if (dimension == null || string.IsNullOrWhiteSpace(dimension.ColumnName))
                continue;

            // MasterJoin 维度：Runtime 的 MetadataColumnId/ColumnName 锚定事实侧 FK
            // （稳定绑定契约，供 Evaluation 使用），SQL 分组/展示必须使用 Master 侧
            // Label 列（如 es_supplier_code），否则会错误地按事实表 FK 分组。
            var groupColumnId = dimension.DimensionLabelColumnId ?? dimension.MetadataColumnId;
            var groupColumnName = !string.IsNullOrWhiteSpace(dimension.DimensionLabelColumnName)
                ? dimension.DimensionLabelColumnName!
                : dimension.ColumnName;

            groups.Add(QualifyColumn(
                dimension.MetadataTableId,
                dimension.TableName,
                groupColumnId,
                groupColumnName,
                joins,
                tables,
                dialect,
                allowMainTableFallback: true));
        }

        if (groups.Count == 0 && plan.Intent?.Dimensions != null)
        {
            foreach (var dimension in plan.Intent.Dimensions)
            {
                if (!string.IsNullOrWhiteSpace(dimension))
                    groups.Add(QualifyIntentField(dimension, tables, dialect));
            }
        }

        if (groups.Count > 0)
            sql.Append(" GROUP BY ").Append(
                string.Join(", ", groups.Distinct(StringComparer.OrdinalIgnoreCase)));
    }

    private static void BuildOrderBy(
        StringBuilder sql,
        QueryPlan plan,
        ISqlDialect dialect,
        List<QueryJoin> joins,
        List<QueryTable> tables)
    {
        var expressions = new List<string>();
        foreach (var order in plan.Orders)
        {
            if (order == null || string.IsNullOrWhiteSpace(order.Field))
                continue;

            var direction = NormalizeOrderDirection(order.Direction);
            var field = QualifyColumn(
                order.MetadataTableId,
                order.TableName,
                order.MetadataColumnId,
                order.Field,
                joins,
                tables,
                dialect,
                allowMainTableFallback: true);

            if (order.IsMetric && order.Aggregation != QueryAggregation.None)
            {
                var aggregation = NormalizeAggregation(order.Aggregation.ToString());
                if (aggregation != "NONE")
                {
                    expressions.Add(
                        aggregation == "COUNT" && order.Field == "*"
                            ? $"COUNT(*) {direction}"
                            : $"{field.AggregationFallback(aggregation)} {direction}");
                    continue;
                }
            }

            expressions.Add($"{field} {direction}");
        }

        if (expressions.Count > 0)
        {
            sql.Append(" ORDER BY ").Append(string.Join(", ", expressions));
            return;
        }

        if (string.IsNullOrWhiteSpace(plan.Intent?.OrderBy))
            return;

        sql.Append(" ORDER BY ")
           .Append(QualifyIntentField(plan.Intent.OrderBy, tables, dialect))
           .Append(' ')
           .Append(NormalizeOrderDirection(plan.Intent.OrderDirection));
    }

    /// <summary>
    /// Intent 承载的字段（OrderBy / Dimensions）只有名称、无表归属。
    /// 单表场景保持裸列名不变；多表场景锚定主表，避免 `create_time` / `status`
    /// 这类多表同名列触发 MySQL "Column ... is ambiguous"。
    /// 口径与 <see cref="ResolveTableName"/> 的多表兜底一致。
    /// </summary>
    private static string QualifyIntentField(
        string field,
        List<QueryTable> tables,
        ISqlDialect dialect)
        => tables.Count > 1 && !string.IsNullOrWhiteSpace(tables[0].TableName)
            ? dialect.EscapeIdentifier(tables[0].TableName!) + "." +
              dialect.EscapeIdentifier(field)
            : dialect.EscapeIdentifier(field);

    private static string NormalizeAggregation(string? aggregation)
    {
        if (string.IsNullOrWhiteSpace(aggregation))
            return "NONE";

        return aggregation.Trim().ToUpperInvariant() switch
        {
            "SUM" => "SUM",
            "COUNT" => "COUNT",
            "AVG" => "AVG",
            "MAX" => "MAX",
            "MIN" => "MIN",
            "NONE" => "NONE",
            _ => "NONE"
        };
    }

    private static string NormalizeOperator(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "=";

        return value.Trim().ToUpperInvariant() switch
        {
            "=" => "=",
            ">" => ">",
            "<" => "<",
            ">=" => ">=",
            "<=" => "<=",
            "<>" => "<>",
            "!=" => "<>",
            "LIKE" => "LIKE",
            "IN" => "IN",
            "IS NULL" => "IS NULL",
            "IS NOT NULL" => "IS NOT NULL",
            _ => "="
        };
    }

    private static string NormalizeOrderDirection(string? direction) =>
        string.Equals(direction, "DESC", StringComparison.OrdinalIgnoreCase)
            ? "DESC" : "ASC";

    private static List<string> ParseInValues(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new List<string>();

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private static object? ConvertParameterValue(string? value, string? dataType)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (string.IsNullOrWhiteSpace(dataType))
            return value;

        switch (dataType.Trim().ToLowerInvariant())
        {
            case "int":
            case "integer":
            case "smallint":
            case "mediumint":
            case "bigint":
                return long.TryParse(value, out var l) ? l : value;
            case "decimal":
            case "numeric":
            case "float":
            case "double":
                return decimal.TryParse(value, out var d) ? d : value;
            case "bit":
            case "bool":
            case "boolean":
                return bool.TryParse(value, out var b) ? b : value;
            case "date":
            case "datetime":
            case "timestamp":
                return DateTime.TryParse(value, out var dt) ? dt : value;
            default:
                return value;
        }
    }

    private static int? ResolveLimit(QueryPlan plan) =>
        plan.Limit ?? plan.Intent?.Limit;
}

internal static class SqlQueryBuilderExtensions
{
    /// <summary>
    /// 保留聚合表达式的最小构造逻辑，避免将聚合名称作为可注入 SQL 片段处理。
    /// </summary>
    public static string AggregationFallback(this string escapedField, string aggregation) =>
        $"{aggregation}({escapedField})";
}
