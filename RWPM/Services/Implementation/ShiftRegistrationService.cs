using RWPM.Common.Validation;
using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Services.Abstraction;
using SR = RWPM.Resources.Shared.ShiftRegistrationResource;

namespace RWPM.Services.Implementation
{
    public class ShiftRegistrationService : IShiftRegistrationService
    {
        private readonly DefaultDatabaseContext _context;
        private readonly IAvailabilityService _availabilityService;
        private readonly IShiftDemandService _demandService;

        public ShiftRegistrationService(DefaultDatabaseContext context, IAvailabilityService availabilityService, IShiftDemandService demandService)
        {
            _context = context;
            _availabilityService = availabilityService;
            _demandService = demandService;
        }

        public async Task<List<ShiftRegistration>> GetEventsAsync(DateTime start, DateTime end, int? employeeId = null, int? storeId = null)
        {
            var query = _context.ShiftRegistration
                .Include(x => x.Employee)
                .ThenInclude(e => e.Account)
                .Include(x => x.Shift)
                .Include(x => x.Store)
                .Where(x => x.WorkDate >= start && x.WorkDate <= end && x.Status != RegistrationStatus.Rejected);

            if (employeeId.HasValue)
            {
                query = query.Where(x => x.EmployeeId == employeeId.Value);
            }

            if (storeId.HasValue)
            {
                query = query.Where(x => x.StoreId == storeId.Value);
            }

            return await query.ToListAsync();
        }

        public async Task<List<ShiftRegistration>> GetRequestsAsync(DateTime start, DateTime end, RegistrationStatus? status = null)
        {
            var query = _context.ShiftRegistration
                .Include(x => x.Employee)
                .ThenInclude(e => e.Account)
                .Include(x => x.Shift)
                .Include(x => x.Store)
                .Where(x => x.WorkDate >= start && x.WorkDate <= end);

            if (status.HasValue)
            {
                query = query.Where(x => x.Status == status.Value);
            }

            return await query.OrderByDescending(x => x.WorkDate).ToListAsync();
        }

        public async Task<ShiftRegistration?> GetByIdAsync(int id)
        {
            return await _context.ShiftRegistration
                .Include(x => x.Employee)
                .Include(x => x.Shift)
                .FirstOrDefaultAsync(x => x.ShiftRegistrationId == id);
        }

        public async Task<ShiftRegistration> CreateAsync(ShiftRegistration entity)
        {
            await using var scheduleTransaction = _context.Database.CurrentTransaction == null
                ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;

            if (entity.WorkDate.Date < DateTime.Today)
            {
                throw new InvalidOperationException(SR.Err_PastDateRegistration);
            }

            await ValidateScheduleConstraintsAsync(entity.EmployeeId, new List<ShiftRegistration> { entity });

            var exists = await _context.ShiftRegistration.AnyAsync(x => 
                x.EmployeeId == entity.EmployeeId && 
                x.ShiftId == entity.ShiftId && 
                x.WorkDate == entity.WorkDate);

            if (exists)
            {
                throw new InvalidOperationException(SR.Err_AlreadyRegistered);
            }

            _context.ShiftRegistration.Add(entity);
            await _context.SaveChangesAsync();
            if (scheduleTransaction != null) await scheduleTransaction.CommitAsync();
            return entity;
        }

        public async Task<List<ShiftRegistration>> CreateBatchAsync(List<ShiftRegistration> entities, bool enforceAvailability = false)
        {
            await using var scheduleTransaction = _context.Database.CurrentTransaction == null
                ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;

            if (!entities.Any()) return entities;

            if (entities.Any(e => e.WorkDate.Date < DateTime.Today))
            {
                throw new InvalidOperationException(SR.Err_PastDateRegistration);
            }

            var employeeId = entities.First().EmployeeId;
            await ValidateScheduleConstraintsAsync(employeeId, entities);
            if (enforceAvailability) await EnsureAvailableAsync(employeeId, entities);

            foreach (var entity in entities)
            {
                var exists = await _context.ShiftRegistration.AnyAsync(x => 
                    x.EmployeeId == entity.EmployeeId && 
                    x.ShiftId == entity.ShiftId && 
                    x.WorkDate == entity.WorkDate);

                if (exists)
                {
                    var shift = await _context.Shift.FindAsync(entity.ShiftId);
                    throw new InvalidOperationException(string.Format(SR.Err_ShiftAlreadyRegisteredOnDate, shift?.ShiftName, entity.WorkDate.ToString("dd/MM/yyyy")));
                }

                _context.ShiftRegistration.Add(entity);
            }

            await _context.SaveChangesAsync();
            if (scheduleTransaction != null) await scheduleTransaction.CommitAsync();
            return entities;
        }

