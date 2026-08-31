// SuperBuilder 前端运行时脚本（RCL 共享，Web 与 MAUI WebView 均加载）
window.SuperBuilder = window.SuperBuilder || {};

// 切换主题：写入根元素 data-theme，CSS 变量随之切换，并持久化到 localStorage
window.SuperBuilder.setTheme = function (theme) {
    theme = theme || 'light';
    document.documentElement.setAttribute('data-theme', theme);
    try { localStorage.setItem('sb-theme', theme); } catch (e) {}
};

// 读取持久化主题（MainLayout 初始化时调用）
window.SuperBuilder.getTheme = function () {
    try { return localStorage.getItem('sb-theme') || 'light'; } catch (e) { return 'light'; }
};

// 渲染图表：spec 为结构化对象 { type, data:{labels,datasets}, options }
// 实例生命周期：同一 canvas 上先销毁旧图表再新建，支持「视图层多轮调整」即时重渲染。
window.SuperBuilder.renderChart = function (canvas, spec) {
    if (!canvas || !canvas.getContext) return;
    if (!window.Chart) {
        var ctx = canvas.getContext('2d');
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        ctx.fillStyle = '#888';
        ctx.font = '14px sans-serif';
        ctx.fillText('图表库未加载（Chart.js）', 20, 40);
        return;
    }
    try {
        if (canvas._sbChart) { canvas._sbChart.destroy(); canvas._sbChart = null; }
        canvas._sbChart = new window.Chart(canvas, spec);
    } catch (e) {
        var c = canvas.getContext('2d');
        c.clearRect(0, 0, canvas.width, canvas.height);
        c.fillStyle = '#dc2626';
        c.font = '13px sans-serif';
        c.fillText('图表渲染失败：' + (e && e.message ? e.message : e), 12, 36);
    }
};

// 显式销毁（组件卸载时可选调用）
window.SuperBuilder.destroyChart = function (canvas) {
    if (canvas && canvas._sbChart) { canvas._sbChart.destroy(); canvas._sbChart = null; }
};
