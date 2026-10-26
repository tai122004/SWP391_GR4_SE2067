using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RWPM.Common.Constants;
using RWPM.Common.Enums;
using RWPM.Common.Helper;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.Performance;
using RWPM.Services.Abstraction;
using R = RWPM.Resources.Shared.PerformanceResource;

namespace RWPM.Services.Implementation
{
    public class PerformanceService : IPerformanceService
    {
        public const int CommentMaxLength = 1000;
        public const int TrendMonths = 6;
        public const int MyMonths = 12;
        private const int MaxIdsForInClause = 1000;

        private readonly DefaultDatabaseContext _context;

        public PerformanceService(DefaultDatabaseContext context)
        {
            _context = context;
        }

        #region Data loading

        private sealed class ActivityData
        {
            public List<ShiftRegistration> Registrations { get; init; } = new();
            public List<AttendanceRecord> Attendance { get; init; } = new();
            public List<LeaveRequest> Leaves { get; init; } = new();
        }

        private async Task<ActivityData> LoadActivityAsync(IReadOnlyCollection<Employee> employees, DateTime from, DateTime to)
        {
            if (employees.Count == 0) return new ActivityData();
            var ids = employees.Select(e => e.EmployeeId).Distinct().ToList();
            var usernames = employees.Select(e => e.Username).Distinct().ToList();
            var filterByIds = ids.Count <= MaxIdsForInClause;

            var regQuery = _context.ShiftRegistration.AsNoTracking().Include(r => r.Shift)
                .Where(r => r.Status == RegistrationStatus.Approved && r.WorkDate >= from && r.WorkDate <= to);
            var attQuery = _context.AttendanceRecord.AsNoTracking().Include(a => a.Shift)
                .Where(a => a.Date >= from && a.Date <= to);
            var leaveQuery = _context.LeaveRequest.AsNoTracking()
                .Where(l => l.Status == RegistrationStatus.Approved && l.StartDate <= to && l.EndDate >= from);
            if (filterByIds)
            {
                regQuery = regQuery.Where(r => ids.Contains(r.EmployeeId));
                attQuery = attQuery.Where(a => usernames.Contains(a.Username));
                leaveQuery = leaveQuery.Where(l => ids.Contains(l.EmployeeId));
            }

            return new ActivityData
            {
                Registrations = await regQuery.ToListAsync(),
                Attendance = await attQuery.ToListAsync(),
                Leaves = await leaveQuery.ToListAsync()
            };
        }

        private async Task<Dictionary<string, string>> GetFullNamesAsync(IEnumerable<string> usernames)
        {
            var list = usernames.Where(u => !string.IsNullOrEmpty(u)).Distinct().ToList();
            if (list.Count == 0) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var accs = await _context.Acc.AsNoTracking()
                .Where(a => list.Contains(a.Username))
                .Select(a => new { a.Username, a.FullName })
                .ToListAsync();
            return accs.GroupBy(a => a.Username, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().FullName, StringComparer.OrdinalIgnoreCase);
        }

        private static PerformanceReviewInfoVM ToInfo(PerformanceReview r, IReadOnlyDictionary<string, string> names) => new()
        {
            Score = r.Score,
            Comment = r.Comment,
            ReviewedBy = r.ReviewedBy,
            ReviewerName = names.TryGetValue(r.ReviewedBy, out var n) ? n : null,
            ReviewedAt = r.ReviewedAt
        };

        private static PerformanceRowVM ToRow(Employee e) => new()
        {
            EmployeeId = e.EmployeeId,
            EmployeeCode = e.EmployeeCode,
            Username = e.Username,
            FullName = e.Account?.FullName ?? e.Username,
            StoreId = e.StoreId,
            StoreName = e.Store?.StoreName,
            Role = e.Account?.Role
        };

        #endregion

        #region Metrics calculation

        private static (DateTime Start, DateTime End) MonthRange(int year, int month)
        {
            var start = new DateTime(year, month, 1);
            return (start, start.AddMonths(1).AddDays(-1));
        }

