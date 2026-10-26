/*
 * Màn hình "Yêu cầu & phê duyệt" — Views/Request/Index.cshtml.
 * Phần danh sách + khung chi tiết: bộ lọc (khoảng ngày, loại đơn, tìm kiếm), chọn đơn, duyệt / từ chối / xóa,
 * duyệt hàng loạt, cảnh báo trước khi duyệt, duyệt điều chỉnh chấm công, xuất CSV.
 * (Các modal tạo đơn mới nằm ở create-forms.js.)
 *
 * Config: window.RWPM.requestIndex
 *   exportData[], requests[]            dữ liệu đơn (xuất file / trang hiện tại)
 *   currentUsername, isManager, isAdmin, isHR, isStoreManager, currentUserStoreId
 *   filter { startDate, endDate, status, type, searchSuffix }
 *   pickerLocale                        locale cho daterangepicker
 *   urls { approvalWarnings, updateStatus, updateBulkStatus, deleteRequest, ... }
 *   i18n { ... }                        chuỗi đã bản địa hóa
 *
 * Hàm global (gọi từ markup): onTypeFilterChange, updateSelectedStatus, submitRejection, deleteSelectedRequest,
 *   clearBulkSelection, updateBulkStatus, submitApproveAttendance, exportRequestsToCSV.
 */