        public async Task<ShiftRegistration> UpdateAsync(ShiftRegistration entity)
        {
            await using var scheduleTransaction = _context.Database.CurrentTransaction == null
                ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;

            await ValidateScheduleConstraintsAsync(entity.EmployeeId, new List<ShiftRegistration> { entity }, new List<int> { entity.ShiftRegistrationId });

            var exists = await _context.ShiftRegistration.AnyAsync(x => 
                x.EmployeeId == entity.EmployeeId && 
                x.ShiftId == entity.ShiftId && 
                x.WorkDate == entity.WorkDate && 
                x.ShiftRegistrationId != entity.ShiftRegistrationId);

            if (exists)
            {
                throw new InvalidOperationException(SR.Err_AlreadyRegistered);
            }

            _context.ShiftRegistration.Update(entity);
            await _context.SaveChangesAsync();
            if (scheduleTransaction != null) await scheduleTransaction.CommitAsync();
            return entity;
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await _context.ShiftRegistration.FindAsync(id);
            if (entity != null)
            {
                await WorkflowGuard.UnlockedPeriodAsync(_context, entity.WorkDate.Date, entity.WorkDate.Date);
                InputRules.Require(!await _context.ShiftSwapRequest.AnyAsync(x => x.SourceRegistrationId == id || x.TargetRegistrationId == id),
                    "Id", "Không thể xóa đăng ký ca này vì đã nằm trong một đơn đổi ca.", "This registration cannot be deleted because it is part of a shift swap request.");
                _context.ShiftRegistration.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateStatusAsync(int id, RegistrationStatus status, string? rejectionReason = null, string? approver = null)
        {
            InputRules.Decision(status);
            if (status == RegistrationStatus.Rejected) rejectionReason = InputRules.Reason(rejectionReason);
            await using var transaction = _context.Database.CurrentTransaction == null
                ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;
            var entity = await _context.ShiftRegistration
                .Include(x => x.Employee)
                .ThenInclude(e => e.Account)
                .FirstOrDefaultAsync(x => x.ShiftRegistrationId == id);
                
            InputRules.Require(entity != null, "Id", "Không tìm thấy đăng ký ca.", "Registration not found.");
            if (entity != null)
            {
                await _context.Entry(entity).ReloadAsync();
                await WorkflowGuard.EmployeeAsync(_context, approver ?? "", entity.EmployeeId, true);
                InputRules.Require(entity.Status == RegistrationStatus.Pending, "Status", "Đăng ký đã được xử lý.", "Registration already processed.");
                await WorkflowGuard.UnlockedPeriodAsync(_context, entity.WorkDate.Date, entity.WorkDate.Date);
                if (status == RegistrationStatus.Approved)
                {
                    await ValidateScheduleConstraintsAsync(entity.EmployeeId, new List<ShiftRegistration> { entity }, new List<int> { id });
                    entity.RejectionReason = null;
                }
                else if (status == RegistrationStatus.Rejected)
                {
                    entity.RejectionReason = rejectionReason;
                }

                entity.Status = status;
                entity.ApprovedBy = approver;
                entity.ApprovedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }
            if (transaction != null) await transaction.CommitAsync();
        }

        public async Task UpdateBulkStatusAsync(List<int> ids, RegistrationStatus status, string? rejectionReason = null, string? approver = null)
        {
            InputRules.Decision(status);
            if (status == RegistrationStatus.Rejected) rejectionReason = InputRules.Reason(rejectionReason);
            await using var transaction = _context.Database.CurrentTransaction == null
                ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;
            var entities = await _context.ShiftRegistration
                .Include(x => x.Employee)
                .ThenInclude(e => e.Account)
                .Where(x => ids.Contains(x.ShiftRegistrationId))
                .ToListAsync();

            InputRules.Require(ids.Count > 0 && ids.Distinct().Count() == ids.Count && entities.Count == ids.Count, "Ids", "Không tìm thấy đầy đủ các đăng ký.", "Some registrations were not found.");
            foreach (var item in entities)
            {
                await _context.Entry(item).ReloadAsync();
                await WorkflowGuard.EmployeeAsync(_context, approver ?? "", item.EmployeeId, true);
                InputRules.Require(item.Status == RegistrationStatus.Pending, "Status", "Đăng ký đã được xử lý.", "Registration already processed.");
                await WorkflowGuard.UnlockedPeriodAsync(_context, item.WorkDate.Date, item.WorkDate.Date);
            }

            if (status == RegistrationStatus.Approved)
            {
                foreach (var group in entities.GroupBy(x => x.EmployeeId))
                    await ValidateScheduleConstraintsAsync(group.Key, group.ToList(), ids);
            }

            foreach (var entity in entities)
            {
                entity.Status = status;
                entity.ApprovedBy = approver;
                entity.ApprovedAt = DateTime.Now;
                if (status == RegistrationStatus.Approved)
                {
                    entity.RejectionReason = null;
                }
                else if (status == RegistrationStatus.Rejected)
                {
                    entity.RejectionReason = rejectionReason;
                }
            }

            await _context.SaveChangesAsync();
            if (transaction != null) await transaction.CommitAsync();
        }

        public async Task SyncWeeklyRegistrationAsync(int employeeId, int storeId, DateTime startOfWeek, List<ShiftRegistration> desiredRegistrations, bool enforceAvailability = false)
        {
            await using var scheduleTransaction = _context.Database.CurrentTransaction == null
                ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;

            var endOfWeek = startOfWeek.AddDays(6);
            
            // 1. Get existing registrations for this week
            var existingRegistrations = await _context.ShiftRegistration
                .Where(x => x.EmployeeId == employeeId && x.WorkDate >= startOfWeek && x.WorkDate <= endOfWeek)
                .ToListAsync();

            // 2. Find missing ones in desired, delete them if they are Pending
            var desiredKeys = desiredRegistrations.Select(x => $"{x.ShiftId}_{x.WorkDate:yyyy-MM-dd}").ToList();
            
            var toDelete = existingRegistrations
                .Where(x => x.Status == RegistrationStatus.Pending && !desiredKeys.Contains($"{x.ShiftId}_{x.WorkDate:yyyy-MM-dd}"))
                .ToList();
            
            _context.ShiftRegistration.RemoveRange(toDelete);

            // 3. Find new ones to add
            var existingKeys = existingRegistrations.Select(x => $"{x.ShiftId}_{x.WorkDate:yyyy-MM-dd}").ToList();
            
            var toAdd = desiredRegistrations
                .Where(x => !existingKeys.Contains($"{x.ShiftId}_{x.WorkDate:yyyy-MM-dd}"))
                .ToList();

            foreach (var item in toAdd)
            {
                item.EmployeeId = employeeId;
                item.StoreId = storeId;
                item.Status = RegistrationStatus.Pending;
                item.CreatedDate = DateTime.Now;
            }

            var ignoreIds = toDelete.Select(x => x.ShiftRegistrationId).ToList();
            await ValidateScheduleConstraintsAsync(employeeId, toAdd, ignoreIds);
            // Chỉ chặn các ca MỚI thêm; ca chờ duyệt sẵn có được giữ nguyên (nhân viên có thể bỏ chọn để hủy)
            if (enforceAvailability) await EnsureAvailableAsync(employeeId, toAdd);

            foreach (var item in toAdd)
            {
                _context.ShiftRegistration.Add(item);
            }

            await _context.SaveChangesAsync();
            if (scheduleTransaction != null) await scheduleTransaction.CommitAsync();
        }

        // ===================== Tích hợp thời gian có thể làm việc / nhu cầu ca =====================

        private const int MaxBlockedRangeDays = 42;

        /// <summary>Chặn (tất cả hoặc không) nếu có ca không phù hợp thời gian có thể làm việc; thông báo liệt kê ngày + ca.</summary>
        private async Task EnsureAvailableAsync(int employeeId, List<ShiftRegistration> registrations)
        {
            if (registrations.Count == 0) return;
            var issues = await GetAvailabilityIssuesAsync(employeeId, registrations.Select(r => (r.ShiftId, r.WorkDate)));
            if (issues.Count == 0) return;
            var lines = issues.Select(i => string.Format(SR.Avail_IssueLine, i.Date.ToString("dd/MM/yyyy"), i.ShiftName, i.Message));
            throw new InvalidOperationException(string.Format(SR.Err_AvailabilityBlocked, string.Join("\n", lines)));
        }

        public async Task<List<ScheduleAvailabilityIssue>> GetAvailabilityIssuesAsync(int employeeId, IEnumerable<(int shiftId, DateTime date)> slots)
        {
            var list = (slots ?? Enumerable.Empty<(int shiftId, DateTime date)>())
                .Select(x => (ShiftId: x.shiftId, Date: x.date.Date)).Distinct().ToList();
            var issues = new List<ScheduleAvailabilityIssue>();
            if (list.Count == 0) return issues;

            var shiftIds = list.Select(x => x.ShiftId).Distinct().ToList();
            var shifts = await _context.Shift.AsNoTracking().Where(x => shiftIds.Contains(x.ShiftId)).ToDictionaryAsync(x => x.ShiftId);
            var items = list.Where(x => shifts.ContainsKey(x.ShiftId)).Select(x => (employeeId, x.Date, shifts[x.ShiftId])).ToList();
            if (items.Count == 0) return issues;

            var checks = await _availabilityService.CheckAssignmentsAsync(items);
            var name = await EmployeeNameAsync(employeeId);
            foreach (var item in items.OrderBy(x => x.Date).ThenBy(x => x.Item3.StartTime))
            {
                if (!checks.TryGetValue((employeeId, item.Date, item.Item3.ShiftId), out var r) || r.IsAvailable) continue;
                issues.Add(new ScheduleAvailabilityIssue
                {
                    EmployeeId = employeeId,
                    EmployeeName = name,
                    ShiftId = item.Item3.ShiftId,
                    ShiftName = item.Item3.GetLocalizedName(),
                    Date = item.Date,
                    ReasonCode = r.ReasonCode,
                    Message = r.Message ?? string.Empty
                });
            }
            return issues;
        }

        public async Task<Dictionary<string, string>> GetBlockedSlotsAsync(int employeeId, int storeId, DateTime from, DateTime to)
        {
            var result = new Dictionary<string, string>();
            var start = from.Date < DateTime.Today ? DateTime.Today : from.Date;
            var end = to.Date;
            if (end < start) return result;
            if ((end - start).TotalDays > MaxBlockedRangeDays) end = start.AddDays(MaxBlockedRangeDays);

            var shifts = await _context.Shift.AsNoTracking()
                .Where(x => x.IsActive && x.EffectiveFrom <= end && (x.EffectiveTo == null || x.EffectiveTo >= start)
                    && x.StoreShifts.Any(link => link.StoreId == storeId && link.IsActive && link.Store.IsActive))
                .ToListAsync();
            if (shifts.Count == 0) return result;

            var items = new List<(int employeeId, DateTime date, Shift shift)>();
            for (var day = start; day <= end; day = day.AddDays(1))
                foreach (var shift in shifts.Where(x => x.IsAvailableOn(day)))
                    items.Add((employeeId, day, shift));

            var checks = await _availabilityService.CheckAssignmentsAsync(items);
            foreach (var (key, check) in checks)
            {
                if (check.IsAvailable) continue;
                result[$"{key.ShiftId}_{key.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)}"] = check.Message ?? SR.Slot_BlockedBadge;
            }
            return result;
        }

        public async Task<ScheduleWarnings> GetAssignmentWarningsAsync(int employeeId, IEnumerable<(int shiftId, DateTime date)> slots)
        {
            var warnings = new ScheduleWarnings();
            foreach (var issue in await GetAvailabilityIssuesAsync(employeeId, slots))
                warnings.Availability.Add(FormatWarning(issue.EmployeeName, issue.Date, issue.ShiftName, issue.Message));
            return warnings;
        }

        public async Task<ScheduleWarnings> GetApprovalWarningsAsync(IReadOnlyCollection<int> registrationIds)
        {
            var warnings = new ScheduleWarnings();
            var ids = (registrationIds ?? Array.Empty<int>()).Distinct().ToList();
            if (ids.Count == 0) return warnings;

            var regs = await _context.ShiftRegistration.AsNoTracking()
                .Include(x => x.Shift)
                .Include(x => x.Employee).ThenInclude(e => e.Account)
                .Where(x => ids.Contains(x.ShiftRegistrationId) && x.Status == RegistrationStatus.Pending)
                .ToListAsync();
            if (regs.Count == 0) return warnings;

            var checks = await _availabilityService.CheckAssignmentsAsync(regs.Select(r => (r.EmployeeId, r.WorkDate.Date, r.Shift)));
            foreach (var r in regs.OrderBy(x => x.WorkDate).ThenBy(x => x.Shift.StartTime))
            {
                if (checks.TryGetValue((r.EmployeeId, r.WorkDate.Date, r.ShiftId), out var c) && !c.IsAvailable)
                    warnings.Availability.Add(FormatWarning(r.Employee?.Account?.FullName ?? r.Employee?.Username ?? $"#{r.EmployeeId}",
                        r.WorkDate, r.Shift.GetLocalizedName(), c.Message ?? string.Empty));
            }

            warnings.Demand.AddRange(await _demandService.GetOverDemandWarningsAsync(
                regs.Select(r => (r.StoreId, r.ShiftId, r.WorkDate.Date, 1))));
            return warnings;
        }

        public async Task<ScheduleWarnings> GetSwapApprovalWarningsAsync(IReadOnlyCollection<int> swapIds)
        {
            var warnings = new ScheduleWarnings();
            var ids = (swapIds ?? Array.Empty<int>()).Distinct().ToList();
            if (ids.Count == 0) return warnings;

            var swaps = await _context.ShiftSwapRequest.AsNoTracking()
                .Include(x => x.SourceRegistration).ThenInclude(r => r.Shift)
                .Include(x => x.TargetRegistration!).ThenInclude(r => r.Shift)
                .Include(x => x.RequesterEmployee).ThenInclude(e => e.Account)
                .Include(x => x.TargetEmployee!).ThenInclude(e => e.Account)
                .Where(x => ids.Contains(x.ShiftSwapRequestId) && x.Status == RegistrationStatus.Pending)
                .ToListAsync();
            if (swaps.Count == 0) return warnings;

            var targetShiftIds = swaps.Where(x => x.TargetShiftId.HasValue).Select(x => x.TargetShiftId!.Value).Distinct().ToList();
            var targetShifts = await _context.Shift.AsNoTracking().Where(x => targetShiftIds.Contains(x.ShiftId)).ToDictionaryAsync(x => x.ShiftId);

            var checks = new List<(int employeeId, DateTime date, Shift shift, string name)>();
            var demand = new List<(int storeId, int shiftId, DateTime date, int adding)>();
            foreach (var swap in swaps)
            {
                var source = swap.SourceRegistration;
                if (source?.Shift == null) continue;
                var requesterName = swap.RequesterEmployee?.Account?.FullName ?? swap.RequesterEmployee?.Username ?? $"#{swap.RequesterEmployeeId}";
                if (swap.TargetRegistrationId.HasValue && swap.TargetEmployeeId.HasValue)
                {
                    // Đổi với đồng nghiệp: mỗi người nhận ca của người kia; số người mỗi ca không đổi
                    var targetName = swap.TargetEmployee?.Account?.FullName ?? swap.TargetEmployee?.Username ?? $"#{swap.TargetEmployeeId}";
                    checks.Add((swap.TargetEmployeeId.Value, source.WorkDate.Date, source.Shift, targetName));
                    if (swap.TargetRegistration?.Shift != null)
                        checks.Add((swap.RequesterEmployeeId, swap.TargetRegistration.WorkDate.Date, swap.TargetRegistration.Shift, requesterName));
                }
                else if (swap.TargetShiftId.HasValue && swap.TargetWorkDate.HasValue && targetShifts.TryGetValue(swap.TargetShiftId.Value, out var targetShift))
                {
                    checks.Add((swap.RequesterEmployeeId, swap.TargetWorkDate.Value.Date, targetShift, requesterName));
                    if (targetShift.ShiftId != source.ShiftId || swap.TargetWorkDate.Value.Date != source.WorkDate.Date)
                        demand.Add((source.StoreId, targetShift.ShiftId, swap.TargetWorkDate.Value.Date, 1));
                }
            }

            if (checks.Count > 0)
            {
                var results = await _availabilityService.CheckAssignmentsAsync(checks.Select(c => (c.employeeId, c.date, c.shift)));
                foreach (var c in checks)
                {
                    if (results.TryGetValue((c.employeeId, c.date, c.shift.ShiftId), out var r) && !r.IsAvailable)
                        warnings.Availability.Add(FormatWarning(c.name, c.date, c.shift.GetLocalizedName(), r.Message ?? string.Empty));
                }
            }
            if (demand.Count > 0)
                warnings.Demand.AddRange(await _demandService.GetOverDemandWarningsAsync(demand));
            return warnings;
        }

        private static string FormatWarning(string employeeName, DateTime date, string shiftName, string message)
            => string.Format(SR.Warn_AvailabilityLine, employeeName, date.ToString("dd/MM/yyyy"), shiftName, message);

        private async Task<string> EmployeeNameAsync(int employeeId)
        {
            var row = await _context.Employee.AsNoTracking().Where(x => x.EmployeeId == employeeId)
                .Select(x => new { x.Username, FullName = x.Account.FullName }).FirstOrDefaultAsync();
            return row?.FullName ?? row?.Username ?? $"#{employeeId}";
        }

        public async Task ValidateScheduleConstraintsAsync(int employeeId, List<ShiftRegistration> pendingRegistrations, List<int>? ignoreIds = null)
        {
            if (!pendingRegistrations.Any()) return;
            var employee = await _context.Employee.AsNoTracking().Include(x => x.Account).Include(x => x.Store).SingleOrDefaultAsync(x => x.EmployeeId == employeeId);
            InputRules.Require(employee != null && employee.IsActive && employee.Account.IsActive && employee.Store != null && employee.Store.IsActive,
                "EmployeeId", "Nhân viên hoặc chi nhánh không hoạt động.", "Employee or store is inactive.");
            foreach (var reg in pendingRegistrations)
            {
                InputRules.Require(reg.EmployeeId == employeeId && reg.StoreId == employee!.StoreId && reg.WorkDate.Date >= DateTime.Today && reg.WorkDate.Date >= employee.JoinDate.Date
                    && (!employee.ResignDate.HasValue || reg.WorkDate.Date < employee.ResignDate.Value.Date),
                    "WorkDate", "Ngày làm/chi nhánh không phù hợp với nhân viên.", "Work date or store is invalid for this employee.");
                await WorkflowGuard.UnlockedPeriodAsync(_context, reg.WorkDate.Date, reg.WorkDate.Date);
            }

            var minDate = pendingRegistrations.Min(x => x.WorkDate).Date.AddDays(-1);
            var maxDate = pendingRegistrations.Max(x => x.WorkDate).Date.AddDays(1);

            var query = _context.ShiftRegistration
                .Include(x => x.Shift)
                .Where(x => x.EmployeeId == employeeId 
                         && x.WorkDate >= minDate 
                         && x.WorkDate <= maxDate
                         && x.Status != RegistrationStatus.Rejected);
                         
            if (ignoreIds != null && ignoreIds.Any())
            {
                query = query.Where(x => !ignoreIds.Contains(x.ShiftRegistrationId));
            }

            var existingRegistrations = await query.AsNoTracking().ToListAsync();

            var pendingIds = pendingRegistrations.Select(x => x.ShiftRegistrationId).Where(id => id > 0).ToList();
            var combinedSchedule = existingRegistrations.Where(x => !pendingIds.Contains(x.ShiftRegistrationId)).ToList();
            
            var shiftIds = pendingRegistrations.Select(x => x.ShiftId).Distinct().ToList();
            var shifts = await _context.Shift.AsNoTracking().Where(s => shiftIds.Contains(s.ShiftId)).ToListAsync();
            
            // Kiểm tra nhân viên có đang trong thời gian nghỉ phép đã được duyệt không
            var approvedLeaves = await _context.LeaveRequest
                .Where(l => l.EmployeeId == employeeId 
                         && l.Status == RegistrationStatus.Approved 
                         && l.StartDate <= maxDate 
                         && l.EndDate >= minDate)
                .ToListAsync();

            var proposedSchedule = new HashSet<ShiftRegistration>();
            foreach (var p in pendingRegistrations)
            {
                var leave = approvedLeaves.FirstOrDefault(l => p.WorkDate.Date >= l.StartDate.Date && p.WorkDate.Date <= l.EndDate.Date);
                if (leave != null)
                {
                    string leaveTypeStr = leave.LeaveType == LeaveType.Paid ? SR.Leave_Paid : SR.Leave_Unpaid;
                    throw new InvalidOperationException(string.Format(SR.Err_OnApprovedLeave, leaveTypeStr, p.WorkDate.ToString("dd/MM/yyyy"), leave.LeaveRequestId));
                }

                var shift = shifts.FirstOrDefault(s => s.ShiftId == p.ShiftId);
                if (shift == null || !shift.IsAvailableOn(p.WorkDate)
                    || !await _context.StoreShift.AnyAsync(x => x.ShiftId == p.ShiftId && x.StoreId == p.StoreId && x.IsActive && x.Store.IsActive))
                    throw new InvalidOperationException(SR.Err_ShiftNotAvailable);
                // Use detached comparison rows; validation must not replace tracked navigation entities.
                var candidate = new ShiftRegistration { EmployeeId = p.EmployeeId, StoreId = p.StoreId,
                    ShiftId = p.ShiftId, WorkDate = p.WorkDate, Shift = shift };
                combinedSchedule.Add(candidate);
                proposedSchedule.Add(candidate);
            }

            combinedSchedule = combinedSchedule.OrderBy(x => x.WorkDate.Date).ThenBy(x => x.Shift.StartTime).ToList();

            for (int i = 0; i < combinedSchedule.Count; i++)
            {
                var current = combinedSchedule[i];
                var currentStart = current.WorkDate.Date.Add(current.Shift.StartTime);
                var currentEnd = current.WorkDate.Date.AddDays(current.Shift.EndDayOffset).Add(current.Shift.EndTime);

                for (int j = i + 1; j < combinedSchedule.Count; j++)
                {
                    var next = combinedSchedule[j];
                    
                    if (!proposedSchedule.Contains(current) && !proposedSchedule.Contains(next))
                    {
                        continue;
                    }

                    var nextStart = next.WorkDate.Date.Add(next.Shift.StartTime);
                    var nextEnd = next.WorkDate.Date.AddDays(next.Shift.EndDayOffset).Add(next.Shift.EndTime);

                    // 1. Chống trùng ca
                    if (currentStart < nextEnd && nextStart < currentEnd)
                    {
                        throw new InvalidOperationException(string.Format(SR.Err_ShiftOverlap, current.Shift.ShiftName, current.WorkDate.ToString("dd/MM/yyyy"), next.Shift.ShiftName, next.WorkDate.ToString("dd/MM/yyyy")));
                    }
                }
            }
        }
    }
}

