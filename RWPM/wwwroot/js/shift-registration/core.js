// Views/ShiftRegistration/Index.cshtml — trạng thái dùng chung, chuỗi giao diện (T) và hàm tiện ích cho các file js/shift-registration/*.
// Phải nạp đầu tiên. Cấu hình: window.RWPM.shiftRegistrationIndex = { isManager, coverageStoreId, shifts, employees,
//   urls: { availableShifts }, i18n: {...} } (shifts / employees = null khi máy chủ không gửi danh sách).
(function ($, cfg) {
    'use strict';
    var L = cfg.i18n;
    var app = window.RWPM.shiftRegistration = {};

    // Tuần đang xem: bắt đầu từ thứ 2 của tuần hiện tại
    var currentWeekStart = new Date();
    var day = currentWeekStart.getDay();
    var diff = currentWeekStart.getDate() - day + (day === 0 ? -6 : 1);
    currentWeekStart = new Date(currentWeekStart.setDate(diff));
    currentWeekStart.setHours(0, 0, 0, 0);

    app.state = {
        currentWeekStart: currentWeekStart,
        wpCurrentMonth: new Date(),
        activeMode: 'my', // Default to my shift registration
        shiftsList: cfg.shifts || [],
        employeesList: cfg.employees || [{ id: 'me', name: L.shiftMe }],
        // Ô bị chặn theo thời gian có thể làm việc ("shiftId_yyyy-MM-dd" -> lý do), chỉ ở tab "Đăng ký ca của tôi"
        blockedSlots: {},
        coverageReqSeq: 0
    };

    const escAttr = s => String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    var T = app.T = {
        approved: escAttr(L.statusApproved),
        rejected: escAttr(L.statusRejected),
        rejectedLong: escAttr(L.statusRejectedLong),
        pending: escAttr(L.statusPending),
        overdue: escAttr(L.statusOverdue),
        notApplicable: escAttr(L.statusNotApplicable),
        notRegistered: escAttr(L.statusNotRegistered),
        noShiftsTitle: escAttr(L.gridNoShiftsTitle),
        noShiftsDesc: escAttr(L.gridNoShiftsDesc),
        workHours: escAttr(L.gridWorkHoursFormat),
        slotApprovedTitle: escAttr(L.slotApprovedTitle),
        slotRejectedTitle: escAttr(L.slotRejectedTitle),
        slotPendingTitle: escAttr(L.slotPendingTitle),
        slotPastTitle: escAttr(L.slotPastTitle),
        slotUnavailableTitle: escAttr(L.slotUnavailableTitle),
        slotAvailableTitle: escAttr(L.slotAvailableTitle),
        viewDetails: escAttr(L.slotViewDetails),
        details: escAttr(L.slotDetails),
        blockedBadge: escAttr(L.slotBlockedBadge),
        blockedPendingHint: L.slotBlockedPendingHint,
        coverageTitle: escAttr(L.coverageTitle),
        coverageTooltip: L.coverageTooltip,
        coverageNotConfigured: L.coverageNotConfigured,
        coverageNotApplicable: escAttr(L.coverageNotApplicable),
        warnTitle: L.warnConfirmTitle,
        warnContinue: L.warnConfirmContinue,
        search: escAttr(L.searchBtn),
        errOccurred: L.shiftErrOccurred,
        sysError: L.shiftSysError,
        errPrefix: L.shiftErrPrefix
    };

    function ajaxErrorText(err, fallback) {
        const json = err && err.responseJSON;
        if (json && json.message) return json.message;
        if (json && typeof json === 'object') {
            const src = json.errors || json;
            const msgs = [];
            Object.keys(src).forEach(k => { const v = src[k]; (Array.isArray(v) ? v : (v && v.errors ? v.errors.map(e => e.errorMessage) : [])).forEach(m => { if (typeof m === 'string' && m) msgs.push(m); }); });
            if (msgs.length) return msgs.join('\n');
        }
        const text = String((err && err.responseText) || '').trim();
        if (text && text.length < 300 && text.charAt(0) !== '<' && text.charAt(0) !== '{') return text;
        return fallback || T.sysError;
    }

    function getISODateString(date) {
        var tzoffset = date.getTimezoneOffset() * 60000;
        return (new Date(date - tzoffset)).toISOString().split('T')[0];
    }

    function escapeHtml(str) {
        if (!str) return '';
        return $('<div>').text(str).html();
    }

    // Hộp xác nhận cảnh báo xếp ca (AppConfirm, mỗi cảnh báo một dòng)
    async function confirmScheduleWarnings(warnings) {
        const list = (warnings || []).join('\n');
        return await AppConfirm({ title: T.warnTitle, message: list + '\n\n' + T.warnContinue, type: 'warning', icon: 'bi-exclamation-triangle' });
    }

    function showAppToast(type, message) {
        if (typeof AppToast !== 'undefined') {
            if (type === 'success') AppToast.success(message);
            else if (type === 'warning') AppToast.warning(message);
            else AppToast.error(message);
        } else {
            AppToast.error(message);
        }
    }

    app.escAttr = escAttr;
    app.escapeHtml = escapeHtml;
    app.getISODateString = getISODateString;
    app.ajaxErrorText = ajaxErrorText;
    app.showAppToast = showAppToast;
    app.confirmScheduleWarnings = confirmScheduleWarnings;
})(jQuery, window.RWPM.shiftRegistrationIndex);
