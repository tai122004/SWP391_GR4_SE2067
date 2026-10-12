using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RWPM.Common;
using RWPM.Common.Enums;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.Payroll;
using RWPM.Services.Abstraction;

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
                    .OrderByDescending(p => p.PeriodYear)
                    .ThenByDescending(p => p.PeriodMonth)
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

            return await query.OrderByDescending(p => p.PeriodYear)
                              .ThenByDescending(p => p.PeriodMonth)
                              .ToListAsync();
        }

        public async Task<Payroll?> GetPayrollByIdAsync(int id)
        {
            return await _context.Payroll
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
                throw new KeyNotFoundException($"Không tìm thấy kỳ lương với ID {payrollId}");
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

        public async Task<Payroll> CalculatePayrollAsync(int month, int year, string operatorUsername)
        {
            if (month < 1 || month > 12)
                throw new ArgumentOutOfRangeException(nameof(month), "Tháng không hợp lệ (1-12)");
            if (year < 2000 || year > 2100)
                throw new ArgumentOutOfRangeException(nameof(year), "Năm không hợp lệ");

            var existing = await _context.Payroll
                .Include(p => p.Details)
                    .ThenInclude(d => d.ItemSnapshots)
                .FirstOrDefaultAsync(p => p.PeriodMonth == month && p.PeriodYear == year);

            if (existing != null && existing.Status == PayrollStatus.Locked)
            {
                throw new InvalidOperationException($"Kỳ lương tháng {month}/{year} đã được chốt (Locked), không thể tính lại!");
            }

            var startDate = new DateTime(year, month, 1);
            var endDate = new DateTime(year, month, DateTime.DaysInMonth(year, month));

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
                    PeriodMonth = month,
                    PeriodYear = year,
                    Title = $"Bảng lương Tháng {month:D2}/{year}",
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

                    string statusStr = "Hợp lệ";
                    if (!ar.WorkHours.HasValue)
                    {
                        missingAttendanceCount++;
                        missingDates.Add($"{ar.Date:dd/MM} (Thiếu vào/ra)");
                        statusStr = "Thiếu giờ vào/ra";
                    }
                    else
                    {
                        bool isLate = ar.Shift != null && ar.CheckInTime.HasValue && (ar.CheckInTime.Value > ar.Shift.StartTime.Add(TimeSpan.FromMinutes(ar.Shift.GracePeriodMinutes ?? RWPM.Common.Constants.ShiftDefaults.GracePeriodMinutes)));
                        bool isEarly = ar.Shift != null && ar.CheckOutTime.HasValue && (ar.CheckOutTime.Value < ar.Shift.EndTime.Subtract(TimeSpan.FromMinutes(ar.Shift.EarlyCheckOutMinutes ?? RWPM.Common.Constants.ShiftDefaults.EarlyCheckOutMinutes)));

                        if (isLate && isEarly)
                        {
                            statusStr = "Đi muộn & Về sớm";
                        }
                        else if (isLate)
                        {
                            statusStr = "Đi muộn";
                        }
                        else if (isEarly)
                        {
                            statusStr = "Về sớm";
                        }
                    }

                    var snapshot = new PayrollItemSnapshot
                    {
                        Date = ar.Date,
                        ShiftId = ar.ShiftId,
                        ShiftName = ar.Shift?.ShiftName ?? "Không xác định",
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
                        Note = ar.WorkHours.HasValue ? null : "Chưa hoàn tất chấm công"
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
                                Status = "Nghỉ phép hưởng lương",
                                Note = $"Đơn nghỉ phép #{matchedLeave.LeaveRequestId} đã duyệt"
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
                                Status = "Nghỉ không lương",
                                Note = isEligibleForPaidLeave ? "Nghỉ không hưởng lương" : "Part-time/Thử việc nghỉ không hưởng lương"
                            });
                        }
                    }
                    else
                    {
                        // Ca đã duyệt nhưng hoàn toàn KHÔNG CÓ chấm công và KHÔNG CÓ đơn nghỉ phép
                        missingAttendanceCount++;
                        missingDates.Add($"{reg.WorkDate:dd/MM} ({reg.Shift?.ShiftName ?? "Ca"})");

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
                            Status = "Vắng / Thiếu chấm công",
                            Note = $"Ca {reg.Shift?.ShiftName ?? ""} đã duyệt nhưng không có chấm công"
                        });
                    }
                }

                // Sắp xếp snapshot theo ngày
                itemSnapshots = itemSnapshots.OrderBy(s => s.Date).ThenBy(s => s.StartTime).ToList();

                // 3. Tính toán tổng lương & trạng thái
                double totalStandardHours = empRegistrations.Sum(r => r.Shift != null ? r.Shift.ScheduledWorkHours : 0);
                double totalPayableHours = actualWorkHours + paidLeaveHours;
                decimal totalSalary = 0;
                PayrollDetailStatus status = PayrollDetailStatus.Valid;
                string? warningNotes = null;

                if (hourlyRate <= 0)
                {
                    totalSalary = 0;
                    status = PayrollDetailStatus.MissingRate;
                    warningNotes = "Chưa thiết lập đơn giá theo giờ (HourlyRate).";
                }
                else
                {
                    totalSalary = Math.Round((decimal)totalPayableHours * hourlyRate, 0);

                    if (missingAttendanceCount > 0)
                    {
                        status = PayrollDetailStatus.HasWarnings;
                        var dateStr = string.Join(", ", missingDates.Take(4));
                        if (missingDates.Count > 4) dateStr += "...";
                        warningNotes = $"Có {missingAttendanceCount} ca bất thường/thiếu chấm công ({dateStr}).";
                    }
                }

                var detail = new PayrollDetail
                {
                    Payroll = payroll,
                    EmployeeId = emp.EmployeeId,
                    EmployeeCode = emp.EmployeeCode,
                    EmployeeName = emp.Account?.FullName ?? emp.Username,
                    StoreId = emp.StoreId,
                    StoreName = emp.Store?.StoreName ?? "Văn phòng / HR",
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
                Details = $"{(isRecalculate ? "Tính lại" : "Khởi tạo")} bảng lương: {payroll.TotalEmployees} nhân viên, tổng giờ công: {payroll.TotalWorkHours:N2}h, tổng tiền: {payroll.TotalGrossSalary:N0} VNĐ"
            });

            await _context.SaveChangesAsync();
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
            var payroll = await _context.Payroll
                .Include(p => p.Details)
                .FirstOrDefaultAsync(p => p.PayrollId == payrollId);

            if (payroll == null)
                throw new KeyNotFoundException($"Không tìm thấy kỳ lương ID {payrollId}");

            if (payroll.Status == PayrollStatus.Locked)
                return;

            // Kiểm tra phía server trước khi khóa:
            // 1. Chặn nếu có nhân viên thiếu đơn giá (MissingRate)
            var missingRates = payroll.Details.Where(d => d.Status == PayrollDetailStatus.MissingRate || d.HourlyRate <= 0).ToList();
            if (missingRates.Any())
            {
                var names = string.Join(", ", missingRates.Take(3).Select(d => d.EmployeeName));
                throw new InvalidOperationException($"Không thể chốt bảng lương! Còn {missingRates.Count} nhân viên chưa có đơn giá lương ({names}). Vui lòng cập nhật đơn giá trước khi chốt.");
            }

            // 2. Chặn nếu có ca thiếu chấm công/chưa giải trình
            var warningDetails = payroll.Details.Where(d => d.Status == PayrollDetailStatus.HasWarnings || d.MissingAttendanceCount > 0).ToList();
            if (warningDetails.Any())
            {
                var names = string.Join(", ", warningDetails.Take(3).Select(d => d.EmployeeName));
                throw new InvalidOperationException($"Không thể chốt bảng lương! Còn {warningDetails.Count} nhân viên có ca bất thường hoặc thiếu chấm công ({names}). Vui lòng xử lý giải trình chấm công trước khi chốt.");
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
                Details = $"Chốt bảng lương thành công với {payroll.Details.Count} nhân viên, tổng quỹ lương: {payroll.TotalGrossSalary:N0} VNĐ"
            });

            await _context.SaveChangesAsync();
        }

        public async Task UnlockPayrollAsync(int payrollId, string reason, string operatorUsername)
        {
            var payroll = await _context.Payroll.FirstOrDefaultAsync(p => p.PayrollId == payrollId);
            if (payroll == null)
                throw new KeyNotFoundException($"Không tìm thấy kỳ lương ID {payrollId}");

            payroll.Status = PayrollStatus.Draft;
            payroll.Note = $"[Mở khóa bởi {operatorUsername} lúc {DateTime.Now:dd/MM/yyyy HH:mm}]: {reason}";
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
                Details = $"Mở khóa bảng lương về trạng thái Nháp. Lý do: {reason}"
            });

            await _context.SaveChangesAsync();
        }

        public async Task DeletePayrollAsync(int payrollId, string operatorUsername)
        {
            var payroll = await _context.Payroll
                .Include(p => p.Details)
                    .ThenInclude(d => d.ItemSnapshots)
                .Include(p => p.AuditLogs)
                .FirstOrDefaultAsync(p => p.PayrollId == payrollId);

            if (payroll == null)
                throw new KeyNotFoundException($"Không tìm thấy kỳ lương ID {payrollId}");

            if (payroll.Status == PayrollStatus.Locked)
                throw new InvalidOperationException("Không thể xóa kỳ lương đã chốt (Locked)!");

            foreach (var d in payroll.Details)
            {
                _context.PayrollItemSnapshot.RemoveRange(d.ItemSnapshots);
            }
            _context.PayrollDetail.RemoveRange(payroll.Details);
            _context.PayrollAuditLog.RemoveRange(payroll.AuditLogs);
            _context.Payroll.Remove(payroll);
            await _context.SaveChangesAsync();
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
                throw new KeyNotFoundException($"Không tìm thấy kỳ lương ID {payrollId}");

            var query = _context.PayrollDetail
                .Include(d => d.Store)
                .Where(d => d.PayrollId == payrollId);

            if (storeId.HasValue && storeId.Value > 0)
            {
                query = query.Where(d => d.StoreId == storeId.Value);
            }

            var details = await query.OrderBy(d => d.StoreId).ThenBy(d => d.EmployeeCode).ToListAsync();

            using var workbook = new XLWorkbook();
            var sheetName = $"BangLuong_{payroll.PeriodMonth:D2}_{payroll.PeriodYear}";
            var ws = workbook.Worksheets.Add(sheetName);

            // Company Title
            ws.Cell(1, 1).Value = "RWPM - HỆ THỐNG QUẢN LÝ NHÂN SỰ & CHẤM CÔNG CHUỖI CỬA HÀNG THỜI TRANG";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Range(1, 1, 1, 12).Merge();

            // Report Title
            ws.Cell(2, 1).Value = $"BẢNG LƯƠNG NHÂN VIÊN THÁNG {payroll.PeriodMonth:D2}/{payroll.PeriodYear}";
            ws.Cell(2, 1).Style.Font.Bold = true;
            ws.Cell(2, 1).Style.Font.FontSize = 16;
            ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#0d6efd");
            ws.Range(2, 1, 2, 12).Merge();

            // Export Info
            ws.Cell(3, 1).Value = $"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm} | Trạng thái: {(payroll.Status == PayrollStatus.Locked ? "Đã chốt (Bất biến)" : "Bản nháp")} | Phạm vi: {(storeId.HasValue ? details.FirstOrDefault()?.StoreName ?? "Chi nhánh" : "Toàn hệ thống")}";
            ws.Cell(3, 1).Style.Font.Italic = true;
            ws.Range(3, 1, 3, 12).Merge();

            // Table Headers
            string[] headers =
            {
                "STT", "Mã NV", "Họ và tên", "Chi nhánh", "Hình thức",
                "Đơn giá/giờ (VNĐ)", "Giờ làm thực tế", "Giờ nghỉ hưởng lương", "Nghỉ không lương (ngày)",
                "Tổng giờ tính lương", "Tổng tiền lương (VNĐ)", "Trạng thái / Ghi chú"
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
                ws.Cell(currentRow, 4).Value = item.StoreName;
                ws.Cell(currentRow, 5).Value = item.EmploymentType.ToString();

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

                string statusText = item.Status switch
                {
                    PayrollDetailStatus.Valid => "Hợp lệ",
                    PayrollDetailStatus.MissingRate => "Thiếu đơn giá",
                    PayrollDetailStatus.HasWarnings => "Có cảnh báo",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(item.WarningNotes))
                {
                    statusText += $" ({item.WarningNotes})";
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
            ws.Cell(currentRow, 1).Value = "TỔNG CỘNG";
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
