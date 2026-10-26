// Views/Availability/Review.cshtml — duyệt / từ chối khung giờ có thể làm việc (từng dòng hoặc hàng loạt, modal lý do từ chối).
// Cấu hình: window.RWPM.availabilityReview = { urls: { decide }, i18n: { approveTitle, approveOne, approveMany, approveOk, rejectMany, failed } }
(function ($, cfg) {
    'use strict';
    var decideUrl = cfg.urls.decide;
    var text = cfg.i18n;
    var pendingRejectIds = [];

    function token() { return $('#antiForgeryForm input[name="__RequestVerificationToken"]').val(); }
    function selectedIds() { return $('.row-check:checked').map(function () { return parseInt(this.value, 10); }).get(); }
    function refreshBulk() {
        var n = selectedIds().length;
        $('#bulkCount').text(n);
        var bar = document.getElementById('bulkBar');
        if (bar) bar.style.setProperty('display', n > 0 ? 'flex' : 'none', 'important');
        var all = $('.row-check').length;
        $('#selectAll').prop('checked', all > 0 && n === all).prop('indeterminate', n > 0 && n < all);
    }

    function decide(ids, decision, reason, done) {
        $.ajax({
            url: decideUrl,
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ ids: ids, decision: decision, reason: reason || null }),
            headers: { 'RequestVerificationToken': token() },
            success: function (res) {
                if (res && res.success) {
                    AppToast.success(res.message);
                    setTimeout(function () { window.location.reload(); }, 900);
                } else {
                    AppToast.error((res && res.message) || text.failed);
                }
                if (done) done(res && res.success);
            },
            error: function (xhr) {
                AppToast.error((xhr.responseJSON && xhr.responseJSON.message) || text.failed);
                if (done) done(false);
            }
        });
    }

    function approve(ids, label) {
        if (!ids.length) return;
        var msg = ids.length === 1 && label ? text.approveOne.replace('{0}', label) : text.approveMany.replace('{0}', ids.length);
        AppConfirm({ title: text.approveTitle, message: msg, confirmText: text.approveOk, type: 'success' }).then(function (ok) {
            if (ok) decide(ids, 1);
        });
    }

    function openReject(ids, label) {
        if (!ids.length) return;
        pendingRejectIds = ids;
        $('#rejectTarget').text(ids.length === 1 && label ? label : text.rejectMany.replace('{0}', ids.length));
        $('#rejectReason').val('');
        $('#rejectReasonLen').text('0');
        $('#rejectReasonError').addClass('d-none');
        bootstrap.Modal.getOrCreateInstance(document.getElementById('rejectReasonModal')).show();
    }

    $(document).on('change', '.row-check', refreshBulk);
    $('#selectAll').on('change', function () { $('.row-check').prop('checked', this.checked); refreshBulk(); });
    $(document).on('click', '.btn-approve', function () { approve([parseInt($(this).data('id'), 10)], $(this).data('label')); });
    $(document).on('click', '.btn-reject', function () { openReject([parseInt($(this).data('id'), 10)], $(this).data('label')); });
    $('#btnBulkApprove').on('click', function () { approve(selectedIds()); });
    $('#btnBulkReject').on('click', function () { openReject(selectedIds()); });
    $('#rejectReason').on('input', function () { $('#rejectReasonLen').text(this.value.length); });
    $('#rejectReasonModal').on('shown.bs.modal', function () { $('#rejectReason').trigger('focus'); });

    $('#btnConfirmReject').on('click', function () {
        var reason = ($('#rejectReason').val() || '').trim();
        if (reason.length < 1 || reason.length > 255) {
            $('#rejectReasonError').removeClass('d-none');
            return;
        }
        var btn = $(this).prop('disabled', true);
        decide(pendingRejectIds, 2, reason, function (ok) {
            btn.prop('disabled', false);
            if (ok) bootstrap.Modal.getOrCreateInstance(document.getElementById('rejectReasonModal')).hide();
        });
    });

    refreshBulk();
})(jQuery, window.RWPM.availabilityReview);
