using RWPM.Common.Attributes;
using System.ComponentModel.DataAnnotations;

namespace RWPM.Models.ViewModels.Acc
{
    public class QuickResetPasswordVM
    {
        [Display(Name = "Username", ResourceType = typeof(Resources.Models.Acc))]
        [RequiredLocalization]
        public string Username { get; set; } = string.Empty;

        [Display(Name = "NewPassword", ResourceType = typeof(Resources.Models.ChangePassword))]
        [RequiredLocalization]
        [MinLengthLocalized(6)]
        public string NewPassword { get; set; } = string.Empty;

        [Display(Name = "ConfirmNewPassword", ResourceType = typeof(Resources.Models.ChangePassword))]
        [RequiredLocalization]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
