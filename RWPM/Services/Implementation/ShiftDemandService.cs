using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Common.Helper;
using RWPM.Common.Validation;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.ShiftDemand;
using RWPM.Services.Abstraction;
using R = RWPM.Resources.Shared.ShiftDemandResource;

namespace RWPM.Services.Implementation
{
    /// <summary>
    /// Nhu cầu nhân sự theo ca (mẫu theo thứ + ghi đè theo ngày) và độ phủ ca (tính trực tiếp từ ShiftRegistration).
    /// Không phụ thuộc IShiftRegistrationService để tránh vòng phụ thuộc DI khi luồng duyệt gọi ngược lại dịch vụ này.
    /// </summary>
    public class ShiftDemandService : IShiftDemandService
    {
        public const int MinRequired = 0;
        public const int MaxRequired = 100;
        public const int NoteMaxLength = 255;
        public const int MaxRangeDays = 93;
        public const int MaxOverrideDaysAhead = 366;
        private const int MaxTemplateCells = 7 * 200;

        private readonly DefaultDatabaseContext _context;
        private readonly IAvailabilityService _availabilityService;

        public ShiftDemandService(DefaultDatabaseContext context, IAvailabilityService availabilityService)
        {
            _context = context;
            _availabilityService = availabilityService;
        }

        private static void Require(bool condition, string field, string message)
        {
            if (!condition) throw new InputValidationException(field, message);
        }

        private static bool IsApplicable(DateTime effectiveFrom, DateTime? effectiveTo, DateTime date) =>
            effectiveFrom.Date <= date.Date && (!effectiveTo.HasValue || effectiveTo.Value.Date >= date.Date);

        private static string ShiftLabel(string shiftName) =>
            shiftName.TrimStart().StartsWith(R.Warn_ShiftPrefixCheck, StringComparison.CurrentCultureIgnoreCase)
                ? shiftName : string.Format(R.Warn_ShiftLabel, shiftName);

        // ===================== Phạm vi =====================

        public async Task<DemandScope> GetScopeAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return new DemandScope();
            var acc = await _context.Acc.AsNoTracking()
                .Where(a => a.Username == username && a.IsActive)
                .Select(a => new { a.Role })
                .SingleOrDefaultAsync();
            if (acc == null) return new DemandScope();

            var own = await _context.Employee.AsNoTracking()
                .Where(e => e.Username == username && e.IsActive)
                .Select(e => new
                {
                    e.EmployeeId,
                    e.StoreId,
                    StoreActive = e.Store != null && e.Store.IsActive,
                    StoreName = e.Store != null ? e.Store.StoreName : null
                })
                .FirstOrDefaultAsync();

            if (acc.Role is AccountRole.Admin or AccountRole.HR)
                return new DemandScope { CanAccess = true, AllStores = true, OwnEmployeeId = own?.EmployeeId };

            if (acc.Role == AccountRole.StoreManager && own?.StoreId != null && own.StoreActive)
                return new DemandScope
                {
                    CanAccess = true,
                    OwnStoreId = own.StoreId,
                    OwnStoreName = own.StoreName,
                    OwnEmployeeId = own.EmployeeId
                };

            return new DemandScope();
        }

        private async Task<DemandScope> EnsureStoreAccessAsync(string actor, int storeId)
        {
            var scope = await GetScopeAsync(actor);
            if (!scope.CanAccessStore(storeId)) throw new UnauthorizedAccessException(R.Err_NoStoreAccess);
            Require(await _context.Store.AnyAsync(s => s.StoreId == storeId && s.IsActive), "StoreId", R.Err_StoreNotFound);
            return scope;
        }

        public async Task<List<DemandStoreOption>> GetStoresAsync(DemandScope scope)
        {
            if (!scope.CanAccess) return new List<DemandStoreOption>();
            var query = _context.Store.AsNoTracking().Where(s => s.IsActive);
            if (!scope.AllStores) query = query.Where(s => s.StoreId == scope.OwnStoreId);
            return await query.OrderBy(s => s.StoreName)
                .Select(s => new DemandStoreOption { StoreId = s.StoreId, StoreCode = s.StoreCode, StoreName = s.StoreName })
                .ToListAsync();
        }

