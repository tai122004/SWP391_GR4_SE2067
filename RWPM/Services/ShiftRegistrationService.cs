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
                .Where(x => x.WorkDate >= start && x.WorkDate <= end);

            if (employeeId.HasValue)
            {
                query = query.Where(x => x.EmployeeId == employeeId.Value);
            }

            if (storeId.HasValue)
            {
                query = query.Where(x => x.Employee.StoreId == storeId.Value);
            }

            return await query.ToListAsync();
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

        public async Task<ShiftRegistration> UpdateAsync(ShiftRegistration entity)
        {
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
            var entity = await _context.ShiftRegistration.FindAsync(id);
            if (entity != null)
            {
                entity.Status = status;
                await _context.SaveChangesAsync();
            }
        }
    }
}
