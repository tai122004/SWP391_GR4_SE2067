/*
 * Form tạo / sửa tài khoản: Views/Acc/Create.cshtml và Views/Acc/Edit.cshtml.
 * Cần nạp sau site.js (generateRandomPassword, generateUsernameFromFullName) và personnel-input.js.
 * Config: window.RWPM.accForm = {
 *   isCreate: bool,               // true ở trang Create: gợi ý username, sinh mật khẩu
 *   i18n: {
 *     copiedPassword,             // chỉ Create (markup nút sao chép dùng RWPM.accForm.i18n.copiedPassword)
 *     storeSectionTitle, hrSectionTitle,
 *     storeSectionHint, hrSectionHint  // chỉ Create; bỏ trống thì không đổi dòng gợi ý
 *   }
 * }
 * Hàm gọi từ markup (giữ global, chỉ Create): togglePasswordVisibility, generateAndSetPassword.
 */
(function ($, cfg) {
    'use strict';

    const i18n = cfg.i18n;

    // Tự động ẩn/hiện và cấu hình trường thông tin hồ sơ theo Role
    function updateStoreSectionVisibility() {
        const roleVal = $('#accRoleSelect').val();
        // StoreManager = 4, Employee = 6
        const isStoreRole = (roleVal == '4' || roleVal == '6' || roleVal == 'StoreManager' || roleVal == 'Employee');
        const isHrRole = (roleVal == '2' || roleVal == 'HR');

        if (isStoreRole) {
            $('#storeAssignmentSection').slideDown(200);
            $('#storeSelectCol').show();
            $('#employmentTypeCol').show();

            $('#empCodeCol').removeClass('col-md-6').addClass('col-md-4');
            $('#joinDateCol').removeClass('col-md-6').addClass('col-md-4');
            $('#hourlyRateCol').removeClass('col-md-6 col-md-8').addClass('col-md-4');

            $('#storeSelectInput').prop('disabled', false);
            $('#employeeSectionTitleText').text(i18n.storeSectionTitle);
            if (i18n.storeSectionHint !== undefined) $('#storeInfoHintText').text(i18n.storeSectionHint);
        } else if (isHrRole) {
            $('#storeAssignmentSection').slideDown(200);
            // HR có thể chọn chi nhánh làm việc để chấm công và đăng ký ca
            $('#storeSelectCol').show();
            $('#employmentTypeCol').hide();

            $('#empCodeCol').removeClass('col-md-6').addClass('col-md-4');
            $('#joinDateCol').removeClass('col-md-6').addClass('col-md-4');
            // HR không có hình thức làm việc -> ô lương theo giờ chiếm phần còn lại của hàng
            $('#hourlyRateCol').removeClass('col-md-4 col-md-6').addClass('col-md-8');

            $('#storeSelectInput').prop('disabled', false);
            $('#employeeSectionTitleText').text(i18n.hrSectionTitle);
            if (i18n.hrSectionHint !== undefined) $('#storeInfoHintText').text(i18n.hrSectionHint);
        } else {
            $('#storeAssignmentSection').slideUp(200);
            $('#storeSelectInput').prop('disabled', true);
        }
    }

    if (cfg.isCreate) {
        let usernameManuallyChanged = false;

        $('#accUsername').on('input', function () {
            usernameManuallyChanged = $(this).val().trim() !== '';
        });

        $('#accFullName').on('input', function () {
            if (!usernameManuallyChanged) {
                const suggested = generateUsernameFromFullName($(this).val());
                $('#accUsername').val(suggested);
            }
        });
    }

    function togglePasswordVisibility(inputId, iconId) {
        const input = document.getElementById(inputId);
        const icon = document.getElementById(iconId);
        if (input.type === 'password') {
            input.type = 'text';
            icon.className = 'bi bi-eye-slash';
        } else {
            input.type = 'password';
            icon.className = 'bi bi-eye';
        }
    }

    function showPasswordForCopy(pass) {
        const input = document.getElementById('accPassword');
        input.type = 'text';
        document.getElementById('toggleIcon').className = 'bi bi-eye-slash';
        $('#generatedPassText').text(pass);
        $('#passwordCopyAlert').removeClass('d-none').addClass('d-flex');
    }

    function generateAndSetPassword() {
        const pass = generateRandomPassword(8);
        $('#accPassword').val(pass);
        showPasswordForCopy(pass);
    }

    $('#accRoleSelect').on('change', updateStoreSectionVisibility);

    $(document).ready(function () {
        if (cfg.isCreate) {
            const currentPass = $('#accPassword').val();
            if (!currentPass) {
                generateAndSetPassword();
            } else {
                // Form vừa báo lỗi (VD: trùng email/SĐT): giữ nguyên mật khẩu đã tạo, không sinh mật khẩu mới
                showPasswordForCopy(currentPass);
            }
            // Người dùng tự sửa mật khẩu -> cập nhật dòng "Mật khẩu: ..." để sao chép đúng
            $('#accPassword').on('input', function () { $('#generatedPassText').text(this.value); });
        }
        updateStoreSectionVisibility();
    });

    if (cfg.isCreate) {
        window.togglePasswordVisibility = togglePasswordVisibility;
        window.generateAndSetPassword = generateAndSetPassword;
    }
})(jQuery, window.RWPM.accForm);
