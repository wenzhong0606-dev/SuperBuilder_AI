using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// 将 Semantic Applicability 的稳定物理绑定转换为 QueryPlanBuilder 使用的绑定契约。
/// </summary>
public static class QueryPlanSemanticResolutionFactory
{
    public static QueryPlanSemanticResolution From(SemanticApplicabilityResult applicability)
    {
        ArgumentNullException.ThrowIfNull(applicability);

        if (!string.Equals(applicability.State, "Resolved", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"只有 State=Resolved 的 Semantic Applicability 才允许创建 QueryPlanSemanticResolution，当前状态为“{applicability.State}”。");
        }

        if (applicability.Resolution is null)
        {
            throw new InvalidOperationException(
                "Semantic Applicability 已标记为 Resolved，但没有提供 Resolution，禁止继续构建 QueryPlan。");
        }

        var resolution = applicability.Resolution;

        if (resolution.TableId <= 0
            || resolution.DataSourceId <= 0
            || resolution.ColumnId <= 0
            || string.IsNullOrWhiteSpace(resolution.Table)
            || string.IsNullOrWhiteSpace(resolution.Column))
        {
            throw new InvalidOperationException(
                "Semantic Applicability Resolution 不完整：TableId、DataSourceId、ColumnId、Table、Column 均必须有效。");
        }

        var filters = applicability.FilterResolutions
            .Select(filter => new QueryPlanFilterResolution
            {
                TableId = filter.TableId,
                DataSourceId = filter.DataSourceId,
                ColumnId = filter.ColumnId,
                SemanticText = filter.SemanticText,
                Table = filter.Table ?? string.Empty,
                Column = filter.Column ?? string.Empty,
                BusinessMeaning = filter.BusinessMeaning,
                Score = filter.Score
            })
            .ToList();

        var dimensions = applicability.DimensionResolutions
            .Select(dimension => new QueryPlanDimensionResolution
            {
                TableId = dimension.TableId,
                DataSourceId = dimension.DataSourceId,
                ColumnId = dimension.ColumnId,
                SemanticText = dimension.SemanticText,
                Table = dimension.Table ?? string.Empty,
                Column = dimension.Column ?? string.Empty,
                BusinessMeaning = dimension.BusinessMeaning,
                Score = dimension.Score
            })
            .ToList();

        return new QueryPlanSemanticResolution
        {
            Metric = new QueryPlanMetricResolution
            {
                TableId = resolution.TableId,
                DataSourceId = resolution.DataSourceId,
                ColumnId = resolution.ColumnId,
                SemanticText = applicability.MetricSemanticText,
                Table = resolution.Table,
                Column = resolution.Column,
                BusinessMeaning = resolution.BusinessMeaning,
                Score = resolution.Score
            },
            Filters = filters,
            Dimensions = dimensions
        };
    }
}