        public async Task<List<DemandShiftInfo>> GetStoreShiftsAsync(int storeId, DateTime? onDate = null)
        {
            var today = DateTime.Today;
            var query = _context.StoreShift.AsNoTracking()
                .Where(l => l.StoreId == storeId && l.IsActive && l.Store.IsActive && l.Shift.IsActive);
            if (onDate.HasValue)
            {
                var d = onDate.Value.Date;
                query = query.Where(l => l.Shift.EffectiveFrom <= d && (l.Shift.EffectiveTo == null || l.Shift.EffectiveTo >= d));
            }
            else
            {
                query = query.Where(l => l.Shift.EffectiveTo == null || l.Shift.EffectiveTo >= today);
            }

            var list = await query.Select(l => new DemandShiftInfo
            {
                ShiftId = l.ShiftId,
                ShiftName = l.Shift.ShiftName,
                StartTime = l.Shift.StartTime,
                EndTime = l.Shift.EndTime,
                EndDayOffset = l.Shift.EndDayOffset,
                EffectiveFrom = l.Shift.EffectiveFrom,
                EffectiveTo = l.Shift.EffectiveTo
            }).ToListAsync();

            return list.GroupBy(x => x.ShiftId).Select(g => g.First())
                .OrderBy(x => x.StartTime).ThenBy(x => x.ShiftName).ToList();
        }

        // ===================== Nhu cầu & độ phủ =====================

        public async Task<List<ShiftCoverageRow>> GetCoverageAsync(int? storeId, DateTime from, DateTime to)
        {
            var start = from.Date;
            var end = to.Date;
            Require(end >= start && (end - start).TotalDays < MaxRangeDays, "Date", string.Format(R.Err_DateRange, MaxRangeDays));

            // 1) Ca áp dụng tại chi nhánh trong khoảng ngày (1 truy vấn)
            var linkQuery = _context.StoreShift.AsNoTracking()
                .Where(l => l.IsActive && l.Store.IsActive && l.Shift.IsActive
                    && l.Shift.EffectiveFrom <= end && (l.Shift.EffectiveTo == null || l.Shift.EffectiveTo >= start));
            if (storeId.HasValue) linkQuery = linkQuery.Where(l => l.StoreId == storeId.Value);

            var links = (await linkQuery.Select(l => new
            {
                l.StoreId,
                l.Store.StoreName,
                l.ShiftId,
                l.Shift.ShiftName,
                l.Shift.StartTime,
                l.Shift.EndTime,
                l.Shift.EffectiveFrom,
                l.Shift.EffectiveTo
            }).ToListAsync())
                .GroupBy(x => (x.StoreId, x.ShiftId)).Select(g => g.First())
                .OrderBy(x => x.StoreName).ThenBy(x => x.StoreId).ThenBy(x => x.StartTime).ThenBy(x => x.ShiftName)
                .ToList();
            if (links.Count == 0) return new List<ShiftCoverageRow>();

            var storeIds = links.Select(x => x.StoreId).Distinct().ToList();
            var shiftIds = links.Select(x => x.ShiftId).Distinct().ToList();

            // 2) Nhu cầu: mẫu tuần + ghi đè trong khoảng (1 truy vấn)
            var demands = await _context.ShiftDemand.AsNoTracking()
                .Where(x => storeIds.Contains(x.StoreId) && shiftIds.Contains(x.ShiftId)
                    && (x.DayOfWeek != null || (x.Date >= start && x.Date <= end)))
                .Select(x => new { x.StoreId, x.ShiftId, x.DayOfWeek, x.Date, x.RequiredCount })
                .ToListAsync();

            // 3) Số đăng ký đã duyệt / chờ duyệt theo chi nhánh+ca+ngày (1 truy vấn gộp nhóm)
            var regCounts = await _context.ShiftRegistration.AsNoTracking()
                .Where(r => storeIds.Contains(r.StoreId) && shiftIds.Contains(r.ShiftId)
                    && r.WorkDate >= start && r.WorkDate <= end
                    && (r.Status == RegistrationStatus.Approved || r.Status == RegistrationStatus.Pending))
                .GroupBy(r => new { r.StoreId, r.ShiftId, r.WorkDate, r.Status })
                .Select(g => new { g.Key.StoreId, g.Key.ShiftId, g.Key.WorkDate, g.Key.Status, Count = g.Count() })
                .ToListAsync();

            var templateMap = new Dictionary<(int, int, DayOfWeek), int>();
            var overrideMap = new Dictionary<(int, int, DateTime), int>();
            foreach (var d in demands)
            {
                if (d.Date.HasValue) overrideMap[(d.StoreId, d.ShiftId, d.Date.Value.Date)] = d.RequiredCount;
                else if (d.DayOfWeek.HasValue) templateMap[(d.StoreId, d.ShiftId, d.DayOfWeek.Value)] = d.RequiredCount;
            }
            var approvedMap = new Dictionary<(int, int, DateTime), int>();
            var pendingMap = new Dictionary<(int, int, DateTime), int>();
            foreach (var c in regCounts)
            {
                var key = (c.StoreId, c.ShiftId, c.WorkDate.Date);
                var map = c.Status == RegistrationStatus.Approved ? approvedMap : pendingMap;
                map[key] = (map.TryGetValue(key, out var existing) ? existing : 0) + c.Count;
            }

            var result = new List<ShiftCoverageRow>();
            for (var day = start; day <= end; day = day.AddDays(1))
            {
                foreach (var link in links)
                {
                    if (!IsApplicable(link.EffectiveFrom, link.EffectiveTo, day)) continue;
                    var key = (link.StoreId, link.ShiftId, day);
                    int? required = null;
                    var isOverride = false;
                    if (overrideMap.TryGetValue(key, out var ov)) { required = ov; isOverride = true; }
                    else if (templateMap.TryGetValue((link.StoreId, link.ShiftId, day.DayOfWeek), out var tv)) required = tv;

                    result.Add(new ShiftCoverageRow
                    {
                        StoreId = link.StoreId,
                        StoreName = link.StoreName,
                        ShiftId = link.ShiftId,
                        ShiftName = link.ShiftName,
                        StartTime = link.StartTime,
                        EndTime = link.EndTime,
                        Date = day,
                        Required = required,
                        IsOverride = isOverride,
                        Approved = approvedMap.TryGetValue(key, out var a) ? a : 0,
                        Pending = pendingMap.TryGetValue(key, out var p) ? p : 0
                    });
                }
            }
            return result;
        }

