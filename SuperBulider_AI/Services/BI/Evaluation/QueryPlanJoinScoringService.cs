using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.10.5 / C.14：评价 Join Endpoint 的业务语义、物理绑定和 JoinType。
/// Golden Join 使用数据库无关的 SemanticText；Runtime QueryJoin 使用 MetadataTableId / MetadataColumnId。
/// 因此本服务负责把 Golden SemanticText 解析到当前 Metadata 的物理绑定，再与 Runtime Join 做严格比较。
/// </summary>
public sealed class QueryPlanJoinScoringService
{
    private readonly SuperBIContext _context;

    public QueryPlanJoinScoringService(SuperBIContext context)
        => _context = context;

    public QueryPlanJoinEvaluationResult Evaluate(
        IReadOnlyList<GoldenJoinExpectation>? expected,
        IReadOnlyList<QueryJoin>? actual)
    {
        if (expected is null)
            return new() { Applicable = false, Passed = true, Score = 1d, Reason = "Joins are not asserted by this case." };

        if (expected.Count == 0)
        {
            var passed = actual is null || actual.Count == 0;
            return new()
            {
                Applicable = true,
                Passed = passed,
                Score = passed ? 1d : 0d,
                Reason = passed ? "No joins expected and none were produced." : "No joins were expected, but runtime produced joins."
            };
        }

        if (actual is null || actual.Count == 0)
            return new() { Applicable = true, Passed = false, Score = 0d, Reason = "Expected joins are missing at runtime." };

        var items = expected.Select(x => EvaluateJoin(x, actual)).ToList();
        var score = items.Count == 0 ? 1d : items.Average(x => x.Score);
        var passedAll = items.All(x => x.Passed);

        return new()
        {
            Applicable = true,
            Passed = passedAll,
            Score = score,
            Items = items,
            Reason = passedAll ? "All expected joins matched semantic and physical bindings." : "One or more join assertions failed."
        };
    }

