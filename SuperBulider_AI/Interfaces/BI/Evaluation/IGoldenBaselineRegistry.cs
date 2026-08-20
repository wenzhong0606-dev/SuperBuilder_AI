using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Interfaces.BI.Evaluation;

public interface IGoldenBaselineRegistry
{
    GoldenBaseline Register(GoldenBaseline baseline);
    GoldenBaseline? Get(string version);
    GoldenBaseline? GetCurrent();
    IReadOnlyList<GoldenBaseline> List();
}