        public async Task<ShiftCoverageRow?> GetCoverageForAsync(int storeId, int shiftId, DateTime date)
        {
            var rows = await GetCoverageAsync(storeId, date.Date, date.Date);
            return rows.FirstOrDefault(r => r.ShiftId == shiftId);
        }

        public async Task<List<string>> GetOverDemandWarningsAsync(IEnumerable<(int storeId, int shiftId, DateTime date, int adding)> items)
        {
            var list = (items ?? Enumerable.Empty<(int storeId, int shiftId, DateTime date, int adding)>())
                .Where(i => i.adding > 0)
                .GroupBy(i => (i.storeId, i.shiftId, Date: i.date.Date))
                .Select(g => (g.Key.storeId, g.Key.shiftId, g.Key.Date, Adding: g.Sum(x => x.adding)))
                .OrderBy(x => x.Date).ToList();
            var warnings = new List<string>();
            if (list.Count == 0) return warnings;

            var storeIds = list.Select(x => x.storeId).Distinct().ToList();
            var shiftIds = list.Select(x => x.shiftId).Distinct().ToList();
            var minDate = list.Min(x => x.Date);
            var maxDate = list.Max(x => x.Date);

            var demands = await _context.ShiftDemand.AsNoTracking()
                .Where(x => storeIds.Contains(x.StoreId) && shiftIds.Contains(x.ShiftId)
                    && (x.DayOfWeek != null || (x.Date >= minDate && x.Date <= maxDate)))
                .Select(x => new { x.StoreId, x.ShiftId, x.DayOfWeek, x.Date, x.RequiredCount })
                .ToListAsync();
            var approved = await _context.ShiftRegistration.AsNoTracking()
                .Where(r => storeIds.Contains(r.StoreId) && shiftIds.Contains(r.ShiftId)
                    && r.WorkDate >= minDate && r.WorkDate <= maxDate && r.Status == RegistrationStatus.Approved)
                .GroupBy(r => new { r.StoreId, r.ShiftId, r.WorkDate })
                .Select(g => new { g.Key.StoreId, g.Key.ShiftId, g.Key.WorkDate, Count = g.Count() })
                .ToListAsync();
            var shiftNames = await _context.Shift.AsNoTracking().Where(s => shiftIds.Contains(s.ShiftId))
                .Select(s => new { s.ShiftId, s.ShiftName }).ToDictionaryAsync(s => s.ShiftId, s => s.ShiftName);
            var storeNames = await _context.Store.AsNoTracking().Where(s => storeIds.Contains(s.StoreId))
                .Select(s => new { s.StoreId, s.StoreName }).ToDictionaryAsync(s => s.StoreId, s => s.StoreName);

            foreach (var item in list)
            {
                var ov = demands.FirstOrDefault(d => d.StoreId == item.storeId && d.ShiftId == item.shiftId && d.Date.HasValue && d.Date.Value.Date == item.Date);
                var tpl = demands.FirstOrDefault(d => d.StoreId == item.storeId && d.ShiftId == item.shiftId && d.DayOfWeek == item.Date.DayOfWeek);
                int? required = ov?.RequiredCount ?? tpl?.RequiredCount;
                if (!required.HasValue) continue;

                var current = approved.Where(a => a.StoreId == item.storeId && a.ShiftId == item.shiftId && a.WorkDate.Date == item.Date).Sum(a => a.Count);
                var total = current + item.Adding;
                if (total <= required.Value) continue;

                warnings.Add(string.Format(R.Warn_OverDemand,
                    ShiftLabel(shiftNames.TryGetValue(item.shiftId, out var sn) ? sn : $"#{item.shiftId}"),
                    item.Date.ToString("dd/MM"),
                    storeNames.TryGetValue(item.storeId, out var st) ? st : $"#{item.storeId}",
                    required.Value, total, total - required.Value));
            }
            return warnings;
        }