        private static List<LeaveScheduleEntry> SafeSnapshot(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<LeaveScheduleEntry>();
            try { return JsonSerializer.Deserialize<List<LeaveScheduleEntry>>(json) ?? new List<LeaveScheduleEntry>(); }
            catch (JsonException) { return new List<LeaveScheduleEntry>(); }
        }

        /// <summary>
        /// Phân loại một ca: cùng quy tắc với PayrollService/AttendanceService — giờ vào/ra được quy về mốc thực
        /// (hỗ trợ ca qua đêm), đi muộn khi vào sau giờ bắt đầu ca quá GracePeriodMinutes, về sớm khi ra trước giờ
        /// kết thúc ca quá EarlyCheckOutMinutes; giờ công lấy từ AttendanceRecord.WorkHours.
        /// </summary>
        private static PerformanceShiftRowVM BuildRow(DateTime date, Shift? shift, AttendanceRecord? att, LeaveRequest? leave, bool scheduled, DateTime now)
        {
            var row = new PerformanceShiftRowVM
            {
                Date = date.Date,
                ShiftName = shift?.ShiftName,
                StartTime = shift?.StartTime,
                EndTime = shift?.EndTime,
                CheckInTime = att?.CheckInTime,
                CheckOutTime = att?.CheckOutTime,
                IsScheduled = scheduled,
                LeaveType = leave?.LeaveType
            };
            (DateTime StartAt, DateTime EndAt)? range = shift != null ? ShiftTimeHelper.GetDateTimeRange(date, shift) : null;

            if (att?.CheckInTime != null)
            {
                row.State = PerformanceShiftState.Worked;
                row.WorkedHours = att.WorkHours ?? 0;
                if (shift != null && range.HasValue)
                {
                    var r = range.Value;
                    var checkInAt = new[] { -1, 0, 1 }.Select(day => date.Date.AddDays(day).Add(att.CheckInTime.Value))
                        .OrderBy(t => Math.Abs((t - r.StartAt).TotalMinutes)).First();
                    var lateMins = (int)(checkInAt - r.StartAt).TotalMinutes;
                    var grace = shift.GracePeriodMinutes ?? ShiftDefaults.GracePeriodMinutes;
                    if (lateMins > grace)
                    {
                        row.IsLate = true;
                        row.LateMinutes = lateMins;
                    }

                    if (att.CheckOutTime.HasValue)
                    {
                        var checkOutAt = new[] { -1, 0, 1, 2 }.Select(day => date.Date.AddDays(day).Add(att.CheckOutTime.Value))
                            .Where(t => t >= checkInAt).OrderBy(t => t).FirstOrDefault();
                        if (checkOutAt != default)
                        {
                            var earlyMins = (int)(r.EndAt - checkOutAt).TotalMinutes;
                            var earlyLimit = shift.EarlyCheckOutMinutes ?? ShiftDefaults.EarlyCheckOutMinutes;
                            if (earlyMins > earlyLimit)
                            {
                                row.IsEarlyLeave = true;
                                row.EarlyLeaveMinutes = earlyMins;
                            }
                        }
                    }
                    else if (now >= r.EndAt)
                    {
                        row.IsMissingCheckOut = true;
                    }
                    else
                    {
                        row.IsInProgress = true;
                    }
                }
                else if (!att.CheckOutTime.HasValue && date.Date < now.Date)
                {
                    row.IsMissingCheckOut = true;
                }
                return row;
            }

            if (leave != null)
            {
                row.State = PerformanceShiftState.Leave;
                return row;
            }

            if (range.HasValue)
            {
                if (now < range.Value.StartAt) row.State = PerformanceShiftState.Upcoming;
                else if (now < range.Value.EndAt) row.State = PerformanceShiftState.Pending;
                else row.State = PerformanceShiftState.Absent;
            }
            else
            {
                row.State = date.Date < now.Date ? PerformanceShiftState.Absent : PerformanceShiftState.Upcoming;
            }
            return row;
        }

