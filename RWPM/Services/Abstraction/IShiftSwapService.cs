using RWPM.Common.Enums;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.ShiftRegistration;

namespace RWPM.Services.Abstraction
{
    public interface IShiftSwapService
    {
        Task<List<ShiftRegistration>> GetEligibleShiftsToSwapAsync(int employeeId);
        Task<List<Employee>> GetStoreCoworkersAsync(int employeeId, int storeId);
        Task<List<ShiftRegistration>> GetCoworkerShiftsAsync(int coworkerId, DateTime? fromDate = null);
        Task<ShiftSwapRequest> CreateSwapRequestAsync(ShiftSwapCreateVM model, string username);
        Task<List<ShiftSwapRequest>> GetSwapRequestsAsync(DateTime start, DateTime end, int? storeId = null, RegistrationStatus? status = null);
        Task<ShiftSwapRequest?> GetByIdAsync(int id);
        Task ApproveSwapAsync(int id, string approverUsername);
        Task RejectSwapAsync(int id, string? reason, string approverUsername);
        Task DeleteSwapAsync(int id, string username);
    }
}