    private QueryPlanJoinItemScore EvaluateJoin(
        GoldenJoinExpectation expected,
        IReadOnlyList<QueryJoin> actual)
    {
        var leftTable = ResolveTable(expected.LeftTableSemanticText);
        var rightTable = ResolveTable(expected.RightTableSemanticText);

        if (leftTable is null || rightTable is null)
        {
            var missing = new List<string>();
            if (leftTable is null) missing.Add($"左表语义“{expected.LeftTableSemanticText}”无法解析");
            if (rightTable is null) missing.Add($"右表语义“{expected.RightTableSemanticText}”无法解析");

            return new QueryPlanJoinItemScore
            {
                LeftTableSemanticText = expected.LeftTableSemanticText,
                RightTableSemanticText = expected.RightTableSemanticText,
                EndpointScore = 0d,
                ColumnScore = 0d,
                JoinTypeScore = 0d,
                BindingScore = 0d,
                Score = 0d,
                Passed = false,
                Reason = string.Join("；", missing) + "。"
            };
        }

        var leftColumn = ResolveColumn(expected.LeftColumnSemanticText, leftTable.Id);
        var rightColumn = ResolveColumn(expected.RightColumnSemanticText, rightTable.Id);

        if (leftColumn is null || rightColumn is null)
        {
            var missing = new List<string>();
            if (leftColumn is null) missing.Add($"左字段语义“{expected.LeftColumnSemanticText}”无法在表 {leftTable.TableName} 中解析");
            if (rightColumn is null) missing.Add($"右字段语义“{expected.RightColumnSemanticText}”无法在表 {rightTable.TableName} 中解析");

            return new QueryPlanJoinItemScore
            {
                LeftTableSemanticText = expected.LeftTableSemanticText,
                RightTableSemanticText = expected.RightTableSemanticText,
                RuntimeLeftTable = leftTable.TableName,
                RuntimeRightTable = rightTable.TableName,
                EndpointScore = 1d,
                ColumnScore = 0d,
                JoinTypeScore = 0d,
                BindingScore = 0d,
                Score = 0d,
                Passed = false,
                Reason = string.Join("；", missing) + "。"
            };
        }

        var expectedJoinType = string.IsNullOrWhiteSpace(expected.JoinType) ? "INNER" : expected.JoinType;
        var match = actual.FirstOrDefault(x =>
            x.LeftTableId == leftTable.Id &&
            x.RightTableId == rightTable.Id &&
            x.LeftColumnId == leftColumn.Id &&
            x.RightColumnId == rightColumn.Id &&
            Equal(expectedJoinType, x.JoinType));

        // INNER JOIN 左右端点可交换，但交换后仍必须满足 Golden 的物理绑定。
        match ??= actual.FirstOrDefault(x =>
            Equal(expectedJoinType, "INNER") &&
            x.LeftTableId == rightTable.Id &&
            x.RightTableId == leftTable.Id &&
            x.LeftColumnId == rightColumn.Id &&
            x.RightColumnId == leftColumn.Id &&
            Equal(expectedJoinType, x.JoinType));

        if (match is null)
        {
            var runtime = actual.FirstOrDefault(x =>
                (x.LeftTableId == leftTable.Id || x.RightTableId == leftTable.Id)
                && (x.LeftTableId == rightTable.Id || x.RightTableId == rightTable.Id));

            return new QueryPlanJoinItemScore
            {
                LeftTableSemanticText = expected.LeftTableSemanticText,
                RightTableSemanticText = expected.RightTableSemanticText,
                RuntimeLeftTable = runtime?.LeftTableName,
                RuntimeLeftColumn = runtime?.LeftColumnName,
                RuntimeRightTable = runtime?.RightTableName,
                RuntimeRightColumn = runtime?.RightColumnName,
                RuntimeJoinType = runtime?.JoinType,
                EndpointScore = runtime is null ? 0d : .5d,
                ColumnScore = runtime is null ? 0d : .5d,
                JoinTypeScore = runtime is null ? 0d : (Equal(expectedJoinType, runtime.JoinType) ? 1d : 0d),
                BindingScore = runtime is null ? 0d : 1d,
                Score = 0d,
                Passed = false,
                Reason = runtime is null
                    ? $"未找到 Golden Join 的物理绑定：{leftTable.TableName}.{leftColumn.ColumnName} → {rightTable.TableName}.{rightColumn.ColumnName}。"
                    : $"Runtime Join Endpoint 与 Golden 不一致：期望 {leftTable.TableName}.{leftColumn.ColumnName} → {rightTable.TableName}.{rightColumn.ColumnName}，实际 {runtime.LeftTableName}.{runtime.LeftColumnName} → {runtime.RightTableName}.{runtime.RightColumnName}。"
            };
        }

        var endpoint = 1d;
        var columns = 1d;
        var type = 1d;
        var binding = match.LeftTableId > 0 && match.LeftColumnId > 0 && match.RightTableId > 0 && match.RightColumnId > 0 ? 1d : 0d;
        var score = endpoint * .30d + columns * .30d + type * .20d + binding * .20d;

        return new QueryPlanJoinItemScore
        {
            LeftTableSemanticText = expected.LeftTableSemanticText,
            RightTableSemanticText = expected.RightTableSemanticText,
            RuntimeLeftTable = match.LeftTableName,
            RuntimeLeftColumn = match.LeftColumnName,
            RuntimeRightTable = match.RightTableName,
            RuntimeRightColumn = match.RightColumnName,
            RuntimeJoinType = match.JoinType,
            EndpointScore = endpoint,
            ColumnScore = columns,
            JoinTypeScore = type,
            BindingScore = binding,
            Score = score,
            Passed = binding == 1d,
            Reason = binding == 1d ? "Join Semantic 与 Runtime 物理绑定一致。" : "Join 物理绑定无效。"
        };
    }

