/*
 * Màn hình "Yêu cầu & phê duyệt" — Views/Request/Index.cshtml (nạp sau index.js).
 * Các modal tạo đơn mới: đăng ký ca, đổi ca, xin nghỉ phép, điều chỉnh chấm công.
 *
 * Config: window.RWPM.requestIndex (dùng chung với index.js)
 *   urls { createShiftReg, createSwap, createLeave, createAttendance, swapFormData, coworkerShifts,
 *          leaveFormData, attendanceFormData }
 *   i18n { reqCreateSuccess, operationFailed, selectShift, swap*, leave*, adj*, sysError, operationUnsuccessful }
 *
 * Hàm global (gọi từ markup): openShiftModal, openSwapModal, openLeaveModal, openAttendanceModal.
 */
(function ($, cfg) {
    'use strict';

    var i18n = cfg.i18n;
    var urls = cfg.urls;

    let currentAnnualBalance = 0;

    function antiForgeryToken() {
        return $('#antiForgeryForm input[name="__RequestVerificationToken"]').val();
    }

    $(document).ready(function() {
        // Form Submit for new Shift Request
        $('#shiftRequestForm').submit(function(e) {
            e.preventDefault();
            var empId = $('#reqEmployeeId').val();
            if (!empId) empId = "0";

            var data = {
                EmployeeId: empId,
                ShiftId: $('#reqShiftId').val(),
                WorkDate: $('#reqWorkDate').val(),
                Note: $('#reqNote').val()
            };

            $.ajax({
                url: urls.createShiftReg,
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(data),
                headers: {
                    'RequestVerificationToken': antiForgeryToken()
                },
                success: function(res) {
                    if (res.success) {
                        $('#createShiftRequestModal').modal('hide');
                        AppToast.success(i18n.reqCreateSuccess);
                        setTimeout(() => window.location.reload(), 1000);
                    }
                },
                error: function(err) {
                    AppToast.error(err.responseJSON?.message || i18n.operationFailed);
                }
            });
        });

        // Form Submit for Shift Swap Request
        $('#swapShiftForm').submit(function(e) {
            e.preventDefault();
            var swapType = $('input[name="swapType"]:checked').val();
            var sourceId = $('#swapSourceRegistrationId').val();
            if (!sourceId) {
                AppToast.warning(i18n.swapSelectSourcePrompt);
                return;
            }

            var data = {
                SourceRegistrationId: parseInt(sourceId),
                SwapType: swapType,
                Reason: $('#swapReason').val().trim()
            };

            if (swapType === 'peer') {
                var targetEmp = $('#swapTargetEmployeeId').val();
                var targetReg = $('#swapTargetRegistrationId').val();
                if (!targetEmp || !targetReg) {
                    AppToast.warning(i18n.swapSelectCoworkerShiftPrompt);
                    return;
                }
                data.TargetEmployeeId = parseInt(targetEmp);
                data.TargetRegistrationId = parseInt(targetReg);
            } else {
                var targetDate = $('#swapTargetWorkDate').val();
                var targetShift = $('#swapTargetShiftId').val();
                if (!targetDate || !targetShift) {
                    AppToast.warning(i18n.selectShift);
                    return;
                }
                data.TargetWorkDate = targetDate;
                data.TargetShiftId = parseInt(targetShift);
            }

            $('#btnSubmitSwap').prop('disabled', true);

            $.ajax({
                url: urls.createSwap,
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(data),
                headers: {
                    'RequestVerificationToken': antiForgeryToken()
                },
                success: function(res) {
                    if (res.success) {
                        $('#createSwapShiftModal').modal('hide');
                        AppToast.success(res.message || i18n.swapSubmitSuccess);
                        setTimeout(() => window.location.reload(), 1000);
                    } else {
                        AppToast.error(res.message || i18n.operationFailed);
                        $('#btnSubmitSwap').prop('disabled', false);
                    }
                },
                error: function(err) {
                    AppToast.error(err.responseJSON?.message || i18n.operationFailed);
                    $('#btnSubmitSwap').prop('disabled', false);
                }
            });
        });

        // Radio button toggle for swap types
        $('input[name="swapType"]').change(function() {
            if ($(this).val() === 'peer') {
                $('#swapPeerSection').slideDown(150);
                $('#swapDirectSection').slideUp(150);
            } else {
                $('#swapPeerSection').slideUp(150);
                $('#swapDirectSection').slideDown(150);
            }
        });

        // When coworker changes, fetch their shifts
        $('#swapTargetEmployeeId').change(function() {
            var coworkerId = $(this).val();
            var shiftSelect = $('#swapTargetRegistrationId');
            shiftSelect.empty().append($('<option value=""></option>').text(i18n.swapSelectCoworkerShiftPrompt));

            if (!coworkerId) return;

            $.get(`${urls.coworkerShifts}?coworkerId=${coworkerId}`, function(res) {
                if (res.success && res.shifts && res.shifts.length > 0) {
                    res.shifts.forEach(s => {
                        shiftSelect.append(`<option value="${s.shiftRegistrationId}">${s.dateStr} - ${s.shiftName} (${s.time})</option>`);
                    });
                } else {
                    shiftSelect.append($('<option value="" disabled></option>').text(i18n.swapNoAvailableShifts));
                }
            });
        });

        // Form Submit for Leave Request
        $('#leaveRequestForm').submit(function(e) {
            e.preventDefault();
            var empId = $('#leaveEmployeeId').val();
            if (!empId) empId = "0";

            var data = {
                EmployeeId: parseInt(empId),
                StartDate: $('#leaveStartDate').val(),
                EndDate: $('#leaveEndDate').val(),
                LeaveType: parseInt($('input[name="leaveType"]:checked').val()),
                Reason: $('#leaveReason').val().trim()
            };

            $('#btnSubmitLeave').prop('disabled', true);

            $.ajax({
                url: urls.createLeave,
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(data),
                headers: {
                    'RequestVerificationToken': antiForgeryToken()
                },
                success: function(res) {
                    if (res.success) {
                        $('#createLeaveRequestModal').modal('hide');
                        AppToast.success(res.message || i18n.leaveSubmitSuccess);
                        setTimeout(() => window.location.reload(), 1000);
                    } else {
                        AppToast.error(res.message || i18n.operationFailed);
                        $('#btnSubmitLeave').prop('disabled', false);
                    }
                },
                error: function(err) {
                    AppToast.error(err.responseJSON?.message || i18n.operationFailed);
                    $('#btnSubmitLeave').prop('disabled', false);
                }
            });
        });

        // Date & LeaveType change event listeners for dynamic validation
        $('#leaveStartDate, #leaveEndDate').change(function() {
            var startVal = $('#leaveStartDate').val();
            var endVal = $('#leaveEndDate').val();
            if (startVal && !endVal) {
                $('#leaveEndDate').val(startVal);
            }
            validateLeaveDays();
        });

        $('input[name="leaveType"]').change(function() {
            validateLeaveDays();
        });

        $('#leaveEmployeeId').change(function() {
            var empId = $(this).val();
            if (empId) {
                fetchLeaveBalance(empId);
            }
        });
    });

    function openShiftModal() {
        $('#createRequestModal').modal('hide');
        $('#shiftRequestForm')[0].reset();
        setTimeout(() => {
            var modal = new bootstrap.Modal(document.getElementById('createShiftRequestModal'));
            modal.show();
        }, 300);
    }

    function openSwapModal() {
        $('#createRequestModal').modal('hide');
        $('#swapShiftForm')[0].reset();
        $('#btnSubmitSwap').prop('disabled', false);
        $('#swapPeerSection').show();
        $('#swapDirectSection').hide();
        $('#swapTypePeer').prop('checked', true);

        var sourceSelect = $('#swapSourceRegistrationId');
        var cowSelect = $('#swapTargetEmployeeId');
        var shiftSelect = $('#swapTargetShiftId');

        sourceSelect.empty().append($('<option value=""></option>').text(i18n.swapSelectSourcePrompt));
        cowSelect.empty().append($('<option value=""></option>').text(i18n.swapSelectCoworkerPrompt));
        shiftSelect.empty().append($('<option value=""></option>').text(i18n.selectShift));

        $.get(urls.swapFormData, function(res) {
            if (res.success) {
                if (res.myShifts && res.myShifts.length > 0) {
                    $('#swapNoShiftsAlert').addClass('d-none');
                    res.myShifts.forEach(s => {
                        sourceSelect.append(`<option value="${s.shiftRegistrationId}">${s.dateStr} - ${s.shiftName} (${s.time})</option>`);
                    });
                } else {
                    $('#swapNoShiftsAlert').removeClass('d-none');
                }

                if (res.coworkers) {
                    res.coworkers.forEach(c => {
                        cowSelect.append(`<option value="${c.employeeId}">${c.employeeCode} - ${c.fullName}</option>`);
                    });
                }

                if (res.availableShifts) {
                    res.availableShifts.forEach(s => {
                        shiftSelect.append(`<option value="${s.shiftId}">${s.shiftName} (${s.time})</option>`);
                    });
                }

                var modal = new bootstrap.Modal(document.getElementById('createSwapShiftModal'));
                modal.show();
            } else {
                AppToast.error(res.message || i18n.operationFailed);
            }
        }).fail(function() {
            AppToast.error(i18n.operationFailed);
        });
    }

    function openLeaveModal() {
        $('#createRequestModal').modal('hide');
        $('#leaveRequestForm')[0].reset();
        $('#btnSubmitLeave').prop('disabled', false);
        $('#leaveBalanceAlert').addClass('d-none');

        const nowLocal = new Date();
        const today = nowLocal.getFullYear() + '-' + String(nowLocal.getMonth() + 1).padStart(2, '0') + '-' + String(nowLocal.getDate()).padStart(2, '0');
        $('#leaveStartDate').val(today);
        $('#leaveEndDate').val(today);
        $('#leaveStartDate').attr('min', today);
        $('#leaveEndDate').attr('min', today);

        const empId = $('#leaveEmployeeId').val() || 0;
        fetchLeaveBalance(empId);

        setTimeout(() => {
            var modal = new bootstrap.Modal(document.getElementById('createLeaveRequestModal'));
            modal.show();
        }, 300);
    }

    function fetchLeaveBalance(empId) {
        $.get(`${urls.leaveFormData}?employeeId=${empId}`, function(res) {
            if (res.success) {
                currentAnnualBalance = res.annualLeaveBalance;
                $('#leaveAnnualBalanceDisplay').text(currentAnnualBalance);
                validateLeaveDays();
            }
        });
    }

    function calculateLeaveDays() {
        const startStr = $('#leaveStartDate').val();
        const endStr = $('#leaveEndDate').val();
        if (!startStr || !endStr) return 0;
        const start = new Date(startStr);
        const end = new Date(endStr);
        if (end < start) return 0;
        const diffTime = Math.abs(end - start);
        return Math.ceil(diffTime / (1000 * 60 * 60 * 24)) + 1;
    }

    function validateLeaveDays() {
        const days = calculateLeaveDays();
        $('#leaveDaysCount').text(days);

        const leaveType = $('input[name="leaveType"]:checked').val();
        if (days <= 0) {
            $('#leaveBalanceAlertText').text(i18n.leaveEndBeforeStart);
            $('#leaveBalanceAlert').removeClass('d-none');
            $('#btnSubmitLeave').prop('disabled', true);
            return;
        }

        if (leaveType === '0') { // Paid
            if (days > currentAnnualBalance) {
                $('#leaveBalanceAlertText').text(i18n.leaveExceedsBalance.replace('{0}', days).replace('{1}', currentAnnualBalance));
                $('#leaveBalanceAlert').removeClass('d-none');
                $('#btnSubmitLeave').prop('disabled', true);
                return;
            }
        }

        $('#leaveBalanceAlert').addClass('d-none');
        $('#btnSubmitLeave').prop('disabled', false);
    }

    function openAttendanceModal() {
        $('#createRequestModal').modal('hide');
        $('#attendanceAdjustmentForm')[0].reset();
        $('#btnSubmitAdj').prop('disabled', false);

        var select = $('#adjRecordSelect');
        select.empty().append($('<option value=""></option>').text(i18n.adjLoadingRecords));

        $.get(urls.attendanceFormData, function(res) {
            select.empty();
            if (res.success && res.records && res.records.length > 0) {
                select.append($('<option value=""></option>').text(i18n.adjSelectShiftPrompt));
                res.records.forEach(r => {
                    var inOut = (r.checkIn ? r.checkIn : '--:--') + ' ~ ' + (r.checkOut ? r.checkOut : '--:--');
                    select.append(`<option value="${r.attendanceId}" data-date="${r.workDate}" data-shift="${r.shiftId || ''}" data-in="${r.checkIn}" data-out="${r.checkOut}">${r.dateStr} - ${r.shiftName} (${inOut})</option>`);
                });
            } else {
                select.append($('<option value="" disabled></option>').text(i18n.adjNoRecords));
            }
        });

        setTimeout(() => {
            var modal = new bootstrap.Modal(document.getElementById('createAttendanceModal'));
            modal.show();
        }, 300);
    }

    // Gắn ngay khi nạp script (như bản inline cũ, không chờ document ready)
    $('#adjRecordSelect').change(function() {
        var selected = $(this).find(':selected');
        var inTime = selected.data('in');
        var outTime = selected.data('out');
        $('#adjRequestedCheckIn').val(inTime || '');
        $('#adjRequestedCheckOut').val(outTime || '');
    });

    $('#attendanceAdjustmentForm').submit(function(e) {
        e.preventDefault();
        var selected = $('#adjRecordSelect').find(':selected');
        var attId = parseInt(selected.val()) || 0;
        var workDate = selected.data('date');
        var shiftId = parseInt(selected.data('shift')) || null;

        if (!attId && !workDate) {
            AppToast.error(i18n.adjErrSelectShift);
            return;
        }

        var inVal = $('#adjRequestedCheckIn').val() || null;
        var outVal = $('#adjRequestedCheckOut').val() || null;
        var reasonVal = $('#adjReason').val().trim();

        if (!reasonVal) {
            AppToast.error(i18n.adjErrReasonRequired);
            return;
        }

        if (!inVal && !outVal) {
            AppToast.error(i18n.adjErrTimeRequired);
            return;
        }

        if (inVal && inVal.length === 5) inVal += ':00';
        if (outVal && outVal.length === 5) outVal += ':00';

        var data = {
            AttendanceRecordId: attId > 0 ? attId : null,
            WorkDate: workDate,
            ShiftId: shiftId,
            RequestedCheckInTime: inVal,
            RequestedCheckOutTime: outVal,
            Reason: reasonVal
        };

        $('#btnSubmitAdj').prop('disabled', true);

        $.ajax({
            url: urls.createAttendance,
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(data),
            headers: {
                'RequestVerificationToken': antiForgeryToken()
            },
            success: function(res) {
                if (res.success) {
                    $('#createAttendanceModal').modal('hide');
                    AppToast.success(res.message);
                    setTimeout(() => window.location.reload(), 1000);
                } else {
                    AppToast.error(res.message || i18n.operationUnsuccessful);
                    $('#btnSubmitAdj').prop('disabled', false);
                }
            },
            error: function(err) {
                AppToast.error(err.responseJSON?.message || i18n.sysError);
                $('#btnSubmitAdj').prop('disabled', false);
            }
        });
    });

    // Được gọi từ thuộc tính onclick trong modal "Tạo yêu cầu"
    window.openShiftModal = openShiftModal;
    window.openSwapModal = openSwapModal;
    window.openLeaveModal = openLeaveModal;
    window.openAttendanceModal = openAttendanceModal;
})(jQuery, window.RWPM.requestIndex);