        // ===================== Mẫu theo tuần =====================

        public async Task<WeeklyTemplateVM> GetWeeklyTemplateAsync(int storeId)
        {
            var shifts = await GetStoreShiftsAsync(storeId);
            var rows = await _context.ShiftDemand.AsNoTracking()
                .Where(x => x.StoreId == storeId && x.DayOfWeek != null)
                .Select(x => new { x.ShiftId, x.DayOfWeek, x.RequiredCount })
                .ToListAsync();
            var values = new Dictionary<string, int>();
            foreach (var r in rows) values[WeeklyTemplateVM.Key(r.ShiftId, r.DayOfWeek!.Value)] = r.RequiredCount;
            return new WeeklyTemplateVM { StoreId = storeId, Shifts = shifts, Values = values };
        }

        public async Task SaveWeeklyTemplateAsync(int storeId, IEnumerable<WeeklyTemplateCellInput> cells, string actor)
        {
            await EnsureStoreAccessAsync(actor, storeId);
            var input = (cells ?? Enumerable.Empty<WeeklyTemplateCellInput>()).Where(c => c != null).ToList();
            Require(input.Count <= MaxTemplateCells, "Cells", R.Err_InvalidInput);
            if (input.Count == 0) return;

            var applicable = (await GetStoreShiftsAsync(storeId)).ToDictionary(x => x.ShiftId);
            var seen = new HashSet<(int, int)>();
            foreach (var c in input)
            {
                Require(applicable.ContainsKey(c.ShiftId), "ShiftId", R.Err_ShiftNotInStore);
                Require(c.DayOfWeek is >= 0 and <= 6, "DayOfWeek", R.Err_DayOfWeek);
                Require(!c.RequiredCount.HasValue || c.RequiredCount.Value is >= MinRequired and <= MaxRequired, "RequiredCount",
                    string.Format(R.Err_RequiredRange, MinRequired, MaxRequired));
                Require(seen.Add((c.ShiftId, c.DayOfWeek)), "Cells", R.Err_DuplicateCell);
            }

            var shiftIds = input.Select(c => c.ShiftId).Distinct().ToList();
            var existing = await _context.ShiftDemand
                .Where(x => x.StoreId == storeId && x.DayOfWeek != null && shiftIds.Contains(x.ShiftId))
                .ToListAsync();
            var now = DateTime.Now;
            foreach (var c in input)
            {
                var dow = (DayOfWeek)c.DayOfWeek;
                var row = existing.FirstOrDefault(x => x.ShiftId == c.ShiftId && x.DayOfWeek == dow);
                if (!c.RequiredCount.HasValue)
                {
                    if (row != null) _context.ShiftDemand.Remove(row);
                    continue;
                }
                if (row == null)
                {
                    _context.ShiftDemand.Add(new ShiftDemand
                    {
                        StoreId = storeId,
                        ShiftId = c.ShiftId,
                        DayOfWeek = dow,
                        Date = null,
                        RequiredCount = c.RequiredCount.Value,
                        CreatedBy = actor,
                        CreatedDate = now
                    });
                }
                else if (row.RequiredCount != c.RequiredCount.Value)
                {
                    row.RequiredCount = c.RequiredCount.Value;
                    row.UpdatedBy = actor;
                    row.UpdatedDate = now;
                }
            }
            await SaveChangesMappedAsync();
        }

