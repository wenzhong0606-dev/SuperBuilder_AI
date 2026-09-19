namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 登录完成 / 登出的平台分流抽象（Phase 1，M8-05 加固）。
///
/// <para>
/// 登录成功后的「持久化 + 跳转」在 Web 与 MAUI 上行为不同：Web 需经一次性交接码把令牌迁入服务端会话并写
/// httpOnly cookie（组件事件不能直接写 cookie，见 hardening §8.1(j)）；MAUI 直接落本地安全存储 / localStorage。
/// 通过本接口隔离，登录页（RCL 共享）无需感知宿主差异。
/// </para>
/// </summary>
public interface ILoginCompletion
{
    /// <summary>登录成功后调用：Web 经交接码写 cookie 并重定向；MAUI 落本地存储后跳转。</summary>
    /// <param name="result">登录结果（含令牌）。</param>
    /// <param name="returnUrl">登录后跳转目标（Web 传给交接端点，MAUI 直接导航）。</param>
    Task CompleteLoginAsync(AuthResult result, string? returnUrl = null);

    /// <summary>登出：Web 跳转至 /auth/session/end 清除 cookie + 服务端会话；MAUI 清本地存储并跳登录。</summary>
    Task LogoutAsync();
}
