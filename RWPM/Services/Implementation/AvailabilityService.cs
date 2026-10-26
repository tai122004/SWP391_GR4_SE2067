using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Common.Helper;
using RWPM.Common.Models;
using RWPM.Common.Validation;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.Availability;
using RWPM.Services.Abstraction;
using R = RWPM.Resources.Shared.AvailabilityResource;

namespace RWPM.Services.Implementation
{
    /// <summary>
    /// Thời gian có thể làm việc.
    /// - Khung giờ lặp lại hằng tuần (EmployeeAvailability): nhân viên tự khai -> Chờ duyệt; Quản lý cửa hàng của chi nhánh (hoặc HR/Admin) duyệt/từ chối.
    ///   Chỉ khung Đã duyệt được tính khi kiểm tra; nhân viên chưa có khung Đã duyệt nào = làm được mọi lúc.
    /// - Ngày không thể làm (EmployeeUnavailability): không cần duyệt, khai trước &gt;= 3 ngày, tối đa 90 ngày; không trừ phép, không ảnh hưởng lương.
    /// </summary>
    public class AvailabilityService : IAvailabilityService
    {
        private const string CodeUnavailable = "Unavailable";
        private const string CodeOutside = "OutsideAvailability";
        private const string ReviewUrl = "/Availability/Review";
        private const string MyUrl = "/Availability/My";

        private readonly DefaultDatabaseContext _db;
        private readonly INotificationService _notifications;
        private readonly ILogger<AvailabilityService> _logger;

        public AvailabilityService(DefaultDatabaseContext db, INotificationService notifications, ILogger<AvailabilityService> logger)
        {
            _db = db;
            _notifications = notifications;
            _logger = logger;
        }

        private static InputValidationException Invalid(string field, string message) => new(field, message);
        private static void Require(bool condition, string field, string message)
        {
            if (!condition) throw Invalid(field, message);
        }
        private static string F(string? pattern, params object?[] args)
            => string.Format(CultureInfo.CurrentCulture, pattern ?? string.Empty, args);
        private static string? CleanText(string? value, string field, string message)
        {
            var text = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            Require(text == null || text.Length <= AvailabilityRules.MaxTextLength, field, message);
            return text;
        }

        #region Kiểm tra ca (dùng bởi xếp ca / đăng ký ca)

        public async Task<AvailabilityCheckResult> CheckAsync(int employeeId, DateTime workDate, Shift shift)
        {
            var map = await CheckManyAsync(new[] { employeeId }, workDate, shift);
            return map.TryGetValue(employeeId, out var r) ? r : AvailabilityCheckResult.Available(false);
        }

        public async Task<Dictionary<int, AvailabilityCheckResult>> CheckManyAsync(IReadOnlyCollection<int> employeeIds, DateTime workDate, Shift shift)
        {
            var ids = (employeeIds ?? Array.Empty<int>()).Distinct().ToList();
            var result = new Dictionary<int, AvailabilityCheckResult>();
            if (ids.Count == 0) return result;
            ArgumentNullException.ThrowIfNull(shift);

            var date = workDate.Date;
            var (start, end) = ShiftTimeHelper.GetDateTimeRange(date, shift);

            var unavailable = await _db.EmployeeUnavailability.AsNoTracking()
                .Where(x => ids.Contains(x.EmployeeId) && x.Date == date)
                .Select(x => new { x.EmployeeId, x.Reason })
                .ToListAsync();
            var unavailableMap = unavailable.GroupBy(x => x.EmployeeId).ToDictionary(g => g.Key, g => g.First().Reason);

            var approved = await _db.EmployeeAvailability.AsNoTracking()
                .Where(x => ids.Contains(x.EmployeeId) && x.Status == RegistrationStatus.Approved)
                .Select(x => new { x.EmployeeId, x.DayOfWeek, x.StartTime, x.EndTime })
                .ToListAsync();
            var windowMap = approved.GroupBy(x => x.EmployeeId)
                .ToDictionary(g => g.Key, g => g.Select(w => (w.DayOfWeek, w.StartTime, w.EndTime)).ToList());

            var shiftName = shift.GetLocalizedName();
            var shiftRange = AvailabilityRules.FormatTime(shift.StartTime) + "–" + AvailabilityRules.FormatTime(shift.EndTime);
            var dateText = date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

            foreach (var id in ids)
            {
                var hasDeclared = windowMap.TryGetValue(id, out var windows);
                if (unavailableMap.TryGetValue(id, out var reason))
                {
                    result[id] = new AvailabilityCheckResult
                    {
                        IsAvailable = false,
                        ReasonCode = CodeUnavailable,
                        HasDeclaredAvailability = hasDeclared,
                        Message = string.IsNullOrWhiteSpace(reason)
                            ? F(R.Check_Unavailable, dateText)
                            : F(R.Check_UnavailableWithReason, dateText, reason)
                    };
                    continue;
                }
                if (!hasDeclared)
                {
                    result[id] = AvailabilityCheckResult.Available(false);
                    continue;
                }
                if (AvailabilityRules.FitsWindows(windows!, start, end))
                {
                    result[id] = AvailabilityCheckResult.Available(true);
                    continue;
                }

                var sameDay = windows!.Where(w => w.DayOfWeek == date.DayOfWeek || (w.DayOfWeek == date.AddDays(-1).DayOfWeek && w.EndTime <= w.StartTime)).OrderBy(w => w.DayOfWeek == date.DayOfWeek ? 1 : 0).ThenBy(w => w.StartTime)
                    .Select(w => AvailabilityRules.FormatRange(w.StartTime, w.EndTime)).ToList();
                result[id] = new AvailabilityCheckResult
                {
                    IsAvailable = false,
                    ReasonCode = CodeOutside,
                    HasDeclaredAvailability = true,
                    Message = F(R.Check_OutsideAvailability, shiftName, shiftRange,
                        AvailabilityRules.DayName(date.DayOfWeek),
                        sameDay.Count > 0 ? string.Join(", ", sameDay) : R.Check_NoWindowThatDay)
                };
            }
            return result;
        }