        public async Task<int> CopyTemplateAsync(int fromStoreId, int toStoreId, string actor)
        {
            var scope = await EnsureStoreAccessAsync(actor, toStoreId);
            if (!scope.AllStores) throw new UnauthorizedAccessException(R.Err_CopyForbidden);
            Require(fromStoreId != toStoreId, "FromStoreId", R.Err_CopySameStore);
            Require(await _context.Store.AnyAsync(s => s.StoreId == fromStoreId && s.IsActive), "FromStoreId", R.Err_StoreNotFound);

            var targetShiftIds = (await GetStoreShiftsAsync(toStoreId)).Select(x => x.ShiftId).ToList();
            if (targetShiftIds.Count == 0) return 0;

            var source = await _context.ShiftDemand.AsNoTracking()
                .Where(x => x.StoreId == fromStoreId && x.DayOfWeek != null && targetShiftIds.Contains(x.ShiftId))
                .Select(x => new { x.ShiftId, x.DayOfWeek, x.RequiredCount })
                .ToListAsync();
            // Chỉ thay thế mẫu của các ca mà chi nhánh nguồn có áp dụng (ca có cấu hình hoặc gắn ở nguồn).
            var sourceShiftIds = await _context.StoreShift.AsNoTracking()
                .Where(l => l.StoreId == fromStoreId && l.IsActive && targetShiftIds.Contains(l.ShiftId))
                .Select(l => l.ShiftId).Distinct().ToListAsync();
            var affected = sourceShiftIds.Union(source.Select(s => s.ShiftId)).ToHashSet();
            if (affected.Count == 0) return 0;

            var target = await _context.ShiftDemand
                .Where(x => x.StoreId == toStoreId && x.DayOfWeek != null && affected.Contains(x.ShiftId))
                .ToListAsync();
            var now = DateTime.Now;
            foreach (var row in target)
            {
                var src = source.FirstOrDefault(s => s.ShiftId == row.ShiftId && s.DayOfWeek == row.DayOfWeek);
                if (src == null) _context.ShiftDemand.Remove(row);
                else if (row.RequiredCount != src.RequiredCount)
                {
                    row.RequiredCount = src.RequiredCount;
                    row.UpdatedBy = actor;
                    row.UpdatedDate = now;
                }
            }
            foreach (var src in source.Where(s => !target.Any(t => t.ShiftId == s.ShiftId && t.DayOfWeek == s.DayOfWeek)))
            {
                _context.ShiftDemand.Add(new ShiftDemand
                {
                    StoreId = toStoreId,
                    ShiftId = src.ShiftId,
                    DayOfWeek = src.DayOfWeek,
                    RequiredCount = src.RequiredCount,
                    CreatedBy = actor,
                    CreatedDate = now
                });
            }
            await SaveChangesMappedAsync();
            return source.Count;
        }

        // ===================== Ghi đè theo ngày =====================

