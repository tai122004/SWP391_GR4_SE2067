using Microsoft.AspNetCore.Mvc.Rendering;
using RWPM.Common.Attributes;
using RWPM.Common;
using System.ComponentModel.DataAnnotations;
using RWPM.Common.Enums;

namespace RWPM.Models.ViewModels.Acc
{
    public class AccCreateVM
    {
        [Display(Name = "Username", ResourceType = typeof(Resources.Models.Acc))]
        [RequiredLocalization]
        [MinLengthLocalized(5)]
        [MaxLengthLocalized(30)]
        [RegexLocalized(RegexHelper.UsernameRegex, RegexHelper.UsernameRegexLocalization)]
        public string Username { get; set; } = string.Empty;

        [Display(Name = "Password", ResourceType = typeof(Resources.Models.Acc))]
        [RequiredLocalization]
        [MaxLengthLocalized(100)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Fullname", ResourceType = typeof(Resources.Models.Acc))]
        [RequiredLocalization]
        [MaxLengthLocalized(100)]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Role", ResourceType = typeof(Resources.Models.Acc))]
        [RequiredLocalization]
        [EnumDataTypeLocalization(typeof(AccountRole))]
        public AccountRole Role { get; set; } = AccountRole.SalesStaff;

        public SelectList? AccountRoleSelectList { get; set; }

        [RequiredLocalization]
        [EmailAddress(ErrorMessageResourceName = "EmailInvalid", ErrorMessageResourceType = typeof(Resources.Shared.Acc))]
        public string Email { get; set; } = string.Empty;

        // Thông tin hồ sơ nhân viên (khi chọn các vai trò làm việc tại cửa hàng)
        [Display(Name = "EmployeeID", ResourceType = typeof(Resources.Models.Employee))]
        [MaxLength(20)]
        public string? EmployeeCode { get; set; }

        [Display(Name = "Store", ResourceType = typeof(Resources.Models.Employee))]
        public int? StoreId { get; set; }

        [Display(Name = "StartDate", ResourceType = typeof(Resources.Models.Employee))]
        [DataType(DataType.Date)]
        public DateTime? JoinDate { get; set; } = DateTime.Today;

        public SelectList? StoreSelectList { get; set; }

        public AccCreateVM() { }

        public AccCreateVM(Entities.Acc entity) 
        {
            Username = entity.Username;
            FullName = entity.FullName;
            Role = entity.Role;
            Email = entity.Email;
        }

        public Entities.Acc ToEntity()
        {
            return new Entities.Acc
            {
                Username = Username,
                Password = Password,
                FullName = FullName,
                Role = Role,
                Email = Email
            };
        }
    }
}