        private static (PerformanceMetrics Metrics, List<PerformanceShiftRowVM> Rows) Compute(Employee emp, ActivityData data, int year, int month, DateTime now)
        {
            var (start, end) = MonthRange(year, month);
            var regs = data.Registrations
                .Where(r => r.EmployeeId == emp.EmployeeId && r.WorkDate.Date >= start && r.WorkDate.Date <= end)
                .OrderBy(r => r.WorkDate).ThenBy(r => r.Shift?.StartTime)
                .ToList();
            var atts = data.Attendance
                .Where(a => string.Equals(a.Username, emp.Username, StringComparison.OrdinalIgnoreCase) && a.Date.Date >= start && a.Date.Date <= end)
                .GroupBy(a => a.AttendanceId).Select(g => g.First())
                .ToList();
            var leaves = data.Leaves.Where(l => l.EmployeeId == emp.EmployeeId).ToList();
            LeaveRequest? LeaveOn(DateTime d) => leaves.FirstOrDefault(l => d.Date >= l.StartDate.Date && d.Date <= l.EndDate.Date);

            var rows = new List<PerformanceShiftRowVM>();
            var usedAtt = new HashSet<int>();

            // 1. Ca đăng ký đã duyệt
            foreach (var reg in regs)
            {
                var att = atts.Where(a => !usedAtt.Contains(a.AttendanceId) && a.Date.Date == reg.WorkDate.Date && a.ShiftId == reg.ShiftId)
                    .OrderByDescending(a => a.CheckInTime.HasValue).ThenBy(a => a.AttendanceId)
                    .FirstOrDefault();
                if (att != null) usedAtt.Add(att.AttendanceId);
                rows.Add(BuildRow(reg.WorkDate, reg.Shift, att, LeaveOn(reg.WorkDate), true, now));
            }

            // 2. Chấm công không khớp ca đã duyệt (ca bị hủy/đổi sau khi đã làm) — vẫn tính giờ công, đi muộn, về sớm
            foreach (var att in atts.Where(a => !usedAtt.Contains(a.AttendanceId)).OrderBy(a => a.Date))
            {
                usedAtt.Add(att.AttendanceId);
                rows.Add(BuildRow(att.Date, att.Shift, att, null, false, now));
            }

            // 3. Ca đã bị hủy khi duyệt đơn nghỉ phép (lấy từ ảnh chụp lịch của đơn nghỉ)
            var regIds = regs.Select(r => r.ShiftRegistrationId).ToHashSet();
            foreach (var leave in leaves)
            {
                foreach (var item in SafeSnapshot(leave.ScheduleSnapshotJson)
                             .Where(x => x.Date.Date >= start && x.Date.Date <= end && !regIds.Contains(x.RegistrationId)))
                {
                    if (atts.Any(a => a.Date.Date == item.Date.Date && a.ShiftId == item.ShiftId)) continue;
                    if (rows.Any(r => r.State == PerformanceShiftState.Leave && r.Date == item.Date.Date && r.StartTime == item.StartTime && r.ShiftName == item.ShiftName)) continue;
                    rows.Add(new PerformanceShiftRowVM
                    {
                        Date = item.Date.Date,
                        ShiftName = item.ShiftName,
                        StartTime = item.StartTime,
                        EndTime = item.EndTime,
                        IsScheduled = false,
                        State = PerformanceShiftState.Leave,
                        LeaveType = leave.LeaveType
                    });
                }
            }

            rows = rows.OrderBy(r => r.Date).ThenBy(r => r.StartTime).ToList();

            var worked = rows.Where(r => r.State == PerformanceShiftState.Worked).ToList();
            var m = new PerformanceMetrics
            {
                ApprovedShifts = regs.Count,
                AttendedShifts = rows.Count(r => r.IsScheduled && r.State == PerformanceShiftState.Worked),
                Absences = rows.Count(r => r.IsScheduled && r.State == PerformanceShiftState.Absent),
                LateCount = worked.Count(r => r.IsLate),
                LateMinutes = worked.Sum(r => r.LateMinutes),
                EarlyLeaveCount = worked.Count(r => r.IsEarlyLeave),
                EarlyLeaveMinutes = worked.Sum(r => r.EarlyLeaveMinutes),
                MissingCheckOutCount = worked.Count(r => r.IsMissingCheckOut),
                LeaveShifts = rows.Count(r => r.State == PerformanceShiftState.Leave),
                WorkedHours = Math.Round(atts.Sum(a => a.WorkHours ?? 0), 2)
            };
            m.DueShifts = m.AttendedShifts + m.Absences;
            m.AttendanceRate = m.DueShifts > 0 ? Math.Round(100.0 * m.AttendedShifts / m.DueShifts, 1) : null;

            // Ngày nghỉ đã duyệt (theo lịch đơn nghỉ, giao với tháng)
            var paidDays = new HashSet<DateTime>();
            var unpaidDays = new HashSet<DateTime>();
            foreach (var leave in leaves)
            {
                var from = leave.StartDate.Date < start ? start : leave.StartDate.Date;
                var to = leave.EndDate.Date > end ? end : leave.EndDate.Date;
                for (var d = from; d <= to; d = d.AddDays(1))
                {
                    if (leave.LeaveType == LeaveType.Paid) paidDays.Add(d); else unpaidDays.Add(d);
                }
            }
            m.PaidLeaveDays = paidDays.Count;
            m.UnpaidLeaveDays = unpaidDays.Count;
            m.SuggestedScore = PerformanceScoring.Suggest(m, worked.Count > 0);
            return (m, rows);
        }

