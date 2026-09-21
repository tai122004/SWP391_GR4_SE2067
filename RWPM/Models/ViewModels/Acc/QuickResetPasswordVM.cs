using System.ComponentModel.DataAnnotations;

namespace RWPM.Models.ViewModels.Acc
{
    public class QuickResetPasswordVM
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
