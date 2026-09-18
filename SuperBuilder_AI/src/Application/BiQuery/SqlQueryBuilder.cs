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
///
/// §10.5：按 MetadataTableId 为每张表分配稳定唯一别名（t0/t1…），FROM/JOIN 使用
/// 三键物理限定名（<see cref="ISqlDialect.QualifyTable"/>），SELECT/WHERE/GROUP BY/
/// ORDER BY 字段统一绑定到表别名；同名跨 schema/跨库表以不同 Id 分配不同别名，不串表。
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

        // 按表在计划中的顺序分配稳定唯一别名（t0, t1, …）。
        // 别名只由服务端生成，绝不接受模型或用户提供的任意 SQL 片段。
        var aliasByRef = new Dictionary<QueryTable, string>();
        for (var i = 0; i < validTables.Count; i++)
            aliasByRef[validTables[i]] = "t" + i;
        // 供按 MetadataTableId 的 O(1) 查找（legacy MetadataTableId==0 不入字典，靠表名/索引兜底）。
        var aliasById = validTables
            .Where(t => t.MetadataTableId > 0)
            .ToDictionary(t => t.MetadataTableId, t => aliasByRef[t]);

        var joins = NormalizeJoins(plan, validTables, dialect);
        var sql = new StringBuilder("SELECT ");
        var parameters = new Dictionary<string, object?>();

        var selectFields = BuildSelectFields(plan, dialect, joins, validTables, aliasById, aliasByRef);
        if (selectFields.Count == 0)
            selectFields.Add("*");
        sql.Append(string.Join(", ", selectFields));

        // FROM：三键物理限定名 + 主表别名。
        var mainTable = validTables[0];
        sql.Append(" FROM ");
        sql.Append(dialect.QualifyTable(mainTable.CatalogName, mainTable.SchemaName, mainTable.TableName!));
        sql.Append(" AS ");
        sql.Append(aliasByRef[mainTable]);

        foreach (var join in joins)
        {
            var left = ResolveJoinEndpoint(join.LeftTableId, join.LeftTableName, validTables, aliasById, aliasByRef, "Left");
            var right = ResolveJoinEndpoint(join.RightTableId, join.RightTableName, validTables, aliasById, aliasByRef, "Right");

            sql.Append(' ');
            sql.Append(join.JoinType);
            sql.Append(" JOIN ");
            sql.Append(dialect.QualifyTable(right.CatalogName, right.SchemaName, right.TableName!));
            sql.Append(" AS ");
            sql.Append(aliasByRef[right]);
            sql.Append(" ON ");
            sql.Append(dialect.EscapeIdentifier(aliasByRef[left]));
            sql.Append('.');
            sql.Append(dialect.EscapeIdentifier(join.LeftColumnName!));
            sql.Append(" = ");
            sql.Append(dialect.EscapeIdentifier(aliasByRef[right]));
            sql.Append('.');
            sql.Append(dialect.EscapeIdentifier(join.RightColumnName!));
        }

        BuildWhere(sql, parameters, plan, dialect, joins, validTables, aliasById, aliasByRef);
        BuildGroupBy(sql, plan, dialect, joins, validTables, aliasById, aliasByRef);
        BuildOrderBy(sql, plan, dialect, joins, validTables, aliasById, aliasByRef);

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
    /// 以 MetadataTableId 为主键校验；仅对历史无 Id 的计划在三键唯一时兼容按名解析，
    /// 三键仍不唯一则拒绝（同名跨 schema 的不同 Id 不误判为同一张表）。
    /// </summary>
    private static List<QueryJoin> NormalizeJoins(
        QueryPlan plan,
        List<QueryTable> tables,
        ISqlDialect dialect)
    {
        if (plan.Joins.Count == 0)
            return new List<QueryJoin>();

        var result = new List<QueryJoin>();
        foreach (var join in plan.Joins)
        {
            if (join == null ||
                string.IsNullOrWhiteSpace(join.LeftColumnName) ||
                string.IsNullOrWhiteSpace(join.RightColumnName))
            {
                continue;
            }

            var left = ResolveJoinEndpoint(join.LeftTableId, join.LeftTableName, tables, null, null, "Left");
            var right = ResolveJoinEndpoint(join.RightTableId, join.RightTableName, tables, null, null, "Right");

            if (left is null || right is null)
            {
                throw new InvalidOperationException(
                    "QueryPlan中的JOIN引用了未声明的Metadata表。");
            }

            if (left.MetadataTableId == right.MetadataTableId &&
                string.Equals(left.TableName, right.TableName, StringComparison.OrdinalIgnoreCase))
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

    /// <summary>
    /// 解析 JOIN 端点对应的 <see cref="QueryTable"/>。优先按 MetadataTableId；
    /// legacy（Id==0）按表名解析，名字在计划内不唯一则抛歧义错误。
    /// <paramref name="aliasById"/> / <paramref name="aliasByRef"/> 为 null 时仅校验存在性（NormalizeJoins 阶段）。
    /// </summary>
    private static QueryTable? ResolveJoinEndpoint(
        long tableId,
        string? tableName,
        List<QueryTable> tables,
        Dictionary<long, string>? aliasById,
        Dictionary<QueryTable, string>? aliasByRef,
        string side)
    {
        if (tableId > 0)
        {
            var byId = tables.FirstOrDefault(t => t.MetadataTableId == tableId);
            if (byId is not null)
                return byId;
            if (!string.IsNullOrWhiteSpace(tableName))
            {
                // Id 未命中但给了名字：按名兜底。
                var byName = tables.Where(t =>
                    string.Equals(t.TableName, tableName, StringComparison.OrdinalIgnoreCase)).ToList();
                if (byName.Count == 1)
                    return byName[0];
                if (byName.Count == 0)
                    return null;
                throw new InvalidOperationException(
                    $"QueryPlan 中的 JOIN {side} 端表名『{tableName}』在计划内不唯一，存在歧义，无法解析物理表。");
            }
            return null;
        }

        if (string.IsNullOrWhiteSpace(tableName))
            return null;

        var matches = tables.Where(t =>
            string.Equals(t.TableName, tableName, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 1)
            return matches[0];
        if (matches.Count == 0)
            return null;
        throw new InvalidOperationException(
            $"QueryPlan 中的 JOIN {side} 端表名『{tableName}』在计划内不唯一，存在歧义，无法解析物理表。");
    }

    private static List<string> BuildSelectFields(
        QueryPlan plan,
        ISqlDialect dialect,
        List<QueryJoin> joins,
        List<QueryTable> tables,
        Dictionary<long, string> aliasById,
        Dictionary<QueryTable, string> aliasByRef)
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
                aliasById,
                aliasByRef,
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
        Dictionary<long, string> aliasById,
        Dictionary<QueryTable, string> aliasByRef,
        ISqlDialect dialect,
        bool allowMainTableFallback = false)
    {
        var alias = ResolveAlias(
            metadataTableId,
            tableName,
            metadataColumnId,
            joins,
            tables,
            aliasById,
            aliasByRef,
            allowMainTableFallback);

        if (!string.IsNullOrWhiteSpace(alias))
            return dialect.EscapeIdentifier(alias) + "." +
                   dialect.EscapeIdentifier(columnName);

        return dialect.EscapeIdentifier(columnName);
    }

    /// <summary>
    /// 解析字段归属表的别名（§10.5 #3）。
    ///
    /// <para>优先级：MetadataTableId → Join 端点表 Id → 表名（唯一）→ 主表兜底（仅条件/排序位）。
    /// 存在同名列却无法唯一对应表 Id 时抛歧义错误，不猜测默认 schema。</para>
    /// </summary>
    private static string? ResolveAlias(
        long metadataTableId,
        string? tableName,
        long metadataColumnId,
        List<QueryJoin> joins,
        List<QueryTable> tables,
        Dictionary<long, string> aliasById,
        Dictionary<QueryTable, string> aliasByRef,
        bool allowMainTableFallback = false)
    {
        if (metadataTableId > 0 && aliasById.TryGetValue(metadataTableId, out var a))
            return a;

        foreach (var join in joins)
        {
            if (join.LeftColumnId == metadataColumnId &&
                aliasById.TryGetValue(join.LeftTableId, out var la))
                return la;
            if (join.RightColumnId == metadataColumnId &&
                aliasById.TryGetValue(join.RightTableId, out var ra))
                return ra;
        }

        if (!string.IsNullOrWhiteSpace(tableName))
        {
            var byName = tables.Where(t =>
                string.Equals(t.TableName, tableName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (byName.Count == 1)
                return aliasByRef[byName[0]];
            if (byName.Count > 1)
                throw new InvalidOperationException(
                    $"字段『{tableName}』在计划内存在同名表（跨 schema/跨库），无法唯一确定归属表，已拒绝以避免串表。");
        }

        if (tables.Count == 1)
            return aliasByRef[tables[0]];

        return allowMainTableFallback
            ? aliasByRef[tables[0]]
            : null;
    }

    private static void BuildWhere(
        StringBuilder sql,
        Dictionary<string, object?> parameters,
        QueryPlan plan,
        ISqlDialect dialect,
        List<QueryJoin> joins,
        List<QueryTable> tables,
        Dictionary<long, string> aliasById,
        Dictionary<QueryTable, string> aliasByRef)
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
                aliasById,
                aliasByRef,
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
				var predicate = BuildMandatoryPredicate(filter, dialect, parameters, ref rlsIndex, tables, aliasById, aliasByRef);
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
		ref int parameterIndex,
		List<QueryTable> tables,
		Dictionary<long, string> aliasById,
		Dictionary<QueryTable, string> aliasByRef)
	{
		if (string.IsNullOrWhiteSpace(filter.TableName) || string.IsNullOrWhiteSpace(filter.Field))
			throw new InvalidOperationException("RLS 策略缺少物理表或字段绑定。");
		var alias = ResolveAlias(filter.MetadataTableId, filter.TableName, 0, new(), tables, aliasById, aliasByRef, allowMainTableFallback: true);
		var field = (alias is not null ? dialect.EscapeIdentifier(alias) + "." : "") +
		            dialect.EscapeIdentifier(filter.Field);
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
        List<QueryTable> tables,
        Dictionary<long, string> aliasById,
        Dictionary<QueryTable, string> aliasByRef)
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
                aliasById,
                aliasByRef,
                dialect,
                allowMainTableFallback: true));
        }

        if (groups.Count == 0 && plan.Intent?.Dimensions != null)
        {
            foreach (var dimension in plan.Intent.Dimensions)
            {
                if (!string.IsNullOrWhiteSpace(dimension))
                    groups.Add(QualifyIntentField(dimension, tables, aliasByRef, dialect));
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
        List<QueryTable> tables,
        Dictionary<long, string> aliasById,
        Dictionary<QueryTable, string> aliasByRef)
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
                aliasById,
                aliasByRef,
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
           .Append(QualifyIntentField(plan.Intent.OrderBy, tables, aliasByRef, dialect))
           .Append(' ')
           .Append(NormalizeOrderDirection(plan.Intent.OrderDirection));
    }

    /// <summary>
    /// Intent 承载的字段（OrderBy / Dimensions）只有名称、无表归属。
    /// 单表场景保持裸列名不变；多表场景锚定主表别名，避免 `create_time` / `status`
    /// 这类多表同名列触发 MySQL "Column ... is ambiguous"。
    /// 口径与 <see cref="ResolveAlias"/> 的多表兜底一致。
    /// </summary>
    private static string QualifyIntentField(
        string field,
        List<QueryTable> tables,
        Dictionary<QueryTable, string> aliasByRef,
        ISqlDialect dialect)
        => tables.Count > 1 && aliasByRef.TryGetValue(tables[0], out var alias)
            ? dialect.EscapeIdentifier(alias) + "." +
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