        public async Task<Dictionary<(int EmployeeId, DateTime Date, int ShiftId), AvailabilityCheckResult>> CheckAssignmentsAsync(
            IEnumerable<(int employeeId, DateTime date, Shift shift)> items)
        {
            var list = (items ?? Enumerable.Empty<(int employeeId, DateTime date, Shift shift)>())
                .Where(i => i.shift != null)
                .Select(i => (EmployeeId: i.employeeId, Date: i.date.Date, Shift: i.shift))
                .GroupBy(i => (i.EmployeeId, i.Date, i.Shift.ShiftId)).Select(g => g.First())
                .ToList();
            var result = new Dictionary<(int EmployeeId, DateTime Date, int ShiftId), AvailabilityCheckResult>();
            if (list.Count == 0) return result;

            var ids = list.Select(i => i.EmployeeId).Distinct().ToList();
            var minDate = list.Min(i => i.Date);
            var maxDate = list.Max(i => i.Date);

            var unavailable = await _db.EmployeeUnavailability.AsNoTracking()
                .Where(x => ids.Contains(x.EmployeeId) && x.Date >= minDate && x.Date <= maxDate)
                .Select(x => new { x.EmployeeId, x.Date, x.Reason })
                .ToListAsync();
            var unavailableMap = unavailable.GroupBy(x => (x.EmployeeId, x.Date.Date))
                .ToDictionary(g => g.Key, g => g.First().Reason);

            var approved = await _db.EmployeeAvailability.AsNoTracking()
                .Where(x => ids.Contains(x.EmployeeId) && x.Status == RegistrationStatus.Approved)
                .Select(x => new { x.EmployeeId, x.DayOfWeek, x.StartTime, x.EndTime })
                .ToListAsync();
            var windowMap = approved.GroupBy(x => x.EmployeeId)
                .ToDictionary(g => g.Key, g => g.Select(w => (w.DayOfWeek, w.StartTime, w.EndTime)).ToList());

            foreach (var item in list)
            {
                var key = (item.EmployeeId, item.Date, item.Shift.ShiftId);
                var hasDeclared = windowMap.TryGetValue(item.EmployeeId, out var windows);
                var dateText = item.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                if (unavailableMap.TryGetValue((item.EmployeeId, item.Date), out var reason))
                {
                    result[key] = new AvailabilityCheckResult
                    {
                        IsAvailable = false,
                        ReasonCode = CodeUnavailable,
                        HasDeclaredAvailability = hasDeclared,
                        Message = string.IsNullOrWhiteSpace(reason)
                            ? F(R.Check_Unavailable, dateText)
                            : F(R.Check_UnavailableWithReason, dateText, reason)
                    };
                    continue;
                }
                if (!hasDeclared)
                {
                    result[key] = AvailabilityCheckResult.Available(false);
                    continue;
                }
                var (start, end) = ShiftTimeHelper.GetDateTimeRange(item.Date, item.Shift);
                if (AvailabilityRules.FitsWindows(windows!, start, end))
                {
                    result[key] = AvailabilityCheckResult.Available(true);
                    continue;
                }
                var sameDay = windows!.Where(w => w.DayOfWeek == item.Date.DayOfWeek || (w.DayOfWeek == item.Date.AddDays(-1).DayOfWeek && w.EndTime <= w.StartTime)).OrderBy(w => w.DayOfWeek == item.Date.DayOfWeek ? 1 : 0).ThenBy(w => w.StartTime)
                    .Select(w => AvailabilityRules.FormatRange(w.StartTime, w.EndTime)).ToList();
                result[key] = new AvailabilityCheckResult
                {
                    IsAvailable = false,
                    ReasonCode = CodeOutside,
                    HasDeclaredAvailability = true,
                    Message = F(R.Check_OutsideAvailability, item.Shift.GetLocalizedName(),
                        AvailabilityRules.FormatTime(item.Shift.StartTime) + "–" + AvailabilityRules.FormatTime(item.Shift.EndTime),
                        AvailabilityRules.DayName(item.Date.DayOfWeek),
                        sameDay.Count > 0 ? string.Join(", ", sameDay) : R.Check_NoWindowThatDay)
                };
            }
            return result;
        }