        public async Task<List<ShiftDemandOverrideItem>> GetOverridesAsync(int storeId, int pastDays = 30)
        {
            var today = DateTime.Today;
            var fromDate = today.AddDays(-Math.Max(0, pastDays));
            var rows = await _context.ShiftDemand.AsNoTracking()
                .Where(x => x.StoreId == storeId && x.Date != null && x.Date >= fromDate)
                .Select(x => new
                {
                    x.ShiftDemandId,
                    x.StoreId,
                    x.ShiftId,
                    x.Shift.ShiftName,
                    x.Shift.StartTime,
                    x.Shift.EndTime,
                    x.Shift.EndDayOffset,
                    Date = x.Date!.Value,
                    x.RequiredCount,
                    x.Note,
                    x.CreatedBy,
                    x.CreatedDate,
                    x.UpdatedBy,
                    x.UpdatedDate
                })
                .ToListAsync();
            if (rows.Count == 0) return new List<ShiftDemandOverrideItem>();

            var shiftIds = rows.Select(r => r.ShiftId).Distinct().ToList();
            var templates = await _context.ShiftDemand.AsNoTracking()
                .Where(x => x.StoreId == storeId && x.DayOfWeek != null && shiftIds.Contains(x.ShiftId))
                .Select(x => new { x.ShiftId, x.DayOfWeek, x.RequiredCount })
                .ToListAsync();

            var items = rows.Select(r => new ShiftDemandOverrideItem
            {
                ShiftDemandId = r.ShiftDemandId,
                StoreId = r.StoreId,
                ShiftId = r.ShiftId,
                ShiftName = r.ShiftName,
                TimeRange = DemandShiftInfo.FormatRange(r.StartTime, r.EndTime, r.EndDayOffset > 0 || r.EndTime <= r.StartTime),
                Date = r.Date.Date,
                RequiredCount = r.RequiredCount,
                Note = r.Note,
                TemplateValue = templates.FirstOrDefault(t => t.ShiftId == r.ShiftId && t.DayOfWeek == r.Date.DayOfWeek)?.RequiredCount,
                IsPast = r.Date.Date < today,
                ModifiedBy = r.UpdatedBy ?? r.CreatedBy,
                ModifiedAt = r.UpdatedDate ?? r.CreatedDate
            }).ToList();

            return items.Where(i => !i.IsPast).OrderBy(i => i.Date).ThenBy(i => i.TimeRange)
                .Concat(items.Where(i => i.IsPast).OrderByDescending(i => i.Date).ThenBy(i => i.TimeRange))
                .ToList();
        }

        public async Task<int> SaveOverrideAsync(ShiftDemandOverrideInput input, string actor)
        {
            Require(input != null, "Input", R.Err_InvalidInput);
            await EnsureStoreAccessAsync(actor, input!.StoreId);

            var today = DateTime.Today;
            Require(input.Date.HasValue, "Date", R.Err_DateRequired);
            var date = input.Date!.Value.Date;
            Require(date >= today, "Date", R.Err_DatePast);
            Require(date <= today.AddDays(MaxOverrideDaysAhead), "Date", R.Err_DateTooFar);
            Require(input.RequiredCount.HasValue && input.RequiredCount.Value is >= MinRequired and <= MaxRequired, "RequiredCount",
                string.Format(R.Err_RequiredRange, MinRequired, MaxRequired));
            var note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
            Require(note == null || note.Length <= NoteMaxLength, "Note", string.Format(R.Err_NoteTooLong, NoteMaxLength));

            var applicable = await GetStoreShiftsAsync(input.StoreId, date);
            Require(applicable.Any(s => s.ShiftId == input.ShiftId), "ShiftId", R.Err_ShiftNotApplicable);

            ShiftDemand? entity = null;
            var id = input.Id ?? 0;
            if (id > 0)
            {
                entity = await _context.ShiftDemand.FirstOrDefaultAsync(x => x.ShiftDemandId == id && x.Date != null);
                Require(entity != null, "Id", R.Err_OverrideNotFound);
                Require(entity!.StoreId == input.StoreId, "StoreId", R.Err_OverrideNotFound);
                Require(entity.Date!.Value.Date >= today, "Date", R.Err_OverridePastLocked);
            }

            var duplicate = await _context.ShiftDemand.AnyAsync(x => x.StoreId == input.StoreId && x.ShiftId == input.ShiftId
                && x.Date == date && x.ShiftDemandId != id);
            Require(!duplicate, "Date", string.Format(R.Err_OverrideExists, date.ToString("dd/MM/yyyy")));

            var now = DateTime.Now;
            if (entity == null)
            {
                entity = new ShiftDemand
                {
                    StoreId = input.StoreId,
                    ShiftId = input.ShiftId,
                    Date = date,
                    DayOfWeek = null,
                    RequiredCount = input.RequiredCount!.Value,
                    Note = note,
                    CreatedBy = actor,
                    CreatedDate = now
                };
                _context.ShiftDemand.Add(entity);
            }
            else
            {
                entity.ShiftId = input.ShiftId;
                entity.Date = date;
                entity.RequiredCount = input.RequiredCount!.Value;
                entity.Note = note;
                entity.UpdatedBy = actor;
                entity.UpdatedDate = now;
            }
            await SaveChangesMappedAsync(date);
            return entity.ShiftDemandId;
        }