        #endregion

        #region Queries

        public async Task<List<PerformanceRowVM>> GetRowsAsync(PerformanceScope scope, int year, int month, int? storeId, string? search)
        {
            var (start, end) = MonthRange(year, month);
            var query = _context.Employee.AsNoTracking()
                .Include(e => e.Account)
                .Include(e => e.Store)
                .Where(e => (e.IsActive && e.JoinDate <= end && (e.ResignDate == null || e.ResignDate >= start))
                    || _context.ShiftRegistration.Any(r => r.EmployeeId == e.EmployeeId && r.Status == RegistrationStatus.Approved && r.WorkDate >= start && r.WorkDate <= end)
                    || _context.PerformanceReview.Any(r => r.EmployeeId == e.EmployeeId && r.Year == year && r.Month == month));

            var effectiveStore = scope.ForcedStoreId ?? (storeId.HasValue && storeId.Value > 0 ? storeId : null);
            if (effectiveStore.HasValue)
                query = query.Where(e => e.StoreId == effectiveStore.Value);
            if (scope.ExcludeEmployeeId.HasValue)
                query = query.Where(e => e.EmployeeId != scope.ExcludeEmployeeId.Value);
            if (scope.OnlyEmployeeRole)
                query = query.Where(e => e.Account.Role == AccountRole.Employee);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(e => e.EmployeeCode.Contains(s) || e.Username.Contains(s) || e.Account.FullName.Contains(s));
            }

            var employees = await query.OrderBy(e => e.StoreId).ThenBy(e => e.EmployeeCode).ToListAsync();
            if (employees.Count == 0) return new List<PerformanceRowVM>();

            var data = await LoadActivityAsync(employees, start, end);
            var ids = employees.Select(e => e.EmployeeId).ToList();
            var reviewQuery = _context.PerformanceReview.AsNoTracking().Where(r => r.Year == year && r.Month == month);
            if (ids.Count <= MaxIdsForInClause) reviewQuery = reviewQuery.Where(r => ids.Contains(r.EmployeeId));
            var reviews = (await reviewQuery.ToListAsync()).GroupBy(r => r.EmployeeId).ToDictionary(g => g.Key, g => g.First());
            var names = await GetFullNamesAsync(reviews.Values.Select(r => r.ReviewedBy));

