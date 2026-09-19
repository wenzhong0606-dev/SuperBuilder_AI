// 验收 #4：登录完成 / 登出改为浏览器端 POST（带 antiforgery 双提交令牌）。
// 交接码经请求体发送（不进 URL），服务端校验 antiforgery 后写 httpOnly cookie 并返回重定向目标。
// 会话 id 不接触此脚本，也不进 window 全局（仅 antiforgery 令牌按标准设计可读）。
window.submitSessionStart = async function (code) {
    var token = window.__afToken || '';
    try {
        var resp = await fetch('/auth/session/start', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': token
            },
            body: new URLSearchParams({ code: code }),
            credentials: 'include'
        });
        if (!resp.ok) return null;
        var data = await resp.json();
        return (data && data.redirect) ? data.redirect : '/';
    } catch (e) {
        return null;
    }
};

window.submitLogout = async function () {
    var token = window.__afToken || '';
    try {
        await fetch('/auth/session/end', {
            method: 'POST',
            headers: { 'RequestVerificationToken': token },
            credentials: 'include'
        });
    } catch (e) { /* 忽略网络错误，登出页会重新拉取会话态 */ }
    return true;
};
