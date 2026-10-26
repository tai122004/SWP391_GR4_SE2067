using System.ComponentModel.DataAnnotations;
using RWPM.Resources.Shared;

namespace RWPM.Common.Enums
{
    public enum PayrollDetailStatus : byte
    {
        [Display(Name = nameof(PayrollResource.DetailStatus_Valid), ResourceType = typeof(PayrollResource))]
        Valid = 1,        // Hợp lệ, tính lương thành công

        [Display(Name = nameof(PayrollResource.DetailStatus_MissingRate), ResourceType = typeof(PayrollResource))]
        MissingRate = 2,  // Chưa có đơn giá theo giờ

        [Display(Name = nameof(PayrollResource.DetailStatus_HasWarnings), ResourceType = typeof(PayrollResource))]
        HasWarnings = 3   // Có ca chấm công bất thường / thiếu vào ra
    }
}
