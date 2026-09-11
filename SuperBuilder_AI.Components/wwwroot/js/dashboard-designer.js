// SuperBuilder 仪表盘可视化设计器运行时（RCL 共享，Web 与 MAUI WebView 均加载）
// 目标：在不引入任何第三方拖拽库的前提下，为 12 栅格画布提供「拖动 / 缩放 / 调色板拖放」能力。
//
// 设计要点：
//  - 单次 attach：监听器挂在画布容器与 document 上，采用事件委托，新增组件无需重挂。
//  - 网格换算：读取画布的 CSS Grid 计算样式（grid-template-columns / grid-auto-rows / gap），
//    把像素位移换算为 (列, 行, 宽, 高) 网格单位；不做像素级绝对定位，布局始终由 CSS Grid 负责。
//  - 拖动/缩放过程中仅预览（直接改内联 grid-column/grid-row），松手时通过 JSInterop 回调 .NET
//    的 OnWidgetGridChanged(id, x, y, w, h)，由 .NET 更新 DSL 并重绘。
//  - 纯点击（没有位移）不回调，交给 Blazor 的 @onclick 处理选中。
window.SuperBuilder = window.SuperBuilder || {};

window.SuperBuilder.designer = (function () {
    var state = { canvas: null, ref: null, drag: null };

    function trackMetrics(canvas) {
        var cs = getComputedStyle(canvas);
        var cols = (cs.gridTemplateColumns || '').split(' ')
            .map(function (v) { return parseFloat(v); })
            .filter(function (n) { return !isNaN(n) && n > 0; });
        var colW = cols.length ? cols[0] : (canvas.clientWidth || 960) / 12;
        var rowH = parseFloat(cs.gridAutoRows) || 64;
        var gap = parseFloat(cs.columnGap) || parseFloat(cs.gap) || 12;
        return { colW: colW, rowH: rowH, gap: gap, cols: cols.length || 12 };
    }

    function clamp(v, lo, hi) { return Math.max(lo, Math.min(hi, v)); }
    function num(v, d) { var n = parseInt(v, 10); return isNaN(n) ? d : n; }

    function posOf(el) {
        return {
            x: num(el.getAttribute('data-x'), 0),
            y: num(el.getAttribute('data-y'), 0),
            w: num(el.getAttribute('data-w'), 4),
            h: num(el.getAttribute('data-h'), 3)
        };
    }

    function applyPreview(el, d) {
        if (d.mode === 'move') {
            d.el.style.gridColumn = (d.cx + 1) + ' / span ' + d.w;
            d.el.style.gridRow = (d.cy + 1) + ' / span ' + d.h;
        } else {
            d.el.style.gridColumn = (d.x + 1) + ' / span ' + d.cw;
            d.el.style.gridRow = (d.y + 1) + ' / span ' + d.ch;
        }
    }

    function onPointerDown(e) {
        if (!state.canvas) return;
        var widget = e.target.closest ? e.target.closest('.sb-dz-widget') : null;
        if (!widget || !state.canvas.contains(widget)) return;
        if (e.target.closest('button, a, input, select, textarea, .sb-dz-nodrag')) return;

        var mode = e.target.closest('.sb-dz-resize') ? 'resize' : 'move';
        var pos = posOf(widget);
        state.drag = {
            el: widget, id: widget.getAttribute('data-id'), mode: mode,
            startX: e.clientX, startY: e.clientY,
            x: pos.x, y: pos.y, w: pos.w, h: pos.h,
            cx: pos.x, cy: pos.y, cw: pos.w, ch: pos.h,
            m: trackMetrics(state.canvas), moved: false
        };
    }

    function onPointerMove(e) {
        var d = state.drag; if (!d) return;
        var stepX = d.m.colW + d.m.gap, stepY = d.m.rowH + d.m.gap;
        var dcol = Math.round((e.clientX - d.startX) / stepX);
        var drow = Math.round((e.clientY - d.startY) / stepY);
        if (dcol !== 0 || drow !== 0) d.moved = true;
        if (d.mode === 'move') {
            d.cx = clamp(d.x + dcol, 0, Math.max(0, d.m.cols - d.w));
            d.cy = Math.max(0, d.y + drow);
        } else {
            d.cw = clamp(d.w + dcol, 1, Math.max(1, d.m.cols - d.x));
            d.ch = Math.max(1, d.h + drow);
        }
        applyPreview(d.el, d);
        if (d.moved) e.preventDefault();
    }

    function onPointerUp() {
        var d = state.drag; if (!d) return;
        state.drag = null;
        if (!d.moved) return;
        var x, y, w, h;
        if (d.mode === 'move') { x = d.cx; y = d.cy; w = d.w; h = d.h; }
        else { x = d.x; y = d.y; w = d.cw; h = d.ch; }
        if (state.ref) {
            state.ref.invokeMethodAsync('OnWidgetGridChanged', d.id, x, y, w, h);
        }
    }

    function onDragOver(e) { e.preventDefault(); }

    function onDrop(e) {
        e.preventDefault();
        if (!state.canvas || !state.ref) return;
        var type = e.dataTransfer && e.dataTransfer.getData('text/sb-widget');
        if (!type) return;
        var rect = state.canvas.getBoundingClientRect();
        var m = trackMetrics(state.canvas);
        var stepX = m.colW + m.gap, stepY = m.rowH + m.gap;
        var x = clamp(Math.round((e.clientX - rect.left) / stepX), 0, Math.max(0, m.cols - 1));
        var y = Math.max(0, Math.round((e.clientY - rect.top) / stepY));
        state.ref.invokeMethodAsync('OnPaletteDrop', type, x, y);
    }

    function attach(canvas, ref) {
        detach();
        if (!canvas) return;
        state.canvas = canvas;
        state.ref = ref;
        canvas.addEventListener('pointerdown', onPointerDown);
        canvas.addEventListener('dragover', onDragOver);
        canvas.addEventListener('drop', onDrop);
        document.addEventListener('pointermove', onPointerMove);
        document.addEventListener('pointerup', onPointerUp);
    }

    function detach() {
        var c = state.canvas;
        if (c) {
            c.removeEventListener('pointerdown', onPointerDown);
            c.removeEventListener('dragover', onDragOver);
            c.removeEventListener('drop', onDrop);
        }
        document.removeEventListener('pointermove', onPointerMove);
        document.removeEventListener('pointerup', onPointerUp);
        state.canvas = null;
        state.ref = null;
        state.drag = null;
    }

    return { attach: attach, detach: detach };
})();
