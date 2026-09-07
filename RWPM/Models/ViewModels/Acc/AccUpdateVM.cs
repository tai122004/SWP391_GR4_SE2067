using Microsoft.AspNetCore.Mvc.Rendering;
using RWPM.Common.Attributes;
using RWPM.Common;
using System.ComponentModel.DataAnnotations;
using RWPM.Common.Enums;

namespace RWPM.Models.ViewModels.Acc
{
    public class AccUpdateVM
    {
        [Display(Name = "Username", ResourceType = typeof(Resources.Models.Acc))]
        public string Username { get; set; } = string.Empty;

        [Display(Name = "Fullname", ResourceType = typeof(Resources.Models.Acc))]
        [RequiredLocalization]
        [MaxLengthLocalized(100)]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Role", ResourceType = typeof(Resources.Models.Acc))]
        [RequiredLocalization]
        [EnumDataTypeLocalization(typeof(AccountRole))]
        public AccountRole Role { get; set; }

        //[Display(Name = "DeptCatId", ResourceType = typeof(Resources.Models.Acc))]
        //[RequiredIntegerSelection]
        //public int DeptCatId { get; set; }
        //public SelectList? DeptCatSelectList { get; set; }
        public SelectList? AccountRoleSelectList { get; set; }

        [RequiredLocalization]
        [EmailAddress(ErrorMessageResourceName = "EmailInvalid", ErrorMessageResourceType = typeof(Resources.Shared.Acc))]
        public string Email { get; set; } = string.Empty;

        public AccUpdateVM() { }
        public AccUpdateVM(Entities.Acc entity) 
        {
            Username = entity.Username;
            FullName = entity.FullName;
            Role = entity.Role;
            //DeptCatId = entity.DeptCatId ?? default;
            Email = entity.Email;
        }

        public Entities.Acc ToEntity()
        {
            return new Entities.Acc
            {
                Username = Username,
                FullName = FullName,
                Role = Role,
                //DeptCatId = this.DeptCatId,
                Email = Email,
            };
        }
    }
}
