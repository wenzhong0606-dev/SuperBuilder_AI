using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SuperBuilder_AI.Components.Services;
using SuperBuilder_AI.Web.Services;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// 验收 #4：静默续期协调器 <see cref="AuthRefreshCoordinator"/> 的行为契约。
/// 用假 <see cref="IRefreshTokenCaller"/> 与假 <see cref="IAuthPersistence"/> 隔离网络与服务端会话。
/// </summary>
public class AuthRefreshCoordinatorTests
{
    private static IConfiguration Config() => new ConfigurationBuilder().Build(); // 默认阈值 300s

    private sealed class FakePersistence : IAuthPersistence
    {
        public SessionData? LastSaved;
        public Task SaveAsync(SessionData data) { LastSaved = data; return Task.CompletedTask; }
        public Task<SessionData?> LoadAsync() => Task.FromResult<SessionData?>(null);
        public Task ClearAsync() => Task.CompletedTask;
    }

    private sealed class FakeCaller : IRefreshTokenCaller
    {
        private readonly RefreshCallResult? _result;
        public int Calls;
        public FakeCaller(RefreshCallResult? result) => _result = result;
        public Task<RefreshCallResult?> CallRefreshAsync(string refreshToken, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(_result);
        }
    }

    [Fact]
    public async Task NearExpiry_Refreshes_And_WritesServerSession()
    {
        var state = new AppState { Token = "old", UserId = 7, RefreshToken = "rt" };
        state.AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(60); // 落入 300s 阈值窗口
        var persistence = new FakePersistence();
        var caller = new FakeCaller(new RefreshCallResult("new-access", "new-rt", 900));
        var coord = new AuthRefreshCoordinator(state, persistence, caller, Config());

        var ok = await coord.EnsureFreshTokenAsync();

        Assert.True(ok);
        Assert.Equal("new-access", state.Token);
        Assert.Equal("new-rt", state.RefreshToken);
        Assert.NotNull(state.AccessTokenExpiresAtUtc);
        Assert.True(state.AccessTokenExpiresAtUtc > DateTimeOffset.UtcNow);
        Assert.NotNull(persistence.LastSaved);
        Assert.Equal("new-access", persistence.LastSaved!.Token);
        Assert.Equal(1, caller.Calls);
    }

    [Fact]
    public async Task NotNearExpiry_SkipsRefresh()
    {
        var state = new AppState { Token = "old", UserId = 7, RefreshToken = "rt" };
        state.AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(10); // 600s > 300s
        var persistence = new FakePersistence();
        var caller = new FakeCaller(new RefreshCallResult("new-access", "new-rt", 900));
        var coord = new AuthRefreshCoordinator(state, persistence, caller, Config());

        var ok = await coord.EnsureFreshTokenAsync();

        Assert.True(ok);
        Assert.Equal("old", state.Token); // 未刷新
        Assert.Equal(0, caller.Calls);
        Assert.Null(persistence.LastSaved);
    }

    [Fact]
    public async Task RefreshFails_ReturnsFalse_KeepsOldToken()
    {
        var state = new AppState { Token = "old", UserId = 7, RefreshToken = "rt" };
        state.AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(30); // 临近过期
        var persistence = new FakePersistence();
        var caller = new FakeCaller(null); // 刷新调用失败
        var coord = new AuthRefreshCoordinator(state, persistence, caller, Config());

        var ok = await coord.EnsureFreshTokenAsync();

        Assert.False(ok); // 交由后续 401 触发登出
        Assert.Equal("old", state.Token); // 令牌不变
        Assert.Equal(1, caller.Calls);
        Assert.Null(persistence.LastSaved); // 失败不写会话
    }

    [Fact]
    public async Task NoRefreshToken_ReturnsFalse_WithoutCalling()
    {
        var state = new AppState { Token = "old", UserId = 7 }; // RefreshToken 默认 null
        state.AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(10);
        var persistence = new FakePersistence();
        var caller = new FakeCaller(new RefreshCallResult("new-access", "new-rt", 900));
        var coord = new AuthRefreshCoordinator(state, persistence, caller, Config());

        var ok = await coord.EnsureFreshTokenAsync();

        Assert.False(ok);
        Assert.Equal(0, caller.Calls);
    }

    [Fact]
    public async Task ConcurrentCalls_RefreshExactlyOnce_SingleFlight()
    {
        var state = new AppState { Token = "old", UserId = 7, RefreshToken = "rt" };
        state.AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(10); // 都临近过期
        var persistence = new FakePersistence();
        var caller = new FakeCaller(new RefreshCallResult("new-access", "new-rt", 900));
        var coord = new AuthRefreshCoordinator(state, persistence, caller, Config());

        var tasks = Enumerable.Range(0, 5).Select(_ => coord.EnsureFreshTokenAsync()).ToArray();
        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(r));
        Assert.Equal(1, caller.Calls); // 单飞：仅真正刷新一次
        Assert.Equal("new-access", state.Token);
    }
}
