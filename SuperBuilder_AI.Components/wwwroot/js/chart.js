// SuperBuilder 前端运行时脚本（RCL 共享，Web 与 MAUI WebView 均加载）
window.SuperBuilder = window.SuperBuilder || {};

// 切换主题：写入根元素 data-theme，CSS 变量随之切换
window.SuperBuilder.setTheme = function (theme) {
    document.documentElement.setAttribute('data-theme', theme || 'light');
};

// 渲染图表：P11.1 骨架——若 Chart.js 已引入则构造图表，否则给出占位提示（图表库选型待定）
window.SuperBuilder.renderChart = function (canvas, type, json) {
    if (!canvas || !canvas.getContext) return;
    var ctx = canvas.getContext('2d');
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    if (!window.Chart) {
        ctx.fillStyle = '#888';
        ctx.font = '14px sans-serif';
        ctx.fillText('图表库待接入（Chart.js 候选）', 20, 40);
        return;
    }
    // P11.2 细化：window.Chart 就绪后在此构造对应 type 的图表
};
