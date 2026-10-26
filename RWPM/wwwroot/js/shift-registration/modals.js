// Views/ShiftRegistration/Index.cshtml — modal đăng ký ca theo ngày (#shiftModal) và modal chi tiết / hủy ca (#viewModal).
// Thứ tự nạp: core.js, grid.js, coverage.js, week-picker.js, modals.js, index.js (cùng dùng window.RWPM.shiftRegistration).
// Cấu hình: window.RWPM.shiftRegistrationIndex = { isManager, coverageStoreId, shifts, employees, urls: { availableShifts }, i18n: {...} }
(function ($, cfg) {
    'use strict';
    var app = window.RWPM.shiftRegistration;
    var S = app.state, T = app.T, L = cfg.i18n;
    var escapeHtml = app.escapeHtml, getISODateString = app.getISODateString, ajaxErrorText = app.ajaxErrorText, showAppToast = app.showAppToast, confirmScheduleWarnings = app.confirmScheduleWarnings;

    function initModals() {
        // Single day shift registration form submission
        $('#shiftForm').submit(function(e) {
            e.preventDefault();
            var selectedShifts = [];
            $('.shift-checkbox:checked').each(function() {
                selectedShifts.push($(this).val());
            });

            if (selectedShifts.length === 0) {
                showAppToast('warning', L.shiftErrSelectOne);
                return;
            }

            var empId = $('#EmployeeId').val();
            var workDate = $('#WorkDate').val();
            var token = $('input[name="__RequestVerificationToken"]').val();

            var data = {
                EmployeeId: empId,
                ShiftId: selectedShifts[0],
                ShiftIds: selectedShifts.map(id => parseInt(id)),
                WorkDate: workDate,
                Note: ""
            };

            sendCreate(data, token, false);
        });

        // Quản lý tạo ca cho nhân viên: máy chủ trả needConfirm + warnings (ngoài thời gian có thể làm việc) -> hỏi xác nhận rồi gửi lại
        function sendCreate(data, token, confirmWarnings) {
            $.ajax({
                url: '/ShiftRegistration/Create' + (confirmWarnings ? '?confirmWarnings=true' : ''),
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(data),
                headers: { 'RequestVerificationToken': token },
                success: async function(res) {
                    if (res && res.needConfirm) {
                        if (await confirmScheduleWarnings(res.warnings)) sendCreate(data, token, true);
                        return;
                    }
                    if (res.success) {
                        $('#shiftModal').modal('hide');
                        showAppToast('success', L.toastRegisterSuccess);
                        fetchEvents();
                        $('#shiftForm')[0].reset();
                    } else {
                        showAppToast('error', res.message || T.errOccurred);
                    }
                },
                error: function(err) {
                    showAppToast('error', T.errPrefix + ajaxErrorText(err));
                }
            });
        }

        // Toggle single day save button
        $(document).on('change', '.shift-checkbox', function() {
            if ($('.shift-checkbox:checked').length > 0) {
                $('#saveShiftBtnContainer').fadeIn(150);
            } else {
                $('#saveShiftBtnContainer').fadeOut(150);
            }
        });
    }

    function filterShifts(text) {
        text = text.toLowerCase();
        $('.shift-option').each(function() {
            var shiftName = $(this).data('shift-name').toLowerCase();
            if (shiftName.indexOf(text) > -1) {
                $(this).show();
            } else {
                $(this).hide();
            }
        });
    }

    function openCreateModal(dateObj, empId) {
        var today = new Date();
        today.setHours(0, 0, 0, 0);

        if (dateObj < today) {
            showAppToast('warning', L.pastDateError);
            return;
        }

        $('#WorkDate').val(getISODateString(dateObj));

        let displayDate = dateObj.getDate().toString().padStart(2, '0') + '/' + 
                          (dateObj.getMonth() + 1).toString().padStart(2, '0') + '/' + 
                          dateObj.getFullYear();
        $('#modalDateDisplay').text(L.modalDateFormat.replace('{0}', displayDate));

        if (empId) {
            $('#EmployeeId').val(empId);
            let emp = S.employeesList.find(e => e.id == empId);
            if (emp) {
                $('#modalEmpName').text(emp.name);
            }
        } else {
            if (S.employeesList.length > 0) {
                let firstEmp = S.employeesList[0];
                if (firstEmp.id === 'me') {
                    $('#EmployeeId').val(0);
                    $('#modalEmpName').text(L.shiftMe);
                } else {
                    $('#EmployeeId').val(firstEmp.id);
                    $('#modalEmpName').text(firstEmp.name);
                }
            }
        }

        $('.shift-checkbox').prop('checked', false);
        $('#saveShiftBtnContainer').hide();
        $('#searchShiftInput').val('');
        $('.shift-list-container').html('<div class="text-center py-3 text-muted small"><span class="spinner-border spinner-border-sm me-1"></span> ' + escapeHtml(L.modalLoadingShifts) + '</div>');

        $.get(cfg.urls.availableShifts, { employeeId: $('#EmployeeId').val(), workDate: $('#WorkDate').val() })
            .done(function(shifts) {
                const container = $('.shift-list-container').empty();
                if (!shifts.length) {
                    container.html($('<div class="text-center py-3 text-muted small">').text(L.modalNoShiftsForDay));
                    return;
                }
                shifts.forEach(function(shift) {
                    const row = $('<div>').addClass('shift-item d-flex justify-content-between align-items-center py-2 px-2 border-bottom shift-option rounded-2').attr('data-shift-name', shift.name);
                    const label = $('<label>').attr('for', 'shift_' + shift.id).addClass('form-check-label small fw-medium text-dark cursor-pointer mb-0').text(shift.name + ' (' + shift.time + ')');
                    const input = $('<input>').attr({ type: 'checkbox', id: 'shift_' + shift.id, name: 'ShiftId', value: shift.id }).addClass('form-check-input shift-checkbox m-0');
                    // blocked: tự đăng ký, ca không phù hợp thời gian có thể làm việc -> khóa; warning: quản lý xếp cho người khác -> chỉ cảnh báo
                    const left = $('<div>').addClass('me-2').append(label);
                    const note = shift.blocked || shift.warning;
                    if (note) {
                        left.append($('<div>').addClass('small mt-1 ' + (shift.blocked ? 'text-danger' : 'text-warning-emphasis'))
                            .append($('<i>').addClass('bi me-1 ' + (shift.blocked ? 'bi-slash-circle' : 'bi-exclamation-triangle')))
                            .append(document.createTextNode(note)));
                    }
                    if (shift.blocked) { input.prop('disabled', true); label.removeClass('cursor-pointer').addClass('text-muted'); }
                    row.append(left, input);
                    container.append(row);
                });
            })
            .fail(function() {
                $('.shift-list-container').html($('<div class="text-center py-3 text-danger small">').text(L.modalLoadShiftsError));
            });

        var modal = new bootstrap.Modal(document.getElementById('shiftModal'));
        modal.show();
    }

    function openViewModal(eventObj) {
        $('#vEmployee').text(eventObj.employeeName);
        $('#vStore').text(eventObj.storeName);
        $('#vShift').text(eventObj.title);
        let timeText = (eventObj.startTime && eventObj.endTime) ? `${eventObj.startTime} – ${eventObj.endTime}` : (eventObj.start.replace('T', ' ') + ' – ' + eventObj.end.replace('T', ' '));
        $('#vTime').text(timeText);

        let statusHtml = '';
        if (eventObj.statusId === 1 || eventObj.statusName === 'Approved') {
            statusHtml = `<span class="badge-status status-approved"><i class="bi bi-check-circle-fill"></i> ${T.approved}</span>`;
        } else if (eventObj.statusId === 2 || eventObj.statusName === 'Rejected') {
            statusHtml = `<span class="badge-status status-rejected"><i class="bi bi-x-circle-fill"></i> ${T.rejectedLong}</span>`;
        } else {
            statusHtml = `<span class="badge-status status-pending"><i class="bi bi-hourglass-split"></i> ${T.pending}</span>`;
        }
        $('#vStatusBadge').html(statusHtml);
        $('#vStatus').text(eventObj.statusName || '');
        $('#vNote').text(eventObj.note || '-');
        $('#vRegistrationId').val(eventObj.id);

        if (eventObj.rejectionReason) {
            $('#vRejectionReason').text(eventObj.rejectionReason);
            $('#vRejectionReasonRow').show();
        } else {
            $('#vRejectionReasonRow').hide();
        }

        if (eventObj.approvedBy) {
            $('#vApprover').text(eventObj.approvedBy);
            $('#vApprovedAt').text(eventObj.approvedAt ? `(${eventObj.approvedAt})` : '');
            $('#vApproverRow').show();
        } else {
            $('#vApproverRow').hide();
        }

        if (!cfg.isManager || S.activeMode === 'my') {
            if (eventObj.statusId === 1 || eventObj.statusName === 'Approved') {
                $('#btnCancelRegistration').hide();
                $('#txtApprovedNotice').show();
                $('#btnGoToSwapShift').show();
            } else {
                $('#btnCancelRegistration').show();
                $('#txtApprovedNotice').hide();
                $('#btnGoToSwapShift').hide();
            }
        }

        var modal = new bootstrap.Modal(document.getElementById('viewModal'));
        modal.show();
    }

    async function deleteRegistration() {
        if (!(await AppConfirm({ message: L.confirmCancel, type: 'danger', icon: 'bi-calendar-x' }))) return;
        var id = $('#vRegistrationId').val();
        $.ajax({
            url: '/ShiftRegistration/Delete',
            type: 'POST',
            data: { id: id },
            headers: { 'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val() },
            success: function(res) {
                if (res.success) {
                    $('#viewModal').modal('hide');
                    showAppToast('success', L.toastCancelSuccess);
                    fetchEvents();
                } else {
                    showAppToast('error', res.message || L.toastCancelFailed);
                }
            },
            error: function(err) {
                showAppToast('error', T.errPrefix + ajaxErrorText(err, L.toastSystemError));
            }
        });
    }

    app.initModals = initModals;
    window.filterShifts = filterShifts;
    window.openCreateModal = openCreateModal;
    window.openViewModal = openViewModal;
    window.deleteRegistration = deleteRegistration;
})(jQuery, window.RWPM.shiftRegistrationIndex);
