using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using SuperBuilder_AI.Components.Services;
using SuperBuilder_AI.Web.Services;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// 验收 #5：<see cref="WebAuthPersistence"/> 订阅 <see cref="AppState.SessionExpired"/>，
/// 于令牌被拒（改密 / 停用经 SecurityStamp 轮换 → API 401）时立即吊销该用户在服务端的所有 Web 会话
/// （<see cref="IWebSessionStore.RemoveByUserId"/>），强制全电路重新登录；其他用户不受影响。
/// </summary>
public class WebAuthPersistenceRevocationTests
{
    private static IConfiguration Config() => new ConfigurationBuilder().Build();

    private static SessionData Data(long userId) => new SessionData(
        Token: "t", TenantId: 1, HomeTenantId: 1, UserId: userId, Username: "u",
        Permissions: new List<string>(), AvailableCultures: new List<string> { "zh-CN" }, DefaultCulture: "zh-CN");

    [Fact]
    public void SessionExpired_Revokes_All_Sessions_For_TargetUser_Only()
    {
        var store = new WebSessionStore(Config());
        var ctx = new CircuitSessionContext();
        var state = new AppState { UserId = 5, Token = "t" };
        var id1 = store.Create(Data(5));
        var id2 = store.Create(Data(5));      // 同一用户的第二个会话
        var otherId = store.Create(Data(99));  // 其他用户

        var persistence = new WebAuthPersistence(ctx, store, state);
        state.NotifySessionExpired(5);

        Assert.Null(store.Get(id1));
        Assert.Null(store.Get(id2));
        Assert.NotNull(store.Get(otherId)); // 其他用户不受影响
    }

    [Fact]
    public void SessionExpired_With_ZeroUserId_Does_Not_Revoke()
    {
        var store = new WebSessionStore(Config());
        var ctx = new CircuitSessionContext();
        var state = new AppState { UserId = 0 };
        var id = store.Create(Data(7));

        var persistence = new WebAuthPersistence(ctx, store, state);
        state.NotifySessionExpired(0); // userId=0 视为无效，跳过吊销

        Assert.NotNull(store.Get(id));
    }
}
