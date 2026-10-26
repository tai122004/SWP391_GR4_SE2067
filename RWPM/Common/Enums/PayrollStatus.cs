using System.ComponentModel.DataAnnotations;
using RWPM.Resources.Shared;

namespace RWPM.Common.Enums
{
    public enum PayrollStatus : byte
    {
        [Display(Name = nameof(PayrollResource.Status_Draft), ResourceType = typeof(PayrollResource))]
        Draft = 1,   // Bản nháp / Mới tính (Cho phép tính lại)

        [Display(Name = nameof(PayrollResource.Status_Locked), ResourceType = typeof(PayrollResource))]
        Locked = 2   // Đã chốt (Bất biến, không thể sửa trừ khi Unlock)
    }
}
