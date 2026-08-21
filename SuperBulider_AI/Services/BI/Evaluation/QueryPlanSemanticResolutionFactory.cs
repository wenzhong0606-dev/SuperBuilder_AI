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
            throw new InvalidOperationException($"只有 State=Resolved 的 Semantic Applicability 才允许创建 QueryPlanSemanticResolution，当前状态为“{applicability.State}”。");

        var metricResolutions = applicability.MetricResolutions
            .Where(x => x.TableId > 0 && x.DataSourceId > 0 && x.ColumnId > 0 && !string.IsNullOrWhiteSpace(x.Table) && !string.IsNullOrWhiteSpace(x.Column))
            .Select(x => new QueryPlanMetricResolution
            {
                TableId = x.TableId,
                DataSourceId = x.DataSourceId,
                ColumnId = x.ColumnId,
                SemanticText = x.SemanticText,
                Table = x.Table ?? string.Empty,
                Column = x.Column ?? string.Empty,
                BusinessMeaning = x.BusinessMeaning,
                Score = x.Score
            })
            .ToList();

        // 向后兼容旧的单 Metric Resolution：旧数据只有 Resolution 时仍允许继续运行。
        if (metricResolutions.Count == 0 && applicability.Resolution is not null)
        {
            var resolution = applicability.Resolution;
            if (resolution.TableId <= 0 || resolution.DataSourceId <= 0 || resolution.ColumnId <= 0 || string.IsNullOrWhiteSpace(resolution.Table) || string.IsNullOrWhiteSpace(resolution.Column))
                throw new InvalidOperationException("Semantic Applicability Resolution 不完整：TableId、DataSourceId、ColumnId、Table、Column 均必须有效。");

            metricResolutions.Add(new QueryPlanMetricResolution
            {
                TableId = resolution.TableId,
                DataSourceId = resolution.DataSourceId,
                ColumnId = resolution.ColumnId,
                SemanticText = applicability.MetricSemanticText,
                Table = resolution.Table,
                Column = resolution.Column,
                BusinessMeaning = resolution.BusinessMeaning,
                Score = resolution.Score
            });
        }

        if (metricResolutions.Count == 0)
            throw new InvalidOperationException("Semantic Applicability 已标记为 Resolved，但没有提供任何 Metric 物理绑定，禁止继续构建 QueryPlan。");

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
            Metrics = metricResolutions,
            Metric = metricResolutions[0],
            Filters = filters,
            Dimensions = dimensions
        };
    }
}