        #endregion

        #region Nhân viên tự khai báo

        private async Task<Employee> RequireSelfAsync(string username)
        {
            var employee = await _db.Employee.AsNoTracking().Include(x => x.Account)
                .SingleOrDefaultAsync(x => x.Username == username);
            if (employee == null || !employee.IsActive || employee.Account == null || !employee.Account.IsActive)
                throw new UnauthorizedAccessException(R.Err_NoProfile);
            return employee;
        }

        public async Task<AvailabilityMyVM> GetMyAsync(string username)
        {
            var today = DateTime.Today;
            var vm = new AvailabilityMyVM
            {
                MinUnavailableDate = AvailabilityRules.MinUnavailableDate(today),
                MaxUnavailableDate = AvailabilityRules.MaxUnavailableDate(today)
            };
            var employee = await _db.Employee.AsNoTracking()
                .Where(x => x.Username == username && x.IsActive)
                .Select(x => new AvailabilityEmployeeInfoVM
                {
                    EmployeeId = x.EmployeeId,
                    EmployeeCode = x.EmployeeCode,
                    FullName = x.Account.FullName,
                    Username = x.Username,
                    StoreId = x.StoreId,
                    StoreName = x.Store != null ? x.Store.StoreName : null
                })
                .SingleOrDefaultAsync();
            if (employee == null) return vm;

            vm.HasProfile = true;
            vm.Employee = employee;
            vm.Windows = await LoadWindowsAsync(employee.EmployeeId, includeAll: true);
            vm.Unavailable = await LoadUpcomingAsync(employee.EmployeeId, today, null, canDelete: true);
            return vm;
        }

        private async Task<List<AvailabilityWindowVM>> LoadWindowsAsync(int employeeId, bool includeAll)
        {
            var rows = await _db.EmployeeAvailability.AsNoTracking()
                .Where(x => x.EmployeeId == employeeId && (includeAll || x.Status != RegistrationStatus.Rejected))
                .Select(x => new AvailabilityWindowVM
                {
                    Id = x.EmployeeAvailabilityId,
                    EmployeeId = x.EmployeeId,
                    DayOfWeek = x.DayOfWeek,
                    StartTime = x.StartTime,
                    EndTime = x.EndTime,
                    Status = x.Status,
                    Note = x.Note,
                    RejectionReason = x.RejectionReason,
                    ApprovedBy = x.ApprovedBy,
                    ApprovedAt = x.ApprovedAt,
                    CreatedDate = x.CreatedDate
                })
                .ToListAsync();
            await FillApproverNamesAsync(rows);
            return rows.OrderBy(x => AvailabilityRules.SortIndex(x.DayOfWeek)).ThenBy(x => x.StartTime).ToList();
        }

        private async Task FillApproverNamesAsync(IEnumerable<AvailabilityWindowVM> rows)
        {
            var names = rows.Where(r => !string.IsNullOrEmpty(r.ApprovedBy)).Select(r => r.ApprovedBy!).Distinct().ToList();
            if (names.Count == 0) return;
            var map = await _db.Acc.AsNoTracking().Where(a => names.Contains(a.Username))
                .Select(a => new { a.Username, a.FullName }).ToListAsync();
            var dict = map.ToDictionary(x => x.Username, x => x.FullName, StringComparer.OrdinalIgnoreCase);
            foreach (var r in rows)
                if (r.ApprovedBy != null && dict.TryGetValue(r.ApprovedBy, out var n)) r.ApprovedByName = n;
        }