    private MetadataTable? ResolveTable(string semanticText)
    {
        if (string.IsNullOrWhiteSpace(semanticText))
            return null;

        var candidates = _context.MetadataTables
            .AsNoTracking()
            .Where(x => x.SearchText != null || x.TableComment != null || x.TableName != null)
            .ToList();

        return candidates
            .Select(x => new { Table = x, Score = TableSemanticScore(x, semanticText) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Table)
            .FirstOrDefault();
    }

    private MetadataColumn? ResolveColumn(string semanticText, long tableId)
    {
        if (string.IsNullOrWhiteSpace(semanticText))
            return null;

        var candidates = _context.MetadataColumns
            .AsNoTracking()
            .Include(x => x.Semantic)
            .Where(x => x.MetadataTableId == tableId)
            .ToList();

        return candidates
            .Select(x => new { Column = x, Score = ColumnSemanticScore(x, semanticText) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Column)
            .FirstOrDefault();
    }

    private static int TableSemanticScore(MetadataTable table, string semanticText)
    {
        var target = Normalize(semanticText);
        var score = 0;
        score = Math.Max(score, ExactOrContainsScore(table.TableComment, target, 100, 70));
        score = Math.Max(score, ExactOrContainsScore(table.SearchText, target, 90, 60));
        score = Math.Max(score, ExactOrContainsScore(table.TableName, target, 80, 50));
        return score;
    }

    private static int ColumnSemanticScore(MetadataColumn column, string semanticText)
    {
        var target = Normalize(semanticText);
        var score = 0;
        score = Math.Max(score, ExactOrContainsScore(column.ColumnComment, target, 100, 70));
        score = Math.Max(score, ExactOrContainsScore(column.SearchText, target, 90, 60));
        score = Math.Max(score, ExactOrContainsScore(column.ColumnName, target, 80, 50));

        var semantic = column.Semantic;
        score = Math.Max(score, ExactOrContainsScore(semantic?.BusinessMeaning, target, 95, 65));
        score = Math.Max(score, ExactOrContainsScore(semantic?.Keywords, target, 95, 65));
        score = Math.Max(score, ExactOrContainsScore(semantic?.Synonyms, target, 95, 65));
        score = Math.Max(score, ExactOrContainsScore(semantic?.ExampleQuestions, target, 90, 60));
        return score;
    }

    private static int ExactOrContainsScore(string? value, string target, int exact, int contains)
    {
        var normalized = Normalize(value);
        if (string.IsNullOrWhiteSpace(normalized)) return 0;
        if (string.Equals(normalized, target, StringComparison.OrdinalIgnoreCase)) return exact;
        return normalized.Contains(target, StringComparison.OrdinalIgnoreCase) ? contains : 0;
    }

    private static string Normalize(string? value)
        => (value ?? string.Empty).Trim().Replace(" ", string.Empty);

    private static bool Equal(string? left, string? right)
        => !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right)
           && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}

public sealed class QueryPlanJoinEvaluationResult
{
    public bool Applicable { get; init; }
    public bool Passed { get; init; }
    public double Score { get; init; }
    public string Reason { get; init; } = string.Empty;
    public IReadOnlyList<QueryPlanJoinItemScore> Items { get; init; } = Array.Empty<QueryPlanJoinItemScore>();
}

public sealed class QueryPlanJoinItemScore
{
    public string LeftTableSemanticText { get; init; } = string.Empty;
    public string RightTableSemanticText { get; init; } = string.Empty;
    public string? RuntimeLeftTable { get; init; }
    public string? RuntimeLeftColumn { get; init; }
    public string? RuntimeRightTable { get; init; }
    public string? RuntimeRightColumn { get; init; }
    public string? RuntimeJoinType { get; init; }
    public double EndpointScore { get; init; }
    public double ColumnScore { get; init; }
    public double JoinTypeScore { get; init; }
    public double BindingScore { get; init; }
    public double Score { get; init; }
    public bool Passed { get; init; }
    public string Reason { get; init; } = string.Empty;
}
