using RWPM.Common.Validation;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RWPM.Common;
using RWPM.Common.Enums;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.Payroll;
using RWPM.Services.Abstraction;
using PayrollResource = RWPM.Resources.Shared.PayrollResource;

namespace RWPM.Services.Implementation
{
    public class PayrollService : IPayrollService
    {
        private readonly DefaultDatabaseContext _context;

        public PayrollService(DefaultDatabaseContext context)
        {
            _context = context;
        }

        public async Task<List<Payroll>> GetPayrollsAsync(int? year = null, int? storeId = null)
        {
            var query = _context.Payroll.AsQueryable();
            if (year.HasValue)
            {
                query = query.Where(p => p.PeriodYear == year.Value);
            }

            if (storeId.HasValue && storeId.Value > 0)
            {
                var payrolls = await query
                    .Include(p => p.Details)
                    .OrderByDescending(p => p.StartDate)
                    .ToListAsync();

                foreach (var p in payrolls)
                {
                    var storeDetails = p.Details.Where(d => d.StoreId == storeId.Value).ToList();
                    p.TotalEmployees = storeDetails.Count;
                    p.TotalWorkHours = Math.Round(storeDetails.Sum(d => d.TotalWorkHours + d.PaidLeaveHours), 2);
                    p.TotalGrossSalary = storeDetails.Sum(d => d.TotalSalary);
                }

                return payrolls;
            }

            return await query.OrderByDescending(p => p.StartDate)
                              .ToListAsync();
        }

        public async Task<Payroll?> GetPayrollByIdAsync(int id)
        {
            return await _context.Payroll.AsNoTracking()
                .Include(p => p.Details)
                    .ThenInclude(d => d.Employee)
                .Include(p => p.Details)
                    .ThenInclude(d => d.Store)
                .FirstOrDefaultAsync(p => p.PayrollId == id);
        }