        public async Task DeleteOverrideAsync(int shiftDemandId, string actor)
        {
            var entity = await _context.ShiftDemand.FirstOrDefaultAsync(x => x.ShiftDemandId == shiftDemandId && x.Date != null);
            Require(entity != null, "Id", R.Err_OverrideNotFound);
            await EnsureStoreAccessAsync(actor, entity!.StoreId);
            Require(entity.Date!.Value.Date >= DateTime.Today, "Date", R.Err_OverridePastLocked);
            _context.ShiftDemand.Remove(entity);
            await _context.SaveChangesAsync();
        }

        private async Task SaveChangesMappedAsync(DateTime? date = null)
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when ((ex.InnerException?.Message ?? "").Contains("IX_ShiftDemand"))
            {
                throw new InputValidationException("Date", date.HasValue
                    ? string.Format(R.Err_OverrideExists, date.Value.ToString("dd/MM/yyyy"))
                    : R.Err_DuplicateCell);
            }
        }

        // ===================== Chi tiết ô độ phủ =====================

        public async Task<CoverageCellDetail> GetCellDetailAsync(int storeId, int shiftId, DateTime date, string actor)
        {
            var scope = await EnsureStoreAccessAsync(actor, storeId);
            var d = date.Date;
            var coverage = await GetCoverageForAsync(storeId, shiftId, d);
            Require(coverage != null, "ShiftId", R.Err_ShiftNotApplicable);
            var shift = await _context.Shift.AsNoTracking().FirstAsync(s => s.ShiftId == shiftId);

            var regs = await _context.ShiftRegistration.AsNoTracking()
                .Where(r => r.StoreId == storeId && r.ShiftId == shiftId && r.WorkDate == d
                    && (r.Status == RegistrationStatus.Approved || r.Status == RegistrationStatus.Pending))
                .Select(r => new
                {
                    r.Status,
                    Item = new CoverageEmployeeItem
                    {
                        ShiftRegistrationId = r.ShiftRegistrationId,
                        EmployeeId = r.EmployeeId,
                        EmployeeCode = r.Employee.EmployeeCode,
                        FullName = r.Employee.Account.FullName,
                        Note = r.Note,
                        ApprovedBy = r.ApprovedBy,
                        ApprovedAt = r.ApprovedAt,
                        CreatedDate = r.CreatedDate,
                        CreatedBy = r.CreatedBy
                    }
                })
                .ToListAsync();

            var isPast = d < DateTime.Today;
            var suggestions = isPast ? new List<CoverageSuggestion>() : await BuildSuggestionsAsync(storeId, shift, d, scope.OwnEmployeeId);

            return new CoverageCellDetail
            {
                Coverage = coverage!,
                TimeRange = DemandShiftInfo.FormatRange(shift.StartTime, shift.EndTime, shift.EndDayOffset > 0 || shift.EndTime <= shift.StartTime),
                IsPast = isPast,
                Approved = regs.Where(r => r.Status == RegistrationStatus.Approved).Select(r => r.Item).OrderBy(x => x.FullName).ToList(),
                Pending = regs.Where(r => r.Status == RegistrationStatus.Pending).Select(r => r.Item).OrderBy(x => x.CreatedDate).ToList(),
                Suggestions = suggestions
            };
        }

        /// <summary>
        /// Nhân viên đang hoạt động của chi nhánh chưa đăng ký ca/ngày này. Loại trừ/đánh dấu:
        /// trùng giờ với ca khác (cùng quy tắc ShiftRegistrationService.ValidateScheduleConstraintsAsync, xét cả ca qua đêm ngày trước/sau),
        /// nghỉ phép đã duyệt, khả dụng (IAvailabilityService.CheckManyAsync), chính người phân công.
        /// </summary>
        private async Task<List<CoverageSuggestion>> BuildSuggestionsAsync(int storeId, Shift shift, DateTime d, int? actorEmployeeId)
        {
            var employees = await _context.Employee.AsNoTracking()
                .Where(e => e.StoreId == storeId && e.IsActive && e.Account.IsActive
                    && e.JoinDate <= d && (e.ResignDate == null || e.ResignDate > d))
                .Select(e => new { e.EmployeeId, e.EmployeeCode, e.Account.FullName })
                .ToListAsync();
            if (employees.Count == 0) return new List<CoverageSuggestion>();
            var empIds = employees.Select(e => e.EmployeeId).ToList();

            // Mọi đăng ký (kể cả bị từ chối) của cùng ca/ngày: ShiftRegistrationService.CreateAsync không cho tạo trùng.
            var sameShift = await _context.ShiftRegistration.AsNoTracking()
                .Where(r => empIds.Contains(r.EmployeeId) && r.ShiftId == shift.ShiftId && r.WorkDate == d)
                .Select(r => new { r.EmployeeId, r.Status })
                .ToListAsync();

            var minDate = d.AddDays(-1);
            var maxDate = d.AddDays(1);
            var nearby = await _context.ShiftRegistration.AsNoTracking()
                .Where(r => empIds.Contains(r.EmployeeId) && r.WorkDate >= minDate && r.WorkDate <= maxDate
                    && r.Status != RegistrationStatus.Rejected && !(r.ShiftId == shift.ShiftId && r.WorkDate == d))
                .Select(r => new { r.EmployeeId, r.WorkDate, r.Shift.ShiftName, r.Shift.StartTime, r.Shift.EndTime, r.Shift.EndDayOffset })
                .ToListAsync();

            var dayEnd = d.AddDays(1);
            var leaves = await _context.LeaveRequest.AsNoTracking()
                .Where(l => empIds.Contains(l.EmployeeId) && l.Status == RegistrationStatus.Approved
                    && l.StartDate < dayEnd && l.EndDate >= minDate)
                .Select(l => new { l.EmployeeId, l.StartDate, l.EndDate })
                .ToListAsync();
            var onLeave = leaves.Where(l => l.StartDate.Date <= d && l.EndDate.Date >= d).Select(l => l.EmployeeId).ToHashSet();

            var (start, end) = ShiftTimeHelper.GetDateTimeRange(d, shift);
            var overlapByEmployee = new Dictionary<int, string>();
            foreach (var r in nearby)
            {
                if (overlapByEmployee.ContainsKey(r.EmployeeId)) continue;
                var other = new Shift { StartTime = r.StartTime, EndTime = r.EndTime, EndDayOffset = r.EndDayOffset };
                var (oStart, oEnd) = ShiftTimeHelper.GetDateTimeRange(r.WorkDate, other);
                if (start < oEnd && oStart < end)
                    overlapByEmployee[r.EmployeeId] = string.Format(R.Reason_Overlap, r.ShiftName, r.WorkDate.ToString("dd/MM"));
            }

            var registeredIds = sameShift.Select(s => s.EmployeeId).ToHashSet();
            var rejectedIds = sameShift.Where(s => s.Status == RegistrationStatus.Rejected).Select(s => s.EmployeeId).ToHashSet();

            var candidateIds = employees.Where(e => !registeredIds.Contains(e.EmployeeId) || rejectedIds.Contains(e.EmployeeId))
                .Select(e => e.EmployeeId).ToList();
            var availability = candidateIds.Count == 0
                ? new Dictionary<int, AvailabilityCheckResult>()
                : await _availabilityService.CheckManyAsync(candidateIds, d, shift);

            var result = new List<CoverageSuggestion>();
            foreach (var e in employees)
            {
                // Đã đăng ký đã duyệt / chờ duyệt: đã nằm trong danh sách tương ứng.
                if (registeredIds.Contains(e.EmployeeId) && !rejectedIds.Contains(e.EmployeeId)) continue;

                availability.TryGetValue(e.EmployeeId, out var check);
                string? reason = null;
                if (actorEmployeeId == e.EmployeeId) reason = R.Reason_Self;
                else if (rejectedIds.Contains(e.EmployeeId)) reason = R.Reason_Rejected;
                else if (onLeave.Contains(e.EmployeeId)) reason = R.Reason_OnLeave;
                else if (overlapByEmployee.TryGetValue(e.EmployeeId, out var overlap)) reason = overlap;
                else if (check != null && !check.IsAvailable) reason = string.IsNullOrWhiteSpace(check.Message) ? R.Reason_Unavailable : check.Message;

                result.Add(new CoverageSuggestion
                {
                    EmployeeId = e.EmployeeId,
                    EmployeeCode = e.EmployeeCode,
                    FullName = e.FullName,
                    IsAvailable = reason == null,
                    Reason = reason,
                    HasDeclaredAvailability = check?.HasDeclaredAvailability ?? false
                });
            }
            return result.OrderByDescending(x => x.IsAvailable).ThenBy(x => x.FullName).ThenBy(x => x.EmployeeCode).ToList();
        }
    }
}