            var now = DateTime.Now;
            var result = new List<PerformanceRowVM>(employees.Count);
            foreach (var emp in employees)
            {
                var row = ToRow(emp);
                row.Metrics = Compute(emp, data, year, month, now).Metrics;
                if (reviews.TryGetValue(emp.EmployeeId, out var rv)) row.Review = ToInfo(rv, names);
                result.Add(row);
            }
            return result;
        }

        public PerformanceSummaryVM Summarize(IReadOnlyCollection<PerformanceRowVM> rows)
        {
            var rates = rows.Where(r => r.Metrics.AttendanceRate.HasValue).Select(r => r.Metrics.AttendanceRate!.Value).ToList();
            return new PerformanceSummaryVM
            {
                AverageAttendanceRate = rates.Count > 0 ? Math.Round(rates.Average(), 1) : null,
                TotalLates = rows.Sum(r => r.Metrics.LateCount),
                TotalAbsences = rows.Sum(r => r.Metrics.Absences),
                ReviewedCount = rows.Count(r => r.Review != null),
                TotalEmployees = rows.Count
            };
        }

        public Task<Employee?> GetEmployeeAsync(int employeeId) =>
            _context.Employee.AsNoTracking().Include(e => e.Account).Include(e => e.Store)
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

        public Task<Employee?> GetEmployeeByUsernameAsync(string username) =>
            _context.Employee.AsNoTracking().Include(e => e.Account).Include(e => e.Store)
                .FirstOrDefaultAsync(e => e.Username == username);

        private async Task<List<PerformanceTrendItemVM>> BuildMonthSeriesAsync(Employee emp, List<(int Year, int Month)> months, Dictionary<string, PerformanceReviewInfoVM>? reviewInfos)
        {
            if (months.Count == 0) return new List<PerformanceTrendItemVM>();
            var first = months.Min(x => new DateTime(x.Year, x.Month, 1));
            var last = months.Max(x => new DateTime(x.Year, x.Month, 1));
            var data = await LoadActivityAsync(new[] { emp }, first, last.AddMonths(1).AddDays(-1));

            int firstKey = first.Year * 12 + first.Month, lastKey = last.Year * 12 + last.Month;
            var reviews = await _context.PerformanceReview.AsNoTracking()
                .Where(r => r.EmployeeId == emp.EmployeeId
                    && r.Year * 12 + r.Month >= firstKey && r.Year * 12 + r.Month <= lastKey)
                .ToListAsync();
            var names = reviewInfos != null ? await GetFullNamesAsync(reviews.Select(r => r.ReviewedBy)) : null;

            var now = DateTime.Now;
            var list = new List<PerformanceTrendItemVM>();
            foreach (var (y, m) in months)
            {
                var rv = reviews.FirstOrDefault(r => r.Year == y && r.Month == m);
                if (rv != null && reviewInfos != null && names != null) reviewInfos[PerformanceMyVM.Key(y, m)] = ToInfo(rv, names);
                list.Add(new PerformanceTrendItemVM
                {
                    Year = y,
                    Month = m,
                    Metrics = Compute(emp, data, y, m, now).Metrics,
                    ReviewScore = rv?.Score
                });
            }
            return list;
        }

        public async Task<PerformanceDetailVM?> GetDetailAsync(int employeeId, int year, int month)
        {
            var emp = await GetEmployeeAsync(employeeId);
            if (emp == null) return null;

            var (start, end) = MonthRange(year, month);
            var data = await LoadActivityAsync(new[] { emp }, start, end);
            var (metrics, shifts) = Compute(emp, data, year, month, DateTime.Now);

            var row = ToRow(emp);
            row.Metrics = metrics;
            var review = await _context.PerformanceReview.AsNoTracking()
                .FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.Year == year && r.Month == month);
            if (review != null) row.Review = ToInfo(review, await GetFullNamesAsync(new[] { review.ReviewedBy }));

            var months = Enumerable.Range(0, TrendMonths)
                .Select(i => start.AddMonths(-(TrendMonths - 1) + i))
                .Select(d => (d.Year, d.Month))
                .ToList();

