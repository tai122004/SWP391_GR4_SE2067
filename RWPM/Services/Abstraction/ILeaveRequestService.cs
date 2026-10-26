using RWPM.Common.Enums;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.ShiftRegistration;

namespace RWPM.Services.Abstraction
{
    public interface ILeaveRequestService
    {
        Task<List<LeaveRequest>> GetLeaveRequestsAsync(DateTime start, DateTime end, int? storeId = null, RegistrationStatus? status = null);
        Task<LeaveRequest?> GetByIdAsync(int id);
        Task<LeaveRequest> CreateLeaveRequestAsync(LeaveRequestCreateVM model, string username);
        Task ApproveLeaveRequestAsync(int id, string approverUsername);
        Task RejectLeaveRequestAsync(int id, string? reason, string approverUsername);
        Task DeleteLeaveRequestAsync(int id, string username);
    }
}
