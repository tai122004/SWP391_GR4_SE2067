// =======================================================
// BỘ CHỌN GIỜ DÙNG CHUNG CHO TOÀN DỰ ÁN (định dạng 12 giờ: 07:30 SA / 07:30 AM)
//
// Áp dụng tự động cho: <input type="time">, <input data-time-picker>
//  - Ô gốc (giữ nguyên name/id/validate) vẫn chứa giá trị 24h "HH:mm" để gửi lên server
//    và để code JS cũ đọc/ghi bằng $(...).val() như trước -> không phải sửa backend.
//  - Hiển thị cho người dùng là ô 12h + nút "Gợi ý khung giờ" (giờ thường dùng + mỗi 30 phút).
//  - data-time-suggestions="06:00,14:00,22:00" : danh sách giờ gợi ý riêng cho từng ô.
//  - Gõ được nhiều kiểu: 7, 730, 7:30, 7h30, 7:30 pm, 7:30 CH, 19:30 ...
// API: AppTimePicker.init(root), AppTimePicker.format12(hhmm), AppTimePicker.parse(text)
// =======================================================
(function (window, document) {
    'use strict';

    var t = window.AppText || function (k, f) { return f || k; };
    var DEFAULT_SUGGESTIONS = ['06:00', '07:00', '07:30', '08:00', '09:00', '12:00', '13:00', '14:00', '17:00', '18:00', '21:00', '22:00'];
    var valueDescriptor = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value');

    function pad(n) { return (n < 10 ? '0' : '') + n; }

    function amLabel() { return t('timeAm', 'AM'); }
    function pmLabel() { return t('timePm', 'PM'); }

    // "19:30" -> "07:30 CH" (vi) / "07:30 PM" (en)
    function format12(hhmm) {
        if (!hhmm) return '';
        var m = /^(\d{1,2}):(\d{2})/.exec(hhmm);
        if (!m) return '';
        var h = parseInt(m[1], 10), min = m[2];
        var suffix = h < 12 ? amLabel() : pmLabel();
        var h12 = h % 12 === 0 ? 12 : h % 12;
        return pad(h12) + ':' + min + ' ' + suffix;
    }

    function stripAccents(s) {
        return s.normalize ? s.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd') : s;
    }

    // Văn bản người dùng gõ -> "HH:mm" (24h) | '' (rỗng) | null (không hợp lệ)
    function parse(text) {
        if (text == null) return '';
        var s = stripAccents(String(text).trim().toLowerCase());
        if (!s) return '';

        var isPm = /\b(pm|p|ch|chieu|toi)\b|pm$|p$/.test(s) || new RegExp('\\b' + stripAccents(pmLabel().toLowerCase()) + '\\b').test(s);
        var isAm = /\b(am|a|sa|sang)\b|am$|a$/.test(s) || new RegExp('\\b' + stripAccents(amLabel().toLowerCase()) + '\\b').test(s);
        var digits = s.replace(/[^\d:h.]/g, '').replace(/[h.]/g, ':');

        var h, m;
        var mm = /^(\d{1,2})(?::(\d{1,2}))?:?$/.exec(digits);
        if (mm) {
            h = parseInt(mm[1], 10);
            m = mm[2] != null && mm[2] !== '' ? parseInt(mm[2], 10) : 0;
            if (mm[2] != null && mm[2].length === 1) m = m * 10; // "7:3" -> 7:30
        } else if (/^\d{3,4}$/.test(digits)) {
            h = parseInt(digits.slice(0, digits.length - 2), 10);
            m = parseInt(digits.slice(-2), 10);
        } else {
            return null;
        }

        if (isPm || isAm) {
            if (h < 1 || h > 12) return null;
            if (isPm && h < 12) h += 12;
            if (isAm && h === 12) h = 0;
        }
        if (h > 23 || m > 59 || isNaN(h) || isNaN(m)) return null;
        return pad(h) + ':' + pad(m);
    }

    function normalize24(v) {
        if (!v) return '';
        var m = /^(\d{1,2}):(\d{2})/.exec(v);
        return m ? pad(parseInt(m[1], 10)) + ':' + m[2] : '';
    }

    function setRealValue(real, v24, fire) {
        valueDescriptor.set.call(real, v24);
        if (fire) {
            real.dispatchEvent(new Event('input', { bubbles: true }));
            real.dispatchEvent(new Event('change', { bubbles: true }));
            if (window.jQuery && real.form && window.jQuery(real.form).data('validator')) {
                try { window.jQuery(real).valid(); } catch (e) { /* ignore */ }
            }
        }
    }

    function buildMenu(real, display) {
        var menu = document.createElement('div');
        menu.className = 'dropdown-menu dropdown-menu-end app-time-menu p-2';

        function header(text) {
            var h = document.createElement('div');
            h.className = 'dropdown-header px-1 pt-1 pb-1 small text-uppercase';
            h.textContent = text;
            return h;
        }

        function item(v24) {
            var b = document.createElement('button');
            b.type = 'button';
            b.className = 'btn btn-sm btn-light border app-time-chip';
            b.textContent = format12(v24);
            b.setAttribute('data-value', v24);
            return b;
        }

        var custom = (real.getAttribute('data-time-suggestions') || '').split(',').map(normalize24).filter(Boolean);
        var common = custom.length ? custom : DEFAULT_SUGGESTIONS;

        menu.appendChild(header(t('timeCommon', 'Common times')));
        var commonWrap = document.createElement('div');
        commonWrap.className = 'd-flex flex-wrap gap-1 px-1 mb-2';
        common.forEach(function (v) { commonWrap.appendChild(item(v)); });
        menu.appendChild(commonWrap);

        menu.appendChild(header(t('timeAll', 'All times')));
        var allWrap = document.createElement('div');
        allWrap.className = 'app-time-all d-grid gap-1 px-1';
        for (var h = 0; h < 24; h++) {
            allWrap.appendChild(item(pad(h) + ':00'));
            allWrap.appendChild(item(pad(h) + ':30'));
        }
        menu.appendChild(allWrap);

        var clear = document.createElement('button');
        clear.type = 'button';
        clear.className = 'btn btn-sm btn-link text-danger w-100 mt-1';
        clear.textContent = t('timeClear', 'Clear');
        clear.setAttribute('data-value', '');
        menu.appendChild(clear);

        menu.addEventListener('click', function (e) {
            var btn = e.target.closest('[data-value]');
            if (!btn) return;
            var v = btn.getAttribute('data-value');
            setRealValue(real, v, true);
            display.value = format12(v);
            display.classList.remove('is-invalid');
            if (window.bootstrap && bootstrap.Dropdown) {
                var dd = bootstrap.Dropdown.getInstance(menu.previousElementSibling);
                if (dd) dd.hide();
            }
            display.focus();
        });
        return menu;
    }

    function highlightCurrent(menu, v24) {
        menu.querySelectorAll('.app-time-chip').forEach(function (b) {
            var on = b.getAttribute('data-value') === v24;
            b.classList.toggle('btn-primary', on);
            b.classList.toggle('text-white', on);
            b.classList.toggle('btn-light', !on);
        });
        var target = menu.querySelector('.app-time-all [data-value="' + (v24 || '08:00') + '"]')
            || menu.querySelector('.app-time-all [data-value="' + (v24 ? v24.slice(0, 2) + ':00' : '08:00') + '"]');
        var all = menu.querySelector('.app-time-all');
        if (target && all) all.scrollTop = Math.max(0, target.offsetTop - all.offsetTop - 40);
    }

    function enhance(real) {
        if (real.dataset.timePickerReady === '1') return;
        real.dataset.timePickerReady = '1';

        var initial = normalize24(real.value);
        real.removeAttribute('data-time-mask');
        if (real.type === 'time') real.type = 'text';

        var isSmall = real.classList.contains('form-control-sm');
        var group = document.createElement('div');
        group.className = 'input-group app-time-picker' + (isSmall ? ' input-group-sm' : '');

        var display = document.createElement('input');
        display.type = 'text';
        display.className = 'form-control app-time-display';
        display.placeholder = t('timePlaceholder', 'hh:mm AM');
        display.autocomplete = 'off';
        display.setAttribute('inputmode', 'text');
        if (real.id) {
            display.id = real.id + '__display';
            // Nhãn <label for="..."> trỏ sang ô hiển thị
            document.querySelectorAll('label[for="' + real.id + '"]').forEach(function (l) { l.setAttribute('for', display.id); });
        }
        if (real.disabled) display.disabled = true;
        if (real.readOnly) display.readOnly = true;
        display.value = format12(initial);

        var toggle = document.createElement('button');
        toggle.type = 'button';
        toggle.className = 'btn btn-outline-secondary app-time-toggle';
        toggle.setAttribute('data-bs-toggle', 'dropdown');
        toggle.setAttribute('data-bs-auto-close', 'outside');
        toggle.setAttribute('aria-expanded', 'false');
        toggle.title = t('timeSuggestions', 'Suggested times');
        toggle.setAttribute('aria-label', toggle.title);
        toggle.innerHTML = '<i class="bi bi-clock"></i>';
        if (real.disabled) toggle.disabled = true;

        var menu = buildMenu(real, display);
        group.appendChild(display);
        group.appendChild(toggle);
        group.appendChild(menu);

        real.parentNode.insertBefore(group, real);
        real.classList.add('visually-hidden', 'app-time-value');
        real.setAttribute('tabindex', '-1');
        real.setAttribute('aria-hidden', 'true');
        real.__timeDisplay = display;
        setRealValue(real, initial, false);

        // Menu luôn hiển thị đầy đủ, kể cả trong modal/bảng có thanh cuộn
        if (window.bootstrap && bootstrap.Dropdown) {
            bootstrap.Dropdown.getOrCreateInstance(toggle, {
                autoClose: 'outside',
                popperConfig: { strategy: 'fixed' }
            });
        }
        toggle.addEventListener('shown.bs.dropdown', function () { highlightCurrent(menu, normalize24(real.value)); });

        function commit() {
            var parsed = parse(display.value);
            if (parsed === null) {
                display.classList.add('is-invalid');
                if (window.AppValidation) window.AppValidation.showError(real, t('timeInvalid', 'Invalid time.'));
                setRealValue(real, '', true);
                // giữ lại chữ người dùng gõ để họ sửa
                return;
            }
            display.classList.remove('is-invalid');
            setRealValue(real, parsed, true);
            display.value = format12(parsed);
        }
        display.addEventListener('change', commit);
        display.addEventListener('blur', commit);
        display.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') { e.preventDefault(); commit(); }
            if (e.key === 'ArrowDown' && window.bootstrap) { e.preventDefault(); bootstrap.Dropdown.getOrCreateInstance(toggle).show(); }
        });

        // Code cũ gán giá trị bằng $(input).val('08:30') -> ô hiển thị tự cập nhật theo
        try {
            Object.defineProperty(real, 'value', {
                configurable: true,
                get: function () { return valueDescriptor.get.call(this); },
                set: function (v) {
                    var n = normalize24(v) || (v ? (parse(v) || '') : '');
                    valueDescriptor.set.call(this, n);
                    display.value = format12(n);
                    display.classList.remove('is-invalid');
                }
            });
        } catch (e) { /* trình duyệt không cho override: bỏ qua */ }

        // Đồng bộ trạng thái lỗi/disabled của ô gốc sang ô hiển thị
        new MutationObserver(function () {
            var invalid = real.classList.contains('is-invalid') || real.classList.contains('input-validation-error');
            display.classList.toggle('is-invalid', invalid);
            display.disabled = real.disabled;
            toggle.disabled = real.disabled;
            display.readOnly = real.readOnly;
        }).observe(real, { attributes: true, attributeFilter: ['class', 'disabled', 'readonly'] });
    }

    function init(root) {
        (root || document).querySelectorAll('input[type="time"], input[data-time-picker]').forEach(enhance);
    }

    // Khi validate HTML5 báo lỗi trên ô gốc (đang ẩn) -> đưa con trỏ về ô hiển thị
    document.addEventListener('invalid', function (e) {
        var d = e.target && e.target.__timeDisplay;
        if (d) setTimeout(function () { try { d.focus(); } catch (err) { /* ignore */ } }, 0);
    }, true);

    window.AppTimePicker = { init: init, format12: format12, parse: parse, enhance: enhance };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { init(document); });
    } else {
        init(document);
    }
    // Modal/nội dung tải động
    document.addEventListener('shown.bs.modal', function (e) { init(e.target); });
})(window, document);