        private async Task<List<UnavailabilityVM>> LoadUpcomingAsync(int employeeId, DateTime from, DateTime? to, bool canDelete)
        {
            var query = _db.EmployeeUnavailability.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.Date >= from);
            if (to.HasValue) query = query.Where(x => x.Date <= to.Value);
            return await query.OrderBy(x => x.Date)
                .Select(x => new UnavailabilityVM { Id = x.EmployeeUnavailabilityId, Date = x.Date, Reason = x.Reason, CanDelete = canDelete })
                .ToListAsync();
        }

        public async Task<List<int>> AddWindowsAsync(string username, AvailabilityWindowInput input)
        {
            ArgumentNullException.ThrowIfNull(input);
            var employee = await RequireSelfAsync(username);

            var days = (input.Days ?? new List<int>()).Distinct().ToList();
            Require(days.Count is >= 1 and <= 7, "Days", R.Err_DaysRequired);
            Require(days.All(d => d is >= 0 and <= 6), "Days", R.Err_DaysInvalid);
            var start = AvailabilityRules.ParseTime(input.StartTime);
            var end = AvailabilityRules.ParseTime(input.EndTime);
            Require(start.HasValue, "StartTime", R.Err_TimeInvalid);
            Require(end.HasValue, "EndTime", R.Err_TimeInvalid);
            Require(start != end, "EndTime", R.Err_TimeSame);
            var note = CleanText(input.Note, "Note", R.Err_NoteTooLong);

            var existing = await _db.EmployeeAvailability.AsNoTracking()
                .Where(x => x.EmployeeId == employee.EmployeeId && x.Status != RegistrationStatus.Rejected)
                .Select(x => new { x.DayOfWeek, x.StartTime, x.EndTime })
                .ToListAsync();

            Require(existing.Count + days.Count <= AvailabilityRules.MaxWindowsTotal, "Days",
                F(R.Err_MaxTotal, AvailabilityRules.MaxWindowsTotal));
            foreach (var d in days.Select(x => (DayOfWeek)x))
            {
                Require(existing.Count(x => x.DayOfWeek == d) + 1 <= AvailabilityRules.MaxWindowsPerDay, "Days",
                    F(R.Err_MaxPerDay, AvailabilityRules.DayName(d), AvailabilityRules.MaxWindowsPerDay));
                var clash = existing.FirstOrDefault(x => AvailabilityRules.WindowsOverlap(d, start!.Value, end!.Value, x.DayOfWeek, x.StartTime, x.EndTime));
                if (clash != null)
                    throw Invalid("StartTime", F(R.Err_Overlap, AvailabilityRules.DayName(d), AvailabilityRules.FormatRange(start!.Value, end!.Value),
                        AvailabilityRules.DayName(clash.DayOfWeek), AvailabilityRules.FormatRange(clash.StartTime, clash.EndTime)));
            }
            // Các khung mới với nhau (cùng giờ, khác thứ) chỉ có thể chồng nhau khi khung dài và qua đêm sang thứ kế tiếp được chọn
            var picked = days.Select(x => (DayOfWeek)x).ToList();
            for (var i = 0; i < picked.Count; i++)
                for (var j = i + 1; j < picked.Count; j++)
                    if (AvailabilityRules.WindowsOverlap(picked[i], start!.Value, end!.Value, picked[j], start.Value, end.Value))
                        throw Invalid("Days", F(R.Err_OverlapSelected, AvailabilityRules.DayName(picked[i]), AvailabilityRules.DayName(picked[j])));

            var now = DateTime.Now;
            now = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
            var entities = picked.Select(d => new EmployeeAvailability
            {
                EmployeeId = employee.EmployeeId,
                DayOfWeek = d,
                StartTime = start!.Value,
                EndTime = end!.Value,
                Status = RegistrationStatus.Pending,
                Note = note,
                CreatedDate = now,
                CreatedBy = username
            }).ToList();
            _db.EmployeeAvailability.AddRange(entities);
            await _db.SaveChangesAsync();

            await NotifySubmittedAsync(employee, entities);
            return entities.Select(e => e.EmployeeAvailabilityId).ToList();
        }

        public async Task DeleteWindowAsync(string username, int windowId)
        {
            var employee = await RequireSelfAsync(username);
            var entity = await _db.EmployeeAvailability.SingleOrDefaultAsync(x => x.EmployeeAvailabilityId == windowId);
            Require(entity != null, "Id", R.Err_WindowNotFound);
            if (entity!.EmployeeId != employee.EmployeeId) throw new UnauthorizedAccessException(R.Err_NotOwner);
            _db.EmployeeAvailability.Remove(entity);
            await _db.SaveChangesAsync();
        }

