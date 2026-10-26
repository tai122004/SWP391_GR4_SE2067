// =======================================================
// THÔNG BÁO DÙNG CHUNG (TOAST GÓC PHẢI) & POPUP XÁC NHẬN
// - AppToast.success/error/warning/info(message)
// - AppConfirm({ title, message, confirmText, cancelText, type }) -> Promise<boolean>
// - <form data-confirm="..."> / <a data-confirm="..."> tự hiện popup trước khi thực hiện
// =======================================================
(function (window, document) {
    'use strict';

    // Chuỗi đa ngôn ngữ do _Layout.cshtml xuất ra từ SharedResource (window.AppI18n)
    function t(key, fallback) {
        var dict = window.AppI18n || {};
        return dict[key] || fallback || key;
    }
    window.AppText = t;

    var TOAST_DURATION = 7000;
    // Kiểu "design system v2": nền trắng, viền mảnh, bo 6px; màu chỉ nằm ở biểu tượng trạng thái
    var TOAST_STYLES = {
        success: { icon: 'bi-check-circle-fill', title: 'toastSuccess', fallback: 'Success' },
        error:   { icon: 'bi-x-circle-fill', title: 'toastError', fallback: 'Error' },
        warning: { icon: 'bi-exclamation-triangle-fill', title: 'toastWarning', fallback: 'Warning' },
        info:    { icon: 'bi-info-circle-fill', title: 'toastInfo', fallback: 'Information' }
    };

    // CSS của toast + popup xác nhận (dùng token của site.css, có giá trị dự phòng)
    function ensureStyles() {
        if (document.getElementById('appUiKitStyle')) return;
        var css =
            '#appToastContainer{display:flex;flex-direction:column;align-items:flex-end;gap:8px}' +
            '.app-toast{display:flex;align-items:flex-start;gap:10px;width:360px;max-width:calc(100vw - 2rem);padding:12px 12px 12px 14px;' +
            'background:var(--app-surface,#fff);color:var(--app-text,#17202B);border:1px solid var(--app-border,#E3E6EA);border-radius:var(--app-radius,6px);' +
            'box-shadow:var(--app-overlay-shadow,0 8px 24px rgba(23,32,43,.12));font-size:.93rem;line-height:1.4;transition:opacity .2s ease}' +
            '.app-toast:not(.show){opacity:0}' +
            '.app-toast-icon{font-size:1.05rem;line-height:1.35;flex:0 0 auto}' +
            '.app-toast-success .app-toast-icon{color:var(--app-success,#1F7A4D)}' +
            '.app-toast-error .app-toast-icon{color:var(--app-danger,#B42318)}' +
            '.app-toast-warning .app-toast-icon{color:#C27A0E}' +
            '.app-toast-info .app-toast-icon{color:var(--app-info,#2F6FB3)}' +
            '.app-toast-body{flex:1 1 auto;min-width:0}' +
            '.app-toast-title{font-weight:600}' +
            '.app-toast-msg{color:var(--app-text-2,#5B6573);white-space:pre-line;overflow-wrap:anywhere}' +
            '.app-toast-time{margin-top:2px;color:var(--app-muted,#6B7480);font-size:.75rem;font-variant-numeric:tabular-nums}' +
            '.app-toast-close{flex:0 0 auto;font-size:.65rem;padding:.35rem;margin:-2px -2px 0 0}' +
            '#appConfirmModal .modal-body{display:flex;gap:12px;align-items:flex-start}' +
            '#appConfirmModal .app-confirm-icon{font-size:1.15rem;line-height:1.4;flex:0 0 auto}' +
            '#appConfirmModal .app-confirm-message{color:var(--app-text-2,#5B6573);white-space:pre-line;margin:0}';
        var style = document.createElement('style');
        style.id = 'appUiKitStyle';
        style.appendChild(document.createTextNode(css));
        (document.head || document.documentElement).appendChild(style);
    }

    function pad(n) { return n < 10 ? '0' + n : '' + n; }
    function nowText() {
        var d = new Date();
        return pad(d.getDate()) + '/' + pad(d.getMonth() + 1) + '/' + d.getFullYear() + ' ' +
            pad(d.getHours()) + ':' + pad(d.getMinutes()) + ':' + pad(d.getSeconds());
    }

    function getContainer() {
        var c = document.getElementById('appToastContainer');
        if (!c) {
            c = document.createElement('div');
            c.id = 'appToastContainer';
            c.className = 'position-fixed bottom-0 end-0 p-3';
            c.style.zIndex = '1090';
            document.body.appendChild(c);
        }
        return c;
    }

    function show(type, message, options) {
        if (message === undefined || message === null || message === '') return null;
        if (!TOAST_STYLES[type]) type = 'info';
        var style = TOAST_STYLES[type];
        var opts = options || {};
        ensureStyles();

        var el = document.createElement('div');
        el.className = 'app-toast app-toast-' + type + ' fade show';
        el.setAttribute('role', type === 'error' ? 'alert' : 'status');
        el.setAttribute('aria-live', type === 'error' ? 'assertive' : 'polite');
        el.setAttribute('data-toast-type', type);

        var icon = document.createElement('i');
        icon.className = 'bi ' + style.icon + ' app-toast-icon';
        icon.setAttribute('aria-hidden', 'true');

        var body = document.createElement('div');
        body.className = 'app-toast-body';
        var title = document.createElement('div');
        title.className = 'app-toast-title';
        title.textContent = opts.title || t(style.title, style.fallback);
        var text = document.createElement('div');
        text.className = 'app-toast-msg';
        text.textContent = String(message);
        var time = document.createElement('div');
        time.className = 'app-toast-time';
        time.textContent = opts.time || nowText();
        body.appendChild(title);
        body.appendChild(text);
        body.appendChild(time);

        var btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'btn-close app-toast-close';
        btn.setAttribute('aria-label', t('close', 'Close'));

        el.appendChild(icon);
        el.appendChild(body);
        el.appendChild(btn);
        getContainer().appendChild(el);

        var removed = false;
        function close() {
            if (removed) return;
            removed = true;
            el.classList.remove('show');
            setTimeout(function () { if (el.parentNode) el.parentNode.removeChild(el); }, 200);
        }
        btn.addEventListener('click', close);

        var duration = opts.duration === undefined ? TOAST_DURATION : opts.duration;
        if (duration > 0) setTimeout(close, duration);
        return el;
    }

    var AppToast = {
        show: show,
        success: function (m, o) { return show('success', m, o); },
        error: function (m, o) { return show('error', m, o); },
        warning: function (m, o) { return show('warning', m, o); },
        info: function (m, o) { return show('info', m, o); }
    };

    // ---------- Popup xác nhận (thay cho confirm() của trình duyệt) ----------
    // Một nút chính duy nhất: btn-primary, riêng thao tác phá hủy (xóa, từ chối...) dùng btn-danger
    var CONFIRM_TYPES = {
        danger:  { icon: 'bi-exclamation-octagon', color: 'danger',  btn: 'btn-danger' },
        warning: { icon: 'bi-exclamation-triangle', color: 'warning', btn: 'btn-primary' },
        primary: { icon: 'bi-question-circle', color: 'primary', btn: 'btn-primary' },
        success: { icon: 'bi-check2-circle', color: 'success', btn: 'btn-primary' }
    };

    var confirmModalEl = null;
    function ensureConfirmModal() {
        if (confirmModalEl) return confirmModalEl;
        ensureStyles();
        confirmModalEl = document.createElement('div');
        confirmModalEl.className = 'modal fade';
        confirmModalEl.id = 'appConfirmModal';
        confirmModalEl.tabIndex = -1;
        confirmModalEl.setAttribute('aria-hidden', 'true');
        confirmModalEl.setAttribute('aria-labelledby', 'appConfirmModalTitle');
        confirmModalEl.style.zIndex = '1070';
        confirmModalEl.innerHTML =
            '<div class="modal-dialog modal-dialog-centered" style="max-width:460px">' +
            '  <div class="modal-content">' +
            '    <div class="modal-header">' +
            '      <h5 class="modal-title app-confirm-title" id="appConfirmModalTitle"></h5>' +
            '      <button type="button" class="btn-close" data-bs-dismiss="modal"></button>' +
            '    </div>' +
            '    <div class="modal-body">' +
            '      <i class="bi app-confirm-icon" aria-hidden="true"></i>' +
            '      <p class="app-confirm-message"></p>' +
            '    </div>' +
            '    <div class="modal-footer">' +
            '      <button type="button" class="btn btn-outline-secondary app-confirm-cancel" data-bs-dismiss="modal"></button>' +
            '      <button type="button" class="btn btn-primary app-confirm-ok"></button>' +
            '    </div>' +
            '  </div>' +
            '</div>';
        document.body.appendChild(confirmModalEl);
        return confirmModalEl;
    }

    function guessType(text) {
        var t = (text || '').toLowerCase();
        if (/(xóa|xoá|delete|remove|từ chối|reject|hủy|huỷ|cancel)/.test(t)) return 'danger';
        if (/(chốt|lock|tính lại|recalc|mở khóa|unlock)/.test(t)) return 'warning';
        return 'primary';
    }

    function AppConfirm(options) {
        if (typeof options === 'string') options = { message: options };
        options = options || {};
        var type = options.type || guessType((options.title || '') + ' ' + (options.message || '') + ' ' + (options.confirmText || ''));
        var cfg = CONFIRM_TYPES[type] || CONFIRM_TYPES.primary;

        // Không có Bootstrap -> fallback về confirm() mặc định
        if (typeof bootstrap === 'undefined' || !bootstrap.Modal) {
            return Promise.resolve(window.confirm(options.message || ''));
        }

        var el = ensureConfirmModal();
        var icon = el.querySelector('.app-confirm-icon');
        icon.className = 'bi app-confirm-icon ' + (options.icon || cfg.icon) + ' text-' + cfg.color;
        el.querySelector('.app-confirm-title').textContent = options.title || t('confirmTitle', 'Please confirm');
        el.querySelector('.app-confirm-message').textContent = options.message || '';
        el.querySelector('.modal-body').style.display = options.message ? '' : 'none';
        el.querySelector('.modal-header .btn-close').setAttribute('aria-label', t('close', 'Close'));
        var okBtn = el.querySelector('.app-confirm-ok');
        okBtn.className = 'btn app-confirm-ok ' + cfg.btn;
        okBtn.textContent = options.confirmText || t('confirmOk', 'Confirm');
        el.querySelector('.app-confirm-cancel').textContent = options.cancelText || t('cancel', 'Cancel');

        var modal = bootstrap.Modal.getOrCreateInstance(el, { backdrop: 'static' });

        return new Promise(function (resolve) {
            var result = false;
            function onOk() { result = true; modal.hide(); }
            function onHidden() {
                okBtn.removeEventListener('click', onOk);
                el.removeEventListener('hidden.bs.modal', onHidden);
                resolve(result);
            }
            okBtn.addEventListener('click', onOk);
            el.addEventListener('hidden.bs.modal', onHidden);
            el.addEventListener('shown.bs.modal', function () {
                // Popup xác nhận có thể mở chồng lên một modal khác -> luôn nằm trên cùng
                var backdrops = document.querySelectorAll('.modal-backdrop');
                if (backdrops.length > 1) backdrops[backdrops.length - 1].style.zIndex = '1065';
                okBtn.focus();
            }, { once: true });
            modal.show();
        });
    }

    function readConfirmOptions(el) {
        return {
            message: el.getAttribute('data-confirm'),
            title: el.getAttribute('data-confirm-title') || undefined,
            confirmText: el.getAttribute('data-confirm-ok') || undefined,
            cancelText: el.getAttribute('data-confirm-cancel') || undefined,
            type: el.getAttribute('data-confirm-type') || undefined,
            icon: el.getAttribute('data-confirm-icon') || undefined
        };
    }

    // <form data-confirm="..."> : chặn submit, hỏi popup, đồng ý thì submit thật
    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!(form instanceof HTMLFormElement) || !form.hasAttribute('data-confirm')) return;
        if (form.dataset.appConfirmed === '1') { delete form.dataset.appConfirmed; return; }
        e.preventDefault();
        e.stopImmediatePropagation();
        var submitter = e.submitter;
        AppConfirm(readConfirmOptions(form)).then(function (ok) {
            if (!ok) return;
            form.dataset.appConfirmed = '1';
            if (form.requestSubmit) {
                form.requestSubmit(submitter && submitter.form === form ? submitter : undefined);
            } else {
                form.submit();
            }
        });
    }, true);

    // <a data-confirm="..." href="..."> / <button data-confirm> ngoài form
    document.addEventListener('click', function (e) {
        var el = e.target.closest ? e.target.closest('a[data-confirm], button[data-confirm]:not([type="submit"])') : null;
        if (!el) return;
        if (el.dataset.appConfirmed === '1') { delete el.dataset.appConfirmed; return; }
        e.preventDefault();
        e.stopImmediatePropagation();
        AppConfirm(readConfirmOptions(el)).then(function (ok) {
            if (!ok) return;
            el.dataset.appConfirmed = '1';
            el.click();
        });
    }, true);

    window.AppToast = AppToast;
    window.AppConfirm = AppConfirm;
    // Tương thích ngược: mọi lời gọi toastr.* cũ đều hiển thị cùng một kiểu toast góc phải
    window.toastr = {
        success: AppToast.success,
        error: AppToast.error,
        warning: AppToast.warning,
        info: AppToast.info,
        options: {}
    };
})(window, document);

