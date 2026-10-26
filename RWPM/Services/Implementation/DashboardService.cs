using Microsoft.EntityFrameworkCore;
using RWPM.Common.Constants;
using RWPM.Common.Enums;
using RWPM.Common.Helper;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.Dashboard;
using RWPM.Services.Abstraction;

namespace RWPM.Services.Implementation
{
    /// <summary>
    /// Home dashboard ("Tổng quan"). Every block is loaded with a small, fixed number of
    /// grouped/projection queries (no per-row queries); time/status logic runs in memory
    /// on the materialized rows because it depends on ShiftTimeHelper.
    /// </summary>
    public class DashboardService : IDashboardService
    {
        private const int ListLimit = 20;

        private const int ShortageDays = 7;

        private readonly DefaultDatabaseContext _ctx;
        private readonly IShiftDemandService _demandService;
        private readonly IAvailabilityService _availabilityService;

        public DashboardService(DefaultDatabaseContext ctx, IShiftDemandService demandService, IAvailabilityService availabilityService)
        {
            _ctx = ctx;
            _demandService = demandService;
            _availabilityService = availabilityService;
        }

        public async Task<DashboardVM> GetDashboardAsync(string username, bool isChainManager, bool isStoreManager, bool canUseAttendance)
        {
            var now = DateTime.Now;
            var today = now.Date;

            var me = await _ctx.Employee.AsNoTracking()
                .Where(e => e.Username == username)
                .Select(e => new
                {
                    e.EmployeeId,
                    e.EmployeeCode,
                    e.StoreId,
                    StoreName = e.Store != null ? e.Store.StoreName : string.Empty,
                    e.AnnualLeaveBalance,
                    FullName = e.Account.FullName
                })
                .FirstOrDefaultAsync();

            var vm = new DashboardVM
            {
                Now = now,
                HasProfile = me != null,
                CanUseAttendance = canUseAttendance,
                FullName = me?.FullName
                    ?? await _ctx.Acc.AsNoTracking().Where(a => a.Username == username).Select(a => a.FullName).FirstOrDefaultAsync()
                    ?? username
            };

            if (isChainManager)
            {
                vm.Mode = DashboardMode.Chain;
                vm.Chain = await BuildChainAsync(now);
                vm.Chain.PendingAvailability = await _availabilityService.GetPendingReviewCountAsync(username);
            }
            else if (isStoreManager && me?.StoreId != null)
            {
                vm.Mode = DashboardMode.Store;
                vm.Store = await BuildStoreAsync(now, me.StoreId.Value, me.StoreName);
                vm.Store.PendingAvailability = await _availabilityService.GetPendingReviewCountAsync(username);
            }
            else
            {
                vm.Mode = DashboardMode.Employee;
            }

            if (me != null)
            {
                vm.My = new MyDashboardVM
                {
                    EmployeeCode = me.EmployeeCode,
                    StoreName = me.StoreName,
                    AnnualLeaveBalance = me.AnnualLeaveBalance
                };
                await FillMyAsync(vm.My, now, me.EmployeeId, username);
            }

            return vm;
        }

        // ------------------------------------------------------------------ Store manager

        private async Task<StoreDashboardVM> BuildStoreAsync(DateTime now, int storeId, string storeName)
        {
            var today = now.Date;
            var result = new StoreDashboardVM { StoreId = storeId, StoreName = storeName };

            result.ActiveHeadcount = await _ctx.Employee.AsNoTracking()
                .CountAsync(e => e.StoreId == storeId && e.IsActive && e.Status != EmploymentStatus.Resigned);

            var assignments = await LoadTodayAssignmentsAsync(now, storeId);

            result.Shifts = assignments
                .GroupBy(a => new { a.WorkDate, a.Shift.ShiftId })
                .Select(g =>
                {
                    var first = g.First();
                    return new TodayShiftVM
                    {
                        ShiftId = first.Shift.ShiftId,
                        ShiftName = first.Shift.ShiftName,
                        WorkDate = first.WorkDate,
                        StartAt = first.StartAt,
                        EndAt = first.EndAt,
                        IsCarryOver = first.WorkDate < today,
                        GraceMinutes = first.Grace,
                        Employees = g
                            .GroupBy(a => a.EmployeeId).Select(x => x.First())
                            .OrderBy(a => StateOrder(a.State)).ThenBy(a => a.FullName)
                            .Select(a => new TodayShiftEmployeeVM
                            {
                                EmployeeId = a.EmployeeId,
                                EmployeeCode = a.EmployeeCode,
                                FullName = a.FullName,
                                State = a.State,
                                LateMinutes = a.LateMinutes,
                                CheckInTime = a.CheckInTime,
                                CheckOutTime = a.CheckOutTime
                            }).ToList()
                    };
                })
                .OrderBy(s => s.StartAt).ThenBy(s => s.ShiftName)
                .ToList();

            var pending = await LoadPendingAsync(storeId: storeId, employeeId: null);
            result.Pending = ToSummary(pending, today);

            result.OnLeave = await LeaveTodayQuery(today, storeId).ToListAsync();
            result.Shortage = BuildShortage(await _demandService.GetCoverageAsync(storeId, today, today.AddDays(ShortageDays - 1)), today);
            return result;
        }

