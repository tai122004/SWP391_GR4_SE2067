// Views/ShiftDemand/Coverage.cshtml — độ phủ ca theo tuần: chọn tuần, chi tiết ô (modal), duyệt đăng ký chờ, phân công nhanh.
// Cấu hình: window.RWPM.shiftDemandCoverage = { storeId, routeStore, i18n: { loading, error, ..., confirmContinue, availabilityTitle } }
(function ($, cfg) {
    'use strict';
    var storeId = cfg.storeId;
    var routeStore = cfg.routeStore;
    var T = cfg.i18n;

    function token() { return $('#antiForgeryForm input[name="__RequestVerificationToken"]').val(); }
    function esc(s) { return $('<div>').text(s == null ? '' : String(s)).html(); }
    function errMsg(xhr) { return (xhr && xhr.responseJSON && xhr.responseJSON.message) || T.error; }
    function fmt(str, args) { return String(str).replace(/\{(\d+)\}/g, function (m, i) { return args[+i] === undefined ? m : args[+i]; }); }
    function ddmm(iso) { var p = String(iso).substring(0, 10).split('-'); return p[2] + '/' + p[1]; }
    function ddmmyyyy(iso) { var p = String(iso).substring(0, 10).split('-'); return p[2] + '/' + p[1] + '/' + p[0]; }
    function dateTime(iso) { if (!iso) return ''; var d = String(iso); return ddmmyyyy(d) + (d.length > 15 ? ' ' + d.substring(11, 16) : ''); }
    function shiftLabel(name) { return String(name).trim().toLowerCase().indexOf(String(T.shiftPrefix).toLowerCase()) === 0 ? name : fmt(T.shiftLabel, [name]); }
    function overWarning(cov, adding) {
        if (cov.required === null || cov.required === undefined) return null;
        var total = cov.approved + adding;
        if (total <= cov.required) return null;
        return fmt(T.overFormat, [shiftLabel(cov.shiftName), ddmm(cov.date), cov.storeName, cov.required, total, total - cov.required]);
    }

    // Week picker -> jump to the week containing the chosen date (server normalizes to Monday)
    $('#weekPicker').on('change', function () {
        if (!this.value) return;
        var url = '/ShiftDemand/Coverage?weekStart=' + encodeURIComponent(this.value);
        if (routeStore) url += '&storeId=' + routeStore;
        window.location.href = url;
    });

    var modalEl = document.getElementById('cellModal');
    var modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    var current = null; // { shiftId, date, data }
    var changed = false;
    modalEl.addEventListener('hidden.bs.modal', function () { if (changed) window.location.reload(); });

    $(document).on('click', 'button.cov-cell', function () {
        current = { shiftId: +$(this).data('shift'), date: $(this).attr('data-date'), data: null };
        $('#cellTitle').text(this.getAttribute('title'));
        modal.show();
        loadDetail();
    });

    function loadDetail() {
        $('#cellBody').html('<div class="text-center text-muted py-4"><span class="spinner-border spinner-border-sm me-2"></span>' + esc(T.loading) + '</div>');
        $.get('/ShiftDemand/CellDetail', { storeId: storeId, shiftId: current.shiftId, date: current.date })
            .done(function (res) {
                if (!res || !res.success) { $('#cellBody').html('<div class="alert alert-danger mb-0">' + esc((res && res.message) || T.error) + '</div>'); return; }
                current.data = res.data;
                render(res.data);
            })
            .fail(function (xhr) { $('#cellBody').html('<div class="alert alert-danger mb-0">' + esc(errMsg(xhr)) + '</div>'); });
    }

    function stat(label, value, cls) {
        return '<div class="col"><div class="cov-stat"><div class="small text-muted">' + esc(label) + '</div><div class="v ' + (cls || '') + '">' + esc(value) + '</div></div></div>';
    }

    function render(d) {
        var c = d.coverage;
        var h = [];
        h.push('<div class="d-flex flex-wrap justify-content-between align-items-center mb-2 gap-2">');
        h.push('<div><div class="fw-semibold text-dark">' + esc(c.shiftName) + ' <span class="text-muted fw-normal small">' + esc(d.timeRange) + '</span></div>');
        h.push('<div class="small text-muted"><i class="bi bi-shop me-1"></i>' + esc(c.storeName) + ' · ' + esc(ddmmyyyy(c.date)) + '</div></div>');
        if (c.isOverride) h.push('<span class="app-badge app-badge-info"><i class="bi bi-pin-angle-fill"></i>' + esc(T.overrideNote) + '</span>');
        h.push('</div>');

        var reqText = (c.required === null || c.required === undefined) ? '?' : c.required;
        var shortCls = c.shortage > 0 ? 'text-danger' : (c.over > 0 ? 'text-warning' : 'text-success');
        h.push('<div class="row g-2 mb-3">');
        h.push(stat(T.required, reqText, ''));
        h.push(stat(T.approved, c.approved, shortCls));
        h.push(stat(T.pending, c.pending, c.pending > 0 ? 'text-warning' : ''));
        h.push(stat(T.shortage, c.required === null ? '—' : (c.over > 0 ? '+' + c.over : c.shortage), shortCls));
        h.push('</div>');
        if (c.required === null || c.required === undefined) h.push('<div class="alert alert-secondary py-2 px-3 small"><i class="bi bi-info-circle me-1"></i>' + esc(T.notConfigured) + '</div>');
        if (d.isPast) h.push('<div class="alert alert-light border py-2 px-3 small"><i class="bi bi-lock me-1"></i>' + esc(T.past) + '</div>');

        // Approved
        h.push('<h6 class="fw-semibold mt-3 mb-2">' + esc(T.assignedList) + ' <span class="app-badge app-badge-neutral ms-1">' + d.approved.length + '</span></h6>');
        if (!d.approved.length) h.push('<div class="text-muted small mb-2">' + esc(T.noAssigned) + '</div>');
        else {
            h.push('<div class="d-grid gap-2 mb-2">');
            d.approved.forEach(function (e) {
                h.push('<div class="cov-list-item d-flex justify-content-between align-items-center"><div><div class="text-dark">' + esc(e.fullName) + '</div><div class="small text-muted">' + esc(e.employeeCode) + '</div></div>'
                    + '<div class="small text-muted text-end">' + (e.approvedBy ? esc(T.approvedBy) + ' ' + esc(e.approvedBy) + '<br>' + esc(dateTime(e.approvedAt)) : '') + '</div></div>');
            });
            h.push('</div>');
        }

        // Pending
        h.push('<h6 class="fw-semibold mt-3 mb-2">' + esc(T.pendingList) + ' <span class="app-badge app-badge-warning ms-1">' + d.pending.length + '</span></h6>');
        if (!d.pending.length) h.push('<div class="text-muted small mb-2">' + esc(T.noPending) + '</div>');
        else {
            h.push('<div class="d-grid gap-2 mb-2">');
            d.pending.forEach(function (e) {
                h.push('<div class="cov-list-item d-flex justify-content-between align-items-center gap-2"><div><div class="text-dark">' + esc(e.fullName) + '</div><div class="small text-muted">' + esc(e.employeeCode) + ' · ' + esc(T.registeredAt) + ' ' + esc(dateTime(e.createdDate)) + '</div>'
                    + (e.note ? '<div class="small text-muted fst-italic">' + esc(e.note) + '</div>' : '') + '</div>'
                    + (d.isPast ? '' : '<button type="button" class="btn btn-sm btn-outline-secondary text-nowrap btn-approve" data-id="' + e.shiftRegistrationId + '" data-name="' + esc(e.fullName) + '"><i class="bi bi-check2 me-1"></i>' + esc(T.approve) + '</button>')
                    + '</div>');
            });
            h.push('</div>');
        }

        // Suggestions
        if (!d.isPast) {
            h.push('<h6 class="fw-semibold mt-3 mb-1">' + esc(T.suggestions) + '</h6>');
            h.push('<div class="small text-muted mb-2">' + esc(T.suggestionsHint) + '</div>');
            if (!d.suggestions.length) h.push('<div class="text-muted small">' + esc(T.noSuggestions) + '</div>');
            else {
                h.push('<div class="d-grid gap-2">');
                d.suggestions.forEach(function (s) {
                    var right = s.isAvailable
                        ? '<button type="button" class="btn btn-sm btn-outline-secondary text-nowrap btn-assign" data-id="' + s.employeeId + '" data-name="' + esc(s.fullName) + '"><i class="bi bi-person-plus me-1"></i>' + esc(T.assign) + '</button>'
                        : '<span class="small text-muted text-end"><i class="bi bi-slash-circle me-1"></i>' + esc(s.reason) + '</span>';
                    var tag = s.isAvailable
                        ? ' <span class="app-badge app-badge-success ms-1">' + esc(T.available) + '</span>' + (s.hasDeclaredAvailability ? '' : ' <span class="app-badge app-badge-neutral">' + esc(T.noDeclared) + '</span>')
                        : '';
                    h.push('<div class="cov-list-item d-flex justify-content-between align-items-center gap-2 ' + (s.isAvailable ? '' : 'is-disabled') + '"><div><div class="' + (s.isAvailable ? 'text-dark' : '') + '">' + esc(s.fullName) + tag + '</div><div class="small text-muted">' + esc(s.employeeCode) + '</div></div>' + right + '</div>');
                });
                h.push('</div>');
            }
        }
        $('#cellBody').html(h.join(''));
    }

    // Approve a pending registration via the existing Request/UpdateStatus endpoint
    $(document).on('click', '#cellBody .btn-approve', async function () {
        var id = $(this).data('id');
        var name = $(this).attr('data-name');
        var warn = current && current.data ? overWarning(current.data.coverage, 1) : null;
        var ok = warn
            ? await AppConfirm({ title: T.overTitle, message: warn + '\n' + T.confirmContinue, type: 'warning' })
            : await AppConfirm({ message: fmt(T.approveConfirm, [name]), type: 'success', icon: 'bi-check2-circle' });
        if (!ok) return;
        sendApprove(id, false, !!warn, $(this).prop('disabled', true));
    });

    // Máy chủ có thể trả needConfirm + warnings (ngoài thời gian có thể làm việc / vượt nhu cầu) -> xác nhận rồi gửi confirmWarnings=true.
    // Nếu người dùng đã xác nhận cảnh báo vượt nhu cầu ở trên và máy chủ chỉ còn cảnh báo vượt nhu cầu -> gửi lại luôn.
    function sendApprove(id, confirmWarnings, overAlreadyConfirmed, $btn) {
        $.ajax({
            url: '/Request/UpdateStatus', type: 'POST',
            data: { id: id, status: 1, requestType: 'ShiftReg', confirmWarnings: confirmWarnings },
            headers: { 'RequestVerificationToken': token() }
        }).done(async function (res) {
            if (res && res.success) { AppToast.success(res.message || T.approveSuccess); changed = true; loadDetail(); return; }
            if (res && res.needConfirm) {
                var avail = res.availabilityWarnings || [];
                var ok = (overAlreadyConfirmed && avail.length === 0)
                    || await AppConfirm({ title: avail.length ? T.availabilityTitle : T.overTitle, message: (overAlreadyConfirmed ? avail : (res.warnings || [])).join('\n') + '\n' + T.confirmContinue, type: 'warning' });
                if (ok) { sendApprove(id, true, overAlreadyConfirmed, $btn); return; }
            } else AppToast.error((res && res.message) || T.error);
            $btn.prop('disabled', false);
        }).fail(function (xhr) { AppToast.error(errMsg(xhr)); $btn.prop('disabled', false); });
    }

    // Assign a suggested employee (create + approve)
    function sendAssign(employeeId, confirmOver, $btn) {
        $btn.prop('disabled', true);
        $.ajax({
            url: '/ShiftDemand/Assign', type: 'POST', contentType: 'application/json; charset=utf-8',
            data: JSON.stringify({ storeId: storeId, shiftId: current.shiftId, date: current.date, employeeId: employeeId, confirmOver: confirmOver }),
            headers: { 'RequestVerificationToken': token() }
        }).done(async function (res) {
            if (res && res.success) { AppToast.success(res.message); changed = true; loadDetail(); return; }
            if (res && res.needConfirm) {
                var ok = await AppConfirm({ title: T.overTitle, message: (res.warnings || []).join('\n') + '\n' + T.confirmContinue, type: 'warning' });
                if (ok) { sendAssign(employeeId, true, $btn); return; }
            } else AppToast.error((res && res.message) || T.error);
            $btn.prop('disabled', false);
        }).fail(function (xhr) { AppToast.error(errMsg(xhr)); $btn.prop('disabled', false); });
    }

    $(document).on('click', '#cellBody .btn-assign', async function () {
        var employeeId = +$(this).data('id');
        var name = $(this).attr('data-name');
        var ok = await AppConfirm({ message: fmt(T.assignConfirm, [name]), type: 'primary', icon: 'bi-person-plus' });
        if (!ok) return;
        // Luôn để server kiểm tra trước (vượt nhu cầu + thời gian có thể làm việc) rồi mới hỏi xác nhận cảnh báo
        sendAssign(employeeId, false, $(this));
    });
})(jQuery, window.RWPM.shiftDemandCoverage);
