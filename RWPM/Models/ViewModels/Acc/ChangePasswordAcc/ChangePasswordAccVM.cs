using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using RWPM.Common.Attributes;
using RWPM.Common;
using System.ComponentModel.DataAnnotations;

namespace RWPM.Models.ViewModels.Acc.ChangePasswordAcc
{
    public class ChangePasswordAccVM
    {
        public string Username { get; set; } = string.Empty;
        public string Role {  get; set; } = string.Empty;   
        public string Dept {  get; set; } = string.Empty;  

        [Display(Name = "NewPassword", ResourceType = typeof(Resources.Models.ChangePassword))]
        [RequiredLocalization]
        [MaxLengthLocalized(100)]
        public string NewPassword { get; set; } = string.Empty;

        [Display(Name = "ConfirmNewPassword", ResourceType = typeof(Resources.Models.ChangePassword))]
        [RequiredLocalization]
        [MaxLengthLocalized(100)]
        public string ConfirmPassword { get; set; } = string.Empty;

        public ChangePasswordAccVM() { }

        public ChangePasswordAccVM(Entities.Acc acc)
        {
            ApplyEntityValue(acc);
        }

        public void ApplyEntityValue(Entities.Acc acc)
        {
            Username = acc.Username;
            Role = UIHelper.GetDisplayName(acc.Role);
            //Dept = acc.DeptCat?.Code ?? string.Empty;
        }

        public ChangePasswordAccDto ToDto()
        {
            return new ChangePasswordAccDto()
            {
                Username = Username,
                ConfirmPassword = ConfirmPassword,
                NewPassword = NewPassword,
            };
        }
    }
}
