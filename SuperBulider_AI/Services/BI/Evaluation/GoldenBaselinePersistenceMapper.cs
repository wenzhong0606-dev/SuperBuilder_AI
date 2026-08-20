using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Maps the released domain baseline to the persistence-neutral record.
/// </summary>
public static class GoldenBaselinePersistenceMapper
{
    public static GoldenBaselinePersistenceRecord ToRecord(GoldenBaseline baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        return new GoldenBaselinePersistenceRecord
        {
            BaselineId = baseline.BaselineId,
            Dataset = baseline.Dataset,
            Version = baseline.Version,
            ReleasedAtUtc = baseline.ReleasedAtUtc,
            Status = baseline.Status,
            QualityScore = baseline.QualityScore,
            TotalCases = baseline.TotalCases,
            EnabledCases = baseline.EnabledCases,
            MissingDimensionCount = baseline.MissingDimensionCount
        };
    }

    public static GoldenBaseline ToBaseline(GoldenBaselinePersistenceRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new GoldenBaseline
        {
            BaselineId = record.BaselineId,
            Dataset = record.Dataset,
            Version = record.Version,
            ReleasedAtUtc = record.ReleasedAtUtc,
            Status = record.Status,
            QualityScore = record.QualityScore,
            TotalCases = record.TotalCases,
            EnabledCases = record.EnabledCases,
            MissingDimensionCount = record.MissingDimensionCount
        };
    }
}
