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
            if (typeof toastr !== 'undefined') {
                toastr.success(successMessage || 'Đã sao chép vào bộ nhớ tạm!');
            } else {
                alert(successMessage || 'Đã sao chép!');
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
            if (typeof toastr !== 'undefined') {
                toastr.success(successMessage || 'Đã sao chép vào bộ nhớ tạm!');
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
    const labelAccount = modalEl.data('label-account') || 'Account';
    const labelPassword = modalEl.data('label-password') || 'Password';
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

    const msgMinLen = modal.data('err-minlen') || 'New password must be at least 6 characters!';
    const msgMismatch = modal.data('err-match') || 'Password confirmation does not match!';
    const textSaving = modal.data('text-saving') || 'Saving...';
    const msgSuccess = modal.data('toast-success') || `Reset password for account ${username} successfully!`;

    if (!newPassword || newPassword.length < 6) {
        if (typeof toastr !== 'undefined') {
            toastr.warning(msgMinLen);
        } else {
            alert(msgMinLen);
        }
        return;
    }

    if (newPassword !== confirmPassword) {
        if (typeof toastr !== 'undefined') {
            toastr.warning(msgMismatch);
        } else {
            alert(msgMismatch);
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
            if (typeof toastr !== 'undefined') {
                toastr.success(msgSuccess);
            }
        },
        error: function (xhr) {
            const msg = xhr.responseText || 'Error resetting password!';
            if (typeof toastr !== 'undefined') {
                toastr.error(msg);
            } else {
                alert(msg);
            }
        },
        complete: function () {
            btnSubmit.prop('disabled', false).html(originalBtnHtml);
        }
    });
}

