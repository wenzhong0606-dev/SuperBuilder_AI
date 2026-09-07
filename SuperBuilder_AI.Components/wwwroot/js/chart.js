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
// 实例生命周期（M8-04 修正）：
//  - 同类型（bar/line/pie 不变）时走「增量 update」——直接替换 data/options 后 chart.update()，
//    避免每次视图调整都销毁+重建整图（性能与内存友好）。
//  - 类型变化或首次渲染时，先销毁旧实例再新建，杜绝同一 canvas 上叠加多个 Chart 实例导致的内存泄漏。
//  - 组件卸载时应调用 destroyChart 释放实例（见 ChartView.razor 的 IAsyncDisposable）。
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
        var existing = canvas._sbChart;
        // 同类型：增量 update（Chart.js 不支持运行时切换 type，必须重建）
        if (existing && existing.config && existing.config.type
            && spec && spec.type && existing.config.type === spec.type) {
            existing.data = spec.data;
            existing.options = spec.options;
            existing.update();
            return;
        }
        // 类型变化或首次：销毁旧实例后新建，避免重复实例
        if (existing) { existing.destroy(); canvas._sbChart = null; }
        canvas._sbChart = new window.Chart(canvas, spec);
    } catch (e) {
        if (canvas._sbChart) { try { canvas._sbChart.destroy(); } catch (_) {} canvas._sbChart = null; }
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

// ===== Ask 会话持久化（S3-2）：localStorage 封装 =====
window.SuperBuilder.setLocalJson = function (key, json) {
    try { localStorage.setItem(key, json); } catch (e) {}
};

window.SuperBuilder.getLocalJson = function (key) {
    try { return localStorage.getItem(key) || null; } catch (e) { return null; }
};

window.SuperBuilder.removeLocal = function (key) {
    try { localStorage.removeItem(key); } catch (e) {}
};

// ===== 文件下载（S3-3 导出 CSV / Excel）=====
window.SuperBuilder.downloadTextFile = function (name, mime, text) {
    try {
        var blob = new Blob([text], { type: mime + ';charset=utf-8' });
        var url = URL.createObjectURL(blob);
        var a = document.createElement('a');
        a.href = url;
        a.download = name;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
    } catch (e) {
        console.error('downloadTextFile failed', e);
    }
};
