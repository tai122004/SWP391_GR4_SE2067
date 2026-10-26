/*
 * Trang danh sách nhân viên (Views/Employee/Index.cshtml).
 * Config: window.RWPM.employeeIndex = {
 *   urls: { updateActiveStatus, deleteConfirmed, previewImport, confirmImport },
 *   i18n: { active, inactive, toastActive, toastInactive, failed, deleted, deleteBtn, processing,
 *           selectFile, verifying, preview, saving, confirm, valid, invalid,
 *           employeeNumber ("... {0}"), serverConnectionError, saveFailed, saveError }
 * }
 * Hàm gọi từ markup (giữ global): openResignEmployeeModal, openDeleteEmployeeModal, confirmDeleteEmployee,
 * previewEmpImport, backToEmpStep1, confirmEmpImport.
 */
(function ($, cfg) {
    'use strict';

    const i18n = cfg.i18n;

    $(document).ready(function () {
        $('.active-toggle').change(function () {
            var checkbox = $(this);
            var employeeId = checkbox.data('id');
            var employeeName = checkbox.data('name');
            var isActive = checkbox.is(':checked');

            $.ajax({
                url: cfg.urls.updateActiveStatus + '/' + employeeId,
                type: 'POST',
                contentType: 'application/json',
                headers: {
                    'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
                },
                data: JSON.stringify({
                    employeeId: employeeId,
                    isActive: isActive
                }),
                success: function () {
                    var choice = checkbox.closest('.status-choice');
                    choice.find('.status-dot').toggleClass('is-active', isActive).toggleClass('is-inactive', !isActive);
                    choice.find('.status-choice-text').text(isActive ? i18n.active : i18n.inactive);
                    AppToast.success((isActive ? i18n.toastActive : i18n.toastInactive) + ': ' + employeeName);
                },
                error: function (xhr) {
                    checkbox.prop('checked', !isActive);
                    AppToast.error((xhr.responseText || i18n.failed));
                }
            });
        });
    });

    // Mở modal thôi việc
    function openResignEmployeeModal(employeeId, fullName, employeeCode) {
        $('#resignEmp_Id').val(employeeId);
        $('#resignEmp_Name').text(fullName || i18n.employeeNumber.replace('{0}', employeeId));
        $('#resignEmp_Code').text(employeeCode || '');
        const modalEl = document.getElementById('modalResignEmployee');
        if (modalEl) {
            new bootstrap.Modal(modalEl).show();
        }
    }

    // Mở modal xác nhận xóa nhân viên
    function openDeleteEmployeeModal(employeeId, fullName, employeeCode) {
        $('#deleteEmp_Id').val(employeeId);
        $('#deleteEmp_Name').text(fullName || i18n.employeeNumber.replace('{0}', employeeId));
        $('#deleteEmp_Code').text(employeeCode || '');
        const modalEl = document.getElementById('modalDeleteEmployee');
        if (modalEl) {
            new bootstrap.Modal(modalEl).show();
        }
    }

    // Submit xóa nhân viên qua AJAX
    function confirmDeleteEmployee() {
        const employeeId = $('#deleteEmp_Id').val();
        const token = $('input[name="__RequestVerificationToken"]').val();
        const btn = $('#deleteEmp_BtnConfirm');
        btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span> ' + escapeHtml(i18n.processing));

        $.ajax({
            url: cfg.urls.deleteConfirmed,
            type: 'POST',
            data: { employeeId: employeeId, __RequestVerificationToken: token },
            success: function (response) {
                if (!response || response.success !== true) {
                    AppToast.error(i18n.failed);
                    return;
                }
                $('#row-emp-' + employeeId).fadeOut(300, function () { $(this).remove(); });
                const modalEl = document.getElementById('modalDeleteEmployee');
                if (modalEl) { bootstrap.Modal.getInstance(modalEl)?.hide(); }
                AppToast.success(i18n.deleted);
            },
            error: function (xhr) {
                AppToast.error(xhr.responseJSON?.message || i18n.failed);
            },
            complete: function () {
                btn.prop('disabled', false).html('<i class="bi bi-trash me-1"></i> ' + escapeHtml(i18n.deleteBtn));
            }
        });
    }

    let currentEmpImportToken = '';

    function escapeHtml(text) {
        if (!text) return '';
        return text.toString()
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function previewEmpImport() {
        const fileInput = document.getElementById('empImportFileInput');
        const errBox = $('#empImportStep1Error');
        errBox.addClass('d-none').text('');

        if (!fileInput.files || fileInput.files.length === 0) {
            errBox.removeClass('d-none').text(i18n.selectFile);
            return;
        }

        const file = fileInput.files[0];
        if (!file.name.toLowerCase().endsWith('.xlsx')) {
            errBox.removeClass('d-none').text(i18n.selectFile);
            return;
        }

        const btn = $('#empBtnPreview');
        btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span> ' + escapeHtml(i18n.verifying));

        const formData = new FormData();
        formData.append('file', file);

        const token = $('input[name="__RequestVerificationToken"]').first().val();

        fetch(cfg.urls.previewImport, {
            method: 'POST',
            headers: {
                'RequestVerificationToken': token
            },
            body: formData
        })
        .then(r => r.json())
        .then(res => {
            if (!res.success) {
                errBox.removeClass('d-none').text(res.message || i18n.failed);
                return;
            }

            const data = res.data;
            currentEmpImportToken = data.importToken;

            $('#empPreviewTotal').text(data.totalRows);
            $('#empPreviewValid').text(data.validCount);
            $('#empPreviewInvalid').text(data.invalidCount);

            $('#empPreviewWarning').addClass('d-none');
            $('#empPreviewSuccessNotice').addClass('d-none');
            $('#empPreviewDangerNotice').addClass('d-none');

            if (data.validCount === 0) {
                $('#empPreviewDangerNotice').removeClass('d-none');
                $('#empBtnConfirm').prop('disabled', true);
            } else if (data.invalidCount > 0) {
                $('#empPreviewWarning').removeClass('d-none');
                $('#empBtnConfirm').prop('disabled', false);
            } else {
                $('#empPreviewSuccessNotice').removeClass('d-none');
                $('#empBtnConfirm').prop('disabled', false);
            }

            const tbody = $('#empPreviewTableBody');
            tbody.empty();

            (data.rows || []).forEach(row => {
                const tr = $('<tr>');
                if (!row.isValid) {
                    tr.addClass('table-danger');
                }

                const statusBadge = row.isValid
                    ? `<span class="app-badge app-badge-success">${escapeHtml(i18n.valid)}</span>`
                    : `<span class="app-badge app-badge-danger">${escapeHtml(i18n.invalid)}</span>`;

                const errHtml = row.isValid
                    ? `<span class="text-muted small">—</span>`
                    : `<span class="text-danger small">${escapeHtml(row.errorMessage || '')}</span>`;

                tr.html(`
                    <td class="text-end text-muted">${row.rowNum}</td>
                    <td class="text-center">${statusBadge}</td>
                    <td class="fw-medium">${escapeHtml(row.username)}</td>
                    <td>${escapeHtml(row.fullName)}</td>
                    <td class="small text-muted">${escapeHtml(row.email)}</td>
                    <td class="small text-muted">${escapeHtml(row.phone || '')}</td>
                    <td>${escapeHtml(row.roleDisplay)}</td>
                    <td>${escapeHtml(row.storeDisplay)}</td>
                    <td class="small text-muted">${escapeHtml(row.empCode)}</td>
                    <td>${errHtml}</td>
                `);

                tbody.append(tr);
            });

            $('#empImportStep1').addClass('d-none');
            $('#empImportStep2').removeClass('d-none');
            $('#empStep1Footer').addClass('d-none');
            $('#empStep2Footer').removeClass('d-none');
        })
        .catch(err => {
            errBox.removeClass('d-none').text(i18n.serverConnectionError + err.message);
        })
        .finally(() => {
            btn.prop('disabled', false).html('<i class="bi bi-search me-1"></i> ' + escapeHtml(i18n.preview));
        });
    }

    function backToEmpStep1() {
        $('#empImportStep2').addClass('d-none');
        $('#empImportStep1').removeClass('d-none');
        $('#empStep2Footer').addClass('d-none');
        $('#empStep1Footer').removeClass('d-none');
    }

    function confirmEmpImport() {
        if (!currentEmpImportToken) return;

        const btn = $('#empBtnConfirm');
        btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span> ' + escapeHtml(i18n.saving));

        const token = $('input[name="__RequestVerificationToken"]').first().val();

        fetch(cfg.urls.confirmImport, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify({ importToken: currentEmpImportToken })
        })
        .then(r => r.json())
        .then(res => {
            if (res.success) {
                AppToast.success(res.message);
                const modalEl = document.getElementById('importExcelModal');
                if (modalEl) {
                    bootstrap.Modal.getInstance(modalEl)?.hide();
                }
                setTimeout(() => {
                    window.location.reload();
                }, 800);
            } else {
                AppToast.error(res.message || i18n.saveFailed);
                btn.prop('disabled', false).html('<i class="bi bi-check2-circle me-1"></i> ' + escapeHtml(i18n.confirm));
            }
        })
        .catch(err => {
            AppToast.error(i18n.saveError + err.message);
            btn.prop('disabled', false).html('<i class="bi bi-check2-circle me-1"></i> ' + escapeHtml(i18n.confirm));
        });
    }

    $('#importExcelModal').on('hidden.bs.modal', function () {
        backToEmpStep1();
        $('#empImportFileInput').val('');
        $('#empImportStep1Error').addClass('d-none').text('');
    });

    window.openResignEmployeeModal = openResignEmployeeModal;
    window.openDeleteEmployeeModal = openDeleteEmployeeModal;
    window.confirmDeleteEmployee = confirmDeleteEmployee;
    window.previewEmpImport = previewEmpImport;
    window.backToEmpStep1 = backToEmpStep1;
    window.confirmEmpImport = confirmEmpImport;
})(jQuery, window.RWPM.employeeIndex);