        // ------------------------------------------------------------------ HR / Admin

        private async Task<ChainDashboardVM> BuildChainAsync(DateTime now)
        {
            var today = now.Date;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var nextMonthStart = monthStart.AddMonths(1);
            var result = new ChainDashboardVM { PreviousMonth = monthStart.AddMonths(-1) };

            var stores = await _ctx.Store.AsNoTracking()
                .Where(s => s.IsActive)
                .OrderBy(s => s.StoreName)
                .Select(s => new { s.StoreId, s.StoreCode, s.StoreName })
                .ToListAsync();

            var headcounts = await _ctx.Employee.AsNoTracking()
                .Where(e => e.IsActive && e.Status != EmploymentStatus.Resigned)
                .GroupBy(e => e.StoreId)
                .Select(g => new { StoreId = g.Key, Count = g.Count() })
                .ToListAsync();

            var assignments = await LoadTodayAssignmentsAsync(now, null);
            var pending = await LoadPendingAsync(storeId: null, employeeId: null);

            var headByStore = headcounts.Where(h => h.StoreId.HasValue).ToDictionary(h => h.StoreId!.Value, h => h.Count);
            var todayByStore = assignments.Where(a => a.WorkDate == today)
                .GroupBy(a => a.StoreId).ToDictionary(g => g.Key, g => g.Count());
            var inShiftByStore = assignments.Where(a => a.State == ShiftAttendanceState.CheckedIn && now < a.EndAt)
                .GroupBy(a => a.StoreId).ToDictionary(g => g.Key, g => g.Select(a => a.EmployeeId).Distinct().Count());
            var pendingByStore = pending.SelectMany(p => p.Value)
                .GroupBy(p => p.StoreId).ToDictionary(g => g.Key, g => g.Sum(p => p.Count));

            result.ActiveHeadcount = headcounts.Sum(h => h.Count);
            result.UnassignedHeadcount = headcounts.Where(h => !h.StoreId.HasValue).Sum(h => h.Count);
            result.TodayAssignments = todayByStore.Values.Sum();
            result.CheckedInNow = inShiftByStore.Values.Sum();
            result.Stores = stores.Select(s => new StoreRowVM
            {
                StoreId = s.StoreId,
                StoreCode = s.StoreCode,
                StoreName = s.StoreName,
                Headcount = headByStore.GetValueOrDefault(s.StoreId),
                TodayAssignments = todayByStore.GetValueOrDefault(s.StoreId),
                CheckedInNow = inShiftByStore.GetValueOrDefault(s.StoreId),
                PendingCount = pendingByStore.GetValueOrDefault(s.StoreId)
            }).ToList();
            result.Pending = ToSummary(pending, today);

            // Ca thiếu người 7 ngày tới: một lần GetCoverageAsync cho mọi chi nhánh
            var coverage = await _demandService.GetCoverageAsync(null, today, today.AddDays(ShortageDays - 1));
            result.Shortage = BuildShortage(coverage, today);
            var shortByStore = coverage.Where(r => r.Shortage > 0).GroupBy(r => r.StoreId)
                .ToDictionary(g => g.Key, g => (Shifts: g.Count(), People: g.Sum(r => r.Shortage)));
            foreach (var row in result.Stores)
            {
                if (!shortByStore.TryGetValue(row.StoreId, out var sh)) continue;
                row.ShortShifts = sh.Shifts;
                row.MissingPeople = sh.People;
            }

            // Payroll: latest period + check that the previous calendar month has been covered.
            result.Payroll = await _ctx.Payroll.AsNoTracking()
                .OrderByDescending(p => p.EndDate).ThenByDescending(p => p.PayrollId)
                .Select(p => new PayrollSummaryVM
                {
                    PayrollId = p.PayrollId,
                    Title = p.Title,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    Status = p.Status,
                    TotalEmployees = p.TotalEmployees,
                    TotalGrossSalary = p.TotalGrossSalary
                })
                .FirstOrDefaultAsync();

            var prevStart = result.PreviousMonth;
            var prevEnd = monthStart.AddDays(-1);
            int prevYear = prevStart.Year, prevMonth = prevStart.Month;
            result.PreviousMonthMissing = !await _ctx.Payroll.AsNoTracking()
                .AnyAsync(p => (p.PeriodYear == prevYear && p.PeriodMonth == prevMonth)
                    || (p.StartDate <= prevEnd && p.EndDate >= prevEnd));

            var onLeave = await LeaveTodayQuery(today, null).ToListAsync();
            result.OnLeaveTotal = onLeave.Count;
            result.OnLeave = onLeave.Take(ListLimit).ToList();

            var newHires = await _ctx.Employee.AsNoTracking()
                .Where(e => e.JoinDate >= monthStart && e.JoinDate < nextMonthStart && e.Status != EmploymentStatus.Resigned)
                .OrderByDescending(e => e.JoinDate).ThenBy(e => e.EmployeeCode)
                .Select(e => new NewHireVM
                {
                    EmployeeCode = e.EmployeeCode,
                    FullName = e.Account.FullName,
                    StoreName = e.Store != null ? e.Store.StoreName : string.Empty,
                    JoinDate = e.JoinDate
                })
                .ToListAsync();
            result.NewHiresTotal = newHires.Count;
            result.NewHires = newHires.Take(ListLimit).ToList();

            return result;
        }

