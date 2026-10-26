// Views/ShiftRegistration/Index.cshtml — lưới ca: tiêu đề cột, các ô ca theo tuần, tổng kết tuần, lọc nhân viên.
// Thứ tự nạp: core.js, grid.js, coverage.js, week-picker.js, modals.js, index.js (cùng dùng window.RWPM.shiftRegistration).
// Cấu hình: window.RWPM.shiftRegistrationIndex = { isManager, coverageStoreId, shifts, employees, urls: { availableShifts }, i18n: {...} }
(function ($, cfg) {
    'use strict';
    var app = window.RWPM.shiftRegistration;
    var S = app.state, T = app.T, L = cfg.i18n;
    var escAttr = app.escAttr, escapeHtml = app.escapeHtml, getISODateString = app.getISODateString;

    function initGrid() {
        // Weekly Checkbox toggle & slot click handlers
        $(document).on('change', '.weekly-shift-cb', function(e) {
            updateSlotCardVisual($(this));
            updateWeeklySummary();
        });

        // Clicking available slot toggles checkbox
        $(document).on('click', '.shift-slot.is-available, .shift-slot.is-pending', function(e) {
            if ($(e.target).is('input[type="checkbox"]') || $(e.target).closest('.btn-view-slot').length > 0 || $(e.target).is('label')) {
                return;
            }
            let cb = $(this).find('.weekly-shift-cb:not(:disabled)');
            if (cb.length > 0) {
                cb.prop('checked', !cb.prop('checked')).trigger('change');
            }
        });
    }

    function updateSlotCardVisual(cb) {
        let slot = cb.closest('.shift-slot');
        let isChecked = cb.is(':checked');

        if (slot.hasClass('was-pending')) {
            if (!isChecked) {
                slot.addClass('is-pending-cancelled');
                slot.find('.badge-status').removeClass('status-pending').addClass('status-unavailable').html('<i class="bi bi-x-circle"></i> ' + L.statusUnregister);
            } else {
                slot.removeClass('is-pending-cancelled');
                slot.find('.badge-status').removeClass('status-unavailable').addClass('status-pending').html('<i class="bi bi-hourglass-split"></i> ' + L.statusPending);
            }
        } else if (!slot.hasClass('is-approved') && !slot.hasClass('is-past') && !slot.hasClass('is-unavailable')) {
            if (isChecked) {
                slot.addClass('is-selected');
                slot.find('.badge-status').removeClass('status-available').addClass('status-selected').html('<i class="bi bi-check-circle-fill"></i> ' + L.statusNewlySelected);
            } else {
                slot.removeClass('is-selected');
                slot.find('.badge-status').removeClass('status-selected').addClass('status-available').html('<i class="bi bi-circle"></i> ' + L.statusNotRegistered);
            }
        }
    }

    function updateWeeklySummary() {
        let newSelectedCount = 0;
        let pendingCount = 0;
        let approvedCount = 0;
        let totalHours = 0;

        let shiftMap = {};
        S.shiftsList.forEach(s => { shiftMap[s.id] = s; });

        $('.weekly-shift-cb:checked').each(function() {
            let slot = $(this).closest('.shift-slot');
            let val = $(this).val();
            let shiftId = parseInt(val.split('_')[0]);
            let sObj = shiftMap[shiftId];
            let hours = (sObj && sObj.hours) ? sObj.hours : 0;

            totalHours += hours;

            if (slot.hasClass('is-approved') || $(this).prop('disabled')) {
                approvedCount++;
            } else if (slot.hasClass('was-pending')) {
                pendingCount++;
            } else {
                newSelectedCount++;
            }
        });

        $('#summaryNewCount').text(newSelectedCount);
        $('#summaryPendingCount').text(pendingCount);
        $('#summaryApprovedCount').text(approvedCount);
        $('#summaryTotalHours').text(L.summaryHoursFormat.replace('{0}', totalHours.toFixed(1)));
    }

    function renderHeader() {
        let tempDate = new Date(S.currentWeekStart);
        let today = new Date();
        let todayIso = getISODateString(today);
        let days = [L.dayMon, L.dayTue, L.dayWed, L.dayThu, L.dayFri, L.daySat, L.daySun];

        if (S.activeMode === 'manager') {
            let tr = '<th class="th-shift-col"><div class="input-group input-group-sm"><span class="input-group-text bg-transparent border-0"><i class="bi bi-search"></i></span><input type="text" class="form-control border-0 bg-transparent ps-1" placeholder="' + T.search + '" aria-label="' + T.search + '" onkeyup="filterEmployeesLocally(this.value)"></div></th>';
            for (let i = 0; i < 7; i++) {
                let cellDateIso = getISODateString(tempDate);
                let isToday = (cellDateIso === todayIso);
                let dateStr = tempDate.getDate().toString().padStart(2, '0') + '/' + (tempDate.getMonth() + 1).toString().padStart(2, '0');
                tr += `<th class="${isToday ? 'th-today' : ''}"><div>${days[i]}</div><div class="small fw-normal text-muted">${dateStr}</div></th>`;
                tempDate.setDate(tempDate.getDate() + 1);
            }
            $('#gridHeaderRow').html(tr);
        } else {
            let tr = '<th class="th-shift-col"><div class="d-flex align-items-center gap-1"><i class="bi bi-clock-history text-muted"></i><span>' + escapeHtml(L.gridShiftColumn) + '</span></div></th>';
            for (let i = 0; i < 7; i++) {
                let cellDateIso = getISODateString(tempDate);
                let isToday = (cellDateIso === todayIso);
                let dateStr = tempDate.getDate().toString().padStart(2, '0') + '/' + (tempDate.getMonth() + 1).toString().padStart(2, '0');
                let todayBadge = isToday ? '<span class="app-badge app-badge-info ms-1" style="height: 18px; font-size: 10px;">' + escapeHtml(L.gridToday) + '</span>' : '';
                tr += `<th class="${isToday ? 'th-today' : ''}"><div>${days[i]} ${todayBadge}</div><div class="small fw-normal text-muted">${dateStr}</div></th>`;
                tempDate.setDate(tempDate.getDate() + 1);
            }
            $('#gridHeaderRow').html(tr);
        }
    }

    function filterEmployeesLocally(text) {
        text = text.toLowerCase();
        $('#gridBody tr').each(function() {
            var empName = $(this).find('.shift-name-col').text().toLowerCase();
            if (empName.indexOf(text) > -1) {
                $(this).show();
            } else {
                $(this).hide();
            }
        });
    }

    function renderGrid(events) {
        let tbody = '';
        let today = new Date();
        today.setHours(0, 0, 0, 0);

        if (S.activeMode === 'manager') {
            var selectedEmpId = $('#filterEmployeeId').val();
            var empsToRender = S.employeesList;

            if (S.employeesList.length === 1 && S.employeesList[0].id === 'me') {
                let uniqueEmps = {};
                events.forEach(e => {
                    if (!uniqueEmps[e.employeeId]) {
                        uniqueEmps[e.employeeId] = { id: e.employeeId, name: e.employeeName };
                    }
                });
                empsToRender = Object.values(uniqueEmps);
                if (empsToRender.length === 0) empsToRender = [{ id: '', name: L.shiftNoShifts }];
            } else if (selectedEmpId) {
                empsToRender = S.employeesList.filter(e => e.id == selectedEmpId);
            }

            empsToRender.forEach(emp => {
                tbody += `<tr>`;
                tbody += `
                    <td class="shift-name-col">
                        <div class="d-flex align-items-center gap-2">
                            <div class="avatar-sm bg-light text-primary rounded-circle d-flex align-items-center justify-content-center p-2" style="width: 32px; height: 32px;">
                                <i class="bi bi-person-fill"></i>
                            </div>
                            <h6 class="mb-0 text-dark small fw-bold">${escapeHtml(emp.name)}</h6>
                        </div>
                    </td>
                `;

                let tempDate = new Date(S.currentWeekStart);
                for (let i = 0; i < 7; i++) {
                    let cellDateStr = getISODateString(tempDate);
                    let dayEvents = events.filter(e => {
                        let eDate = e.start.split('T')[0];
                        return (e.employeeId == emp.id || !emp.id) && eDate === cellDateStr;
                    });

                    if (dayEvents.length > 0) {
                        tbody += `<td class="shift-grid-cell" onclick="openCreateModal(new Date('${cellDateStr}'), '${emp.id}')">`;
                        tbody += `<div class="d-flex flex-column gap-1 h-100">`;
                        dayEvents.forEach(dayEvent => {
                            let eJson = JSON.stringify(dayEvent).replace(/'/g, "&#39;");
                            let timeDisplay = (dayEvent.startTime && dayEvent.endTime) ? `${escapeHtml(dayEvent.startTime)} – ${escapeHtml(dayEvent.endTime)}` : '';
                            let badgeHtml = dayEvent.statusId === 1 
                                ? `<span class="badge-status status-approved"><i class="bi bi-check-circle-fill"></i> ${T.approved}</span>` 
                                : (dayEvent.statusId === 2 
                                    ? `<span class="badge-status status-rejected"><i class="bi bi-x-circle-fill"></i> ${T.rejected}</span>` 
                                    : `<span class="badge-status status-pending"><i class="bi bi-hourglass-split"></i> ${T.pending}</span>`);
                            let slotCls = dayEvent.statusId === 1 ? 'is-approved' : (dayEvent.statusId === 2 ? 'is-rejected' : 'is-pending');
                            tbody += `
                                <div class="shift-slot ${slotCls}" style="cursor:pointer" onclick='event.stopPropagation(); openViewModal(${eJson})'>
                                    <div class="d-flex justify-content-between align-items-center">
                                        <span class="slot-time-text">${escapeHtml(dayEvent.title)}</span>
                                        ${badgeHtml}
                                    </div>
                                    <div class="slot-sub-text text-muted">${timeDisplay}</div>
                                </div>
                            `;
                        });
                        tbody += `</div></td>`;
                    } else {
                        tbody += `
                            <td class="shift-grid-cell" onclick="openCreateModal(new Date('${cellDateStr}'), '${emp.id}')">
                                <div class="shift-slot is-available justify-content-center align-items-center text-muted">
                                    <i class="bi bi-plus-lg fs-5"></i>
                                </div>
                            </td>
                        `;
                    }
                    tempDate.setDate(tempDate.getDate() + 1);
                }
                tbody += `</tr>`;
            });
        } else {
            // Employee Mode: Shifts List as Rows
            if (S.shiftsList.length === 0) {
                tbody += `
                    <tr>
                        <td colspan="8" class="text-center py-5">
                            <div class="text-muted mb-2"><i class="bi bi-calendar-x fs-1"></i></div>
                            <h6 class="text-dark">${T.noShiftsTitle}</h6>
                            <p class="text-muted small mb-0">${T.noShiftsDesc}</p>
                        </td>
                    </tr>
                `;
            } else {
                S.shiftsList.forEach(shift => {
                    tbody += `<tr>`;

                    // Sticky Left Column: Shift Name & Working Hours
                    tbody += `
                        <td class="shift-name-col">
                            <div class="shift-info-card">
                                <div class="d-flex align-items-center gap-2 mb-1">
                                    <span class="shift-color-bar"></span>
                                    <strong class="shift-name-text text-truncate" title="${escapeHtml(shift.name)}">${escapeHtml(shift.name)}</strong>
                                </div>
                                <div class="shift-time-text">
                                    <i class="bi bi-clock me-1 text-muted"></i>${shift.start} – ${shift.end}
                                </div>
                                ${shift.hours ? `<div class="shift-hours-text"><i class="bi bi-hourglass-split me-1 text-muted"></i>${T.workHours.replace('{0}', shift.hours.toFixed(1))}</div>` : ''}
                            </div>
                        </td>
                    `;

                    // Day Columns
                    let tempDate = new Date(S.currentWeekStart);
                    for (let i = 0; i < 7; i++) {
                        let cellDateStr = getISODateString(tempDate);
                        let cellDateObj = new Date(tempDate);
                        cellDateObj.setHours(0, 0, 0, 0);

                        let dayEvent = events.find(e => {
                            let eDate = e.start.split('T')[0];
                            return e.shiftId == shift.id && eDate === cellDateStr;
                        });

                        let isPast = cellDateObj < today;
                        let isUnavailable = cellDateStr < shift.from || (shift.to && cellDateStr > shift.to);

                        if (dayEvent) {
                            let isApproved = (dayEvent.statusId === 1 || dayEvent.statusName === "Approved");
                            let isRejected = (dayEvent.statusId === 2 || dayEvent.statusName === "Rejected");
                            let eJson = JSON.stringify(dayEvent).replace(/'/g, "&#39;");

                            if (isApproved) {
                                tbody += `
                                    <td class="shift-grid-cell">
                                        <div class="shift-slot is-approved" onclick='event.stopPropagation(); openViewModal(${eJson})' title="${T.slotApprovedTitle}">
                                            <div class="d-flex justify-content-between align-items-center mb-1">
                                                <span class="badge-status status-approved">
                                                    <i class="bi bi-check-circle-fill"></i> ${T.approved}
                                                </span>
                                                <i class="bi bi-info-circle text-success small btn-view-slot" title="${T.viewDetails}"></i>
                                            </div>
                                            <div class="slot-time-text">${shift.start} – ${shift.end}</div>
                                            <input type="checkbox" class="form-check-input weekly-shift-cb d-none" 
                                                value="${shift.id}_${cellDateStr}" checked disabled>
                                        </div>
                                    </td>
                                `;
                            } else if (isRejected) {
                                tbody += `
                                    <td class="shift-grid-cell">
                                        <div class="shift-slot is-rejected" onclick='event.stopPropagation(); openViewModal(${eJson})' title="${T.slotRejectedTitle}">
                                            <div class="d-flex justify-content-between align-items-center mb-1">
                                                <span class="badge-status status-rejected">
                                                    <i class="bi bi-x-circle-fill"></i> ${T.rejected}
                                                </span>
                                                <i class="bi bi-info-circle text-danger small btn-view-slot" title="${T.viewDetails}"></i>
                                            </div>
                                            <div class="slot-time-text">${shift.start} – ${shift.end}</div>
                                            <input type="checkbox" class="form-check-input weekly-shift-cb d-none" 
                                                value="${shift.id}_${cellDateStr}">
                                        </div>
                                    </td>
                                `;
                            } else {
                                // Pending (ca đã đăng ký nhưng nay không còn phù hợp thời gian có thể làm việc -> hiện cảnh báo, vẫn cho bỏ chọn)
                                let pendingBlock = S.blockedSlots[`${shift.id}_${cellDateStr}`];
                                let pendingWarn = pendingBlock ? `<i class="bi bi-exclamation-triangle-fill text-danger small ms-1" title="${escAttr(T.blockedPendingHint.replace('{0}', pendingBlock))}"></i>` : '';
                                tbody += `
                                    <td class="shift-grid-cell">
                                        <div class="shift-slot is-pending was-pending" title="${T.slotPendingTitle}">
                                            <div class="d-flex justify-content-between align-items-center mb-1">
                                                <div class="form-check mb-0 p-0 d-flex align-items-center">
                                                    <input type="checkbox" class="form-check-input weekly-shift-cb m-0 me-2" 
                                                        id="cb_${shift.id}_${cellDateStr}"
                                                        value="${shift.id}_${cellDateStr}" checked>
                                                    <label class="form-check-label badge-status status-pending m-0 cursor-pointer" for="cb_${shift.id}_${cellDateStr}">
                                                        <i class="bi bi-hourglass-split"></i> ${T.pending}
                                                    </label>
                                                </div>
                                                <span class="d-inline-flex align-items-center">${pendingWarn}<i class="bi bi-info-circle text-warning-emphasis small btn-view-slot ms-1" title="${T.details}" onclick='event.stopPropagation(); openViewModal(${eJson})'></i></span>
                                            </div>
                                            <div class="slot-time-text">${shift.start} – ${shift.end}</div>
                                        </div>
                                    </td>
                                `;
                            }
                        } else if (isPast) {
                            tbody += `
                                <td class="shift-grid-cell">
                                    <div class="shift-slot is-past" title="${T.slotPastTitle}">
                                        <div class="d-flex justify-content-between align-items-center mb-1">
                                            <span class="badge-status status-past">
                                                <i class="bi bi-lock-fill"></i> ${T.overdue}
                                            </span>
                                        </div>
                                        <div class="slot-time-text text-muted">${shift.start} – ${shift.end}</div>
                                        <input type="checkbox" class="form-check-input weekly-shift-cb d-none" 
                                            value="${shift.id}_${cellDateStr}" disabled>
                                    </div>
                                </td>
                            `;
                        } else if (isUnavailable) {
                            tbody += `
                                <td class="shift-grid-cell">
                                    <div class="shift-slot is-unavailable" title="${T.slotUnavailableTitle}">
                                        <div class="d-flex justify-content-between align-items-center mb-1">
                                            <span class="badge-status status-unavailable">
                                                <i class="bi bi-dash-circle"></i> ${T.notApplicable}
                                            </span>
                                        </div>
                                        <div class="slot-time-text text-muted">${shift.start} – ${shift.end}</div>
                                        <input type="checkbox" class="form-check-input weekly-shift-cb d-none" 
                                            value="${shift.id}_${cellDateStr}" disabled>
                                    </div>
                                </td>
                            `;
                        } else if (S.blockedSlots[`${shift.id}_${cellDateStr}`]) {
                            // Không phù hợp thời gian có thể làm việc (ngày không thể làm / ngoài khung giờ đã duyệt)
                            let reason = escAttr(S.blockedSlots[`${shift.id}_${cellDateStr}`]);
                            tbody += `
                                <td class="shift-grid-cell">
                                    <div class="shift-slot is-unavailable is-blocked" title="${reason}">
                                        <div class="d-flex justify-content-between align-items-center mb-1">
                                            <span class="badge-status status-unavailable">
                                                <i class="bi bi-slash-circle"></i> ${T.blockedBadge}
                                            </span>
                                        </div>
                                        <div class="slot-time-text text-muted">${shift.start} – ${shift.end}</div>
                                        <input type="checkbox" class="form-check-input weekly-shift-cb d-none"
                                            value="${shift.id}_${cellDateStr}" disabled>
                                    </div>
                                </td>
                            `;
                        } else {
                            // Available slot
                            tbody += `
                                <td class="shift-grid-cell">
                                    <div class="shift-slot is-available" title="${T.slotAvailableTitle}">
                                        <div class="d-flex justify-content-between align-items-center mb-1">
                                            <div class="form-check mb-0 p-0 d-flex align-items-center">
                                                <input type="checkbox" class="form-check-input weekly-shift-cb m-0 me-2" 
                                                    id="cb_${shift.id}_${cellDateStr}"
                                                    value="${shift.id}_${cellDateStr}">
                                                <label class="form-check-label badge-status status-available m-0 cursor-pointer" for="cb_${shift.id}_${cellDateStr}">
                                                    <i class="bi bi-circle"></i> ${T.notRegistered}
                                                </label>
                                            </div>
                                        </div>
                                        <div class="slot-time-text">${shift.start} – ${shift.end}</div>
                                    </div>
                                </td>
                            `;
                        }
                        tempDate.setDate(tempDate.getDate() + 1);
                    }
                    tbody += `</tr>`;
                });
            }
        }

        $('#gridBody').html(tbody);
    }

    app.initGrid = initGrid;
    app.renderHeader = renderHeader;
    app.renderGrid = renderGrid;
    app.updateSlotCardVisual = updateSlotCardVisual;
    app.updateWeeklySummary = updateWeeklySummary;
    window.filterEmployeesLocally = filterEmployeesLocally;
})(jQuery, window.RWPM.shiftRegistrationIndex);
