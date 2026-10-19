using RWPM.Common.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Resources;

namespace RWPM.Models.ViewModels.Auth
{
    public class LoginVM
    {
        [Display(Name = "Username", ResourceType = typeof(Resources.Shared.Auth))]
        [RequiredLocalization]
        public string Username { get; set; } = string.Empty;

        [Display(Name = "Password", ResourceType = typeof(Resources.Shared.Auth))]
        [RequiredLocalization]
        public string Password { get; set; } = string.Empty;
    }
}