        public async Task<PayrollDetailVM> GetPayrollDetailAsync(int payrollId, int? storeId = null, string? search = null, PayrollDetailStatus? status = null)
        {
            var payroll = await _context.Payroll.FirstOrDefaultAsync(p => p.PayrollId == payrollId);
            if (payroll == null)
            {
                throw new KeyNotFoundException(string.Format(PayrollResource.Err_PayrollNotFoundId, payrollId));
            }

            var query = _context.PayrollDetail
                .Include(d => d.Employee)
                    .ThenInclude(e => e.Account)
                .Include(d => d.Store)
                .Include(d => d.ItemSnapshots)
                .Where(d => d.PayrollId == payrollId);

            if (storeId.HasValue && storeId.Value > 0)
            {
                query = query.Where(d => d.StoreId == storeId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(d => d.EmployeeCode.ToLower().Contains(s) || d.EmployeeName.ToLower().Contains(s));
            }

            if (status.HasValue)
            {
                query = query.Where(d => d.Status == status.Value);
            }

            var details = await query.OrderBy(d => d.StoreId).ThenBy(d => d.EmployeeCode).ToListAsync();
            var stores = await _context.Store.OrderBy(s => s.StoreName).ToListAsync();

            return new PayrollDetailVM
            {
                Payroll = payroll,
                Details = details,
                SelectedStoreId = storeId,
                Search = search,
                StatusFilter = status,
                Stores = stores
            };
        }

        // =====================================================================
        // KỲ LƯƠNG LIÊN TỤC
        // Mỗi kỳ lương mới phải bắt đầu đúng ngày sau ngày kết thúc của kỳ trước
        // -> không có ngày công nào bị bỏ sót giữa 2 kỳ (VD: kỳ 10/09 - 05/10 thì kỳ sau bắt buộc từ 06/10).
        // =====================================================================

        /// <summary>Một kỳ lương dài tối đa 1 tháng: [start, start + 1 tháng - 1 ngày].</summary>
        private static DateTime GetMaxEndDate(DateTime startDate) => startDate.Date.AddMonths(1).AddDays(-1);

        private static List<PayrollGapVM> FindGaps(List<(DateTime Start, DateTime End)> periods)
        {
            var gaps = new List<PayrollGapVM>();
            var ordered = periods.OrderBy(p => p.Start).ToList();
            for (int i = 1; i < ordered.Count; i++)
            {
                var expectedStart = ordered[i - 1].End.AddDays(1);
                if (ordered[i].Start > expectedStart)
                {
                    var gapEnd = ordered[i].Start.AddDays(-1);
                    var maxEnd = GetMaxEndDate(expectedStart);
                    gaps.Add(new PayrollGapVM
                    {
                        StartDate = expectedStart,
                        EndDate = gapEnd,
                        MaxEndDate = maxEnd < gapEnd ? maxEnd : gapEnd
                    });
                }
            }
            return gaps;
        }

        public async Task<PayrollPeriodSuggestionVM> GetNextPeriodSuggestionAsync()
        {
            var today = DateTime.Today;
            var periods = await _context.Payroll
                .Select(p => new { p.StartDate, p.EndDate, p.Title })
                .ToListAsync();

            var last = periods.OrderByDescending(p => p.EndDate).FirstOrDefault();
            var vm = new PayrollPeriodSuggestionVM
            {
                Gaps = FindGaps(periods.Select(p => (p.StartDate.Date, p.EndDate.Date)).ToList())
            };

            if (last != null)
            {
                vm.IsStartLocked = true;
                vm.LastEndDate = last.EndDate.Date;
                vm.LastTitle = last.Title;
                vm.SuggestedStartDate = last.EndDate.Date.AddDays(1);
            }
            else
            {
                // Chưa có kỳ lương nào: gợi ý ngày 01 của tháng hiện tại, HR được phép chọn lại
                vm.SuggestedStartDate = new DateTime(today.Year, today.Month, 1);
            }

            var maxEnd = GetMaxEndDate(vm.SuggestedStartDate);
            vm.MaxEndDate = maxEnd < today ? maxEnd : today;
            vm.SuggestedEndDate = vm.MaxEndDate;
            return vm;
        }

        /// <summary>
        /// Validate một kỳ lương MỚI (không áp dụng khi tính lại kỳ đã tồn tại).
        /// </summary>
        private async Task ValidateNewPeriodAsync(DateTime startDate, DateTime endDate)
        {
            var today = DateTime.Today;

            if (endDate > today)
                throw new InvalidOperationException(
                    string.Format(PayrollResource.Err_EndAfterToday, endDate.ToString("dd/MM/yyyy"), today.ToString("dd/MM/yyyy")));

            var maxEnd = GetMaxEndDate(startDate);
            if (endDate > maxEnd)
                throw new InvalidOperationException(
                    string.Format(PayrollResource.Err_MaxOneMonth, startDate.ToString("dd/MM/yyyy"), maxEnd.ToString("dd/MM/yyyy")));

            var periods = await _context.Payroll
                .Select(p => new { p.StartDate, p.EndDate, p.Title })
                .ToListAsync();

            // Kỳ liền trước (kết thúc trước ngày bắt đầu) phải nối tiếp đúng 1 ngày
            var previous = periods.Where(p => p.EndDate < startDate).OrderByDescending(p => p.EndDate).FirstOrDefault();
            if (previous != null)
            {
                var requiredStart = previous.EndDate.Date.AddDays(1);
                if (startDate != requiredStart)
                {
                    throw new InvalidOperationException(
                        string.Format(PayrollResource.Err_MustStartFrom,
                            requiredStart.ToString("dd/MM/yyyy"),
                            PayrollDisplayText.Title(previous.Title),
                            startDate.ToString("dd/MM/yyyy"),
                            startDate.AddDays(-1).ToString("dd/MM/yyyy")));
                }
                return;
            }

            // Chưa có kỳ trước nhưng có kỳ sau (tính bổ sung giai đoạn trước kỳ đầu tiên): phải kết thúc sát kỳ sau
            var next = periods.Where(p => p.StartDate > endDate).OrderBy(p => p.StartDate).FirstOrDefault();
            if (next != null)
            {
                var requiredEnd = next.StartDate.Date.AddDays(-1);
                if (endDate != requiredEnd)
                {
                    throw new InvalidOperationException(
                        string.Format(PayrollResource.Err_MustEndBefore, PayrollDisplayText.Title(next.Title), requiredEnd.ToString("dd/MM/yyyy")));
                }
            }
        }

        public async Task<Payroll> CalculatePayrollAsync(DateTime startDate, DateTime endDate, string operatorUsername)
        {
            await using var calculationTransaction = _context.Database.CurrentTransaction == null
                ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;
            startDate = startDate.Date;
            endDate = endDate.Date;
            if (startDate == DateTime.MinValue || endDate == DateTime.MinValue)
                throw new ArgumentException(PayrollResource.Err_SelectBothDates);
            if (startDate > endDate)
                throw new ArgumentException(PayrollResource.Err_StartAfterEnd);

            var existing = await _context.Payroll
                .Include(p => p.Details)
                    .ThenInclude(d => d.ItemSnapshots)
                .FirstOrDefaultAsync(p => p.StartDate == startDate && p.EndDate == endDate);

            var overlapping = await _context.Payroll
                .Where(p => p.StartDate <= endDate && p.EndDate >= startDate)
                .Where(p => existing == null || p.PayrollId != existing.PayrollId)
                .Select(p => new { p.StartDate, p.EndDate })
                .FirstOrDefaultAsync();

            if (overlapping != null)
            {
                throw new InvalidOperationException(
                    string.Format(PayrollResource.Err_Overlap, overlapping.StartDate.ToString("dd/MM/yyyy"), overlapping.EndDate.ToString("dd/MM/yyyy")));
            }

            if (existing != null && existing.Status == PayrollStatus.Locked)
            {
                throw new InvalidOperationException(string.Format(PayrollResource.Err_AlreadyLocked, startDate.ToString("dd/MM/yyyy"), endDate.ToString("dd/MM/yyyy")));
            }

            // Kỳ mới -> bắt buộc nối tiếp kỳ trước, không vượt quá hôm nay, tối đa 1 tháng
            if (existing == null)
            {
                await ValidateNewPeriodAsync(startDate, endDate);
            }

            var periodMonth = startDate.Month;
            var periodYear = startDate.Year;

            // Load dữ liệu hoạt động trong kỳ
            var attendanceRecords = await _context.AttendanceRecord
                .Include(a => a.Shift)
                .Where(a => a.Date >= startDate && a.Date <= endDate)
                .ToListAsync();

            var registrations = await _context.ShiftRegistration
                .Include(sr => sr.Shift)
                .Where(sr => sr.WorkDate >= startDate && sr.WorkDate <= endDate && sr.Status == RegistrationStatus.Approved)
                .ToListAsync();

            var leaves = await _context.LeaveRequest
                .Where(l => l.StartDate <= endDate && l.EndDate >= startDate && l.Status == RegistrationStatus.Approved)
                .ToListAsync();

            // Tập hợp nhân viên có hoạt động trong kỳ
            var empUsernamesWithAttendance = attendanceRecords.Select(a => a.Username).Distinct().ToHashSet();
            var empIdsWithRegistrations = registrations.Select(r => r.EmployeeId).Distinct().ToHashSet();
            var empIdsWithLeaves = leaves.Select(l => l.EmployeeId).Distinct().ToHashSet();

            // Chọn nhân viên:
            // 1. Đang làm việc hoặc thời gian làm việc giao thoa với kỳ lương (kể cả đã nghỉ việc giữa kỳ)
            // 2. HOẶC có phát sinh giờ công, ca đăng ký, hay đơn nghỉ trong kỳ
            var employees = await _context.Employee
                .Include(e => e.Account)
                .Include(e => e.Store)
                .Where(e => (e.JoinDate <= endDate && (e.ResignDate == null || e.ResignDate >= startDate))
                         || empUsernamesWithAttendance.Contains(e.Username)
                         || empIdsWithRegistrations.Contains(e.EmployeeId)
                         || empIdsWithLeaves.Contains(e.EmployeeId))
                .ToListAsync();

            Payroll payroll;
            bool isRecalculate = false;
            if (existing != null)
            {
                payroll = existing;
                _context.PayrollDetail.RemoveRange(existing.Details);
                payroll.Details.Clear();
                isRecalculate = true;
            }
            else
            {
                payroll = new Payroll
                {
                    PeriodMonth = periodMonth,
                    PeriodYear = periodYear,
                    Title = PayrollDisplayText.StoredTitle(startDate, endDate), // persisted; translated at display time
                    StartDate = startDate,
                    EndDate = endDate,
                    Status = PayrollStatus.Draft,
                    CreatedBy = operatorUsername,
                    CreatedDate = DateTime.UtcNow
                };
                _context.Payroll.Add(payroll);
            }

            payroll.UpdatedBy = operatorUsername;
            payroll.UpdatedDate = DateTime.UtcNow;

            var detailsList = new List<PayrollDetail>();

            foreach (var emp in employees)
            {
                bool isEligibleForPaidLeave = (emp.EmploymentType == EmploymentType.FullTime && emp.Status == EmploymentStatus.Official);

                var empAttendance = attendanceRecords
                    .Where(a => a.Username == emp.Username)
                    .GroupBy(a => a.AttendanceId)
                    .Select(g => g.First())
                    .OrderBy(a => a.Date)
                    .ToList();

                var empRegistrations = registrations
                    .Where(r => r.EmployeeId == emp.EmployeeId)
                    .OrderBy(r => r.WorkDate)
                    .ToList();

                var empLeaves = leaves
                    .Where(l => l.EmployeeId == emp.EmployeeId)
                    .ToList();

                double actualWorkHours = 0;
                double paidLeaveHours = 0;
                double leaveScheduledHours = 0;
                int paidLeaveDaysCount = 0;
                int unpaidLeaveDaysCount = 0;
                int missingAttendanceCount = 0;
                var missingDates = new List<string>();
                var itemSnapshots = new List<PayrollItemSnapshot>();

                decimal hourlyRate = emp.HourlyRate.HasValue && emp.HourlyRate.Value > 0 ? emp.HourlyRate.Value : 0;

                // 1. Xử lý các bản ghi chấm công thực tế
                foreach (var ar in empAttendance)
                {
                    double hours = ar.WorkHours ?? 0;
                    actualWorkHours += hours;

                    string statusStr = PayrollDisplayText.SnapValid;
                    if (!ar.WorkHours.HasValue)
                    {
                        missingAttendanceCount++;
                        missingDates.Add($"{ar.Date:dd/MM} {PayrollDisplayText.MissingInOutTag}");
                        statusStr = PayrollDisplayText.SnapMissingInOut;
                    }
                    else
                    {
                        bool isLate = ar.Shift != null && ar.CheckInTime.HasValue && (ar.CheckInTime.Value > ar.Shift.StartTime.Add(TimeSpan.FromMinutes(ar.Shift.GracePeriodMinutes ?? RWPM.Common.Constants.ShiftDefaults.GracePeriodMinutes)));
                        bool isEarly = ar.Shift != null && ar.CheckOutTime.HasValue && (ar.CheckOutTime.Value < ar.Shift.EndTime.Subtract(TimeSpan.FromMinutes(ar.Shift.EarlyCheckOutMinutes ?? RWPM.Common.Constants.ShiftDefaults.EarlyCheckOutMinutes)));

                        if (isLate && isEarly)
                        {
                            statusStr = PayrollDisplayText.SnapLateAndEarly;
                        }
                        else if (isLate)
                        {
                            statusStr = PayrollDisplayText.SnapLate;
                        }
                        else if (isEarly)
                        {
                            statusStr = PayrollDisplayText.SnapEarly;
                        }
                    }

                    var snapshot = new PayrollItemSnapshot
                    {
                        Date = ar.Date,
                        ShiftId = ar.ShiftId,
                        ShiftName = ar.Shift?.ShiftName ?? PayrollDisplayText.ShiftUnknown,
                        StartTime = ar.Shift?.StartTime,
                        EndTime = ar.Shift?.EndTime,
                        CheckInTime = ar.CheckInTime,
                        CheckOutTime = ar.CheckOutTime,
                        ActualWorkHours = Math.Round(hours, 2),
                        PaidLeaveHours = 0,
                        TotalPayableHours = Math.Round(hours, 2),
                        HourlyRate = hourlyRate,
                        TotalAmount = hourlyRate > 0 ? Math.Round((decimal)hours * hourlyRate, 0) : 0,
                        SourceType = "Attendance",
                        Status = statusStr,
                        Note = ar.WorkHours.HasValue ? null : PayrollDisplayText.NoteAttendanceIncomplete
                    };

                    itemSnapshots.Add(snapshot);
                }

                // 2. Đối chiếu với ca đã duyệt (Tìm ca thiếu chấm công hoặc ca nghỉ phép)
                foreach (var reg in empRegistrations)
                {
                    // Kiểm tra xem ca này đã có bản ghi chấm công chưa
                    bool hasAttendance = empAttendance.Any(a => a.Date.Date == reg.WorkDate.Date && (a.ShiftId == reg.ShiftId || arMatchesShift(a, reg)));
                    if (hasAttendance)
                    {
                        continue; // Đã xử lý ở mục chấm công
                    }

                    // Ca chưa có chấm công -> Kiểm tra xem ngày này có đơn nghỉ phép đã duyệt không
                    var matchedLeave = empLeaves.FirstOrDefault(l => reg.WorkDate.Date >= l.StartDate.Date && reg.WorkDate.Date <= l.EndDate.Date);

                    double shiftScheduledHours = reg.Shift != null ? reg.Shift.ScheduledWorkHours : 0;
                    if (shiftScheduledHours <= 0 && reg.Shift != null)
                    {
                        var span = reg.Shift.EndTime - reg.Shift.StartTime - TimeSpan.FromMinutes(reg.Shift.BreakMinutes);
                        shiftScheduledHours = span.TotalHours > 0 ? span.TotalHours : 0;
                    }

                    if (matchedLeave != null)
                    {
                        if (matchedLeave.LeaveType == LeaveType.Paid && isEligibleForPaidLeave)
                        {
                            // Nghỉ phép có lương hợp lệ
                            paidLeaveHours += shiftScheduledHours;
                            paidLeaveDaysCount++;

                            itemSnapshots.Add(new PayrollItemSnapshot
                            {
                                Date = reg.WorkDate,
                                ShiftId = reg.ShiftId,
                                ShiftName = reg.Shift?.ShiftName,
                                StartTime = reg.Shift?.StartTime,
                                EndTime = reg.Shift?.EndTime,
                                CheckInTime = null,
                                CheckOutTime = null,
                                ActualWorkHours = 0,
                                PaidLeaveHours = Math.Round(shiftScheduledHours, 2),
                                TotalPayableHours = Math.Round(shiftScheduledHours, 2),
                                HourlyRate = hourlyRate,
                                TotalAmount = hourlyRate > 0 ? Math.Round((decimal)shiftScheduledHours * hourlyRate, 0) : 0,
                                SourceType = "PaidLeave",
                                Status = PayrollDisplayText.SnapPaidLeave,
                                Note = PayrollDisplayText.StoredLeaveApprovedNote(matchedLeave.LeaveRequestId)
                            });
                        }
                        else
                        {
                            // Nghỉ không lương (Part-time hoặc LeaveType = Unpaid)
                            unpaidLeaveDaysCount++;

                            itemSnapshots.Add(new PayrollItemSnapshot
                            {
                                Date = reg.WorkDate,
                                ShiftId = reg.ShiftId,
                                ShiftName = reg.Shift?.ShiftName,
                                StartTime = reg.Shift?.StartTime,
                                EndTime = reg.Shift?.EndTime,
                                CheckInTime = null,
                                CheckOutTime = null,
                                ActualWorkHours = 0,
                                PaidLeaveHours = 0,
                                TotalPayableHours = 0,
                                HourlyRate = hourlyRate,
                                TotalAmount = 0,
                                SourceType = "UnpaidLeave",
                                Status = PayrollDisplayText.SnapUnpaidLeave,
                                Note = isEligibleForPaidLeave ? PayrollDisplayText.NoteUnpaidLeave : PayrollDisplayText.NoteUnpaidLeaveNonEligible
                            });
                        }
                    }
                    else
                    {
                        // Ca đã duyệt nhưng hoàn toàn KHÔNG CÓ chấm công và KHÔNG CÓ đơn nghỉ phép
                        missingAttendanceCount++;
                        missingDates.Add($"{reg.WorkDate:dd/MM} ({reg.Shift?.ShiftName ?? PayrollDisplayText.ShiftFallback})");

                        itemSnapshots.Add(new PayrollItemSnapshot
                        {
                            Date = reg.WorkDate,
                            ShiftId = reg.ShiftId,
                            ShiftName = reg.Shift?.ShiftName,
                            StartTime = reg.Shift?.StartTime,
                            EndTime = reg.Shift?.EndTime,
                            CheckInTime = null,
                            CheckOutTime = null,
                            ActualWorkHours = 0,
                            PaidLeaveHours = 0,
                            TotalPayableHours = 0,
                            HourlyRate = hourlyRate,
                            TotalAmount = 0,
                            SourceType = "MissingAttendance",
                            Status = PayrollDisplayText.SnapAbsentMissing,
                            Note = PayrollDisplayText.StoredShiftNoAttendanceNote(reg.Shift?.ShiftName)
                        });
                    }
                }

                // Leave payroll evidence survives cancellation of the approved registrations.
                foreach (var leave in empLeaves.Where(x => x.ScheduleSnapshotJson != null))
                {
                    var evidence = LeavePayrollRules.Evidence(leave.ScheduleSnapshotJson!);
                    foreach (var item in evidence.Where(x => x.Date >= startDate && x.Date <= endDate))
                    {
                        // An approved registration handled above must not also pay from its snapshot.
                        if (empRegistrations.Any(x => x.ShiftRegistrationId == item.RegistrationId)) continue;
                        var worked = empAttendance.Where(x => x.Date.Date == item.Date.Date && x.ShiftId == item.ShiftId).Sum(x => x.WorkHours ?? 0);
                        var leaveHours = leave.LeaveType == LeaveType.Paid && isEligibleForPaidLeave
                            ? LeavePayrollRules.PayableHours(item.ScheduledHours, worked) : 0;
                        leaveScheduledHours += item.ScheduledHours;
                        paidLeaveHours += leaveHours;
                        if (leaveHours > 0) paidLeaveDaysCount++;
                        else if (leave.LeaveType != LeaveType.Paid || !isEligibleForPaidLeave) unpaidLeaveDaysCount++;
                        itemSnapshots.Add(new PayrollItemSnapshot { Date = item.Date, ShiftId = item.ShiftId, ShiftName = item.ShiftName,
                            StartTime = item.StartTime, EndTime = item.EndTime, ActualWorkHours = 0,
                            PaidLeaveHours = Math.Round(leaveHours, 2), TotalPayableHours = Math.Round(leaveHours, 2),
                            HourlyRate = hourlyRate, TotalAmount = Math.Round((decimal)leaveHours * hourlyRate, 0),
                            SourceType = leave.LeaveType == LeaveType.Paid && isEligibleForPaidLeave ? "PaidLeave" : "UnpaidLeave",
                            Status = leave.LeaveType == LeaveType.Paid && isEligibleForPaidLeave ? PayrollDisplayText.SnapPaidLeave : PayrollDisplayText.SnapUnpaidLeave,
                            Note = PayrollDisplayText.StoredLeaveApprovedNote(leave.LeaveRequestId) });
                    }
                }
                // Never invent hours for legacy approved paid leave whose schedule evidence was lost.
                if (isEligibleForPaidLeave && empLeaves.Any(x => x.LeaveType == LeaveType.Paid && x.ScheduleSnapshotJson == null))
                {
                    missingAttendanceCount++;
                    missingDates.Add(InputRules.Text("Đơn nghỉ có lương cũ thiếu dữ liệu ca gốc, cần rà soát.", "Legacy paid leave has no schedule evidence; review required."));
                }

                // Sắp xếp snapshot theo ngày
                itemSnapshots = itemSnapshots.OrderBy(s => s.Date).ThenBy(s => s.StartTime).ToList();

                // 3. Tính toán tổng lương & trạng thái
                paidLeaveDaysCount = itemSnapshots.Where(x => x.PaidLeaveHours > 0).Select(x => x.Date.Date).Distinct().Count();
                unpaidLeaveDaysCount = itemSnapshots.Where(x => x.SourceType == "UnpaidLeave").Select(x => x.Date.Date).Distinct().Count();
                double totalStandardHours = leaveScheduledHours + empRegistrations.Sum(r => r.Shift != null ? r.Shift.ScheduledWorkHours : 0);
                double totalPayableHours = actualWorkHours + paidLeaveHours;
                decimal totalSalary = 0;
                PayrollDetailStatus status = PayrollDetailStatus.Valid;
                string? warningNotes = null;

                if (hourlyRate <= 0)
                {
                    totalSalary = 0;
                    status = PayrollDetailStatus.MissingRate;
                    warningNotes = PayrollDisplayText.WarnMissingRate;
                }
                else
                {
                    totalSalary = Math.Round((decimal)totalPayableHours * hourlyRate, 0);

                    if (missingAttendanceCount > 0)
                    {
                        status = PayrollDetailStatus.HasWarnings;
                        var dateStr = string.Join(", ", missingDates.Take(4));
                        if (missingDates.Count > 4) dateStr += "...";
                        warningNotes = PayrollDisplayText.StoredAbnormalWarning(missingAttendanceCount, dateStr);
                    }
                }

                var detail = new PayrollDetail
                {
                    Payroll = payroll,
                    EmployeeId = emp.EmployeeId,
                    EmployeeCode = emp.EmployeeCode,
                    EmployeeName = emp.Account?.FullName ?? emp.Username,
                    StoreId = emp.StoreId,
                    StoreName = emp.Store?.StoreName ?? PayrollDisplayText.StoreOfficeHR,
                    EmploymentType = emp.EmploymentType,
                    HourlyRate = hourlyRate,
                    TotalStandardHours = Math.Round(totalStandardHours, 2),
                    TotalWorkHours = Math.Round(actualWorkHours, 2),
                    PaidLeaveHours = Math.Round(paidLeaveHours, 2),
                    TotalSalary = totalSalary,
                    ApprovedLeaveDays = paidLeaveDaysCount,
                    UnpaidLeaveDays = unpaidLeaveDaysCount,
                    MissingAttendanceCount = missingAttendanceCount,
                    Status = status,
                    WarningNotes = warningNotes,
                    ItemSnapshots = itemSnapshots
                };

                detailsList.Add(detail);
            }

            payroll.Details = detailsList;
            payroll.TotalEmployees = detailsList.Count;
            payroll.TotalWorkHours = Math.Round(detailsList.Sum(d => d.TotalWorkHours + d.PaidLeaveHours), 2);
            payroll.TotalGrossSalary = detailsList.Sum(d => d.TotalSalary);

            // Ghi audit log
            _context.PayrollAuditLog.Add(new PayrollAuditLog
            {
                Payroll = payroll,
                Action = isRecalculate ? "Recalculate" : "Calculate",
                FromStatus = isRecalculate ? PayrollStatus.Draft : null,
                ToStatus = PayrollStatus.Draft,
                OperatorUsername = operatorUsername,
                Timestamp = DateTime.UtcNow,
                Details = PayrollDisplayText.StoredAuditCalculate(isRecalculate, payroll.TotalEmployees, payroll.TotalWorkHours, payroll.TotalGrossSalary)
            });

            await _context.SaveChangesAsync();
            if (calculationTransaction != null) await calculationTransaction.CommitAsync();
            return payroll;
        }

        private static bool arMatchesShift(AttendanceRecord ar, ShiftRegistration reg)
        {
            if (ar.Date.Date != reg.WorkDate.Date) return false;
            if (ar.ShiftId.HasValue && reg.ShiftId > 0 && ar.ShiftId.Value == reg.ShiftId) return true;
            return false;
        }

        public async Task LockPayrollAsync(int payrollId, string operatorUsername)
        {
            await using var lockTransaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var payroll = await _context.Payroll
                .Include(p => p.Details)
                .FirstOrDefaultAsync(p => p.PayrollId == payrollId);

            if (payroll == null)
                throw new KeyNotFoundException(string.Format(PayrollResource.Err_PayrollNotFoundId, payrollId));

            if (payroll.Status == PayrollStatus.Locked)
                return;

            // Rebuild only payroll snapshots from current read-only source data before locking.
            payroll = await CalculatePayrollAsync(payroll.StartDate, payroll.EndDate, operatorUsername);
            // Kiểm tra phía server trước khi khóa:
            // 1. Chặn nếu có nhân viên thiếu đơn giá (MissingRate)
            var missingRates = payroll.Details.Where(d => d.Status == PayrollDetailStatus.MissingRate || d.HourlyRate <= 0).ToList();
            if (missingRates.Any())
            {
                var names = string.Join(", ", missingRates.Take(3).Select(d => d.EmployeeName));
                throw new InvalidOperationException(string.Format(PayrollResource.Err_LockMissingRate, missingRates.Count, names));
            }

            // 2. Chặn nếu có ca thiếu chấm công/chưa giải trình
            var warningDetails = payroll.Details.Where(d => d.Status == PayrollDetailStatus.HasWarnings || d.MissingAttendanceCount > 0).ToList();
            if (warningDetails.Any())
            {
                var names = string.Join(", ", warningDetails.Take(3).Select(d => d.EmployeeName));
                throw new InvalidOperationException(string.Format(PayrollResource.Err_LockWarnings, warningDetails.Count, names));
            }

            payroll.Status = PayrollStatus.Locked;
            payroll.LockedBy = operatorUsername;
            payroll.LockedDate = DateTime.UtcNow;
            payroll.UpdatedBy = operatorUsername;
            payroll.UpdatedDate = DateTime.UtcNow;

            _context.PayrollAuditLog.Add(new PayrollAuditLog
            {
                PayrollId = payrollId,
                Action = "Lock",
                FromStatus = PayrollStatus.Draft,
                ToStatus = PayrollStatus.Locked,
                OperatorUsername = operatorUsername,
                Timestamp = DateTime.UtcNow,
                Details = PayrollDisplayText.StoredAuditLock(payroll.Details.Count, payroll.TotalGrossSalary)
            });

            await _context.SaveChangesAsync();
            await lockTransaction.CommitAsync();
        }

        public async Task UnlockPayrollAsync(int payrollId, string reason, string operatorUsername)
        {
            await using var payrollTransaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            var payroll = await _context.Payroll.FirstOrDefaultAsync(p => p.PayrollId == payrollId);
            if (payroll == null)
                throw new KeyNotFoundException(string.Format(PayrollResource.Err_PayrollNotFoundId, payrollId));

            InputRules.Require(payroll.Status == PayrollStatus.Locked, "Status", "Chỉ được mở khóa bảng lương đã chốt.", "Only a locked payroll can be unlocked.");
            reason = InputRules.Reason(reason, 200);
            payroll.Status = PayrollStatus.Draft;
            payroll.Note = PayrollDisplayText.StoredUnlockNote(operatorUsername, DateTime.Now, reason);
            payroll.LockedBy = null;
            payroll.LockedDate = null;
            payroll.UpdatedBy = operatorUsername;
            payroll.UpdatedDate = DateTime.UtcNow;

            _context.PayrollAuditLog.Add(new PayrollAuditLog
            {
                PayrollId = payrollId,
                Action = "Unlock",
                FromStatus = PayrollStatus.Locked,
                ToStatus = PayrollStatus.Draft,
                OperatorUsername = operatorUsername,
                Timestamp = DateTime.UtcNow,
                Reason = reason,
                Details = PayrollDisplayText.StoredAuditUnlock(reason)
            });

            await _context.SaveChangesAsync();
            await payrollTransaction.CommitAsync();
        }

        public async Task DeletePayrollAsync(int payrollId, string operatorUsername)
        {
            await using var payrollTransaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            var payroll = await _context.Payroll
                .Include(p => p.Details)
                    .ThenInclude(d => d.ItemSnapshots)
                .Include(p => p.AuditLogs)
                .FirstOrDefaultAsync(p => p.PayrollId == payrollId);

            if (payroll == null)
                throw new KeyNotFoundException(string.Format(PayrollResource.Err_PayrollNotFoundId, payrollId));

            if (payroll.Status == PayrollStatus.Locked)
                throw new InvalidOperationException(PayrollResource.Err_DeleteLocked);

            foreach (var d in payroll.Details)
            {
                _context.PayrollItemSnapshot.RemoveRange(d.ItemSnapshots);
            }
            _context.PayrollDetail.RemoveRange(payroll.Details);
            _context.PayrollAuditLog.RemoveRange(payroll.AuditLogs);
            _context.Payroll.Remove(payroll);
            await _context.SaveChangesAsync();
            await payrollTransaction.CommitAsync();
        }

        public async Task<EmployeePayslipVM?> GetEmployeePayslipAsync(string username, int? month = null, int? year = null, int? payrollId = null)
        {
            var employee = await _context.Employee
                .Include(e => e.Account)
                .Include(e => e.Store)
                .FirstOrDefaultAsync(e => e.Username == username);

            if (employee == null) return null;

            IQueryable<PayrollDetail> query = _context.PayrollDetail
                .Include(d => d.Payroll)
                .Include(d => d.Store)
                .Include(d => d.ItemSnapshots)
                    .ThenInclude(s => s.Shift)
                .Where(d => d.EmployeeId == employee.EmployeeId);

            if (payrollId.HasValue)
            {
                query = query.Where(d => d.PayrollId == payrollId.Value);
            }
            else if (month.HasValue && year.HasValue)
            {
                query = query.Where(d => d.Payroll.PeriodMonth == month.Value && d.Payroll.PeriodYear == year.Value);
            }
            else
            {
                query = query.OrderByDescending(d => d.Payroll.PeriodYear).ThenByDescending(d => d.Payroll.PeriodMonth);
            }

            var detail = await query.FirstOrDefaultAsync();
            if (detail == null) return null;

            var startDate = detail.Payroll.StartDate;
            var endDate = detail.Payroll.EndDate;

            var attendances = await _context.AttendanceRecord
                .Include(a => a.Shift)
                .Where(a => a.Username == username && a.Date >= startDate && a.Date <= endDate)
                .OrderBy(a => a.Date)
                .ToListAsync();

            var leaves = await _context.LeaveRequest
                .Where(l => l.EmployeeId == employee.EmployeeId && l.StartDate <= endDate && l.EndDate >= startDate && l.Status == RegistrationStatus.Approved)
                .OrderBy(l => l.StartDate)
                .ToListAsync();

            var snapshots = detail.ItemSnapshots.OrderBy(s => s.Date).ThenBy(s => s.StartTime).ToList();

            return new EmployeePayslipVM
            {
                Detail = detail,
                Payroll = detail.Payroll,
                Employee = employee,
                AttendanceRecords = attendances,
                LeaveRequests = leaves,
                ItemSnapshots = snapshots
            };
        }

        public async Task<EmployeePayslipVM?> GetEmployeePayslipByIdAsync(int payrollId, int employeeId)
        {
            var detail = await _context.PayrollDetail
                .Include(d => d.Payroll)
                .Include(d => d.Store)
                .Include(d => d.ItemSnapshots)
                    .ThenInclude(s => s.Shift)
                .FirstOrDefaultAsync(d => d.PayrollId == payrollId && d.EmployeeId == employeeId);

            if (detail == null) return null;

            var employee = await _context.Employee
                .Include(e => e.Account)
                .Include(e => e.Store)
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

            if (employee == null) return null;

            var startDate = detail.Payroll.StartDate;
            var endDate = detail.Payroll.EndDate;

            var attendances = await _context.AttendanceRecord
                .Include(a => a.Shift)
                .Where(a => a.Username == employee.Username && a.Date >= startDate && a.Date <= endDate)
                .OrderBy(a => a.Date)
                .ToListAsync();

            var leaves = await _context.LeaveRequest
                .Where(l => l.EmployeeId == employee.EmployeeId && l.StartDate <= endDate && l.EndDate >= startDate && l.Status == RegistrationStatus.Approved)
                .OrderBy(l => l.StartDate)
                .ToListAsync();

            var snapshots = detail.ItemSnapshots.OrderBy(s => s.Date).ThenBy(s => s.StartTime).ToList();

            return new EmployeePayslipVM
            {
                Detail = detail,
                Payroll = detail.Payroll,
                Employee = employee,
                AttendanceRecords = attendances,
                LeaveRequests = leaves,
                ItemSnapshots = snapshots
            };
        }

        public async Task<List<EmployeePayslipSummaryVM>> GetEmployeePayslipHistoryAsync(string username)
        {
            var employee = await _context.Employee.FirstOrDefaultAsync(e => e.Username == username);
            if (employee == null) return new List<EmployeePayslipSummaryVM>();

            return await _context.PayrollDetail
                .Include(d => d.Payroll)
                .Where(d => d.EmployeeId == employee.EmployeeId && d.Payroll.Status == PayrollStatus.Locked)
                .OrderByDescending(d => d.Payroll.PeriodYear)
                .ThenByDescending(d => d.Payroll.PeriodMonth)
                .Select(d => new EmployeePayslipSummaryVM
                {
                    PayrollId = d.PayrollId,
                    PeriodMonth = d.Payroll.PeriodMonth,
                    PeriodYear = d.Payroll.PeriodYear,
                    Title = d.Payroll.Title,
                    TotalWorkHours = d.TotalWorkHours + d.PaidLeaveHours,
                    HourlyRate = d.HourlyRate,
                    TotalSalary = d.TotalSalary,
                    Status = d.Payroll.Status,
                    LockedDate = d.Payroll.LockedDate
                })
                .ToListAsync();
        }

        public async Task<List<PayrollAuditLog>> GetPayrollAuditLogsAsync(int payrollId)
        {
            return await _context.PayrollAuditLog
                .Where(l => l.PayrollId == payrollId)
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();
        }

        public async Task<byte[]> ExportPayrollToExcelAsync(int payrollId, int? storeId = null)
        {
            var payroll = await _context.Payroll.FirstOrDefaultAsync(p => p.PayrollId == payrollId);
            if (payroll == null)
                throw new KeyNotFoundException(string.Format(PayrollResource.Err_PayrollNotFoundId, payrollId));

            var query = _context.PayrollDetail
                .Include(d => d.Store)
                .Where(d => d.PayrollId == payrollId);

            if (storeId.HasValue && storeId.Value > 0)
            {
                query = query.Where(d => d.StoreId == storeId.Value);
            }

            var details = await query.OrderBy(d => d.StoreId).ThenBy(d => d.EmployeeCode).ToListAsync();

            using var workbook = new XLWorkbook();
            var sheetName = string.Format(PayrollResource.Excel_FileName, payroll.StartDate.ToString("ddMMyy"), payroll.EndDate.ToString("ddMMyy"));
            var ws = workbook.Worksheets.Add(sheetName);

            // Company Title
            ws.Cell(1, 1).Value = PayrollResource.Excel_CompanyTitle;
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Range(1, 1, 1, 12).Merge();

            // Report Title
            ws.Cell(2, 1).Value = string.Format(PayrollResource.Excel_ReportTitle, payroll.StartDate.ToString("dd/MM/yyyy"), payroll.EndDate.ToString("dd/MM/yyyy"));
            ws.Cell(2, 1).Style.Font.Bold = true;
            ws.Cell(2, 1).Style.Font.FontSize = 16;
            ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#0d6efd");
            ws.Range(2, 1, 2, 12).Merge();

            // Export Info
            ws.Cell(3, 1).Value = string.Format(PayrollResource.Excel_ExportInfo,
                DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                payroll.Status == PayrollStatus.Locked ? PayrollResource.Excel_StatusLocked : PayrollResource.Status_Draft,
                storeId.HasValue
                    ? (details.FirstOrDefault()?.StoreName is string scopeStore ? PayrollDisplayText.StoreName(scopeStore) : PayrollResource.Col_Store)
                    : PayrollResource.Excel_ScopeAll);
            ws.Cell(3, 1).Style.Font.Italic = true;
            ws.Range(3, 1, 3, 12).Merge();

            // Table Headers
            string[] headers =
            {
                PayrollResource.Excel_ColNo, PayrollResource.Col_EmployeeCode, PayrollResource.Col_FullName, PayrollResource.Col_Store, PayrollResource.Col_EmploymentType,
                PayrollResource.Excel_ColRate, PayrollResource.Card_ActualHours, PayrollResource.Excel_ColPaidLeaveHours, PayrollResource.Excel_ColUnpaidDays,
                PayrollResource.Col_PayableHours, PayrollResource.Excel_ColSalary, PayrollResource.Excel_ColStatusNote
            };

            int startRow = 5;
            for (int col = 0; col < headers.Length; col++)
            {
                var cell = ws.Cell(startRow, col + 1);
                cell.Value = headers[col];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0d6efd");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }

            int currentRow = startRow + 1;
            int stt = 1;
            foreach (var item in details)
            {
                ws.Cell(currentRow, 1).Value = stt++;
                ws.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Cell(currentRow, 2).Value = item.EmployeeCode;
                ws.Cell(currentRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Cell(currentRow, 3).Value = item.EmployeeName;
                ws.Cell(currentRow, 4).Value = PayrollDisplayText.StoreName(item.StoreName);
                ws.Cell(currentRow, 5).Value = UIHelper.GetDisplayName(item.EmploymentType);

                var cellRate = ws.Cell(currentRow, 6);
                cellRate.Value = item.HourlyRate;
                cellRate.Style.NumberFormat.Format = "#,##0";
                cellRate.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                var cellWork = ws.Cell(currentRow, 7);
                cellWork.Value = item.TotalWorkHours;
                cellWork.Style.NumberFormat.Format = "#,##0.00";
                cellWork.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                var cellPaidLeave = ws.Cell(currentRow, 8);
                cellPaidLeave.Value = item.PaidLeaveHours;
                cellPaidLeave.Style.NumberFormat.Format = "#,##0.00";
                cellPaidLeave.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                var cellUnpaidLeave = ws.Cell(currentRow, 9);
                cellUnpaidLeave.Value = item.UnpaidLeaveDays;
                cellUnpaidLeave.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                var cellPayable = ws.Cell(currentRow, 10);
                cellPayable.Value = item.TotalWorkHours + item.PaidLeaveHours;
                cellPayable.Style.NumberFormat.Format = "#,##0.00";
                cellPayable.Style.Font.Bold = true;
                cellPayable.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                var cellSalary = ws.Cell(currentRow, 11);
                cellSalary.Value = item.TotalSalary;
                cellSalary.Style.NumberFormat.Format = "#,##0";
                cellSalary.Style.Font.Bold = true;
                cellSalary.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                string statusText = PayrollDisplayText.DetailStatusText(item.Status);
                if (!string.IsNullOrEmpty(item.WarningNotes))
                {
                    statusText += $" ({PayrollDisplayText.WarningNotes(item.WarningNotes)})";
                }
                var cellStatus = ws.Cell(currentRow, 12);
                cellStatus.Value = statusText;
                if (item.Status == PayrollDetailStatus.MissingRate)
                {
                    cellStatus.Style.Font.FontColor = XLColor.Red;
                }
                else if (item.Status == PayrollDetailStatus.HasWarnings)
                {
                    cellStatus.Style.Font.FontColor = XLColor.Orange;
                }

                currentRow++;
            }

            // Summary Row
            ws.Cell(currentRow, 1).Value = PayrollResource.Excel_Total;
            ws.Range(currentRow, 1, currentRow, 5).Merge();
            ws.Cell(currentRow, 1).Style.Font.Bold = true;
            ws.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(currentRow, 1, currentRow, 12).Style.Fill.BackgroundColor = XLColor.FromHtml("#e9ecef");

            ws.Cell(currentRow, 6).Value = "";

            var sumWorkCell = ws.Cell(currentRow, 7);
            sumWorkCell.Value = details.Sum(d => d.TotalWorkHours);
            sumWorkCell.Style.NumberFormat.Format = "#,##0.00";
            sumWorkCell.Style.Font.Bold = true;

            var sumPaidLeaveCell = ws.Cell(currentRow, 8);
            sumPaidLeaveCell.Value = details.Sum(d => d.PaidLeaveHours);
            sumPaidLeaveCell.Style.NumberFormat.Format = "#,##0.00";
            sumPaidLeaveCell.Style.Font.Bold = true;

            var sumUnpaidLeaveCell = ws.Cell(currentRow, 9);
            sumUnpaidLeaveCell.Value = details.Sum(d => d.UnpaidLeaveDays);
            sumUnpaidLeaveCell.Style.Font.Bold = true;
            sumUnpaidLeaveCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var sumPayableCell = ws.Cell(currentRow, 10);
            sumPayableCell.Value = details.Sum(d => d.TotalWorkHours + d.PaidLeaveHours);
            sumPayableCell.Style.NumberFormat.Format = "#,##0.00";
            sumPayableCell.Style.Font.Bold = true;

            var sumSalaryCell = ws.Cell(currentRow, 11);
            sumSalaryCell.Value = details.Sum(d => d.TotalSalary);
            sumSalaryCell.Style.NumberFormat.Format = "#,##0";
            sumSalaryCell.Style.Font.Bold = true;

            ws.Cell(currentRow, 12).Value = "";

            // Borders
            var tableRange = ws.Range(startRow, 1, currentRow, 12);
            tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }
    }
}