            return new PerformanceDetailVM
            {
                Employee = row,
                Year = year,
                Month = month,
                Shifts = shifts,
                Trend = await BuildMonthSeriesAsync(emp, months, null)
            };
        }

        public async Task<PerformanceMyVM> GetMyAsync(int employeeId)
        {
            var emp = await GetEmployeeAsync(employeeId);
            if (emp == null) return new PerformanceMyVM { HasProfile = false };

            var current = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var joinMonth = new DateTime(emp.JoinDate.Year, emp.JoinDate.Month, 1);
            var months = new List<(int, int)>();
            for (var i = 0; i < MyMonths; i++)
            {
                var d = current.AddMonths(-i);
                if (d < joinMonth && months.Count > 0) break;
                months.Add((d.Year, d.Month));
            }

            var vm = new PerformanceMyVM { HasProfile = true, Employee = ToRow(emp) };
            vm.Months = await BuildMonthSeriesAsync(emp, months, vm.Reviews);
            return vm;
        }

        #endregion

        #region Review

        public async Task SaveReviewAsync(PerformanceReviewInput input, string reviewerUsername)
        {
            if (input.Year < 2000 || input.Year > 9999 || input.Month is < 1 or > 12)
                throw new ArgumentException(R.Err_InvalidMonth);
            var today = DateTime.Today;
            if (input.Year * 12 + input.Month > today.Year * 12 + today.Month)
                throw new ArgumentException(R.Err_FutureMonth);
            if (!input.Score.HasValue || input.Score.Value < 1 || input.Score.Value > 5)
                throw new ArgumentException(R.Err_ScoreRange);
            var comment = string.IsNullOrWhiteSpace(input.Comment) ? null : input.Comment.Trim();
            if (comment != null && comment.Length > CommentMaxLength)
                throw new ArgumentException(string.Format(R.Err_CommentLength, CommentMaxLength));
            if (string.IsNullOrWhiteSpace(reviewerUsername))
                throw new ArgumentException(R.Err_NoPermission);

            var emp = await _context.Employee.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == input.EmployeeId);
            if (emp == null) throw new ArgumentException(R.Err_EmployeeNotFound);
            if (string.Equals(emp.Username, reviewerUsername, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(R.Err_SelfReview);

            var (start, end) = MonthRange(input.Year, input.Month);
            var data = await LoadActivityAsync(new[] { emp }, start, end);
            var metrics = Compute(emp, data, input.Year, input.Month, DateTime.Now).Metrics;
            var snapshot = JsonSerializer.Serialize(metrics);
            var now = DateTime.Now;

            var existing = await _context.PerformanceReview
                .FirstOrDefaultAsync(r => r.EmployeeId == emp.EmployeeId && r.Year == input.Year && r.Month == input.Month);
            if (existing == null)
            {
                _context.PerformanceReview.Add(new PerformanceReview
                {
                    EmployeeId = emp.EmployeeId,
                    StoreId = emp.StoreId,
                    Year = input.Year,
                    Month = input.Month,
                    Score = input.Score.Value,
                    Comment = comment,
                    ReviewedBy = reviewerUsername,
                    ReviewedAt = now,
                    MetricsSnapshotJson = snapshot,
                    CreatedBy = reviewerUsername,
                    CreatedDate = now
                });
            }
            else
            {
                existing.StoreId = emp.StoreId;
                existing.Score = input.Score.Value;
                existing.Comment = comment;
                existing.ReviewedBy = reviewerUsername;
                existing.ReviewedAt = now;
                existing.MetricsSnapshotJson = snapshot;
                existing.UpdatedBy = reviewerUsername;
                existing.UpdatedDate = now;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Hai người đánh giá cùng lúc -> vi phạm unique (EmployeeId, Year, Month)
                throw new ArgumentException(R.Err_Concurrent);
            }
        }

        #endregion

        #region Export

        public Task<byte[]> ExportExcelAsync(IReadOnlyCollection<PerformanceRowVM> rows, int year, int month)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add(R.Excel_SheetName);

            ws.Cell(1, 1).Value = string.Format(R.Excel_Title, month.ToString("D2"), year);
            ws.Range(1, 1, 1, 20).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(2, 1).Value = R.Excel_FormulaNote;
            ws.Range(2, 1, 2, 20).Merge();
            ws.Cell(2, 1).Style.Font.Italic = true;

            var headers = new[]
            {
                R.Excel_ColNo, R.Col_EmployeeCode, R.Col_FullName, R.Col_Store,
                R.Col_ApprovedShifts, R.Col_AttendedShifts, R.Col_AttendanceRate, R.Col_Absences,
                R.Col_LateCount, R.Col_LateMinutes, R.Col_EarlyCount, R.Col_EarlyMinutes,
                R.Col_MissingCheckOut, R.Col_WorkedHours, R.Col_PaidLeaveDays, R.Col_UnpaidLeaveDays,
                R.Col_SuggestedScore, R.Col_ReviewScore, R.Col_Comment, R.Col_Reviewer
            };
            const int headerRow = 4;
            for (var i = 0; i < headers.Length; i++) ws.Cell(headerRow, i + 1).Value = headers[i];
            var headerRange = ws.Range(headerRow, 1, headerRow, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#0d6efd");
            headerRange.Style.Font.FontColor = XLColor.White;

            var rowIdx = headerRow + 1;
            var no = 1;
            foreach (var r in rows)
            {
                var m = r.Metrics;
                ws.Cell(rowIdx, 1).Value = no++;
                ws.Cell(rowIdx, 2).Value = r.EmployeeCode;
                ws.Cell(rowIdx, 3).Value = r.FullName;
                ws.Cell(rowIdx, 4).Value = r.StoreName ?? "-";
                ws.Cell(rowIdx, 5).Value = m.ApprovedShifts;
                ws.Cell(rowIdx, 6).Value = m.AttendedShifts;
                if (m.AttendanceRate.HasValue)
                {
                    ws.Cell(rowIdx, 7).Value = m.AttendanceRate.Value / 100.0;
                    ws.Cell(rowIdx, 7).Style.NumberFormat.Format = "0.0%";
                }
                else ws.Cell(rowIdx, 7).Value = "-";
                ws.Cell(rowIdx, 8).Value = m.Absences;
                ws.Cell(rowIdx, 9).Value = m.LateCount;
                ws.Cell(rowIdx, 10).Value = m.LateMinutes;
                ws.Cell(rowIdx, 11).Value = m.EarlyLeaveCount;
                ws.Cell(rowIdx, 12).Value = m.EarlyLeaveMinutes;
                ws.Cell(rowIdx, 13).Value = m.MissingCheckOutCount;
                ws.Cell(rowIdx, 14).Value = m.WorkedHours;
                ws.Cell(rowIdx, 14).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(rowIdx, 15).Value = m.PaidLeaveDays;
                ws.Cell(rowIdx, 16).Value = m.UnpaidLeaveDays;
                if (m.SuggestedScore.HasValue)
                {
                    ws.Cell(rowIdx, 17).Value = m.SuggestedScore.Value;
                    ws.Cell(rowIdx, 17).Style.NumberFormat.Format = "0.0";
                }
                else ws.Cell(rowIdx, 17).Value = "-";
                if (r.Review != null)
                {
                    ws.Cell(rowIdx, 18).Value = (int)r.Review.Score;
                    ws.Cell(rowIdx, 19).Value = r.Review.Comment ?? string.Empty;
                    ws.Cell(rowIdx, 20).Value = r.Review.ReviewerName ?? r.Review.ReviewedBy;
                }
                else
                {
                    ws.Cell(rowIdx, 18).Value = R.NotReviewed;
                }
                rowIdx++;
            }

            ws.Columns(1, 18).AdjustToContents();
            ws.Column(19).Width = 50;
            ws.Column(19).Style.Alignment.WrapText = true;
            ws.Column(20).AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return Task.FromResult(stream.ToArray());
        }

        #endregion
    }
}
