using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// QueryPlan 业务语义验证器。
///
/// 核心职责：
/// 1. Metric 业务语义验证
/// 2. Dimension 业务语义验证
/// 3. Filter 时间语义验证
/// 4. Aggregation 合法性验证
///
/// 注意：字段解析必须以当前 QueryPlan 已绑定的表为作用域，
/// 不允许使用全局同名字段进行错误匹配。
/// </summary>
public class QuerySemanticValidator
{
    public QuerySemanticValidationResult Validate(
        QueryPlan plan,
        QueryPlanValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(context);

        var result = new QuerySemanticValidationResult();

        ValidateMetrics(plan, context, result);
        ValidateDimensions(plan, context, result);
        ValidateFilters(plan, context, result);
        ValidateAggregations(plan, context, result);

        return result;
    }

    private static void ValidateMetrics(
        QueryPlan plan,
        QueryPlanValidationContext context,
        QuerySemanticValidationResult result)
    {
        foreach (var metric in plan.Metrics)
        {
            var column = FindColumn(metric.Field, plan, context);

            if (column == null)
            {
                result.AddError(
                    "MetricFieldNotFound",
                    metric.Field,
                    $"Metric 字段不存在:{metric.Field}");
                continue;
            }

            if (!CanAggregate(metric.SemanticType, column, metric.Aggregation))
            {
                result.AddError(
                    "InvalidMetricAggregation",
                    metric.Field,
                    $"业务语义{metric.SemanticType}不支持聚合:{metric.Aggregation}",
                    column.Id,
                    column.MetadataTableId);
            }
        }
    }

    private static void ValidateDimensions(
        QueryPlan plan,
        QueryPlanValidationContext context,
        QuerySemanticValidationResult result)
    {
        foreach (var dimension in plan.Dimensions)
        {
            var column = FindColumnById(dimension.MetadataColumnId, context);
            column ??= FindColumn(dimension.ColumnName, plan, context);

            if (column == null)
            {
                result.AddError(
                    "DimensionFieldNotFound",
                    dimension.ColumnName,
                    $"Dimension 字段不存在:{dimension.ColumnName}");
                continue;
            }

            if (IsNumeric(column))
            {
                result.AddWarning(
                    "NumericDimension",
                    dimension.ColumnName,
                    $"数值字段不建议作为Dimension:{dimension.ColumnName}",
                    column.Id,
                    column.MetadataTableId);
            }

            if (!string.IsNullOrWhiteSpace(dimension.SemanticType))
            {
                var type = dimension.SemanticType.Trim().ToUpperInvariant();
                var allowed = new[] { "DIMENSION", "ENTITY", "CATEGORY", "TIME" };

                if (!allowed.Contains(type))
                {
                    result.AddWarning(
                        "InvalidDimensionSemanticType",
                        dimension.ColumnName,
                        $"未知Dimension语义类型:{dimension.SemanticType}",
                        column.Id,
                        column.MetadataTableId);
                }
            }
        }
    }

    private static void ValidateFilters(
        QueryPlan plan,
        QueryPlanValidationContext context,
        QuerySemanticValidationResult result)
    {
        foreach (var filter in plan.Filters)
        {
            var column = FindColumn(filter.Field, plan, context);

            if (column == null)
            {
                result.AddError(
                    "FilterFieldNotFound",
                    filter.Field,
                    $"Filter 字段不存在:{filter.Field}");
                continue;
            }

            if (IsDateOperator(filter.Operator) && !IsDateColumn(column))
            {
                result.AddError(
                    "InvalidDateFilter",
                    filter.Field,
                    $"字段{filter.Field}不是日期字段",
                    column.Id,
                    column.MetadataTableId);
            }
        }
    }

