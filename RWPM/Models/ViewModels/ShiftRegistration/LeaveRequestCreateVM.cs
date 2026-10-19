using RWPM.Common.Attributes;
using RWPM.Common.Enums;
using System.ComponentModel.DataAnnotations;

namespace RWPM.Models.ViewModels.ShiftRegistration
{
    public class LeaveRequestCreateVM
    {
        public int EmployeeId { get; set; }

        [RequiredLocalization]
        [Display(Name = "Leave_StartDate", ResourceType = typeof(RWPM.Resources.Shared.RequestResource))]
        public DateTime StartDate { get; set; }

        [RequiredLocalization]
        [Display(Name = "Leave_EndDate", ResourceType = typeof(RWPM.Resources.Shared.RequestResource))]
        public DateTime EndDate { get; set; }

        [RequiredLocalization]
        [Display(Name = "Leave_Type", ResourceType = typeof(RWPM.Resources.Shared.RequestResource))]
        public LeaveType LeaveType { get; set; } = LeaveType.Paid;

        [RequiredLocalization]
        [MaxLengthLocalized(500)]
        [Display(Name = "Leave_Reason", ResourceType = typeof(RWPM.Resources.Shared.RequestResource))]
        public string Reason { get; set; } = string.Empty;
    }
}
