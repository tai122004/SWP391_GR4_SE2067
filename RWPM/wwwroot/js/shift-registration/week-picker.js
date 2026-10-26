// Views/ShiftRegistration/Index.cshtml — hiển thị tuần hiện tại và lịch chọn tuần (popover).
// Thứ tự nạp: core.js, grid.js, coverage.js, week-picker.js, modals.js, index.js (cùng dùng window.RWPM.shiftRegistration).
// Cấu hình: window.RWPM.shiftRegistrationIndex = { isManager, coverageStoreId, shifts, employees, urls: { availableShifts }, i18n: {...} }
(function ($, cfg) {
    'use strict';
    var app = window.RWPM.shiftRegistration;
    var S = app.state, T = app.T, L = cfg.i18n;

    function initWeekPicker() {
        // Week Picker Popover logic
        $('#btnToggleWeekPicker').click(function(e) {
            e.stopPropagation();
            $('#weekPickerDropdown').toggle();
            if ($('#weekPickerDropdown').is(':visible')) {
                S.wpCurrentMonth = new Date(S.currentWeekStart);
                S.wpCurrentMonth.setDate(1);
                renderWeekPickerCalendar();
            }
        });

        $(document).click(function(e) {
            if (!$(e.target).closest('#btnToggleWeekPicker, #weekPickerDropdown').length) {
                $('#weekPickerDropdown').hide();
            }
        });

        $('.wp-prev-month').click(function(e){ e.stopPropagation(); S.wpCurrentMonth.setMonth(S.wpCurrentMonth.getMonth() - 1); renderWeekPickerCalendar(); });
        $('.wp-next-month').click(function(e){ e.stopPropagation(); S.wpCurrentMonth.setMonth(S.wpCurrentMonth.getMonth() + 1); renderWeekPickerCalendar(); });
        $('.wp-prev-year').click(function(e){ e.stopPropagation(); S.wpCurrentMonth.setFullYear(S.wpCurrentMonth.getFullYear() - 1); renderWeekPickerCalendar(); });
        $('.wp-next-year').click(function(e){ e.stopPropagation(); S.wpCurrentMonth.setFullYear(S.wpCurrentMonth.getFullYear() + 1); renderWeekPickerCalendar(); });
    }

    function renderWeekPickerCalendar() {
        $('#wpMonthYearDisplay').text(L.pickerMonthYearFormat.replace('{0}', S.wpCurrentMonth.getMonth() + 1).replace('{1}', S.wpCurrentMonth.getFullYear()));

        let firstDay = new Date(S.wpCurrentMonth.getFullYear(), S.wpCurrentMonth.getMonth(), 1);
        let dayOfWeek = firstDay.getDay();
        let diffToMonday = dayOfWeek === 0 ? -6 : 1 - dayOfWeek;
        let startDate = new Date(firstDay);
        startDate.setDate(firstDay.getDate() + diffToMonday);

        let tbody = '';

        for (let w = 0; w < 6; w++) {
            let rowMonday = new Date(startDate);
            let firstDayOfYear = new Date(rowMonday.getFullYear(), 0, 1);
            let pastDaysOfYear = (rowMonday - firstDayOfYear) / 86400000;
            let weekNum = Math.ceil((pastDaysOfYear + firstDayOfYear.getDay() + 1) / 7);
            let isActive = (rowMonday.getTime() === S.currentWeekStart.getTime()) ? 'active' : '';

            tbody += `<tr class="wp-week-row ${isActive}" data-monday="${rowMonday.toISOString()}">`;
            tbody += `<td class="wp-week-num">${weekNum}</td>`;

            for (let d = 0; d < 7; d++) {
                let cellDate = new Date(startDate);
                let isCurrentMonth = cellDate.getMonth() === S.wpCurrentMonth.getMonth();
                let opacity = isCurrentMonth ? '1' : '0.35';
                tbody += `<td style="opacity: ${opacity}">${cellDate.getDate()}</td>`;
                startDate.setDate(startDate.getDate() + 1);
            }
            tbody += `</tr>`;
        }

        $('#wpCalendarBody').html(tbody);

        $('.wp-week-row').click(function(e) {
            e.stopPropagation();
            let monIso = $(this).data('monday');
            S.currentWeekStart = new Date(monIso);
            updateWeekDisplay();
            fetchEvents();
            $('#weekPickerDropdown').hide();
        });
    }

    function updateWeekDisplay() {
        let weekEnd = new Date(S.currentWeekStart);
        weekEnd.setDate(S.currentWeekStart.getDate() + 6);

        let firstDayOfYear = new Date(S.currentWeekStart.getFullYear(), 0, 1);
        let pastDaysOfYear = (S.currentWeekStart - firstDayOfYear) / 86400000;
        let weekNum = Math.ceil((pastDaysOfYear + firstDayOfYear.getDay() + 1) / 7);

        let startStr = S.currentWeekStart.getDate().toString().padStart(2, '0') + '/' + (S.currentWeekStart.getMonth() + 1).toString().padStart(2, '0');
        let endStr = weekEnd.getDate().toString().padStart(2, '0') + '/' + (weekEnd.getMonth() + 1).toString().padStart(2, '0') + '/' + weekEnd.getFullYear();

        $('#currentWeekDisplay').text(L.weekFormat.replace('{0}', weekNum));
        $('#currentWeekRangeDisplay').text(`(${startStr} – ${endStr})`);

        app.renderHeader();
    }

    app.initWeekPicker = initWeekPicker;
    app.updateWeekDisplay = updateWeekDisplay;
})(jQuery, window.RWPM.shiftRegistrationIndex);
