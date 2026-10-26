/*
 * Nút ẩn/hiện mật khẩu cho Views/Acc/ChangePassword.cshtml và Views/Acc/ChangePasswordAccount.cshtml.
 * Không cần config. Markup gọi onclick="togglePassword('<inputId>', this)" nên hàm phải là global.
 */
function togglePassword(id, btn) {
    'use strict';
    const input = document.getElementById(id);
    input.type = input.type === 'password' ? 'text' : 'password';
    const icon = btn && btn.querySelector('i');
    if (icon) icon.className = input.type === 'password' ? 'bi bi-eye' : 'bi bi-eye-slash';
}