// =======================================================
// VALIDATE: LỖI HIỂN THỊ NGAY DƯỚI TỪNG Ô NHẬP (ĐA NGÔN NGỮ)
// 1. Form có jQuery Validation (unobtrusive): tự tạo <span data-valmsg-for> cho ô nào còn thiếu,
//    và dịch các thông báo mặc định (step, min, max, email...) theo ngôn ngữ đang chọn.
// 2. Form không dùng jQuery Validation (modal, form AJAX...): chặn bong bóng mặc định của trình duyệt,
//    hiển thị lỗi bên dưới ô nhập bằng tiếng Việt/Anh theo resource.
// 3. Lỗi trả về từ server (ModelState) được gắn đúng vào từng ô (window.AppServerErrors do _Layout xuất ra).
// =======================================================
(function (window, document, $) {
    'use strict';
    var t = window.AppText || function (k, f) { return f || k; };

    function cssEscape(v) { return window.CSS && CSS.escape ? CSS.escape(v) : String(v).replace(/(["\\.\[\]#:])/g, '\\$1'); }

    // Vị trí đặt thông báo: sau input-group / select2 / chính ô nhập
    function anchorOf(field) {
        var group = field.closest('.input-group');
        if (group) return group;
        var next = field.nextElementSibling;
        if (next && next.classList && next.classList.contains('select2')) return next;
        return field;
    }

    function messageHolder(field, create) {
        var form = field.form || document;
        var name = field.getAttribute('name');
        if (name) {
            var existing = form.querySelector('[data-valmsg-for="' + cssEscape(name) + '"]');
            if (existing) return existing;
        }
        var anchor = anchorOf(field);
        var sib = anchor.nextElementSibling;
        if (sib && sib.classList.contains('app-field-error')) return sib;
        if (!create) return null;
        var span = document.createElement('span');
        span.className = 'app-field-error text-danger small d-block mt-1';
        if (name) {
            span.setAttribute('data-valmsg-for', name);
            span.setAttribute('data-valmsg-replace', 'true');
            span.classList.add('field-validation-valid');
        }
        anchor.parentNode.insertBefore(span, anchor.nextSibling);
        return span;
    }

    function showError(field, message) {
        var holder = messageHolder(field, true);
        holder.textContent = message;
        holder.classList.remove('field-validation-valid');
        holder.classList.add('field-validation-error');
        field.classList.add('is-invalid', 'input-validation-error');
    }

    function clearError(field) {
        var holder = messageHolder(field, false);
        if (holder && !holder.hasAttribute('data-valmsg-for-locked')) {
            holder.textContent = '';
            holder.classList.remove('field-validation-error');
            holder.classList.add('field-validation-valid');
        }
        field.classList.remove('is-invalid', 'input-validation-error');
    }

    // Thông báo bản địa hóa cho HTML5 validity
    function nativeMessage(field) {
        var v = field.validity;
        if (v.valueMissing) return field.getAttribute('data-val-required') || t('required', 'This field is required.');
        if (v.typeMismatch) return field.type === 'email' ? t('email') : (field.type === 'url' ? t('url') : t('pattern'));
        if (v.badInput) return field.type === 'date' ? t('date') : t('number');
        if (v.stepMismatch) return t('step').replace('{0}', field.getAttribute('step'));
        if (v.rangeUnderflow) return t('min').replace('{0}', field.getAttribute('min'));
        if (v.rangeOverflow) return t('max').replace('{0}', field.getAttribute('max'));
        if (v.tooLong) return t('maxlength').replace('{0}', field.getAttribute('maxlength'));
        if (v.tooShort) return t('minlength').replace('{0}', field.getAttribute('minlength'));
        if (v.patternMismatch) return field.getAttribute('title') || t('pattern');
        return field.validationMessage;
    }

    // (2) Form không có jQuery Validation: thay bong bóng trình duyệt bằng lỗi dưới ô nhập
    var firstInvalidHandled = null;
    document.addEventListener('invalid', function (e) {
        var field = e.target;
        if (!field || !field.form) return;
        e.preventDefault();
        showError(field, nativeMessage(field));
        if (firstInvalidHandled !== field.form) {
            firstInvalidHandled = field.form;
            try { field.focus({ preventScroll: false }); } catch (err) { field.focus(); }
            setTimeout(function () { firstInvalidHandled = null; }, 0);
        }
    }, true);

    function revalidate(e) {
        var field = e.target;
        if (!field || !field.classList || !field.classList.contains('is-invalid')) return;
        if (field.form && $ && $.data && $(field.form).data('validator')) return; // jQuery Validation tự xử lý
        if (field.checkValidity && field.checkValidity()) clearError(field);
    }
    document.addEventListener('input', revalidate, true);
    document.addEventListener('change', revalidate, true);

    // Thông báo mặc định của jQuery Validation theo ngôn ngữ hiện tại (chuỗi lấy từ SharedResource qua window.AppI18n).
    // Gọi ngay sau khi nạp jquery.validate (_ValidationScriptsPartial) và lại lúc DOM ready; gọi nhiều lần không sao.
    function applyValidatorMessages() {
        if (!$ || !$.validator) return false;
        var fmt = $.validator.format;
        $.extend($.validator.messages, {
            required: t('required'),
            remote: t('remote'),
            email: t('email'),
            url: t('url'),
            date: t('date'),
            dateISO: t('date'),
            number: t('number'),
            digits: t('digits'),
            creditcard: t('creditcard'),
            equalTo: t('equalTo'),
            maxlength: fmt(t('maxlength')),
            minlength: fmt(t('minlength')),
            rangelength: fmt(t('rangelength')),
            range: fmt(t('range')),
            max: fmt(t('max')),
            min: fmt(t('min')),
            step: fmt(t('step'))
        });
        // Số kiểu Việt Nam (25.000 / 25000,5) vẫn được chấp nhận ở phía client
        $.validator.methods.number = function (value, element) {
            return this.optional(element) || /^-?\d+([.,]\d+)?$/.test(String(value).replace(/\s/g, ''));
        };
        return true;
    }
    window.AppValidationI18n = { apply: applyValidatorMessages };

    function prepareForms() {
        // (1) Tạo chỗ hiển thị lỗi cho mọi ô có data-val nhưng chưa có <span asp-validation-for>
        document.querySelectorAll('form [data-val="true"][name]').forEach(function (field) {
            if (field.type === 'hidden') return;
            messageHolder(field, true);
        });

        // Dịch thông báo mặc định của jQuery Validation
        applyValidatorMessages();

        // (3) Lỗi từ server: gắn vào đúng ô nhập
        var serverErrors = window.AppServerErrors || {};
        Object.keys(serverErrors).forEach(function (key) {
            if (!key) return;
            var field = document.querySelector('[name="' + cssEscape(key) + '"]');
            if (!field) return;
            var holder = messageHolder(field, true);
            if (!holder.textContent.trim()) {
                holder.textContent = serverErrors[key];
                holder.classList.remove('field-validation-valid');
                holder.classList.add('field-validation-error');
            }
            field.classList.add('is-invalid', 'input-validation-error');
        });
    }

    // Chạy trước jquery.validate.unobtrusive (được nạp sau site.js) để span có sẵn khi nó parse form
    if ($) { $(prepareForms); } else { document.addEventListener('DOMContentLoaded', prepareForms); }

    // Menu dropdown (VD: nút "Thao tác") nằm trong bảng/khung có thanh cuộn bị che mất:
    // cho menu thoát ra khỏi khung bằng định vị fixed (Popper strategy 'fixed') -> luôn hiển thị đầy đủ.
    // Đăng ký trên window (capture) để chạy TRƯỚC handler của Bootstrap (Bootstrap bắt sự kiện ở document, capture)
    window.addEventListener('click', function (e) {
        var toggle = e.target.closest ? e.target.closest('[data-bs-toggle="dropdown"]') : null;
        if (!toggle || typeof bootstrap === 'undefined' || !bootstrap.Dropdown) return;
        if (!toggle.closest('.table-responsive, .overflow-auto, .overflow-hidden, .overflow-scroll, .modal-body')) return;
        if (bootstrap.Dropdown.getInstance(toggle)) return;
        bootstrap.Dropdown.getOrCreateInstance(toggle, { popperConfig: { strategy: 'fixed' } });
    }, true);

    // Ô số điện thoại: chỉ cho nhập chữ số, tối đa 10 số (quy tắc: 10 số, bắt đầu bằng 0)
    document.addEventListener('input', function (e) {
        var el = e.target;
        if (!el || !el.matches || !el.matches('[data-phone-input]')) return;
        var digits = el.value.replace(/\D/g, '').slice(0, 10);
        if (digits !== el.value) el.value = digits;
    }, true);

    window.AppValidation = { showError: showError, clearError: clearError };
})(window, document, window.jQuery);

// =======================================================
// UI/UX UTILITIES: TỐI ƯU HÓA THAO TÁC NGƯỜI DÙNG
// =======================================================

// 1. Sinh mật khẩu ngẫu nhiên an toàn & dễ đọc
function generateRandomPassword(length = 8) {
    const uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    const lowercase = "abcdefghijkmnpqrstuvwxyz";
    const numbers = "23456789";
    const special = "@#$!";

    let password = "";
    password += uppercase.charAt(Math.floor(Math.random() * uppercase.length));
    password += lowercase.charAt(Math.floor(Math.random() * lowercase.length));
    password += numbers.charAt(Math.floor(Math.random() * numbers.length));
    password += special.charAt(Math.floor(Math.random() * special.length));

    const allChars = uppercase + lowercase + numbers + special;
    for (let i = password.length; i < length; i++) {
        password += allChars.charAt(Math.floor(Math.random() * allChars.length));
    }

    return password.split('').sort(() => 0.5 - Math.random()).join('');
}

// 2. Sao chép nội dung vào Clipboard kèm Toastr
function copyToClipboard(text, successMessage) {
    if (!text) return;
    if (navigator.clipboard && window.isSecureContext) {
        navigator.clipboard.writeText(text).then(() => {
            if (typeof AppToast !== 'undefined') {
                AppToast.success(successMessage || AppText('copied'));
            } else {
                AppToast.success(successMessage || AppText('copied'));
            }
        }).catch(err => {
            console.error('Lỗi khi sao chép:', err);
        });
    } else {
        const textArea = document.createElement("textarea");
        textArea.value = text;
        textArea.style.position = "fixed";
        textArea.style.left = "-999999px";
        document.body.appendChild(textArea);
        textArea.focus();
        textArea.select();
        try {
            document.execCommand('copy');
            if (typeof AppToast !== 'undefined') {
                AppToast.success(successMessage || AppText('copied'));
            }
        } catch (err) {
            console.error('Fallback copy error', err);
        }
        document.body.removeChild(textArea);
    }
}

// 3. Chuyển đổi tên tiếng Việt thành username gợi ý (VD: "Nguyễn Văn An" -> "an_nv")
// Kết quả khớp UsernameRegex (bắt đầu bằng chữ, chỉ gồm chữ/số/_/-) và dài 5-30 ký tự
function generateUsernameFromFullName(fullName) {
    if (!fullName) return "";
    let str = fullName.toLowerCase().trim();
    str = str.normalize('NFD').replace(/[\u0300-\u036f]/g, '');
    str = str.replace(/[đĐ]/g, 'd');
    str = str.replace(/[^a-z0-9\s]/g, '');
    const parts = str.split(/\s+/).filter(Boolean);
    if (parts.length === 0) return "";

    let result;
    if (parts.length === 1) {
        result = parts[0];
    } else {
        const lastName = parts[parts.length - 1];
        const initials = parts.slice(0, parts.length - 1).map(p => p[0]).join('');
        result = `${lastName}_${initials}`;
    }
    if (!/^[a-z]/.test(result)) result = 'u' + result;
    if (result.length < 5) result = result.padEnd(5, '0');
    return result.substring(0, 30);
}

// 4. Modal Đổi Mật Khẩu Nhanh
function updateQuickResetCopyText() {
    const modalEl = $('#modalQuickResetPassword');
    const labelAccount = modalEl.data('label-account') || AppText('account', 'Account');
    const labelPassword = modalEl.data('label-password') || AppText('password', 'Password');
    const username = $('#quickResetModal_Username').val();
    const password = $('#quickResetModal_NewPassword').val();
    $('#quickResetModal_CopyText').text(`${labelAccount}: ${username} | ${labelPassword}: ${password}`);
}

function regenerateQuickResetPassword() {
    const generated = generateRandomPassword(8);
    $('#quickResetModal_NewPassword').val(generated);
    $('#quickResetModal_ConfirmPassword').val(generated);
    updateQuickResetCopyText();
}

function openQuickResetPassword(username, fullName) {
    $('#quickResetModal_Username').val(username);
    $('#quickResetModal_DisplayUser').text(fullName ? `${fullName} (@${username})` : `@${username}`);
    
    const generated = generateRandomPassword(8);
    $('#quickResetModal_NewPassword').val(generated);
    $('#quickResetModal_ConfirmPassword').val(generated);
    updateQuickResetCopyText();

    const modalEl = document.getElementById('modalQuickResetPassword');
    if (modalEl) {
        const modal = new bootstrap.Modal(modalEl);
        modal.show();
    }
}

function submitQuickResetPassword() {
    const modal = $('#modalQuickResetPassword');
    const username = $('#quickResetModal_Username').val();
    const newPassword = $('#quickResetModal_NewPassword').val();
    const confirmPassword = $('#quickResetModal_ConfirmPassword').val();
    const token = $('input[name="__RequestVerificationToken"]').val();

    const msgMinLen = modal.data('err-minlen') || AppText('passwordMinLength');
    const msgMismatch = modal.data('err-match') || AppText('passwordMismatch');
    const textSaving = modal.data('text-saving') || AppText('saving');
    const msgSuccess = modal.data('toast-success') || AppText('resetPasswordSuccess').replace('{0}', username);

    if (!newPassword || newPassword.length < 8 || newPassword.length > 100 || !/[a-z]/.test(newPassword) || !/[A-Z]/.test(newPassword) || !/[0-9]/.test(newPassword)) {
        if (typeof AppToast !== 'undefined') {
            AppToast.warning(msgMinLen);
        } else {
            AppToast.warning(msgMinLen);
        }
        return;
    }

    if (newPassword !== confirmPassword) {
        if (typeof AppToast !== 'undefined') {
            AppToast.warning(msgMismatch);
        } else {
            AppToast.warning(msgMismatch);
        }
        return;
    }

    const btnSubmit = $('#quickResetModal_BtnSubmit');
    const originalBtnHtml = btnSubmit.html();
    btnSubmit.prop('disabled', true).html(`<span class="spinner-border spinner-border-sm me-1"></span> ${textSaving}`);

    $.ajax({
        url: '/Acc/QuickResetPassword',
        type: 'POST',
        contentType: 'application/json',
        headers: {
            'RequestVerificationToken': token
        },
        data: JSON.stringify({
            username: username,
            newPassword: newPassword,
            confirmPassword: confirmPassword
        }),
        success: function () {
            const modalEl = document.getElementById('modalQuickResetPassword');
            if (modalEl) {
                const modal = bootstrap.Modal.getInstance(modalEl);
                if (modal) modal.hide();
            }
            if (typeof AppToast !== 'undefined') {
                AppToast.success(msgSuccess);
            }
        },
        error: function (xhr) {
            const msg = xhr.responseText || AppText('resetPasswordError');
            if (typeof AppToast !== 'undefined') {
                AppToast.error(msg);
            } else {
                AppToast.error(msg);
            }
        },
        complete: function () {
            btnSubmit.prop('disabled', false).html(originalBtnHtml);
        }
    });
}

document.addEventListener('DOMContentLoaded', function () {
    const menuButton = document.getElementById('appMenuToggle');
    const sidebar = document.getElementById('appSidebar');
    if (menuButton && sidebar) {
        menuButton.addEventListener('click', function () {
            const isOpen = sidebar.classList.toggle('is-open');
            menuButton.setAttribute('aria-expanded', String(isOpen));
        });
        document.addEventListener('click', function (event) {
            if (window.innerWidth <= 991 && !sidebar.contains(event.target) && !menuButton.contains(event.target)) {
                sidebar.classList.remove('is-open');
                menuButton.setAttribute('aria-expanded', 'false');
            }
        });
    }
    const pathParts = window.location.pathname.split('/').filter(Boolean);
    const controller = pathParts[0]?.toLowerCase() || 'home';
    const action = pathParts[1]?.toLowerCase() || 'index';
    const actionList = function (value) { return (value || '').toLowerCase().split(',').map(function (s) { return s.trim(); }).filter(Boolean); };
    document.querySelectorAll('.app-nav-link[data-nav-controller]').forEach(function (link) {
        // data-nav-actions: chỉ active khi action nằm trong danh sách; data-nav-exclude-actions: không active với các action này
        const onlyActions = actionList(link.dataset.navActions);
        const excludedActions = actionList(link.dataset.navExcludeActions);
        if (link.dataset.navController.toLowerCase() === controller
            && (onlyActions.length === 0 || onlyActions.includes(action))
            && !excludedActions.includes(action)) {
            link.classList.add('active');
            link.setAttribute('aria-current', 'page');
        }
    });
});