    private static void ValidateAggregations(
        QueryPlan plan,
        QueryPlanValidationContext context,
        QuerySemanticValidationResult result)
    {
        foreach (var field in plan.Fields)
        {
            if (string.IsNullOrWhiteSpace(field.Aggregation)
                || field.Aggregation.Equals("NONE", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var column = FindColumn(field.ColumnName, plan, context);

            if (column == null)
            {
                continue;
            }

            if (!CanAggregate(null, column, field.Aggregation))
            {
                result.AddError(
                    "InvalidFieldAggregation",
                    field.ColumnName,
                    $"字段{field.ColumnName}不能执行聚合:{field.Aggregation}",
                    column.Id,
                    column.MetadataTableId);
            }
        }
    }

    /// <summary>
    /// 在当前 QueryPlan 已绑定的表范围内解析字段。
    ///
    /// 这是本次修复的关键：
    /// 同名字段可能存在于多个 MetadataTable，不能直接对 context.Columns
    /// 做全局 FirstOrDefault，否则例如 quantity 会被错误解析到另一张表。
    /// </summary>
    private static MetadataColumn? FindColumn(
        string? field,
        QueryPlan plan,
        QueryPlanValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(field))
            return null;

        var tableIds = plan.Tables
            .Select(x => x.MetadataTableId)
            .Where(x => x > 0)
            .Distinct()
            .ToHashSet();

        foreach (var tableId in tableIds)
        {
            if (!context.TableColumns.TryGetValue(tableId, out var columns))
                continue;

            var matched = columns.FirstOrDefault(x =>
                string.Equals(x.ColumnName, field, StringComparison.OrdinalIgnoreCase));

            if (matched != null)
                return matched;
        }

        // 兼容没有完整 TableColumns 的旧验证上下文，但只有在 QueryPlan
        // 已经没有可用表作用域时才允许全局解析。
        if (tableIds.Count == 0)
        {
            return context.Columns.Values.FirstOrDefault(x =>
                string.Equals(x.ColumnName, field, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private static MetadataColumn? FindColumnById(
        long id,
        QueryPlanValidationContext context)
    {
        if (id <= 0)
            return null;

        return context.Columns.TryGetValue(id, out var column)
            ? column
            : null;
    }

    private static bool CanAggregate(
        string? semanticType,
        MetadataColumn column,
        string? aggregation)
    {
        if (string.IsNullOrWhiteSpace(aggregation)
            || aggregation.Equals("NONE", StringComparison.OrdinalIgnoreCase))
            return true;

        var semantic = semanticType?.Trim().ToUpperInvariant();
        var normalizedAggregation = aggregation.Trim().ToUpperInvariant();

        switch (semantic)
        {
            case "AMOUNT":
            case "QUANTITY":
            case "RATIO":
                return normalizedAggregation switch
                {
                    "SUM" => true,
                    "AVG" => true,
                    "AVERAGE" => true,
                    "MAX" => true,
                    "MIN" => true,
                    "COUNT" => true,
                    _ => false
                };

            case "COUNT":
                return normalizedAggregation is "COUNT" or "DISTINCTCOUNT" or "DISTINCT_COUNT";

            case "DATE":
                return normalizedAggregation == "COUNT";
        }

        var type = column.DataType?.Trim().ToLowerInvariant() ?? string.Empty;
        var numeric = type.Contains("int")
            || type.Contains("decimal")
            || type.Contains("numeric")
            || type.Contains("float")
            || type.Contains("double")
            || type.Contains("money");

        if (numeric)
        {
            return normalizedAggregation switch
            {
                "SUM" => true,
                "AVG" => true,
                "AVERAGE" => true,
                "MAX" => true,
                "MIN" => true,
                "COUNT" => true,
                "DISTINCTCOUNT" => true,
                "DISTINCT_COUNT" => true,
                _ => false
            };
        }

        return normalizedAggregation is "COUNT" or "DISTINCTCOUNT" or "DISTINCT_COUNT";
    }

    private static bool IsNumeric(MetadataColumn column)
    {
        var type = column.DataType?.Trim().ToLowerInvariant() ?? string.Empty;

        return type.Contains("int")
            || type.Contains("decimal")
            || type.Contains("numeric")
            || type.Contains("float")
            || type.Contains("double")
            || type.Contains("money");
    }

    private static bool IsDateColumn(MetadataColumn column)
    {
        var type = column.DataType?.Trim().ToLowerInvariant() ?? string.Empty;
        return type.Contains("date") || type.Contains("time");
    }

    private static bool IsDateOperator(string? op)
    {
        if (string.IsNullOrWhiteSpace(op))
            return false;

        return op.Contains("DATE", StringComparison.OrdinalIgnoreCase)
            || op.Contains("YEAR", StringComparison.OrdinalIgnoreCase)
            || op.Contains("MONTH", StringComparison.OrdinalIgnoreCase);
    }
}