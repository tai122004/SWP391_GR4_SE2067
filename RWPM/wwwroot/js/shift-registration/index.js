// Views/ShiftRegistration/Index.cshtml — chuyển chế độ xem, tải sự kiện, đồng bộ đăng ký tuần, sao chép tuần trước, khởi tạo trang.
// Thứ tự nạp: core.js, grid.js, coverage.js, week-picker.js, modals.js, index.js (cùng dùng window.RWPM.shiftRegistration).
// Cấu hình: window.RWPM.shiftRegistrationIndex = { isManager, coverageStoreId, shifts, employees, urls: { availableShifts }, i18n: {...} }
(function ($, cfg) {
    'use strict';
    var app = window.RWPM.shiftRegistration;
    var S = app.state, T = app.T, L = cfg.i18n;
    var escapeHtml = app.escapeHtml, getISODateString = app.getISODateString, ajaxErrorText = app.ajaxErrorText, showAppToast = app.showAppToast;

    function switchViewMode(mode) {
        S.activeMode = mode;
        if (mode === 'my') {
            $('#tabMyRegistration').addClass('active').attr('aria-selected', 'true');
            $('#tabManagerSchedule').removeClass('active').attr('aria-selected', 'false');
            $('#managerFilterEmployeeContainer').hide();
            $('#managerActionBtns').hide();
            $('.weekly-summary-bar').show();
        } else {
            $('#tabMyRegistration').removeClass('active').attr('aria-selected', 'false');
            $('#tabManagerSchedule').addClass('active').attr('aria-selected', 'true');
            $('#managerFilterEmployeeContainer').show();
            $('#managerActionBtns').show();
            $('.weekly-summary-bar').hide();
        }
        app.renderHeader();
        fetchEvents();
    }

    function changeStoreFilter(storeId) {
        window.location.href = '/ShiftRegistration?storeId=' + storeId;
    }

    document.addEventListener('DOMContentLoaded', function() {
        if (cfg.isManager) {
            switchViewMode(S.activeMode);
        } else {
            app.updateWeekDisplay();
            fetchEvents();
        }

        // Week Navigation
        $('#btnPrevWeek').click(function() {
            S.currentWeekStart.setDate(S.currentWeekStart.getDate() - 7);
            app.updateWeekDisplay();
            fetchEvents();
        });

        $('#btnNextWeek').click(function() {
            S.currentWeekStart.setDate(S.currentWeekStart.getDate() + 7);
            app.updateWeekDisplay();
            fetchEvents();
        });

        $('#btnTodayWeek').click(function() {
            let now = new Date();
            let day = now.getDay();
            let diff = now.getDate() - day + (day === 0 ? -6 : 1);
            S.currentWeekStart = new Date(now.setDate(diff));
            S.currentWeekStart.setHours(0, 0, 0, 0);
            app.updateWeekDisplay();
            fetchEvents();
        });

        $('#filterEmployeeId').change(function() {
            fetchEvents();
        });

        // Single day shift registration form + modal checkboxes
        app.initModals();

        // Weekly checkbox toggle & slot click handlers
        app.initGrid();

        // Week Picker Popover logic
        app.initWeekPicker();
    });

    function fetchEvents() {
        let isMyMode = (S.activeMode === 'my');
        app.loadCoverage();
        let empId = isMyMode ? '' : ($('#filterEmployeeId').val() || '');
        let startStr = getISODateString(S.currentWeekStart);
        let endStr = new Date(S.currentWeekStart);
        endStr.setDate(endStr.getDate() + 7);

        $('#iconRefresh').addClass('spin-animation');

        $.ajax({
            url: '/ShiftRegistration/GetEvents',
            type: 'GET',
            data: {
                start: startStr,
                end: getISODateString(endStr),
                employeeId: empId,
                myOnly: isMyMode,
                withAvailability: isMyMode
            },
            success: function(res) {
                // withAvailability -> { events, blocked }; ngược lại -> mảng sự kiện
                let events = Array.isArray(res) ? res : ((res && res.events) || []);
                S.blockedSlots = (!Array.isArray(res) && res && res.blocked) ? res.blocked : {};
                app.renderGrid(events);
                app.updateWeeklySummary();
            },
            error: function() {
                showAppToast('error', L.shiftErrLoadShift);
            },
            complete: function() {
                $('#iconRefresh').removeClass('spin-animation');
            }
        });
    }

    function syncWeeklySchedule() {
        var token = $('input[name="__RequestVerificationToken"]').val();
        var registrations = [];

        $('.weekly-shift-cb:checked:not(:disabled)').each(function() {
            var parts = $(this).val().split('_');
            registrations.push({
                EmployeeId: 0,
                ShiftId: parseInt(parts[0]),
                WorkDate: parts[1],
                Note: ""
            });
        });

        // Retain approved shifts so they aren't removed by sync
        $('.weekly-shift-cb:checked:disabled').each(function() {
            var parts = $(this).val().split('_');
            registrations.push({
                EmployeeId: 0,
                ShiftId: parseInt(parts[0]),
                WorkDate: parts[1],
                Note: ""
            });
        });

        var data = {
            StartOfWeek: getISODateString(S.currentWeekStart),
            Registrations: registrations
        };

        var btn = $('#btnSubmitWeekly');
        var originalBtnHtml = btn.html();
        btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1" role="status"></span> ' + escapeHtml(L.btnSending));

        $.ajax({
            url: '/ShiftRegistration/SyncWeekly',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(data),
            headers: { 'RequestVerificationToken': token },
            success: function(res) {
                if (res.success) {
                    showAppToast('success', L.shiftRegSuccess);
                    fetchEvents();
                } else {
                    showAppToast('error', res.message || T.errOccurred);
                }
            },
            error: function(err) {
                showAppToast('error', T.errPrefix + ajaxErrorText(err));
            },
            complete: function() {
                btn.prop('disabled', false).html(originalBtnHtml);
            }
        });
    }

    function copyPreviousWeek() {
        let prevWeekStart = new Date(S.currentWeekStart);
        prevWeekStart.setDate(prevWeekStart.getDate() - 7);

        let startStr = getISODateString(prevWeekStart);
        let endStr = new Date(prevWeekStart);
        endStr.setDate(endStr.getDate() + 7);

        $.ajax({
            url: '/ShiftRegistration/GetEvents',
            type: 'GET',
            data: {
                start: startStr,
                end: getISODateString(endStr),
                employeeId: '',
                myOnly: true
            },
            success: function(events) {
                if (events.length === 0) {
                    showAppToast('warning', L.shiftErrNoPrevShifts);
                    return;
                }

                $('.weekly-shift-cb:not(:disabled)').prop('checked', false);
                let today = new Date();
                today.setHours(0,0,0,0);
                let checkedCount = 0;

                events.forEach(e => {
                    let eDate = new Date(e.start.split('T')[0]);
                    eDate.setDate(eDate.getDate() + 7);
                    let targetDateStr = getISODateString(eDate);

                    if (eDate >= today) {
                        let cb = $(`.weekly-shift-cb[value="${e.shiftId}_${targetDateStr}"]`);
                        if (cb.length > 0 && !cb.prop('disabled')) {
                            cb.prop('checked', true).trigger('change');
                            checkedCount++;
                        }
                    }
                });

                showAppToast('success', L.toastCopiedFormat.replace('{0}', checkedCount));
            },
            error: function() {
                showAppToast('error', L.shiftErrLoadPrev);
            }
        });
    }

    window.switchViewMode = switchViewMode;
    window.changeStoreFilter = changeStoreFilter;
    window.fetchEvents = fetchEvents;
    window.syncWeeklySchedule = syncWeeklySchedule;
    window.copyPreviousWeek = copyPreviousWeek;
})(jQuery, window.RWPM.shiftRegistrationIndex);
