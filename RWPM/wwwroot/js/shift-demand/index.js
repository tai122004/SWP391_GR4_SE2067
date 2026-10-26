// Views/ShiftDemand/Index.cshtml — cấu hình nhu cầu nhân sự: mẫu tuần, ngày đặc biệt (override), sao chép mẫu.
// Cấu hình: window.RWPM.shiftDemandIndex = { storeId, today ('yyyy-MM-dd'), i18n: { saved, invalidCell, error, addTitle,
//   editTitle, dateRequired, confirmDelete, confirmCopy, leaveUnsaved } }
(function ($, cfg) {
    'use strict';
    var storeId = cfg.storeId;
    var T = cfg.i18n;
    var today = cfg.today;

    function token() { return $('#antiForgeryForm input[name="__RequestVerificationToken"]').val(); }
    function errMsg(xhr) { return (xhr && xhr.responseJSON && xhr.responseJSON.message) || T.error; }
    function postJson(url, payload) {
        return $.ajax({
            url: url, type: 'POST', contentType: 'application/json; charset=utf-8',
            data: JSON.stringify(payload), headers: { 'RequestVerificationToken': token() }
        });
    }

    // ---------- Weekly template ----------
    var dirty = false;
    function markDirty() {
        dirty = $('.demand-input').toArray().some(function (el) { return el.value !== el.getAttribute('data-original'); });
        $('#templateDirtyHint').css('visibility', dirty ? 'visible' : 'hidden');
    }
    $(document).on('input change', '.demand-input', function () {
        var v = this.value;
        $(this).toggleClass('is-invalid', v !== '' && !(/^\d{1,3}$/.test(v) && +v >= 0 && +v <= 100));
        markDirty();
    });
    window.addEventListener('beforeunload', function (e) { if (dirty) { e.preventDefault(); e.returnValue = T.leaveUnsaved; } });

    $('#btnSaveTemplate').on('click', function () {
        var cells = [], invalid = false;
        $('.demand-input').each(function () {
            var v = (this.value || '').trim();
            if (v !== '' && !(/^\d{1,3}$/.test(v) && +v >= 0 && +v <= 100)) { invalid = true; $(this).addClass('is-invalid'); return; }
            cells.push({ shiftId: +this.getAttribute('data-shift'), dayOfWeek: +this.getAttribute('data-dow'), requiredCount: v === '' ? null : +v });
        });
        if (invalid) { AppToast.warning(T.invalidCell); return; }
        var $btn = $(this).prop('disabled', true);
        postJson('/ShiftDemand/SaveTemplate', { storeId: storeId, cells: cells })
            .done(function (res) {
                if (res && res.success) {
                    $('.demand-input').each(function () { this.setAttribute('data-original', this.value); });
                    markDirty();
                    AppToast.success(res.message || T.saved);
                } else AppToast.error((res && res.message) || T.error);
            })
            .fail(function (xhr) { AppToast.error(errMsg(xhr)); })
            .always(function () { $btn.prop('disabled', false); });
    });

    // ---------- Overrides ----------
    var modalEl = document.getElementById('overrideModal');
    var modal = modalEl ? bootstrap.Modal.getOrCreateInstance(modalEl) : null;
    function updateNoteCount() { $('#ovNoteCount').text(($('#ovNote').val() || '').length); }
    $('#ovNote').on('input', updateNoteCount);

    function openOverride(data) {
        $('#overrideError').addClass('d-none').text('');
        $('#overrideModalTitle').text(data.id ? T.editTitle : T.addTitle);
        $('#ovId').val(data.id || '');
        $('#ovDate').val(data.date || today);
        $('#ovShift').val(String(data.shift || $('#ovShift option:first').val()));
        $('#ovRequired').val(data.required === undefined || data.required === null ? '' : data.required);
        $('#ovNote').val(data.note || '');
        updateNoteCount();
        modal.show();
    }
    $('#btnAddOverride').on('click', function () { openOverride({}); });
    $(document).on('click', '.btn-edit-override', function () {
        var b = $(this);
        openOverride({ id: b.data('id'), shift: b.data('shift'), date: b.attr('data-date'), required: b.data('required'), note: b.attr('data-note') });
    });

    $('#overrideForm').on('submit', function (e) {
        e.preventDefault();
        var $err = $('#overrideError').addClass('d-none');
        var date = $('#ovDate').val();
        var req = ($('#ovRequired').val() || '').trim();
        var note = ($('#ovNote').val() || '').trim();
        if (!date) { $err.text(T.dateRequired).removeClass('d-none'); return; }
        if (!/^\d{1,3}$/.test(req) || +req > 100) { $err.text(T.invalidCell).removeClass('d-none'); return; }
        var payload = {
            id: $('#ovId').val() ? +$('#ovId').val() : null,
            storeId: storeId, shiftId: +$('#ovShift').val(), date: date, requiredCount: +req, note: note || null
        };
        var $btn = $('#btnSubmitOverride').prop('disabled', true);
        postJson('/ShiftDemand/SaveOverride', payload)
            .done(function (res) {
                if (res && res.success) {
                    AppToast.success(res.message);
                    dirty = false;
                    setTimeout(function () { window.location.reload(); }, 600);
                } else $err.text((res && res.message) || T.error).removeClass('d-none');
            })
            .fail(function (xhr) { $err.text(errMsg(xhr)).removeClass('d-none'); })
            .always(function () { $btn.prop('disabled', false); });
    });

    $(document).on('click', '.btn-delete-override', async function () {
        var id = $(this).data('id');
        var label = $(this).attr('data-label');
        if (!(await AppConfirm({ message: T.confirmDelete.replace('{0}', label), type: 'danger' }))) return;
        $.ajax({ url: '/ShiftDemand/DeleteOverride', type: 'POST', data: { id: id }, headers: { 'RequestVerificationToken': token() } })
            .done(function (res) {
                if (res && res.success) {
                    AppToast.success(res.message);
                    dirty = false;
                    setTimeout(function () { window.location.reload(); }, 600);
                } else AppToast.error((res && res.message) || T.error);
            })
            .fail(function (xhr) { AppToast.error(errMsg(xhr)); });
    });

    // ---------- Copy template ----------
    $('#btnSubmitCopy').on('click', async function () {
        var from = +$('#copySource').val();
        var fromName = $('#copySource option:selected').text();
        if (!(await AppConfirm({ message: T.confirmCopy.replace('{0}', fromName), type: 'warning' }))) return;
        var $btn = $(this).prop('disabled', true);
        postJson('/ShiftDemand/CopyTemplate', { fromStoreId: from, toStoreId: storeId })
            .done(function (res) {
                if (res && res.success) {
                    AppToast.success(res.message);
                    dirty = false;
                    setTimeout(function () { window.location.reload(); }, 600);
                } else AppToast.error((res && res.message) || T.error);
            })
            .fail(function (xhr) { AppToast.error(errMsg(xhr)); })
            .always(function () { $btn.prop('disabled', false); });
    });
})(jQuery, window.RWPM.shiftDemandIndex);
