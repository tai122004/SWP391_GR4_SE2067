// Views/Shift/Create.cshtml, Views/Shift/Edit.cshtml (partial _Form.cshtml)
// - Bộ chọn nhiều chi nhánh (.shift-store-picker): tìm kiếm, chọn tất cả / bỏ chọn, hiển thị tên đã chọn.
// - Nút khung giờ gợi ý (.shift-preset): điền giờ bắt đầu/kết thúc/nghỉ giữa ca.
// Không cần cấu hình; nhãn lấy từ data-* trên markup.

(function () {
    'use strict';

    // ---- Bộ chọn chi nhánh (chạy ngay khi tải script, script nằm cuối trang) ----
    document.querySelectorAll('.shift-store-picker').forEach(function (picker) {
        const toggle = picker.querySelector('[data-bs-toggle="dropdown"]');
        const search = picker.querySelector('.store-search');
        const options = Array.from(picker.querySelectorAll('.store-option'));
        const details = picker.querySelector('.store-selection-details');
        const normalize = value => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase().trim();
        function update() {
            const names = options.filter(option => option.querySelector('input').checked)
                .map(option => option.querySelector('label').textContent.trim());
            toggle.textContent = names.length === 1 ? names[0] : names.length
                ? picker.dataset.selectedFormat.replace('{0}', names.length) : picker.dataset.placeholder;
            details.hidden = names.length < 2;
            picker.querySelector('.store-selection-names').textContent = names.join(', ');
        }
        search.addEventListener('input', function () {
            const query = normalize(search.value);
            options.forEach(option => { option.hidden = !normalize(option.querySelector('label').textContent).includes(query); });
            picker.querySelector('.store-no-results').hidden = options.some(option => !option.hidden);
        });
        picker.querySelector('.store-select-all').addEventListener('click', function () {
            options.forEach(option => { option.querySelector('input').checked = true; });
            update();
        });
        picker.querySelector('.store-clear-all').addEventListener('click', function () {
            options.forEach(option => { option.querySelector('input').checked = false; });
            update();
        });
        options.forEach(option => option.querySelector('input').addEventListener('change', update));
        picker.addEventListener('shown.bs.dropdown', function () { search.focus(); });
        picker.closest('form').addEventListener('reset', function () { setTimeout(update, 0); });
        update();
    });

    // ---- Khung giờ gợi ý ----
    // Khung giờ gợi ý cho ca làm việc (hiển thị theo định dạng 12h dùng chung)
    document.addEventListener('DOMContentLoaded', function () {
        var fmt = (window.AppTimePicker && AppTimePicker.format12) || function (v) { return v; };
        function setVal(id, v) { var el = document.getElementById(id); if (el) { el.value = v; el.dispatchEvent(new Event('change', { bubbles: true })); } }
        function setChecked(id, on) { var el = document.getElementById(id); if (el) el.checked = !!on; }
        document.querySelectorAll('.shift-preset').forEach(function (btn) {
            var d = btn.dataset;
            btn.textContent = d.label + ' · ' + fmt(d.start) + ' – ' + fmt(d.end);
            btn.addEventListener('click', function () {
                setVal('StartTime', d.start);
                setVal('EndTime', d.end);
                setChecked('EndsNextDay', d.nextDay === 'true');
                setVal('BreakStartTime', d.breakStart || '');
                setVal('BreakEndTime', d.breakEnd || '');
                setChecked('BreakStartsNextDay', false);
                setChecked('BreakEndsNextDay', false);
            });
        });
    });
})();
