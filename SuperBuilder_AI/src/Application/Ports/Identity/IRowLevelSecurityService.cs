using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces.Identity;

public interface IRowLevelSecurityService
{
	Task<string> GetPolicyFingerprintAsync(long tenantId, long userId, CancellationToken ct = default);
	Task ApplyAsync(QueryPlan plan, long tenantId, long userId, CancellationToken ct = default);
}
