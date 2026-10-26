using Microsoft.AspNetCore.Mvc.Rendering;
using RWPM.Common.Attributes;
using RWPM.Common;
using System.ComponentModel.DataAnnotations;
using RWPM.Common.Enums;

namespace RWPM.Models.ViewModels.Acc
{
    public class AccCreateVM
    {
        private string _username = string.Empty;
        private string _fullName = string.Empty;
        private string _email = string.Empty;
        private string? _phoneNumber = null;
        private string? _employeeCode = null;
        private string? _citizenId = null;

        [Display(Name = "Username", ResourceType = typeof(Resources.Models.Acc))]
        [RequiredLocalization]
        [MinLengthLocalized(5)]
        [MaxLengthLocalized(30)]
        [RegexLocalized(RegexHelper.UsernameRegex, RegexHelper.UsernameRegexLocalization)]
        public string Username { get => _username; set => _username = value?.Trim() ?? string.Empty; }

        [Display(Name = "Password", ResourceType = typeof(Resources.Models.Acc))]
        [RequiredLocalization]
        [MaxLengthLocalized(100)]
        [PersonnelInput("Password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Fullname", ResourceType = typeof(Resources.Models.Acc))]
        [RequiredLocalization]
        [MaxLengthLocalized(100)]
        [PersonnelInput("Name")]
        public string FullName { get => _fullName; set => _fullName = value?.Trim() ?? string.Empty; }

        [Display(Name = "Role", ResourceType = typeof(Resources.Models.Acc))]
        [RequiredLocalization]
        [EnumDataTypeLocalization(typeof(AccountRole))]
        public AccountRole Role { get; set; } = AccountRole.Employee;

        public SelectList? AccountRoleSelectList { get; set; }

        [Display(Name = "Email", ResourceType = typeof(Resources.Models.Acc))]
        [RequiredLocalization]
        [EmailAddress(ErrorMessageResourceName = "EmailInvalid", ErrorMessageResourceType = typeof(Resources.Shared.Acc))]
        [MaxLengthLocalized(100)]
        [PersonnelInput("Email")]
        public string Email { get => _email; set => _email = value?.Trim() ?? string.Empty; }

        [Display(Name = "PhoneNumber", ResourceType = typeof(Resources.Models.Acc))]
        [MaxLengthLocalized(10)]
        [RegexLocalized(RWPM.Common.Helper.PhoneHelper.VnPhonePattern, "RegularExpression_VnPhone")]
        public string? PhoneNumber { get => _phoneNumber; set => _phoneNumber = value?.Trim(); }

        [Display(Name = "DateOfBirth", ResourceType = typeof(Resources.Models.Acc))]
        [RequiredLocalization]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [Display(Name = "Gender", ResourceType = typeof(Resources.Models.Acc))]
        [EnumDataType(typeof(Gender))]
        public Gender? Gender { get; set; }

        // Thông tin hồ sơ nhân viên (khi chọn các vai trò làm việc tại cửa hàng)
        [Display(Name = "EmployeeID", ResourceType = typeof(Resources.Models.Employee))]
        [MaxLengthLocalized(20)]
        public string? EmployeeCode { get => _employeeCode; set => _employeeCode = value?.Trim(); }

        // Số CCCD/CMND bắt buộc với mọi hồ sơ nhân viên (mọi vai trò được phép tạo đều sinh hồ sơ nhân viên)
        [Display(Name = "CitizenId", ResourceType = typeof(Resources.Models.Employee))]
        [RequiredLocalization]
        [MaxLengthLocalized(12)]
        public string? CitizenId { get => _citizenId; set => _citizenId = RWPM.Common.Validation.InputRules.NormalizeCitizenId(value); }

        [Display(Name = "Store", ResourceType = typeof(Resources.Models.Employee))]
        public int? StoreId { get; set; }

        [Display(Name = "StartDate", ResourceType = typeof(Resources.Models.Employee))]
        [DataType(DataType.Date)]
        public DateTime? JoinDate { get; set; } = DateTime.Today;

        [Display(Name = "EmploymentType", ResourceType = typeof(Resources.Models.Employee))]
        [EnumDataType(typeof(EmploymentType))]
        public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;

        [Display(Name = "HourlyRate", ResourceType = typeof(Resources.Models.Employee))]
        public decimal? HourlyRate { get; set; } = 25000;

        public SelectList? StoreSelectList { get; set; }

        public AccCreateVM() { }

        public AccCreateVM(Entities.Acc entity) 
        {
            Username = entity.Username;
            FullName = entity.FullName;
            Role = entity.Role;
            Email = entity.Email;
            PhoneNumber = entity.PhoneNumber;
            DateOfBirth = entity.DateOfBirth;
            Gender = entity.Gender;
        }

        public Entities.Acc ToEntity()
        {
            return new Entities.Acc
            {
                Username = Username,
                Password = Password,
                FullName = FullName,
                Role = Role,
                Email = Email,
                PhoneNumber = PhoneNumber,
                DateOfBirth = DateOfBirth,
                Gender = Gender
            };
        }
    }
}

