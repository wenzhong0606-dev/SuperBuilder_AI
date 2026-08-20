using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.8 Golden Dataset Quality Gate。
/// 验证 Golden Dataset 本身是否满足成为 Regression Baseline 的最低质量要求。
/// </summary>
public sealed class GoldenDatasetQualityGate
{
    public GoldenDatasetQualityScorecard Evaluate(GoldenQueryDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        var issues = new List<GoldenDatasetQualityIssue>();
        var warnings = new List<string>();
        var cases = dataset.Cases ?? new List<GoldenQueryCase>();
        var enabled = cases.Where(x => x.Enabled).ToList();

        if (string.IsNullOrWhiteSpace(dataset.Version))
            issues.Add(Issue("DATASET_VERSION_MISSING", "Error", "", "Dataset version is required."));

        if (!string.Equals(dataset.Dataset, "query-plan-golden", StringComparison.OrdinalIgnoreCase))
            issues.Add(Issue("DATASET_NAME_INVALID", "Error", "", "Dataset name must be 'query-plan-golden'."));

        if (cases.Count == 0)
            issues.Add(Issue("DATASET_EMPTY", "Error", "", "Golden Dataset contains no cases."));

        var duplicates = cases.Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);
        foreach (var duplicate in duplicates)
            issues.Add(Issue("DUPLICATE_CASE_ID", "Error", duplicate.Key, "Case ID must be unique."));

        foreach (var item in cases)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
                issues.Add(Issue("CASE_ID_MISSING", "Error", "", "Case ID is required."));
            if (string.IsNullOrWhiteSpace(item.Name))
                issues.Add(Issue("CASE_NAME_MISSING", "Error", item.Id, "Case name is required."));
            if (string.IsNullOrWhiteSpace(item.Question))
                issues.Add(Issue("QUESTION_MISSING", "Error", item.Id, "Question is required."));
            if (item.Expected is null)
                issues.Add(Issue("EXPECTATION_MISSING", "Error", item.Id, "Expected Ground Truth is required."));
            else
                ValidateExpectation(item, issues);
        }

        if (enabled.Count == 0)
            issues.Add(Issue("NO_ENABLED_CASE", "Error", "", "At least one enabled Golden Case is required."));

        var score = Math.Max(0, 100 - issues.Count * 10);
        if (enabled.Count < 5)
            warnings.Add("Golden Dataset has fewer than 5 enabled cases; regression coverage may be weak.");

        return new GoldenDatasetQualityScorecard
        {
            Dataset = dataset.Dataset,
            Version = dataset.Version,
            Passed = issues.Count == 0,
            Score = score,
            TotalCases = cases.Count,
            EnabledCases = enabled.Count,
            Issues = issues,
            Warnings = warnings
        };
    }

    private static void ValidateExpectation(GoldenQueryCase item, List<GoldenDatasetQualityIssue> issues)
    {
        var expected = item.Expected;
        if (expected.Limit is < 0)
            issues.Add(Issue("INVALID_LIMIT", "Error", item.Id, "Limit cannot be negative."));

        if (expected.Metrics is not null && expected.Metrics.Any(x => string.IsNullOrWhiteSpace(x.SemanticText)))
            issues.Add(Issue("METRIC_SEMANTIC_MISSING", "Error", item.Id, "Metric semantic text is required when metrics are specified."));

        if (expected.Tables is not null && expected.Tables.Any(x => string.IsNullOrWhiteSpace(x.TableName)))
            issues.Add(Issue("TABLE_NAME_MISSING", "Error", item.Id, "Table name is required when tables are specified."));
    }

    private static GoldenDatasetQualityIssue Issue(string code, string severity, string caseId, string message) => new()
    {
        Code = code,
        Severity = severity,
        CaseId = caseId,
        Message = message
    };
}
