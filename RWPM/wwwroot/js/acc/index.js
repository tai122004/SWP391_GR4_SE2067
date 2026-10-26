/*
 * Trang danh sách tài khoản (Views/Acc/Index.cshtml).
 * Config: window.RWPM.accIndex = {
 *   urls: { previewImport, confirmImport, updateActiveStatus },
 *   i18n: { copied, selectFile, verifying, preview, saving, confirm, valid, invalid, failed, active, inactive }
 * }
 * Hàm gọi từ markup (giữ global): previewAccImport, backToAccStep1, confirmAccImport, onToggleAccountStatus.
 * Markup dùng trực tiếp RWPM.accIndex.i18n.copied cho nút sao chép.
 */
(function ($, cfg) {
    'use strict';

    const i18n = cfg.i18n;
    let currentAccImportToken = '';

    function escapeHtml(text) {
        if (!text) return '';
        return text.toString()
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function previewAccImport() {
        const fileInput = document.getElementById('accImportFileInput');
        const errBox = $('#accImportStep1Error');
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

        const btn = $('#accBtnPreview');
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
            currentAccImportToken = data.importToken;

            $('#accPreviewTotal').text(data.totalRows);
            $('#accPreviewValid').text(data.validCount);
            $('#accPreviewInvalid').text(data.invalidCount);

            $('#accPreviewWarning').addClass('d-none');
            $('#accPreviewSuccessNotice').addClass('d-none');
            $('#accPreviewDangerNotice').addClass('d-none');

            if (data.validCount === 0) {
                $('#accPreviewDangerNotice').removeClass('d-none');
                $('#accBtnConfirm').prop('disabled', true);
            } else if (data.invalidCount > 0) {
                $('#accPreviewWarning').removeClass('d-none');
                $('#accBtnConfirm').prop('disabled', false);
            } else {
                $('#accPreviewSuccessNotice').removeClass('d-none');
                $('#accBtnConfirm').prop('disabled', false);
            }

            const tbody = $('#accPreviewTableBody');
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

            $('#accImportStep1').addClass('d-none');
            $('#accImportStep2').removeClass('d-none');
            $('#accStep1Footer').addClass('d-none');
            $('#accStep2Footer').removeClass('d-none');
        })
        .catch(err => {
            errBox.removeClass('d-none').text(i18n.failed + ': ' + err.message);
        })
        .finally(() => {
            btn.prop('disabled', false).html('<i class="bi bi-search me-1"></i> ' + escapeHtml(i18n.preview));
        });
    }

    function backToAccStep1() {
        $('#accImportStep2').addClass('d-none');
        $('#accImportStep1').removeClass('d-none');
        $('#accStep2Footer').addClass('d-none');
        $('#accStep1Footer').removeClass('d-none');
    }

    function confirmAccImport() {
        if (!currentAccImportToken) return;

        const btn = $('#accBtnConfirm');
        btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span> ' + escapeHtml(i18n.saving));

        const token = $('input[name="__RequestVerificationToken"]').first().val();

        fetch(cfg.urls.confirmImport, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify({ importToken: currentAccImportToken })
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
                AppToast.error(res.message || i18n.failed);
                btn.prop('disabled', false).html('<i class="bi bi-check2-circle me-1"></i> ' + escapeHtml(i18n.confirm));
            }
        })
        .catch(err => {
            AppToast.error(i18n.failed + ': ' + err.message);
            btn.prop('disabled', false).html('<i class="bi bi-check2-circle me-1"></i> ' + escapeHtml(i18n.confirm));
        });
    }

    $('#importExcelModal').on('hidden.bs.modal', function () {
        backToAccStep1();
        $('#accImportFileInput').val('');
        $('#accImportStep1Error').addClass('d-none').text('');
    });

    function onToggleAccountStatus(username, isActive) {
        const token = $('input[name="__RequestVerificationToken"]').val();
        const checkbox = document.getElementById("isActiveSwitch_" + username);

        fetch(cfg.urls.updateActiveStatus + '/' + username, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify({
                username: username,
                isActive: isActive
            })
        })
        .then(response => {
            if (!response.ok) {
                return response.text().then(text => { throw new Error(text); });
            }
            AppToast.success((isActive ? i18n.active : i18n.inactive) + ': ' + username);
            if (checkbox) {
                const choice = checkbox.closest('.status-choice');
                choice?.querySelector('.status-dot')?.classList.toggle('is-active', isActive);
                choice?.querySelector('.status-dot')?.classList.toggle('is-inactive', !isActive);
                const text = choice?.querySelector('.status-choice-text');
                if (text) text.textContent = isActive ? i18n.active : i18n.inactive;
            }
        })
        .catch(error => {
            AppToast.error(error.message || i18n.failed);
            if (checkbox) checkbox.checked = !isActive;
        });
    }

    window.previewAccImport = previewAccImport;
    window.backToAccStep1 = backToAccStep1;
    window.confirmAccImport = confirmAccImport;
    window.onToggleAccountStatus = onToggleAccountStatus;
})(jQuery, window.RWPM.accIndex);