        // ------------------------------------------------------------------ "Của tôi"

        private async Task FillMyAsync(MyDashboardVM my, DateTime now, int employeeId, string username)
        {
            var today = now.Date;
            var yesterday = today.AddDays(-1);
            var lastDay = today.AddDays(7);

            var rows = await _ctx.ShiftRegistration.AsNoTracking()
                .Where(r => r.EmployeeId == employeeId && r.Status == RegistrationStatus.Approved
                    && r.WorkDate >= yesterday && r.WorkDate <= lastDay)
                .Select(r => new AssignmentRow
                {
                    StoreId = r.StoreId,
                    StoreName = r.Store.StoreName,
                    EmployeeId = r.EmployeeId,
                    Username = r.Employee.Username,
                    EmployeeCode = r.Employee.EmployeeCode,
                    FullName = r.Employee.Account.FullName,
                    WorkDate = r.WorkDate,
                    Shift = r.Shift
                })
                .ToListAsync();

            var records = await _ctx.AttendanceRecord.AsNoTracking()
                .Where(a => a.Username == username && a.Date >= yesterday && a.Date <= today)
                .Select(a => new AttendanceRow
                {
                    AttendanceId = a.AttendanceId,
                    Username = a.Username,
                    Date = a.Date,
                    ShiftId = a.ShiftId,
                    CheckInTime = a.CheckInTime,
                    CheckOutTime = a.CheckOutTime
                })
                .ToListAsync();

            ApplyStates(rows, records, now);

            MyShiftVM Map(AssignmentRow a) => new MyShiftVM
            {
                ShiftName = a.Shift.ShiftName,
                StoreName = a.StoreName,
                WorkDate = a.WorkDate,
                StartAt = a.StartAt,
                EndAt = a.EndAt,
                IsCarryOver = a.WorkDate < today,
                State = a.State,
                LateMinutes = a.LateMinutes,
                CheckInTime = a.CheckInTime,
                CheckOutTime = a.CheckOutTime
            };

            my.Today = rows
                .Where(a => a.WorkDate == today || (a.WorkDate == yesterday && IsOpenCarryOver(a, now)))
                .OrderBy(a => a.StartAt).Select(Map).ToList();
            my.Upcoming = rows
                .Where(a => a.WorkDate > today)
                .OrderBy(a => a.StartAt).Select(Map).ToList();

            var pending = await LoadPendingAsync(storeId: null, employeeId: employeeId);
            my.Pending = ToSummary(pending, today);

            my.LatestPayslip = await _ctx.PayrollDetail.AsNoTracking()
                .Where(d => d.EmployeeId == employeeId && d.Payroll.Status == PayrollStatus.Locked)
                .OrderByDescending(d => d.Payroll.EndDate).ThenByDescending(d => d.PayrollId)
                .Select(d => new MyPayslipVM
                {
                    PayrollId = d.PayrollId,
                    Title = d.Payroll.Title,
                    StartDate = d.Payroll.StartDate,
                    EndDate = d.Payroll.EndDate,
                    TotalSalary = d.TotalSalary
                })
                .FirstOrDefaultAsync();
        }