(function ($, cfg) {
    'use strict';

    var requestsExportData = cfg.exportData;
    var requestsData = cfg.requests;
    var currentUsername = cfg.currentUsername;
    var isManager = cfg.isManager;
    var isCurrentUserAdmin = cfg.isAdmin;
    var isCurrentUserHR = cfg.isHR;
    var isCurrentUserStoreManager = cfg.isStoreManager;
    var currentUserStoreId = cfg.currentUserStoreId;
    var reqI18n = cfg.i18n;
    var urls = cfg.urls;
    var filter = cfg.filter;

    let currentSelectedId = 0;
    let currentSelectedType = 'ShiftReg';
    let pendingRejectType = ''; // 'single' or 'bulk'

    function antiForgeryToken() {
        return $('#antiForgeryForm input[name="__RequestVerificationToken"]').val();
    }

    $(document).ready(function() {
        // Date Picker setup
        if ($.fn.daterangepicker) {
            $('#dateRangePicker').daterangepicker({
                locale: cfg.pickerLocale,
                startDate: filter.startDate,
                endDate: filter.endDate
            }, function(start, end, label) {
                var statusParam = filter.status;
                var statusQuery = statusParam ? `&status=${statusParam}` : '';
                var typeParam = filter.type;
                var typeQuery = typeParam ? `&type=${typeParam}` : '';
                window.location.href = `?start=${start.format('YYYY-MM-DD')}&end=${end.format('YYYY-MM-DD')}${statusQuery}${typeQuery}${filter.searchSuffix}`;
            });
        }

        // Enter: tìm kiếm phía server trên toàn bộ danh sách (mọi trang); gõ phím: lọc nhanh trên trang hiện tại
        $('#requestSearchInput').on('keydown', function (e) {
            if (e.key !== 'Enter') return;
            e.preventDefault();
            var params = new URLSearchParams(window.location.search);
            var q = $(this).val().trim();
            if (q) params.set('q', q); else params.delete('q');
            params.delete('pageNumber');
            window.location.search = params.toString();
        });

        // Client-side quick filter
        $('#requestSearchInput').on('input', function() {
            var query = $(this).val().toLowerCase().trim();
            var visibleCount = 0;
            $('.request-card').each(function() {
                var match = $(this).text().toLowerCase().indexOf(query) !== -1;
                $(this).toggleClass('d-none', !match);
                if (match) visibleCount++;
            });
            $('#filteredCountBadge').text(visibleCount);
        });

        // Card click logic
        $(document).on('click', '.request-card', function(e) {
            if ($(e.target).is('input[type="checkbox"]')) {
                return;
            }
            $('.request-card').removeClass('active-card');
            $(this).addClass('active-card');

            var id = $(this).data('id');
            var type = $(this).data('type');
            showDetail(id, type);
        });

        // Keyboard: Enter / Space on a focused row opens it
        $(document).on('keydown', '.request-card', function(e) {
            if ($(e.target).is('input')) return;
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                $(this).trigger('click');
            }
        });

        // Select first card by default if any
        if (requestsData.length > 0) {
            $('.request-card').first().click();
        }

        // Checkbox logic
        $('#selectAll').change(function() {
            $('.request-checkbox').prop('checked', $(this).prop('checked'));
            checkBulkAction();
        });

        $(document).on('change', '.request-checkbox', function() {
            if ($('.request-checkbox:checked').length === $('.request-checkbox').length) {
                $('#selectAll').prop('checked', true);
            } else {
                $('#selectAll').prop('checked', false);
            }
            checkBulkAction();
        });
    });

    function onTypeFilterChange(typeVal) {
        var statusParam = filter.status;
        var statusQuery = statusParam ? `&status=${statusParam}` : '';
        var typeQuery = typeVal ? `&type=${typeVal}` : '';
        window.location.href = `?start=${filter.startDate}&end=${filter.endDate}${statusQuery}${typeQuery}${filter.searchSuffix}`;
    }

    function statusBadgeHtml(status) {
        if (status === 0) return '<span class="app-badge app-badge-warning">' + escapeHtml(reqI18n.pendingShort) + '</span>';
        if (status === 1) return '<span class="app-badge app-badge-success">' + escapeHtml(reqI18n.approvedShort) + '</span>';
        return '<span class="app-badge app-badge-danger">' + escapeHtml(reqI18n.rejectedShort) + '</span>';
    }

    function dlRow(label, valueHtml) {
        return '<div class="req-dl-row"><dt>' + escapeHtml(label) + '</dt><dd>' + valueHtml + '</dd></div>';
    }

    // Khung "Cần lưu ý trước khi duyệt": cảnh báo phía client (phép năm) + cảnh báo máy chủ (cùng nguồn với needConfirm)
    function renderWarnings(list) {
        var items = (list || []).filter(function (w) { return w; });
        if (items.length === 0) {
            $('#detailWarningsBox').hide();
            $('#detailWarningsList').empty();
            return;
        }
        var ul = $('#detailWarningsList').empty();
        items.forEach(function (w) { ul.append($('<li></li>').text(w)); });
        $('#detailWarningsBox').show();
    }

    function loadApprovalWarnings(req, localWarnings) {
        renderWarnings(localWarnings);
        if (req.requestType !== 'ShiftReg' && req.requestType !== 'ShiftSwap') return;
        var reqId = req.id, reqType = req.requestType;
        $.get(urls.approvalWarnings, { id: reqId, requestType: reqType }, function (res) {
            if (currentSelectedId !== reqId || currentSelectedType !== reqType) return; // người dùng đã chọn đơn khác
            if (res && res.warnings && res.warnings.length) {
                renderWarnings(localWarnings.concat(res.warnings));
            }
        });
    }

    function showDetail(id, type) {
        var req = requestsData.find(x => x.id === id && x.requestType === type);
        if (!req) return;

        currentSelectedId = id;
        currentSelectedType = type;

        $('#detailTypeName').text(req.requestTypeName);
        $('#detailEmployeeName').text(req.employeeName);
        $('#detailStoreName').text(req.storeName || '-');
        $('#detailCreatedDate').text(req.createdDateStr);
        $('#detailCode').text(req.code);
        $('#detailWorkDate').text(req.workDateStr);
        $('#detailNote').text(req.note || '-');
        $('#detailStatus').html(statusBadgeHtml(req.status));
        $('#detailApproverNote').val('');

        var localWarnings = [];
        if (req.requestType === 'Leave') {
            $('#detailShiftLabel').text(reqI18n.leaveDuration);
            $('#detailShiftName').text(req.shiftName);
            $('#detailLeaveTypeDesc').text(req.leaveTypeName + ' (' + req.leaveDays + ' ' + reqI18n.daysUnit + ')');
            $('#detailLeaveBalanceBadge').text(req.annualLeaveBalance + ' ' + reqI18n.daysUnit);
            $('.detail-leave-row').show();
            $('.detail-att-row').hide();
            if (req.status === 0 && req.leaveType === 0 && req.leaveDays > req.annualLeaveBalance) {
                localWarnings.push(reqI18n.leaveExceedsBalance.replace('{0}', req.leaveDays).replace('{1}', req.annualLeaveBalance));
            }
        } else if (req.requestType === 'Attendance') {
            $('#detailShiftLabel').text(reqI18n.shift);
            $('#detailShiftName').text(req.shiftName);
            $('.detail-leave-row').hide();
            $('#detailAttOriginal').text((req.actualCheckInTimeStr || '--:--') + ' ~ ' + (req.actualCheckOutTimeStr || '--:--'));
            $('#detailAttProposed').text((req.requestedCheckInTimeStr || '--:--') + ' ~ ' + (req.requestedCheckOutTimeStr || '--:--'));
            $('.detail-att-row').show();
        } else {
            $('#detailShiftLabel').text(reqI18n.shift);
            $('#detailShiftName').text(req.shiftName);
            $('.detail-leave-row').hide();
            $('.detail-att-row').hide();
        }

        // Shift Swap target
        if (req.requestType === 'ShiftSwap' && req.swapSummary) {
            $('#detailSwapSummary').text(req.swapSummary);
            $('#detailSwapTargetRow').show();
        } else {
            $('#detailSwapTargetRow').hide();
        }

        // Reset actions, notice, warnings & result
        $('#btnApproveDetail').hide();
        $('#btnRejectDetail').hide();
        $('#btnDeleteDetail').hide();
        $('#detailActionButtonsContainer').hide();
        $('#detailDecisionArea').hide();
        $('#requestApprovalNotice').hide();
        $('#detailResultBanner').hide().empty();
        renderWarnings([]);

        if (req.status === 0) { // Pending
            var isSelf = (req.requesterUsername && req.requesterUsername.toLowerCase() === currentUsername.toLowerCase());
            var canApprove = false;
            var cannotApproveReason = '';

            if (!isManager) {
                canApprove = false;
                cannotApproveReason = 'not_manager';
            } else if (isSelf) {
                canApprove = false;
                cannotApproveReason = 'self';
            } else if (req.requestType === 'Attendance') {
                // Đơn của Quản lý cửa hàng / HR chỉ Admin được duyệt (giống kiểm tra phía máy chủ)
                var isRequesterManagement = (req.requesterRole === 2 || req.requesterRole === 4); // HR = 2, StoreManager = 4
                if (isRequesterManagement) {
                    if (isCurrentUserAdmin) {
                        canApprove = true;
                    } else {
                        canApprove = false;
                        cannotApproveReason = 'admin_only';
                    }
                } else {
                    if (isCurrentUserAdmin || isCurrentUserHR) {
                        canApprove = true;
                    } else if (isCurrentUserStoreManager) {
                        if (currentUserStoreId > 0 && req.storeId === currentUserStoreId) {
                            canApprove = true;
                        } else {
                            canApprove = false;
                            cannotApproveReason = 'store_mismatch';
                        }
                    }
                }
            } else {
                if (isCurrentUserAdmin || isCurrentUserHR) {
                    canApprove = true;
                } else if (isCurrentUserStoreManager) {
                    if (currentUserStoreId > 0 && req.storeId === currentUserStoreId) {
                        canApprove = true;
                    } else {
                        canApprove = false;
                        cannotApproveReason = 'store_mismatch';
                    }
                }
            }

            if (canApprove) {
                $('#detailDecisionArea').show();
                $('#btnApproveDetail').css('display', 'inline-flex');
                $('#btnRejectDetail').css('display', 'inline-flex');
                $('#detailActionButtonsContainer').css('display', 'flex');
                loadApprovalWarnings(req, localWarnings);
            } else {
                if (isSelf || !isManager) {
                    $('#btnDeleteDetail').css('display', 'inline-flex');
                    $('#detailActionButtonsContainer').css('display', 'flex');
                } else if (cannotApproveReason === 'admin_only') {
                    $('#requestApprovalNotice').html('<i class="bi bi-shield-lock me-2"></i>' + escapeHtml(reqI18n.noticeAdminOnly)).show();
                } else if (cannotApproveReason === 'store_mismatch') {
                    $('#requestApprovalNotice').html('<i class="bi bi-info-circle me-2"></i>' + escapeHtml(reqI18n.noticeStoreMismatch)).show();
                }
            }
        } else { // Processed: who / when / reason instead of buttons
            var rows = dlRow(reqI18n.processedBy, '<strong>' + escapeHtml(req.approvedBy || reqI18n.defaultManager) + '</strong>');
            if (req.approvedAt) rows += dlRow(reqI18n.processedAt, '<span class="tabular">' + escapeHtml(req.approvedAt) + '</span>');
            if (req.status === 2 && req.rejectionReason) rows += dlRow(reqI18n.rejectReason, escapeHtml(req.rejectionReason));
            var title = req.status === 1 ? reqI18n.bannerApproved : reqI18n.bannerRejected;
            var icon = req.status === 1 ? 'bi-check-circle text-success' : 'bi-x-circle text-danger';
            $('#detailResultBanner').html(
                '<div class="req-result">' +
                    '<div class="fw-medium pt-2 pb-1"><i class="bi ' + icon + ' me-1"></i>' + escapeHtml(title) + '</div>' +
                    '<dl class="req-dl">' + rows + '</dl>' +
                '</div>'
            ).show();
        }

        $('#emptyDetailPane').hide();
        $('#requestDetailPane').fadeIn(150);
    }

    async function updateSelectedStatus(status) {
        if (currentSelectedId === 0) return;

        if (status === 2) {
            pendingRejectType = 'single';
            // Ghi chú trong khung chi tiết được điền sẵn làm lý do từ chối (người duyệt vẫn xác nhận trong hộp thoại)
            $('#modalRejectionReason').val(($('#detailApproverNote').val() || '').trim());
            var modal = new bootstrap.Modal(document.getElementById('rejectReasonModal'));
            modal.show();
            return;
        }

        if (currentSelectedType === 'Attendance' && status === 1) {
            openApproveAttendanceModal(currentSelectedId);
            $('#apprModal_ApproverNote').val(($('#detailApproverNote').val() || '').trim());
            return;
        }

        if (!(await AppConfirm({ message: reqI18n.confirmApproveMsg, type: 'success', icon: 'bi-check2-circle' }))) return;

        sendSingleStatus(currentSelectedId, status, currentSelectedType, false);
    }

    // Cảnh báo khi duyệt (ngoài thời gian có thể làm việc / vượt nhu cầu): máy chủ trả needConfirm -> hỏi lại rồi gửi confirmWarnings=true
    async function confirmApprovalWarnings(warnings) {
        return await AppConfirm({
            title: reqI18n.warnConfirmTitle,
            message: (warnings || []).join('\n') + '\n\n' + reqI18n.warnConfirmContinue,
            type: 'warning',
            icon: 'bi-exclamation-triangle'
        });
    }

    function sendSingleStatus(id, status, requestType, confirmWarnings) {
        $.ajax({
            url: urls.updateStatus,
            type: 'POST',
            data: {
                id: id,
                status: status,
                requestType: requestType,
                confirmWarnings: confirmWarnings
            },
            headers: {
                'RequestVerificationToken': antiForgeryToken()
            },
            success: async function(res) {
                if (res && res.needConfirm) {
                    if (await confirmApprovalWarnings(res.warnings)) sendSingleStatus(id, status, requestType, true);
                    return;
                }
                if (res.success) {
                    AppToast.success(res.message || reqI18n.updateStatusSuccess);
                    setTimeout(() => window.location.reload(), 800);
                } else {
                    AppToast.error(reqI18n.errorPrefix + res.message);
                }
            },
            error: function() {
                AppToast.error(reqI18n.sysError);
            }
        });
    }

    function submitRejection() {
        var reason = $('#modalRejectionReason').val().trim();
        if (!reason) {
            AppToast.warning(reqI18n.rejectReasonRequired);
            return;
        }

        $('#rejectReasonModal').modal('hide');

        if (pendingRejectType === 'single') {
            if (currentSelectedId === 0) return;
            $.ajax({
                url: urls.updateStatus,
                type: 'POST',
                data: {
                    id: currentSelectedId,
                    status: 2,
                    reason: reason,
                    requestType: currentSelectedType
                },
                headers: {
                    'RequestVerificationToken': antiForgeryToken()
                },
                success: function(res) {
                    if (res.success) {
                        AppToast.success(res.message || reqI18n.rejectSuccess);
                        setTimeout(() => window.location.reload(), 800);
                    } else {
                        AppToast.error(reqI18n.errorPrefix + res.message);
                    }
                },
                error: function() {
                    AppToast.error(reqI18n.sysError);
                }
            });
        } else if (pendingRejectType === 'bulk') {
            var selectedIds = [];
            $('.request-checkbox:checked').each(function() {
                selectedIds.push(parseInt($(this).val()));
            });

            if (!ensureSingleSelectedType()) return;
            var firstChecked = $('.request-checkbox:checked').first();
            var reqType = firstChecked.data('type') || 'ShiftReg';

            var data = {
                Ids: selectedIds,
                Status: 2,
                Reason: reason,
                RequestType: reqType
            };

            $.ajax({
                url: urls.updateBulkStatus,
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(data),
                headers: {
                    'RequestVerificationToken': antiForgeryToken()
                },
                success: function(res) {
                    if (res.success) {
                        AppToast.success(reqI18n.bulkRejectSuccess);
                        setTimeout(() => window.location.reload(), 800);
                    } else {
                        AppToast.error(reqI18n.errorPrefix + res.message);
                    }
                },
                error: function() {
                    AppToast.error(reqI18n.sysError);
                }
            });
        }
    }

    // Duyệt/từ chối hàng loạt chỉ áp dụng cho một loại đơn mỗi lần (Id các loại đơn có thể trùng nhau)
    function ensureSingleSelectedType() {
        var types = new Set();
        $('.request-checkbox:checked').each(function() { types.add($(this).data('type') || 'ShiftReg'); });
        if (types.size > 1) {
            AppToast.warning(reqI18n.bulkSingleTypeOnly);
            return false;
        }
        return true;
    }

    async function deleteSelectedRequest() {
        if (currentSelectedId === 0) return;
        if (!(await AppConfirm({ message: reqI18n.confirmDeleteMsg, type: 'danger' }))) return;

        $.ajax({
            url: urls.deleteRequest,
            type: 'POST',
            data: {
                id: currentSelectedId,
                requestType: currentSelectedType
            },
            headers: {
                'RequestVerificationToken': antiForgeryToken()
            },
            success: function(res) {
                if (res.success) {
                    AppToast.success(res.message || reqI18n.deleteSuccess);
                    setTimeout(() => window.location.reload(), 800);
                } else {
                    AppToast.error(reqI18n.errorPrefix + res.message);
                }
            },
            error: function() {
                AppToast.error(reqI18n.sysError);
            }
        });
    }

    function checkBulkAction() {
        var checked = $('.request-checkbox:checked');
        var selectedCount = checked.length;
        $('.request-card').removeClass('is-checked');
        checked.closest('.request-card').addClass('is-checked');
        $('#bulkSelectedCount').text(selectedCount);
        $('#bulkSelectedText').text(reqI18n.selectedCount.replace('{0}', selectedCount));
        if (selectedCount > 0) {
            $('#bulkActionBar').css('display', 'flex');
        } else {
            $('#bulkActionBar').hide();
        }
    }

    function clearBulkSelection() {
        $('.request-checkbox').prop('checked', false);
        $('#selectAll').prop('checked', false);
        checkBulkAction();
    }

    async function updateBulkStatus(status) {
        var selectedIds = [];
        $('.request-checkbox:checked').each(function() {
            selectedIds.push(parseInt($(this).val()));
        });

        if (selectedIds.length === 0) return;
        if (!ensureSingleSelectedType()) return;

        if (status === 2) {
            pendingRejectType = 'bulk';
            $('#modalRejectionReason').val('');
            var modal = new bootstrap.Modal(document.getElementById('rejectReasonModal'));
            modal.show();
            return;
        }

        var confirmBulkMsg = reqI18n.confirmBulkApproveMsg.replace('{0}', selectedIds.length);
        if (!(await AppConfirm({ message: confirmBulkMsg, type: 'success', icon: 'bi-check2-all' }))) return;

        var firstChecked = $('.request-checkbox:checked').first();
        var reqType = firstChecked.data('type') || 'ShiftReg';

        var data = {
            Ids: selectedIds,
            Status: status,
            RequestType: reqType
        };

        sendBulkStatus(data);
    }

    function sendBulkStatus(data) {
        $.ajax({
            url: urls.updateBulkStatus,
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(data),
            headers: {
                'RequestVerificationToken': antiForgeryToken()
            },
            success: async function(res) {
                if (res && res.needConfirm) {
                    if (await confirmApprovalWarnings(res.warnings)) sendBulkStatus(Object.assign({}, data, { ConfirmWarnings: true }));
                    return;
                }
                if (res.success) {
                    AppToast.success(reqI18n.bulkActionSuccess);
                    setTimeout(() => window.location.reload(), 800);
                } else {
                    AppToast.error(reqI18n.errorPrefix + res.message);
                }
            },
            error: function() {
                AppToast.error(reqI18n.sysError);
            }
        });
    }

    function openApproveAttendanceModal(id) {
        var req = requestsData.find(x => x.id === id && x.requestType === 'Attendance');
        if (!req) return;

        $('#apprModal_EmpName').text(req.employeeName || '-');
        $('#apprModal_WorkDate').text(req.workDateStr || '-');
        $('#apprModal_ShiftName').text(req.shiftName || '-');
        $('#apprModal_OldInOut').text((req.actualCheckInTimeStr || '--:--') + ' ~ ' + (req.actualCheckOutTimeStr || '--:--'));
        $('#apprModal_ReqInOut').text((req.requestedCheckInTimeStr || '--:--') + ' ~ ' + (req.requestedCheckOutTimeStr || '--:--'));
        $('#apprModal_Reason').text(req.note || reqI18n.apprNoReason);

        // Giờ yêu cầu trống thì điền sẵn giờ hiện tại của bản ghi chấm công để không bị ghi đè thành rỗng khi duyệt
        var hasTime = function(v) { return v && v !== '-'; };
        var inVal = hasTime(req.requestedCheckInTimeStr) ? req.requestedCheckInTimeStr : (hasTime(req.actualCheckInTimeStr) ? req.actualCheckInTimeStr : '');
        var outVal = hasTime(req.requestedCheckOutTimeStr) ? req.requestedCheckOutTimeStr : (hasTime(req.actualCheckOutTimeStr) ? req.actualCheckOutTimeStr : '');
        $('#apprModal_ApprovedCheckIn').val(inVal);
        $('#apprModal_ApprovedCheckOut').val(outVal);
        $('#apprModal_ApproverNote').val('');
        $('#btnConfirmApproveAttendance').prop('disabled', false).html('<i class="bi bi-check-circle me-1"></i>' + escapeHtml(reqI18n.confirmApproveBtn));

        var modal = new bootstrap.Modal(document.getElementById('approveAttendanceModal'));
        modal.show();
    }

    function submitApproveAttendance() {
        var inTime = $('#apprModal_ApprovedCheckIn').val();
        var outTime = $('#apprModal_ApprovedCheckOut').val();
        var note = $('#apprModal_ApproverNote').val().trim();

        if (!inTime && !outTime) {
            AppToast.error(reqI18n.apprErrTimeRequired);
            return;
        }

        if (inTime && inTime.length === 5) inTime += ':00';
        if (outTime && outTime.length === 5) outTime += ':00';

        $('#btnConfirmApproveAttendance').prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span> ' + escapeHtml(reqI18n.processing));

        $.ajax({
            url: urls.updateStatus,
            type: 'POST',
            data: {
                id: currentSelectedId,
                status: 1,
                requestType: 'Attendance',
                approvedCheckInTime: inTime || null,
                approvedCheckOutTime: outTime || null,
                approverNote: note || null
            },
            headers: {
                'RequestVerificationToken': antiForgeryToken()
            },
            success: function(res) {
                if (res.success) {
                    $('#approveAttendanceModal').modal('hide');
                    AppToast.success(res.message || reqI18n.adjApproveSuccess);
                    setTimeout(() => window.location.reload(), 800);
                } else {
                    $('#btnConfirmApproveAttendance').prop('disabled', false).html('<i class="bi bi-check-circle me-1"></i>' + escapeHtml(reqI18n.confirmApproveBtn));
                    AppToast.error(reqI18n.errorPrefix + res.message);
                }
            },
            error: function(err) {
                $('#btnConfirmApproveAttendance').prop('disabled', false).html('<i class="bi bi-check-circle me-1"></i>' + escapeHtml(reqI18n.confirmApproveBtn));
                AppToast.error(err.responseJSON?.message || reqI18n.sysError);
            }
        });
    }

    function exportRequestsToCSV() {
        if (!requestsExportData || requestsExportData.length === 0) {
            AppToast.warning(reqI18n.exportNoData);
            return;
        }

        var csvRows = [];
        var csvHeaders = reqI18n.exportHeaders;
        csvRows.push(csvHeaders.map(function(h) { return '"' + String(h).replace(/"/g, '""') + '"'; }).join(','));

        requestsExportData.forEach(function(r) {
            var statusStr = r.status === 1 ? reqI18n.approved : (r.status === 2 ? reqI18n.rejected : reqI18n.pending);
            var row = [
                `"${(r.code || '').replace(/"/g, '""')}"`,
                `"${(r.requestTypeName || '').replace(/"/g, '""')}"`,
                `"${(r.employeeName || '').replace(/"/g, '""')}"`,
                `"${(r.storeName || '').replace(/"/g, '""')}"`,
                `"${(r.shiftName || '').replace(/"/g, '""')}"`,
                `"${(r.workDateStr || '').replace(/"/g, '""')}"`,
                `"${(r.createdDateStr || '').replace(/"/g, '""')}"`,
                `"${statusStr.replace(/"/g, '""')}"`,
                `"${(r.approvedBy || '').replace(/"/g, '""')}"`,
                `"${(r.rejectionReason || '').replace(/"/g, '""')}"`,
                `"${(r.note || '').replace(/"/g, '""')}"`
            ];
            csvRows.push(row.join(','));
        });

        var csvContent = '﻿' + csvRows.join('\r\n');
        var blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
        var url = URL.createObjectURL(blob);
        var a = document.createElement('a');
        var now = new Date();
        var dateStr = now.getFullYear() + (now.getMonth() + 1).toString().padStart(2, '0') + now.getDate().toString().padStart(2, '0');
        a.href = url;
        a.download = reqI18n.exportFileName + '_' + dateStr + '.csv';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);

        AppToast.success(reqI18n.exportSuccess);
    }

    function escapeHtml(str) {
        if (!str) return '';
        return $('<div>').text(str).html();
    }

    // Được gọi từ thuộc tính onclick / onchange / onsubmit trong markup
    window.onTypeFilterChange = onTypeFilterChange;
    window.updateSelectedStatus = updateSelectedStatus;
    window.submitRejection = submitRejection;
    window.deleteSelectedRequest = deleteSelectedRequest;
    window.clearBulkSelection = clearBulkSelection;
    window.updateBulkStatus = updateBulkStatus;
    window.submitApproveAttendance = submitApproveAttendance;
    window.exportRequestsToCSV = exportRequestsToCSV;
})(jQuery, window.RWPM.requestIndex);