        public async Task<int> AddUnavailabilityAsync(string username, UnavailabilityInput input)
        {
            ArgumentNullException.ThrowIfNull(input);
            var employee = await RequireSelfAsync(username);
            var today = DateTime.Today;
            Require(input.Date.HasValue && input.Date.Value.Date > DateTime.MinValue.Date, "Date", R.Err_DateRequired);
            var date = input.Date!.Value.Date;
            var min = AvailabilityRules.MinUnavailableDate(today);
            var max = AvailabilityRules.MaxUnavailableDate(today);
            Require(date >= min, "Date", F(R.Err_DateLead, AvailabilityRules.UnavailabilityLeadDays, min.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
            Require(date <= max, "Date", F(R.Err_DateTooFar, AvailabilityRules.UnavailabilityMaxAheadDays, max.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
            var reason = CleanText(input.Reason, "Reason", R.Err_ReasonTooLong);

            Require(!await _db.EmployeeUnavailability.AnyAsync(x => x.EmployeeId == employee.EmployeeId && x.Date == date),
                "Date", R.Err_DateDuplicate);

            var registrations = await _db.ShiftRegistration.AsNoTracking()
                .Where(x => x.EmployeeId == employee.EmployeeId && x.WorkDate == date
                    && (x.Status == RegistrationStatus.Approved || x.Status == RegistrationStatus.Pending))
                .Select(x => new { x.Status, x.Shift.ShiftName })
                .ToListAsync();
            if (registrations.Any(x => x.Status == RegistrationStatus.Approved))
                throw Invalid("Date", F(R.Err_DateHasApprovedShift, date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    string.Join(", ", registrations.Where(x => x.Status == RegistrationStatus.Approved).Select(x => x.ShiftName))));
            if (registrations.Count > 0)
                throw Invalid("Date", F(R.Err_DateHasPendingShift, date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    string.Join(", ", registrations.Select(x => x.ShiftName))));

            var now = DateTime.Now;
            var entity = new EmployeeUnavailability
            {
                EmployeeId = employee.EmployeeId,
                Date = date,
                Reason = reason,
                CreatedDate = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second),
                CreatedBy = username
            };
            _db.EmployeeUnavailability.Add(entity);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when ((ex.InnerException?.Message ?? "").Contains("IX_EmployeeUnavailability"))
            {
                throw Invalid("Date", R.Err_DateDuplicate);
            }
            return entity.EmployeeUnavailabilityId;
        }

        public async Task DeleteUnavailabilityAsync(string username, int unavailabilityId)
        {
            var employee = await RequireSelfAsync(username);
            var entity = await _db.EmployeeUnavailability.SingleOrDefaultAsync(x => x.EmployeeUnavailabilityId == unavailabilityId);
            Require(entity != null, "Id", R.Err_DateNotFound);
            if (entity!.EmployeeId != employee.EmployeeId) throw new UnauthorizedAccessException(R.Err_NotOwner);
            Require(entity.Date >= DateTime.Today, "Id", R.Err_DatePastDelete);
            _db.EmployeeUnavailability.Remove(entity);
            await _db.SaveChangesAsync();
        }

        #endregion

        #region Phạm vi duyệt

        private sealed record ReviewScope(Acc Actor, int? StoreId)
        {
            public bool All => StoreId == null;
        }

        private async Task<ReviewScope?> TryGetReviewScopeAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return null;
            var actor = await _db.Acc.AsNoTracking().SingleOrDefaultAsync(x => x.Username == username && x.IsActive);
            if (actor == null) return null;
            if (actor.Role is AccountRole.Admin or AccountRole.HR) return new ReviewScope(actor, null);
            if (actor.Role == AccountRole.StoreManager)
            {
                var storeId = await _db.Employee.AsNoTracking()
                    .Where(x => x.Username == username && x.IsActive && x.Store != null && x.Store.IsActive)
                    .Select(x => x.StoreId).SingleOrDefaultAsync();
                if (storeId.HasValue) return new ReviewScope(actor, storeId);
            }
            return null;
        }

        private async Task<ReviewScope> RequireReviewScopeAsync(string username)
            => await TryGetReviewScopeAsync(username) ?? throw new UnauthorizedAccessException(R.Err_NoReviewPermission);

        private static bool InScope(ReviewScope scope, int? employeeStoreId) => scope.All || (employeeStoreId.HasValue && employeeStoreId == scope.StoreId);

        public async Task<bool> CanReviewAsync(string username) => await TryGetReviewScopeAsync(username) != null;

        private IQueryable<EmployeeAvailability> ScopedWindows(ReviewScope scope, int? storeId, string? search)
        {
            var username = scope.Actor.Username;
            var query = _db.EmployeeAvailability.AsNoTracking().Where(x => x.Employee.Username != username && x.Employee.IsActive);
            var effectiveStore = scope.StoreId ?? (storeId > 0 ? storeId : null);
            if (effectiveStore.HasValue) query = query.Where(x => x.Employee.StoreId == effectiveStore.Value);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(x => x.Employee.Account.FullName.Contains(s) || x.Employee.EmployeeCode.Contains(s) || x.Employee.Username.Contains(s));
            }
            return query;
        }

        #endregion

        #region Duyệt

        public async Task<AvailabilityReviewVM> GetReviewAsync(string username, AvailabilityReviewFilter filter)
        {
            filter ??= new AvailabilityReviewFilter();
            var scope = await RequireReviewScopeAsync(username);

            RegistrationStatus? status = RegistrationStatus.Pending;
            if (string.Equals(filter.Status, "All", StringComparison.OrdinalIgnoreCase)) status = null;
            else if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<RegistrationStatus>(filter.Status, true, out var parsed) && Enum.IsDefined(parsed))
                status = parsed;

            var search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
            if (search?.Length > 100) search = search[..100];
            var storeId = scope.StoreId ?? (filter.StoreId > 0 ? filter.StoreId : null);

            var baseQuery = ScopedWindows(scope, storeId, search);
            var counts = await baseQuery.GroupBy(x => x.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();

            var today = DateTime.Today;
            var horizon = today.AddDays(AvailabilityRules.SummaryDays);
            var unavailableQuery = _db.EmployeeUnavailability.AsNoTracking()
                .Where(x => x.Date >= today && x.Date <= horizon && x.Employee.IsActive && x.Employee.Username != username);
            if (storeId.HasValue) unavailableQuery = unavailableQuery.Where(x => x.Employee.StoreId == storeId.Value);
            if (search != null)
                unavailableQuery = unavailableQuery.Where(x => x.Employee.Account.FullName.Contains(search) || x.Employee.EmployeeCode.Contains(search) || x.Employee.Username.Contains(search));

            var listQuery = status.HasValue ? baseQuery.Where(x => x.Status == status.Value) : baseQuery;
            var total = await listQuery.CountAsync();
            var pageSize = filter.PageSize is > 0 and <= 100 ? filter.PageSize : 20;
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            var page = Math.Clamp(filter.PageNumber, 1, totalPages);

            var rows = await listQuery
                .OrderBy(x => x.Status)
                .ThenBy(x => x.Employee.Account.FullName)
                .ThenBy(x => x.EmployeeId)
                .ThenBy(x => ((int)x.DayOfWeek + 6) % 7)
                .ThenBy(x => x.StartTime)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new AvailabilityReviewRowVM
                {
                    Id = x.EmployeeAvailabilityId,
                    EmployeeId = x.EmployeeId,
                    DayOfWeek = x.DayOfWeek,
                    StartTime = x.StartTime,
                    EndTime = x.EndTime,
                    Status = x.Status,
                    Note = x.Note,
                    RejectionReason = x.RejectionReason,
                    ApprovedBy = x.ApprovedBy,
                    ApprovedAt = x.ApprovedAt,
                    CreatedDate = x.CreatedDate,
                    EmployeeCode = x.Employee.EmployeeCode,
                    FullName = x.Employee.Account.FullName,
                    Username = x.Employee.Username,
                    StoreName = x.Employee.Store != null ? x.Employee.Store.StoreName : null
                })
                .ToListAsync();
            await FillApproverNamesAsync(rows);

            string? scopeStoreName = null;
            if (scope.StoreId.HasValue)
                scopeStoreName = await _db.Store.AsNoTracking().Where(s => s.StoreId == scope.StoreId.Value).Select(s => s.StoreName).SingleOrDefaultAsync();

            return new AvailabilityReviewVM
            {
                StoreId = storeId,
                Status = status,
                Search = search,
                IsStoreManager = !scope.All,
                ScopeStoreName = scopeStoreName,
                PendingCount = counts.Where(c => c.Key == RegistrationStatus.Pending).Sum(c => c.Count),
                ApprovedCount = counts.Where(c => c.Key == RegistrationStatus.Approved).Sum(c => c.Count),
                RejectedCount = counts.Where(c => c.Key == RegistrationStatus.Rejected).Sum(c => c.Count),
                UpcomingUnavailableCount = await unavailableQuery.CountAsync(),
                Paged = new PaginationRes<AvailabilityReviewRowVM>(rows, page, pageSize, total)
            };
        }