        // ------------------------------------------------------------------ Shared loaders

        private static ShortageSummaryVM BuildShortage(List<ShiftCoverageRow> rows, DateTime today)
        {
            var shortRows = rows.Where(r => r.Shortage > 0)
                .OrderBy(r => r.Date).ThenBy(r => r.StartTime).ThenBy(r => r.StoreName).ThenBy(r => r.ShiftName)
                .ToList();
            return new ShortageSummaryVM
            {
                From = today,
                To = today.AddDays(ShortageDays - 1),
                HasDemand = rows.Any(r => r.Required.HasValue),
                ShortShiftCount = shortRows.Count,
                MissingPeople = shortRows.Sum(r => r.Shortage),
                Items = shortRows.Take(ListLimit).Select(r => new ShortageItemVM
                {
                    StoreId = r.StoreId,
                    StoreName = r.StoreName,
                    ShiftId = r.ShiftId,
                    ShiftName = r.ShiftName,
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                    Date = r.Date,
                    Approved = r.Approved,
                    Required = r.Required ?? 0,
                    Pending = r.Pending
                }).ToList()
            };
        }

        /// <summary>
        /// Approved assignments of today plus overnight shifts started yesterday that are still running,
        /// with their attendance state. Two queries in total.
        /// </summary>
        private async Task<List<AssignmentRow>> LoadTodayAssignmentsAsync(DateTime now, int? storeId)
        {
            var today = now.Date;
            var yesterday = today.AddDays(-1);

            var query = _ctx.ShiftRegistration.AsNoTracking()
                .Where(r => r.Status == RegistrationStatus.Approved
                    && (r.WorkDate == today || (r.WorkDate == yesterday && r.Shift.EndDayOffset >= 1)));
            if (storeId.HasValue)
                query = query.Where(r => r.StoreId == storeId.Value);

            var rows = await query
                .Select(r => new AssignmentRow
                {
                    StoreId = r.StoreId,
                    StoreName = r.Store.StoreName,
                    EmployeeId = r.EmployeeId,
                    Username = r.Employee.Username,
                    EmployeeCode = r.Employee.EmployeeCode,
                    FullName = r.Employee.Account.FullName,
                    WorkDate = r.WorkDate,
                    Shift = r.Shift
                })
                .ToListAsync();

            rows = rows.Where(r => r.WorkDate == today || IsOpenCarryOver(r, now)).ToList();
            if (rows.Count == 0) return rows;

            var recordQuery = _ctx.AttendanceRecord.AsNoTracking()
                .Where(a => a.Date >= yesterday && a.Date <= today);
            if (storeId.HasValue)
            {
                var usernames = rows.Select(r => r.Username).Distinct().ToList();
                recordQuery = recordQuery.Where(a => usernames.Contains(a.Username));
            }

            var records = await recordQuery
                .Select(a => new AttendanceRow
                {
                    AttendanceId = a.AttendanceId,
                    Username = a.Username,
                    Date = a.Date,
                    ShiftId = a.ShiftId,
                    CheckInTime = a.CheckInTime,
                    CheckOutTime = a.CheckOutTime
                })
                .ToListAsync();

            ApplyStates(rows, records, now);
            return rows;
        }

        private static bool IsOpenCarryOver(AssignmentRow row, DateTime now)
        {
            var (_, endAt) = ShiftTimeHelper.GetDateTimeRange(row.WorkDate, row.Shift);
            return row.Shift.EndDayOffset >= 1 && now < endAt;
        }

