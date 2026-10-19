using System.ComponentModel.DataAnnotations;

namespace RWPM.Common.Enums
{
    public enum LeaveType
    {
        [Display(ResourceType = typeof(Resources.Shared.RequestResource), Name = "LeaveType_Paid")]
        Paid = 0,

        [Display(ResourceType = typeof(Resources.Shared.RequestResource), Name = "LeaveType_Unpaid")]
        Unpaid = 1
    }
}
