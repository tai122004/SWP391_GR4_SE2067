using RWPM.Common.Enums;

namespace RWPM.Models.ViewModels.ShiftRegistration
{
    public class RequestItemVM
    {
        public int Id { get; set; }
        public string RequestType { get; set; } = "ShiftReg"; // "ShiftReg" | "ShiftSwap"
        public string RequestTypeName { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeCode { get; set; } = string.Empty;
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string CreatedDateStr { get; set; } = string.Empty;
        public RegistrationStatus Status { get; set; }
        public string ShiftName { get; set; } = string.Empty;
        public string WorkDateStr { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string? RejectionReason { get; set; }
        public string? ApprovedBy { get; set; }
        public string? ApprovedAtStr { get; set; }

        // Specific to ShiftSwap:
        public string? TargetEmployeeName { get; set; }
        public string? TargetShiftName { get; set; }
        public string? TargetWorkDateStr { get; set; }
        public string? SwapSummary { get; set; }

        // Specific to LeaveRequest:
        public string? StartDateStr { get; set; }
        public string? EndDateStr { get; set; }
        public LeaveType? LeaveType { get; set; }
        public string? LeaveTypeName { get; set; }
        public int LeaveDays { get; set; }
        public int AnnualLeaveBalance { get; set; }

        // Specific to AttendanceAdjustmentRequest:
        public string? ActualCheckInTimeStr { get; set; }
        public string? ActualCheckOutTimeStr { get; set; }
        public string? RequestedCheckInTimeStr { get; set; }
        public string? RequestedCheckOutTimeStr { get; set; }
        public string? AdjustmentSummary { get; set; }
    }
}
