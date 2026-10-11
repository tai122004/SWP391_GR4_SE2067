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

        public async Task<List<Payroll>> GetPayrollsAsync(int? year = null)
        {
            var query = _context.Payroll.AsQueryable();
            if (year.HasValue)
            {
                query = query.Where(p => p.PeriodYear == year.Value);
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
                .FirstOrDefaultAsync(p => p.PeriodMonth == month && p.PeriodYear == year);

            if (existing != null && existing.Status == PayrollStatus.Locked)
            {
                throw new InvalidOperationException($"Kỳ lương tháng {month}/{year} đã được chốt (Locked), không thể tính lại!");
            }

            var startDate = new DateTime(year, month, 1);
            var endDate = new DateTime(year, month, DateTime.DaysInMonth(year, month));

            // Load data
            var employees = await _context.Employee
                .Include(e => e.Account)
                .Include(e => e.Store)
                .Where(e => e.IsActive || e.Status != EmploymentStatus.Resigned)
                .ToListAsync();

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

            Payroll payroll;
            if (existing != null)
            {
                payroll = existing;
                _context.PayrollDetail.RemoveRange(existing.Details);
                payroll.Details.Clear();
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
                var empAttendance = attendanceRecords
                    .Where(a => a.Username == emp.Username)
                    .GroupBy(a => a.AttendanceId)
                    .Select(g => g.First())
                    .ToList();

                double totalWorkHours = 0;
                int missingAttendanceCount = 0;
                var missingDates = new List<string>();

                foreach (var ar in empAttendance)
                {
                    if (ar.WorkHours.HasValue)
                    {
                        totalWorkHours += ar.WorkHours.Value;
                    }
                    else
                    {
                        missingAttendanceCount++;
                        missingDates.Add(ar.Date.ToString("dd/MM"));
                    }
                }

                // Scheduled standard hours
                var empRegistrations = registrations.Where(r => r.EmployeeId == emp.EmployeeId).ToList();
                double totalStandardHours = empRegistrations.Sum(r => r.Shift != null ? r.Shift.ScheduledWorkHours : 0);

                // Approved leave days
                var empLeaves = leaves.Where(l => l.EmployeeId == emp.EmployeeId).ToList();
                int approvedLeaveDays = 0;
                foreach (var l in empLeaves)
                {
                    var effStart = l.StartDate < startDate ? startDate : l.StartDate;
                    var effEnd = l.EndDate > endDate ? endDate : l.EndDate;
                    if (effEnd >= effStart)
                    {
                        approvedLeaveDays += (effEnd - effStart).Days + 1;
                    }
                }

                // Check hourly rate & warnings
                decimal hourlyRate = 0;
                decimal totalSalary = 0;
                PayrollDetailStatus status = PayrollDetailStatus.Valid;
                string? warningNotes = null;

                if (!emp.HourlyRate.HasValue || emp.HourlyRate.Value <= 0)
                {
                    hourlyRate = 0;
                    totalSalary = 0;
                    status = PayrollDetailStatus.MissingRate;
                    warningNotes = "Chưa thiết lập đơn giá theo giờ (HourlyRate).";
                }
                else
                {
                    hourlyRate = emp.HourlyRate.Value;
                    totalSalary = Math.Round((decimal)totalWorkHours * hourlyRate, 0);

                    if (missingAttendanceCount > 0)
                    {
                        status = PayrollDetailStatus.HasWarnings;
                        var dateStr = string.Join(", ", missingDates.Take(5));
                        if (missingDates.Count > 5) dateStr += "...";
                        warningNotes = $"Có {missingAttendanceCount} ca thiếu vào/ra ({dateStr}).";
                    }
                }

                var detail = new PayrollDetail
                {
                    Payroll = payroll,
                    EmployeeId = emp.EmployeeId,
                    EmployeeCode = emp.EmployeeCode,
                    EmployeeName = emp.Account?.FullName ?? emp.Username,
                    StoreId = emp.StoreId,
                    StoreName = emp.Store?.StoreName ?? string.Empty,
                    EmploymentType = emp.EmploymentType,
                    HourlyRate = hourlyRate,
                    TotalStandardHours = Math.Round(totalStandardHours, 2),
                    TotalWorkHours = Math.Round(totalWorkHours, 2),
                    TotalSalary = totalSalary,
                    ApprovedLeaveDays = approvedLeaveDays,
                    MissingAttendanceCount = missingAttendanceCount,
                    Status = status,
                    WarningNotes = warningNotes
                };

                detailsList.Add(detail);
            }

            payroll.Details = detailsList;
            payroll.TotalEmployees = detailsList.Count;
            payroll.TotalWorkHours = Math.Round(detailsList.Sum(d => d.TotalWorkHours), 2);
            payroll.TotalGrossSalary = detailsList.Sum(d => d.TotalSalary);

            await _context.SaveChangesAsync();
            return payroll;
        }

        public async Task LockPayrollAsync(int payrollId, string operatorUsername)
        {
            var payroll = await _context.Payroll.FirstOrDefaultAsync(p => p.PayrollId == payrollId);
            if (payroll == null)
                throw new KeyNotFoundException($"Không tìm thấy kỳ lương ID {payrollId}");

            if (payroll.Status == PayrollStatus.Locked)
                return;

            payroll.Status = PayrollStatus.Locked;
            payroll.LockedBy = operatorUsername;
            payroll.LockedDate = DateTime.UtcNow;
            payroll.UpdatedBy = operatorUsername;
            payroll.UpdatedDate = DateTime.UtcNow;

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

            await _context.SaveChangesAsync();
        }

        public async Task DeletePayrollAsync(int payrollId, string operatorUsername)
        {
            var payroll = await _context.Payroll
                .Include(p => p.Details)
                .FirstOrDefaultAsync(p => p.PayrollId == payrollId);

            if (payroll == null)
                throw new KeyNotFoundException($"Không tìm thấy kỳ lương ID {payrollId}");

            if (payroll.Status == PayrollStatus.Locked)
                throw new InvalidOperationException("Không thể xóa kỳ lương đã chốt (Locked)!");

            _context.PayrollDetail.RemoveRange(payroll.Details);
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

            return new EmployeePayslipVM
            {
                Detail = detail,
                Payroll = detail.Payroll,
                Employee = employee,
                AttendanceRecords = attendances,
                LeaveRequests = leaves
            };
        }

        public async Task<EmployeePayslipVM?> GetEmployeePayslipByIdAsync(int payrollId, int employeeId)
        {
            var detail = await _context.PayrollDetail
                .Include(d => d.Payroll)
                .Include(d => d.Store)
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

            return new EmployeePayslipVM
            {
                Detail = detail,
                Payroll = detail.Payroll,
                Employee = employee,
                AttendanceRecords = attendances,
                LeaveRequests = leaves
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
                    TotalWorkHours = d.TotalWorkHours,
                    HourlyRate = d.HourlyRate,
                    TotalSalary = d.TotalSalary,
                    Status = d.Payroll.Status,
                    LockedDate = d.Payroll.LockedDate
                })
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
            ws.Cell(1, 1).Value = "RWPM - HỆ THỐNG QUẢN LÝ NHÂN SỰ & CHẤM CÔNG CHUỖI BÁN LẺ";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Range(1, 1, 1, 11).Merge();

            // Report Title
            ws.Cell(2, 1).Value = $"BẢNG LƯƠNG NHÂN VIÊN THÁNG {payroll.PeriodMonth:D2}/{payroll.PeriodYear}";
            ws.Cell(2, 1).Style.Font.Bold = true;
            ws.Cell(2, 1).Style.Font.FontSize = 16;
            ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#0d6efd");
            ws.Range(2, 1, 2, 11).Merge();

            // Export Info
            ws.Cell(3, 1).Value = $"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm} | Trạng thái: {(payroll.Status == PayrollStatus.Locked ? "Đã chốt" : "Bản nháp")}";
            ws.Cell(3, 1).Style.Font.Italic = true;
            ws.Range(3, 1, 3, 11).Merge();

            // Table Headers
            string[] headers =
            {
                "STT", "Mã NV", "Họ và tên", "Chi nhánh", "Hình thức",
                "Giờ theo lịch", "Giờ thực tế", "Nghỉ phép (ngày)", "Đơn giá/giờ (VNĐ)",
                "Tổng tiền lương (VNĐ)", "Trạng thái / Ghi chú"
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

                var cellStd = ws.Cell(currentRow, 6);
                cellStd.Value = item.TotalStandardHours;
                cellStd.Style.NumberFormat.Format = "#,##0.00";
                cellStd.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                var cellWork = ws.Cell(currentRow, 7);
                cellWork.Value = item.TotalWorkHours;
                cellWork.Style.NumberFormat.Format = "#,##0.00";
                cellWork.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                var cellLeave = ws.Cell(currentRow, 8);
                cellLeave.Value = item.ApprovedLeaveDays;
                cellLeave.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                var cellRate = ws.Cell(currentRow, 9);
                cellRate.Value = item.HourlyRate;
                cellRate.Style.NumberFormat.Format = "#,##0";
                cellRate.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                var cellSalary = ws.Cell(currentRow, 10);
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
                var cellStatus = ws.Cell(currentRow, 11);
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
            ws.Range(currentRow, 1, currentRow, 11).Style.Fill.BackgroundColor = XLColor.FromHtml("#e9ecef");

            var sumStdCell = ws.Cell(currentRow, 6);
            sumStdCell.Value = details.Sum(d => d.TotalStandardHours);
            sumStdCell.Style.NumberFormat.Format = "#,##0.00";
            sumStdCell.Style.Font.Bold = true;

            var sumWorkCell = ws.Cell(currentRow, 7);
            sumWorkCell.Value = details.Sum(d => d.TotalWorkHours);
            sumWorkCell.Style.NumberFormat.Format = "#,##0.00";
            sumWorkCell.Style.Font.Bold = true;

            var sumLeaveCell = ws.Cell(currentRow, 8);
            sumLeaveCell.Value = details.Sum(d => d.ApprovedLeaveDays);
            sumLeaveCell.Style.Font.Bold = true;
            sumLeaveCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(currentRow, 9).Value = "";

            var sumSalaryCell = ws.Cell(currentRow, 10);
            sumSalaryCell.Value = details.Sum(d => d.TotalSalary);
            sumSalaryCell.Style.NumberFormat.Format = "#,##0";
            sumSalaryCell.Style.Font.Bold = true;

            ws.Cell(currentRow, 11).Value = "";

            // Borders
            var tableRange = ws.Range(startRow, 1, currentRow, 11);
            tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }
    }
}
