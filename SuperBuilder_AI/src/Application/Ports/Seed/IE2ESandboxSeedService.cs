using System.Threading;
using System.Threading.Tasks;

namespace SuperBuilder_AI.Interfaces.Seed;

/// <summary>
/// E2E 沙箱种子服务（env-gated，仅 CI 验证用）。
/// <para>
/// 在全新 CI 库创建隔离的 <c>e2eapp</c> 租户 + <c>e2eadmin</c>(tenant-admin) / <c>e2ereader</c>(viewer) 账号，
/// 使权限矩阵 E2E 能真实认证并验证按钮级守卫（正向管理员可见、负向读者隐藏）。
/// 幂等：<c>e2eapp</c> 已存在则跳过。仅当环境变量 <c>E2E_SEED=true</c> 时由 API 启动序列调用，生产默认不触发。
/// 凭据与 CI 工作流 <c>SB_E2E_*</c> 变量保持一致（密码 longping00）。
/// </para>
/// </summary>
public interface IE2ESandboxSeedService
{
    Task SeedAsync(CancellationToken ct = default);
}
