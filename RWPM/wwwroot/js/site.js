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
    var TOAST_STYLES = {
        success: { bg: 'bg-success text-white', icon: 'bi-check-circle-fill', close: 'btn-close-white', sub: 'text-white-50' },
        error:   { bg: 'bg-danger text-white',  icon: 'bi-exclamation-triangle-fill', close: 'btn-close-white', sub: 'text-white-50' },
        warning: { bg: 'bg-warning text-dark',  icon: 'bi-exclamation-circle-fill', close: '', sub: 'text-dark text-opacity-75' },
        info:    { bg: 'bg-primary text-white', icon: 'bi-info-circle-fill', close: 'btn-close-white', sub: 'text-white-50' }
    };

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
        var style = TOAST_STYLES[type] || TOAST_STYLES.info;
        var opts = options || {};

        var el = document.createElement('div');
        el.className = 'alert app-toast ' + style.bg + ' rounded shadow-sm p-3 mb-2 position-relative fade show';
        el.setAttribute('role', type === 'error' ? 'alert' : 'status');
        el.style.minWidth = '300px';
        el.style.maxWidth = '420px';

        var row = document.createElement('div');
        row.className = 'd-flex justify-content-between';

        var left = document.createElement('div');
        left.className = 'd-flex align-items-center';
        var icon = document.createElement('i');
        icon.className = 'bi ' + style.icon + ' me-2';
        var text = document.createElement('span');
        text.style.whiteSpace = 'pre-line';
        text.textContent = String(message);
        left.appendChild(icon);
        left.appendChild(text);

        var right = document.createElement('div');
        right.className = 'd-flex flex-column align-items-end ms-3';
        var btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'btn-close ' + style.close + ' mt-1';
        btn.setAttribute('aria-label', t('close', 'Close'));
        right.appendChild(btn);

        row.appendChild(left);
        row.appendChild(right);

        var time = document.createElement('div');
        time.innerHTML = '<small class="' + style.sub + '"></small>';
        time.firstChild.textContent = opts.time || nowText();

        el.appendChild(row);
        el.appendChild(time);
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
    var CONFIRM_TYPES = {
        danger:  { icon: 'bi-trash3', color: 'danger',  btn: 'btn-danger' },
        warning: { icon: 'bi-exclamation-triangle', color: 'warning', btn: 'btn-warning' },
        primary: { icon: 'bi-question-circle', color: 'primary', btn: 'btn-primary' },
        success: { icon: 'bi-check2-circle', color: 'success', btn: 'btn-success' }
    };

    var confirmModalEl = null;
    function ensureConfirmModal() {
        if (confirmModalEl) return confirmModalEl;
        confirmModalEl = document.createElement('div');
        confirmModalEl.className = 'modal fade';
        confirmModalEl.id = 'appConfirmModal';
        confirmModalEl.tabIndex = -1;
        confirmModalEl.setAttribute('aria-hidden', 'true');
        confirmModalEl.style.zIndex = '1070';
        confirmModalEl.innerHTML =
            '<div class="modal-dialog modal-dialog-centered" style="max-width:440px">' +
            '  <div class="modal-content border-0 shadow">' +
            '    <div class="modal-body text-center px-4 pt-4 pb-3">' +
            '      <div class="app-confirm-icon rounded-circle d-inline-flex align-items-center justify-content-center mb-3" style="width:64px;height:64px">' +
            '        <i class="bi fs-2"></i>' +
            '      </div>' +
            '      <h5 class="fw-bold mb-2 app-confirm-title"></h5>' +
            '      <p class="text-muted mb-0 app-confirm-message" style="white-space:pre-line"></p>' +
            '    </div>' +
            '    <div class="modal-footer border-0 justify-content-center gap-2 pb-4 pt-0">' +
            '      <button type="button" class="btn btn-light border px-4 app-confirm-cancel" data-bs-dismiss="modal"></button>' +
            '      <button type="button" class="btn px-4 app-confirm-ok"></button>' +
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
        var iconWrap = el.querySelector('.app-confirm-icon');
        iconWrap.className = 'app-confirm-icon rounded-circle d-inline-flex align-items-center justify-content-center mb-3 bg-' + cfg.color + ' bg-opacity-10 text-' + cfg.color;
        iconWrap.querySelector('i').className = 'bi ' + (options.icon || cfg.icon) + ' fs-2';
        el.querySelector('.app-confirm-title').textContent = options.title || t('confirmTitle', 'Please confirm');
        el.querySelector('.app-confirm-message').textContent = options.message || '';
        var okBtn = el.querySelector('.app-confirm-ok');
        okBtn.className = 'btn px-4 app-confirm-ok ' + cfg.btn;
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

    function prepareForms() {
        // (1) Tạo chỗ hiển thị lỗi cho mọi ô có data-val nhưng chưa có <span asp-validation-for>
        document.querySelectorAll('form [data-val="true"][name]').forEach(function (field) {
            if (field.type === 'hidden') return;
            messageHolder(field, true);
        });

        // Dịch thông báo mặc định của jQuery Validation
        if ($ && $.validator) {
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
                equalTo: t('equalTo'),
                maxlength: fmt(t('maxlength')),
                minlength: fmt(t('minlength')),
                max: fmt(t('max')),
                min: fmt(t('min')),
                step: fmt(t('step'))
            });
            // Số kiểu Việt Nam (25.000 / 25000,5) vẫn được chấp nhận ở phía client
            $.validator.methods.number = function (value, element) {
                return this.optional(element) || /^-?\d+([.,]\d+)?$/.test(String(value).replace(/\s/g, ''));
            };
        }

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

// 3. Chuyển đổi tên tiếng Việt thành username gợi ý (VD: "Nguyễn Văn An" -> "an.nv")
function generateUsernameFromFullName(fullName) {
    if (!fullName) return "";
    let str = fullName.toLowerCase().trim();
    str = str.normalize('NFD').replace(/[\u0300-\u036f]/g, '');
    str = str.replace(/[đĐ]/g, 'd');
    str = str.replace(/[^a-z0-9\s]/g, '');
    const parts = str.split(/\s+/).filter(Boolean);
    if (parts.length === 0) return "";
    if (parts.length === 1) return parts[0];
    
    const lastName = parts[parts.length - 1];
    const initials = parts.slice(0, parts.length - 1).map(p => p[0]).join('');
    return `${lastName}.${initials}`;
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

    if (!newPassword || newPassword.length < 6) {
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
    const controller = window.location.pathname.split('/').filter(Boolean)[0]?.toLowerCase() || 'home';
    document.querySelectorAll('.app-nav-link[data-nav-controller]').forEach(function (link) {
        if (link.dataset.navController.toLowerCase() === controller) {
            link.classList.add('active');
            link.setAttribute('aria-current', 'page');
        }
    });
});
