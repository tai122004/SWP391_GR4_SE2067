/*
 * Bảng lương — Views/Payroll/Index.cshtml (chỉ nạp khi người xem là Admin/HR)
 * Modal "Tính lương kỳ mới": kiểm tra khoảng ngày (kỳ lương phải nối tiếp liên tục, tối đa 1 tháng),
 * "Tính bù" khoảng ngày bị hổng.
 * Global: window.openCalculateModal(start?, end?, maxEnd?, gapFill?) — gọi từ onclick trong view.
 * Config: window.RWPM.payrollIndex = {
 *   next: { start, end, maxEnd, startLocked, lastEnd, today },   // ngày dạng yyyy-MM-dd (lastEnd: dd/MM/yyyy)
 *   i18n: { selectBothDates, latestEnd, alreadyCalculatedUntil, endBeforeStart, endAfterToday, maxOneMonth,
 *           summaryPeriodLabel, days, nextPeriodStartsFrom, gapFillInfo, calculating }
 * }
 */
(function ($, cfg) {
    'use strict';

    // Thông tin kỳ tiếp theo do server tính (kỳ lương phải nối tiếp liên tục)
    var NEXT = cfg.next;
    var T = cfg.i18n;
    var infoHtml = document.getElementById('calcPeriodInfoText').innerHTML;
    var isGapFill = false;

    var startEl = document.getElementById('calcStartDate');
    var endEl = document.getElementById('calcEndDate');
    var hintEl = document.getElementById('calcEndHint');
    var summaryEl = document.getElementById('calcSummary');
    var errorEl = document.getElementById('calcError');
    var submitBtn = document.getElementById('calcSubmitBtn');

    function parse(v) { if (!v) return null; var p = v.split('-'); return new Date(+p[0], +p[1] - 1, +p[2]); }
    function iso(d) { return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0'); }
    function vn(d) { return String(d.getDate()).padStart(2, '0') + '/' + String(d.getMonth() + 1).padStart(2, '0') + '/' + d.getFullYear(); }
    function addMonthsMinusOneDay(d) {
        // Giống C#: start.AddMonths(1).AddDays(-1) (kẹp về cuối tháng nếu tháng sau ngắn hơn)
        var y = d.getFullYear(), m = d.getMonth() + 1, day = d.getDate();
        var lastDay = new Date(y, m + 1, 0).getDate();
        var r = new Date(y, m, Math.min(day, lastDay));
        r.setDate(r.getDate() - 1);
        return r;
    }
    function minDate(a, b) { return a < b ? a : b; }

    var gapMaxEnd = null;

    function validate() {
        var s = parse(startEl.value), e = parse(endEl.value), today = parse(NEXT.today);
        var errors = [];
        if (!s || !e) {
            errors.push(T.selectBothDates);
        } else {
            var maxEnd = minDate(addMonthsMinusOneDay(s), today);
            if (gapMaxEnd) maxEnd = minDate(maxEnd, gapMaxEnd);
            endEl.min = iso(s);
            endEl.max = iso(maxEnd);
            hintEl.textContent = T.latestEnd.replace('{0}', vn(maxEnd));

            if (s > today) errors.push(T.alreadyCalculatedUntil.replace('{0}', NEXT.lastEnd).replace('{1}', vn(s)));
            else if (e < s) errors.push(T.endBeforeStart.replace('{0}', vn(s)));
            else if (e > today) errors.push(T.endAfterToday.replace('{0}', vn(today)));
            else if (e > maxEnd) errors.push(T.maxOneMonth.replace('{0}', vn(maxEnd)));
        }

        if (errors.length) {
            errorEl.innerHTML = '<i class="bi bi-x-circle me-1"></i>' + errors[0];
            errorEl.classList.remove('d-none');
            endEl.classList.add('is-invalid');
            summaryEl.innerHTML = '';
            submitBtn.disabled = true;
            return false;
        }

        errorEl.classList.add('d-none');
        endEl.classList.remove('is-invalid');
        submitBtn.disabled = false;
        var days = Math.round((e - s) / 86400000) + 1;
        var next = new Date(e); next.setDate(next.getDate() + 1);
        summaryEl.innerHTML =
            '<div class="d-flex flex-wrap gap-3 text-secondary">' +
            '<span><i class="bi bi-calendar-range me-1"></i>' + T.summaryPeriodLabel + ' <strong class="text-dark">' + T.days.replace('{0}', days) + '</strong></span>' +
            (isGapFill ? '' : '<span><i class="bi bi-arrow-right-circle me-1"></i>' + T.nextPeriodStartsFrom + ' <strong class="text-dark">' + vn(next) + '</strong></span>') +
            '</div>';
        return true;
    }

    // start/end/maxEnd: dùng khi "Tính bù" một khoảng ngày bị hổng
    function openCalculateModal(start, end, maxEnd, gapFill) {
        isGapFill = !!gapFill;
        gapMaxEnd = gapFill && maxEnd ? parse(maxEnd) : null;
        startEl.value = start || NEXT.start;
        endEl.value = end || NEXT.end;
        var lockStart = isGapFill || NEXT.startLocked;
        startEl.readOnly = lockStart;
        startEl.classList.toggle('bg-light', lockStart);

        var infoText = document.getElementById('calcPeriodInfoText');
        if (isGapFill) {
            infoText.innerHTML = T.gapFillInfo.replace('{0}', vn(parse(start)));
        } else {
            infoText.innerHTML = infoHtml;
        }
        validate();
        bootstrap.Modal.getOrCreateInstance(document.getElementById('calculateModal')).show();
    }
    window.openCalculateModal = openCalculateModal; // dùng trong onclick="openCalculateModal(...)"

    startEl.addEventListener('change', function () {
        // Kỳ đầu tiên: tự gợi ý ngày kết thúc tròn 1 tháng khi đổi ngày bắt đầu
        var s = parse(startEl.value);
        if (s && !startEl.readOnly) endEl.value = iso(minDate(addMonthsMinusOneDay(s), parse(NEXT.today)));
        validate();
    });
    endEl.addEventListener('change', validate);
    endEl.addEventListener('input', validate);

    document.getElementById('calculateForm').addEventListener('submit', function (e) {
        if (!validate()) {
            e.preventDefault();
            return;
        }
        submitBtn.disabled = true;
        submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>' + T.calculating;
    });
})(jQuery, window.RWPM.payrollIndex);
