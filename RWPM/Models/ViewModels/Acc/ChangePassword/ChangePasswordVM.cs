using RWPM.Common.Attributes;
using RWPM.Common;
using System.ComponentModel.DataAnnotations;

namespace RWPM.Models.ViewModels.Acc.ChangePassword
{
    public class ChangePasswordVM
    {
        [Display(Name = "OldPassword", ResourceType = typeof(Resources.Models.ChangePassword))]
        [RequiredLocalization]
        public string OldPassword { get; set; } = string.Empty;

        [Display(Name = "NewPassword", ResourceType = typeof(Resources.Models.ChangePassword))]
        [RequiredLocalization]
        [MaxLengthLocalized(100)]
        public string NewPassword { get; set; } = string.Empty;

        [Display(Name = "ConfirmNewPassword", ResourceType = typeof(Resources.Models.ChangePassword))]
        [RequiredLocalization]
        [MaxLengthLocalized(100)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
