using RWPM.Common.Enums;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.ShiftRegistration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RWPM.Services.Abstraction
{
    public interface IAttendanceAdjustmentService
    {
        Task<AttendanceAdjustmentRequest> CreateRequestAsync(AttendanceAdjustmentCreateVM model, string username);
        Task<List<AttendanceAdjustmentRequest>> GetRequestsAsync(DateTime? startDate, DateTime? endDate, int? storeId, RegistrationStatus? status);
        Task<AttendanceAdjustmentRequest?> GetByIdAsync(int id);
        Task ApproveAsync(int id, string approverUsername, TimeSpan? finalCheckInTime = null, TimeSpan? finalCheckOutTime = null, string? approverNote = null);
        Task RejectAsync(int id, string reason, string rejectorUsername);
        Task DeleteAsync(int id, string username);
    }
}
