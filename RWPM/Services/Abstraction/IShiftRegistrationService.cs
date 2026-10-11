using RWPM.Common.Enums;
using RWPM.Models.Entities;

namespace RWPM.Services.Abstraction
{
    public interface IShiftRegistrationService
    {
        Task<List<ShiftRegistration>> GetEventsAsync(DateTime start, DateTime end, int? employeeId = null, int? storeId = null);
        Task<List<ShiftRegistration>> GetRequestsAsync(DateTime start, DateTime end, RegistrationStatus? status = null);
        Task<ShiftRegistration?> GetByIdAsync(int id);
        Task<ShiftRegistration> CreateAsync(ShiftRegistration entity);
        Task<List<ShiftRegistration>> CreateBatchAsync(List<ShiftRegistration> entities);
        Task<ShiftRegistration> UpdateAsync(ShiftRegistration entity);
        Task DeleteAsync(int id);
        Task UpdateStatusAsync(int id, RegistrationStatus status, string? rejectionReason = null, string? approver = null);
        Task UpdateBulkStatusAsync(List<int> ids, RegistrationStatus status, string? rejectionReason = null, string? approver = null);
        Task SyncWeeklyRegistrationAsync(int employeeId, int storeId, DateTime startOfWeek, List<ShiftRegistration> desiredRegistrations);
    }
}