        public async Task<AvailabilityDecisionResult> DecideAsync(string username, IReadOnlyCollection<int> windowIds, RegistrationStatus decision, string? reason)
        {
            InputRules.Decision(decision);
            var ids = (windowIds ?? Array.Empty<int>()).Where(x => x > 0).Distinct().ToList();
            Require(ids.Count > 0, "Ids", R.Err_NothingSelected);
            Require(ids.Count <= 500, "Ids", R.Err_TooManySelected);
            string? rejectReason = null;
            if (decision == RegistrationStatus.Rejected)
            {
                rejectReason = reason?.Trim() ?? string.Empty;
                Require(rejectReason.Length is >= 1 and <= AvailabilityRules.MaxTextLength, "Reason", R.Err_RejectReason);
            }

            var scope = await RequireReviewScopeAsync(username);
            var entities = await _db.EmployeeAvailability.Include(x => x.Employee).ThenInclude(e => e.Account)
                .Where(x => ids.Contains(x.EmployeeAvailabilityId)).ToListAsync();
            Require(entities.Count > 0, "Ids", R.Err_WindowNotFound);

            if (entities.Any(x => string.Equals(x.Employee.Username, username, StringComparison.OrdinalIgnoreCase)))
                throw new UnauthorizedAccessException(R.Err_SelfApprove);
            if (entities.Any(x => !InScope(scope, x.Employee.StoreId)))
                throw new UnauthorizedAccessException(R.Err_OutOfScope);

            var pending = entities.Where(x => x.Status == RegistrationStatus.Pending).ToList();
            var skipped = ids.Count - pending.Count;
            if (pending.Count == 0) return new AvailabilityDecisionResult(0, skipped);

            if (decision == RegistrationStatus.Approved)
            {
                // Phòng hờ dữ liệu cũ: không duyệt khung chồng lấn với khung đã duyệt khác của cùng nhân viên
                var empIds = pending.Select(x => x.EmployeeId).Distinct().ToList();
                var approved = await _db.EmployeeAvailability.AsNoTracking()
                    .Where(x => empIds.Contains(x.EmployeeId) && x.Status == RegistrationStatus.Approved)
                    .Select(x => new { x.EmployeeId, x.DayOfWeek, x.StartTime, x.EndTime }).ToListAsync();
                var accepted = new List<EmployeeAvailability>();
                foreach (var w in pending)
                {
                    var conflict = approved.Any(a => a.EmployeeId == w.EmployeeId && AvailabilityRules.WindowsOverlap(w.DayOfWeek, w.StartTime, w.EndTime, a.DayOfWeek, a.StartTime, a.EndTime))
                        || accepted.Any(a => a.EmployeeId == w.EmployeeId && AvailabilityRules.WindowsOverlap(w.DayOfWeek, w.StartTime, w.EndTime, a.DayOfWeek, a.StartTime, a.EndTime));
                    if (conflict)
                        throw Invalid("Ids", F(R.Err_ApproveOverlap, w.Employee.Account?.FullName ?? w.Employee.Username,
                            AvailabilityRules.DayName(w.DayOfWeek), AvailabilityRules.FormatRange(w.StartTime, w.EndTime)));
                    accepted.Add(w);
                }
            }

            var now = DateTime.Now;
            now = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
            foreach (var w in pending)
            {
                w.Status = decision;
                w.RejectionReason = decision == RegistrationStatus.Rejected ? rejectReason : null;
                w.ApprovedBy = username.Length > 30 ? username[..30] : username;
                w.ApprovedAt = now;
                w.UpdatedBy = w.ApprovedBy;
                w.UpdatedDate = now;
            }
            await _db.SaveChangesAsync();

            await NotifyDecisionAsync(pending, decision, rejectReason);
            return new AvailabilityDecisionResult(pending.Count, skipped);
        }

