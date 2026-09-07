using RWPM.Common.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Resources;

namespace RWPM.Models.ViewModels.Auth
{
    public class LoginVM
    {
        [RequiredLocalization]
        public string Username { get; set; } = string.Empty;

        [RequiredLocalization]
        public string Password { get; set; } = string.Empty;
    }
}
