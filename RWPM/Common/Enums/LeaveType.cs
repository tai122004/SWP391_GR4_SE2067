using System.ComponentModel.DataAnnotations;

namespace RWPM.Common.Enums
{
    public enum LeaveType
    {
        [Display(Name = "Có phép (Hưởng lương - Trừ phép năm)")]
        Paid = 0,

        [Display(Name = "Không phép (Không lương)")]
        Unpaid = 1
    }
}