        public async Task<int> GetPendingReviewCountAsync(string username)
        {
            var scope = await TryGetReviewScopeAsync(username);
            if (scope == null) return 0;
            return await ScopedWindows(scope, null, null).CountAsync(x => x.Status == RegistrationStatus.Pending);
        }

        #endregion

        #region Quản lý xem một nhân viên

        private async Task<AvailabilityEmployeeInfoVM> RequireEmployeeInScopeAsync(string username, int employeeId)
        {
            Require(employeeId > 0, "EmployeeId", R.Err_EmployeeNotFound);
            // Tự xem / Admin, HR / StoreManager cùng chi nhánh; còn lại ném UnauthorizedAccessException
            await WorkflowGuard.EmployeeAsync(_db, username, employeeId);
            var info = await _db.Employee.AsNoTracking().Where(x => x.EmployeeId == employeeId)
                .Select(x => new AvailabilityEmployeeInfoVM
                {
                    EmployeeId = x.EmployeeId,
                    EmployeeCode = x.EmployeeCode,
                    FullName = x.Account.FullName,
                    Username = x.Username,
                    StoreId = x.StoreId,
                    StoreName = x.Store != null ? x.Store.StoreName : null
                }).SingleOrDefaultAsync();
            return info ?? throw Invalid("EmployeeId", R.Err_EmployeeNotFound);
        }