        private static void ApplyStates(List<AssignmentRow> rows, List<AttendanceRow> records, DateTime now)
        {
            // Attendance is keyed by (Username, work date, shift) — same key CheckInAsync uses.
            var lookup = records
                .GroupBy(r => (r.Username.ToLower(), r.Date.Date, r.ShiftId))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.AttendanceId).First());

            foreach (var row in rows)
            {
                var (startAt, endAt) = ShiftTimeHelper.GetDateTimeRange(row.WorkDate, row.Shift);
                row.StartAt = startAt;
                row.EndAt = endAt;
                row.Grace = row.Shift.GracePeriodMinutes ?? ShiftDefaults.GracePeriodMinutes;

                lookup.TryGetValue((row.Username.ToLower(), row.WorkDate.Date, (int?)row.Shift.ShiftId), out var rec);
                row.CheckInTime = rec?.CheckInTime;
                row.CheckOutTime = rec?.CheckOutTime;

                if (rec?.CheckInTime != null)
                {
                    // Resolve the actual check-in moment (overnight shifts may check in on another calendar day).
                    var checkInAt = new[] { -1, 0, 1 }
                        .Select(d => row.WorkDate.Date.AddDays(d).Add(rec.CheckInTime.Value))
                        .OrderBy(t => Math.Abs((t - startAt).TotalMinutes))
                        .First();
                    var late = (int)Math.Floor((checkInAt - startAt).TotalMinutes);
                    row.LateMinutes = late > row.Grace ? late : 0;
                    row.State = rec.CheckOutTime.HasValue ? ShiftAttendanceState.CheckedOut : ShiftAttendanceState.CheckedIn;
                }
                else if (rec?.CheckOutTime != null)
                {
                    row.State = ShiftAttendanceState.CheckedOut;
                }
                else if (now < startAt)
                {
                    row.State = ShiftAttendanceState.NotStarted;
                }
                else if (now >= endAt)
                {
                    row.State = ShiftAttendanceState.Absent;
                }
                else
                {
                    var lateNow = (int)Math.Floor((now - startAt).TotalMinutes);
                    if (lateNow > row.Grace)
                    {
                        row.State = ShiftAttendanceState.Late;
                        row.LateMinutes = lateNow;
                    }
                    else
                    {
                        row.State = ShiftAttendanceState.AwaitingCheckIn;
                    }
                }
            }
        }

        private static int StateOrder(ShiftAttendanceState s) => s switch
        {
            ShiftAttendanceState.Late => 0,
            ShiftAttendanceState.Absent => 1,
            ShiftAttendanceState.AwaitingCheckIn => 2,
            ShiftAttendanceState.CheckedIn => 3,
            ShiftAttendanceState.NotStarted => 4,
            _ => 5
        };

        /// <summary>
        /// Pending requests of the four kinds, grouped by store (one query per kind).
        /// Scope mirrors RequestController.Index: chain = all, store = StoreId, employee = own requests.
        /// </summary>
        private async Task<Dictionary<string, List<PendingAgg>>> LoadPendingAsync(int? storeId, int? employeeId)
        {
            var reg = _ctx.ShiftRegistration.AsNoTracking().Where(x => x.Status == RegistrationStatus.Pending);
            var swap = _ctx.ShiftSwapRequest.AsNoTracking().Where(x => x.Status == RegistrationStatus.Pending);
            var leave = _ctx.LeaveRequest.AsNoTracking().Where(x => x.Status == RegistrationStatus.Pending);
            var adj = _ctx.AttendanceAdjustmentRequest.AsNoTracking().Where(x => x.Status == RegistrationStatus.Pending);

            if (storeId.HasValue)
            {
                reg = reg.Where(x => x.StoreId == storeId.Value);
                swap = swap.Where(x => x.StoreId == storeId.Value);
                leave = leave.Where(x => x.StoreId == storeId.Value);
                adj = adj.Where(x => x.StoreId == storeId.Value);
            }
            if (employeeId.HasValue)
            {
                reg = reg.Where(x => x.EmployeeId == employeeId.Value);
                swap = swap.Where(x => x.RequesterEmployeeId == employeeId.Value);
                leave = leave.Where(x => x.EmployeeId == employeeId.Value);
                adj = adj.Where(x => x.EmployeeId == employeeId.Value);
            }

            // Min/Max use the same date field RequestController.Index filters each kind on.
            return new Dictionary<string, List<PendingAgg>>
            {
                ["reg"] = await reg.GroupBy(x => x.StoreId)
                    .Select(g => new PendingAgg { StoreId = g.Key, Count = g.Count(), Min = g.Min(x => x.WorkDate), Max = g.Max(x => x.WorkDate) })
                    .ToListAsync(),
                ["swap"] = await swap.GroupBy(x => x.StoreId)
                    .Select(g => new PendingAgg { StoreId = g.Key, Count = g.Count(), Min = g.Min(x => x.CreatedDate), Max = g.Max(x => x.CreatedDate) })
                    .ToListAsync(),
                ["leave"] = await leave.GroupBy(x => x.StoreId)
                    .Select(g => new PendingAgg { StoreId = g.Key, Count = g.Count(), Min = g.Min(x => x.StartDate), Max = g.Max(x => x.EndDate) })
                    .ToListAsync(),
                ["adj"] = await adj.GroupBy(x => x.StoreId)
                    .Select(g => new PendingAgg { StoreId = g.Key, Count = g.Count(), Min = g.Min(x => x.WorkDate), Max = g.Max(x => x.WorkDate) })
                    .ToListAsync()
            };
        }

        private static PendingSummaryVM ToSummary(Dictionary<string, List<PendingAgg>> pending, DateTime today)
        {
            var all = pending.SelectMany(p => p.Value).ToList();
            var summary = new PendingSummaryVM
            {
                ShiftRegistration = pending["reg"].Sum(x => x.Count),
                Swap = pending["swap"].Sum(x => x.Count),
                Leave = pending["leave"].Sum(x => x.Count),
                Adjustment = pending["adj"].Sum(x => x.Count)
            };
            if (all.Count > 0)
            {
                // Request/Index defaults to [1st of month, +2 months - 1]; widen it so the list shows every pending item.
                var defaultStart = new DateTime(today.Year, today.Month, 1);
                var defaultEnd = defaultStart.AddMonths(2).AddDays(-1);
                var min = all.Min(x => x.Min).Date;
                var max = all.Max(x => x.Max).Date;
                summary.RangeStart = min < defaultStart ? min : defaultStart;
                summary.RangeEnd = max > defaultEnd ? max : defaultEnd;
            }
            return summary;
        }

        private IQueryable<LeaveTodayVM> LeaveTodayQuery(DateTime today, int? storeId)
        {
            var q = _ctx.LeaveRequest.AsNoTracking()
                .Where(l => l.Status == RegistrationStatus.Approved && l.StartDate <= today && l.EndDate >= today);
            if (storeId.HasValue)
                q = q.Where(l => l.StoreId == storeId.Value);

            return q.OrderBy(l => l.Store.StoreName).ThenBy(l => l.Employee.Account.FullName)
                .Select(l => new LeaveTodayVM
                {
                    EmployeeCode = l.Employee.EmployeeCode,
                    FullName = l.Employee.Account.FullName,
                    StoreName = l.Store.StoreName,
                    LeaveType = l.LeaveType,
                    StartDate = l.StartDate,
                    EndDate = l.EndDate
                });
        }

        // ------------------------------------------------------------------ Internal row types

        private sealed class AssignmentRow
        {
            public int StoreId { get; set; }
            public string StoreName { get; set; } = string.Empty;
            public int EmployeeId { get; set; }
            public string Username { get; set; } = string.Empty;
            public string EmployeeCode { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public DateTime WorkDate { get; set; }
            public Shift Shift { get; set; } = null!;

            // Computed in memory
            public DateTime StartAt { get; set; }
            public DateTime EndAt { get; set; }
            public int Grace { get; set; }
            public ShiftAttendanceState State { get; set; }
            public int LateMinutes { get; set; }
            public TimeSpan? CheckInTime { get; set; }
            public TimeSpan? CheckOutTime { get; set; }
        }

        private sealed class AttendanceRow
        {
            public int AttendanceId { get; set; }
            public string Username { get; set; } = string.Empty;
            public DateTime Date { get; set; }
            public int? ShiftId { get; set; }
            public TimeSpan? CheckInTime { get; set; }
            public TimeSpan? CheckOutTime { get; set; }
        }

        private sealed class PendingAgg
        {
            public int StoreId { get; set; }
            public int Count { get; set; }
            public DateTime Min { get; set; }
            public DateTime Max { get; set; }
        }
    }
}
