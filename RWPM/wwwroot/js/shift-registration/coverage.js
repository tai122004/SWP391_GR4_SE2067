// Views/ShiftRegistration/Index.cshtml — độ phủ ca (tab Quản lý lịch ca) lấy từ ShiftDemand/CoverageJson.
// Thứ tự nạp: core.js, grid.js, coverage.js, week-picker.js, modals.js, index.js (cùng dùng window.RWPM.shiftRegistration).
// Cấu hình: window.RWPM.shiftRegistrationIndex = { isManager, coverageStoreId, shifts, employees, urls: { availableShifts }, i18n: {...} }
(function ($, cfg) {
    'use strict';
    var app = window.RWPM.shiftRegistration;
    var S = app.state, T = app.T, L = cfg.i18n;
    var escAttr = app.escAttr, escapeHtml = app.escapeHtml, getISODateString = app.getISODateString;

    // ===== Độ phủ ca (tab "Quản lý lịch ca"): một lần gọi ShiftDemand/CoverageJson mỗi lần tải / đổi tuần =====
    function loadCoverage() {
        const $body = $('#coverageBody');
        if (!cfg.isManager || S.activeMode !== 'manager' || !cfg.coverageStoreId) {
            S.coverageReqSeq++;
            $body.empty();
            $('#btnCoverageLink').hide();
            return;
        }
        const from = getISODateString(S.currentWeekStart);
        const toDate = new Date(S.currentWeekStart);
        toDate.setDate(toDate.getDate() + 6);
        $('#btnCoverageLink').attr('href', '/ShiftDemand/Coverage?storeId=' + cfg.coverageStoreId + '&weekStart=' + from).show();
        const seq = ++S.coverageReqSeq;
        $.get('/ShiftDemand/CoverageJson', { storeId: cfg.coverageStoreId, from: from, to: getISODateString(toDate) })
            .done(function(rows) {
                if (seq !== S.coverageReqSeq) return;
                renderCoverage(Array.isArray(rows) ? rows : []);
            })
            .fail(function() {
                if (seq !== S.coverageReqSeq) return;
                // Không có quyền xem độ phủ (hoặc lỗi): ẩn khối độ phủ, không ảnh hưởng lịch ca
                $body.empty();
                $('#btnCoverageLink').hide();
            });
    }

    function renderCoverage(rows) {
        const $body = $('#coverageBody');
        if (S.activeMode !== 'manager' || rows.length === 0) { $body.empty(); return; }
        const fmt = (tpl, args) => String(tpl || '').replace(/\{(\d+)\}/g, (m, i) => args[i] ?? '');
        const shifts = [];
        const map = {};
        rows.forEach(r => {
            const d = String(r.date || '').split('T')[0];
            map[r.shiftId + '_' + d] = r;
            if (!shifts.some(x => x.shiftId === r.shiftId)) shifts.push(r);
        });
        shifts.sort((a, b) => String(a.startTime).localeCompare(String(b.startTime)) || String(a.shiftName).localeCompare(String(b.shiftName)));

        let html = `<tr class="coverage-head"><td colspan="8" class="text-muted fw-semibold">${T.coverageTitle}</td></tr>`;
        shifts.forEach(sh => {
            const time = String(sh.startTime || '').substring(0, 5) + ' – ' + String(sh.endTime || '').substring(0, 5);
            html += `<tr><td class="shift-name-col"><div class="small fw-semibold text-dark text-truncate" title="${escAttr(sh.shiftName)}">${escapeHtml(sh.shiftName)}</div><div class="text-muted" style="font-size:11px">${time}</div></td>`;
            const d = new Date(S.currentWeekStart);
            for (let i = 0; i < 7; i++) {
                const r = map[sh.shiftId + '_' + getISODateString(d)];
                if (!r) {
                    html += `<td class="text-center"><span class="text-muted small" title="${T.coverageNotApplicable}">·</span></td>`;
                } else if (r.required === null || r.required === undefined) {
                    html += `<td class="text-center"><span class="text-muted small" title="${escAttr(fmt(T.coverageNotConfigured, [r.approved, r.pending]))}">–</span></td>`;
                } else {
                    const cls = r.approved < r.required
                        ? 'app-badge-danger'
                        : (r.approved > r.required ? 'app-badge-warning' : 'app-badge-success');
                    html += `<td class="text-center"><span class="app-badge coverage-badge justify-content-center ${cls}" title="${escAttr(fmt(T.coverageTooltip, [r.approved, r.required, r.pending]))}">${r.approved}/${r.required}</span></td>`;
                }
                d.setDate(d.getDate() + 1);
            }
            html += `</tr>`;
        });
        $body.html(html);
    }

    app.loadCoverage = loadCoverage;
})(jQuery, window.RWPM.shiftRegistrationIndex);