        public async Task<AvailabilityEmployeeVM> GetEmployeeViewAsync(string username, int employeeId)
        {
            var info = await RequireEmployeeInScopeAsync(username, employeeId);
            var today = DateTime.Today;
            var scope = await TryGetReviewScopeAsync(username);
            return new AvailabilityEmployeeVM
            {
                Employee = info,
                Windows = await LoadWindowsAsync(employeeId, includeAll: false),
                Upcoming = await LoadUpcomingAsync(employeeId, today, AvailabilityRules.MaxUnavailableDate(today), canDelete: false),
                CanReview = scope != null && InScope(scope, info.StoreId)
                    && !string.Equals(info.Username, username, StringComparison.OrdinalIgnoreCase)
            };
        }

        public async Task<AvailabilitySummaryDto> GetEmployeeSummaryAsync(string username, int employeeId, int days = 30)
        {
            var info = await RequireEmployeeInScopeAsync(username, employeeId);
            days = Math.Clamp(days, 1, AvailabilityRules.UnavailabilityMaxAheadDays);
            var today = DateTime.Today;
            var windows = (await LoadWindowsAsync(employeeId, includeAll: false)).Where(w => w.Status == RegistrationStatus.Approved).ToList();
            var dates = await LoadUpcomingAsync(employeeId, today, today.AddDays(days), canDelete: false);
            return new AvailabilitySummaryDto
            {
                EmployeeId = info.EmployeeId,
                FullName = info.FullName,
                HasDeclaredAvailability = windows.Count > 0,
                Windows = windows.Select(w => new AvailabilitySummaryWindowDto
                {
                    DayOfWeek = (int)w.DayOfWeek,
                    DayName = w.DayName,
                    Start = AvailabilityRules.FormatTime(w.StartTime),
                    End = AvailabilityRules.FormatTime(w.EndTime),
                    CrossesMidnight = w.CrossesMidnight,
                    Display = AvailabilityRules.DayShortName(w.DayOfWeek) + " " + w.TimeRange
                }).ToList(),
                UnavailableDates = dates.Select(d => new AvailabilitySummaryDateDto
                {
                    Date = d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Display = AvailabilityRules.DayShortName(d.Date.DayOfWeek) + " " + d.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    Reason = d.Reason
                }).ToList()
            };
        }

        #endregion

        #region Thông báo (không bao giờ làm hỏng thao tác chính)

        private async Task NotifySubmittedAsync(Employee employee, List<EmployeeAvailability> windows)
        {
            try
            {
                var approvers = await _notifications.GetApproverUsernamesAsync(employee.StoreId ?? 0, employee.Username);
                if (approvers.Count == 0) return;
                var encoded = AvailabilityRules.EncodeWindows(windows.Select(w => (w.DayOfWeek, w.StartTime, w.EndTime)));
                await _notifications.NotifyAsync(approvers, NotificationTypes.AvailabilitySubmitted,
                    new[] { employee.Account?.FullName ?? employee.Username, windows.Count.ToString(CultureInfo.InvariantCulture), encoded },
                    ReviewUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify approvers about availability of employee {EmployeeId}", employee.EmployeeId);
            }
        }

        private async Task NotifyDecisionAsync(List<EmployeeAvailability> windows, RegistrationStatus decision, string? reason)
        {
            try
            {
                foreach (var group in windows.GroupBy(w => w.Employee.Username))
                {
                    var encoded = AvailabilityRules.EncodeWindows(group.Select(w => (w.DayOfWeek, w.StartTime, w.EndTime)));
                    var count = group.Count().ToString(CultureInfo.InvariantCulture);
                    if (decision == RegistrationStatus.Approved)
                        await _notifications.NotifyAsync(group.Key, NotificationTypes.AvailabilityApproved, new[] { count, encoded }, MyUrl);
                    else
                        await _notifications.NotifyAsync(group.Key, NotificationTypes.AvailabilityRejected, new[] { count, encoded, reason }, MyUrl);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify availability decision");
            }
        }

        #endregion
    }
}
