using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Services.Abstraction;

namespace RWPM.Services
{
    public class ShiftRegistrationService : IShiftRegistrationService
    {
        private readonly DefaultDatabaseContext _context;

        public ShiftRegistrationService(DefaultDatabaseContext context)
        {
            _context = context;
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
            if (entity.WorkDate.Date < DateTime.Today)
            {
                throw new Exception("Không thể đăng ký ca cho những ngày trong quá khứ.");
            }

            await ValidateScheduleConstraintsAsync(entity.EmployeeId, new List<ShiftRegistration> { entity });

            var exists = await _context.ShiftRegistration.AnyAsync(x => 
                x.EmployeeId == entity.EmployeeId && 
                x.ShiftId == entity.ShiftId && 
                x.WorkDate == entity.WorkDate);

            if (exists)
            {
                throw new Exception("This shift is already registered for this employee on the selected date.");
            }

            _context.ShiftRegistration.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<List<ShiftRegistration>> CreateBatchAsync(List<ShiftRegistration> entities)
        {
            if (!entities.Any()) return entities;

            if (entities.Any(e => e.WorkDate.Date < DateTime.Today))
            {
                throw new Exception("Không thể đăng ký ca cho những ngày trong quá khứ.");
            }

            var employeeId = entities.First().EmployeeId;
            await ValidateScheduleConstraintsAsync(employeeId, entities);

            foreach (var entity in entities)
            {
                var exists = await _context.ShiftRegistration.AnyAsync(x => 
                    x.EmployeeId == entity.EmployeeId && 
                    x.ShiftId == entity.ShiftId && 
                    x.WorkDate == entity.WorkDate);

                if (exists)
                {
                    var shift = await _context.Shift.FindAsync(entity.ShiftId);
                    throw new Exception($"Ca làm việc {shift?.ShiftName} đã được đăng ký cho ngày {entity.WorkDate:dd/MM/yyyy}.");
                }

                _context.ShiftRegistration.Add(entity);
            }

            await _context.SaveChangesAsync();
            return entities;
        }

        public async Task<ShiftRegistration> UpdateAsync(ShiftRegistration entity)
        {
            await ValidateScheduleConstraintsAsync(entity.EmployeeId, new List<ShiftRegistration> { entity }, new List<int> { entity.ShiftRegistrationId });

            var exists = await _context.ShiftRegistration.AnyAsync(x => 
                x.EmployeeId == entity.EmployeeId && 
                x.ShiftId == entity.ShiftId && 
                x.WorkDate == entity.WorkDate && 
                x.ShiftRegistrationId != entity.ShiftRegistrationId);

            if (exists)
            {
                throw new Exception("This shift is already registered for this employee on the selected date.");
            }

            _context.ShiftRegistration.Update(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await _context.ShiftRegistration.FindAsync(id);
            if (entity != null)
            {
                _context.ShiftRegistration.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateStatusAsync(int id, RegistrationStatus status)
        {
            var entity = await _context.ShiftRegistration
                .Include(x => x.Employee)
                .ThenInclude(e => e.Account)
                .FirstOrDefaultAsync(x => x.ShiftRegistrationId == id);
                
            if (entity != null)
            {
                entity.Status = status;
                await _context.SaveChangesAsync();

                if (status == RegistrationStatus.Approved && entity.Employee?.Account?.Role == AccountRole.SalesStaff)
                {
                    await AutoScheduleForSalesStaffAsync(new List<ShiftRegistration> { entity });
                }
            }
        }

        public async Task UpdateBulkStatusAsync(List<int> ids, RegistrationStatus status)
        {
            var entities = await _context.ShiftRegistration
                .Include(x => x.Employee)
                .ThenInclude(e => e.Account)
                .Where(x => ids.Contains(x.ShiftRegistrationId))
                .ToListAsync();

            if (!entities.Any()) return;

            foreach (var entity in entities)
            {
                entity.Status = status;
            }

            await _context.SaveChangesAsync();

            if (status == RegistrationStatus.Approved)
            {
                var salesStaffRegistrations = entities
                    .Where(x => x.Employee?.Account?.Role == AccountRole.SalesStaff)
                    .ToList();

                if (salesStaffRegistrations.Any())
                {
                    await AutoScheduleForSalesStaffAsync(salesStaffRegistrations);
                }
            }
        }

        private async Task AutoScheduleForSalesStaffAsync(List<ShiftRegistration> approvedRegistrations)
        {
            var newShifts = new List<ShiftRegistration>();

            foreach (var reg in approvedRegistrations)
            {
                // Find all remaining days in the same month with the same DayOfWeek
                var endOfMonth = new DateTime(reg.WorkDate.Year, reg.WorkDate.Month, DateTime.DaysInMonth(reg.WorkDate.Year, reg.WorkDate.Month));
                var nextDate = reg.WorkDate.AddDays(7);

                while (nextDate <= endOfMonth)
                {
                    // Check if this exact shift for this employee already exists
                    var exists = await _context.ShiftRegistration.AnyAsync(x => 
                        x.EmployeeId == reg.EmployeeId && 
                        x.ShiftId == reg.ShiftId && 
                        x.WorkDate == nextDate);

                    if (!exists)
                    {
                        // To avoid adding duplicates in the same batch processing if a manager approved multiple weeks at once
                        var existsInBatch = newShifts.Any(x => 
                            x.EmployeeId == reg.EmployeeId && 
                            x.ShiftId == reg.ShiftId && 
                            x.WorkDate == nextDate);

                        if (!existsInBatch)
                        {
                            newShifts.Add(new ShiftRegistration
                            {
                                EmployeeId = reg.EmployeeId,
                                StoreId = reg.StoreId,
                                ShiftId = reg.ShiftId,
                                WorkDate = nextDate,
                                Status = RegistrationStatus.Approved,
                                CreatedDate = DateTime.Now,
                                Note = "Auto-scheduled"
                            });
                        }
                    }

                    nextDate = nextDate.AddDays(7);
                }
            }

            if (newShifts.Any())
            {
                _context.ShiftRegistration.AddRange(newShifts);
                await _context.SaveChangesAsync();
            }
        }

        public async Task SyncWeeklyRegistrationAsync(int employeeId, int storeId, DateTime startOfWeek, List<ShiftRegistration> desiredRegistrations)
        {
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

            foreach (var item in toAdd)
            {
                _context.ShiftRegistration.Add(item);
            }

            await _context.SaveChangesAsync();
        }

        private async Task ValidateScheduleConstraintsAsync(int employeeId, List<ShiftRegistration> pendingRegistrations, List<int> ignoreIds = null)
        {
            if (!pendingRegistrations.Any()) return;

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

            var existingRegistrations = await query.ToListAsync();

            var pendingIds = pendingRegistrations.Select(x => x.ShiftRegistrationId).Where(id => id > 0).ToList();
            var combinedSchedule = existingRegistrations.Where(x => !pendingIds.Contains(x.ShiftRegistrationId)).ToList();
            
            var shiftIds = pendingRegistrations.Select(x => x.ShiftId).Distinct().ToList();
            var shifts = await _context.Shift.Where(s => shiftIds.Contains(s.ShiftId)).ToListAsync();
            
            foreach (var p in pendingRegistrations)
            {
                p.Shift = shifts.FirstOrDefault(s => s.ShiftId == p.ShiftId);
                if (p.Shift != null)
                {
                    combinedSchedule.Add(p);
                }
            }

            combinedSchedule = combinedSchedule.OrderBy(x => x.WorkDate.Date).ThenBy(x => x.Shift.StartTime).ToList();

            for (int i = 0; i < combinedSchedule.Count; i++)
            {
                var current = combinedSchedule[i];
                var currentStart = current.WorkDate.Date.Add(current.Shift.StartTime);
                var currentEnd = current.Shift.EndTime < current.Shift.StartTime 
                    ? current.WorkDate.Date.AddDays(1).Add(current.Shift.EndTime) 
                    : current.WorkDate.Date.Add(current.Shift.EndTime);

                for (int j = i + 1; j < combinedSchedule.Count; j++)
                {
                    var next = combinedSchedule[j];
                    
                    if (!pendingRegistrations.Contains(current) && !pendingRegistrations.Contains(next))
                    {
                        continue;
                    }

                    var nextStart = next.WorkDate.Date.Add(next.Shift.StartTime);
                    var nextEnd = next.Shift.EndTime < next.Shift.StartTime 
                        ? next.WorkDate.Date.AddDays(1).Add(next.Shift.EndTime) 
                        : next.WorkDate.Date.Add(next.Shift.EndTime);

                    // 1. Chống trùng ca
                    if (currentStart < nextEnd && nextStart < currentEnd)
                    {
                        throw new Exception($"Trùng ca làm việc: {current.Shift.ShiftName} ({current.WorkDate:dd/MM/yyyy}) và {next.Shift.ShiftName} ({next.WorkDate:dd/MM/yyyy}).");
                    }
                }
            }
        }
    }
}
