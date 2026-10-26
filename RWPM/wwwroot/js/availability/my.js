// Views/Availability/My.cshtml — form thêm khung giờ có thể làm việc: chọn nhanh ngày, gợi ý ca qua đêm, kiểm tra trước khi gửi.
// Cấu hình: window.RWPM.availabilityMy = { i18n: { daysRequired, timeInvalid, timeSame } }
(function (cfg) {
    'use strict';
    var form = document.getElementById('addWindowForm');
    if (!form) return;

    document.querySelectorAll('[data-days]').forEach(function (a) {
        a.addEventListener('click', function (e) {
            e.preventDefault();
            var wanted = a.getAttribute('data-days').split(',').filter(function (x) { return x !== ''; });
            form.querySelectorAll('.avail-day').forEach(function (cb) { cb.checked = wanted.indexOf(cb.value) >= 0; });
            document.getElementById('daysError').classList.add('d-none');
        });
    });

    function toggleOvernight() {
        var s = document.getElementById('StartTime').value, e = document.getElementById('EndTime').value;
        document.getElementById('overnightHint').style.display = (s && e && e <= s) ? '' : 'none';
    }
    ['StartTime', 'EndTime'].forEach(function (id) {
        var el = document.getElementById(id);
        el.addEventListener('change', toggleOvernight);
        el.addEventListener('input', toggleOvernight);
    });
    toggleOvernight();

    form.addEventListener('submit', function (e) {
        var anyDay = form.querySelectorAll('.avail-day:checked').length > 0;
        var s = document.getElementById('StartTime').value, en = document.getElementById('EndTime').value;
        var re = /^([01][0-9]|2[0-3]):[0-5][0-9]$/;
        document.getElementById('daysError').classList.toggle('d-none', anyDay);
        if (!anyDay) { e.preventDefault(); AppToast.warning(cfg.i18n.daysRequired); return; }
        if (!re.test(s) || !re.test(en)) { e.preventDefault(); AppToast.warning(cfg.i18n.timeInvalid); return; }
        if (s === en) { e.preventDefault(); AppToast.warning(cfg.i18n.timeSame); }
    });
})(window.RWPM.availabilityMy);
