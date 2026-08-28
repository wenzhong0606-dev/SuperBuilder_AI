using SuperBuilder_AI.Interfaces.BI.Entity;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Entity;

namespace SuperBuilder_AI.Services.BI.Entity;

/// <summary>
/// 将已持久化的 Business Entity + PhysicalBinding 映射为 Phase 2.7 Resolution。
/// 只使用已确认的 PhysicalBinding；不做物理字段猜测，也不生成 SQL。
/// </summary>
public sealed class EntityQueryPlanMapper : IEntityQueryPlanMapper
{
    public Task<QueryPlanSemanticResolution> MapAsync(
        BusinessEntity entity,
        QueryIntent intent,
        long dataSourceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(intent);
        cancellationToken.ThrowIfCancellationRequested();

        var bindings = entity.Keys.SelectMany(x => x.PhysicalBindings)
            .Concat(entity.Attributes.SelectMany(x => x.PhysicalBindings))
            .Concat(entity.Metrics.SelectMany(x => x.PhysicalBindings))
            .Concat(entity.SourceRelationships.SelectMany(x => x.PhysicalBindings))
            .Concat(entity.TargetRelationships.SelectMany(x => x.PhysicalBindings))
            .Where(x => x.IsActive && x.DataSourceId == dataSourceId && x.MetadataTable != null && x.MetadataColumn != null)
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToList();

        if (bindings.Count == 0)
            throw new InvalidOperationException($"No active PhysicalBinding found for BusinessEntity '{entity.Id}' in DataSource '{dataSourceId}'.");

        var metrics = intent.Metrics.Select(metric =>
        {
            var definition = FindMetric(entity, metric.SemanticText, metric.Name);
            var binding = SelectBinding(definition.PhysicalBindings, dataSourceId, metric.SemanticText, metric.Name);
            return new QueryPlanMetricResolution
            {
                TableId = binding.MetadataTableId,
                DataSourceId = binding.DataSourceId,
                ColumnId = binding.MetadataColumnId,
                SemanticText = metric.SemanticText,
                Table = binding.MetadataTable!.TableName ?? string.Empty,
                Column = binding.MetadataColumn!.ColumnName ?? string.Empty,
                BusinessMeaning = definition.Description ?? definition.DisplayName,
                MetricType = string.Equals(definition.SemanticType, "EntityCount", StringComparison.OrdinalIgnoreCase) ? "EntityCount" : "ColumnMetric"
            };
        }).ToList();

        var dimensions = intent.Dimensions.Select(dimension =>
        {
            var definition = FindAttribute(entity, dimension);
            var binding = SelectBinding(definition.PhysicalBindings, dataSourceId, dimension);
            return new QueryPlanDimensionResolution
            {
                TableId = binding.MetadataTableId,
                DataSourceId = binding.DataSourceId,
                ColumnId = binding.MetadataColumnId,
                SemanticText = dimension,
                Table = binding.MetadataTable!.TableName ?? string.Empty,
                Column = binding.MetadataColumn!.ColumnName ?? string.Empty,
                BusinessMeaning = definition.Description ?? definition.DisplayName,
                ResolutionType = "BusinessEntityAttribute",
                ExecutionCapability = "Executable",
                DimensionLabelColumnId = binding.MetadataColumnId,
                DimensionLabelColumn = binding.MetadataColumn!.ColumnName
            };
        }).ToList();

        var filters = intent.Filters.Select(filter =>
        {
            var definition = FindAttribute(entity, filter.SemanticText);
            var binding = SelectBinding(definition.PhysicalBindings, dataSourceId, filter.SemanticText);
            return new QueryPlanFilterResolution
            {
                TableId = binding.MetadataTableId,
                DataSourceId = binding.DataSourceId,
                ColumnId = binding.MetadataColumnId,
                SemanticText = filter.SemanticText,
                Table = binding.MetadataTable!.TableName ?? string.Empty,
                Column = binding.MetadataColumn!.ColumnName ?? string.Empty,
                BusinessMeaning = definition.Description ?? definition.DisplayName
            };
        }).ToList();

        var tables = bindings.Select(x => new QueryPlanTableResolution
        {
            TableId = x.MetadataTableId,
            DataSourceId = x.DataSourceId,
            SemanticText = entity.Name,
            Table = x.MetadataTable!.TableName ?? string.Empty,
            BusinessMeaning = entity.Description ?? entity.DisplayName
        }).GroupBy(x => new { x.TableId, x.DataSourceId }).Select(x => x.First()).ToList();

        var orders = new List<QueryPlanOrderResolution>();
        if (!string.IsNullOrWhiteSpace(intent.OrderBy))
        {
            var definition = FindAttribute(entity, intent.OrderBy);
            var binding = SelectBinding(definition.PhysicalBindings, dataSourceId, intent.OrderBy);
            orders.Add(new QueryPlanOrderResolution
            {
                TableId = binding.MetadataTableId,
                DataSourceId = binding.DataSourceId,
                ColumnId = binding.MetadataColumnId,
                SemanticText = intent.OrderBy,
                Table = binding.MetadataTable!.TableName ?? string.Empty,
                Column = binding.MetadataColumn!.ColumnName ?? string.Empty,
                BusinessMeaning = definition.Description ?? definition.DisplayName
            });
        }

        return Task.FromResult(new QueryPlanSemanticResolution
        {
            Metrics = metrics,
            Filters = filters,
            Dimensions = dimensions,
            Tables = tables,
            Orders = orders
        });
    }

    private static BusinessEntityMetric FindMetric(BusinessEntity entity, string? semanticText, string? name)
    {
        return FindUnique(entity.Metrics, semanticText, name, x => new[] { x.Name, x.DisplayName, x.Description });
    }

    private static BusinessEntityAttribute FindAttribute(BusinessEntity entity, string semanticText)
    {
        return FindUnique(entity.Attributes, semanticText, null, x => new[] { x.Name, x.DisplayName, x.Description });
    }

    private static T FindUnique<T>(IEnumerable<T> items, string? first, string? second, Func<T, IEnumerable<string?>> candidates) where T : class
    {
        var terms = new[] { first, second }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => Normalize(x!)).Distinct().ToArray();
        if (terms.Length == 0) throw new InvalidOperationException("At least one semantic lookup term is required.");
        var matches = items.Where(item => candidates(item).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => Normalize(x!)).Any(value => terms.Contains(value))).ToList();
        if (matches.Count != 1) throw new InvalidOperationException($"Expected exactly one semantic definition match, found {matches.Count}.");
        return matches[0];
    }

    private static PhysicalBinding SelectBinding(IEnumerable<PhysicalBinding> bindings, long dataSourceId, string? first, string? second = null)
    {
        var active = bindings.Where(x => x.IsActive && x.DataSourceId == dataSourceId && x.MetadataTable != null && x.MetadataColumn != null)
            .OrderBy(x => x.Priority).ThenBy(x => x.Id).ToList();
        if (active.Count == 1) return active[0];
        if (active.Count == 0) throw new InvalidOperationException($"No active PhysicalBinding found for '{first ?? second}' in DataSource '{dataSourceId}'.");
        throw new InvalidOperationException($"Multiple active PhysicalBindings found for '{first ?? second}' in DataSource '{dataSourceId}'; explicit priority/selection is required.");
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
